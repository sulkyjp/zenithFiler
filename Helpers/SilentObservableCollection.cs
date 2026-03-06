using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ZenithFiler
{
    /// <summary>
    /// 変更通知を一時的に抑制できる ObservableCollection。
    /// ReplaceAll で全アイテムを一括置換し、CollectionChanged を Reset 1 回だけ発火する。
    /// これにより WPF のレイアウト再計算（MeasureOverride 等）の連鎖を防ぎ、
    /// StackOverflowException を回避する。
    /// </summary>
    public class SilentObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification;

        /// <summary>
        /// 全アイテムを一括置換する。CollectionChanged は Reset を 1 回だけ発火する。
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            _suppressNotification = true;
            try
            {
                Items.Clear();
                foreach (var item in items)
                    Items.Add(item);
            }
            finally
            {
                _suppressNotification = false;
            }
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// 複数アイテムを一括追加する。CollectionChanged は Reset を 1 回だけ発火する。
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            _suppressNotification = true;
            try
            {
                foreach (var item in items)
                    Items.Add(item);
            }
            finally
            {
                _suppressNotification = false;
            }
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }
    }
}
