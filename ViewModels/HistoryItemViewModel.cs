using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;

namespace ZenithFiler
{
    public partial class HistoryItemViewModel : ObservableObject
    {
        private readonly HistoryRecord _record;

        public HistoryItemViewModel(HistoryRecord record)
        {
            _record = record;
            Name = System.IO.Path.GetFileName(record.Path);
            if (string.IsNullOrEmpty(Name)) Name = record.Path;
        }

        public string Path => _record.Path;
        public string Name { get; }
        public SourceType SourceType => _record.SourceType;
        public DateTime LastAccessed => _record.LastAccessed;
        public DateTime LastAccessedDate => _record.LastAccessed.Date;
        public int AccessCount => _record.AccessCount;

        public PackIconLucideKind IconKind => SourceType switch
        {
            SourceType.Local => PackIconLucideKind.HardDrive,
            SourceType.Server => PackIconLucideKind.Server,
            SourceType.Box => PackIconLucideKind.Archive,
            SourceType.SPO => PackIconLucideKind.Cloud,
            _ => PackIconLucideKind.Folder
        };

        public HistoryRecord Record => _record;
    }
}
