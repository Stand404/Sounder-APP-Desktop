using System.Threading.Tasks;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 平台特定的图标生成后端。
    /// </summary>
    public interface IIconGeneratorBackend
    {
        string GetIconFileName();
        string? GenerateIcon(string sourcePath, string destPath);
    }
}
