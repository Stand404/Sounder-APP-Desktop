using System;
using System.IO;
using NAudio.Wave;
using NLayer.NAudioSupport;

namespace Sounder_APP.Services.Windows
{
    /// <summary>
    /// Windows 音频后端：使用 WaveOutEvent + 原生解码器。
    /// </summary>
    public class WindowsAudioBackend : IAudioBackend
    {
        public IWavePlayer CreatePlayer() => new WaveOutEvent();

        public WaveStream CreateReader(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            return ext switch
            {
                ".wav" => new WaveFileReader(path),
                ".mp3" => new Mp3FileReader(path),
                ".aiff" or ".aif" => new AiffFileReader(path),
                _ => new MediaFoundationReader(path),
            };
        }

        public void Warmup()
        {
            _ = typeof(IWavePlayer);
            _ = typeof(WaveFileReader);
            _ = typeof(Mp3FileReader);
            _ = typeof(AiffFileReader);
            _ = typeof(WaveFormat);
            _ = typeof(WaveOutEvent);
        }
    }
}
