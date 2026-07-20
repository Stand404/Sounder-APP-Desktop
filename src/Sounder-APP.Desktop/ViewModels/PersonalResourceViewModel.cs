using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using Sounder_APP.Models;
using Sounder_APP.Services;
using Sounder_APP.Views;

namespace Sounder_APP.ViewModels
{
    public partial class PersonalResourceViewModel : AudioPlaybackViewModelBase, ISearchViewModel
    {
        private readonly ResourceService _resourceService;
        private readonly DownloadManager _downloadManager;

        [ObservableProperty]
        private ObservableCollection<Resource> _resources = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isEmpty = true;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _hasMore = true;

        [ObservableProperty]
        private bool _isLoadingMore;

        [ObservableProperty]
        private string _statusText = string.Empty;

        // ===== 编辑模式状态 =====

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private PersonalResourceEditViewModel? _editViewModel;

        /// <summary>编辑模式下正在编辑的原始资源引用</summary>
        private Resource? _editingResource;

        /// <summary>编辑模式打开前选中的资源（用于切换资源时恢复）</summary>
        private Resource? _previousSelectedResource;

        /// <summary>离开编辑后待选中的资源 ID（防止 ListBox ItemsSource 重建时冲掉 TwoWay 选中的目标）</summary>
        private string? _pendingSelectResourceId;

        private List<Resource> _allResources = new();

        /// <summary>列表为空时隐藏整个右侧详情面板</summary>
        public bool ShowDetailPanel => !IsLoading && !IsEmpty;

        partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowDetailPanel));
        partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(ShowDetailPanel));

        public PersonalResourceViewModel(ResourceService resourceService, DownloadManager downloadManager)
        {
            _resourceService = resourceService;
            _downloadManager = downloadManager;
            _ = LoadResourcesAsync();
        }

        // ===== 资源加载 =====

        [RelayCommand]
        private async Task LoadResourcesAsync()
        {
            IsLoading = true;
            HasError = false;
            SearchQuery = string.Empty;
            StatusText = LocalizationService.Instance.Get("loading");
            try
            {
                var items = await _resourceService.GetLocalResourcesAsync();
                _allResources = items;
                Resources = new ObservableCollection<Resource>(items);
                IsEmpty = Resources.Count == 0;
                HasMore = false;

                // 保留当前选中项（优先使用离开编辑后的目标 ID）；否则选第一个
                var currentId = _pendingSelectResourceId ?? SelectedResource?.Id;
                _pendingSelectResourceId = null;
                if (Resources.Count > 0)
                    SelectedResource = Resources.FirstOrDefault(r => r.Id == currentId) ?? Resources[0];
                else
                    SelectedResource = null;

                StatusText = IsEmpty
                    ? LocalizationService.Instance.Get("no_personal_resources")
                    : LocalizationService.Instance.Get("resource_count_fmt", Resources.Count);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = LocalizationService.Instance.Get("load_error_fmt", ex.Message);
                StatusText = LocalizationService.Instance.Get("load_failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                await LoadResourcesAsync();
                return;
            }

            IsLoading = true;
            HasError = false;
            StatusText = LocalizationService.Instance.Get("searching");
            try
            {
                var query = SearchQuery.Trim().ToLower();
                var filtered = _allResources
                    .Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                             || r.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Resources = new ObservableCollection<Resource>(filtered);
                IsEmpty = Resources.Count == 0;
                HasMore = false;

                // 保留当前选中项（优先使用离开编辑后的目标 ID）；否则选第一个
                var currentId = _pendingSelectResourceId ?? SelectedResource?.Id;
                _pendingSelectResourceId = null;
                if (Resources.Count > 0)
                    SelectedResource = Resources.FirstOrDefault(r => r.Id == currentId) ?? Resources[0];
                else
                    SelectedResource = null;

                StatusText = IsEmpty
                    ? LocalizationService.Instance.Get("no_match_resource")
                    : LocalizationService.Instance.Get("found_resource_count_fmt", Resources.Count);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ===== 卸载（带确认） =====

        [RelayCommand]
        private async Task DeleteResourceAsync(Resource? resource)
        {
            if (resource == null) return;

            // 二次确认弹窗
            var parent = GetTopWindow();
            if (parent == null) return;

            var result = await DirtyConfirmWindow.ShowDeleteConfirmAsync(parent, resource.DisplayName);
            if (!result) return;

            _downloadManager.Cleanup(resource.Id);
            await _resourceService.UninstallResourceAsync(resource);
            _allResources.Remove(resource);
            Resources.Remove(resource);
            IsEmpty = Resources.Count == 0;

            if (SelectedResource == resource)
                SelectedResource = Resources.FirstOrDefault();

            StatusText = IsEmpty
                ? LocalizationService.Instance.Get("no_personal_resources")
                : LocalizationService.Instance.Get("resource_count_fmt", Resources.Count);
        }

        // ===== 导入资源（从 zip 文件导入） =====

        [RelayCommand]
        private async Task ImportResourceAsync()
        {
            var parent = GetTopWindow();
            if (parent == null) return;

            var storageProvider = parent.StorageProvider;
            var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LocalizationService.Instance.Get("import_title"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(LocalizationService.Instance.Get("zip_file_type")) { Patterns = new[] { "*.zip" } }
                }
            });

            if (result.Count == 0) return;

            var zipPath = result[0].Path.LocalPath;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var tempDir = Path.Combine(Path.GetTempPath(), $"sounder_import_{timestamp}");

            try
            {
                // 1. 解压到临时目录
                ZipFile.ExtractToDirectory(zipPath, tempDir);
                Debug.WriteLine($"[Import] 已解压到临时目录: {tempDir}");

                // 2. 定位 manifest.json（处理嵌套目录的情况）
                var contentRoot = FindContentRoot(tempDir);

                // 3. 读取 manifest.json
                var manifestPath = Path.Combine(contentRoot, "manifest.json");
                if (!File.Exists(manifestPath))
                    throw new InvalidOperationException(LocalizationService.Instance.Get("invalid_package_no_manifest"));

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize(manifestJson, SettingsJsonContext.Default.ExportManifest);
                if (manifest == null || manifest.Version < 1 || manifest.Resource == null)
                    throw new InvalidOperationException(LocalizationService.Instance.Get("invalid_package_incomplete_manifest"));

                Debug.WriteLine($"[Import] manifest 读取成功: {manifest.Resource.DisplayName}");

                // 4. 创建新资源
                var newId = $"local_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}";
                var newResource = new Resource
                {
                    Id = newId,
                    Name = manifest.Resource.DisplayName,
                    DisplayName = manifest.Resource.DisplayName,
                    Description = manifest.Resource.Description ?? string.Empty,
                    Icon = string.Empty,
                    Source = ResourceSource.Local,
                    IsInstalled = true,
                    PublishDate = manifest.ExportDate ?? DateTime.Now.ToString("yyyy-MM-dd"),
                    AudioItems = new List<AudioItem>()
                };

                var dstDir = ResourceService.GetResourceInstallDir(newId);
                Directory.CreateDirectory(dstDir);

                // 5. 导入图标
                long iconSize = 0;
                if (manifest.Files != null && !string.IsNullOrEmpty(manifest.Files.Icon))
                {
                    var iconSrc = Path.Combine(contentRoot, manifest.Files.Icon);
                    var ext = GetFileExtension(manifest.Files.Icon, "jpg");
                    var iconDest = Path.Combine(dstDir, $"icon.{ext}");
                    try
                    {
                        if (File.Exists(iconSrc))
                        {
                            File.Copy(iconSrc, iconDest, overwrite: true);
                            newResource.Icon = iconDest;
                            iconSize = new FileInfo(iconDest).Length;
                            Debug.WriteLine($"[Import] 图标已导入: icon.{ext} ({iconSize} B)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Import] 导入图标失败: {ex.Message}");
                    }
                }

                // 6. 导入音频
                var audioMetas = manifest.Resource.AudioList ?? new List<ExportManifest.ExportAudioMeta>();
                var audioFiles = manifest.Files?.Audios ?? new List<string?>();
                for (int i = 0; i < audioMetas.Count; i++)
                {
                    var meta = audioMetas[i];
                    var filePath = i < audioFiles.Count ? audioFiles[i] : null;
                    var audioId = $"audio_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}";

                    var newAudio = new AudioItem
                    {
                        Id = audioId,
                        Name = meta.Name ?? $"音频{i + 1}",
                        Src = string.Empty,
                        DurationMs = meta.DurationMs,
                        OrderIndex = i + 1
                    };

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        var audioSrc = Path.Combine(contentRoot, filePath);
                        var ext = GetFileExtension(filePath, "mp3");
                        var audioDest = Path.Combine(dstDir, $"audio_{i}.{ext}");
                        try
                        {
                            if (File.Exists(audioSrc))
                            {
                                File.Copy(audioSrc, audioDest, overwrite: true);
                                newAudio.Src = audioDest;
                                newAudio.Size = new FileInfo(audioDest).Length;

                                // 如果 manifest 中没有时长（或为 0），从音频文件读取
                                if (newAudio.DurationMs <= 0)
                                {
                                    try
                                    {
                                        using var reader = new AudioFileReader(audioDest);
                                        newAudio.DurationMs = (long)reader.TotalTime.TotalMilliseconds;
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[Import] 读取音频时长失败: {ex.Message}");
                                    }
                                }

                                Debug.WriteLine($"[Import] 音频[{i}]已导入: audio_{i}.{ext} ({newAudio.SizeFormatted}, {newAudio.DurationFormatted})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Import] 导入音频[{i}]失败: {ex.Message}");
                        }
                    }

                    newResource.AudioItems.Add(newAudio);
                }

                // 计算资源包总大小（图标 + 所有音频）
                var totalBytes = iconSize + newResource.AudioItems.Sum(a => a.Size);
                newResource.Size = totalBytes.ToString();

                // 7. 保存 resource.json
                ResourceService.SaveResourceJson(newResource);

                // 8. 清理临时目录
                CleanupTempDir(tempDir);

                // 9. 刷新列表
                _allResources.Add(newResource);
                Resources.Add(newResource);
                SelectedResource = newResource;
                IsEmpty = false;
                StatusText = LocalizationService.Instance.Get("resource_count_fmt", Resources.Count);
                Debug.WriteLine($"[Import] 导入完成: {newResource.DisplayName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Import] 导入失败: {ex.Message}");
                CleanupTempDir(tempDir);
            }
        }

        // ===== 创建资源 =====

        [RelayCommand]
        private void CreateResource()
        {
            var newId = $"local_{DateTime.Now.Ticks}_{Guid.NewGuid():N}";
            var dir = ResourceService.GetResourceInstallDir(newId);
            Directory.CreateDirectory(dir);

            // 复制默认图标 Assets/sounder.jpg
            var iconPath = string.Empty;
            var destIcon = Path.Combine(dir, "icon.jpg");
            try
            {
                var uri = new Uri("avares://Sounder-APP/Assets/sounder.jpg");
                using var stream = AssetLoader.Open(uri);
                using var fileStream = File.Create(destIcon);
                stream.CopyTo(fileStream);
                iconPath = destIcon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Create] 复制默认图标失败: {ex.Message}");
            }

            var resource = new Resource
            {
                Id = newId,
                Name = LocalizationService.Instance.Get("default_resource_name"),
                DisplayName = LocalizationService.Instance.Get("default_resource_name"),
                Description = LocalizationService.Instance.Get("default_resource_desc"),
                Icon = iconPath,
                Source = Models.ResourceSource.Local,
                IsInstalled = true,
                AudioItems = new List<AudioItem>()
            };

            ResourceService.SaveResourceJson(resource);

            _allResources.Add(resource);
            Resources.Add(resource);
            SelectedResource = resource;
            IsEmpty = false;
            StatusText = LocalizationService.Instance.Get("resource_count_fmt", Resources.Count);

            // 自动进入编辑模式
            EditResource(resource);
        }

        // ===== 克隆资源 =====

        [RelayCommand]
        private async Task CloneResourceAsync(Resource? resource)
        {
            if (resource == null) return;

            var newId = $"local_{DateTime.Now.Ticks}_{Guid.NewGuid():N}";
            var clone = resource.Clone();
            clone.Id = newId;
            clone.DisplayName = resource.DisplayName + LocalizationService.Instance.Get("clone_suffix");
            clone.Source = Models.ResourceSource.Local;
            clone.IsInstalled = true;

            // 复制资源文件夹
            var srcDir = ResourceService.GetResourceInstallDir(resource.Id);
            var dstDir = ResourceService.GetResourceInstallDir(newId);
            Directory.CreateDirectory(dstDir);

            // 拷贝音频文件到新目录
            foreach (var audio in clone.AudioItems)
            {
                if (!string.IsNullOrEmpty(audio.Src) && !audio.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var audioFileName = Path.GetFileName(audio.Src);
                    var destAudioPath = Path.Combine(dstDir, audioFileName);
                    try
                    {
                        if (File.Exists(audio.Src))
                            File.Copy(audio.Src, destAudioPath, overwrite: true);
                        audio.Src = destAudioPath;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Clone] 复制音频文件失败: {ex.Message}");
                    }
                }
            }

            // 拷贝图标文件
            if (!string.IsNullOrEmpty(clone.Icon) && !clone.Icon.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var iconFileName = Path.GetFileName(clone.Icon);
                var destIconPath = Path.Combine(dstDir, iconFileName);
                try
                {
                    if (File.Exists(clone.Icon))
                    {
                        File.Copy(clone.Icon, destIconPath, overwrite: true);
                        clone.Icon = destIconPath;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Clone] 复制图标文件失败: {ex.Message}");
                }
            }

            // 保存克隆资源的 JSON
            ResourceService.SaveResourceJson(clone);

            // 更新列表
            _allResources.Add(clone);
            Resources.Add(clone);
            SelectedResource = clone;
            IsEmpty = false;
            StatusText = LocalizationService.Instance.Get("resource_count_fmt", Resources.Count);
        }

        // ===== 导出资源（打包为 zip 文件） =====

        [RelayCommand]
        private async Task ExportResourceAsync(Resource? resource)
        {
            if (resource == null) return;

            var parent = GetTopWindow();
            if (parent == null) return;

            // 让用户选择 zip 文件保存位置
            var storageProvider = parent.StorageProvider;
            var safeName = SanitizeFileName(resource.DisplayName);
            var zipFile = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = LocalizationService.Instance.Get("export_title"),
                SuggestedFileName = $"{safeName}.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(LocalizationService.Instance.Get("zip_file_type")) { Patterns = new[] { "*.zip" } }
                }
            });

            if (zipFile == null) return;

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var tempDir = Path.Combine(Path.GetTempPath(), $"sounder_export_{timestamp}");
            var srcDir = ResourceService.GetResourceInstallDir(resource.Id);

            try
            {
                Directory.CreateDirectory(tempDir);

                // 1. 导出图标
                string? iconFileName = null;
                if (!string.IsNullOrEmpty(resource.Icon) && !resource.Icon.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var ext = GetFileExtension(resource.Icon, "jpg");
                    iconFileName = $"icon.{ext}";
                    var iconDest = Path.Combine(tempDir, iconFileName);
                    try
                    {
                        File.Copy(resource.Icon, iconDest, overwrite: true);
                        Debug.WriteLine($"[Export] 图标已复制: {iconFileName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Export] 导出图标失败，跳过: {ex.Message}");
                        iconFileName = null;
                    }
                }

                // 2. 导出音频文件
                var audiosDir = Path.Combine(tempDir, "audios");
                Directory.CreateDirectory(audiosDir);
                var audioFileNames = new List<string?>();
                var audioMetas = new List<ExportManifest.ExportAudioMeta>();

                for (int i = 0; i < resource.AudioItems.Count; i++)
                {
                    var audio = resource.AudioItems[i];
                    audioMetas.Add(new ExportManifest.ExportAudioMeta
                    {
                        Name = audio.Name,
                        DurationMs = audio.DurationMs
                    });

                    if (!string.IsNullOrEmpty(audio.Src) && !audio.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        var ext = GetFileExtension(audio.Src, "mp3");
                        var fileName = $"audio_{i}.{ext}";
                        var audioDest = Path.Combine(audiosDir, fileName);
                        try
                        {
                            File.Copy(audio.Src, audioDest, overwrite: true);
                            audioFileNames.Add($"audios/{fileName}");
                            Debug.WriteLine($"[Export] 音频[{i}]已复制: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Export] 导出音频[{i}]失败，跳过: {ex.Message}");
                            audioFileNames.Add(null);
                        }
                    }
                    else
                    {
                        audioFileNames.Add(null);
                    }
                }

                // 3. 写入 manifest.json
                var manifest = new ExportManifest
                {
                    Version = 1,
                    ExportDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    Resource = new ExportManifest.ExportResourceMeta
                    {
                        DisplayName = resource.DisplayName,
                        Description = resource.Description,
                        AudioList = audioMetas
                    },
                    Files = new ExportManifest.ExportFiles
                    {
                        Icon = iconFileName,
                        Audios = audioFileNames
                    }
                };

                var manifestJson = JsonSerializer.Serialize(manifest, SettingsJsonContext.Default.ExportManifest);
                var manifestPath = Path.Combine(tempDir, "manifest.json");
                await File.WriteAllTextAsync(manifestPath, manifestJson);
                Debug.WriteLine("[Export] manifest.json 已写入");

                // 4. 打包为 zip
                var destPath = zipFile.Path.LocalPath;
                if (File.Exists(destPath)) File.Delete(destPath);
                ZipFile.CreateFromDirectory(tempDir, destPath);
                Debug.WriteLine($"[Export] ZIP 已保存到: {destPath}");

                // 5. 清理临时目录
                CleanupTempDir(tempDir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Export] 导出失败: {ex.Message}");
                CleanupTempDir(tempDir);
            }
        }

        // ===== 打开资源目录 =====

        [RelayCommand]
        private void OpenResourceFolder(Resource? resource)
        {
            if (resource == null) return;
            var dir = ResourceService.GetResourceInstallDir(resource.Id);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenFolder] 打开目录失败: {ex.Message}");
            }
        }

        /// <summary>递归拷贝目录</summary>
        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }

        // ===== 导出/导入辅助方法 =====

        /// <summary>清理临时目录（忽略错误）</summary>
        private static void CleanupTempDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CleanupTemp] 清理失败: {ex.Message}");
            }
        }

        /// <summary>获取文件扩展名（不含点），默认返回 defaultExt</summary>
        private static string GetFileExtension(string filePath, string defaultExt)
        {
            try
            {
                var ext = Path.GetExtension(filePath)?.TrimStart('.');
                return string.IsNullOrEmpty(ext) ? defaultExt : ext.ToLower();
            }
            catch
            {
                return defaultExt;
            }
        }

        /// <summary>过滤文件名中的非法字符</summary>
        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "resource" : sanitized;
        }

        /// <summary>在解压目录中定位 manifest.json 所在的内容根目录</summary>
        private static string FindContentRoot(string tempDir)
        {
            // 首先尝试根目录
            if (File.Exists(Path.Combine(tempDir, "manifest.json")))
                return tempDir;

            // 查找子目录
            try
            {
                var subDirs = Directory.GetDirectories(tempDir);
                if (subDirs.Length == 1)
                {
                    Debug.WriteLine($"[Import] 使用子目录作为内容根: {subDirs[0]}");
                    return subDirs[0];
                }

                foreach (var sub in subDirs)
                {
                    if (File.Exists(Path.Combine(sub, "manifest.json")))
                    {
                        Debug.WriteLine($"[Import] 在子目录找到 manifest: {sub}");
                        return sub;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Import] 查找内容根目录失败: {ex.Message}");
            }

            // 降级返回 tempDir
            return tempDir;
        }

        // ===== 选中资源变更拦截（ListBox TwoWay 绑定触发） =====

        /// <summary>
        /// 编辑模式下切换资源：自动保存当前编辑并退出，切换到新资源的详情页。
        /// </summary>
        protected override async void OnSelectedResourceChangedCore(Resource? value)
        {
            if (value == null) return;

            // 点击正在编辑的同一个资源 → 忽略
            if (IsEditing && _editingResource != null && value.Id == _editingResource.Id) return;

            // 非编辑模式 → 正常切换
            if (!IsEditing) return;

            // 编辑模式 → 保存当前、退出，切换到新资源的详情页
            // 先标记目标资源 ID，防止 LoadResourcesAsync 重建集合时丢失选中
            _pendingSelectResourceId = value.Id;

            if (EditViewModel != null)
                await EditViewModel.LeaveEditCommand.ExecuteAsync(null);
            // LeaveEdit → ExitEditMode + LoadResources → 
            // LoadResourcesAsync 优先使用 _pendingSelectResourceId 恢复选中
        }

        // ===== 选中资源（编程式调用入口） =====

        [RelayCommand]
        private void SelectResource(Resource? resource)
        {
            if (resource == null || resource == SelectedResource) return;
            SelectedResource = resource;
        }

        // ===== 编辑模式 =====

        [RelayCommand]
        private void EditResource(Resource? resource)
        {
            if (resource == null) return;
            _editingResource = resource;
            _previousSelectedResource = SelectedResource;
            EnterEditMode(resource);
        }

        private void EnterEditMode(Resource resource)
        {
            EditViewModel = new PersonalResourceEditViewModel(
                _resourceService, _downloadManager, _audioPlayer, resource,
                OnLeaveEdit);
            IsEditing = true;
        }

        private void ExitEditMode()
        {
            IsEditing = false;
            EditViewModel = null;
            _editingResource = null;
            _previousSelectedResource = null;
        }

        /// <summary>离开编辑回调：退出编辑模式并刷新列表</summary>
        private void OnLeaveEdit()
        {
            ExitEditMode();
            _ = LoadResourcesCommand.ExecuteAsync(null);
        }

        // ===== 桩命令（兼容通用 DetailContent 绑定，实际不会触发） =====

        [RelayCommand]
        private Task InstallResourceAsync(Resource? resource) => Task.CompletedTask;

        /// <summary>
        /// 将资源添加到桌面：创建 .url 快捷方式指向 sounder://play?resourceId=xxx
        /// </summary>
        [RelayCommand]
        private async Task AddToDesktopAsync(Resource? resource)
        {
            if (resource == null) return;

            var success = await DesktopShortcutService.CreateShortcutAsync(resource);
            if (!success) return;

            // 确保 URL 协议已注册
            UrlProtocolService.RegisterProtocol();

            // 模态弹窗提示
            var L = LocalizationService.Instance;
            await DirtyConfirmWindow.ShowToastAsync(L.Get("added_to_desktop"), L.Get("shortcut_created_fmt", resource.Name));
        }

        private static Window? GetTopWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }
    }
}