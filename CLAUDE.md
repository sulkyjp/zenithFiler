# CLAUDE.md (Zenith Filer プロジェクト規約)

## 1. ビルド・実行コマンド
Claude Code から直接実行可能な主要コマンドです。
- **Build (WPF):** `dotnet build`
- **Build (Electron):** `npm run build`
- **Run (WPF):** `dotnet run --project ZenithFiler.csproj`
- **Run (Electron/Dev):** `npm run dev`
- **Test:** `dotnet test`

## 2. プロジェクト基本方針
- [cite_start]**技術スタック:** .NET 8 / WPF 環境 [cite: 4]。
- [cite_start]**MVVM:** `CommunityToolkit.Mvvm` (`ObservableProperty` 等) を使用する [cite: 4]。
- [cite_start]**アイコン:** `PackIconLucide` に統一する [cite: 4]。
- [cite_start]**ライブラリ活用:** 自作を避け、Vanara や WindowsAPICodePack 等の信頼できる NuGet パッケージを優先する [cite: 4]。
- [cite_start]**用語・命名規則:** 画面領域を「ナビペイン（左）」「Aペイン（中央）」「Bペイン（右）」と呼称する [cite: 4]。

## 3. コーディング規約

### C# (.cs)
- [cite_start]**MVVM実装:** ViewModel は `ObservableObject` を継承し、`[ObservableProperty]` を使用する [cite: 1][cite_start]。コマンドは `RelayCommand` を用いる [cite: 1]。
- [cite_start]**非同期・ファイル操作:** 重い処理は `async Task` で実装し、`await Task.Run` で UI ブロックを防ぐ [cite: 1][cite_start]。パス解決は `PathHelper.GetPhysicalPath` で正規化する [cite: 1]。
- [cite_start]**エラー処理:** ユーザー向けエラーは `MessageBox` で表示する [cite: 1][cite_start]。P/Invoke 等では `try/finally` でリソースを確実に解放する [cite: 1]。
- [cite_start]**Nullable:** 有効化されているため、`?.` や `??` を用いて null を安全かつ明示的に扱う [cite: 1, 2]。

### XAML (.xaml)
- [cite_start]**リソース:** 色やブラシは `App.xaml` の `StaticResource` を優先し、インラインカラーはプロジェクト規定値に揃える [cite: 8]。
- [cite_start]**アイコン:** `icon:PackIconLucide` を使用し、`Kind` には正式名（例: `Pencil`）を指定する [cite: 8]。
- [cite_start]**レイアウト:** `IsTabStop="False"` を活用し、フォーカスをリスト等に集約させる [cite: 8]。

## 4. UI/UX & QA 品質基準
- [cite_start]**ステータスバー:** メッセージ末尾に句点「。」を付けない [cite: 5]。
- [cite_start]**ツールチップ:** アクション指向（例：「〜をロック」）で記述し、ショートカットキーを `(Ctrl+S)` 形式で併記する [cite: 5][cite_start]。状態に応じた動的な切り替えを行う [cite: 5]。
- [cite_start]**動作確認 (QA):** 変更完了時、AIは F2(リネーム)、Tab(ペイン切替)、D&D、右クリック等の基本操作が壊れていないか確認し、`MANUAL.md` と整合性を照合する義務を負う [cite: 5]。

## 5. GlowBar 進捗表示パターン（共通規約）
バックグラウンド処理に進捗表示を付ける場合、以下の **DispatcherTimer 追従方式** を標準パターンとして使用する。`ReportFileOperationProgress`（Background dispatch + スロットル）は **コピー/移動専用** であり、新規機能では使用しない。

