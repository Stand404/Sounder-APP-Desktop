#if MACOS
using Avalonia.Controls;
using Avalonia.Platform;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sounder_APP.Services.Mac
{
    /// <summary>
    /// macOS 窗口透明辅助类。
    /// 通过 ObjC P/Invoke 设置 NSWindow.isOpaque = NO 和 backgroundColor = clearColor，
    /// 解决 macOS 上透明窗口的圆角和阴影渲染问题。
    /// </summary>
    internal static class MacWindowTransparency
    {
        public static void Configure(Window window)
        {
            window.TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            window.Opened += OnWindowOpened;
        }

        private static void OnWindowOpened(object? sender, EventArgs e)
        {
            if (sender is not Window window) return;
            window.Opened -= OnWindowOpened;

            try
            {
                var platformHandle = window.TryGetPlatformHandle();
                if (platformHandle == null || platformHandle.Handle == IntPtr.Zero) return;

                MakeNSWindowTransparent(platformHandle.Handle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MacWindowTransparency] 配置透明窗口失败: {ex.Message}");
            }
        }

        private static void MakeNSWindowTransparent(IntPtr nsWindow)
        {
            var selSetOpaque = sel_registerName("setOpaque:");
            var selSetBackgroundColor = sel_registerName("setBackgroundColor:");
            var selClearColor = sel_registerName("clearColor");

            var nsColorClass = objc_getClass("NSColor");

            // [window setOpaque:NO]
            objc_msgSend_bool(nsWindow, selSetOpaque, false);

            // [NSColor clearColor]
            var clearColor = objc_msgSend_IntPtr(nsColorClass, selClearColor);

            // [window setBackgroundColor:[NSColor clearColor]]
            objc_msgSend_IntPtr_IntPtr(nsWindow, selSetBackgroundColor, clearColor);
        }

        // ---- ObjC Runtime P/Invoke ----

        [DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_bool(IntPtr self, IntPtr op, bool arg);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_IntPtr(IntPtr self, IntPtr op);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr self, IntPtr op, IntPtr arg);
    }
}
#endif
