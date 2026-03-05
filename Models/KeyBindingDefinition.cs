using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler.Models
{
    /// <summary>settings.json 永続化用 DTO。ActionId + キー文字列を保持。</summary>
    public class KeyBindingDto
    {
        public string ActionId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Modifiers { get; set; } = string.Empty;
    }

    /// <summary>キーバインド定義。デフォルト値とカスタム値を保持し、Active（実効）値を提供する。</summary>
    public partial class KeyBindingDefinition : ObservableObject
    {
        public string ActionId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string GroupName { get; init; } = string.Empty;

        // デフォルト値（変更不可）
        public Key DefaultKey { get; init; }
        public ModifierKeys DefaultModifiers { get; init; }

        // カスタム値（null = デフォルト使用）
        [ObservableProperty]
        private Key? _customKey;

        [ObservableProperty]
        private ModifierKeys? _customModifiers;

        /// <summary>実効キー（カスタムがあればカスタム、なければデフォルト）。</summary>
        public Key ActiveKey => CustomKey ?? DefaultKey;

        /// <summary>実効修飾キー。</summary>
        public ModifierKeys ActiveModifiers => CustomModifiers ?? DefaultModifiers;

        /// <summary>カスタマイズされているか。</summary>
        public bool IsCustomized => CustomKey.HasValue;

        /// <summary>競合警告メッセージ。空文字列 = 競合なし。</summary>
        [ObservableProperty]
        private string _conflictWarning = string.Empty;

        partial void OnCustomKeyChanged(Key? value)
        {
            OnPropertyChanged(nameof(ActiveKey));
            OnPropertyChanged(nameof(ActiveModifiers));
            OnPropertyChanged(nameof(IsCustomized));
            OnPropertyChanged(nameof(DisplayText));
        }

        partial void OnCustomModifiersChanged(ModifierKeys? value)
        {
            OnPropertyChanged(nameof(ActiveKey));
            OnPropertyChanged(nameof(ActiveModifiers));
            OnPropertyChanged(nameof(IsCustomized));
            OnPropertyChanged(nameof(DisplayText));
        }

        /// <summary>表示用テキスト（例: "Ctrl+Z"）。</summary>
        public string DisplayText => FormatKeyCombo(ActiveKey, ActiveModifiers);

        /// <summary>デフォルトの表示用テキスト。</summary>
        public string DefaultDisplayText => FormatKeyCombo(DefaultKey, DefaultModifiers);

        /// <summary>キーコンボを表示用文字列に変換する。</summary>
        public static string FormatKeyCombo(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");

            string keyStr = key switch
            {
                Key.Back => "Backspace",
                Key.Delete => "Delete",
                Key.Return => "Enter",
                Key.Space => "Space",
                Key.Left => "\u2190",
                Key.Right => "\u2192",
                Key.Up => "\u2191",
                Key.Down => "\u2193",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                _ => key.ToString()
            };

            parts.Add(keyStr);
            return string.Join("+", parts);
        }

        /// <summary>文字列からキーを解析する。</summary>
        public static bool TryParseKey(string keyStr, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(keyStr)) return false;
            return System.Enum.TryParse(keyStr, true, out key);
        }

        /// <summary>文字列から修飾キーを解析する。</summary>
        public static bool TryParseModifiers(string modStr, out ModifierKeys modifiers)
        {
            modifiers = ModifierKeys.None;
            if (string.IsNullOrEmpty(modStr)) return true; // 空文字列は None
            foreach (var part in modStr.Split('+', ','))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (System.Enum.TryParse<ModifierKeys>(trimmed, true, out var m))
                    modifiers |= m;
                else
                    return false;
            }
            return true;
        }
    }

    /// <summary>UI 表示用グループ。グループ名とバインド定義一覧を保持。</summary>
    public class KeyBindingGroupViewModel
    {
        public string GroupName { get; init; } = string.Empty;
        public List<KeyBindingDefinition> Bindings { get; init; } = new();
    }
}
