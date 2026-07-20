using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sounder_APP.Models
{
    /// <summary>
    /// API 列表响应外层
    /// {"message":"操作成功","data":{"total":131,"page":1,"size":10,"data":[...]}}
    /// </summary>
    public class ApiListResponse<T>
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public ApiListData<T>? Data { get; set; }

        public bool Success => Data?.Items != null;
    }

    /// <summary>
    /// API 列表响应内层 data
    /// </summary>
    public class ApiListData<T>
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("data")]
        public List<T> Items { get; set; } = new();
    }

    /// <summary>
    /// 通用 API 响应（单个详情）
    /// {"message":"操作成功","data":{...}}
    /// </summary>
    public class ApiDetailResponse<T>
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        public bool Success => Data != null;
    }

    /// <summary>
    /// API 返回的原始资源数据
    /// </summary>
    public class RemoteResource
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("audioList")]
        public List<RemoteAudioItem> AudioList { get; set; } = new();

        [JsonPropertyName("size")]
        public string Size { get; set; } = "0"; // API 返回字符串如 "24.40KB"

        [JsonPropertyName("publishDate")]
        public string PublishDate { get; set; } = string.Empty;

    }

    /// <summary>
    /// API 返回的原始音频数据
    /// </summary>
    public class RemoteAudioItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Src { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public long Duration { get; set; } // 毫秒
    }

    /// <summary>
    /// 投稿列表 API 响应外层
    /// {"message":"操作成功","data":{"submissions":[...],"total":131,"page":1,"size":10,"statusCounts":{...}}}
    /// </summary>
    public class SubmissionListResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public SubmissionListData? Data { get; set; }

        public bool Success => Data?.Submissions != null;
    }

    public class SubmissionListData
    {
        [JsonPropertyName("submissions")]
        public List<RemoteSubmission> Submissions { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("statusCounts")]
        public SubmissionStatusCounts? StatusCounts { get; set; }
    }

    public class SubmissionStatusCounts
    {
        [JsonPropertyName("pending")]
        public int Pending { get; set; }

        [JsonPropertyName("in_progress")]
        public int InProgress { get; set; }

        [JsonPropertyName("completed")]
        public int Completed { get; set; }
    }

    /// <summary>
    /// 创建投稿 API 响应
    /// {"message":"操作成功","data":{...submission...}}
    /// </summary>
    public class SubmissionDetailResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public RemoteSubmission? Data { get; set; }

        public bool Success => Data != null;
    }
}
