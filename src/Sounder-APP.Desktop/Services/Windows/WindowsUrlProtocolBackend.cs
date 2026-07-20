using System;
using System.Diagnostics;

#pragma warning disable CA1416

namespace Sounder_APP.Services.Windows
{
    /// <summary>
    /// Windows URL 协议后端：通过注册表注册 sounder:// 协议。
    /// </summary>
    public class WindowsUrlProtocolBackend : IUrlProtocolBackend
    {
        private const string ProtocolName = "sounder";
        private const string ProtocolDescription = "URL:Sounder Protocol";

        public void RegisterProtocol(string exePath)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
                if (key == null) return;

                key.SetValue("", ProtocolDescription);
                key.SetValue("URL Protocol", "");

                using var iconKey = key.CreateSubKey("DefaultIcon");
                iconKey?.SetValue("", $"\"{exePath}\",0");

                using var cmdKey = key.CreateSubKey(@"shell\open\command");
                cmdKey?.SetValue("", $"\"{exePath}\" \"%1\"");

                Debug.WriteLine($"[UrlProtocol] 协议 {ProtocolName}:// 已注册 (Windows)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlProtocol] 注册失败: {ex.Message}");
            }
        }

        public void UnregisterProtocol()
        {
            try
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProtocolName}", false);
                Debug.WriteLine($"[UrlProtocol] 协议 {ProtocolName}:// 已注销 (Windows)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlProtocol] 注销失败: {ex.Message}");
            }
        }
    }
}
