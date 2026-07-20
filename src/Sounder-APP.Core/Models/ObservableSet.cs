using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sounder_APP.Models
{
    /// <summary>
    /// 可监听的字符串「计数集合」— 内部使用 Dictionary&lt;string,int&gt; 跟踪每个 ID 的实例计数。
    /// 同一个 ID 可被多次 Add/Remove，只在 0⇄1 转换时触发 PropertyChanged（对 UI 表现为进入/离开集合）。
    /// 适用于同一音频 ID 可能同时存在多个播放实例（如快速连击叠加模式）的场景。
    /// API 签名兼容原始 HashSet 版本（Contains/Add/Remove/Clear/Count/IsEmpty）。
    /// </summary>
    public class ObservableSet : INotifyPropertyChanged, IEnumerable<string>
    {
        private readonly Dictionary<string, int> _counts = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool Contains(string item) => _counts.ContainsKey(item);

        /// <summary>增加计数。0→1 时触发 PropertyChanged 并返回 true，否则返回 false。</summary>
        public bool Add(string item)
        {
            _counts.TryGetValue(item, out var count);
            count++;
            _counts[item] = count;
            if (count == 1) // 0→1 首次出现
            {
                Notify();
                return true;
            }
            return false;
        }

        /// <summary>减少计数。1→0 时移除条目并触发 PropertyChanged，返回 true。计数仍大于 0 时只减少计数不触发通知。</summary>
        public bool Remove(string item)
        {
            if (!_counts.TryGetValue(item, out var count) || count <= 0)
                return false;

            count--;
            if (count <= 0)
            {
                _counts.Remove(item); // 从集合中消失
                Notify();
                return true;
            }
            _counts[item] = count;    // 还有同 ID 的其他实例
            return true;
        }

        public void Clear()
        {
            if (_counts.Count > 0)
            {
                _counts.Clear();
                Notify();
            }
        }

        /// <summary>字典中当前存在（计数 > 0）的项数。</summary>
        public int Count => _counts.Count;

        /// <summary>是否为完全空集。</summary>
        public bool IsEmpty => _counts.Count == 0;

        public IEnumerator<string> GetEnumerator() => _counts.Keys.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _counts.Keys.GetEnumerator();

        private void Notify([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
