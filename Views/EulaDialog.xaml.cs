using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ZenithFiler.Views
{
    public partial class EulaDialog : Window
    {
        public EulaDialog()
        {
            InitializeComponent();
            LoadEulaText();
        }

        private void LoadEulaText()
        {
            // apps/EULA.md をビルド出力から読み込み
            var eulaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps", "EULA.md");
            if (File.Exists(eulaPath))
            {
                EulaTextBlock.Text = File.ReadAllText(eulaPath);
            }
            else
            {
                EulaTextBlock.Text = "使用許諾契約書の読み込みに失敗しました。";
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
