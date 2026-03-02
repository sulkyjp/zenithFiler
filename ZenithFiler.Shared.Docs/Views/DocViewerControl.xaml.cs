using Markdig;
using Markdig.Wpf;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using MahApps.Metro.IconPacks;
using ZenithFiler.Helpers;

using WpfMarkdown = Markdig.Wpf.Markdown;

namespace ZenithFiler
{
    /// <summary>
    /// MANUAL.md / CHANGELOG.md などの Markdown ドキュメントを、
    /// 左ナビ（目次）＋右本文のレイアウトで表示する共通ビューア。
    /// </summary>
    public partial class DocViewerControl : UserControl
    {
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers(Markdig.Extensions.AutoIdentifiers.AutoIdentifierOptions.GitHub)
            .Build();

        private ManualViewModel? _viewModel;
        private bool _isUpdatingFromScroll;
        private readonly DispatcherTimer _scrollDebounceTimer;

        private double _scrollPositionManual;
        private double _scrollPositionChangelog;
        private bool _lastIsChangelog;

        private readonly DispatcherTimer _searchDebounceTimer;

        private DocumentSearchHelper? _searchHelper;

        public DocViewerControl()
        {
            InitializeComponent();

            _scrollDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _scrollDebounceTimer.Tick += ScrollDebounceTimer_Tick;

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            Loaded += DocViewerControl_Loaded;
            DataContextChanged += DocViewerControl_DataContextChanged;
        }

        private void DocViewerControl_Loaded(object sender, RoutedEventArgs e)
        {
            AttachViewModel(DataContext as ManualViewModel);
        }

        private void DocViewerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachViewModel(e.NewValue as ManualViewModel);
        }

        private void AttachViewModel(ManualViewModel? vm)
        {
            if (ReferenceEquals(_viewModel, vm))
            {
                return;
            }

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.SearchNextRequested -= ViewModel_SearchNextRequested;
                _viewModel.SearchPreviousRequested -= ViewModel_SearchPreviousRequested;
            }

