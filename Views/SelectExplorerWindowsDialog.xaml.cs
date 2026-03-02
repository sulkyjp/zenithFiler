using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public partial class SelectExplorerWindowsDialog : Window
    {
        public IReadOnlyList<ExplorerWindowInfo> SelectedItems { get; private set; } = new List<ExplorerWindowInfo>();

        public SelectExplorerWindowsDialog(IReadOnlyList<ExplorerWindowInfo> items)
        {
            InitializeComponent();
            ExplorerListBox.ItemsSource = items;
            if (items.Count > 0)
                ExplorerListBox.SelectedIndex = 0;

            var main = Application.Current.MainWindow;
            if (main != null && main.IsLoaded)
            {
                Owner = main;
                Topmost = main.Topmost;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            TryConfirmSelection();
        }

        private void ExplorerListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TryConfirmSelection();
        }

        private void TryConfirmSelection()
        {
            var selected = ExplorerListBox.SelectedItems.Cast<ExplorerWindowInfo>().ToList();
            if (selected.Count == 0)
            {
                ZenithDialog.Show("1件以上選択してください", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Info);
                return;
            }
            SelectedItems = selected;
            DialogResult = true;
        }
    }
}
