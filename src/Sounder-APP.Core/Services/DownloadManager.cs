using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sounder_APP.Models;

namespace Sounder_APP.Services
{
    /// <summary>全局下载状态（参照 Android DownloadStatus）</summary>
    public enum DownloadStatus { Idle, Downloading, Paused, Completed, Failed }

    /// <summary>全局下载快照，供 UI 和 ViewModel 读取（参照 Android DownloadState）</summary>
    public class DownloadState
    {
        public string ResourceId { get; set; } = string.Empty;
        public DownloadStatus Status { get; set; } = DownloadStatus.Idle;
        public float Progress { get; set; }
    }

    /// <summary>详细的下载进度（参照 Android DownloadProgress）</summary>
    public class DownloadProgressInfo
    {
        public string ResourceId { get; set; } = string.Empty;
        public long BytesWritten { get; set; }
        public long TotalBytes { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFailed { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public float Progress => TotalBytes > 0
            ? Math.Clamp((float)BytesWritten / TotalBytes, 0f, 1f)
            : 0f;
    }

    /// <summary>持久化下载进度记录（.download 文件序列化模型，参照 Android DownloadRecord）</summary>
    public class DownloadRecordData
    {
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public int TotalAudioCount { get; set; }
        public List<DownloadedAudioItemData> DownloadedAudioItems { get; set; } = new();
        public bool IconDownloaded { get; set; }
        public DateTime LastUpdateTime { get; set; }
        /// <summary>远程音频列表 JSON（含原始 URL），暂停后继续时无需重新请求远程详情（参照 Android remoteAudioJson）</summary>
        public string? RemoteAudioJson { get; set; }

        /// <summary>从缓存记录重建 Resource 详情，暂停后继续时无需重新请求 API（参照 Android toRemoteResource()）</summary>
        public Resource? ToResource()
        {
            if (string.IsNullOrEmpty(RemoteAudioJson)) return null;
            var audioItems = JsonSerializer.Deserialize(RemoteAudioJson, SettingsJsonContext.Default.ListAudioItem);
            if (audioItems == null || audioItems.Count == 0) return null;

            return new Resource
            {
                Id = ResourceId,
                Name = ResourceName,
                DisplayName = DisplayName,
                Description = Description,
                Icon = Icon,
                Size = Size,
                PublishDate = PublishDate,
                AudioItems = audioItems,
                Source = ResourceSource.Local,
                IsInstalled = true
            };
        }
    }

    public class DownloadedAudioItemData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Src { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public int OrderIndex { get; set; }
    }

    /// <summary>
    /// 下载管理器 — 负责资源离线安装（下载音频+图标，带实时进度 / 暂停 / 取消）。
    /// 参照 Android DownloadManager.kt 设计。
    /// </summary>
    public class DownloadManager : IDisposable
    {
        private readonly ResourceService _resourceService;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        /// <summary>资源安装根目录: %LOCALAPPDATA%\Sounder-APP\installed\{resourceId}\</summary>
        public static readonly string InstallRoot;

        /// <summary>统一缓存目录（与 AudioPlaybackService 共享）</summary>
        public static readonly string AudioCacheDir;

        /// <summary>状态变更事件（供 ViewModel 实时观察）</summary>
        public event Action<DownloadState>? StateChanged;

        static DownloadManager()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            InstallRoot = Path.Combine(localAppData, "Sounder-APP", "installed");
            AudioCacheDir = Path.Combine(localAppData, "Sounder-APP", "cache");
            Directory.CreateDirectory(InstallRoot);
        }

        public DownloadManager(ResourceService resourceService)
        {
            _resourceService = resourceService;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Sounder-APP/1.0 (Windows; .NET 10; Avalonia)");
        }

        // ==================== 全局状态管理 ====================

        private readonly ConcurrentDictionary<string, DownloadState> _states = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancelTokens = new();
        private readonly ConcurrentDictionary<string, Action<DownloadProgressInfo>> _progressListeners = new();
        /// <summary>启动守卫：防止同一资源重复并发下载（参照 Android DownloadManager.kt running map）</summary>
        private readonly ConcurrentDictionary<string, bool> _runningTasks = new();

        /// <summary>获取资源的全局下载状态</summary>
        public DownloadState? GetState(string resourceId) =>
            _states.TryGetValue(resourceId, out var state) ? state : null;

        /// <summary>资源是否正在下载或已暂停</summary>
        public bool IsDownloadActive(string resourceId)
        {
            if (!_states.TryGetValue(resourceId, out var state)) return false;
            return state.Status is DownloadStatus.Downloading or DownloadStatus.Paused;
        }

        /// <summary>资源下载是否处于暂停状态</summary>
        public bool IsDownloadPaused(string resourceId)
        {
            if (!_states.TryGetValue(resourceId, out var state)) return false;
            return state.Status == DownloadStatus.Paused;
        }

        /// <summary>恢复资源的内存状态为已暂停（从 .download 缓存重建，供页面刷新后按钮状态恢复）</summary>
        public void SetStatePaused(string resourceId, float progress = 0f)
        {
            if (!_cancelTokens.ContainsKey(resourceId))
                _cancelTokens[resourceId] = new CancellationTokenSource();
            UpdateState(resourceId, DownloadStatus.Paused, progress);
        }

        /// <summary>检查 InstallResourceByIdAsync 是否正在运行（防止并发点击进入空闲分支）</summary>
        public bool IsDownloadRunning(string resourceId) => _runningTasks.ContainsKey(resourceId);

        /// <summary>注册进度回调</summary>
        public void RegisterProgressListener(string resourceId, Action<DownloadProgressInfo> listener) =>
            _progressListeners[resourceId] = listener;

        /// <summary>取消注册进度回调</summary>
        public void UnregisterProgressListener(string resourceId) =>
            _progressListeners.TryRemove(resourceId, out _);

        /// <summary>暂停下载</summary>
        public void PauseDownload(string resourceId)
        {
            if (!_states.TryGetValue(resourceId, out var state)) return;
            if (state.Status != DownloadStatus.Downloading) return;

            CancelToken(resourceId);
            UpdateState(resourceId, DownloadStatus.Paused, state.Progress);
            Debug.WriteLine($"[DownloadManager] 已暂停: id={resourceId}");
        }

        /// <summary>清理下载状态（卸载资源时调用）</summary>
        public void Cleanup(string resourceId)
        {
            CancelToken(resourceId);
            _states.TryRemove(resourceId, out _);
            _progressListeners.TryRemove(resourceId, out _);
            Debug.WriteLine($"[DownloadManager] 已清理下载状态: id={resourceId}");
        }

        /// <summary>获取所有活跃下载的 ID</summary>
        public System.Collections.Generic.IEnumerable<string> GetActiveDownloadIds() =>
            _states.Where(kv => kv.Value.Status is DownloadStatus.Downloading or DownloadStatus.Paused)
                   .Select(kv => kv.Key);

        // ==================== 安装流程（参照 Android installResourceById） ====================

        /// <summary>
        /// 安装资源：获取详情 → 保存元数据 → 下载图标 → 下载音频 → 更新本地路径。
        /// 支持通过 CancellationToken 取消/暂停。
        /// existingResource 为可选参数，传入时跳过网络请求；
        /// 否则优先从 .download 缓存重建详情，仅首次下载时请求远程 API。
        /// （参照 Android installResourceById 三级来源：existingResource / toRemoteResource / network）
        /// </summary>
        /// <param name="resourceId">资源 ID</param>
        /// <param name="progress">外部进度报告（可选）</param>
        /// <param name="existingResource">已有资源详情（可选），传入则跳过网络请求和 .download 缓存重建</param>
        /// <returns>安装是否成功</returns>
        public async Task<bool> InstallResourceByIdAsync(
            string resourceId,
            IProgress<float>? progress = null,
            Resource? existingResource = null)
        {
            // 启动守卫：防止同一资源重复并发下载（参照 Android DownloadManager.kt）
            if (!_runningTasks.TryAdd(resourceId, true))
            {
                Debug.WriteLine($"[DownloadManager] 资源已在下载中，忽略重复请求: id={resourceId}");
                return false;
            }

            var cts = new CancellationTokenSource();
            _cancelTokens[resourceId] = cts;
            var token = cts.Token;

            UpdateState(resourceId, DownloadStatus.Downloading, 0f);

            try
            {
                // 1. 获取资源详情（参照 Android: existingResource ?: toRemoteResource() ?: repo.getRemoteResourceDetail）
                Resource? detail = null;

                if (existingResource != null)
                {
                    detail = existingResource;
                    Debug.WriteLine($"[DownloadManager] 使用传入的资源详情: {detail.DisplayName}");
                }
                else
                {
                    var cachedRecord = ReadDownloadRecord(resourceId);
                    detail = cachedRecord?.ToResource();
                    if (detail != null)
                    {
                        Debug.WriteLine($"[DownloadManager] 从 .download 缓存恢复详情: {detail.DisplayName}");
                    }
                }

                if (detail == null)
                {
                    Debug.WriteLine($"[DownloadManager] >>> 从网络获取资源详情: id={resourceId}");
                    detail = await _resourceService.GetResourceDetailAsync(resourceId);
                    if (detail == null)
                    {
                        Debug.WriteLine($"[DownloadManager] 获取资源详情失败: id={resourceId}");
                        UpdateState(resourceId, DownloadStatus.Failed, 0f);
                        return false;
                    }

                    Debug.WriteLine($"[DownloadManager] 获取详情成功: {detail.DisplayName}, 音频文件数={detail.AudioItems.Count}");
                }

                // 确保 .download 中包含远程音频列表 JSON（含原始 URL），暂停后继续时无需重新请求 API
                // 首次调用会新建 .download 文件，后续调用已存在则仅补充 RemoteAudioJson
                EnsureRemoteAudioJsonInRecord(detail);

                // 2. 创建资源文件夹 + 标记为本地资源（resource.json 等音频下载完成后再保存）
                detail.IsInstalled = true;
                detail.Source = ResourceSource.Local;

                Debug.WriteLine($"[DownloadManager] 资源文件夹已创建: {InstallRoot}/{resourceId}");

                var audioDir = Path.Combine(InstallRoot, resourceId, "audio");
                Directory.CreateDirectory(audioDir);

                var audioCount = detail.AudioItems.Count;
                var hasIcon = !string.IsNullOrEmpty(detail.Icon) && !detail.Icon.StartsWith("file://");
                var totalTasks = audioCount + (hasIcon ? 1 : 0);
                if (totalTasks == 0) totalTasks = 1; // 避免除零

                // 3. 图标优先下载（小文件、速度快），参照 Android DownloadManager.kt
                if (hasIcon)
                {
                    token.ThrowIfCancellationRequested();

                    var localIcon = await DownloadIconAsync(resourceId, detail.Icon, token);
                    token.ThrowIfCancellationRequested();

                    if (localIcon != null)
                    {
                        // 复制到资源自身文件夹，detail.Icon 记录自身路径 installed/{resourceId}/icon.{ext}
                        var resourceFolderIcon = ResourceService.SaveIconToResourceFolder(resourceId, localIcon);
                        detail.Icon = resourceFolderIcon ?? localIcon;
                        Debug.WriteLine($"[DownloadManager] 图标已下载: {detail.Icon}");
                    }
                    else
                    {
                        Debug.WriteLine($"[DownloadManager] 图标下载失败: url={detail.Icon}");
                    }

                    var iconProgress = 1f / totalTasks;
                    progress?.Report(iconProgress);
                    UpdateState(resourceId, DownloadStatus.Downloading, iconProgress);
                }

                token.ThrowIfCancellationRequested();

                // 4. 逐个下载音频文件，每完成一个就增量保存到 .download
                var iconOffset = hasIcon ? 1f / totalTasks : 0f;
                var updatedAudioList = await DownloadAudioFilesAsync(
                    resourceId, detail.AudioItems, audioDir, totalTasks, token, progress, detail, iconOffset);

                token.ThrowIfCancellationRequested();
                detail.AudioItems = updatedAudioList;

                if (!hasIcon)
                {
                    UpdateState(resourceId, DownloadStatus.Downloading, 1f);
                    progress?.Report(1f);
                }

                // 5. 所有文件下载完成，保存 resource.json（最终产物），清理 .download
                ResourceService.SaveResourceJson(detail);
                DeleteDownloadRecord(resourceId);
                Debug.WriteLine($"[DownloadManager] resource.json 已保存（共 {updatedAudioList.Count} 个音频），.download 已清理");

                // 6. 标记安装完成
                UpdateState(resourceId, DownloadStatus.Completed, 1f);
                _cancelTokens.TryRemove(resourceId, out _);

                int localCount = updatedAudioList.Count;
                Debug.WriteLine($"[DownloadManager] <<< 安装完成: {localCount} 个本地文件, 图标={(hasIcon ? "已下载" : "无")}");
                return true;
            }
            catch (OperationCanceledException)
            {
                // 被暂停/取消，保持当前状态
                if (_states.TryGetValue(resourceId, out var st) &&
                    st.Status != DownloadStatus.Paused &&
                    st.Status != DownloadStatus.Completed)
                {
                    UpdateState(resourceId, DownloadStatus.Paused, st.Progress);
                }
                Debug.WriteLine($"[DownloadManager] 下载已暂停/取消: id={resourceId}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadManager] 安装异常: {ex.GetType().Name}: {ex.Message}");
                UpdateState(resourceId, DownloadStatus.Failed, 0f);
                return false;
            }
            finally
            {
                _cancelTokens.TryRemove(resourceId, out _);
                _runningTasks.TryRemove(resourceId, out _);
            }
        }

        // ==================== 音频批量下载 ====================

        /// <summary>下载指定资源的所有音频文件，支持从 .download 断点续传，每完成一个就持久化进度</summary>
        private async Task<List<AudioItem>> DownloadAudioFilesAsync(
            string resourceId,
            List<AudioItem> audioList,
            string audioDir,
            int totalTasks,
            CancellationToken token,
            IProgress<float>? progress,
            Resource detail,
            float baseOffset = 0f)
        {
            if (audioList.Count == 0) return new List<AudioItem>();

            var results = new List<AudioItem>();

            // 尝试从 .download 恢复已下载的音频项
            var recoveredSet = new HashSet<string>();
            var record = ReadDownloadRecord(resourceId);
            if (record?.DownloadedAudioItems != null)
            {
                foreach (var downloaded in record.DownloadedAudioItems)
                {
                    var localFile = Path.Combine(audioDir, $"audio_{downloaded.OrderIndex + 1}{Path.GetExtension(downloaded.Src)}");
                    if (File.Exists(localFile))
                    {
                        results.Add(new AudioItem
                        {
                            Id = downloaded.Id,
                            Name = downloaded.Name,
                            Src = localFile,
                            DurationMs = downloaded.DurationMs,
                            OrderIndex = downloaded.OrderIndex
                        });
                        recoveredSet.Add(downloaded.Id);
                        Debug.WriteLine($"[DownloadManager]   [断点续传] 已恢复: {downloaded.Name} ({localFile})");
                    }
                    else if (File.Exists(downloaded.Src))
                    {
                        results.Add(new AudioItem
                        {
                            Id = downloaded.Id,
                            Name = downloaded.Name,
                            Src = downloaded.Src,
                            DurationMs = downloaded.DurationMs,
                            OrderIndex = downloaded.OrderIndex
                        });
                        recoveredSet.Add(downloaded.Id);
                        Debug.WriteLine($"[DownloadManager]   [断点续传] 已恢复: {downloaded.Name} ({downloaded.Src})");
                    }
                }
            }

            var recoveredCount = results.Count;
            var startProgress = baseOffset + (audioList.Count > 0
                ? (float)recoveredCount / totalTasks
                : 0f);

            // 新处理（非恢复）的计数，用于正确计算进度
            int newlyProcessed = 0;

            for (int i = 0; i < audioList.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var audio = audioList[i];

                // 已从 .download 恢复的跳过
                if (recoveredSet.Contains(audio.Id))
                    continue;

                // 从 URL 提取扩展名
                var ext = GetExtensionFromUrl(audio.Src);
                var filename = $"audio_{i + 1}{ext}";
                var localFile = Path.Combine(audioDir, filename);

                // 如果文件已存在（无后缀名兜底检测），跳过下载
                var existingFile = FindExistingAudioFile(audioDir, i + 1);
                if (existingFile != null)
                {
                    Debug.WriteLine($"[DownloadManager]   [{i + 1}/{audioList.Count}] 文件已存在, 跳过: {audio.Name} ({existingFile})");
                    results.Add(new AudioItem
                    {
                        Id = audio.Id,
                        Name = audio.Name,
                        Src = existingFile,
                        DurationMs = audio.DurationMs,
                        OrderIndex = audio.OrderIndex
                    });

                    // 持久化进度到 .download
                    SaveDownloadRecord(detail, results, false);

                    newlyProcessed++;
                    var p = startProgress + (float)newlyProcessed / totalTasks;
                    progress?.Report(p);
                    UpdateState(resourceId, DownloadStatus.Downloading, p);
                    continue;
                }

                var audioUrl = audio.Src;
                bool success;

                // 先检查音频缓存中是否已有该 URL 对应的文件
                var cachePath = GetAudioCachePath(audioUrl);
                if (File.Exists(cachePath))
                {
                    Debug.WriteLine($"[DownloadManager]   [{i + 1}/{audioList.Count}] ✓ 缓存命中, 从缓存拷贝: {audio.Name}");
                    await CopyFileWithRetryAsync(cachePath, localFile, token);
                    success = true;
                }
                else
                {
                    Debug.WriteLine($"[DownloadManager]   [{i + 1}/{audioList.Count}] 开始下载音频: {audio.Name}");

                    var currentNewlyProcessed = newlyProcessed; // 捕获当前值供回调使用
                    success = await DownloadFileWithProgressAsync(
                        audioUrl, localFile, token, resourceId,
                        (written, total) =>
                        {
                            // 当前音频的进度占总工作的比例
                            var audioWeight = 1f / totalTasks;
                            var baseProgress = startProgress + (float)currentNewlyProcessed / totalTasks;
                            var fileProgress = total > 0 ? (float)written / total : 0f;
                            var overall = baseProgress + fileProgress * audioWeight;
                            progress?.Report(overall);
                            UpdateState(resourceId, DownloadStatus.Downloading, overall);
                        });
                }

                if (success)
                {
                    Debug.WriteLine($"[DownloadManager]   [{i + 1}/{audioList.Count}] ✓ 音频就绪: {audio.Name}");
                    results.Add(new AudioItem
                    {
                        Id = audio.Id,
                        Name = audio.Name,
                        Src = localFile,
                        DurationMs = audio.DurationMs,
                        OrderIndex = audio.OrderIndex
                    });
                }
                else
                {
                    // 下载失败，保留远程 URL 以支持在线试听
                    Debug.WriteLine($"[DownloadManager]   [{i + 1}/{audioList.Count}] ✗ 下载失败, 保留远程: {audio.Name}");
                    results.Add(new AudioItem
                    {
                        Id = audio.Id,
                        Name = audio.Name,
                        Src = audioUrl,
                        DurationMs = audio.DurationMs,
                        OrderIndex = audio.OrderIndex
                    });
                }

                // 持久化进度到 .download
                SaveDownloadRecord(detail, results, false);

                newlyProcessed++;
                var overallP = startProgress + (float)newlyProcessed / totalTasks;
                progress?.Report(overallP);
                UpdateState(resourceId, DownloadStatus.Downloading, overallP);
            }

            return results;
        }

        // ==================== 图标下载 ====================

        /// <summary>下载资源图标（参照 Android downloadResourceIcon）</summary>
        private async Task<string?> DownloadIconAsync(string resourceId, string iconUrl, CancellationToken token)
        {
            if (string.IsNullOrEmpty(iconUrl)) return null;

            var ext = Path.GetExtension(iconUrl.Split('?')[0]);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            // 集中图标目录：文件名即资源 id（如 {id}.png）
            var iconFile = ResourceService.GetIconPath(resourceId, ext);

            if (File.Exists(iconFile))
            {
                Debug.WriteLine($"[DownloadManager]   图标已存在: {iconFile}");
                return iconFile;
            }

            // 检查缓存中是否已有该图标
            var cachePath = GetAudioCachePath(iconUrl);
            if (File.Exists(cachePath))
            {
                Debug.WriteLine($"[DownloadManager]   图标缓存命中, 从缓存拷贝: {iconUrl}");
                await CopyFileWithRetryAsync(cachePath, iconFile, token);
                return iconFile;
            }

            Debug.WriteLine($"[DownloadManager]   开始下载图标: url={iconUrl}");
            var success = await DownloadFileWithProgressAsync(iconUrl, iconFile, token, resourceId);
            return success ? iconFile : null;
        }

        // ==================== 通用 HTTP 下载 ====================

        /// <summary>下载文件到指定路径，支持进度报告和取消</summary>
        private async Task<bool> DownloadFileWithProgressAsync(
            string url, string destPath, CancellationToken token,
            string resourceId,
            Action<long, long>? onProgress = null)
        {
            var tempPath = destPath + ".tmp";
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                token.ThrowIfCancellationRequested();

                // 先确保 temp 文件不存在（避免残留文件被锁导致 FileStream 创建失败）
                TryDeleteQuietly(tempPath);

                try
                {
                    using var response = await _httpClient.GetAsync(url,
                        HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    await using var sourceStream = await response.Content.ReadAsStreamAsync(token);

                    // 创建目标文件流（带重试，防止 temp 文件被其他进程锁住）
                    FileStream? destStream = null;
                    for (int fsAttempt = 0; fsAttempt < 3; fsAttempt++)
                    {
                        try
                        {
                            destStream = new FileStream(tempPath,
                                FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 4096,
                                FileOptions.Asynchronous | FileOptions.SequentialScan);
                            break;
                        }
                        catch (IOException ex)
                        {
                            TryDeleteQuietly(tempPath);
                            if (fsAttempt < 2)
                            {
                                Debug.WriteLine($"[DownloadManager]   创建文件流重试({fsAttempt + 1}): {ex.Message}");
                                await Task.Delay(50 + fsAttempt * 50, token);
                            }
                            else
                            {
                                Debug.WriteLine($"[DownloadManager]   无法创建文件流(已重试3次): {tempPath}");
                                return false;
                            }
                        }
                    }
                    if (destStream == null) return false;
                    await using (destStream)
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;
                        int chunkIndex = 0;

                        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            await destStream.WriteAsync(buffer, 0, bytesRead, token);
                            totalRead += bytesRead;
                            chunkIndex++;

                            if (chunkIndex % 32 == 0)
                            {
                                onProgress?.Invoke(totalRead, totalBytes);
                                NotifyProgressListener(resourceId, totalRead, totalBytes);
                            }
                        }

                        // 最终进度
                        onProgress?.Invoke(totalRead, totalBytes);
                        NotifyProgressListener(resourceId, totalRead, totalBytes);

                        await destStream.FlushAsync(token);
                    } // using 释放 destStream

                    // 重命名临时文件到目标文件（带重试）
                    await MoveFileWithRetryAsync(tempPath, destPath, token);

                    return true;
                }
                catch (OperationCanceledException)
                {
                    TryDeleteQuietly(tempPath);
                    throw;
                }
                catch (HttpIOException ex) when (attempt < maxRetries - 1)
                {
                    Debug.WriteLine($"[DownloadManager]   HTTP流异常(第{attempt + 1}次重试): {ex.Message} | url={url}");
                    TryDeleteQuietly(tempPath);
                    await Task.Delay(300 * (1 << attempt), token);
                }
                catch (HttpRequestException ex) when (attempt < maxRetries - 1)
                {
                    Debug.WriteLine($"[DownloadManager]   HTTP请求异常(第{attempt + 1}次重试): {ex.Message} | url={url}");
                    TryDeleteQuietly(tempPath);
                    await Task.Delay(300 * (1 << attempt), token);
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    Debug.WriteLine($"[DownloadManager]   文件访问冲突(第{attempt + 1}次重试): {ex.Message} | url={url}");
                    TryDeleteQuietly(tempPath);
                    // 指数退避等待（200ms → 400ms → 800ms）
                    await Task.Delay(200 * (1 << attempt), token);
                }
                catch (Exception ex)
                {
                    TryDeleteQuietly(tempPath);
                    Debug.WriteLine($"[DownloadManager]   下载异常: {ex.GetType().Name}: {ex.Message} | url={url}");
                    return false;
                }
            }

