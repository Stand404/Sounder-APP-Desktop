using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sounder_APP.Services;

namespace Sounder_APP.Models
{
    /// <summary>
    /// 源码生成的 JSON 序列化上下文（API 调用）。
    /// 在 PublishTrimmed=true 时替代反射调用，防止裁剪删除序列化类型。
    /// </summary>
    [JsonSerializable(typeof(ApiListResponse<RemoteResource>))]
    [JsonSerializable(typeof(ApiDetailResponse<RemoteResource>))]
    [JsonSerializable(typeof(RemoteResource))]
    [JsonSerializable(typeof(RemoteAudioItem))]
    [JsonSerializable(typeof(SubmissionListResponse))]
    [JsonSerializable(typeof(SubmissionListData))]
    [JsonSerializable(typeof(SubmissionStatusCounts))]
    [JsonSerializable(typeof(SubmissionDetailResponse))]
    [JsonSerializable(typeof(RemoteSubmission))]
    [JsonSerializable(typeof(PlaybackSettings))]
    [JsonSerializable(typeof(Dictionary<string, PlaybackSettings>))]
    [JsonSourceGenerationOptions(
        PropertyNameCaseInsensitive = true)]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// 本地持久化文件的 JSON 序列化上下文（WriteIndented 用于可读性）。
    /// </summary>
    [JsonSerializable(typeof(InstalledResourceData))]
    [JsonSerializable(typeof(InstalledAudioItemData))]
    [JsonSerializable(typeof(DownloadRecordData))]
    [JsonSerializable(typeof(DownloadedAudioItemData))]
    [JsonSerializable(typeof(List<AudioItem>))]
    [JsonSerializable(typeof(ExportManifest))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
