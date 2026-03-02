using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ZenithFiler;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZenithFiler.Helpers
{
    /// <summary>
    /// FlowDocument 内のテキスト検索・ハイライト・目次同期を行うヘルパー。
    /// DocViewerControl の検索ロジックを分離したもの。
    /// </summary>
    public class DocumentSearchHelper
    {
        private readonly ManualViewModel _viewModel;
        private readonly Action<string> _selectTocItem;
        private readonly Action _updateActiveSection;
        private readonly Dispatcher _dispatcher;

        private readonly List<TextRange> _searchMatches = new();
        private int _currentMatchIndex = -1;

        public DocumentSearchHelper(
            ManualViewModel viewModel,
            Action<string> selectTocItem,
            Action updateActiveSection,
            Dispatcher dispatcher)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _selectTocItem = selectTocItem ?? throw new ArgumentNullException(nameof(selectTocItem));
            _updateActiveSection = updateActiveSection ?? throw new ArgumentNullException(nameof(updateActiveSection));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// ドキュメント内を検索し、ヒット箇所をハイライトする。
        /// </summary>
        public void PerformSearch(FlowDocument document, string query)
        {
            if (document == null) return;

            _searchMatches.Clear();
            _currentMatchIndex = -1;

            var documentRange = new TextRange(document.ContentStart, document.ContentEnd);
            documentRange.ApplyPropertyValue(TextElement.BackgroundProperty, null);
            var textBrush = Application.Current.TryFindResource("TextBrush") as Brush ?? Brushes.Black;
            documentRange.ApplyPropertyValue(TextElement.ForegroundProperty, textBrush);

            if (string.IsNullOrWhiteSpace(query))
            {
                UpdateSearchStatus();
                return;
            }

            TextPointer current = document.ContentStart;

            while (current != null)
            {
                if (current.CompareTo(document.ContentEnd) >= 0) break;

                if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = current.GetTextInRun(LogicalDirection.Forward);
                    int index = textRun.IndexOf(query, StringComparison.OrdinalIgnoreCase);

                    if (index >= 0)
                    {
                        TextPointer start = current.GetPositionAtOffset(index);
                        TextPointer end = start.GetPositionAtOffset(query.Length);

                        var range = new TextRange(start, end);
                        range.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
                        range.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);

                        _searchMatches.Add(range);
                        current = end;
                    }
                    else
                    {
                        current = current.GetNextContextPosition(LogicalDirection.Forward);
                    }
                }
                else
                {
                    current = current.GetNextContextPosition(LogicalDirection.Forward);
                }
            }

            if (_searchMatches.Any())
            {
                NavigateMatch(1);
            }
            else
            {
                UpdateSearchStatus();
            }
        }

        /// <summary>
        /// 検索ヒット間を移動する。direction: 1=次, -1=前。
        /// </summary>
        public void NavigateMatch(int direction)
        {
            if (_searchMatches.Count == 0) return;

            if (_currentMatchIndex >= 0 && _currentMatchIndex < _searchMatches.Count)
            {
                var oldRange = _searchMatches[_currentMatchIndex];
                oldRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
                oldRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);
            }

            if (_currentMatchIndex == -1 && direction > 0)
            {
                _currentMatchIndex = 0;
            }
            else
            {
                _currentMatchIndex += direction;
                if (_currentMatchIndex >= _searchMatches.Count) _currentMatchIndex = 0;
                if (_currentMatchIndex < 0) _currentMatchIndex = _searchMatches.Count - 1;
            }

            var newRange = _searchMatches[_currentMatchIndex];
            var accentBrush = Application.Current.TryFindResource("AccentBrush") as Brush ?? Brushes.Orange;
            var invertedTextBrush = Application.Current.TryFindResource("InvertedTextBrush") as Brush ?? Brushes.White;

            newRange.ApplyPropertyValue(TextElement.BackgroundProperty, accentBrush);
            newRange.ApplyPropertyValue(TextElement.ForegroundProperty, invertedTextBrush);

            if (newRange.Start.Parent is FrameworkContentElement element)
            {
                element.BringIntoView();
            }

            UpdateSearchStatus();
            SyncTocWithMatch(newRange);
        }

        /// <summary>
        /// 検索結果の状態を ViewModel に反映する。
        /// </summary>
        public void UpdateSearchStatus()
        {
            _viewModel.HasSearchMatches = _searchMatches.Any();

            if (_searchMatches.Any())
            {
                _viewModel.SearchStatusText = $"{_currentMatchIndex + 1} / {_searchMatches.Count} items";
            }
            else
            {
                _viewModel.SearchStatusText = "";
                if (!string.IsNullOrEmpty(_viewModel.SearchQuery))
                {
                    _viewModel.SearchStatusText = "No results";
                }
            }
        }

        /// <summary>
        /// マッチした箇所の親見出しに合わせて目次を同期する。
        /// </summary>
        public void SyncTocWithMatch(TextRange range)
        {
            DependencyObject curr = range.Start.Parent;
            while (curr != null)
            {
                if (curr is Paragraph p && p.Tag is string id && !string.IsNullOrEmpty(id))
                {
                    _selectTocItem(id);
                    return;
                }

                if (curr is FrameworkContentElement fce)
                {
                    curr = fce.Parent;
                }
                else if (curr is FrameworkElement fe)
                {
                    curr = fe.Parent;
                }
                else
                {
                    break;
                }
            }

            _dispatcher.BeginInvoke(new Action(() => _updateActiveSection()), DispatcherPriority.Background);
        }
    }
}
