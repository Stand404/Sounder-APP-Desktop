using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 图片两级缓存（内存 + 磁盘），URL → Bitmap
    /// </summary>
    public static class ImageCache
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> MemoryCache = new();
        private static readonly HttpClient Client = new();
        private static readonly string CacheDir;

        public static string GetCacheDirectory() => CacheDir;

        static ImageCache()
        {
            CacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sounder-APP", "cache");
            Directory.CreateDirectory(CacheDir);
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
            // 忽略查询参数后取扩展名，默认 .jpg
            var path = url.Split('?')[0];
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext) && ext.Length <= 8)
                return ext.ToLowerInvariant();
            return ".jpg";
        }

        /// <summary>
        /// 同步加载（仅走内存和磁盘缓存），不阻塞 UI 线程做网络请求。
        /// 未缓存时返回 null，由异步方法补充。
        /// </summary>
        public static Bitmap? Load(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            // 1. 内存缓存
            if (MemoryCache.TryGetValue(url, out var cached))
                return cached;

            var cachePath = GetCachePath(url);

            // 2. 磁盘缓存
            if (File.Exists(cachePath))
            {
                try
                {
                    var bitmap = new Bitmap(cachePath);
                    MemoryCache[url] = bitmap;
                    return bitmap;
                }
                catch
                {
                    File.Delete(cachePath);
                }
            }

            return null; // 不阻塞 UI 线程，返回 null 让降级显示
        }

        /// <summary>
        /// 异步加载（网络下载 + 磁盘缓存），不阻塞调用线程。
        /// 支持本地文件路径（以 file:// 开头或存在的文件路径）。
        /// </summary>
        public static async Task<Bitmap?> LoadAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            // 优先走缓存
            var cached = Load(url);
            if (cached != null) return cached;

            // 0. 本地文件路径 → 直接加载
            if (File.Exists(url))
            {
                try
                {
                    var bitmap = new Bitmap(url);
                    MemoryCache[url] = bitmap;
                    return bitmap;
                }
                catch
                {
                    MemoryCache.TryAdd(url, null);
                    return null;
                }
            }

            var cachePath = GetCachePath(url);

            // 3. 网络下载（异步非阻塞）
            try
            {
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync()
                    .ConfigureAwait(false);

                File.WriteAllBytes(cachePath, bytes);

                var bitmap = new Bitmap(new MemoryStream(bytes));
                MemoryCache[url] = bitmap;
                return bitmap;
            }
            catch
            {
                MemoryCache.TryAdd(url, null);
                return null;
            }
        }

        /// <summary>
        /// 计算磁盘缓存总大小（字节）
        /// </summary>
        public static long GetCacheSize()
        {
            try
            {
                if (!Directory.Exists(CacheDir)) return 0;
                return Directory.EnumerateFiles(CacheDir).Sum(f => new FileInfo(f).Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 清除所有缓存（内存 + 磁盘）
        /// </summary>
        public static void ClearAll()
        {
            MemoryCache.Clear();
            try
            {
                if (Directory.Exists(CacheDir))
                    Directory.Delete(CacheDir, recursive: true);
                Directory.CreateDirectory(CacheDir);
            }
            catch { }
        }

        /// <summary>
        /// 使指定路径的缓存失效（本地文件被覆盖后强制重新加载）。
        /// 同一个路径第二次写入新内容时，若不失效会返回旧 Bitmap。
        /// </summary>
        public static void Invalidate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            MemoryCache.TryRemove(url, out _);
            try
            {
                var cachePath = GetCachePath(url);
                if (File.Exists(cachePath)) File.Delete(cachePath);
            }
            catch { }
        }
    }
}
