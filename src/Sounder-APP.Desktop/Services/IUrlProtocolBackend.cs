namespace Sounder_APP.Services
{
    /// <summary>
    /// 平台特定的 URL 协议注册后端。
    /// </summary>
    public interface IUrlProtocolBackend
    {
        void RegisterProtocol(string exePath);
        void UnregisterProtocol();
    }
}
