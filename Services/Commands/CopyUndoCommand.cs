using System.IO;
using ZenithFiler.Services.Interfaces;
using Vanara.Windows.Shell;

namespace ZenithFiler.Services.Commands
{
    public class CopyUndoCommand : IUndoCommand
    {
        private readonly string _copiedFilePath; // コピー先のフルパス

        public string Description => "コピー";

        public CopyUndoCommand(string copiedFilePath)
        {
            _copiedFilePath = copiedFilePath;
        }

        public void Undo()
        {
            // コピーの取り消し＝コピーしてできたファイルの削除
            // 完全に消去せず、ゴミ箱に入れるのが安全
            try 
            {
                using var op = new ShellFileOperations();
                using var item = new ShellItem(_copiedFilePath);
                op.QueueDeleteOperation(item);
                op.PerformOperations();
            }
            catch
            {
                // Vanaraが失敗した場合は通常の削除（ゴミ箱ではない）を試みる
                if (Directory.Exists(_copiedFilePath))
                {
                    Directory.Delete(_copiedFilePath, true);
                }
                else if (File.Exists(_copiedFilePath))
                {
                    File.Delete(_copiedFilePath);
                }
            }
        }
    }
}
