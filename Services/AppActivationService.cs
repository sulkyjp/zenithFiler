using System;
using System.Windows;

namespace ZenithFiler
{
    /// <summary>
    /// アプリケーションのアクティブ/非アクティブ状態を管理するサービスです。
    /// </summary>
    public class AppActivationService
    {
        private static readonly Lazy<AppActivationService> _instance = new(() => new AppActivationService());
        public static AppActivationService Instance => _instance.Value;

        public bool IsActive { get; private set; } = true;

        public event EventHandler<bool>? ActivationChanged;

        private AppActivationService()
        {
            if (Application.Current != null)
            {
                Application.Current.Activated += (s, e) => SetActive(true);
                Application.Current.Deactivated += (s, e) => SetActive(false);
            }
        }

        private void SetActive(bool isActive)
        {
            if (IsActive != isActive)
            {
                IsActive = isActive;
                ActivationChanged?.Invoke(this, isActive);
            }
        }
    }
}
