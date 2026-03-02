using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ZenithFiler
{
    public class TabStateDto
    {
        public string Path     { get; set; } = string.Empty;
        public bool   IsLocked { get; set; }
    }

    public class PaneStateDto
    {
        public List<TabStateDto> Tabs             { get; set; } = new();
        public int               SelectedTabIndex { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FileViewMode      FileViewMode     { get; set; } = FileViewMode.Details;
        public string            SortProperty     { get; set; } = "LastModified";
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ListSortDirection SortDirection    { get; set; } = ListSortDirection.Descending;
        public bool              IsGroupFoldersFirst { get; set; } = true;
    }

    public class WorkingSetDto
    {
        public string       Id        { get; set; } = Guid.NewGuid().ToString();
        public string       Name      { get; set; } = string.Empty;
        public string       CreatedAt { get; set; } = string.Empty;
        public int          PaneCount { get; set; } = 2;
        public PaneStateDto LeftPane  { get; set; } = new();
        public PaneStateDto RightPane { get; set; } = new();
    }
}
