using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Sounder_APP.Services.Mac
{
    /// <summary>
    /// macOS 音频输出设备 — 基于 afplay + 临时 WAV 文件。
    /// 实现 IWavePlayer 接口，作为 WaveOutEvent 的跨平台替代方案。
    /// </summary>
    public class MacAudioOutput : IWavePlayer, IDisposable
    {
        private const int FloatBufferSize = 16384;
        private const int ProcessExitTimeoutMs = 30000;
        private const int KillTimeoutMs = 2000;

        private IWaveProvider? _waveProvider;
        private Process? _audioProcess;
        private Thread? _playbackThread;
        private volatile bool _isRunning;
        private volatile bool _isDisposing;
        private volatile float _volume = 1.0f;
        private readonly object _stateLock = new();
        private readonly StringBuilder _stderrBuffer = new();
        private string? _tempWavPath;

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public WaveFormat? OutputWaveFormat => _waveProvider?.WaveFormat;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public MacAudioOutput()
        {
            Log($"后端选择: afplay");
        }

        public void Init(IWaveProvider waveProvider)
        {
            ArgumentNullException.ThrowIfNull(waveProvider);
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
                Name = "MacAudioOutput",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _playbackThread.Start();
        }

        public void Pause() => Stop();

        public void Stop()
        {
            lock (_stateLock)
            {
                if (PlaybackState == PlaybackState.Stopped) return;
                PlaybackState = PlaybackState.Stopped;
            }

            _isRunning = false;

            // 在后台线程中杀进程和等待，避免阻塞 UI 线程
            var process = _audioProcess;
            _audioProcess = null;
            if (process != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            try { process.StandardInput.Close(); } catch { }
                            if (!process.WaitForExit(1000))
                            {
                                process.Kill(entireProcessTree: true);
                                process.WaitForExit(2000);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        try { process.Dispose(); } catch { }
                    }
                });
            }

            _playbackThread = null;
        }

        public void Dispose()
        {
            if (_isDisposing) return;
            _isDisposing = true;
            Stop();
            CleanupTempFile();
            GC.SuppressFinalize(this);
        }

        private void KillProcess()
        {
            try
            {
                if (_audioProcess != null && !_audioProcess.HasExited)
                {
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

        private void PlaybackThreadProc()
        {
            Exception? error = null;

            try
            {
                var format = _waveProvider!.WaveFormat;
                Log($"准备播放: {format.SampleRate}Hz {format.BitsPerSample}bit {format.Channels}ch");

                // 将所有 PCM 数据写入临时 WAV 文件
                _tempWavPath = Path.Combine(Path.GetTempPath(), $"sounder_{Guid.NewGuid():N}.wav");
                WriteWavFile(_tempWavPath, format);
                Log($"WAV 文件已写入: {_tempWavPath}");

                // 使用 afplay 播放
                StartAfplayProcess(_tempWavPath);

                // 等待进程退出
                WaitForProcessExit();
                Log($"播放完成");
            }
            catch (Exception ex)
            {
                error = ex;
                Log($"播放异常: {ex.GetType().Name}: {ex.Message}");
                Log($"Stderr: {GetStderrText()}");
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

                CleanupTempFile();
            }
        }

        private void WriteWavFile(string path, WaveFormat format)
        {
            // 读取所有 PCM 数据
            var buffer = ArrayPool<byte>.Shared.Rent(FloatBufferSize);
            try
            {
                using var ms = new MemoryStream();
                bool isIeeeFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;

                while (_isRunning)
                {
                    var bytesRead = _waveProvider!.Read(buffer, 0, FloatBufferSize);
                    if (bytesRead <= 0) break;

                    if (isIeeeFloat)
                    {
                        var pcmBytes = ConvertFloatToPcm(buffer, bytesRead);
                        ms.Write(pcmBytes, 0, pcmBytes.Length);
                    }
                    else
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }
                }

                var pcmData = ms.ToArray();

                // 构建 WAV 文件头 + PCM 数据
                using var fileStream = File.Create(path);
                WriteWavHeader(fileStream, format, pcmData.Length);
                fileStream.Write(pcmData, 0, pcmData.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static void WriteWavHeader(Stream stream, WaveFormat format, int dataSize)
        {
            int channels = format.Channels;
            int sampleRate = format.SampleRate;
            int bitsPerSample = 16; // 统一输出为 16-bit PCM
            int blockAlign = channels * (bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;
            int fileSize = 36 + dataSize;

            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);            // Subchunk1Size (PCM)
            writer.Write((short)1);      // AudioFormat (PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
        }

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
                if (s > 1.0f) s = 1.0f;
                else if (s < -1.0f) s = -1.0f;
                dstSpan[i] = (short)(s * 32767f);
            }

            return pcmBytes;
        }

        private void StartAfplayProcess(string wavPath)
        {
            var psi = new ProcessStartInfo("afplay")
            {
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = null,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add(wavPath);
            psi.ArgumentList.Add("--volume");
            psi.ArgumentList.Add(Math.Round(_volume * 100).ToString());

            _audioProcess = Process.Start(psi);
            if (_audioProcess == null)
                throw new InvalidOperationException("无法启动 afplay 进程。请确保 macOS 版本正常。");

            _stderrBuffer.Clear();
            _audioProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    lock (_stderrBuffer)
                        _stderrBuffer.AppendLine(e.Data);
            };
            _audioProcess.BeginErrorReadLine();

            Log($"afplay 进程已启动 (PID={_audioProcess.Id})");
        }

        private void WaitForProcessExit()
        {
            if (_audioProcess == null) return;
            Log($"Stderr: {GetStderrText()}");

            if (_isRunning)
            {
                if (!_audioProcess.WaitForExit(ProcessExitTimeoutMs))
                {
                    Log("afplay 进程超时，强制终止");
                    _audioProcess.Kill(entireProcessTree: true);
                    _audioProcess.WaitForExit(KillTimeoutMs);
                }
                else
                {
                    Log($"afplay 退出，退出码: {_audioProcess.ExitCode}");
                }
            }
        }

        private string GetStderrText()
        {
            lock (_stderrBuffer)
                return _stderrBuffer.ToString().Trim();
        }

        private void CleanupTempFile()
        {
            try
            {
                if (_tempWavPath != null && File.Exists(_tempWavPath))
                {
                    File.Delete(_tempWavPath);
                    _tempWavPath = null;
                }
            }
            catch { }
        }

        private static void Log(string message)
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Sounder-APP", "crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath,
                    $"[MacAudioOutput] {DateTime.Now:HH:mm:ss} {message}\n");
            }
            catch { }
        }
    }
}
