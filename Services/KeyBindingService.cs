using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ZenithFiler.Models;

namespace ZenithFiler.Services
{
    /// <summary>
    /// キーバインドの中央レジストリ。デフォルトショートカットの登録、カスタマイズ、競合検出を提供する。
    /// </summary>
    public class KeyBindingService
    {
        private readonly Dictionary<string, KeyBindingDefinition> _bindings = new();
        private readonly List<string> _registrationOrder = new();

        /// <summary>バインド変更時に発火。MainWindow の InputBindings 再構築に使用。</summary>
        public event EventHandler? BindingsChanged;

        public KeyBindingService()
        {
            RegisterDefaults();
        }

        private void RegisterDefaults()
        {
            // グローバル
            Register("Global.Undo", "元に戻す", "グローバル", Key.Z, ModifierKeys.Control);
            Register("Global.FocusActivePane", "アクティブペインにフォーカス", "グローバル", Key.E, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.OpenControlDeck", "設定を開く", "グローバル", Key.O, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.FocusSearch", "検索", "グローバル", Key.F, ModifierKeys.Control);
            Register("Global.FocusIndexSearch", "インデックス検索", "グローバル", Key.F, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.SetPaneCount1", "1画面モード", "グローバル", Key.Q, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.SetPaneCount2", "2画面モード", "グローバル", Key.W, ModifierKeys.Control | ModifierKeys.Shift);

            // サイドバー
            Register("Global.SidebarFavorites", "お気に入り", "サイドバー", Key.D1, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.SidebarTree", "ツリー", "サイドバー", Key.D2, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.SidebarHistory", "履歴", "サイドバー", Key.D3, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.SidebarIndexSearch", "インデックス検索", "サイドバー", Key.D4, ModifierKeys.Control | ModifierKeys.Shift);
            Register("Global.SidebarWorkingSet", "ワーキングセット", "サイドバー", Key.D5, ModifierKeys.Control | ModifierKeys.Shift);

            // ウィンドウ
            Register("Window.QuickPreview", "クイックプレビュー", "ウィンドウ", Key.Space, ModifierKeys.None);
            Register("Window.SwitchPanes", "ペイン切替", "ウィンドウ", Key.Tab, ModifierKeys.None);

            // ファイル一覧
            Register("FileList.Rename", "名前の変更", "ファイル一覧", Key.F2, ModifierKeys.None);
            Register("FileList.Delete", "削除", "ファイル一覧", Key.Delete, ModifierKeys.None);
            Register("FileList.SelectAll", "すべて選択", "ファイル一覧", Key.A, ModifierKeys.Control);
            Register("FileList.Copy", "コピー", "ファイル一覧", Key.C, ModifierKeys.Control);
            Register("FileList.Paste", "貼り付け", "ファイル一覧", Key.V, ModifierKeys.Control);
            Register("FileList.Cut", "切り取り", "ファイル一覧", Key.X, ModifierKeys.Control);
            Register("FileList.NewFolder", "新しいフォルダー", "ファイル一覧", Key.N, ModifierKeys.Control | ModifierKeys.Shift);
            Register("FileList.Open", "開く", "ファイル一覧", Key.Return, ModifierKeys.None);
            Register("FileList.GoUp", "上のフォルダーへ", "ファイル一覧", Key.Back, ModifierKeys.None);
            Register("FileList.Refresh", "更新", "ファイル一覧", Key.F5, ModifierKeys.None);
            Register("FileList.Back", "戻る", "ファイル一覧", Key.Left, ModifierKeys.Alt);
            Register("FileList.Forward", "進む", "ファイル一覧", Key.Right, ModifierKeys.Alt);
        }

        private void Register(string actionId, string displayName, string groupName, Key key, ModifierKeys modifiers)
        {
            _bindings[actionId] = new KeyBindingDefinition
            {
                ActionId = actionId,
                DisplayName = displayName,
                GroupName = groupName,
                DefaultKey = key,
                DefaultModifiers = modifiers
            };
            _registrationOrder.Add(actionId);
        }

        /// <summary>指定アクション ID のバインド定義を取得する。</summary>
        public KeyBindingDefinition? Get(string actionId)
        {
            return _bindings.TryGetValue(actionId, out var def) ? def : null;
        }

        /// <summary>全バインド定義を登録順で返す。</summary>
        public IReadOnlyList<KeyBindingDefinition> GetAll()
        {
            return _registrationOrder.Select(id => _bindings[id]).ToList();
        }

        /// <summary>グループ別にバインド定義を返す。</summary>
        public List<KeyBindingGroupViewModel> GetGroups()
        {
            return GetAll()
                .GroupBy(b => b.GroupName)
                .Select(g => new KeyBindingGroupViewModel
                {
                    GroupName = g.Key,
                    Bindings = g.ToList()
                })
                .ToList();
        }

        /// <summary>指定キー＋修飾子がアクションに一致するか判定する。</summary>
        public bool Matches(string actionId, Key key, ModifierKeys modifiers)
        {
            if (!_bindings.TryGetValue(actionId, out var def)) return false;
            return def.ActiveKey == key && def.ActiveModifiers == modifiers;
        }

        /// <summary>アクションの表示用ショートカットテキストを返す（例: "Ctrl+Z"）。</summary>
        public string GetShortcutText(string actionId)
        {
            if (!_bindings.TryGetValue(actionId, out var def)) return string.Empty;
            return def.DisplayText;
        }

        /// <summary>カスタムバインドを設定する。</summary>
        public void SetCustomBinding(string actionId, Key key, ModifierKeys modifiers)
        {
            if (!_bindings.TryGetValue(actionId, out var def)) return;

            // デフォルトと同じならカスタムをクリア
            if (key == def.DefaultKey && modifiers == def.DefaultModifiers)
            {
                def.CustomKey = null;
                def.CustomModifiers = null;
            }
            else
            {
                def.CustomKey = key;
                def.CustomModifiers = modifiers;
            }

            UpdateConflicts();
            BindingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>指定アクションのカスタムバインドをデフォルトに戻す。</summary>
        public void ResetToDefault(string actionId)
        {
            if (!_bindings.TryGetValue(actionId, out var def)) return;
            def.CustomKey = null;
            def.CustomModifiers = null;
            def.ConflictWarning = string.Empty;
            UpdateConflicts();
            BindingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>全バインドをデフォルトに戻す。</summary>
        public void ResetAll()
        {
            foreach (var def in _bindings.Values)
            {
                def.CustomKey = null;
                def.CustomModifiers = null;
                def.ConflictWarning = string.Empty;
            }
            BindingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>指定キーコンボが他のアクションと競合するか検出する（自分自身を除外）。</summary>
        public KeyBindingDefinition? FindConflict(string excludeActionId, Key key, ModifierKeys modifiers)
        {
            foreach (var def in _bindings.Values)
            {
                if (def.ActionId == excludeActionId) continue;
                if (def.ActiveKey == key && def.ActiveModifiers == modifiers)
                    return def;
            }
            return null;
        }

        /// <summary>全バインドの競合警告を更新する。</summary>
        private void UpdateConflicts()
        {
            // まず全クリア
            foreach (var def in _bindings.Values)
                def.ConflictWarning = string.Empty;

            // 重複検出
            var grouped = _bindings.Values
                .GroupBy(d => (d.ActiveKey, d.ActiveModifiers))
                .Where(g => g.Count() > 1);

            foreach (var group in grouped)
            {
                var names = group.Select(d => d.DisplayName).ToList();
                foreach (var def in group)
                {
                    var others = names.Where(n => n != def.DisplayName).ToList();
                    def.ConflictWarning = $"「{string.Join("」「", others)}」と競合";
                }
            }
        }

        /// <summary>起動時にカスタム設定を適用する。</summary>
        public void ApplyCustomBindings(List<KeyBindingDto>? dtos)
        {
            if (dtos == null || dtos.Count == 0) return;

            foreach (var dto in dtos)
            {
                if (!_bindings.TryGetValue(dto.ActionId, out var def)) continue;
                if (!KeyBindingDefinition.TryParseKey(dto.Key, out var key)) continue;
                KeyBindingDefinition.TryParseModifiers(dto.Modifiers, out var modifiers);

                if (key == def.DefaultKey && modifiers == def.DefaultModifiers)
                    continue; // デフォルトと同じならスキップ

                def.CustomKey = key;
                def.CustomModifiers = modifiers;
            }

            UpdateConflicts();
        }

        /// <summary>保存用にカスタムバインドの DTO リストを取得する（カスタマイズ済みのみ）。</summary>
        public List<KeyBindingDto> GetCustomBindingsForSave()
        {
            return _bindings.Values
                .Where(d => d.IsCustomized)
                .Select(d => new KeyBindingDto
                {
                    ActionId = d.ActionId,
                    Key = d.ActiveKey.ToString(),
                    Modifiers = d.ActiveModifiers == ModifierKeys.None ? string.Empty : d.ActiveModifiers.ToString()
                })
                .ToList();
        }
    }
}
