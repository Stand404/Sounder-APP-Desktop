using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Sounder_APP.Models;
using Sounder_APP.Services;

namespace Sounder_APP.ViewModels
{
    /// <summary>
    /// 资源包导入/导出的核心逻辑（manifest 构建、文件打包、解压解析等）。
    /// 与 UI/ViewModel 状态无关，可独立使用。
    /// </summary>
    public static class ResourcePackageHelper
    {
        /// <summary>
        /// 导出资源包到指定 zip 路径。
        /// 音频文件放在 audios/ 子目录，manifest 的 audios 数组只写文件名（无文件夹前缀），
        /// manifest.json 使用 UTF-8 无 BOM 编码。
        /// </summary>
        public static async Task ExportResourcePackageAsync(Resource resource, string destZipPath)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var tempDir = Path.Combine(Path.GetTempPath(), $"sounder_export_{timestamp}");
            try
            {
                Directory.CreateDirectory(tempDir);

                // 1. 导出图标到临时目录根
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

                // 2. 导出音频文件到 audios/ 子目录（manifest 只写文件名，不写路径前缀）
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
                            audioFileNames.Add(fileName);
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

                // 3. 写入 manifest.json（UTF-8 无 BOM）
                var manifest = new ExportManifest
                {
                    Version = 1,
                    ExportDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Resource = new ExportManifest.ExportResourceMeta
                    {
                        DisplayName = resource.DisplayName,
                        Description = resource.Description,
                        AudioItems = audioMetas
                    },
                    Files = new ExportManifest.ExportFiles
                    {
                        Icon = iconFileName,
                        Audios = audioFileNames
                    }
                };

                var manifestJson = JsonSerializer.Serialize(manifest, SettingsJsonContext.Default.ExportManifest);
                var manifestPath = Path.Combine(tempDir, "manifest.json");
                await File.WriteAllTextAsync(manifestPath, manifestJson, new UTF8Encoding(false));
                Debug.WriteLine("[Export] manifest.json 已写入");

                // 4. 打包为 zip
                if (File.Exists(destZipPath)) File.Delete(destZipPath);
                ZipFile.CreateFromDirectory(tempDir, destZipPath);
                Debug.WriteLine($"[Export] ZIP 已保存到: {destZipPath}");
            }
            finally
            {
                CleanupTempDir(tempDir);
            }
        }

        /// <summary>导入结果</summary>
        public class ImportResult
        {
            /// <summary>解析后的 manifest 数据</summary>
            public ExportManifest Manifest { get; set; } = null!;

            /// <summary>manifest.json 所在的内容根目录（音频/图标文件均相对于此路径）</summary>
            public string ContentRoot { get; set; } = string.Empty;

            /// <summary>临时解压根目录（用于最终清理）</summary>
            public string TempDir { get; set; } = string.Empty;
        }

        /// <summary>
        /// 导入（解压并解析）资源包 zip 文件。
        /// 返回 ImportResult 后调用方应尽快处理文件并调用 <see cref="CleanupTempDir"/> 清理。
        /// </summary>
        public static async Task<ImportResult> ImportResourcePackageAsync(string zipPath)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var tempDir = Path.Combine(Path.GetTempPath(), $"sounder_import_{timestamp}");

            ZipFile.ExtractToDirectory(zipPath, tempDir);
            Debug.WriteLine($"[Import] 已解压到临时目录: {tempDir}");

            var contentRoot = FindContentRoot(tempDir);

            var manifestPath = Path.Combine(contentRoot, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                CleanupTempDir(tempDir);
                throw new InvalidOperationException(LocalizationService.Instance.Get("invalid_package_no_manifest"));
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize(manifestJson, SettingsJsonContext.Default.ExportManifest);
            if (manifest == null || manifest.Version < 1 || manifest.Resource == null)
            {
                CleanupTempDir(tempDir);
                throw new InvalidOperationException(LocalizationService.Instance.Get("invalid_package_incomplete_manifest"));
            }

            Debug.WriteLine($"[Import] manifest 读取成功: {manifest.Resource.DisplayName}");

            return new ImportResult
            {
                Manifest = manifest,
                ContentRoot = contentRoot,
                TempDir = tempDir
            };
        }

        // ===== 辅助方法 =====

        /// <summary>清理临时目录（忽略错误）</summary>
        public static void CleanupTempDir(string dir)
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
        public static string GetFileExtension(string filePath, string defaultExt)
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
        public static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "resource" : sanitized;
        }

        /// <summary>递归拷贝目录</summary>
        public static void CopyDirectoryRecursive(string sourceDir, string destDir)
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

        /// <summary>
        /// 在内容根目录的 audios/ 子目录下查找文件。
        /// 返回实际存在的完整路径，未找到返回 null。
        /// </summary>
        public static string? FindFileInContentRoot(string contentRoot, string fileName)
        {
            var inAudios = Path.Combine(contentRoot, "audios", fileName);
            if (File.Exists(inAudios))
                return inAudios;
            return null;
        }

        /// <summary>在解压目录中定位 manifest.json 所在的内容根目录</summary>
        private static string FindContentRoot(string tempDir)
        {
            if (File.Exists(Path.Combine(tempDir, "manifest.json")))
                return tempDir;

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

            return tempDir;
        }
    }
}
