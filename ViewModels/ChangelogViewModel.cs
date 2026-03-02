using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler
{
    public class ChangelogEntry
    {
        public string Version { get; set; } = "";
        public string Date { get; set; } = "";
        public List<ChangelogSection> Sections { get; set; } = new();
    }

    public class ChangelogSection
    {
        public string Title { get; set; } = "";
        public List<string> Items { get; set; } = new();
    }

    public partial class ChangelogViewModel : ObservableObject
    {
        [ObservableProperty]
        private List<ChangelogEntry> _entries = new();

        public ChangelogViewModel()
        {
            LoadChangelog();
        }

        private void LoadChangelog()
        {
            try
            {
                // apps フォルダ優先、次に実行ファイルのディレクトリまたはカレントディレクトリから探す
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var paths = new[]
                {
                    Path.Combine(baseDir, "apps", "CHANGELOG.md"),
                    Path.Combine(baseDir, "CHANGELOG.md"),
                    "CHANGELOG.md",
                    Path.Combine(Directory.GetParent(baseDir)?.Parent?.Parent?.FullName ?? "", "CHANGELOG.md") // デバッグ用
                };

                foreach (var path in paths)
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        var content = File.ReadAllText(path);
                        Entries = ParseChangelog(content);
                        if (Entries.Any()) break;
                    }
                }
            }
            catch
            {
                // エラー時は空リストのまま
            }
        }

        private List<ChangelogEntry> ParseChangelog(string content)
        {
            var entries = new List<ChangelogEntry>();
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            ChangelogEntry? currentEntry = null;
            ChangelogSection? currentSection = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Version header: ## [0.0.8] - 2026-02-01
                var versionMatch = Regex.Match(trimmedLine, @"^##\s*\[(.*?)\]\s*-\s*(.*)");
                if (versionMatch.Success)
                {
                    currentEntry = new ChangelogEntry
                    {
                        Version = versionMatch.Groups[1].Value,
                        Date = versionMatch.Groups[2].Value
                    };
                    entries.Add(currentEntry);
                    currentSection = null;
                    continue;
                }

                // Section header: ### Added
                var sectionMatch = Regex.Match(trimmedLine, @"^###\s*(.*)");
                if (sectionMatch.Success && currentEntry != null)
                {
                    currentSection = new ChangelogSection
                    {
                        Title = sectionMatch.Groups[1].Value
                    };
                    currentEntry.Sections.Add(currentSection);
                    continue;
                }

                // List item: - ...
                if (trimmedLine.StartsWith("-") && currentSection != null)
                {
                    var itemText = trimmedLine.Substring(1).Trim();
                    currentSection.Items.Add(StripMarkdownAsterisks(itemText));
                }
            }

            return entries;
        }

        /// <summary>
        /// 表示用に Markdown の強調記号（**）を除去する。更新履歴画面でアスタリスクが表示されないようにする。
        /// </summary>
        private static string StripMarkdownAsterisks(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(text, @"\*\*", "");
        }
    }
}
