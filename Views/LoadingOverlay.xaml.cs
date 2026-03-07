using System.Windows;
using System.Windows.Controls;

namespace ZenithFiler
{
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (!WindowSettings.ShowStartupEffectsEnabled)
                    Visibility = Visibility.Collapsed;
            };
        }
    }
}
