namespace Sounder_APP.Models
{
    /// <summary>播放模式</summary>
    public enum PlayMode
    {
        /// <summary>叠加模式：每次播放创建独立实例，新老音频同时播放</summary>
        Overlay,
        /// <summary>替换模式：停止全部后播放当前音频</summary>
        Replace,
        /// <summary>循环模式：根据 LoopMode 决定单曲循环或列表循环</summary>
        Loop
    }

    /// <summary>循环模式下的子模式</summary>
    public enum LoopMode
    {
        /// <summary>单曲循环：循环播放当前音频</summary>
        SingleLoop,
        /// <summary>列表循环：按顺序循环播放列表中的所有音频</summary>
        ListLoop
    }

    /// <summary>播放顺序</summary>
    public enum OrderMode
    {
        /// <summary>顺序播放</summary>
        Order,
        /// <summary>随机播放</summary>
        Random
    }

    /// <summary>每个资源的播放设置（独立持久化）</summary>
    public class PlaybackSettings
    {
        public PlayMode PlayMode { get; set; } = PlayMode.Overlay;
        public LoopMode LoopMode { get; set; } = LoopMode.SingleLoop;
        public OrderMode OrderMode { get; set; } = OrderMode.Order;
        public int CurrentAudioIndex { get; set; } // 顺序播放的当前索引（持久化）

        public static PlaybackSettings Default => new();
    }
}
