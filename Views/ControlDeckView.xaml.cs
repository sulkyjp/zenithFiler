using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZenithFiler.Controls;

namespace ZenithFiler.Views
{
    public partial class ControlDeckView : UserControl
    {
        public ControlDeckView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
                oldVm.AppSettings.PropertyChanged -= AppSettings_PropertyChanged;
            if (e.NewValue is MainViewModel newVm)
                newVm.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
        }

        private void AppSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettingsViewModel.IsUpdateReadyToRestart))
            {
                if (sender is AppSettingsViewModel settings)
                    ApplyUpdateBtn.Visibility = settings.IsUpdateReadyToRestart ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void HotkeyRecorder_KeyChanged(object? sender, KeyChangedEventArgs e)
        {
            // ViewModel 経由で保存
            if (DataContext is MainViewModel vm)
                vm.AppSettings.SaveKeyBindings();
        }

        private void IndexSummaryRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement fe
                && fe.DataContext is IndexSearchTargetItemViewModel item
                && DataContext is MainViewModel vm)
            {
                vm.IndexSearchSettings.OpenItemSettingsCommand.Execute(item);
                e.Handled = true;
            }
        }

        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is MainViewModel vm)
                    await vm.AppSettings.CheckForUpdateAsync();
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] CheckUpdateBtn_Click: {ex.Message}"); }
        }

        private void ApplyUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            App.UpdateService?.ApplyAndRestart();
        }

        private async void ResetStatsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is MainViewModel vm)
                    await vm.AppSettings.ResetStatisticsAsync();
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] ResetStatsBtn_Click: {ex.Message}"); }
        }
    }
}
