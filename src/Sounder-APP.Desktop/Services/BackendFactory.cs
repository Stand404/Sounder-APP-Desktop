using System;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 平台后端工厂：根据当前操作系统创建对应的平台实现。
    /// 所有平台特定代码集中在此工厂和各后端子目录中，服务类不再包含 if(OS) 分支。
    /// </summary>
    public static class BackendFactory
    {
        public static IAudioBackend CreateAudioBackend()
        {
            if (OperatingSystem.IsLinux())
                return new Linux.LinuxAudioBackend();
            if (OperatingSystem.IsMacOS())
                return new Mac.MacAudioBackend();
            return new Windows.WindowsAudioBackend();
        }

        public static IInstanceLockBackend CreateInstanceLockBackend()
        {
            if (OperatingSystem.IsLinux())
                return new Linux.LinuxInstanceLockBackend();
            if (OperatingSystem.IsMacOS())
                return new Mac.MacInstanceLockBackend();
            return new Windows.WindowsInstanceLockBackend();
        }

        public static IUrlProtocolBackend CreateUrlProtocolBackend()
        {
            if (OperatingSystem.IsLinux())
                return new Linux.LinuxUrlProtocolBackend();
            if (OperatingSystem.IsMacOS())
                return new Mac.MacUrlProtocolBackend();
            return new Windows.WindowsUrlProtocolBackend();
        }

        public static IDesktopShortcutBackend CreateDesktopShortcutBackend()
        {
            if (OperatingSystem.IsLinux())
                return new Linux.LinuxDesktopShortcutBackend();
            if (OperatingSystem.IsMacOS())
                return new Mac.MacDesktopShortcutBackend();
            return new Windows.WindowsDesktopShortcutBackend();
        }

        public static IIconGeneratorBackend CreateIconGeneratorBackend()
        {
            if (OperatingSystem.IsLinux())
                return new Linux.LinuxIconGeneratorBackend();
            if (OperatingSystem.IsMacOS())
                return new Mac.MacIconGeneratorBackend();
            return new Windows.WindowsIconGeneratorBackend();
        }
    }
}
