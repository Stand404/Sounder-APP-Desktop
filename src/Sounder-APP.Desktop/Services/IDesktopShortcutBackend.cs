namespace Sounder_APP.Services
{
    /// <summary>
    /// 平台特定的桌面快捷方式后端。
    /// </summary>
    public interface IDesktopShortcutBackend
    {
        string GetShortcutExtension();
        bool CreateShortcut(string shortcutPath, string displayName, string resourceId, string iconPath);
    }
}