            _viewModel = vm;
            _searchHelper = null;

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.SearchNextRequested += ViewModel_SearchNextRequested;
                _viewModel.SearchPreviousRequested += ViewModel_SearchPreviousRequested;
                RenderMarkdown();
            }
            else
            {
                DocViewer.Document = null;
            }
        }

        private void ViewModel_SearchNextRequested(object? sender, EventArgs e)
        {
            _searchHelper?.NavigateMatch(1);
        }

        private void ViewModel_SearchPreviousRequested(object? sender, EventArgs e)
        {
            _searchHelper?.NavigateMatch(-1);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManualViewModel.MarkdownText))
            {
                RenderMarkdown();
                if (!string.IsNullOrEmpty(_viewModel?.SearchQuery))
                {
                    _searchDebounceTimer.Stop();
                    _searchDebounceTimer.Start();
                }
            }
            else if (e.PropertyName == nameof(ManualViewModel.IsChangelog))
            {
                SaveCurrentScrollPositionTo(_lastIsChangelog);
                _lastIsChangelog = _viewModel?.IsChangelog ?? false;
            }
            else if (e.PropertyName == nameof(ManualViewModel.SearchQuery))
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            }
        }

        private void SaveCurrentScrollPositionTo(bool forChangelog)
        {
            var sv = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(DocViewer);
            if (sv == null) return;
            double offset = sv.VerticalOffset;
            if (forChangelog)
                _scrollPositionChangelog = offset;
            else
                _scrollPositionManual = offset;
        }

        private void RestoreScrollPositionForCurrentMode()
        {
            if (_viewModel == null) return;
            double target = _viewModel.IsChangelog ? _scrollPositionChangelog : _scrollPositionManual;
            var sv = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(DocViewer);
            if (sv == null) return;
            target = Math.Max(0, Math.Min(sv.ScrollableHeight, target));
            sv.ScrollToVerticalOffset(target);
        }

        private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            if (_viewModel != null && DocViewer.Document != null)
            {
                EnsureSearchHelper();
                _searchHelper?.PerformSearch(DocViewer.Document, _viewModel.SearchQuery);
            }
        }

        private void EnsureSearchHelper()
        {
            if (_viewModel == null) return;
            if (_searchHelper != null) return;

            _searchHelper = new DocumentSearchHelper(
                _viewModel,
                SelectTocItem,
                UpdateActiveSection,
                Dispatcher);
        }

        private void DocViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            const double scrollAmountPerTick = 80.0;
            var sv = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(DocViewer);
            if (sv == null) return;

            double delta = -(e.Delta / 120.0) * scrollAmountPerTick;
            double newOffset = sv.VerticalOffset + delta;
            newOffset = Math.Max(0, Math.Min(sv.ScrollableHeight, newOffset));
            sv.ScrollToVerticalOffset(newOffset);
            e.Handled = true;
        }

        private void RenderMarkdown()
        {
            if (_viewModel is null)
            {
                DocViewer.Document = null;
                return;
            }

            var markdown = _viewModel.MarkdownText ?? string.Empty;

            var doc = WpfMarkdown.ToFlowDocument(markdown, _markdownPipeline);

            PostProcessDocument(doc);

            DocViewer.Document = doc;

            DocViewer.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(DocViewer_ScrollChanged));
            DocViewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(DocViewer_ScrollChanged));

            Dispatcher.BeginInvoke(new Action(RestoreScrollPositionForCurrentMode), DispatcherPriority.Loaded);
        }

        private void PostProcessDocument(FlowDocument doc)
        {
            foreach (var block in doc.Blocks.ToList())
            {
                if (block is Paragraph p)
                {
                    var level = GetHeadingLevel(p);
                    if (level > 0)
                    {
                        var text = GetText(p);
                        var id = ManualViewModel.GenerateAnchorId(text);
                        p.Tag = id;

                        if (level == 3)
                        {
                            EnhanceCategoryHeading(p, text);
                        }
                        else if (level == 2)
                        {
                            EnhanceVersionHeading(p);
                        }
                    }
                }
            }
        }

        private void EnhanceVersionHeading(Paragraph p)
        {
            p.FontWeight = FontWeights.SemiBold;
            p.FontSize = 22;
        }

        private int GetHeadingLevel(Paragraph p)
        {
            var size = p.FontSize;
            if (size >= 23) return 1;
            if (size >= 19) return 2;
            if (size >= 17) return 3;
            return 0;
        }

        private void EnhanceCategoryHeading(Paragraph paragraph, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var label = text.Trim();
            PackIconLucideKind? iconKind = null;

            if (string.Equals(label, "Added", StringComparison.OrdinalIgnoreCase))
            {
                iconKind = PackIconLucideKind.Plus;
            }
            else if (string.Equals(label, "Fixed", StringComparison.OrdinalIgnoreCase))
            {
                iconKind = PackIconLucideKind.Wrench;
            }
            else if (string.Equals(label, "Changed", StringComparison.OrdinalIgnoreCase))
            {
                iconKind = PackIconLucideKind.Pencil;
            }

            if (iconKind is null)
            {
                return;
            }

            paragraph.Inlines.Clear();

            Brush? accentBrush = null;
            try
            {
                accentBrush = (Brush?)Application.Current.FindResource("AccentBrush");
            }
            catch (Exception ex)
            {
                DocViewerLogHelper.TryLog(ex);
            }

            var icon = new PackIconLucide
            {
                Kind = iconKind.Value,
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (accentBrush is not null)
            {
                icon.Foreground = accentBrush;
                paragraph.Foreground = accentBrush;
            }

            paragraph.Inlines.Add(new InlineUIContainer(icon));
            paragraph.Inlines.Add(new Run(label));
        }

        private string GetText(Paragraph p)
        {
            return new TextRange(p.ContentStart, p.ContentEnd).Text.Trim();
        }

        private void TocListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFromScroll) return;
            if (_viewModel?.SelectedTocItem is null) return;
            if (DocViewer.Document == null) return;

            var id = _viewModel.SelectedTocItem.Id;
            var targetBlock = FindBlockByTag(DocViewer.Document, id);

            if (targetBlock != null)
            {
                targetBlock.BringIntoView();
            }
        }

        private FrameworkContentElement? FindBlockByTag(FlowDocument doc, string id)
        {
            foreach (var block in doc.Blocks)
            {
                if (block is Paragraph p && p.Tag is string tag && tag == id)
                {
                    return p;
                }
            }
            return null;
        }

        private void DocViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _scrollDebounceTimer.Stop();
            _scrollDebounceTimer.Start();
        }

        private void ScrollDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _scrollDebounceTimer.Stop();
            UpdateActiveSection();
        }

        private void UpdateActiveSection()
        {
            if (_viewModel is null) return;
            if (DocViewer.Document == null) return;

            var doc = DocViewer.Document;
            var headingParagraphs = doc.Blocks
                .OfType<Paragraph>()
                .Where(p => p.Tag is string id && !string.IsNullOrEmpty(id))
                .ToList();

            if (headingParagraphs.Count == 0) return;

            Paragraph? current = null;
            const double threshold = 80;

            foreach (var h in headingParagraphs)
            {
                try
                {
                    var rect = h.ContentStart.GetCharacterRect(LogicalDirection.Forward);

                    if (rect.Top > threshold)
                    {
                        break;
                    }

                    current = h;
                }
                catch (Exception ex)
                {
                    DocViewerLogHelper.TryLog(ex);
                }
            }

            current ??= headingParagraphs.FirstOrDefault();

            if (current?.Tag is string currentId && !string.IsNullOrEmpty(currentId))
            {
                SelectTocItem(currentId);
            }
        }

        private void SelectTocItem(string id)
        {
            if (_viewModel is null) return;

            var item = _viewModel.TocItems.FirstOrDefault(x => x.Id == id);
            if (item != null && item != _viewModel.SelectedTocItem)
            {
                _isUpdatingFromScroll = true;
                _viewModel.SelectedTocItem = item;
                TocListBox.ScrollIntoView(item);
                _isUpdatingFromScroll = false;
            }
        }
    }
}
