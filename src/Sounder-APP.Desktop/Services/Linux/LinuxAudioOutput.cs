using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Sounder_APP.Services.Linux
{
    /// <summary>
    /// Linux 音频输出设备 — 支持 PulseAudio (pacat) 和 ALSA (aplay) 后端。
    /// 实现 IWavePlayer 接口，可作为 WaveOutEvent 的跨平台替代方案。
    ///
    /// 后端优先级：
    ///   1. PulseAudio (pacat) — 出现在音量管理器中
    ///   2. ALSA (aplay)      — 静默降级
    /// </summary>
    public class LinuxAudioOutput : IWavePlayer, IDisposable
    {
        // ==================== 常量 ====================

        /// <summary>浮点样本的缓冲区大小（字节）</summary>
        private const int FloatBufferSize = 16384;

        /// <summary>等待播放器进程退出的超时（毫秒）</summary>
        private const int ProcessExitTimeoutMs = 30000;

        /// <summary>终止进程后的等待超时（毫秒）</summary>
        private const int KillTimeoutMs = 2000;

        // ==================== 字段 ====================

        private IWaveProvider? _waveProvider;
        private Process? _audioProcess;
        private Thread? _playbackThread;
        private volatile bool _isRunning;
        private volatile bool _isDisposing;
        private volatile float _volume = 1.0f;
        private readonly object _stateLock = new();

        private readonly bool _usePulseAudio;

        // ==================== 属性 ====================

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public WaveFormat? OutputWaveFormat => _waveProvider?.WaveFormat;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        // ==================== 构造 ====================

        public LinuxAudioOutput()
        {
            // 探测可用的音频后端
            _usePulseAudio = FindExecutable("pacat") != null;
        }

        // ==================== IWavePlayer 接口 ====================

        public void Init(IWaveProvider waveProvider)
        {
            ArgumentNullException.ThrowIfNull(waveProvider);
            // NLayer 解码后输出的已经是 PCM 数据，直接使用，无需格式转换
            // （WaveFormatConversionStream 在 Linux 上会因缺少 Msacm32.dll 而崩溃）
            _waveProvider = waveProvider;
        }

        public void Play()
        {
            if (_waveProvider == null) return;

            lock (_stateLock)
            {
                if (PlaybackState == PlaybackState.Playing) return;
                PlaybackState = PlaybackState.Playing;
            }

            _isRunning = true;
            _playbackThread = new Thread(PlaybackThreadProc)
            {
                Name = "LinuxAudioOutput",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _playbackThread.Start();
        }

        public void Pause()
        {
            // Linux 上不支持暂停，通过停止实现
            Stop();
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                if (PlaybackState == PlaybackState.Stopped) return;
                PlaybackState = PlaybackState.Stopped;
            }

            _isRunning = false;

            // 终止音频进程
            KillProcess();

            // 等待播放线程退出
            if (_playbackThread?.IsAlive == true)
            {
                _playbackThread.Join(KillTimeoutMs);
            }
            _playbackThread = null;
        }

        public void Dispose()
        {
            if (_isDisposing) return;
            _isDisposing = true;
            Stop();
            GC.SuppressFinalize(this);
        }

        // ==================== 进程管理 ====================

        private void KillProcess()
        {
            try
            {
                if (_audioProcess != null && !_audioProcess.HasExited)
                {
                    // 关闭 stdin 让进程自然结束
                    try { _audioProcess.StandardInput.Close(); } catch { }
                    if (!_audioProcess.WaitForExit(1000))
                    {
                        _audioProcess.Kill(entireProcessTree: true);
                        _audioProcess.WaitForExit(KillTimeoutMs);
                    }
                }
            }
            catch { }
            finally
            {
                try { _audioProcess?.Dispose(); } catch { }
                _audioProcess = null;
            }
        }

        // ==================== 后端探测 ====================

        /// <summary>在 PATH 中查找可执行文件</summary>
        private static string? FindExecutable(string name)
        {
            try
            {
                // Linux: which
                var proc = Process.Start(new ProcessStartInfo("which")
                {
                    ArgumentList = { name },
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null)
                {
                    var path = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(1000);
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(path))
                        return path;
                }
            }
            catch { }

            // 回退：手动搜索 PATH
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':');
            foreach (var dir in paths)
            {
                var full = Path.Combine(dir.Trim(), name);
                if (File.Exists(full))
                    return full;
            }
            return null;
        }

        // ==================== IeeeFloat → S16_LE 转换 ====================

        /// <summary>将 IEEE Float 缓冲区转换为 16-bit PCM 缓冲区</summary>
        private static byte[] ConvertFloatToPcm(byte[] floatBuffer, int bytesRead)
        {
            int sampleCount = bytesRead / 4;
            int pcmByteCount = sampleCount * 2;
            var pcmBytes = new byte[pcmByteCount];

            var srcSpan = MemoryMarshal.Cast<byte, float>(floatBuffer.AsSpan(0, bytesRead));
            var dstSpan = MemoryMarshal.Cast<byte, short>(pcmBytes.AsSpan(0, pcmByteCount));

            for (int i = 0; i < sampleCount; i++)
            {
                float s = srcSpan[i];
                // 钳位到 [-1.0, 1.0]
                if (s > 1.0f) s = 1.0f;
                else if (s < -1.0f) s = -1.0f;
                dstSpan[i] = (short)(s * 32767f);
            }

            return pcmBytes;
        }

        // ==================== 播放线程 ====================

        private void PlaybackThreadProc()
        {
            Exception? error = null;

            try
            {
                var format = _waveProvider!.WaveFormat;
                bool isIeeeFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;

                StartAudioProcess(format.SampleRate, format.Channels);

                var outputStream = _audioProcess!.StandardInput.BaseStream;
                int totalBytesWritten = 0;

                if (isIeeeFloat)
                {
                    // IEEE Float (32-bit) → S16_LE (16-bit) 实时转换
                    var floatBuffer = ArrayPool<byte>.Shared.Rent(FloatBufferSize);
                    try
                    {
                        while (_isRunning)
                        {
                            var bytesRead = _waveProvider.Read(floatBuffer, 0, FloatBufferSize);
                            if (bytesRead <= 0) break;

                            var pcmBytes = ConvertFloatToPcm(floatBuffer, bytesRead);
                            outputStream.Write(pcmBytes, 0, pcmBytes.Length);
                            outputStream.Flush();
                            totalBytesWritten += pcmBytes.Length;
                        }
                    }
                    finally
                    {
                        // 注意：ArrayPool 不能归还为 null，但 floatBuffer 始终非 null
                        ArrayPool<byte>.Shared.Return(floatBuffer);
                    }
                }
                else
                {
                    // 已经是 16-bit PCM，直接写入
                    var buffer = ArrayPool<byte>.Shared.Rent(FloatBufferSize);
                    try
                    {
                        while (_isRunning)
                        {
                            var bytesRead = _waveProvider.Read(buffer, 0, FloatBufferSize);
                            if (bytesRead <= 0) break;

                            outputStream.Write(buffer, 0, bytesRead);
                            outputStream.Flush();
                            totalBytesWritten += bytesRead;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                // 关闭输入流，让播放器进程自然结束
                CloseInputStream();

                // 等待进程退出
                WaitForProcessExit();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                KillProcess();

                lock (_stateLock)
                {
                    if (_isRunning || PlaybackState == PlaybackState.Playing)
                    {
                        PlaybackState = PlaybackState.Stopped;
                        _isRunning = false;

                        try
                        {
                            var args = error != null
                                ? new StoppedEventArgs(error)
                                : new StoppedEventArgs();
                            PlaybackStopped?.Invoke(this, args);
                        }
                        catch { }
                    }
                }
            }
        }

        // ==================== 音频进程启动 ====================

        private void StartAudioProcess(int sampleRate, int channels)
        {
            // 优先 PulseAudio，失败时自动降级到 ALSA
            if (_usePulseAudio)
            {
                try
                {
                    StartPacatProcess(sampleRate, channels);
                    return;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[LinuxAudioOutput] PulseAudio 不可用: {ex.Message}");
                }
            }
            StartAplayProcess(sampleRate, channels);
        }

        private void StartPacatProcess(int sampleRate, int channels)
        {
            var psi = new ProcessStartInfo("pacat")
            {
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--raw");
            psi.ArgumentList.Add("--format=s16le");
            psi.ArgumentList.Add("--rate=" + sampleRate);
            psi.ArgumentList.Add("--channels=" + channels);
            psi.ArgumentList.Add("--stream-name=发声APP");
            psi.ArgumentList.Add("--latency-msec=50");

            _audioProcess = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 pacat 进程");
        }

        private void StartAplayProcess(int sampleRate, int channels)
        {
            var psi = new ProcessStartInfo("aplay")
            {
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add("raw");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("S16_LE");
            psi.ArgumentList.Add("-r");
            psi.ArgumentList.Add(sampleRate.ToString());
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(channels.ToString());
            psi.ArgumentList.Add("-D");
            psi.ArgumentList.Add("default");

            _audioProcess = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 aplay 进程");
        }

        // ==================== 进程收尾 ====================

        private void CloseInputStream()
        {
            try
            {
                if (_audioProcess != null && !_audioProcess.HasExited)
                {
                    _audioProcess.StandardInput.Flush();
                    _audioProcess.StandardInput.Close();
                }
            }
            catch { }
        }

        private void WaitForProcessExit()
        {
            if (_audioProcess == null) return;
            if (_isRunning)
            {
                if (!_audioProcess.WaitForExit(ProcessExitTimeoutMs))
                    _audioProcess.Kill(entireProcessTree: true);
            }
        }
    }
}
