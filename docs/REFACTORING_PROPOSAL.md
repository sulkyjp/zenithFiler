# リファクタリング提案書

> **方針**: 機能劣化を一切させず、処理性能・信頼性を向上させる。提案方式で進め、各項目は承認後に実施する。

---

## 1. 優先度: 高（パフォーマンス・信頼性に直結）

### 1-1. Markdown パイプラインのキャッシュ（DocViewerControl.xaml.cs）

**現状の問題**  
`RenderMarkdown()` が呼ばれるたびに `MarkdownPipelineBuilder` でパイプラインを新規構築している。同一設定のパイプラインは再利用可能。

**提案**  
静的フィールドでパイプラインを1回だけ構築し、以降は再利用する。

```csharp
// 例: クラス内に追加
private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
    .Build();
```

**影響範囲**: `DocViewerControl` のみ  
**機能変化**: なし（変換結果は同一）  
**期待効果**: マニュアル／更新履歴のタブ切替・再描画時のオーバーヘッド削減

---

### 1-2. FindVisualChild の共通化

**現状の問題**  
`FindVisualChild<T>` が以下に重複実装されている:

- `DocViewerControl.xaml.cs`
- `FilePaneControl.xaml.cs`
- `Converters.cs`（GridViewStarWidthConverter）
- `Behaviors\TabContentTransitionBehavior.cs`
- `Behaviors\TabIndicatorSlideBehavior.cs`（FindVisualChildByPredicate）

**提案**  
共通ヘルパークラス（例: `VisualTreeHelperExtensions`）に1つに集約し、各所から参照する。

- 例: `ZenithFiler.Helpers.VisualTreeHelperExtensions.FindVisualChild<T>()`
- `FindVisualChildByPredicate` も同クラスに追加し、必要なら両方提供

**影響範囲**: 複数ファイル  
**機能変化**: なし（同一ロジックの集約）  
**期待効果**: 保守性の向上、将来の修正漏れ防止

---

### 1-3. DocViewerControl: ScrollChanged ハンドラの重複登録防止

**現状の問題**  
`RenderMarkdown()` で `DocViewer.AddHandler(ScrollViewer.ScrollChangedEvent, ...)` を呼んでいるが、`RemoveHandler` していない。  
Markdown が再描画されるたびにハンドラが追加され、スクロール時に複数回呼ばれる可能性がある。

**提案**  
- 初回のみ登録する、または  
- 登録前に `RemoveHandler` で削除してから `AddHandler` する

**影響範囲**: `DocViewerControl` のみ  
**機能変化**: なし（むしろ不正動作の防止）  
**期待効果**: スクロール時の重複処理の防止

---

### 1-4. DocViewerControl: 例外処理の明確化

**現状の問題**  
- `EnhanceCategoryHeading`: `Application.Current.FindResource` を `try-catch` で囲んでいるが、`catch` 内で何もしていない（ログ・フォールバックのみ）。  
- `UpdateActiveSection`: `GetCharacterRect` まわりで `catch` で例外を無視している。

**提案**  
- いずれも「想定内の失敗」として扱う場合は、ログ出力（`App.FileLogger` 等）を追加する。  
- リソース取得失敗時は、既存通り `accentBrush` を null のままにしてフォールバックとして扱う（現状維持）。  
- 必要に応じて `catch` の対象を `Exception` ではなく `ResourceReferenceKeyNotFoundException` などに限定する。

**影響範囲**: `DocViewerControl` のみ  
**機能変化**: なし（挙動は同じ、ログのみ追加）  
**期待効果**: トラブルシューティングのしやすさ向上

---

## 2. 優先度: 中（保守性・可読性）

### 2-1. DocViewerControl: 検索ロジックの分離

**現状の問題**  
`DocViewerControl.xaml.cs` が約460行あり、検索・ハイライト・スクロール連動・Markdown レンダリングが混在している。

**提案**  
- 検索関連（`PerformSearch`, `NavigateMatch`, `UpdateSearchStatus`, `SyncTocWithMatch`）を別クラス（例: `DocumentSearchHelper`）に切り出す。  
- 必要に応じて `FlowDocument` と `ManualViewModel` への参照を渡す形で利用する。  
- View の Code-behind は「イベントハンドラと ViewModel の橋渡し」に集中させる。

**影響範囲**: `DocViewerControl` と新規クラス  
**機能変化**: なし  
**期待効果**: 責務の明確化、テスト容易性の向上

---

### 2-2. DocViewerControl: PostProcessDocument のブロック列挙

**現状の問題**  
`PostProcessDocument` 内で `doc.Blocks.ToList()` によりコレクションを一旦コピーしている。  
コメントには「列挙中にコレクションが変更されたと判定されるのを防ぐため」とあるが、実際には `foreach` 内で `doc.Blocks` を変更していない。

