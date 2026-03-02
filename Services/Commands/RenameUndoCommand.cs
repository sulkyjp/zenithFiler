using System.IO;
using ZenithFiler.Services.Interfaces;

namespace ZenithFiler.Services.Commands
{
    public class RenameUndoCommand : IUndoCommand
    {
        private readonly string _oldPath;
        private readonly string _newPath;
        private readonly bool _isDirectory;

        public string Description => "名前の変更";

        public RenameUndoCommand(string oldPath, string newPath, bool isDirectory)
        {
            _oldPath = oldPath;
            _newPath = newPath;
            _isDirectory = isDirectory;
        }

        public void Undo()
        {
            if (_isDirectory)
            {
                if (Directory.Exists(_newPath))
                {
                    FileIoRetryHelper.MoveDirectory(_newPath, _oldPath);
                }
            }
            else
            {
                if (File.Exists(_newPath))
                {
                    FileIoRetryHelper.MoveFile(_newPath, _oldPath);
                }
            }
        }
    }
}
