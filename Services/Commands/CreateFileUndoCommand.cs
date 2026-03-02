using System.IO;
using ZenithFiler.Services.Interfaces;

namespace ZenithFiler.Services.Commands
{
    public class CreateFileUndoCommand : IUndoCommand
    {
        private readonly string _path;

        public string Description => "新規ファイル作成";

        public CreateFileUndoCommand(string path)
        {
            _path = path;
        }

        public void Undo()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
