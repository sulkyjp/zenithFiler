using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ZenithFiler.Views
{
    public partial class ControlDeckView : UserControl
    {
        private SettingsCategory? _lastCategory;

        public ControlDeckView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MainViewModel vm)
            {
                vm.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
            }
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.AppSettings.PropertyChanged -= AppSettings_PropertyChanged;
            }
        }

        private void AppSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AppSettingsViewModel.ActiveCategory)) return;
            if (sender is not AppSettingsViewModel settings) return;

            var newCategory = settings.ActiveCategory;
            if (_lastCategory == newCategory) return;
            _lastCategory = newCategory;

            AnimateCategorySwitch();
        }

        private async void AnimateCategorySwitch()
        {
            // フェードアウト 80ms
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(80))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var tcsOut = new System.Threading.Tasks.TaskCompletionSource();
            fadeOut.Completed += (_, _) => tcsOut.SetResult();
            ContentArea.BeginAnimation(OpacityProperty, fadeOut);
            await tcsOut.Task;

            // DataTrigger による Visibility 切替を確定
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // フェードイン 120ms
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            fadeIn.Completed += (_, _) =>
            {
                ContentArea.BeginAnimation(OpacityProperty, null);
                ContentArea.Opacity = 1;
            };
            ContentArea.BeginAnimation(OpacityProperty, fadeIn);
        }
    }
}