**提案**  
- 現状の `ToList()` は安全側に倒した妥当な実装として維持する。  
- あるいは、`doc.Blocks` の変更が明らかにないなら `ToList()` を外してパフォーマンスを狙うことも可能（要検証）。  
- 変更する場合は、`doc.Blocks` の変更がないことをコードレビューで確認する。

**影響範囲**: `DocViewerControl` のみ  
**機能変化**: なし  
**期待効果**: 不要なアロケーション削減（`ToList` 削除時）

---

### 2-3. MainViewModel: メソッド・プロパティの整理

**現状の問題**  
`MainViewModel` が約730行あり、初期化・コマンド・イベント・設定・履歴・ツリー・ウィンドウ操作などが混在している。

**提案**  
段階的に、以下のような方針で整理する（一度に大幅変更はしない）:

- ウィンドウ配置関連（SnapLeft, MaximizeWindow 等）を `WindowCommands` のようなヘルパーに委譲する。  
- ツリー操作関連（RequestRenameTreeFolder, RequestDeleteTreeFolder 等）を別の Handler クラスに切り出す。  
- 履歴関連（RefreshHistoryAsync, FilterHistoryItem 等）を `HistoryViewModel` または既存の ViewModel に寄せる。

**影響範囲**: `MainViewModel` と新規／既存クラス  
**機能変化**: なし  
**期待効果**: 可読性・テスト容易性の向上

---

### 2-4. ManualViewModel: アイコン種別マッピングの整理

**現状の問題**  
`ManualTocItem.IconKind` の getter が長く、`Title.Contains(...)` の連鎖で可読性が低い。

**提案**  
- 見出し→アイコン名のマッピングを `Dictionary` または静的メソッドに切り出す。  
- あるいは、`Title` をキーとした `switch` 式で整理する。

**影響範囲**: `ManualViewModel` のみ  
**機能変化**: なし  
**期待効果**: 可読性・メンテナンス性の向上

---

## 3. 優先度: 低（将来の改善）

### 3-1. TabItemViewModel の分割

**現状の問題**  
`TabItemViewModel` が2000行超と非常に長い。

**提案**  
- まずは「検索」「ファイル操作」「ソート・フィルタ」など、責務ごとに region か partial で区切る。  
- その後、検索ロジックやファイルリスト操作を別クラスに切り出す余地を検討する。

**影響範囲**: `TabItemViewModel` と関連クラス  
**機能変化**: なし  
**期待効果**: 長期的な保守性向上

---

### 3-2. 非同期処理の統一

**現状の問題**  
`_ = LoadDocumentForCurrentModeAsync()` のように fire-and-forget のパターンが散見される。  
意図的な場合は問題ないが、例外が握りつぶされないよう注意が必要。

**提案**  
- フォアグラウンドで完了を待つ必要がない処理は、継続で `TaskScheduler.UnobservedTaskException` 等のログを確認する。  
- 必要に応じて `async void` を避け、`async Task` + 適切な `await` または `ContinueWith` で統一する。

**影響範囲**: 複数ファイル  
**機能変化**: なし  
**期待効果**: 未処理例外の早期発見・信頼性向上

---

## 4. 実施順序の推奨

| 順序 | 項目              | リスク | 工数 |
|------|-------------------|--------|------|
| 1    | 1-1 パイプラインキャッシュ | 低     | 小   |
| 2    | 1-2 FindVisualChild 共通化 | 低   | 中   |
| 3    | 1-3 ScrollChanged 重複登録防止 | 低 | 小   |
| 4    | 1-4 例外処理の明確化      | 低     | 小   |
| 5    | 2-1 検索ロジック分離      | 低     | 中   |
| 6    | 2-2 PostProcessDocument  | 低     | 小   |
| 7    | 2-3 MainViewModel 整理  | 中     | 大   |
| 8    | 2-4 IconKind マッピング   | 低     | 小   |

---

## 5. 各実施後の確認項目（QA）

リファクタリング完了後、以下を確認する:

- **キーボード**: F2(リネーム), Del(削除), Ctrl+C/V, Tab(ペイン切替), Enter(開く)  
- **マウス**: ドラッグ＆ドロップ（同一/別フォルダ）、右クリックメニュー  
- **DocViewer**: マニュアル／更新履歴の表示、目次クリック、検索、スクロール連動  
- **MANUAL.md**: 記載と実際の挙動が一致しているか

---

## 6. 合意・承認

各項目を実施する前に、この提案書の該当項目について承認を得ることを推奨する。  
承認後に実装し、QA を実施したうえで CHANGELOG.md に記録する。

---

*作成日: 2026-02-11*
