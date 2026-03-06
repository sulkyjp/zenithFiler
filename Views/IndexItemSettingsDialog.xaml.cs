using System.Windows;

namespace ZenithFiler.Views
{
    public partial class IndexItemSettingsDialog : Window
    {
        public IndexItemSettingsDialog()
        {
            InitializeComponent();

            var main = Application.Current.MainWindow;
            if (main != null && main.IsLoaded)
            {
                Owner = main;
                Topmost = main.Topmost;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
