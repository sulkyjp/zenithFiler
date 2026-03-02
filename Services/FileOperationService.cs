using System;

namespace ZenithFiler.Services
{
    /// <summary>
    /// ファイル一覧（A/Bペイン）でのフォルダ操作完了イベントを発行するサービス。
    /// DirectoryTreeViewModel が購読し、ツリービューへの即時・差分反映を行う。
    /// FileSystemWatcher とは独立し、一覧からの直接操作の「発信源」として二重更新を防止する。
    /// </summary>
    public class FileOperationService
    {
        private static readonly Lazy<FileOperationService> _instance = new(() => new FileOperationService());
        public static FileOperationService Instance => _instance.Value;

        /// <summary>新規フォルダ作成完了時（親パス, 作成されたフォルダのフルパス）</summary>
        public event Action<string, string>? FolderCreated;

        /// <summary>フォルダリネーム完了時（旧パス, 新パス）</summary>
        public event Action<string, string>? FolderRenamed;

        /// <summary>フォルダ移動完了時（移動元親パス, 移動先親パス, 移動前フルパス, 移動後フルパス）</summary>
        public event Action<string, string, string, string>? FolderMoved;

        /// <summary>フォルダ削除完了時（親パス, 削除されたフォルダのフルパス）</summary>
        public event Action<string, string>? FolderDeleted;

        private FileOperationService() { }

        /// <summary>新規フォルダ作成完了を通知。一覧での作成直後に呼ぶ。</summary>
        public void RaiseFolderCreated(string parentPath, string newFolderPath)
        {
            FolderCreated?.Invoke(parentPath, newFolderPath);
        }

        /// <summary>フォルダリネーム完了を通知。</summary>
        public void RaiseFolderRenamed(string oldPath, string newPath)
        {
            FolderRenamed?.Invoke(oldPath, newPath);
        }

        /// <summary>フォルダ移動完了を通知。</summary>
        public void RaiseFolderMoved(string sourceParentPath, string targetParentPath, string oldFolderPath, string movedFolderPath)
        {
            FolderMoved?.Invoke(sourceParentPath, targetParentPath, oldFolderPath, movedFolderPath);
        }

        /// <summary>フォルダ削除完了を通知。</summary>
        public void RaiseFolderDeleted(string parentPath, string deletedFolderPath)
        {
            FolderDeleted?.Invoke(parentPath, deletedFolderPath);
        }
    }
}
