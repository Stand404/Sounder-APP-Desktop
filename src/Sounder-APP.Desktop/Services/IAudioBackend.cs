using NAudio.Wave;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 平台特定的音频后端：创建播放器、解码器、预加载。
    /// </summary>
    public interface IAudioBackend
    {
        IWavePlayer CreatePlayer();
        WaveStream CreateReader(string path);
        void Warmup();
    }
}