### 基本構造
```csharp
// 1. GlowBar 開始 + 初期値（UI スレッド上）
MainVM?.BeginFileOperation("[機能名] 処理中...", FlowDirection.LeftToRight);
if (MainVM != null) MainVM.FileOperationProgress = 2;
await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

// 2. Timer セットアップ（UI スレッド上で直接プロパティを追従）
double progressTarget = 2;
string statusText = "[機能名] 処理中...";
var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
progressTimer.Tick += (_, _) => {
    if (MainVM == null) return;
    double target = Volatile.Read(ref progressTarget);
    double current = MainVM.FileOperationProgress;
    MainVM.FileOperationStatusText = statusText;
    if (Math.Abs(target - current) < 0.3) return;
    double step = (target - current) * 0.18;
    if (step > 0 && step < 0.5) step = 0.5;
    MainVM.FileOperationProgress = Math.Min(current + step, target);
};
progressTimer.Start();
var sw = Stopwatch.StartNew();

// 3. バックグラウンド処理（Volatile.Write で目標値を更新するだけ）
try {
    await Task.Run(() => {
        Volatile.Write(ref progressTarget, 10.0);  // フェーズ遷移
        statusText = "[機能名] N / Total 件を処理中...";
        // ... 処理ループで 2% 刻みで progressTarget を更新 ...
        Volatile.Write(ref progressTarget, 95.0);   // 保存フェーズ
    });
} finally {
    sw.Stop();
    // 4. 最低 800ms 表示 → Timer が目標値まで追いつく時間を保証
    var min = TimeSpan.FromMilliseconds(800);
    if (sw.Elapsed < min) await Task.Delay(min - sw.Elapsed);
    progressTimer.Stop();
    MainVM?.EndFileOperation();
}
```

### ルール
- **目標値の更新**: バックグラウンドスレッドからは `Volatile.Write(ref progressTarget, value)` と `statusText = "..."` のみ。Dispatcher への dispatch は不要。
- **進捗レンジ**: スキャン 2→9%、メイン処理 10→90%、後処理/保存 95%。`EndFileOperation` が 100% + フェードアウトを自動処理。
- **報告粒度**: メイン処理ループでは **2% 刻み**（整数パーセント追跡 `lastTargetPct + 2`）。
- **最低表示時間**: `Stopwatch` で計測し、800ms 未満なら差分を `Task.Delay` で待機。
- **ステータスバー書式**: `[機能名] N / Total 件を処理中... (XX%)`。末尾に句点を付けない。
- **完了通知**: `App.Notification.Notify` のトースト通知のみ。ポップアップ（MessageBox）は使用しない。
- **BeginBusy**: `using var busyToken = MainVM?.BeginBusy()` を併用する（GlowBar 表示中はスピナーが自動非表示）。

## [cite_start]6. 開発ワークフロー（厳守） [cite: 6]
コード変更時、以下の順序で実行すること：
1.  [cite_start]**【即時】変更履歴の追記:** 実装完了後、報告前に必ず `CHANGELOG.md` の `## [Unreleased]` セクションへ追記する [cite: 6, 7]。
    - [cite_start]`### Added`, `### Changed`, `### Fixed` の既存見出し配下の末尾に箇条書きで追加し、新規に見出しを重複作成しない [cite: 7]。
2.  [cite_start]**【即時】マニュアル更新:** 仕様や操作に変更がある場合、`MANUAL.md` を更新する [cite: 6]。
3.  **【自動/提案】バージョン更新:**
    - **Patch（0.0.X）自動更新:** バグ修正、微調整、パフォーマンス改善、ドキュメントのみの更新など軽微な変更の場合は、ユーザーに確認せず自動で Patch バージョンを上げ、`.csproj` と `CHANGELOG.md` を同期する。
    - **Minor（0.X.0）以上は提案:** 新機能追加・既存機能の大幅変更など、Patch を超える変更の場合はユーザーにバージョン（Minor/Major）を上げるか提案し、承認後に実行する。
4.  [cite_start]**【実行】一括同期:** `.csproj` のバージョン、`CHANGELOG.md`、`MANUAL.md`（該当時）を同期して更新する [cite: 6]。
5.  **【自動】Git コミット & プッシュ:** バージョン更新が完了したら、ユーザーに確認せず自動で `git add` → `git commit` → `git push` を実行する。コミットメッセージは変更内容を簡潔に記述し、末尾に `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>` を付与する。
