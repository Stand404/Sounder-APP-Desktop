using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sounder_APP.Models
{
    /// <summary>
    /// 投稿状态
    /// </summary>
    public enum SubmissionStatus
    {
        Pending,      // 待审核
        InProgress,   // 进行中
        Completed     // 已完成
    }

    /// <summary>
    /// 投稿领域模型
    /// </summary>
    public class Submission : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int Id { get; set; }

        /// <summary>资源包全称</summary>
        public string ResourceName { get; set; } = string.Empty;

        /// <summary>APP简称</summary>
        public string AppName { get; set; } = string.Empty;

        /// <summary>简述</summary>
        public string Brief { get; set; } = string.Empty;

        /// <summary>平台（哔哩/抖音/快手/粉丝群）</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>平台账号</summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>状态侧边颜色（用于卡片左侧色条）</summary>
        public string StatusSideColor => Status switch
        {
            SubmissionStatus.Pending => "#F59E0B",    // 琥珀色
            SubmissionStatus.InProgress => "#5DA3E8",  // 蓝色
            SubmissionStatus.Completed => "#10B981",   // 绿色
            _ => "#5DA3E8"
        };

        /// <summary>昵称</summary>
        public string Nickname { get; set; } = string.Empty;

        /// <summary>图片来源（游戏截图/动漫截图/互联网/视频截图）</summary>
        public string ImageSource { get; set; } = string.Empty;

        /// <summary>图片来源链接</summary>
        public string ImageSourceLink { get; set; } = string.Empty;

        /// <summary>声音来源（游戏录制/视频链接）</summary>
        public string VoiceSource { get; set; } = string.Empty;

        /// <summary>声音来源链接</summary>
        public string VoiceSourceLink { get; set; } = string.Empty;

        /// <summary>网盘文件链接</summary>
        public string FileLink { get; set; } = string.Empty;

        /// <summary>投稿状态</summary>
        public SubmissionStatus Status { get; set; }

        /// <summary>计划日期（用于分组）</summary>
        public string PlannedDate { get; set; } = string.Empty;

        /// <summary>审核状态</summary>
        public string ReviewStatus { get; set; } = string.Empty;

        /// <summary>创建时间</summary>
        public string CreatedAt { get; set; } = string.Empty;

        // ===== 展示用计算属性 =====

        /// <summary>状态文本</summary>
        public string StatusText => Status switch
        {
            SubmissionStatus.Pending => "待审核",
            SubmissionStatus.InProgress => "进行中",
            SubmissionStatus.Completed => "已完成",
            _ => "未知"
        };

        /// <summary>平台显示文本</summary>
        public string PlatformDisplay => string.IsNullOrEmpty(Platform) ? "" : Platform;

        /// <summary>格式化的创建时间</summary>
        public string CreatedAtDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(CreatedAt)) return string.Empty;
                if (DateTime.TryParse(CreatedAt, out var dt))
                    return dt.ToString("yyyy-MM-dd HH:mm");
                return CreatedAt;
            }
        }

        /// <summary>是否有图片来源链接</summary>
        public bool HasImageSourceLink => !string.IsNullOrEmpty(ImageSourceLink);

        /// <summary>是否有声音来源链接</summary>
        public bool HasVoiceSourceLink => !string.IsNullOrEmpty(VoiceSourceLink);

        /// <summary>是否有文件链接</summary>
        public bool HasFileLink => !string.IsNullOrEmpty(FileLink);

        /// <summary>是否有平台账号</summary>
        public bool HasPlatformId => !string.IsNullOrEmpty(PlatformId);

        /// <summary>从 API 数据创建领域模型</summary>
        public static Submission FromRemote(RemoteSubmission remote)
        {
            return new Submission
            {
                Id = remote.Id,
                ResourceName = remote.ResourceName,
                AppName = remote.AppName,
                Brief = remote.Brief,
                Platform = remote.Platform,
                PlatformId = remote.PlatformId,
                Nickname = remote.Nickname,
                ImageSource = remote.ImageSource,
                ImageSourceLink = remote.ImageSourceLink,
                VoiceSource = remote.VoiceSource,
                VoiceSourceLink = remote.VoiceSourceLink,
                FileLink = remote.FileLink,
                Status = MapStatus(remote.Status),
                PlannedDate = remote.PlannedDate,
                ReviewStatus = remote.ReviewStatus,
                CreatedAt = remote.CreatedAt
            };
        }

        private static SubmissionStatus MapStatus(string status)
        {
            return status switch
            {
                "pending" => SubmissionStatus.Pending,
                "in_progress" => SubmissionStatus.InProgress,
                "completed" => SubmissionStatus.Completed,
                _ => SubmissionStatus.Pending
            };
        }
    }

    /// <summary>
    /// API 返回的原始投稿数据
    /// </summary>
    public class RemoteSubmission
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("resourceName")]
        public string ResourceName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("appName")]
        public string AppName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("brief")]
        public string Brief { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("platformId")]
        public string PlatformId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("imageSource")]
        public string ImageSource { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("imageSourceLink")]
        public string ImageSourceLink { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("voiceSource")]
        public string VoiceSource { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("voiceSourceLink")]
        public string VoiceSourceLink { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("fileLink")]
        public string FileLink { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("plannedDate")]
        public string PlannedDate { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("reviewStatus")]
        public string ReviewStatus { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}
