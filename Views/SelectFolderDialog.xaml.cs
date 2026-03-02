using System;
using System.IO;
using System.Windows;

namespace ZenithFiler
{
    public partial class SelectFolderDialog : Window
    {
        private readonly DirectoryTreeViewModel _treeViewModel;

        /// <summary>ユーザーが選択したフォルダのパス。OK で確定した場合のみ有効。</summary>
        public string? SelectedPath { get; private set; }

        /// <summary>フォルダ選択ダイアログを開く。</summary>
        /// <param name="initialPath">初期表示時に展開・選択するフォルダのパス。null または無効な場合はドライブルートのみ表示。</param>
        public SelectFolderDialog(string? initialPath = null)
        {
            InitializeComponent();
            _treeViewModel = new DirectoryTreeViewModel();
            DataContext = _treeViewModel;

            if (_treeViewModel.Drives.Count == 0)
                _treeViewModel.LoadDrives();

            var main = Application.Current.MainWindow;
            if (main != null && main.IsLoaded)
            {
                Owner = main;
                Topmost = main.Topmost;
            }

            Loaded += async (_, _) =>
            {
                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(PathHelper.GetPhysicalPath(initialPath)))
                    await _treeViewModel.ExpandToPathAsync(initialPath);
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderTree.SelectedItem is DirectoryItemViewModel item && !string.IsNullOrEmpty(item.FullPath))
            {
                SelectedPath = item.FullPath;
                DialogResult = true;
            }
            else
            {
                App.Notification.Notify("フォルダを選択してください", "インデックス検索対象の追加");
            }
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _treeViewModel.UnsubscribeFromFileOperations();
        }
    }
}
