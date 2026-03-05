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
            Register("Global.Undo", "元に戻す", "グローバル", Key.Z, ModifierKeys.Control,
                "ファイル操作（コピー・移動・リネーム）を取り消します。おすすめ: Ctrl+Z（標準）");
            Register("Global.FocusActivePane", "アクティブペインにフォーカス", "グローバル", Key.E, ModifierKeys.Control | ModifierKeys.Shift,
                "アクティブなファイル一覧ペインにフォーカスを戻します");
            Register("Global.OpenControlDeck", "設定を開く", "グローバル", Key.O, ModifierKeys.Control | ModifierKeys.Shift,
                "設定パネル（Control Deck）を開きます");
            Register("Global.FocusSearch", "検索", "グローバル", Key.F, ModifierKeys.Control,
                "現在のフォルダー内をファイル名で検索します。おすすめ: Ctrl+F（標準）");
            Register("Global.FocusIndexSearch", "インデックス検索", "グローバル", Key.F, ModifierKeys.Control | ModifierKeys.Shift,
                "インデックスを使用して全フォルダーを横断検索します");
            Register("Global.SetPaneCount1", "1画面モード", "グローバル", Key.Q, ModifierKeys.Control | ModifierKeys.Shift,
                "ファイル一覧を1ペインのみ表示するレイアウトに切り替えます");
            Register("Global.SetPaneCount2", "2画面モード", "グローバル", Key.W, ModifierKeys.Control | ModifierKeys.Shift,
                "ファイル一覧をA/Bの2ペイン表示するレイアウトに切り替えます");

            // サイドバー
            Register("Global.SidebarFavorites", "お気に入り", "サイドバー", Key.D1, ModifierKeys.Control | ModifierKeys.Shift,
                "サイドバーをお気に入りパネルに切り替えます");
            Register("Global.SidebarTree", "ツリー", "サイドバー", Key.D2, ModifierKeys.Control | ModifierKeys.Shift,
                "サイドバーをフォルダーツリーパネルに切り替えます");
            Register("Global.SidebarHistory", "履歴", "サイドバー", Key.D3, ModifierKeys.Control | ModifierKeys.Shift,
                "サイドバーを閲覧履歴パネルに切り替えます");
            Register("Global.SidebarIndexSearch", "インデックス検索", "サイドバー", Key.D4, ModifierKeys.Control | ModifierKeys.Shift,
                "サイドバーをインデックス検索パネルに切り替えます");
            Register("Global.SidebarWorkingSet", "ワーキングセット", "サイドバー", Key.D5, ModifierKeys.Control | ModifierKeys.Shift,
                "サイドバーをワーキングセットパネルに切り替えます");

            // ウィンドウ
            Register("Window.QuickPreview", "クイックプレビュー", "ウィンドウ", Key.Space, ModifierKeys.None,
                "選択中のファイルをプレビューウィンドウで表示します。おすすめ: Space（macOS Finder 準拠）");
            Register("Window.SwitchPanes", "ペイン切替", "ウィンドウ", Key.Tab, ModifierKeys.None,
                "A/Bペイン間でフォーカスを切り替えます。おすすめ: Tab");

            // ファイル一覧
            Register("FileList.Rename", "名前の変更", "ファイル一覧", Key.F2, ModifierKeys.None,
                "選択中のファイルやフォルダーの名前を変更します。おすすめ: F2（標準）");
            Register("FileList.Delete", "削除", "ファイル一覧", Key.Delete, ModifierKeys.None,
                "選択中のファイルやフォルダーをごみ箱に移動します。おすすめ: Delete（標準）");
            Register("FileList.SelectAll", "すべて選択", "ファイル一覧", Key.A, ModifierKeys.Control,
                "現在のフォルダー内のすべてのファイルを選択します。おすすめ: Ctrl+A（標準）");
            Register("FileList.Copy", "コピー", "ファイル一覧", Key.C, ModifierKeys.Control,
                "選択中のファイルをクリップボードにコピーします。おすすめ: Ctrl+C（標準）");
            Register("FileList.Paste", "貼り付け", "ファイル一覧", Key.V, ModifierKeys.Control,
                "クリップボードのファイルを現在のフォルダーに貼り付けます。おすすめ: Ctrl+V（標準）");
            Register("FileList.Cut", "切り取り", "ファイル一覧", Key.X, ModifierKeys.Control,
                "選択中のファイルを切り取り（移動用）としてクリップボードに格納します。おすすめ: Ctrl+X（標準）");
            Register("FileList.NewFolder", "新しいフォルダー", "ファイル一覧", Key.N, ModifierKeys.Control | ModifierKeys.Shift,
                "現在のフォルダー内に新しいフォルダーを作成します");
            Register("FileList.Open", "開く", "ファイル一覧", Key.Return, ModifierKeys.None,
                "選択中のファイルを関連付けられたアプリで開きます。フォルダーの場合はそのフォルダーに移動します");
            Register("FileList.GoUp", "上のフォルダーへ", "ファイル一覧", Key.Back, ModifierKeys.None,
                "親フォルダーに移動します。おすすめ: Backspace（標準）");
            Register("FileList.Refresh", "更新", "ファイル一覧", Key.F5, ModifierKeys.None,
                "現在のフォルダーの内容を再読み込みします。おすすめ: F5（標準）");
            Register("FileList.Back", "戻る", "ファイル一覧", Key.Left, ModifierKeys.Alt,
                "閲覧履歴の前のフォルダーに戻ります。おすすめ: Alt+←（ブラウザ準拠）");
            Register("FileList.Forward", "進む", "ファイル一覧", Key.Right, ModifierKeys.Alt,
                "閲覧履歴の次のフォルダーに進みます。おすすめ: Alt+→（ブラウザ準拠）");
        }

        private void Register(string actionId, string displayName, string groupName, Key key, ModifierKeys modifiers, string description = "")
        {
            _bindings[actionId] = new KeyBindingDefinition
            {
                ActionId = actionId,
                DisplayName = displayName,
                Description = description,
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
