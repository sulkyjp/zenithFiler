using System.IO;
using ZenithFiler.Services.Interfaces;

namespace ZenithFiler.Services.Commands
{
    public class MoveUndoCommand : IUndoCommand
    {
        private readonly string _sourcePath;
        private readonly string _destPath; // 移動先のパス（移動後のフルパス）

        public string Description => "移動";

        public MoveUndoCommand(string sourcePath, string destPath)
        {
            _sourcePath = sourcePath;
            _destPath = destPath;
        }

        public void Undo()
        {
            bool isDirectory = Directory.Exists(_destPath);
            bool isFile = File.Exists(_destPath);

            if (isDirectory)
            {
                FileIoRetryHelper.MoveDirectory(_destPath, _sourcePath);
            }
            else if (isFile)
            {
                FileIoRetryHelper.MoveFile(_destPath, _sourcePath);
            }
        }
    }
}
