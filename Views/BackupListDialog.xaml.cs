using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ZenithFiler.Services;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public partial class BackupListDialog : Window
    {
        public BackupListDialog()
        {
            InitializeComponent();
            Reload();
        }

        private void Reload()
        {
            BackupListView.ItemsSource = SettingsBackupService.GetBackups();
        }

        private void BackupListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BackupListView.SelectedItem is not BackupEntry entry) return;

            var confirm = ZenithDialog.Show(
                $"この設定（{entry.Timestamp:yyyy/MM/dd HH:mm:ss}）で復元しますか？\n現在の設定は上書きされます。",
                "設定の復元",
                ZenithDialogButton.OKCancel,
                ZenithDialogIcon.Warning);
            if (confirm != ZenithDialogResult.OK) return;

            try
            {
                SettingsBackupService.Restore(entry.JsonPath);
                _ = App.FileLogger.LogAsync($"[Settings] Recovery: restored from '{Path.GetFileName(entry.JsonPath)}'");
            }
            catch (Exception ex)
            {
                ZenithDialog.Show($"復元に失敗しました。\n{ex.Message}", "エラー",
                    ZenithDialogButton.OK, ZenithDialogIcon.Error);
                return;
            }

            var restart = ZenithDialog.Show(
                "設定を復元しました。変更を有効にするにはアプリの再起動が必要です。\n今すぐ再起動しますか？",
                "再起動の確認",
                ZenithDialogButton.YesNo,
                ZenithDialogIcon.Question);
            if (restart == ZenithDialogResult.Yes)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                    Application.Current.Shutdown();
                }
            }

            Close();
        }

        private void LockMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (BackupListView.SelectedItem is BackupEntry entry)
            {
                SettingsBackupService.SetLock(entry.JsonPath, locked: true);
                Reload();
            }
        }

        private void UnlockMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (BackupListView.SelectedItem is BackupEntry entry)
            {
                SettingsBackupService.SetLock(entry.JsonPath, locked: false);
                Reload();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
