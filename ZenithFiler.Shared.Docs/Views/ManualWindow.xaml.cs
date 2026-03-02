using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZenithFiler
{
    public partial class ManualWindow : Window
    {
        private readonly ManualViewModel _viewModel;

        /// <summary>
        /// 既定コンストラクタ（マニュアルタブから開始）。
        /// XAML からの生成用に残しつつ、内部的には引数付きコンストラクタに委譲する。
        /// </summary>
        public ManualWindow() : this(startWithChangelog: false)
        {
        }

        /// <summary>
        /// isChangelog が true の場合は「更新履歴」タブをアクティブにした状態で開く。
        /// MainWindow からの「更新履歴」メニューなどで使用する。
        /// </summary>
        public ManualWindow(bool startWithChangelog)
        {
            InitializeComponent();
            _viewModel = new ManualViewModel(startWithChangelog);
            DataContext = _viewModel;

            SetMode(isChangelog: startWithChangelog);

            // 単独起動（Owner 未設定）時は Loaded でタイトル・位置を設定
            // 本体から呼ばれる場合は Owner が ShowDialog 前に設定されるため、Loaded 時点で判定可能
            Loaded += (s, e) =>
            {
                if (Owner == null)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            };
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ManualTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetMode(isChangelog: false);
        }

        private void ChangelogTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetMode(isChangelog: true);
        }

        private void SetMode(bool isChangelog)
        {
            _viewModel.IsChangelog = isChangelog;

            ManualTabButton.IsChecked = !isChangelog;
            ChangelogTabButton.IsChecked = isChangelog;

            if (isChangelog)
            {
                Title = "更新履歴";
                if (HeaderTitleTextBlock != null)
                {
                    HeaderTitleTextBlock.Text = "Zenith Filer 更新履歴";
                }
            }
            else
            {
                Title = "ヘルプ / ドキュメント";
                if (HeaderTitleTextBlock != null)
                {
                    HeaderTitleTextBlock.Text = "Zenith Filer マニュアル";
                }
            }
        }
    }
}