            Debug.WriteLine($"[DownloadManager]   下载失败(已重试{maxRetries}次): url={url}");
            return false;
        }

        /// <summary>安全移动文件（带重试 + 指数退避）</summary>
        private static async Task MoveFileWithRetryAsync(
            string sourcePath, string destPath, CancellationToken token)
        {
            const int moveRetries = 5;
            for (int i = 0; i < moveRetries; i++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    // 删除目标（如果存在）
                    if (File.Exists(destPath))
                        File.Delete(destPath);

                    File.Move(sourcePath, destPath);
                    return;
                }
                catch (IOException) when (i < moveRetries - 1)
                {
                    Debug.WriteLine($"[DownloadManager] MoveFile重试({i + 1}/{moveRetries}): {sourcePath} -> {destPath}");
                    await Task.Delay(100 * (1 << i), token);
                }
            }

            // 最后一次重试也失败，尝试强制覆盖
            try
            {
                TryDeleteQuietly(destPath);
                File.Move(sourcePath, destPath, overwrite: true);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[DownloadManager] MoveFile最终失败: {ex.Message} | {sourcePath} -> {destPath}");
            }
        }

        /// <summary>安静地删除文件（忽略所有异常，带重试）</summary>
        private static void TryDeleteQuietly(string path)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    return;
                }
                catch
                {
                    if (i < 2) Thread.Sleep(30);
                }
            }
        }

        /// <summary>
        /// 从 URL 计算音频缓存路径（与 AudioPlaybackService.GetCachePath 算法一致）。
        /// 缓存目录: %LOCALAPPDATA%\Sounder-APP\audio_cache\{md5(url).hex}{ext}
        /// </summary>
        private static string GetAudioCachePath(string url)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(url));
            var ext = GetExtensionFromUrl(url);
            return Path.Combine(AudioCacheDir, Convert.ToHexString(hash).ToLowerInvariant() + ext);
        }

        /// <summary>从 URL 提取文件扩展名（去掉查询参数后取扩展名，默认 .mp3）</summary>
        private static string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return ".mp3";

            var ext = Path.GetExtension(url.Split('?')[0]);
            if (!string.IsNullOrEmpty(ext) && ext.Length <= 8)
                return ext;

            return ".mp3";
        }

        /// <summary>在目录中查找匹配 audio_{index}.* 的已存在文件</summary>
        private static string? FindExistingAudioFile(string audioDir, int index)
        {
            try
            {
                if (!Directory.Exists(audioDir)) return null;

                var prefix = $"audio_{index}.";
                return Directory.GetFiles(audioDir, $"{prefix}*")
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>安全拷贝文件（带重试 + 指数退避）</summary>
        private static async Task CopyFileWithRetryAsync(
            string sourcePath, string destPath, CancellationToken token)
        {
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            const int copyRetries = 5;
            for (int i = 0; i < copyRetries; i++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    // 删除目标文件（如果存在）
                    TryDeleteQuietly(destPath);

                    File.Copy(sourcePath, destPath, overwrite: true);
                    return;
                }
                catch (IOException) when (i < copyRetries - 1)
                {
                    Debug.WriteLine($"[DownloadManager] CopyFile重试({i + 1}/{copyRetries}): {sourcePath} -> {destPath}");
                    await Task.Delay(100 * (1 << i), token);
                }
            }

            // 最后一次重试也失败，静默吞下
            try
            {
                TryDeleteQuietly(destPath);
                File.Copy(sourcePath, destPath, overwrite: true);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[DownloadManager] CopyFile最终失败: {ex.Message} | {sourcePath} -> {destPath}");
            }
        }

        // ==================== 内部辅助 ====================

        /// <summary>获取 .download 文件路径</summary>
        private static string GetDownloadRecordPath(string resourceId) =>
            Path.Combine(InstallRoot, resourceId, ".download");

        /// <summary>保存下载进度到 .download 文件</summary>
        private static void SaveDownloadRecord(Resource detail, List<AudioItem> downloadedAudioItems, bool iconDownloaded)
        {
            try
            {
                var dir = Path.Combine(InstallRoot, detail.Id);
                Directory.CreateDirectory(dir);
                var path = GetDownloadRecordPath(detail.Id);
                var tmpPath = path + ".tmp";

                var record = new DownloadRecordData
                {
                    ResourceId = detail.Id,
                    ResourceName = detail.Name,
                    DisplayName = detail.DisplayName,
                    Description = detail.Description,
                    Icon = detail.Icon,
                    Size = detail.Size,
                    PublishDate = detail.PublishDate,
                    TotalAudioCount = detail.AudioItems?.Count ?? 0,
                    DownloadedAudioItems = downloadedAudioItems.Select(a => new DownloadedAudioItemData
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Src = a.Src,
                        DurationMs = a.DurationMs,
                        OrderIndex = a.OrderIndex
                    }).ToList(),
                    IconDownloaded = iconDownloaded,
                    LastUpdateTime = DateTime.Now,
                    RemoteAudioJson = detail.AudioItems?.Count > 0
                        ? JsonSerializer.Serialize(detail.AudioItems, SettingsJsonContext.Default.ListAudioItem)
                        : null
                };

                var json = JsonSerializer.Serialize(record, SettingsJsonContext.Default.DownloadRecordData);
                TryDeleteQuietly(tmpPath);
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadManager] 保存 .download 失败(id={detail.Id}): {ex.Message}");
            }
        }

        /// <summary>读取 .download 文件中的下载进度记录</summary>
        private static DownloadRecordData? ReadDownloadRecord(string resourceId)
        {
            try
            {
                var path = GetDownloadRecordPath(resourceId);
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.DownloadRecordData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadManager] 读取 .download 失败(id={resourceId}): {ex.Message}");
                return null;
            }
        }

        /// <summary>删除 .download 文件（下载完成后清理）</summary>
        private static void DeleteDownloadRecord(string resourceId)
        {
            try
            {
                var path = GetDownloadRecordPath(resourceId);
                TryDeleteQuietly(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadManager] 删除 .download 失败(id={resourceId}): {ex.Message}");
            }
        }

        /// <summary>确保 .download 记录中包含远程音频列表 JSON（含原始 URL），
        /// 供暂停后继续时通过 ToResource() 重建详情，无需重新请求远程 API。
        /// 首次获取详情后立即调用，确保在下载任何文件前已持久化。</summary>
        private static void EnsureRemoteAudioJsonInRecord(Resource detail)
        {
            try
            {
                var record = ReadDownloadRecord(detail.Id);
                if (record != null && !string.IsNullOrEmpty(record.RemoteAudioJson)) return;

                record ??= new DownloadRecordData();
                record.ResourceId = detail.Id;
                record.ResourceName = detail.Name;
                record.DisplayName = detail.DisplayName;
                record.Description = detail.Description;
                record.Icon = detail.Icon;
                record.Size = detail.Size;
                record.PublishDate = detail.PublishDate;
                record.TotalAudioCount = detail.AudioItems?.Count ?? 0;
                record.RemoteAudioJson = detail.AudioItems?.Count > 0
                    ? JsonSerializer.Serialize(detail.AudioItems, SettingsJsonContext.Default.ListAudioItem)
                    : null;
                record.LastUpdateTime = DateTime.Now;

                var dir = Path.Combine(InstallRoot, detail.Id);
                Directory.CreateDirectory(dir);
                var path = GetDownloadRecordPath(detail.Id);
                var tmpPath = path + ".tmp";
                var json = JsonSerializer.Serialize(record, SettingsJsonContext.Default.DownloadRecordData);
                TryDeleteQuietly(tmpPath);
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, path, overwrite: true);

                Debug.WriteLine($"[DownloadManager] RemoteAudioJson 已缓存 (id={detail.Id}, {detail.AudioItems?.Count} 条)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadManager] 缓存 RemoteAudioJson 失败(id={detail.Id}): {ex.Message}");
            }
        }

        /// <summary>扫描所有未完成的下载记录，返回 ResourceId 列表</summary>
        public static List<string> GetPendingDownloads()
        {
            var result = new List<string>();
            try
            {
                if (!Directory.Exists(InstallRoot)) return result;

                foreach (var dir in Directory.GetDirectories(InstallRoot))
                {
                    var recordPath = Path.Combine(dir, ".download");
                    if (File.Exists(recordPath))
                    {
                        var resourceId = Path.GetFileName(dir);
                        result.Add(resourceId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadManager] 扫描未完成下载失败: {ex.Message}");
            }
            return result;
        }

        /// <summary>检查指定资源是否有未完成的下载记录</summary>
        public static bool HasPendingDownload(string resourceId)
        {
            return File.Exists(GetDownloadRecordPath(resourceId));
        }

        private void UpdateState(string resourceId, DownloadStatus status, float progress)
        {
            var state = new DownloadState
            {
                ResourceId = resourceId,
                Status = status,
                Progress = progress
            };
            _states[resourceId] = state;
            StateChanged?.Invoke(state);
        }

        private void NotifyProgressListener(string resourceId, long bytesWritten, long totalBytes)
        {
            if (!_progressListeners.TryGetValue(resourceId, out var listener)) return;

            var info = new DownloadProgressInfo
            {
                ResourceId = resourceId,
                BytesWritten = bytesWritten,
                TotalBytes = totalBytes
            };
            listener(info);
        }

        private void CancelToken(string resourceId)
        {
            if (_cancelTokens.TryRemove(resourceId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var cts in _cancelTokens.Values)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch { }
            }
            _cancelTokens.Clear();
            _httpClient.Dispose();
        }
    }
}
