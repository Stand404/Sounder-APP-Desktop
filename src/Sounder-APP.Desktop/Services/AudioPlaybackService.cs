using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using Sounder_APP.Models;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 音频播放管理器 — 支持并发多实例 + 对象池（参照 Android AudioPlayerManager）
    /// 平台特定实现（播放器创建、解码器选择）委托给 <see cref="IAudioBackend"/>。
    /// </summary>
    public class AudioPlaybackService : IDisposable
    {
        private class PlaybackInstance
        {
            public string InstanceId { get; set; } = string.Empty;
            public string AudioId { get; set; } = string.Empty;
            public IWavePlayer Player { get; set; } = null!;
            public WaveStream Reader { get; set; } = null!;
        }

        private readonly IAudioBackend _backend;
        private readonly Dictionary<string, PlaybackInstance> _activeInstances = new();
        private readonly Stack<IWavePlayer> _playerPool = new();
        private const int MaxPoolSize = 4;
        private long _instanceCounter;
        private bool _isDisposed;

        private static readonly string CacheDir;
        private static readonly HttpClient Client = new();

        public AudioPlaybackService()
        {
            _backend = BackendFactory.CreateAudioBackend();
        }

        /// <summary>
        /// 预加载平台音频类型，防止裁剪后运行时找不到类型。
        /// 在 App 启动时调用。
        /// </summary>
        public static void Warmup()
        {
            try
            {
                var backend = BackendFactory.CreateAudioBackend();
                backend.Warmup();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Audio Warmup FAILED] {ex}");
            }
        }

        /// <summary>实例开始播放回调（参数：instanceId）</summary>
        public event Action<string>? PlaybackStarted;
        /// <summary>实例播放停止回调（参数：instanceId）</summary>
        public event Action<string>? PlaybackStopped;

        static AudioPlaybackService()
        {
            CacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sounder-APP", "cache");
            Directory.CreateDirectory(CacheDir);
        }

        /// <summary>是否有任何实例正在播放</summary>
        public bool IsAnyPlaying
        {
            get
            {
                foreach (var kv in _activeInstances)
                    if (kv.Value.Player.PlaybackState == PlaybackState.Playing)
                        return true;
                return false;
            }
        }

        /// <summary>指定音频 ID 是否有活跃实例</summary>
        public bool IsPlaying(string audioId)
        {
            foreach (var kv in _activeInstances)
                if (kv.Value.AudioId == audioId)
                    return true;
            return false;
        }

        /// <summary>获取所有正在播放的音频 ID</summary>
        public HashSet<string> GetPlayingAudioIds()
        {
            var ids = new HashSet<string>();
            foreach (var kv in _activeInstances)
                if (kv.Value.Player.PlaybackState == PlaybackState.Playing)
                    ids.Add(kv.Value.AudioId);
            return ids;
        }

        /// <summary>获取磁盘缓存目录路径</summary>
        public static string GetCacheDirectory() => CacheDir;

        /// <summary>计算磁盘缓存总大小（字节）</summary>
        public static long GetCacheSize()
        {
            try
            {
                if (!Directory.Exists(CacheDir)) return 0;
                return Directory.EnumerateFiles(CacheDir).Sum(f => new FileInfo(f).Length);
            }
            catch { return 0; }
        }

        /// <summary>缓存中已存在的音频数量</summary>
        public static int GetCachedCount()
        {
            try
            {
                return Directory.Exists(CacheDir)
                    ? Directory.GetFiles(CacheDir).Length
                    : 0;
            }
            catch { return 0; }
        }

        /// <summary>清除所有缓存</summary>
        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDir))
                    Directory.Delete(CacheDir, recursive: true);
                Directory.CreateDirectory(CacheDir);
            }
            catch { }
        }

        private static string GetCachePath(string url)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(url));
            var ext = GetExtensionFromUrl(url);
            return Path.Combine(CacheDir, Convert.ToHexString(hash).ToLowerInvariant() + ext);
        }

        private static string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            var path = url.Split('?')[0];
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext) && ext.Length <= 8)
                return ext.ToLowerInvariant();
            return ".mp3";
        }

        /// <summary>
        /// 下载并缓存音频文件（若未缓存）。如果是本地文件路径则直接返回。
        /// </summary>
        public async Task<string> EnsureCachedAsync(string url)
        {
            // 本地文件路径 → 直接使用
            if (File.Exists(url))
                return url;

            var cachePath = GetCachePath(url);
            if (!File.Exists(cachePath))
            {
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync()
                    .ConfigureAwait(false);
                File.WriteAllBytes(cachePath, bytes);
            }
            return cachePath;
        }

        /// <summary>
        /// 播放音频。根据 playMode 决定并发/替换/循环行为。
        /// 返回 instanceId，可用于后续停止指定实例。
        /// </summary>
        public async Task<string?> PlayAsync(string url, string audioId, PlayMode mode)
        {
            try
            {
                // 替换/循环模式：先停止所有
                if (mode == PlayMode.Replace || mode == PlayMode.Loop)
                    StopAll();

                // 确保缓存
                var localPath = await EnsureCachedAsync(url);

                var reader = _backend.CreateReader(localPath);
                var player = ObtainPlayer();
                var instanceId = $"{audioId}_{++_instanceCounter}";

                var inst = new PlaybackInstance
                {
                    InstanceId = instanceId,
                    AudioId = audioId,
                    Player = player,
                    Reader = reader
                };

                player.PlaybackStopped += (_, args) => OnInstanceStopped(instanceId, args.Exception, audioId);

                player.Init(reader);
                player.Play();

                lock (_activeInstances)
                {
                    _activeInstances[instanceId] = inst;
                }

                PlaybackStarted?.Invoke(instanceId);
                return instanceId;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PlayAsync] 错误: {ex}");

                PlaybackStopped?.Invoke("error_" + audioId);
                return null;
            }
        }

        /// <summary>循环播放：播放结束后重新 seek 到开头</summary>
        public async Task<string?> PlayLoopAsync(string url, string audioId)
        {
            try
            {
                StopAll();
                var localPath = await EnsureCachedAsync(url);

                var reader = _backend.CreateReader(localPath);
                var player = ObtainPlayer();
                var instanceId = $"{audioId}_{++_instanceCounter}";

                var inst = new PlaybackInstance
                {
                    InstanceId = instanceId,
                    AudioId = audioId,
                    Player = player,
                    Reader = reader
                };

                player.PlaybackStopped += (_, args) =>
                {
                    try
                    {
                        // 检查实例是否仍活跃（防止 StopAll 已清理后的事件触发）
                        lock (_activeInstances)
                        {
                            if (!_activeInstances.ContainsKey(instanceId))
                                return;
                        }
                        // 循环模式：seek 到开头重新播放
                        inst.Reader.Position = 0;
                        inst.Player.Play();
                        // 重新通知 PlaybackStarted，让 ViewModel 将状态从 Loading 切换回 Playing
                        PlaybackStarted?.Invoke(instanceId);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[PlayLoopAsync callback FAILED] instanceId={instanceId}: {ex}");
                        OnInstanceStopped(instanceId, args.Exception ?? ex, audioId);
                    }
                };

                player.Init(reader);
                player.Play();

                lock (_activeInstances)
                {
                    _activeInstances[instanceId] = inst;
                }

                PlaybackStarted?.Invoke(instanceId);
                return instanceId;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PlayLoopAsync] 错误: {ex}");

                PlaybackStopped?.Invoke("error_" + audioId);
                return null;
            }
        }

        /// <summary>停止指定 instanceId 的播放</summary>
        public void StopInstance(string instanceId)
        {
            lock (_activeInstances)
            {
                if (!_activeInstances.TryGetValue(instanceId, out var inst)) return;
                _activeInstances.Remove(instanceId);
                ReleaseInstance(inst);
            }
        }

        /// <summary>停止指定音频 ID 的所有播放实例</summary>
        public void StopAudio(string audioId)
        {
            lock (_activeInstances)
            {
                var toRemove = new List<string>();
                foreach (var kv in _activeInstances)
                    if (kv.Value.AudioId == audioId)
                        toRemove.Add(kv.Key);

                foreach (var id in toRemove)
                {
                    if (_activeInstances.TryGetValue(id, out var inst))
                    {
                        _activeInstances.Remove(id);
                        ReleaseInstance(inst);
                    }
                }
            }
        }

        /// <summary>停止所有播放</summary>
        public void StopAll()
        {
            lock (_activeInstances)
            {
                foreach (var inst in _activeInstances.Values)
                    ReleaseInstance(inst);
                _activeInstances.Clear();
            }
        }

        // ===== 对象池 =====

        private IWavePlayer ObtainPlayer()
        {
            lock (_playerPool)
            {
                if (_playerPool.Count > 0)
                    return _playerPool.Pop();
            }
            return _backend.CreatePlayer();
        }

        private void RecyclePlayer(IWavePlayer player)
        {
            try
            {
                if (player.PlaybackState == PlaybackState.Playing)
                    player.Stop();
            }
            catch { }

            lock (_playerPool)
            {
                if (_playerPool.Count < MaxPoolSize)
                    _playerPool.Push(player);
                else
                    player.Dispose();
            }
        }

        private void ReleaseInstance(PlaybackInstance inst)
        {
            try { inst.Reader.Dispose(); } catch { }
            RecyclePlayer(inst.Player);
        }

        private void OnInstanceStopped(string instanceId, Exception? playbackError = null, string? audioId = null)
        {
            if (playbackError != null)
            {
                Console.Error.WriteLine($"[OnInstanceStopped ERROR] instanceId={instanceId}, audioId={audioId}: {playbackError}");
            }

            bool wasActive;
            lock (_activeInstances)
            {
                wasActive = _activeInstances.TryGetValue(instanceId, out var inst);
                if (wasActive)
                {
                    _activeInstances.Remove(instanceId);
                    ReleaseInstance(inst!);
                }
            }
            // 只有实例确实在活跃列表时才通知，防止因播放器对象池复用时
            // 残留的旧 PlaybackStopped 事件处理函数导致 ViewModel 状态提前释放
            if (wasActive)
            {
                try
                {
                    // 播放出错时，使用 error_ + audioId 前缀通知 ViewModel 展示错误提示
                    // ViewModel 端 OnPlaybackStopped 接收 "error_{audioId}" 格式
                    var eventId = playbackError != null ? "error_" + (audioId ?? instanceId) : instanceId;
                    PlaybackStopped?.Invoke(eventId);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[OnInstanceStopped event FAILED] instanceId={instanceId}: {ex}");
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopAll();
            lock (_playerPool)
            {
                while (_playerPool.Count > 0)
                    _playerPool.Pop().Dispose();
            }
        }
    }
}
