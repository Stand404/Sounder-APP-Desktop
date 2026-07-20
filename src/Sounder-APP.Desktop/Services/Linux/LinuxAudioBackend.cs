using System;
using System.IO;
using NAudio.Wave;
using NLayer.NAudioSupport;

namespace Sounder_APP.Services.Linux
{
    /// <summary>
    /// Linux 音频后端：使用 LinuxAudioOutput (PulseAudio/ALSA) + NLayer 解码器。
    /// </summary>
    public class LinuxAudioBackend : IAudioBackend
    {
        public IWavePlayer CreatePlayer() => new LinuxAudioOutput();

        public WaveStream CreateReader(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            return ext switch
            {
                ".wav" => new WaveFileReader(path),
                ".mp3" => new Mp3FileReaderBase(
                    File.OpenRead(path),
                    wf => new Mp3FrameDecompressor(wf)),
                ".aiff" or ".aif" => new AiffFileReader(path),
                _ => CreateFallbackReader(path),
            };
        }

        private static WaveStream CreateFallbackReader(string path)
        {
            try
            {
                return new Mp3FileReaderBase(
                    File.OpenRead(path),
                    wf => new Mp3FrameDecompressor(wf));
            }
            catch
            {
                throw new NotSupportedException(
                    $"Linux 上不支持音频格式 '{Path.GetExtension(path)}'，请安装 ffmpeg 或使用 WAV/MP3 格式。");
            }
        }

        public void Warmup()
        {
            _ = typeof(IWavePlayer);
            _ = typeof(WaveFileReader);
            _ = typeof(Mp3FileReader);
            _ = typeof(AiffFileReader);
            _ = typeof(WaveFormat);
            _ = typeof(LinuxAudioOutput);
        }
    }
}
