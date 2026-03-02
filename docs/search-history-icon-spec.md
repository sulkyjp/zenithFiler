# 検索履歴の通常／インデックス識別アイコン － 可否と対応方針

## 可否

**可能です。** 実装に必要な要素は既に揃っています。

- 検索履歴は `SearchHistoryRecord`（SQLite）でキーワード＋最終検索日時を保持している。
- インデックス検索モードは `TabItemViewModel.IsIndexSearchMode` で管理されている。
- 検索バーでは通常時は `Search`（虫眼鏡）、インデックス時は `Zap`（雷）アイコンを使用している（`TabContentControl.xaml` 既存）。

上記を組み合わせ、履歴の「保存時」にモードを記録し、「表示時」にアイコンを切り替える形で対応できます。

---

## 対応方針

### 1. データ層

| 対象 | 変更内容 |
|------|----------|
| **SearchHistoryRecord** | `IsIndexSearch`（bool）プロパティを追加。主キーを「キーワードのみ」から「キーワード＋モード」の組み合わせに変更し、同一キーワードでも「通常」と「インデックス」を別履歴として保存できるようにする。 |
| **DatabaseService** | `SaveSearchHistoryAsync(string keyword, bool isIndexSearch)` に変更。`GetSearchHistoryAsync` の戻り値を「キーワード＋IsIndexSearch」のリスト（または DTO のリスト）に変更。既存の `Keyword` 単体 PK のままだと同一キーワードで上書きされるため、複合キー（例: 内部で `Key = $"{keyword}\t{(isIndexSearch ? "1" : "0")}"` のような文字列 PK）または別設計で「同じキーワードで通常／インデックス 2 件」を許容する。 |
| **マイグレーション** | 既存 DB に `IsIndexSearch` 列を追加する場合、既存レコードは「通常検索」として扱う（`IsIndexSearch = false`）。新規テーブル作成の場合は初回から上記スキーマで作成する。 |

### 2. ViewModel 層

| 対象 | 変更内容 |
|------|----------|
| **TabItemViewModel** | `SearchHistory` を `ObservableCollection<string>` から、キーワードとモードを持つ型（例: `SearchHistoryItem` の `ObservableCollection<SearchHistoryItem>`）に変更。`LoadSearchHistoryAsync` で取得したリストをその型で保持。`ExecuteSearch`／`SelectSearchHistory`／`PerformSearchAsync` 内で `SaveSearchHistoryAsync` を呼ぶ箇所で、現在の `IsIndexSearchMode` を渡す。`SelectSearchHistory` は「選択した履歴のキーワード」で検索する際、必要に応じて選択した履歴の `IsIndexSearch` に合わせて `IsIndexSearchMode` を更新するかどうかは仕様次第（現状は「選択したキーワードで検索」のみで、モードはそのままでも可）。 |

### 3. View 層（UI）

| 対象 | 変更内容 |
|------|----------|
| **TabContentControl.xaml** | 履歴リストの `ListBox` の `ItemsSource` を `SearchHistory`（新しい型のコレクション）にバインド。各項目の左側アイコンを、現在の固定 `Clock` から「通常検索なら `Search`（虫眼鏡）、インデックス検索なら `Zap`（雷）」に切り替える。例: `ItemsSource` を `SearchHistory` にし、アイコンの `Kind` を `SearchHistoryItem.IsIndexSearch` にバインドするコンバーター（bool → Lucide の Kind）で `Search` / `Zap` を出し分ける。 |
| **TabContentControl.xaml.cs** | `SearchHistoryItem_PreviewMouseLeftButtonDown` や Enter キー処理で、選択アイテムを `string` ではなく `SearchHistoryItem`（またはキーワード＋モード）として扱い、キーワードを `SelectSearchHistory` に渡す。必要なら選択した履歴の `IsIndexSearch` で `IsIndexSearchMode` を更新。 |

### 4. 既存挙動との整合

- **履歴から選択して検索**: クリック／Enter で「そのキーワードで検索」は現状どおり。オプションで「選択した履歴がインデックス検索だった場合は、その選択時にインデックスモードに切り替える」とすると、履歴と現在モードの対応が分かりやすくなる。
- **件数上限（100 件）**: 通常＋インデックスを別レコードとする場合、「キーワード＋モード」の組み合わせで最大 100 件まで、という仕様にすると既存のクリーンアップ処理と整合する。

---

## 実装時の注意点

1. **主キー設計**  
   sqlite-net では単一列の `[PrimaryKey]` が一般的なため、`Key = $"{Keyword}\t{(IsIndexSearch ? "1" : "0")}"` のような複合文字列を PK にし、`Keyword` と `IsIndexSearch` は別プロパティとして持たせる形が扱いやすい。既存の `Keyword` を PK のままにすると、同じキーワードで「通常」と「インデックス」の 2 件を保持できない。

2. **既存 DB**  
   既存ユーザーの DB に `IsIndexSearch` 列を追加する場合、ALTER で列追加し、既存行は `false`（通常検索）で埋める。新規インストール時は最初から新スキーマでテーブル作成する。

3. **アイコン**  
   虫眼鏡は既存の `Kind="Search"`、雷は `Kind="Zap"` で、検索バーと履歴で統一する。

---

## まとめ

- **可否**: 可能。
- **方針**: 履歴を「キーワード＋通常／インデックス」で保存し、一覧表示時に `IsIndexSearch` に応じて虫眼鏡（Search）／雷（Zap）を出し分ける。保存・取得・UI の 3 層を上記のとおり変更する。

この方針で実装すれば、ご要望の「通常検索時は虫眼鏡、インデックス検索時は雷を履歴に表示する」を満たせます。
