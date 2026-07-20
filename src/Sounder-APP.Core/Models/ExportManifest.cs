using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sounder_APP.Models
{
    /// <summary>
    /// 资源包导出/导入的 manifest.json 序列化模型
    /// </summary>
    public class ExportManifest
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("exportDate")]
        public string? ExportDate { get; set; }

        [JsonPropertyName("resource")]
        public ExportResourceMeta? Resource { get; set; }

        [JsonPropertyName("files")]
        public ExportFiles? Files { get; set; }

        public class ExportResourceMeta
        {
            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; } = string.Empty;

            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("audioItems")]
            public List<ExportAudioMeta> AudioItems { get; set; } = new();
        }

        public class ExportAudioMeta
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("durationMs")]
            public long DurationMs { get; set; }
        }

        public class ExportFiles
        {
            [JsonPropertyName("icon")]
            public string? Icon { get; set; }

            [JsonPropertyName("audios")]
            public List<string?> Audios { get; set; } = new();
        }
    }
}
