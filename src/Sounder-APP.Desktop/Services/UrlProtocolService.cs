using System;
using System.IO;

#pragma warning disable CA1416 // Registry API 仅在 Windows 上调用，调用处有 OperatingSystem.IsWindows() 守卫

namespace Sounder_APP.Services
{
    /// <summary>
    /// 自定义 URL 协议服务：注册/注销 sounder:// 协议，解析协议 URL。
    /// 平台特定实现（注册表/.desktop）委托给 <see cref="IUrlProtocolBackend"/>。
    /// </summary>
    public static class UrlProtocolService
    {
        private const string ProtocolName = "sounder";

        /// <summary>当前可执行文件路径</summary>
        private static readonly string ExePath =
            Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounder-APP");

        private static readonly IUrlProtocolBackend _backend = BackendFactory.CreateUrlProtocolBackend();

        /// <summary>注册 sounder:// 协议到系统</summary>
        public static void RegisterProtocol() => _backend.RegisterProtocol(ExePath);

        /// <summary>注销 sounder:// 协议</summary>
        public static void UnregisterProtocol() => _backend.UnregisterProtocol();

        /// <summary>
        /// 解析 sounder://play/?resourceId=xxx 格式的 URL，返回 resourceId。
        /// 与 DesktopShortcutService 生成格式保持一致。
        /// </summary>
        public static string? ParsePlayUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            // 去掉前缀 "sounder://"
            var query = url;
            if (url.StartsWith($"{ProtocolName}://", StringComparison.OrdinalIgnoreCase))
                query = url[($"{ProtocolName}://").Length..];

            // sounder://play/?resourceId=xxx → play/?resourceId=xxx
            if (!query.StartsWith("play/?", StringComparison.OrdinalIgnoreCase))
                return null;

            query = query[6..]; // 去掉 "play/?"

            // 解析 query string
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals("resourceId", StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }

            return null;
        }
    }
}
