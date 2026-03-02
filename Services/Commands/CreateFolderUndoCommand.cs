using System.IO;
using ZenithFiler.Services.Interfaces;

namespace ZenithFiler.Services.Commands
{
    public class CreateFolderUndoCommand : IUndoCommand
    {
        private readonly string _path;

        public string Description => "新規フォルダ作成";

        public CreateFolderUndoCommand(string path)
        {
            _path = path;
        }

        public void Undo()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path);
            }
        }
    }
}
