using System;
using System.IO;
using System.Windows;
using Vanara.Windows.Shell;
using ZenithFiler.Helpers;
using ZenithFiler.Services.Commands;
using ZenithFiler.Views;

namespace ZenithFiler.Services
{
    /// <summary>
    /// ナビペインのディレクトリツリーにおけるフォルダ操作（リネーム・削除）を実行するハンドラ。
    /// MainViewModel の責務を軽減するため分離。
    /// </summary>
    public class TreeFolderOperationHandler
    {
        private readonly Func<bool> _isTreeViewLocked;
        private readonly Action _triggerLockWarning;
        private readonly UndoService _undoService;

        public TreeFolderOperationHandler(
            Func<bool> isTreeViewLocked,
            Action triggerLockWarning,
            UndoService undoService)
        {
            _isTreeViewLocked = isTreeViewLocked ?? throw new ArgumentNullException(nameof(isTreeViewLocked));
            _triggerLockWarning = triggerLockWarning ?? throw new ArgumentNullException(nameof(triggerLockWarning));
            _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        }

        /// <summary>
        /// フォルダのリネームを実行する。
        /// </summary>
        /// <param name="path">変更対象のフォルダの物理パス</param>
        /// <param name="newName">新しい名前</param>
        /// <param name="parentDir">親ディレクトリのパス</param>
        public void ExecuteRename(string path, string newName, string parentDir)
        {
            if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrEmpty(parentDir) || string.IsNullOrEmpty(path))
                return;
            if (!Directory.Exists(path))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ツリー名前変更: {path}");
                return;
            }

            string newPath = Path.Combine(parentDir, newName);
            try
            {
                FileIoRetryHelper.MoveDirectory(path, newPath);
                _undoService.Register(new RenameUndoCommand(path, newPath, isDirectory: true));
                FileOperationService.Instance.RaiseFolderRenamed(path, newPath);
                App.Notification.Notify("名前を変更しました", $"名前を変更: '{path}' -> '{newPath}'");
            }
            catch (Exception ex)
            {
                var friendlyMsg = Helpers.RestartManagerHelper.GetFriendlyErrorMessage(ex, path);
                App.Notification.Notify("名前の変更に失敗しました", $"名前の変更に失敗しました。Target: '{path}', NewName: '{newPath}' 詳細: {ex}");
                ZenithDialog.Show($"名前の変更に失敗しました。\n{friendlyMsg}", "名前の変更", ZenithDialogButton.OK, ZenithDialogIcon.Error);
            }
        }

        /// <summary>
        /// フォルダの削除を実行する。
        /// </summary>
        public void ExecuteDelete(string path, string parentPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(parentPath))
                return;

            try
            {
                using var op = new ShellFileOperations();
                using var shellItem = new ShellItem(path);
                op.QueueDeleteOperation(shellItem);
                op.PerformOperations();
                FileOperationService.Instance.RaiseFolderDeleted(parentPath, path);
                App.Notification.Notify("フォルダを削除しました", $"削除: {path}");
            }
            catch (Exception ex)
            {
                var friendlyMsg = Helpers.RestartManagerHelper.GetFriendlyErrorMessage(ex, path);
                App.Notification.Notify("削除中にエラーが発生しました", $"削除失敗 詳細: {ex}");
                ZenithDialog.Show($"削除中にエラーが発生しました。\n{friendlyMsg}", "削除", ZenithDialogButton.OK, ZenithDialogIcon.Error);
            }
        }

        /// <summary>
        /// ツリービューロック中に禁止操作を試みた場合の処理を行う。
        /// </summary>
        public bool CheckLockAndWarn()
        {
            if (!_isTreeViewLocked()) return false;
            App.Notification.Notify("ロック中のため変更できませんでした", "ツリービューロック: 操作をブロック");
            _triggerLockWarning();
            return true;
        }
    }
}
