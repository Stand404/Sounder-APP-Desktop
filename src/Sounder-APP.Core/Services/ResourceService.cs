using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Sounder_APP.Models;

namespace Sounder_APP.Services
{
    public class ResourceService
    {
        private readonly ApiService _api;

        private static readonly string InstallRoot;

        /// <summary>集中图标目录（installed 的同级 installed_icons/，每个资源一个以「资源 id」命名的图标文件）</summary>
        private static readonly string IconRoot;

        /// <summary>资源被卸载时触发（参数：resourceId）</summary>
        public static event Action<string>? ResourceUninstalled;

        static ResourceService()
        {
            var appDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sounder-APP");
            InstallRoot = Path.Combine(appDir, "installed");
            Directory.CreateDirectory(InstallRoot);
            // 参照 Android DownloadManager.kt：installed_icons/{resourceId}/icon.*
            IconRoot = Path.Combine(appDir, "installed_icons");
            Directory.CreateDirectory(IconRoot);
        }

        public ResourceService()
        {
            _api = new ApiService();
        }

        // ==================== 磁盘 I/O：按资源文件夹存储 ====================

        /// <summary>
        /// 扫描 installed/ 目录，读取每个子文件夹中的 resource.json 并还原为 Resource 列表。
        /// </summary>
        public List<Resource> GetInstalledResources()
        {
            var resources = new List<Resource>();
            try
            {
                if (!Directory.Exists(InstallRoot))
                    return resources;

                foreach (var dir in Directory.GetDirectories(InstallRoot))
                {
                    var jsonPath = Path.Combine(dir, "resource.json");
                    if (!File.Exists(jsonPath)) continue;

                    var resource = LoadResourceFromJson(jsonPath);
                    if (resource != null)
                        resources.Add(resource);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 扫描已安装资源失败: {ex.Message}");
            }
            return resources;
        }

        /// <summary>
        /// 将资源保存到 installed/{id}/resource.json（原子写入：先写 .tmp 再 rename）。
        /// 每个资源独立文件夹，不存在并发写冲突。
        /// </summary>
        public static void SaveResourceJson(Resource resource)
        {
            try
            {
                var dir = Path.Combine(InstallRoot, resource.Id);
                Directory.CreateDirectory(dir);
                var jsonPath = Path.Combine(dir, "resource.json");
                var tmpPath = jsonPath + ".tmp";

                var data = new InstalledResourceData
                {
                    Id = resource.Id,
                    Name = resource.Name,
                    DisplayName = resource.DisplayName,
                    Description = resource.Description,
                    Icon = resource.Icon,
                    Size = resource.Size,
                    PublishDate = resource.PublishDate,
                    AudioList = resource.AudioItems.Select((a, i) => new InstalledAudioItemData
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Src = a.Src,
                        Duration = a.DurationMs,
                        OrderIndex = a.OrderIndex
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(data, SettingsJsonContext.Default.InstalledResourceData);

                // 原子写入：先写临时文件，再重命名覆盖目标（NTFS 上 rename 是原子的）
                TryDeleteQuietly(tmpPath);
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, jsonPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 保存资源 JSON 失败(id={resource.Id}): {ex.Message}");
            }
        }

        /// <summary>
        /// 从 installed/{id}/resource.json 加载单个资源。
        /// </summary>
        public static Resource? LoadResourceJson(string id)
        {
            try
            {
                var dir = Path.Combine(InstallRoot, id);
                var jsonPath = Path.Combine(dir, "resource.json");
                if (!File.Exists(jsonPath)) return null;
                return LoadResourceFromJson(jsonPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 加载资源 JSON 失败(id={id}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 公开方法：从任意路径的 JSON 文件加载 Resource（用于导入）。
        /// </summary>
        public static Resource? LoadResourceJsonFromPath(string jsonPath) => LoadResourceFromJson(jsonPath);

        /// <summary>
        /// 从 JSON 文件路径反序列化为 Resource 对象。
        /// </summary>
        private static Resource? LoadResourceFromJson(string jsonPath)
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var data = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.InstalledResourceData);
                if (data == null) return null;

                return new Resource
                {
                    Id = data.Id,
                    Name = data.Name,
                    DisplayName = data.DisplayName,
                    Description = data.Description,
                    Icon = data.Icon,
                    Size = data.Size,
                    PublishDate = data.PublishDate,
                    Source = ResourceSource.Local,
                    IsInstalled = true,
                    AudioItems = data.AudioList.Select(a => new AudioItem
                    {
                        Id = string.IsNullOrEmpty(a.Id) ? $"{data.Id}_{a.OrderIndex}" : a.Id,
                        Name = a.Name,
                        Src = a.Src,
                        DurationMs = a.Duration,
                        OrderIndex = a.OrderIndex
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 反序列化资源 JSON 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>安静地删除文件（忽略所有异常），带重试</summary>
        private static void TryDeleteQuietly(string path)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    return;
                }
                catch
                {
                    if (i < 2) System.Threading.Thread.Sleep(30);
                }
            }
        }

        /// <summary>
        /// 删除整个资源文件夹。
        /// </summary>
        public static void DeleteResourceFolder(string resourceId)
        {
            try
            {
                // 一并清理集中图标目录中该资源的图标
                DeleteIconForResource(resourceId);

                var dir = Path.Combine(InstallRoot, resourceId);
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                    Debug.WriteLine($"[ResourceService] 已删除资源文件夹: {dir}");
                    ResourceUninstalled?.Invoke(resourceId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 删除资源文件夹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查资源文件夹是否存在（用于标记已安装）。
        /// </summary>
        private static bool IsResourceInstalled(string resourceId)
        {
            var jsonPath = Path.Combine(InstallRoot, resourceId, "resource.json");
            return File.Exists(jsonPath);
        }

        // ==================== 公开 API ====================

        /// <summary>获取已安装资源列表（供"我的资源"页面使用）</summary>
        public Task<List<Resource>> GetLocalResourcesAsync()
        {
            return Task.FromResult(GetInstalledResources());
        }

        /// <summary>根据 ID 查找已安装的资源</summary>
        public Resource? GetInstalledResourceById(string id)
        {
            return LoadResourceJson(id);
        }

        /// <summary>
        /// 卸载资源：删除文件夹。
        /// </summary>
        public Task<bool> UninstallResourceAsync(Resource resource)
        {
            DeleteResourceFolder(resource.Id);
            return Task.FromResult(true);
        }

        /// <summary>
        /// 保存编辑后的资源到文件夹。
        /// </summary>
        public async Task SaveResourceEditAsync(string resourceId, Resource editedResource)
        {
            SaveResourceJson(editedResource);
            await Task.CompletedTask;
        }

        // ==================== 原始 API（商店资源） ====================

        /// <summary>
        /// 将 API 返回的资源列表与本地已安装资源按 ID 比对（检查文件夹是否存在），标记已安装项。
        /// </summary>
        private void MarkInstalledItems(List<Resource> items)
        {
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.Id) && IsResourceInstalled(item.Id))
                    item.IsInstalled = true;
            }
        }

        /// <summary>
        /// 获取商店资源。仅使用 API 返回的真实数据，永不使用本地离线数据。
        /// </summary>
        public async Task<List<Resource>> GetCloudResourcesAsync(int page = 1, int pageSize = 10)
        {
            var apiResult = await _api.GetResourceListAsync(page, pageSize);

            if (apiResult.Success && apiResult.Data != null)
            {
                var items = apiResult.Data.Items.Select(Resource.FromRemote).ToList();
                MarkInstalledItems(items);
                return items;
            }

            return new List<Resource>();
        }

        public async Task<List<Resource>> SearchCloudResourcesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return await GetCloudResourcesAsync();

            var apiResult = await _api.SearchResourcesAsync(query);
            if (apiResult.Success && apiResult.Data != null)
            {
                var items = apiResult.Data.Items.Select(Resource.FromRemote).ToList();
                MarkInstalledItems(items);
                return items;
            }

            return new List<Resource>();
        }

        /// <summary>
        /// 获取单个资源的完整详情
        /// </summary>
        public async Task<Resource?> GetResourceDetailAsync(string id)
        {
            var apiResult = await _api.GetResourceDetailAsync(id);
            if (apiResult.Success && apiResult.Data != null)
                return Resource.FromRemote(apiResult.Data);
            return null;
        }

        /// <summary>获取安装目录</summary>
        public static string GetResourceInstallDir(string resourceId) =>
            Path.Combine(InstallRoot, resourceId);

        // ==================== 集中图标目录（installed 同级 installed_icons/） ====================
        // 参照 Android DownloadManager.kt：每个已安装资源一个以「资源 id」命名的图标文件，
        // 如 {resourceId}.png，专门供编辑页图标选择抽屉读取。

        /// <summary>获取集中图标根目录路径</summary>
        public static string IconDirectory => IconRoot;

        /// <summary>
        /// 获取某资源在集中图标目录中的图标文件完整路径。
        /// 结构：installed_icons/{resourceId}.{ext}（默认 png）。
        /// </summary>
        public static string GetIconPath(string resourceId, string? extension = null)
        {
            var ext = string.IsNullOrEmpty(extension) ? "png" : extension.TrimStart('.');
            return Path.Combine(IconRoot, $"{resourceId}.{ext}");
        }

        /// <summary>删除集中图标目录中该资源的图标文件（以资源 id 命名的文件）</summary>
        public static void DeleteIconForResource(string resourceId)
        {
            try
            {
                var files = Directory.GetFiles(IconRoot, $"{resourceId}.*");
                foreach (var file in files)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try { File.Delete(file); break; }
                        catch { if (i < 2) System.Threading.Thread.Sleep(30); }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 删除集中图标失败(id={resourceId}): {ex.Message}");
            }
        }

        /// <summary>枚举集中图标目录中所有资源的图标文件路径（{resourceId}.{ext}）</summary>
        public static IEnumerable<string> GetAllIconFiles()
        {
            try
            {
                if (!Directory.Exists(IconRoot)) return Enumerable.Empty<string>();
                var images = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
                return Directory.GetFiles(IconRoot)
                    .Where(f => images.Contains(Path.GetExtension(f).ToLowerInvariant()));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 枚举集中图标失败: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 将源图标文件保存为某资源的集中图标（installed_icons/{id}.{ext}）。
        /// 保存前会删除该资源原有的集中图标文件。
        /// 若源文件已是该资源的集中图标目标，则直接复用、不再复制。
        /// 返回新的图标路径；失败返回 null。
        /// </summary>
        public static string? SaveIconForResource(string resourceId, string srcPath)
        {
            try
            {
                if (!File.Exists(srcPath)) return null;

                var ext = Path.GetExtension(srcPath);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                var destPath = GetIconPath(resourceId, ext);

                // 源已是该资源的集中图标目标（例如复用自身），无需复制
                if (Path.GetFullPath(srcPath).Equals(Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    return destPath;

                DeleteIconForResource(resourceId);
                File.Copy(srcPath, destPath, overwrite: true);
                return destPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 保存集中图标失败(id={resourceId}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将源图标文件保存为某资源「自己文件夹」内的图标（installed/{id}/icon.{ext}）。
        /// 仅影响资源自身目录，不会修改公共 installed_icons 目录。
        /// 保存前会删除该资源自己文件夹中原有的 icon.*。
        /// 返回新的图标路径；失败返回 null。
        /// </summary>
        public static string? SaveIconToResourceFolder(string resourceId, string srcPath)
        {
            try
            {
                if (!File.Exists(srcPath)) return null;

                var installDir = GetResourceInstallDir(resourceId);
                Directory.CreateDirectory(installDir);

                var ext = Path.GetExtension(srcPath);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                var destPath = Path.Combine(installDir, $"icon{ext}");

                // 源已是该资源自身文件夹的图标目标（例如复用自身），无需复制
                if (Path.GetFullPath(srcPath).Equals(Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    return destPath;

                // 清理自己文件夹中原有的 icon.*
                foreach (var old in Directory.GetFiles(installDir, "icon.*"))
                {
                    TryDeleteQuietly(old);
                }

                File.Copy(srcPath, destPath, overwrite: true);
                return destPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceService] 保存资源自身图标失败(id={resourceId}): {ex.Message}");
                return null;
            }
        }
    }

    // ==================== 序列化模型 ====================

    /// <summary>已安装资源的 JSON 序列化模型（独立于 API RemoteResource）</summary>
    public class InstalledResourceData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Size { get; set; } = "0";
        public string PublishDate { get; set; } = string.Empty;
        public List<InstalledAudioItemData> AudioList { get; set; } = new();
    }

    /// <summary>已安装音频的 JSON 序列化模型</summary>
    public class InstalledAudioItemData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Src { get; set; } = string.Empty;
        public long Duration { get; set; }
        public int OrderIndex { get; set; }
    }
}
