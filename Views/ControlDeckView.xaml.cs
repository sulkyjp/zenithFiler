using System.Windows;
using System.Windows.Controls;
using ZenithFiler.Controls;

namespace ZenithFiler.Views
{
    public partial class ControlDeckView : UserControl
    {
        public ControlDeckView()
        {
            InitializeComponent();
        }

        private void HotkeyRecorder_KeyChanged(object? sender, KeyChangedEventArgs e)
        {
            // ViewModel 経由で保存
            if (DataContext is MainViewModel vm)
                vm.AppSettings.SaveKeyBindings();
        }
    }
}
