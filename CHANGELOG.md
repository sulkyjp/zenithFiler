


# Zenith Filer - Version History

## [0.25.2] - 2026-03-07

### Fixed
- 自動アップデート適用時に Encoding 932 (Shift_JIS) が未登録で `NotSupportedException` が発生する問題を修正（UTF-8 BOM + `chcp 65001` に変更）

## [0.25.1] - 2026-03-07

### Changed
- **Effects ON/OFF リファクタリング**: 演出カテゴリ設定の重複コードを共通化
  - WindowSettings: 9個の個別 `SetShowXxxRuntime` を `SetEffectCategoryRuntime` に統合、`SaveShowScanBarOnly` を `SaveEffectCategoryOnly` に統合
  - AppSettingsViewModel: `ApplyEffectCategory` ヘルパーで8個の OnChanged ハンドラを式本体に簡素化、Load メソッドの9フィールド代入をタプル分解で集約
  - TabContentControl: 4つの Popup アニメーションを `AnimatePopupOpen` ヘルパーに共通化、`ShowDragAdorner` をアーリーリターンに変更
  - MainWindow: Preview 3メソッドの分岐方向を `if (!enabled)` に統一

## [0.25.0] - 2026-03-07

### Added
- **演出カテゴリ9セクション化**: 演出効果を9カテゴリに分類し、カテゴリ別にON/OFFトグルで制御可能に
  - A. 起動・全体（ShowStartupEffects）: ウェルカムアニメーション、ローディングオーバーレイ
  - B. GlowBar（ShowGlowBar）: 進捗バー全体のフェード・補間・グロー
  - C. スキャンバー（ShowScanBar）: フォルダ読み込み時のスキャンバー（既存）
  - D. タブ操作（ShowTabEffects）: タブインジケータースライド、コンテンツフェード、D&Dマーカー
  - E. ペイン・トランジション（ShowPaneTransitions）: ペインフェード、Control Deck展開/閉じ、サイドバー幅アニメーション
  - F. テーマ（ShowThemeEffects）: テーマ切替オーバーレイ、テーマトースト通知
  - H. クイックプレビュー（ShowPreviewEffects）: プレビュー開閉アニメーション
  - I. ファイル一覧（ShowListEffects）: ホバーエフェクト、コンテキストメニューアニメ
  - L. ドラッグ＆ドロップ（ShowDragEffects）: ドラッグアドーナー表示

### Changed
- **旧設定マイグレーション**: EnableMicroAnimations→D/E/H、EnableListAnimations→I、ShowWelcomeAnimation→A に自動変換
- **旧設定廃止**: EnableMicroAnimations・EnableListAnimations・ShowWelcomeAnimation の static フラグ・ランタイムセッター・SaveOnly を削除（JSON 互換のためインスタンスプロパティは残留）

## [0.24.6] - 2026-03-07

### Added
- **演出カテゴリ新設**: Control Deck に「演出」カテゴリを新設し、アニメーション・視覚効果関連の設定を統合。ウェルカムアニメーション（General から移動）、マイクロアニメーション・一覧表示アニメーション（Display から移動）、新規のスキャンバー設定の4項目を配置
- **スキャンバー設定**: フォルダ読み込み・検索中に表示されるスキャンバー（流れるプログレスバー）の表示/非表示を設定可能に（演出カテゴリ）

## [0.24.5] - 2026-03-07

### Added
- **ウェルカムアニメーション設定**: 起動時のステータスバーウェルカムアニメーションの表示/非表示を設定可能に（General カテゴリ → 表示セクション）。オフにするとステータスバーが即表示される

## [0.24.4] - 2026-03-07

### Changed
- **起動シーケンス高速化**: スプラッシュスクリーンを廃止し、サービス初期化（TrayIcon / CpuIdle / Update）をレンダリング完了後に遅延実行するよう変更。起動時の白画面ギャップを軽減
- **Welcome アニメーション短縮**: 通常起動時のステータスバーウェルカムアニメーションを約10秒から約3.5秒に短縮（初回起動時は従来通り）
- **白画面解消**: ウィンドウ表示を ContentRendered 完了後に遅延させ、コンテンツが描画済みの状態で一発表示するよう変更。起動時の白画面フラッシュを解消
- **EULA 統合**: 初回起動時の EULA 同意をウェルカムウィンドウ内のページとして統合。別ダイアログでの表示を廃止（既存ユーザーの未同意時は従来のダイアログを継続使用）

## [0.24.3] - 2026-03-07

### Added
- **ダウンロードフォルダ自動ソート**: ダウンロードフォルダに移動した際、更新日時の新しい順に自動ソートする設定を追加（General カテゴリ）。他フォルダに移動するとデフォルトソートに戻る

### Changed
- **マニュアル最新化**: セクション8「インストール・ビルド構成」を刷新。themes/・scripts/ フォルダ、ZenithDocViewer.exe、自動アップデート、EULA の説明を追加。基本設定の説明に常駐モード・隠しファイル・拡張子表示・ダウンロード自動ソート・ショートカットキー・利用統計・バージョン情報カテゴリの記載を追加
- **マニュアル技術詳細**: セクション 4-8「技術的なしくみ」を 10 サブセクションに大幅拡充。使用ライブラリ一覧、Lucene フィールド定義、日本語トークナイズ、クエリ構築パイプライン（具体例付き）、インデクシングパイプライン、同時実行制御、SQLite スキーマ、FileSystemWatcher 連携を詳説

### Fixed
- **自動アップデート**: 更新検出後にダウンロードが開始されず「再起動して適用」ボタンが表示されない問題を修正。検出と同時に自動ダウンロードを開始するよう変更
- **自動アップデート**: アプリ終了時にダウンロード済み更新を自動適用する機能を追加。ボタン押下による即時適用と、次回終了時の自動適用の2通りで更新可能に
- **安定性向上**: IndexService の SemaphoreSlim 解放漏れ修正、TreeViewDragDropBehavior の同期 Dispatcher.Invoke をデッドロック防止のため非同期化、async void イベントハンドラへの try-catch 追加（9箇所）

## [0.24.2] - 2026-03-07

### Changed
- **Control Deck ナビ**: カテゴリナビを3グループ構成に整理（外観・操作 / 検索・データ管理 / アプリ情報）。ショートカットキーをテーマの後に移動し、セパレータを2本に変更

### Fixed
- **EULA**: アプリ終了時の設定保存で EULA 同意状態が上書きされ、毎回 EULA ダイアログが表示される問題を修正

## [0.24.1] - 2026-03-07

### Changed
- **EULA**: 使用許諾契約ダイアログをバージョン更新ごとの再表示から初回起動時のみの表示に変更

### Added
- **フォルダ TOP50**: 統計ページに「よく表示するフォルダ」テーブルを追加。アクセス回数降順で上位50件を表示し、星アイコンでお気に入りに追加可能
- **フォルダ TOP50 クリック**: フォルダ名またはパスをクリックするとエクスプローラで当該フォルダを表示（ホバー時に下線表示）

## [0.24.0] - 2026-03-07 : 利用統計ページ

### Added
- **利用統計**: Control Deck に「統計」カテゴリを追加し、各機能（検索・リネーム・コピー・削除・タブ操作・テーマ変更など全21アクション）の使用回数を表示
- **統計リセット**: 統計ページのリセットボタンで全カウントを初期化可能
- **永続化**: SQLite の `ActionStats` テーブルに記録し、アプリ再起動後もカウントを保持

### Fixed
- **統計ページ**: 未使用アクション（Count=0）も含む全21アクションを常に表示するよう修正
- **統計ページ UI**: インデックスページ風の Grid テーブルデザインに統一（Border コンテナ・ヘッダー行・セパレーター）
- **統計カウント**: `File.Move` が記録されない問題を修正。D&D/ペースト時に移動は `File.Move`、コピーは `File.Copy` として正しく記録されるよう改善。`DragDrop.Drop` はD&D操作自体の記録として分離
- **統計リアルタイム反映**: 操作実行時に統計ページの使用回数が即座に更新されるよう改善（再起動不要）

### Changed
- **統計テーブル**: 表示幅いっぱいに拡張し、「カウント契機」列を追加（機能・カウント契機・使用回数の3カラム構成）

## [0.23.0] - 2026-03-07 : GitHub API ベース自動アップデート機能

### Added
- **自動更新**: GitHub Releases API から新バージョンを自動検知し、ZIP ダウンロード → バッチスクリプトで上書き → 再起動までフル自動で行う `UpdateService` を追加
- **自動更新設定**: About ページに「自動更新を有効にする」チェックボックスと「更新を確認」ボタンを追加
- **更新適用 UI**: ダウンロード完了後に「再起動して適用」ボタンを表示し、ワンクリックで更新を適用
- **定期チェック**: 起動 30 秒後に初回チェック、以後 4 時間間隔でバックグラウンドチェック
- **GlowBar 進捗**: ダウンロード中は GlowBar でリアルタイム進捗表示
- **`--updated` 引数**: アップデート適用後の再起動時にトースト通知で更新完了を表示
- **設定永続化**: `AutoUpdate` / `LastUpdateCheck` / `SkippedVersion` を settings.json に保存
- **リリーススクリプト**: `scripts/pack.ps1` でリリース ZIP を自動作成

## [0.22.1] - 2026-03-07 : EULA 同意画面・コード署名基盤・About バージョン動的化

### Added
- **EULA**: 日本語シェアウェア向け使用許諾契約書（`EULA.md`）を追加
- **EULA ダイアログ**: 初回起動時（およびバージョン更新時）に EULA 同意画面を表示。同意しない場合はアプリを終了
- **コード署名**: 自己署名証明書の作成・EXE 署名・検証を行う PowerShell スクリプト（`scripts/sign.ps1`）を追加
- **csproj**: EULA.md の配布設定、署名ターゲットのコメント付きテンプレートを追加

### Changed
- **About ページ**: バージョン表示をハードコードから `MainViewModel.AppVersion` へのバインディングに変更

### Fixed
- **About ページ**: バージョン番号が `0.17.0` のまま更新されていなかった問題を修正

## [0.22.0] - 2026-03-06 : テーマ色指定の詳細化 — ハードコード色の DynamicResource 化

### Added
- **テーマシステム**: 12 個の新規テーマカラーキーを追加（PopupBackground/Text/Hover、FilterActiveIndicator、FilterChipBackground/Border、FilterToggleCheckedBackground、Success、PreviewPanelBorder、PreviewPdfBackground、IndexStatusNormal/Warm）
- **全 20 テーマ JSON**: 新 12 キーをテーマ別に最適化した値で追加

### Changed
- **TabContentControl.xaml**: フィルタポップアップ・スコープ・サイズ・日付パネル内の約 40 箇所のハードコード色を DynamicResource に置換
- **MainWindow.xaml**: Quick Preview、ステータスバー、テレメトリー、インデックス行の約 25 箇所を DynamicResource 化
- **FilePaneControl.xaml**: タブ一覧ポップアップ・閉じるボタンの 8 箇所を DynamicResource 化
- **RenameDialog**: ダイアログ全体（XAML + コードビハインド）の約 12 箇所を DynamicResource 化
- **ZenithDialog**: static frozen brush を動的リソース参照に変更（テーマ連動アイコン色）
- **ControlDeckView.xaml**: グロー DropShadowEffect・エラー表示・成功アイコンの 4 箇所を DynamicResource 化
- **ProjectSetsView.xaml**: フッターボタンテキスト・ホバー背景の 5 箇所を DynamicResource 化
- **MainWindow.xaml.cs**: ドロップハイライト・PDF ページ数テキストの 2 箇所をリソース参照に変更
- **DragAdorner.cs**: 枠線色を BorderBrush リソース参照に変更

### Fixed
- **視認性**: ダークテーマでフィルタポップアップの黒文字が背景に溶け込んで読めない問題を修正
- **視認性**: ライトテーマ以外で Quick Preview パネルのファイル名・補足テキストが見えない問題を修正
- **視認性**: テーマ切替時にリネームダイアログ・ZenithDialog のテキスト色が追従しない問題を修正

## [0.21.4] - 2026-03-06 : 起動安定性・UI レスポンス改善

### Changed
- **起動**: `OnStartup` を async 化し、`GetAwaiter().GetResult()` によるUIスレッドブロッキングを排除。テーマ設定抽出をメモリ上で完結するよう変更（`ReadThemeSettingsFromSettings` の sync File I/O を廃止）
- **検索結果**: `SilentObservableCollection.AddRange` を追加し、検索結果のバッチ追加で CollectionChanged 発火を大幅削減（500件で500回→10回）
- **検索結果**: `FilteredSearchResultCount` をキャッシュ化し、毎回の `Cast<object>().Count()` O(n) 走査を排除
- **タブ切替**: `FilePaneViewModel.NotifyAllPropertiesChanged` を `OnPropertyChanged(string.Empty)` 1回に集約（8回→1回）
- **レイアウト**: `LayoutUpdated` の常時購読を除去し、`DataContextChanged` に置換（不要な `UpdateColumnWidths` 連続発火を排除）
- **キーバインド**: `Dispatcher.Invoke`（ブロッキング）を `Dispatcher.InvokeAsync`（非ブロッキング）に置換（MainWindow、TabContentControl）
- **アイコン読込**: `DispatcherPriority.Normal` → `Background` に変更し、入力操作を優先。不要な `Task.Delay(30)` を削除
- **パンくずリスト**: `Typeface` インスタンスを static キャッシュ化し、幅推定ごとの再生成を排除

### Fixed
- **ファイル削除**: `DeleteItems` で HWND を UI スレッドで事前取得し、`Task.Run` 内の `Dispatcher.Invoke` ブロッキングを排除
- **インデックス**: 起動2秒後の `ConfigureIndexUpdate(null, ...)` が正しい設定を上書きしてインデックスを壊すバグを修正（二重呼び出しを削除）

## [0.21.3] - 2026-03-06 : リソースリーク修正・終了時タスク管理改善

### Fixed
- **安定性**: ShellThumbnailService に IDisposable を実装し、アプリ終了時に STA スレッドと BlockingCollection を確実に解放するよう修正
- **安定性**: App_Exit のシャットダウン順序を整備し、ShellThumbnailService の Dispose を追加（COM リソースの後始末を保証）
- **安定性**: IndexService の Dispose で `_globalIndexingCts` が Cancel/Dispose されない問題を修正

## [0.21.2] - 2026-03-06 : 7-zip コンテキストメニュー修正・内部リファクタリング

### Fixed
- **コンテキストメニュー**: 7-zip 等のシェル拡張メニュー項目をクリックしても実行されない問題を修正（Vanara の `CMINVOKECOMMANDINFOEX.lpVerbW` が String 型のため `SafeResourceId` が MAKEINTRESOURCE ではなく不正なポインタにマーシャリングされていた。`CMIC_MASK_UNICODE` を除去し `lpVerb`（ResourceId 型）のみ使用するよう変更）
- **コンテキストメニュー**: `GetMenuItemText` の `MIIM_STRING` 定数が `0x80`（MIIM_BITMAP）になっていたバグを修正。メニュー項目テキストが文字化けし、削除判定のフォールバックが機能しない原因だった

### Fixed
- **検索バー**: ペイン幅が狭い場合にフィルタボタンが検索キーワード入力欄のスペースを圧迫する問題を修正（テキスト入力列に `MinWidth` を設定し、キーワード表示を最優先化）
- **検索履歴**: ポップアップ幅が狭い場合に検索条件（プリセット・サイズ・日付）がキーワード表示を圧迫する問題を修正（キーワード列に `MinWidth`、条件列に `MaxWidth` を設定。条件表示を StackPanel から TextBlock+Run に変更し `TextTrimming` を有効化）

### Changed
- **ShellContextMenu リファクタリング**: GetCommandString の重複パターンを `TryGetCommandVerb`/`GetCommandStringCore` に統合、削除判定を `IsDeleteCommand` に集約、`GetMenuItemText` を再帰化（ハードコード3階層→深度制限付き再帰）、MIIM 定数を `NativeMethods` に統合して `CloudShellMenuService` と共有、デッドコード除去

## [0.21.1] - 2026-03-06 : グローバルホットキーのカスタマイズ対応

### Changed
- **ショートカット**: 「アクティブペインにフォーカス」のショートカット変更がグローバルホットキー（トレイ復帰）にも反映されるように改善

### Fixed
- **トレイ復帰**: トレイ格納状態からグローバルホットキーでウィンドウが再表示されない問題を修正（`Hide()` 後に `Show()` が呼ばれていなかった）

## [0.21.0] - 2026-03-06 : 検索通知の重複表示を根本修正

### Fixed
- **通知**: NotificationService を即時上書き方式に変更し、通知の重複表示を根本修正（pending 方式を廃止、同一メッセージ抑制 + タイマーリセットで即座に差し替え）
- **検索通知**: 検索実行時にGlowBarステータステキストと通知メッセージが重複表示される問題を修正（GlowBarのテキスト表示を廃止しプログレスバーのみに統一、通知エリアをGlowBar状態から独立化、同一メッセージ通知時のタイマーリセット、HideStatusAfterDelayAsync のレース条件修正）

## [0.20.9] - 2026-03-05 : ステータスバー通知の重複表示を抑制

### Fixed
- **通知**: 同一メッセージが連続してステータスバーに表示される問題を修正（表示中・pending ともに直前と同一メッセージを抑制）

## [0.20.8] - 2026-03-05 : 設定カード名の明確化

### Changed
- **設定画面**: 基本設定ページの「表示」カード名を「ファイル表示」に変更し、表示ページとの区別を明確化

## [0.20.7] - 2026-03-05 : 通知設定を表示ページに統合

### Changed
- **設定画面**: 「通知の表示時間」を基本設定ページから表示ページへ移動し、「起動時の通知」と統合した「通知」カードにまとめた

## [0.20.6] - 2026-03-05 : Ctrl+終了で常駐モードバイパス

### Added
- **常駐モード即終了**: Ctrl キーを押しながら Alt+F4 または × ボタンを押すと、常駐モードが有効でもトレイに隠れずに即座にアプリを終了できるように

## [0.20.5] - 2026-03-05 : インデックス スケジュール実行バグ修正

### Fixed
- **インデックス スケジュール**: アイテム別スケジュール（曜日・時刻）設定が実際には無視されるバグを修正（`ConfigureIndexUpdate` に `itemSettings` が渡されていなかった）

## [0.20.4] - 2026-03-05 : パンくずリスト表示不具合の修正

### Fixed
- **パンくずリスト**: フォルダ名が表示されない不具合を修正（XAML バインディングが存在しないプロパティを参照していた）

## [0.20.3] - 2026-03-05 : インデックスアイテム別設定サマリーテーブル

### Added
- **インデックスアイテム別設定サマリーテーブル**: Control Deck › インデックス設定ページの最下部に、登録済みアイテムの設定一覧テーブルを追加。フォルダ名・ロック状態・スケジュール・更新方式を俯瞰可能に
- 行ダブルクリックまたは⚙ボタンで既存の詳細設定ポップアップを直接起動し、OK で即時反映

## [0.20.2] - 2026-03-05 : インデックスアイテム別詳細設定ポップアップ

### Added
- **インデックスアイテム別詳細設定ポップアップ**: 右クリック「詳細設定...」から専用ダイアログを表示。スケジュール（曜日・時刻）・ロック（アーカイブ）・更新方式（グローバル/差分/フル再作成）を一箇所で管理可能に
- **Per-Item 更新方式（IndexItemUpdateMode）**: アイテムごとに差分更新・フル再作成を選択可能。未設定時はグローバル設定に従う
- **IndexScheduleDto → IndexItemSettingsDto マイグレーション**: 旧スケジュール設定から新詳細設定への自動変換

### Changed
- **コンテキストメニュー簡素化**: スケジュール設定サブメニュー（曜日・時刻の多階層メニュー）を削除し、「詳細設定...」1項目に集約。操作性を改善
- **IndexService**: 定期更新時に Per-Item UpdateMode をチェックし、FullRebuild 指定のアイテムは削除+再スキャン、Incremental は差分更新にルーティング

## [0.20.1] - 2026-03-05 : インデックスアイテム別スケジュール設定 UI 追加

### Added
- **インデックスアイテム別スケジュール設定 UI**: インデックス対象フォルダの右クリックメニューに「スケジュール設定」サブメニューを追加。曜日チェック（月～日）・時刻選択（3時間刻み 8段階）・リセットを GUI から操作可能に
- **スケジュール設定インジケーター**: カスタムスケジュールが設定されたアイテムにカレンダーアイコンを表示。ToolTip でスケジュール内容を確認可能

## [0.20.0] - 2026-03-05 : 設定オプション拡充（基本設定 4 項目 + インデックス設定 2 項目）

### Added
- **タイトルバーのパス表示切替**: 基本設定 › 表示で、タイトルバーにフォルダパスを表示するかを選択可能に（デフォルト: ON）
- **ファイル拡張子の表示切替**: 基本設定 › 表示で、ファイル名の拡張子表示/非表示を切替可能に（デフォルト: ON）。ToolTip ではフルネームを表示
- **隠しファイルの表示切替**: 基本設定 › 表示で、Windows の隠し属性ファイル・フォルダの表示/非表示を切替可能に（デフォルト: OFF）
- **常駐モード（タスクトレイ）**: 基本設定 › 操作で、ウィンドウの×ボタンでトレイに最小化する常駐モードを追加（デフォルト: OFF）。トレイアイコンのダブルクリックで復帰、右クリック「終了」で完全終了
- **インデックスのアイテム別スケジュール**: インデックス対象フォルダごとに更新曜日・時刻を個別設定可能に。未設定のフォルダはグローバルの更新間隔に従う
- **CPU アイドル時のみインデックス更新**: インデックス設定 › パフォーマンスと負荷で、CPU 使用率が閾値以下の時にのみインデックスを更新するオプションを追加（閾値: 10%/20%/30% から選択）

## [0.19.6] - 2026-03-05 : タイトルバー文字色テーマ連動

### Changed
- **タイトルバー文字色・ボタン色がテーマに連動**: `TitleBarTextColor` を新設し、タイトルバーのテキスト・ウィンドウ制御ボタン（ー □ ×）の色がテーマごとに自動切替。全20テーマに世界観に合った個別色を設定

### Fixed
- **テーマ切替時にタイトルバー文字色が反映されない不具合を修正**: `ThemeBaseColors` に `TitleBarTextColor` プロパティが未定義だったため、JSONデシリアライズ時に値が無視されていた問題を修正

## [0.19.5] - 2026-03-05 : 内部リファクタリング・パフォーマンス改善

### Changed
- **ShellThumbnailService**: LRU キャッシュを `List<string>` から `LinkedList` + `Dictionary` に変更し、キャッシュ操作を O(n) → O(1) に改善。バリデーション・キャッシュ参照の重複コードを `TryGetCachedOrValidate` に抽出
- **ThemeService**: `MergeCategory()` のリフレクション結果を `ConcurrentDictionary` でキャッシュし、テーマ切替時の重複リフレクションを排除
- **DirectoryTreeViewModel**: `LoadDrivesAsync()` の `isReadyTask.Wait(500)` ブロッキングを `Task.WhenAny` + `Task.Delay` パターンに変更し、全ドライブを並列チェック
- **DatabaseService**: `CleanupSearchHistoryAsync` / `CleanupRenameHistoryAsync` の `.ConvertAll().Cast<object>().ToArray()` 3段階アロケーションを `.Select(r => (object)r.Key).ToArray()` にインライン化
- **Fire-and-forget**: `TaskHelper.FireAndForget` 拡張メソッドを導入し、`_ = SomeAsync()` パターン（AppSettingsViewModel 3箇所・MainViewModel 4箇所）を例外ログ付きに置換
- **TabItemViewModel**: `ContinueWith(OnlyOnFaulted)` パターン2箇所を async/await + `FireAndForget` にモダン化

## [0.19.4] - 2026-03-05 : バックアップ一覧ページネーション

### Added
- **バックアップ一覧にページネーション機能**: バックアップ件数が20件を超える場合、20件ごとのページ分割で表示。`< 1, 2, …, 46, 47 >` 形式のページ番号バーで任意ページにジャンプ可能。左右ボタンで前後ページ移動。20件以下の場合はバー非表示

### Fixed
- **起動時のUIフリーズを軽減**: テーマスキャン・テーマJSON読み込みをバックグラウンドスレッドで並列実行し、リソース辞書のXAMLパースと同時進行するよう最適化。バックアップ一覧の読み込みも非同期化

## [0.19.3] - 2026-03-05 : バックアップ一覧インライン表示・概要編集

### Added
- **設定 › リカバリにバックアップ一覧をインライン表示**: BackupListDialog を廃止し、設定画面内でバックアップの一覧表示・復元・ロック切替を完結。各行に日時・ロック状態・変更概要・編集/復元ボタンを配置
- **変更概要の編集機能**: バックアップの変更概要をインラインで編集可能に。鉛筆ボタンクリック → テキスト入力 → Enter で確定、Escape でキャンセル。ロック中は編集不可

### Changed
- **BackupListDialog を削除**: 別ウィンドウでのバックアップ一覧表示を廃止し、設定画面の Backup カテゴリ内にインライン統合

## [0.19.2] - 2026-03-05 : ショートカットキー説明追加

### Added
- **ショートカットキー設定画面に概要説明を追加**: 各アクションの下に機能説明とおすすめキー設定の情報を表示。KeyBindingDefinition に Description プロパティを追加し、全25アクションに日本語の説明文を設定

## [0.19.1] - 2026-03-05 : ショートカットキー画面修正

### Fixed
- **ショートカットキー設定画面を開くとアプリが異常終了する問題を修正**: `NonEmptyStringToVisibilityConverter` が `MainWindow.xaml` のローカルリソースにのみ定義されており、`ControlDeckView.xaml` からは参照スコープ外だったため `StaticResource` 解決に失敗していた。`ControlDeckView.xaml` の `UserControl.Resources` にコンバーターを追加
- **個別キーバインドのリセットボタンが機能しない問題を修正**: `ResetKeyBinding` コマンド実行後に UI の再ロード（`LoadKeyBindings`）が呼ばれておらず、表示が更新されなかった

## [0.19.0] - 2026-03-05 : ショートカットキーカスタマイズ

### Added
- **設定 › ショートカットキー（新規カテゴリ）**: キーボードショートカットのカスタマイズ機能を追加。全25アクション（グローバル/サイドバー/ウィンドウ/ファイル一覧）のキーバインドをクリック→キー入力で変更可能
- **HotkeyRecorderControl**: クリックで録音モードに入り、任意のキー＋修飾子をキャプチャするカスタムコントロール
- **KeyBindingService**: キーバインドの中央レジストリ。デフォルト値の管理、競合検出、カスタムバインドの永続化を提供
- **ツールチップ動的化**: サイドバー切替ボタン・ツールバーボタンのショートカット表示をキーバインド設定に連動して動的に更新

### Changed
- **InputBindings 動的構築**: MainWindow の InputBindings を XAML ハードコードからコードビハインドでの動的構築に移行し、キーバインド変更の即時反映を実現
- **キー判定の一元化**: TabContentControl / MainWindow の PreviewKeyDown 内のハードコードキー判定を `App.KeyBindings.Matches()` に統一
- **ツールチップからショートカット表記を削除**: FilePaneControl（タブ追加/閉じる）、ControlDeckView（閉じる）のツールチップからハードコードされたショートカット表記を削除
- **WelcomeWindow のショートカット表示を削除**: 初回チュートリアルのショートカット TextBlock を6箇所削除（カスタマイズ可能になったため）
- **MANUAL.md**: ショートカット一覧に「標準のキー割り当て」注釈を追加

## [0.18.0] - 2026-03-05 : シェアウェアライセンス基盤

### Added
- **シェアウェアライセンス基盤**: 有償機能の使用回数カウント（SQLite `UsageRecords` テーブル）とロックファイル（`.zenith_license`）による全機能解除の仕組みを追加
- **LicenseService**: `App.License` として提供。`CanUseAsync` / `RecordUsageAsync` / `GetRemainingAsync` で任意の機能にガードを追加可能
- **設定 › ライセンス画面**: ライセンス状態バナー（Free/Full）と各機能の使用状況（使用回数・上限・ProgressBar）を表示するUIに更新
- **テーマ変更回数カウント**: テーマ変更を有償機能として `ThemeChange` キーで使用回数を追跡（ライセンス画面に表示）
- **ワーキングセット ツールチップ**: 保存済みセットをホバーすると A/B ペインの全タブのフルパスをポップアップ表示
- **テーマタイル選択グロー拡張**: ランダムモード・ランダム抽選時にも、適用中テーマのタイルにプリセットモードと同じ選択グロー（AccentBrush 枠）を表示するよう変更

## [0.17.1] - 2026-03-05 : クラウドドライブお気に入りの遅延アクセス対応

### Fixed
- **BOX Drive / OneDrive 等クラウドドライブのお気に入りが初回クリック時に「パスが見つかりません」となる問題を修正**: クラウドパス（Box / SPO）のオンデマンドハイドレーション遅延に対応し、親ディレクトリへのアクセスでトリガー後に最大2回リトライするよう変更

## [0.17.0] - 2026-03-05 : 設定ページ拡充（Display カテゴリ新設・General/Search 設定追加）

### Added
- **設定 › 表示 › 一覧表示アニメーション**: ファイル一覧のホバーエフェクト（アイコンビューのオーバーレイフェード・お気に入りボタン円背景・検索アイコンスケール）の ON/OFF を追加
- **設定 › 表示 › 起動時の通知**: 起動時テーマ適用トーストの ON/OFF 設定をテーマページから表示ページへ移動し、詳細説明を追加
- **設定 › ライセンス（新規カテゴリ）**: シェアウェア化に向けてライセンス登録ページを追加（現在は準備中プレースホルダー）
- **設定 › バージョン情報（新規カテゴリ）**: アプリ概要・バージョン・作者情報を表示するページを追加
- **設定 › 表示（新規カテゴリ）**: ファイル一覧の行高をコンパクト(24px) / 標準(32px) / ゆったり(40px) から選択できるように。設定は即座に反映され再起動後も保持される
- **設定 › 表示 › マイクロアニメーション**: ホバーフェード・タブ切替トランジション等の ON/OFF を設定ページから直接切り替えできるように
- **設定 › 基本設定 › 操作**: シングルクリックでフォルダを開くモード、ファイル削除時の確認ダイアログ表示、起動時タブ復元の各トグルを追加
- **設定 › 基本設定 › 通知の表示時間**: トースト通知の表示時間を短め(1.5秒) / 標準(3秒) / 長め(5秒) から選択できるように
- **設定 › 表示 › 新規タブのデフォルト表示**: 新規タブ作成時のフォルダ先頭表示・ソートプロパティ・ソート方向のデフォルト値を設定できるように

### Changed
- **設定 › 表示 › 新規タブのデフォルト表示**: 検索ページから表示ページへ移動し、「Ctrl+T やパスを指定して開いた新規タブに適用される」旨の説明文を追加
- **設定 › 基本設定 › 通知の表示時間**: 説明テキスト「ファイルのコピー・移動・削除の完了や、テーマ変更などの操作結果をステータスバーに表示する時間を設定します」を追加
- **設定 › 基本設定 › 操作**: 各チェックボックス（シングルクリック・削除確認・タブ復元）の下に補足説明を追加
- **設定 › 表示 › ファイル一覧の行高**: 説明テキスト「ファイル一覧の各行の高さを調整します。コンパクトにすると一度に多くのファイルを表示でき、ゆったりにするとクリックしやすくなります」を追加
- **テーマページのヘッダーを簡略化**: 「通知を表示」チェックボックスをテーマセクションのヘッダーから削除し、設定 › 表示に専用カードとして移動
- **設定ナビゲーションの並び順を変更**: 基本設定 → 表示 → テーマ → 検索 → インデックス → リカバリ → ライセンス → バージョン情報 の順に整理し、ライセンス・バージョン情報の前にセパレーターを追加
- `WindowSettings.cs`: `ListRowHeight`, `SingleClickOpenFolder`, `ConfirmDelete`, `RestoreTabsOnStartup`, `NotificationDurationMs`, `DefaultGroupFoldersFirst`, `DefaultSortProperty`, `DefaultSortDirection` の各フィールドを追加。それぞれ static アクセサ・runtime setter・デバウンス保存ヘルパーを整備
- `NotificationService.cs`: `Notify` のデフォルト表示時間を固定値 3000ms から `WindowSettings.NotificationDurationMsValue` に変更
- `TabItemViewModel.cs`: `_isGroupFoldersFirst` / `_sortProperty` / `_sortDirection` のフィールド初期値をグローバル設定から取得するよう変更。新規タブのデフォルト表示に即座に反映
- `MainViewModel.cs`: `RestoreTabsOnStartup = false` の場合、ホームフォルダのみで起動するよう `InitializeAsync` を変更
- `TabContentControl.xaml.cs`: シングルクリックモード時にフォルダをシングルクリックでナビゲーション、ダブルクリックの重複実行を防止

### Fixed
- **「新規タブのデフォルト表示」のソート方向設定が反映されないバグを修正**: ConverterParameter に文字列 "Ascending"/"Descending" を渡していたため `ListSortDirection` enum への変換が失敗していた。`{x:Static}` で enum 値を直接渡すよう修正
- **起動時タブ復元 OFF で起動後にペインが空になるバグを修正**: `RestoreTabsOnStartup = false` 時に生成する `PaneSettings` の `TabPaths` が空リストのため `RestoreTabsAsync` が早期 return していた。`EnsureTabPaths` を適用してホームフォルダ（未設定時はデスクトップ/ダウンロード）を確実にセットするよう修正
- **マイクロアニメーション OFF 時にタブインジケーターのスライドが止まらないバグを修正**: `TabIndicatorSlideBehavior` が `MicroAnimationsEnabled` を参照しておらず、設定に関わらずインジケーターがアニメーションし続けていた
- **マイクロアニメーション OFF 時に D&D 挿入マーカーのフェードが止まらないバグを修正**: `TabControlDragDropBehavior.FadeIn()` が `MicroAnimationsEnabled` を参照しておらず、設定に関わらずフェードインが発生していた
- **設定ページのカテゴリ切り替えアニメーションを削除**: `ControlDeckView.xaml.cs` の `AnimateCategorySwitch()` (フェードアウト80ms + フェードイン120ms) を除去し、即時切り替えに変更
- **ファイル一覧の行高設定が反映されないバグを修正**: `FileListView` の inline `ItemContainerStyle` が `AppResources.xaml` の implicit style を上書きしており `MinHeight` が届いていなかった。inline スタイルに `<Setter Property="MinHeight" Value="{DynamicResource ListRowHeight}"/>` を追加
- **v0.17.0 追加設定がアプリ終了時にリセットされるバグを修正**: `MainWindow_Closing` の終了時保存オブジェクトに `EnableMicroAnimations` / `ListRowHeight` / `SingleClickOpenFolder` / `ConfirmDelete` / `RestoreTabsOnStartup` / `NotificationDurationMs` / `DefaultGroupFoldersFirst` / `DefaultSortProperty` / `DefaultSortDirection` が含まれておらず、終了のたびにデフォルト値で上書きされていた

## [0.16.9] - 2026-03-04 : WPF-UI implicit style 未定義による UI テーマ非追従を修正

### Added
- **テーマ切り替えトランジションアニメーション**: テーマ変更時に背景色ディゾルブオーバーレイ（フェードイン 130ms → フェードアウト 250ms）で滑らかに切り替わるようになった

### Fixed
- **履歴ビューの各行の背景がテーマに追従しないグレーになる問題を修正**: `AppResources.xaml` が `<ui:ControlsDictionary />` を取り込んでいるが `Expander` の implicit style が未定義のため WPF-UI のグレーカード背景が適用されていた。カスタム `Expander` implicit style を追加し、コンテンツ領域を透明テンプレートで上書き
- **アプリ設定 › リカバリの「今すぐバックアップ」「復元...」ボタンが他のボタンとデザインが異なる問題を修正**: `Button` の implicit style が未定義のため WPF-UI の Fluent アクセントスタイルが適用されていた。`x:Key="TextButton"` の名前付きスタイルを `AppResources.xaml` に追加し `ControlDeckView.xaml` のリカバリボタンに適用
- **アプリ設定 › A/Bペインのホームパス TextBox の背景色・罫線がテーマに追従しない問題を修正**: `TextBox` の implicit style が未定義のため WPF-UI の TextBox スタイル（ReadOnly 状態でグレー上書き等）が適用されていた。カスタム `TextBox` implicit style を `AppResources.xaml` に追加し、すべての色を `DynamicResource` で参照するテンプレートで上書き
  - `MainWindow.xaml`: `ThemeTransitionOverlay`（ZIndex=850）を追加
  - `MainWindow.xaml.cs`: `AnimateThemeTransitionBeginAsync` / `AnimateThemeTransitionEnd` を実装し、デリゲートとして ViewModel に配線
  - `AppSettingsViewModel.cs`: `ApplyThemeAnimatedAsync` ヘルパーを追加。`OnSelectedThemeNameChanged` および `DrawRandomThemeAsync` からアニメーション付き適用を呼び出すよう変更。連続クリック時はアニメなしで即時適用

## [0.16.8] - 2026-03-04 : 起動後フリーズ改善（パンくずリスト非同期化）

### Fixed
- **起動直後に少しの間操作できないフリーズを改善**: タブ復元時（`RestoreTabsAsync`）のコンストラクタ内で `UpdatePathSegments()` が ShellItem COM 呼び出しをUIスレッドで同期実行していたため、タブ数 × パス深度分のブロックが発生していた（例：4タブ × 深さ4 = 16回のCOM呼び出し）
  - `TabItemViewModel.cs`: `UpdatePathSegments()` を3つのメソッドに分離
    - `UpdatePathSegmentsAsync()` — `Task.Run` でコアを呼び出しUIスレッドに反映するオーケストレーター
    - `ComputePathSegmentsCore(string path)` — ShellItem COM 呼び出しを含む純粋な計算（バックグラウンド・スレッドセーフ）
    - `ComputePathSegmentsFallback(string path)` — 例外時の単純文字列分割フォールバック
  - コンストラクタおよびナビゲーション完了後の呼び出しを `_ = UpdatePathSegmentsAsync()` に変更
  - パンくずリストの表示は `await` 完了後に更新されるが、バックグラウンド処理が高速なため視覚的な遅延は生じない
  - パス変更中に別ナビゲーションが来た場合は結果を破棄（`CurrentPath != pathAtStart` チェック）

## [0.16.7] - 2026-03-04 : ファイル操作後のUIフリーズ改善

### Fixed
- **ファイル操作・自動リフレッシュ後にUIが短時間フリーズする問題を改善**: `LoadDirectoryAsync` の Dispatcher ブロック内で `MergeItems` と `ApplySort` を連続実行していたため、500〜2000件規模のフォルダで30〜200msのUIスレッドブロックが発生していた
  - `TabItemViewModel.cs`: `MergeItems`（リスト更新）と `ApplySort`（ソート）を別の `Dispatcher.InvokeAsync(Background)` に分割し、両者の間でキーボード・マウス入力を処理できる隙間を確保
  - `AppSettingsViewModel.cs`: `DrawRandomTheme()` で毎回 `ScanThemes()`（ディスクI/O）を呼んでいた箇所をキャッシュ済みの `AvailableThemes` を使うよう変更

## [0.16.6] - 2026-03-04 : ホーム設定カードのテキスト・アイコン不可視問題を修正

### Fixed
- **Aペイン・Bペインのホーム設定カードでテキストとアイコンが不可視になる問題を修正**: 暗色テーマ（blueprint 等）でカード背景と同色の `InputBackgroundBrush` を TextBox 背景に使用した際、Foreground 未指定によりテキストが見えなくなっていた
  - `ControlDeckView.xaml`: Aペイン・Bペインのホームパス TextBox に `Foreground="{DynamicResource TextBrush}"` を追加
  - `AppResources.xaml`: `IconButton` スタイルに `Foreground="{DynamicResource TextBrush}"` を追加（`IconToggleButton` と統一）。アイコンが暗色カード上で不可視になる問題を解消

## [0.16.5] - 2026-03-04 : ショートカット全無効バグ修正

### Fixed
- **ControlDeck を閉じるとショートカットキーが全て無効になる問題を修正**: ControlDeck（設定パネル）を Esc キー等で閉じると、オーバーレイが `Collapsed` になった際に ControlDeckView 内の要素（RadioButton 等）が持っていたフォーカスが失われ、`Keyboard.FocusedElement` が `null` になっていた。この状態では WPF のキーイベントルーティングが機能せず全ショートカットが無効になっていた
  - `CloseControlDeckAsync()` 完了後にアクティブペインのファイルリストへフォーカスを明示的に復元するよう修正
  - `MainWindow.Activated` ハンドラーを追加し、ウィンドウがアクティブ化した際にペイン内にフォーカスがなければ自動復元する安全網を追加

## [0.16.4] - 2026-03-04 : コンテキストメニュー白飛び根本修正

### Fixed
- **コンテキストメニューの白背景問題を根本修正**: `App.xaml` に定義された `Style TargetType="ContextMenu"` がハードコードカラー（`#FEFEFE` / `#F5F5F0`）を使用していたため、テーマ変更が反映されず常に白背景になっていた問題を修正
  - `App.xaml` の `ContextMenu` スタイルテンプレート内の色をすべて `{DynamicResource}` 参照に変更（`ContextMenuBorderBrush` / `ContextMenuOuterBackgroundBrush` / `ContextMenuBackgroundBrush`）
  - `AppResources.xaml` の同名スタイル（`App.xaml` に隠されていた死コード）を削除

## [0.16.3] - 2026-03-04 : コンテキストメニュー・マニュアルページのテーマ対応修正

### Fixed
- **マニュアルウィンドウのテーマ非対応を修正**: ハードコードされた色をテーマ対応の DynamicResource に統一
  - `ManualWindow.xaml`: `Background="#F5F1E3"` → `{DynamicResource BackgroundBrush}`、ヘッダー・タブセグメント背景もテーマブラシに変更
  - `DocViewerControl.xaml`: `CardBackgroundBrush` / `CardHoverBrush` / `AcrylicSidebarBrush` / `AcrylicBackgroundBrush` をすべて `{DynamicResource}` に変更（テーマ変更で即時更新）
  - `MarkdownStyles.xaml`: テーブルヘッダー・セル罫線・引用ブロック・コードスパン・見出し2背景のハードコード色を `{DynamicResource}` に統一。グラデーションブラシを廃止しテーマの `SelectionBrush` / `ListHoverBrush` / `BorderBrush` / `InputBackgroundBrush` で代替
- **マニュアルウィンドウのテーマ非対応を修正**: ハードコードされた色をテーマ対応の DynamicResource に統一
  - `ManualWindow.xaml`: `Background="#F5F1E3"` → `{DynamicResource BackgroundBrush}`、ヘッダー・タブセグメント背景もテーマブラシに変更
  - `DocViewerControl.xaml`: `CardBackgroundBrush` / `CardHoverBrush` / `AcrylicSidebarBrush` / `AcrylicBackgroundBrush` をすべて `{DynamicResource}` に変更（テーマ変更で即時更新）
  - `MarkdownStyles.xaml`: テーブルヘッダー・セル罫線・引用ブロック・コードスパン・見出し2背景のハードコード色を `{DynamicResource}` に統一。グラデーションブラシを廃止しテーマの `SelectionBrush` / `ListHoverBrush` / `BorderBrush` / `InputBackgroundBrush` で代替

### Changed
- **blueprint テーマ**: テーマ Author を `KAKASAKA` → `K.AKASAKA` に変更

## [0.16.2] - 2026-03-04 : 起動トースト位置をコーナー通知スタイルに変更

### Removed
- **ControlDeck のペイン個別テーマ「将来実装」プレースホルダーを削除**: ペインごとのテーマ割り当てが v0.16.0 で実装済みのため、旧来の予告ラベルを除去

### Changed
- **起動時テーマトーストを右下コーナー通知に変更**: 画面中央から右下に移動し、操作を妨げない配置に改善
  - スライドイン演出を追加（下から上へ 14px、`QuadraticEase`、0.3s）
  - フェードアウトを 0.75s → 0.5s に短縮
  - サイズをコンパクト化（`MaxWidth 640 → 300`、パディング `22,14 → 16,12`、フォントサイズ 14 → 13）
  - `Grid.RowSpan="3"` 中央配置 → `Grid.Row="1"` 右下 `Margin="0,0,20,16"` に変更
  - テキストを `TextWrapping="Wrap"` → `NoWrap` に変更（コーナー通知は1行が自然なため）

## [0.16.1] - 2026-03-04 : 起動トースト通知改善・ON/OFF 設定追加

### Added
- **「通知を表示」トグル**: Control Deck テーマセクションのヘッダー右端に小型チェックボックスを追加。起動時テーマ適用トーストの表示／非表示を即座に切り替え・保存できる
  - `ShowStartupToast` フィールドを `settings.json` に追加（デフォルト `true`）
  - `WindowSettings.SaveShowStartupToastOnly` デバウンス保存メソッドを追加

### Changed
- **ペイン個別適用モードの起動トースト**: 全ペインのテーマを一覧表示するフォーマットに変更
  - 例: `Applied Themes` / `Navi: Standard  |  A: Midnight  |  B: Crimson`
  - パーソナライズ・自動選択モードは従来通り `Applied Theme` / `[テーマ名]` を表示
  - `NotificationService.ShowThemeToast` に `label` 引数を追加しヘッダー文字列を動的切り替え
- **トースト Border の幅制限**: `MaxWidth="640"` と `Margin="20,0"` を設定し、長いペイン名テキストでもはみ出さないよう調整
- **トーストラベルをバインディング化**: `Text="Applied Theme"` を `{Binding Notification.ThemeToastLabel}` に変更

## [0.16.0] - 2026-03-04 : ペイン個別テーマ適用

### Added
- **ペイン個別テーマ適用**: Control Deck の「ペイン個別適用」モードで、ナビペイン・Aペイン・Bペインに異なるテーマを独立して設定できるようになった
  - 各ペインの `ResourceDictionary` にテーマカラーを書き込み、WPF の DynamicResource 解決メカニズムにより自動的にペイン固有テーマが優先される
  - 選択したテーマは `settings.json` に `NavPaneThemeName` / `APaneThemeName` / `BPaneThemeName` として保存され、再起動後に復元される
  - 「パーソナライズ」または「自動選択モード」に切り替えると全ペインをリセットしてグローバルテーマに統一する
- **`WindowSettings` ペインテーマフィールド追加**: `NavPaneThemeName`・`APaneThemeName`・`BPaneThemeName` の 3 フィールドと `SavePaneThemeNames` デバウンス保存メソッドを追加
- **`AppSettingsViewModel` ペイン ResourceDictionary 管理**: `RegisterPaneResources`・`LoadPaneThemeNames`・`ClearPaneResources` メソッドを追加
- **`App.StartupPaneThemes`**: 起動時高速パーサーにペインテーマ名読み取りを追加し、`StartupPaneThemes` プロパティとして公開

## [0.15.2] - 2026-03-03 : 即時ランダム抽選ボタン追加・自動選択モードカード UI ブラッシュアップ

### Added
- **「今すぐ抽選」ボタン**: Control Deck の「自動選択モード」カード内に即時ランダム抽選ボタンを追加。起動を待たずに現在の抽選設定（全テーマ／カテゴリ指定）に従ってテーマをランダム選択・ライブ適用する
  - `DrawRandomThemeCommand`（CanExecute: `IsRandomModeActive`）として実装
  - AccentBrush 枠線スタイル・ホバー/プレス視覚フィードバック付き
  - 自動選択モード有効時のみ `Visibility="Visible"`、無効時は `Visibility="Collapsed"` で非表示
  - AccentBrush 塗りつぶし背景 + 白テキスト・アイコン・スケールアニメーション

### Changed
- **「今すぐ抽選」ボタンを AccentBrush 塗りつぶしアクションスタイルに変更**: 設定 RadioButton との役割差別化
  - AccentBrush 背景 + 白テキスト・アイコンで「実行スイッチ」として際立たせる
  - ホバー時: スケール 1.0→1.04 アニメーション（QuadraticEase）+ 白発光オーバーレイ (Opacity 0.15)
  - プレス時: スケール 0.96 の押し込み感 → リリースで 1.04 に戻るバウンス感
- **ThemeRandomizeDeck RadioButton をペイン個別適用と完全統一**: Opacity トリガーを除去し `IsEnabled="{Binding AppSettings.IsRandomModeActive}"` に変更。`OptionRadioButton` スタイル・制御方式ともにペイン個別適用セクションと同一に
- **「今すぐ抽選」ボタンのホバー検知を全域対応に修正**: `EventTrigger RoutedEvent="MouseEnter/Leave"` → `Trigger Property="IsMouseOver"` + `EnterActions/ExitActions` に置換。子要素がイベントをキャプチャした場合でも Button 全域でホバーを正確に検知する
- **ホバーオーバーレイが Padding 領域を含む全域を覆うよう修正**: ControlTemplate のルートを `Border (Padding="0,10")` → `Grid` に変更。AccentBrush 用 Border と HoverGlow Border をともに Grid 直下に配置し、Padding 由来の「境界線」アーティファクトを根本解消。ContentPresenter の Padding は `Margin="{TemplateBinding Padding}"` で等価再現
- **プレス演出をアニメーション競合なし方式に変更**: `EventTrigger PreviewMouseLeftButtonDown/Up` → `Trigger Property="IsPressed"` Setter（`Opacity=0.68`）でシンプルな押し込み感を実現。IsMouseOver アニメーションとの競合を排除
- **上部 Margin を 15px → 22px に拡大**: ゆとりある配置に調整
- **「今すぐ抽選」ボタンの Margin をボタン側に移動**: `Margin="0,22,0,0"` をボタン自身に設定。`Visibility="Collapsed"` 時に余白が残らない構造に改善

## [0.15.1] - 2026-03-03 : トーストUI位置修正・上部空白解消

### Fixed
- **トーストが Grid Row 0（TitleBar）に入り上部に空白が発生する問題を修正**: `Grid.RowSpan="3"` を付与してオーバーレイ配置に変更。レイアウトに影響しない
- **トースト表示位置を中央に変更**: `HorizontalAlignment="Center" VerticalAlignment="Center"` に修正。アイコン・フォントサイズも中央向けに拡大調整

## [0.15.0] - 2026-03-03 : テーマ永続化・ランダム選択エンジン・起動時トースト通知

### Added
- **テーマモード永続化**: `settings.json` に `CurrentThemeMode`・`AutoSelectSubMode`・`SelectedCategory`・`SavedThemeName` の4フィールドを追加。再起動後も「自動選択モード」「カテゴリ指定」「プリセット選択テーマ」が完全復元される
  - `MainWindow_Closing` の全体保存に4フィールドを含めることで、デフォルト値への上書きを防止
  - `App.StartupSavedThemeName` を `ReadThemeSettingsFromSettings` から取得・キャッシュし、`LoadThemeSettings` で `_savedThemeNameCache` を初期化
  - マイグレーション互換: `SavedThemeName` が旧 settings.json に未記録の場合は `ThemeName` でフォールバック
- **ランダム選択エンジン**: 起動時に `CurrentThemeMode == "Auto"` の場合、`PickRandomTheme` で前回適用テーマを除いたプールからランダム選択。`AutoSelectSubMode == "Category"` 時は指定カテゴリに絞って選択
- **起動時テーマトースト通知**: ウィンドウ描画完了後 900ms に右下コーナートーストで「Applied Theme: [テーマ名]」を表示。0.35秒でフェードイン → 2.5秒後に 0.75秒でフェードアウト

### Changed
- **カテゴリ説明文の MaxWidth 調整**: `ThemeCategoryDesc` TextBlock の MaxWidth を 250 → 280 に変更、右 Margin を 12 に拡張して折り返し安定化

## [0.14.14] - 2026-03-03 : カテゴリランダムモード・カテゴリ選択誘導インジケーター追加

### Added
- **カテゴリ選択誘導アンカーインジケーター**: 「現在のカテゴリからランダム」モードかつカテゴリ未選択の状態で、各カテゴリグループのヘッダーに「クリックして選択」バッジ（Hand アイコン + テキスト）をパルスアニメーション付きで表示。どれかカテゴリを選択すると非表示になる
  - **`IsAwaitingCategorySelection` プロパティ**: `IsRandomCategoryModeActive && string.IsNullOrEmpty(SelectedRandomCategory)` で算出。`ThemeRandomizeMode`・`ActiveThemeMode`・`SelectedRandomCategory` の変更時に自動更新
  - **XAML アンカー Badge**: `DataTrigger.EnterActions/ExitActions` + `BeginStoryboard/StopStoryboard` による Opacity パルスアニメーション（0.4→1.0、0.85秒、AutoReverse・無限ループ）。非表示時は `Visibility="Collapsed"` でレイアウト空間を占有しない

## [0.14.13] - 2026-03-03 : 自動選択モード RadioButton の直接選択バグ修正

### Fixed
- **「現在のカテゴリからランダム」直接選択バグ**: 他モード中に「現在のカテゴリからランダム」RadioButton をクリックすると必ず「全テーマからランダム」になってしまう問題を修正
  - **根本原因**: RadioButton StackPanel の `IsEnabled="{Binding AppSettings.IsRandomModeActive}"` により他モード中は RadioButton が無効化。無効コントロールはマウスイベントを処理しないため、クリックが親 Border の `MouseBinding` に素通りし `SetThemeModeCommand(Random)` が発火。`OnActiveThemeModeChanged` 内で `ThemeRandomizeMode==Disabled` → `AllThemes` に強制上書きされていた
  - **XAML 修正**: `IsEnabled` 属性を除去し、代わりに `<Style>` + `DataTrigger` で `Opacity="0.45"` の淡色表示に切り替え。RadioButton を常時クリック可能にしつつ非アクティブ時は視覚的に控えめに表示
  - **ViewModel 修正**: `OnThemeRandomizeModeChanged` に「`ThemeRandomizeMode` が非 Disabled 値に変更され かつ `ActiveThemeMode != Random` の場合、自動的に `ActiveThemeMode = Random` にする」処理を追加。RadioButton クリック単体でモード切り替えが完結するよう修正

## [0.14.12] - 2026-03-03 : 適用中テーマタイルのハイライト根本修正

### Fixed
- **適用中テーマタイルのハイライト根本修正**: `ListBoxItem.IsSelected` に依存する `MultiDataTrigger` を廃止。代わりに `IsCurrentPresetThemeConverter`（新規 `IMultiValueConverter`）を用いた `DataTrigger(MultiBinding)` に置き換え、`ThemeInfo.Name` と `AppSettings.SelectedThemeName` の直接文字列比較で現在適用中テーマを判定する。設定パネル起動直後（`LoadTheme()` による初期化後）から即座に AccentBrush 太枠（2.5px）+ SelectionBrush 背景が表示される

### Added
- **`IsCurrentPresetThemeConverter`**: `Converters.cs` に追加。`values[0]=ThemeInfo.Name`、`values[1]=SelectedThemeName`、`values[2]=IsAssignmentModeActive` を受け取り、プリセットモード中かつテーマ名が一致する場合のみ `true` を返す。ペイン・ランダムモード中は `IsAssignmentModeActive=true` のため自動的に `false`

## [0.14.11] - 2026-03-03 : カテゴリ選択 UI 強化・モードカード境界線強調・署名改善

### Changed
- **カテゴリホバー演出の強化**: 「カテゴリからランダム」モード時のホバーに `DropShadowEffect`（BlurRadius=10, Opacity=0.28, SoftBlue）を追加。`BorderThickness` を 1.5 → 2 に変更し、クリック可能な範囲を視覚的に明示
- **選択カテゴリの発光強化**: 選択カテゴリの `DropShadowEffect` を BlurRadius=18, Opacity=0.52 で設定。ホバーより強い光量で「選ばれた感」を表現。DropShadowEffect は WPF の `Setter.Value` を介して ControlTemplate trigger から直接設定
- **モードカード 選択境界線の太さ強化**: Preset / Random / Pane 3 枚のカード、アクティブ時の `BorderThickness` を `"2"` → `"2.5"` に拡張し、現在のアクティブモードをより確実に視認できるよう強調
- **適用中タイルのハイライト条件を厳密化**: `ListBoxItem` の選択状態 MultiDataTrigger の条件を `IsRandomCategoryModeActive=False` から `IsAssignmentModeActive=False` に変更。プリセットモード時のみ AccentBrush の太枠（2.5px）+ SelectionBrush 背景が表示され、ペイン・ランダムモード中は表示しない（カテゴリ枠 or ペイン選択が主役のため）
- **クリエイター署名の視認性向上**: `Design by KAKASAKA` の `FontSize` を `9` → `11` に拡大、`Opacity` を `0.45` → `0.55` に向上、`Margin` を `0,3,0,0` → `0,4,0,0` に調整

## [0.14.10] - 2026-03-03 : カテゴリカラム構造安定化・選択 UI 整理

### Changed
- **GroupItem Border 常設化**: カテゴリグループ枠（CategoryGroupBorder）の `Padding="16,12,16,16"` と `Margin="0,0,0,12"` をモード条件なしで常時適用。`BorderBrush="Transparent"` をデフォルトとし、枠は見えないまま常に一定の余白構造を維持するよう変更
- **カテゴリ枠トリガーの簡素化**: IsRandomCategoryModeActive DataTrigger から Padding・Margin・BorderThickness・BorderBrush の条件付き変更を削除。カーソル変更（`Cursor="Hand"`）のみに絞り、余白・枠は常時デフォルト値で安定
- **選択カテゴリ枠の明確化**: IsCategorySelected DataTrigger の BorderThickness を 2.5 → 2 に統一。AccentBrush 太枠 + SelectionBrush 背景のみをトリガーし、Margin 二重変更を廃止
- **HeaderTemplate 外側マージン削除**: HeaderTemplate 外側 StackPanel の `Margin="0,16,0,14"` を `Margin="0,0,0,0"` に変更。Border の常時 Padding が外側余白を担うため、二重加算による過剰な上下余白を解消
- **説明文 TextBlock MaxWidth 設定**: `MaxWidth="300"` を明示的に設定し、常時 Padding 付き Border 内での折り返し幅を確実に固定

## [0.14.9] - 2026-03-03 : カテゴリ説明文 折り返し根本修正

### Fixed
- **カテゴリ説明文 TextWrapping 根本修正**: 説明文 TextBlock 自身に `MaxWidth="460"` を追加。従来は親 StackPanel の `MaxWidth="620"` のみだったが、日本語テキスト（FontSize=11、約11px/文字）は最長55文字でも約605px に収まり 620px 内で折り返しが発生しなかった。TextBlock 自身に 460px を指定することで Measure・Arrange 両フェーズで幅が確実にクランプされ、全カテゴリ説明文（48〜55文字）が常に2行に折り返されるよう修正

## [0.14.8] - 2026-03-03 : カテゴリ UI 修正・ランダムモード タイル挙動改善

### Changed
- **カテゴリヘッダー 幅制限**: `GroupStyle.HeaderTemplate` 外側 `StackPanel` に `MaxWidth="620"` を追加。説明文が横に伸びてレイアウトを押し広げる現象を解消
- **カテゴリ説明文 余白調整**: 説明文 TextBlock の Margin を `0,5,0,0` → `0,6,0,8` に変更し、ヘッダー行との間隔とタイル群との間隔を確保
- **カテゴリランダムモード時のタイル選択状態を抑制**: `ListBox.ItemContainerStyle` の `IsSelected` トリガーを `MultiDataTrigger`（IsSelected=True かつ IsRandomCategoryModeActive=False）に変更。「現在のカテゴリからランダム」モード中はタイルをクリックしてもカード個別の青枠は表示されず、カテゴリグループ枠のみが強調される
- **クリエイター署名のスタイル**: `FontWeight="Light"` を追加し細身のフォントに変更。`Opacity` を `0.5` → `0.45` に微調整
- **全テーマ JSON Author 更新**: `"K.AKASAKA"` → `"KAKASAKA"` に変更（20テーマ）

## [0.14.7] - 2026-03-03 : テーマギャラリー カテゴリ UI・説明文・クレジット表示の刷新

### Added
- **カテゴリコンテナ 全域クリック判定**: カテゴリランダムモード時、カードの隙間・余白領域を含めたカテゴリ全体をクリック可能に。`GroupStyle.ContainerStyle` の `ControlTemplate` に `Border.InputBindings` で `SelectRandomCategoryCommand` を登録
- **カテゴリコンテナ ホバー演出**: `MultiDataTrigger`（IsRandomCategoryModeActive + IsMouseOver）でホバー時に `ListHoverBrush` 背景 + `AccentBrush` 枠に変化。選択状態（DataTrigger 後順）が常に優先

### Changed
- **カテゴリグループ枠 余白を拡充**: モードアクティブ時の Padding を `8,0` → `16` に拡大し、枠内テーマカードとのゆとりを確保。CornerRadius も `6` → `8` に変更。Margin を `0,0,0,8` → `0,0,0,12` に拡大
- **カテゴリランダムモード時のカーソル**: `Cursor="Hand"` をモード DataTrigger 内に移動し、モード外は通常カーソルを維持
- **`StackPanel Background="Transparent"`**: カテゴリコンテナ内の StackPanel に `Background="Transparent"` を追加し、余白部分でのマウスイベントを確実に受け取るよう修正
- **カテゴリ説明文 深度向上**: `ThemeCategoryDescriptionConverter` の説明文を全カテゴリ刷新。各カテゴリの世界観・用途・雰囲気を具体的かつ文学的に表現した日本語テキストに変更
- **クリエイター署名のデザイン刷新**: テーマカードの著者表示を `{Binding Author, StringFormat='Design by {0}'}` 形式に変更。フォントサイズを `10` → `9` に縮小し、`Opacity="0.5"` + `Margin="0,3,0,0"` でミニマルかつ高品質な署名スタイルを実現
- **全テーマ JSON の Author フィールド更新**: 旧著者名 → `"K.AKASAKA"` に変更（20 テーマ）

## [0.14.6] - 2026-03-03 : テーマ適用モード 排他制御 & カテゴリ選択 UI

### Added
- **`ThemeApplyMode` 列挙体**: `Preset / Random / Pane` の3値。3カードの排他制御に使用
- **`ActiveThemeMode` プロパティ**: テーマ適用モードを保持。切り替え時に他モードの内部状態を自動リセット
- **`IsRandomModeActive` / `IsRandomCategoryModeActive` プロパティ**: 自動選択モード状態の論理プロパティ。XAML の `IsEnabled` バインドに使用
- **`SelectedRandomCategory` プロパティ**: カテゴリランダムモード時に選択中のカテゴリ名を保持
- **`SetThemeModeCommand`**: カードクリックで `ActiveThemeMode` を切り替えるコマンド（`RelayCommand<ThemeApplyMode>`）
- **`SelectRandomCategoryCommand`**: カテゴリヘッダー・テーマタイルクリックで対象カテゴリを設定するコマンド（`RelayCommand<string?>`）
- **`IsCategorySelectedConverter`**: カテゴリ名・選択カテゴリ・モード有効フラグの3値を受け取る `IMultiValueConverter`。GroupItem の DataTrigger に使用
- **GroupStyle.ContainerStyle（カテゴリグループ枠）**: カテゴリランダムモード時に全カテゴリへ薄い枠を表示。選択カテゴリはアクセントカラーの太枠 + SelectionBrush 背景でハイライト
- **カテゴリヘッダー クリック選択**: GroupStyle.HeaderTemplate に `MouseBinding` を追加。ヘッダーをクリックするとそのカテゴリが `SelectedRandomCategory` に設定される

### Changed
- **「テーマ・適用モード」3カード 排他制御化**: 各カードを `MouseBinding` でクリック選択可能にし、`Style.Triggers` でアクティブカードにアクセントカラー強調枠を表示。3カードは完全排他
- **パーソナライズ・プリセット カード簡素化**: 20スロット `ListBox` を削除し、「好きなテーマを自由にクリックして適用する標準モード」として機能を整理
- **自動選択モード カード**: 「無効」RadioButton と「将来実装」バッジを削除。カード非選択時は RadioButton を `IsEnabled=false` でグレーアウト。選択時は「全テーマからランダム」をデフォルト設定
- **ペイン個別適用 カード**: ペイン RadioButton を `IsEnabled` バインドでカード未選択時はグレーアウト
- **ヒントバー解除ボタン**: `ClearAssignmentModeCommand` → `SetThemeModeCommand(Preset)` に変更
- **`GalleryModeHint` 式**: `ActiveThemeMode` のスイッチ式に刷新。各モード・サブモードに応じたテキストを返す
- **`IsAssignmentModeActive`**: `ActiveThemeMode != Preset` の1式に簡素化
- **`OnSelectedThemeInfoChanged`**: カテゴリランダムモード時はテーマ適用せずカテゴリを選択するルーティングを追加

### Removed
- **`PresetSlotViewModel` クラス**: スロット機能廃止に伴い削除
- **`PresetSlots` プロパティ**: 同上
- **`ActivePresetSlot` プロパティ**: 同上
- **`ClearAssignmentModeCommand`**: `SetThemeModeCommand` に統合
- **`OnActivePresetSlotChanged` / `OnSelectedPaneTargetChanged` ハンドラ**: `OnActiveThemeModeChanged` に統合

## [0.14.5] - 2026-03-03 : テーマ・適用モード インタラクティブ化

### Added
- **「テーマ・適用モード」セクション名変更**: 「高度なテーマ制御」を「テーマ・適用モード」に改称し、機能の目的を明確化
- **プリセットスロット インタラクティブ化**: ハードコードされた 20 スロット表示を `PresetSlotViewModel` にバインドした `ListBox` に刷新。スロットをクリックして選択し、テーマギャラリーのタイルをクリックするとそのスロットに登録される（スロット選択中はアクセントカラーで強調表示、登録済みスロットはテーマ名を表示）
- **ペイン個別適用カードを有効化**: 無効化されていた ComboBox グリッドを `RadioButton` セレクターに置き換え。ナビ / Aペイン / Bペインを選択後にテーマタイルをクリックすると、そのペインにテーマ名が記憶される（将来の実際の適用実装への基盤）
- **ギャラリー割り当てモード ヒントバー**: スロット・ペインのどちらかが選択されているとき、テーマギャラリー上部にアクセントカラーのバナーで「スロット N を選択中 — テーマをクリックして登録」等のガイダンスを表示。「解除」ボタンでモードを即座に終了可能
- **テーマタイル ペインバッジ**: 各テーマカードの右上に [ナビ][A][B] バッジを表示するシステムを実装。`ThemePaneBadgeVisibilityConverter`（IMultiValueConverter）でペイン割り当て済みテーマのみバッジが出現
- **`PaneTarget` 列挙体**: `None / Nav / APane / BPane` の4値を追加
- **`PresetSlotViewModel` クラス**: スロット番号・テーマ名・表示テキストを保持する Observable なスロット VM
- **`IsAssignmentModeActive` / `GalleryModeHint` プロパティ**: 割り当てモードの状態とヒント文字列を UI に提供
- **`ClearAssignmentModeCommand`**: スロット選択とペインターゲット選択を一括クリアするコマンド
- **スロット / ペイン選択の排他制御**: `OnActivePresetSlotChanged` / `OnSelectedPaneTargetChanged` で双方向の排他制御を実装

## [0.14.4] - 2026-03-03 : テーマ設定ビュー 大幅レイアウト刷新

### Changed
- **「高度なテーマ制御」をページ最上部に移動**: モード選択 → テーマ選択という論理的な操作順序を実現。テーマギャラリーの上に「高度なテーマ制御」セクションを配置し、ギャラリーとの間に 24px マージンを確保
- **高度なテーマ制御カードの並び替え**: プリセット（有効）→ 自動選択（将来）→ ペイン個別（将来）の順に再配置
- **「パーソナライズ・プリセット」を有効機能として強調**: アクセントカラーのボーダー（1.5px）とアクセントアイコンで他カードと差別化。"将来実装" バッジを廃止し、1〜20 のスロットグリッド（52×28px × 20 タイル）で UI ビジョンを視覚化
- **テーマ・ツール（エクスポート）セクションを完全削除**: デバッグ用途だったカードとエクスポートボタンを除去し、設定画面をクリーンアップ

## [0.14.3] - 2026-03-03 : テーマギャラリー UI 強化・高度テーマ制御フレームワーク

### Added
- **standard テーマの先頭固定**: スタンダードカテゴリ内で "standard" テーマが常にリスト先頭に表示されるよう `StandardFirstSortKey` プロパティと追加ソートを実装
- **テーマ・ツール カード**: 「テーマをエクスポート」ボタンを説明文付きの白カードに昇格。他の設定カードと統一されたデザインで配置
- **高度なテーマ制御セクション**: テーマギャラリー下部に3カードを追加
  - **自動選択モード（Randomize）**: 起動時のテーマ選択を「無効 / 全テーマからランダム / 現在カテゴリからランダム」で設定可能な RadioButton UI
  - **パーソナライズ・プリセット**: 将来実装の 20 スロット定義を示すプレースホルダーカード
  - **ペイン個別適用**: ナビ・Aペイン・Bペインへのテーマ個別割り当て UI（ComboBox レイアウト、将来実装プレースホルダー付き）
- **`ThemeRandomizeMode` 列挙体**: `Disabled` / `AllThemes` / `CurrentCategory` の3モードを `AppSettingsViewModel` に追加

### Fixed
- **カテゴリヘッダーの下余白拡張**: 説明文追加後にカードと重なる問題を解消するため StackPanel の下マージンを 8→14px に拡張、説明文に 5px の上マージンを追加

## [0.14.2] - 2026-03-03 : テーマギャラリー説明文・ホバー演出

### Added
- **カテゴリ概要テキスト**: テーマギャラリーの各カテゴリヘッダー直下にグループのコンセプトを説明する短い文を追加（スタンダード・ウォーム & コージー・プロフェッショナル・プレミアム・レトロ & テック）
- **テーマカードに説明文を表示**: テーマ名とカラーチップの間に JSON の `Description` フィールドを表示。2行までで折り返し、超えた場合は省略記号で省略。ホバー時はツールチップで全文を確認可能
- **テーマカードのホバーリフト演出**: マウスホバー時にカードが 2px 上方向に浮き上がる TranslateTransform アニメーション（120ms）を追加
- **ThemeCategoryDescriptionConverter**: カテゴリ表示名からグループ概要テキストを返す新コンバーター

## [0.14.1] - 2026-03-03 : テーマカテゴリ分類

### Added
- **テーマカテゴリ分類**: Control Deck のテーマギャラリーを5カテゴリ（スタンダード・ウォーム & コージー・プロフェッショナル・プレミアム・レトロ & テック）でグループ化。カテゴリヘッダーにアイコン・名前・件数を表示し、各グループ内はカードが横並びの WrapPanel で配置
- **テーマ JSON に Category フィールド追加**: テーマエクスポート時に `"Category"` をメタデータとして出力。再読み込み時にカテゴリが正しく復元される
- **ThemeCategoryIconConverter**: カテゴリ表示名から PackIconLucide アイコンを解決するコンバーター

### Changed
- **テーマソート順をカテゴリ順 → 名前順に変更**: カテゴリ内でアルファベット順にソート

## [0.14.0] - 2026-03-03 : Control Deck（ダッシュボード形式の設定ビュー）

### Added
- **Control Deck（ダッシュボード形式の設定ビュー）**: サイドバー内の設定 ScrollViewer を廃止し、メインウィンドウ全域を使った2カラムのオーバーレイ「Control Deck」に刷新。左ナビで5カテゴリ（基本設定・検索・インデックス・リカバリ・テーマ）を切替え、右コンテンツ領域にカード形式で設定項目を配置
- **テーマカードにカラーチップ表示**: テーマ選択時に背景色・アクセント色・テキスト色・サイドバー色の4色を Ellipse で視覚的にプレビュー
- **Control Deck 開閉アニメーション**: 背後のペインを Scale(0.95) + 暗転しつつオーバーレイをフェードイン/アウト。カテゴリ切替時はコンテンツ領域の Opacity フェードで滑らかに遷移

### Changed
- **設定ボタン (Ctrl+Shift+O) を Control Deck に接続**: 歯車ボタンとキーバインドが新しい Control Deck を開くように変更
- **「インデックスの設定を開く」を Control Deck のインデックスカテゴリに直接遷移**: インデックス検索ビューからの設定リンクが Control Deck のインデックスカテゴリを即座に表示

## [0.13.9] - 2026-03-03 : マルチコア JIT プロファイリング＋Debug TieredCompilation による起動高速化

### Changed
- **マルチコア JIT プロファイリングを追加**: `Main()` 冒頭で `ProfileOptimization.SetProfileRoot` / `StartProfile` を呼び出し、起動時の JIT パターンをプロファイルに記録。2回目以降の起動ではバックグラウンドスレッドで先行 JIT コンパイルを実行し、コールド起動を 20-40% 短縮
- **Debug ビルドに TieredCompilation を有効化**: `TieredCompilation` + `TieredCompilationQuickJit` を Debug 構成にも追加し、開発時の JIT 遅延を軽減
- **`.gitignore` に `*.jitprofile` を追加**: JIT プロファイルファイルをバージョン管理から除外

## [0.13.8] - 2026-03-03 : 起動速度の改善（設定並列読み込み＋お気に入りアイコン非同期化）

### Changed
- **カスタム Main() による起動高速化**: App.xaml の BuildAction を Page に変更して自動生成 Main を抑止。カスタム `Main()` で WPF フレームワーク初期化前にスプラッシュ表示と設定並列読み込みを開始し、`Application` コンストラクタ + `InitializeComponent` と並列実行
- **起動診断ログのバックグラウンド化**: `FileLogger.LogStartupDiagnostics()` を `Task.Run` でバックグラウンド実行に変更し、UI スレッドの I/O を排除
- **お気に入りアイコンの非同期バッチ読み込み**: `FromDto()` 内の同期アイコン取得を除去し、`LoadFromSettings()` 完了後に `LoadIconsAsync()` で全アイコンをバックグラウンド一括取得。アイテム数に比例した UI ブロックを解消
- **ウェルカムウィンドウ最終ページのレイアウト改善**: 「アプリを起動」ボタンをフッター右下（「次へ」と同じ位置）に移動し、操作の一貫性を向上。中央エリアには歓迎メッセージを表示

## [0.13.7] - 2026-03-03 : ワーキングセット切り替えの堅牢化＋同期スワップ演出

### Changed
- **ワーキングセット切り替えをコードビハインド `BeginAnimation` + `TaskCompletionSource` 方式に刷新**: XAML DataTrigger/Storyboard 方式を全廃し、`MainWindow.xaml.cs` から `PaneContentArea.BeginAnimation` で直接 Opacity を制御。`DoubleAnimation.Completed` + `Dispatcher.InvokeAsync(Render)` で Opacity=0 の描画確定まで待ち、ViewModel 側で `await` することでフェードアウト描画完了→スワップ→フェードインの順序を**構造的に保証**
- **`Func<Task>` デリゲート方式で View-ViewModel 間のアニメーション連携**: `MainViewModel.AnimatePaneFadeOut` / `AnimatePaneFadeIn` デリゲートを `MainWindow` コンストラクタで設定。`ProjectSetsViewModel` は `await _main.AnimatePaneFadeOut()` でフェードアウト完了を待ってからスワップを開始し、スワップ完了後に `await _main.AnimatePaneFadeIn()` でフェードインを待つ
- **プレビュー間切り替え時のフェードなしロールバックを廃止**: `CancelPreviewInternalAsync(animate: false)` による中間ロールバックが A/B ペインを丸見えのまま差し替えていた根本原因を修正。プレビュー間切り替えでは元のロールバックポイントを保持したまま現在の表示をそのままフェードアウトし、新セットに直接差し替える方式に変更
- **`IsWorkingSetSwitching` プロパティを廃止**: DataTrigger 不要のため `MainViewModel` から除去。`Func<Task>` デリゲートが代替
- **ワーキングセット切り替えに多重発火ガードを追加**: `_isSwitching` フラグにより素早いダブルクリック等での重複実行を防止
- **ワーキングセット切り替え時の全タブパスを事前バリデーション**: `ValidateAndResolvePathsAsync` で全タブの存在確認を実施し、アクセス不能なネットワークパス等をデスクトップ/ダウンロードに自動置換。置換があった場合はトースト通知で詳細を表示
- **ワーキングセット切り替え失敗時の自動ロールバック**: 例外発生時にフェードを即解除し、元の状態への復帰を試行。復帰も失敗した場合は状態リセットのみ実行しトースト通知でエラーを報告
- **キャンセル（戻す）操作にも同じクロスフェード演出を適用**: `CancelPreviewInternalAsync` に `animate` パラメータを追加し、独立キャンセル時はフェード付き、StartPreview 内からの連鎖キャンセル時はフェードなしで効率化
- **不要プロパティ `WorkingSetSwitchMessage` を削除**: オーバーレイ廃止に伴い `MainViewModel` から除去

### Fixed
- **タブ復元時に先頭タブ以外のパスが検証されない問題を修正**: `ResolvePathForTabRestoreAsync` の `isFirstTab` ガードを除去し、全タブで `DirectoryExistsSafeAsync` による存在確認を実施。PC / UNC ルートは仮想パスとしてスキップ

## [0.13.6] - 2026-03-03 : 7-zip Shell コンテキストメニュー圧縮・展開の実行修正

### Fixed
- **7-zip 等の Shell 拡張が生成するダイアログが画面外に表示され操作不能になる問題を修正**: STA スレッドの隠しウィンドウ（0x0 サイズ、位置 (0,0)）が Shell 拡張のダイアログ親として不適切だった。`HwndSource` を右クリック位置に 1x1 の `WS_POPUP` ウィンドウとして作成し、メインウィンドウをオーナーに設定することでダイアログがメインウィンドウ付近に正しく表示されるよう修正
- **`CMINVOKECOMMANDINFOEX` に `CMIC_MASK_PTINVOKE` / `CMIC_MASK_NOASYNC` フラグを追加**: `ptInvoke` フィールドを Shell 拡張が活用可能にし、同期実行を要求することで STA スレッドの早期終了を防止
- **メッセージポンプの初期静止閾値を 300ms → 2 秒に引き上げ**: 7-zip が `InvokeCommand` 後に非同期でダイアログを生成する場合、300ms では短すぎてメッセージポンプが早期終了していた
- **空フォルダのサイズ列が空白だった問題を修正**: `GetFolderSizesFromIndexAsync` が中身 0 件のフォルダを結果辞書に含めなかったため `FolderIndexedSize` が `null` のままだった。TotalHits=0 の場合も `result[folderPath] = 0` を設定し、`totalSize > 0` ガードも除去。`DisplaySize` の条件を `HasValue` に変更し「0.0 B」と表示するよう修正

## [0.13.5] - 2026-03-02 : 選択ハイライトのアクティブ/非アクティブ切替・フォーカス枠統一

### Fixed
- **ホバーアニメーションが共有 SelectionBrush を破壊する致命的バグを修正**: ホバー終了時の `ColorAnimation` が `(Border.Background).(SolidColorBrush.Color)` を経由して現在の Background ブラシの Color を Transparent に書き換えていた。選択中は Background が共有リソース `SelectionBrush`（非 Frozen）に差し替わるため、共有リソースの Color が破壊されアプリ全体の選択ハイライトが消失していた。インライン SolidColorBrush に `x:Name` を付与し、アニメーションが名前指定でインラインブラシのみを対象とするよう変更
- **非アクティブペインの選択色を区別**: ListView（詳細ビュー）・ListBox（アイコンビュー）の選択ハイライトを、アクティブペインでは青（#D0E6F8）、非アクティブペインではグレー（#E5E5E5）に切替え。プロパティトリガー（`IsSelected`）をフォールバックとして保持し、非アクティブ時のみ MultiDataTrigger でグレーに上書き
- **ListView の FocusVisualStyle を統一**: 詳細ビューにシステムデフォルトの点線矩形が表示されていた問題を修正。角丸なしの `DetailsFocusVisualStyle` を新設し、アイコンビューの `SubtleFocusVisualStyle` と統一
- **仮想化スクロール時の描画ズレ防止**: ListView に `VirtualizingPanel.ScrollUnit="Pixel"` を追加し、大量ファイルのスクロール中に選択ハイライトがズレる問題を防止

## [0.13.4] - 2026-03-02 : Shell コンテキストメニュー「新規作成」「圧縮・展開」修正

### Fixed
- **Shell コンテキストメニュー「新規作成」が動作しない問題を修正**: `CMINVOKECOMMANDINFOEX` に `lpDirectory`/`lpDirectoryW`（作業ディレクトリ）が未設定だったため、shell 拡張の NewMenu ハンドラが作成先ディレクトリを認識できなかった。背景メニューではフォルダパス、ファイル選択時は親ディレクトリを設定するよう修正
- **Shell コンテキストメニュー「圧縮」「展開」の進捗ダイアログが表示されない問題を修正**: STA スレッドが `InvokeCommand` 後すぐ終了していたため、圧縮/展開の進捗ダイアログが機能しなかった。`DispatcherFrame` + `DispatcherTimer` によるメッセージポンプを追加し、shell 拡張のダイアログウィンドウが閉じるまで STA スレッドを維持するよう修正
- **Shell ダイアログの z-order を改善**: `GetViewObject`/`GetChildrenUIObjects` のオーナーウィンドウとして STA ダミーウィンドウではなくメインウィンドウのハンドルを使用するよう変更
- **Shell コンテキストメニュー操作後のフォルダ自動リフレッシュ**: 背景メニュー操作（新規作成等）や長時間 verb（圧縮/展開）完了後に `LoadDirectoryAsync` を呼び出してフォルダ内容を自動再読み込み
- **Shell コンテキストメニュー操作後にファイル一覧が即時更新されない問題を修正**: STA スレッドでのメニュー表示中にアプリが `Deactivated` 状態となり、FileSystemWatcher の変更通知が「非アクティブ中は後で更新」として保留されていた。`IsExpectingShellChange` が `true` のとき `IsActive` / `isVisibleTab` チェックをバイパスし、即時リフレッシュするよう修正
- **Shell 新規作成の応答速度を改善**: メッセージポンプを2段階方式に変更（ダイアログなし操作は 300ms で早期終了、ダイアログあり操作は従来通り 1.5 秒待機）。FSW スロットルを `IsExpectingShellChange` 時は 50ms に短縮（通常 500ms）
- **Shell 新規作成アイテムへの自動フォーカス**: 新規フォルダだけでなく、新規テキストファイル等すべての新規作成アイテムにフォーカスが当たるよう拡張。フォルダの場合は従来通り自動リネームモードも起動
- **forceRefresh フラグの読み取り順序バグを修正**: `IsExpectingShellChange` が Created ハンドラで `false` にリセットされた後に `forceRefresh` を読んでいたため、常に `false` になり即時リフレッシュが機能していなかった。フラグ取得をリセット前に移動
- **MVVMTK0034 警告を解消**: `SearchFilterViewModel._dateFilter` backing field の直接参照 3 箇所を生成プロパティ `DateFilter` に変更。`_isLoading` ガードで再帰呼出し・中間通知を抑制
- **7-zip 等サードパーティ Shell 拡張のコンテキストメニューが実行できない問題を修正（3点）**:
  - `GetCommandString`（Unicode 版）に `catch` がなく、`E_NOTIMPL` を返す Shell 拡張で `COMException` がスローされ `InvokeCommand` に到達しなかった。例外を捕捉し空 verb として続行するよう修正
  - `GetViewObject`/`GetChildrenUIObjects`/`InvokeCommand` に渡す `hwnd` を UI スレッドの `mainHwnd` から STA スレッドの `hwnd` に変更。7-zip 等の Shell 拡張は `InvokeCommand` の呼出しスレッド上でモーダルダイアログを生成するため、cross-thread の `hwnd` ではダイアログ生成に失敗していた
  - `GetMenuItemText` をサブメニュー再帰検索に対応。7-zip 等はサブメニュー内に項目を配置するため、ルートメニューのみの検索では `menuText` が取得できなかった。verb が空の場合は Shell 拡張と判断し常にメッセージポンプを実行するよう変更

## [0.13.3] - 2026-03-02 : インデックス走査の高速化

### Changed
- **インデックス走査をスタックベース手動再帰に変更**: `RecurseSubdirectories = true` による一括列挙から、スタックベースの手動再帰走査に切り替え。除外対象フォルダ（`node_modules`, `.git`, `bin`, `obj` 等）の子孫ファイルを一切走査しなくなり、I/O 負荷を大幅削減
- **除外フォルダリストを拡充**: `node_modules`, `.git`, `.vs`, `obj`, `bin`, `.svn`, `.hg`, `__pycache__`, `bower_components`, `.gradle`, `.next`, `.nuxt`, `venv`, `.venv`, `.nuget`, `INetCache` 等の中間生成・キャッシュフォルダを除外対象に追加。HashSet による O(1) 判定に変更
- **進捗報告を時間ベース（100ms 間隔）に変更**: 従来の件数ベース（100件ごと）から 100ms 間隔のタイマー方式に変更し、UI スレッドへの描画負荷を削減しつつカウンターの更新頻度を適正化
- **ローカルドライブの Commit バッチサイズを拡大**: ローカルドライブの Lucene Commit 間隔を 100件→500件に拡大し、Commit オーバーヘッドを削減（Box/ネットワークは従来通り）
- **スレッド優先度を BelowNormal に設定**: インデックス走査スレッドの優先度を `BelowNormal` に下げ、ファイル操作など UI 操作のレスポンスを優先
- **除外判定の配列を static readonly に変更**: `IsExcludedPath`, `IsExcludedFileName`, `IsExcludedFolderName` の判定配列をメソッドローカルから static readonly フィールドに移動し、毎回のヒープ割り当てを排除

## [0.13.2] - 2026-03-02 : ウェルカム・ガイド・ウィンドウ

### Added
- **ウェルカム・ガイド・ウィンドウ**: 初回起動時（`settings.json` 未作成）にウェルカムウィンドウを表示。アプリの基本コンセプト・クラウド連携の説明・マニュアルリンクを提供し、6種のおすすめテーマからスタイルを選んで始められる体験を追加

### Changed
- **ウェルカムウィンドウをウィザード形式に拡張**: 1ページ構成だったウェルカムウィンドウを全9ページのウィザード形式に刷新。ようこそ（BOX Drive/OneDrive 連携アナウンス）→ ナビペイン各ビュー紹介6ページ（お気に入り・ツリー・履歴・インデックス検索・ワーキングセット・設定）→ テーマ選択 → 準備完了の順に「次へ/戻る」で遷移し、最後に「アプリを起動」で開始
- **テーマ選択を全テーマ対応に変更**: ウィザードのテーマ選択ページで、おすすめ6種のみだった選択肢を全テーマ（20種）に拡大。スクロール対応のカードレイアウトで一覧表示
- **ウィザード Page 1〜6 にスクリーンショット画像枠を追加**: ナビペイン各ビュー紹介ページ（お気に入り・ツリー・履歴・インデックス検索・ワーキングセット・設定）に角丸 Border + Image を挿入。`assets/welcome/` に PNG を配置すると各ページに実際の画面イメージが表示される。画像未配置時はビルドエラーなく空白表示。ウィンドウ高さを 560→680 に拡大

### Fixed
- **お気に入り右クリック時のクラッシュを修正**: MenuItem サブメニューの Popup テンプレート内で `StaticResource ShadowColor` を参照していたが、Popup は独立したビジュアルツリーのため StaticResource 解決が失敗していた。App.xaml の ContextMenu テンプレートと同様にハードコード値に変更

## [0.13.1] - 2026-03-01 : テーマメタデータ表示

### Added
- **テーマメタデータ表示**: テーマ選択 UI を3段表示に刷新。太字のテーマ名・灰色の説明・著者を各行に表示し、テーマの内容が一目でわかるように改善。ナビペイン幅を超えるテキストは `...` で省略。著者未設定時は著者行を非表示
- **ThemeInfo DTO**: テーマ名・説明・著者情報を保持する `ThemeInfo` クラスを新規作成。`DisplaySubText` で説明と著者を結合表示
- **テーマ JSON メタデータ**: テーマ JSON に `Description` / `Author` フィールドを追加。エクスポート時にも自動出力。メタデータのない古いテーマは「説明はありません」をフォールバック表示
- **テーマ選択カード型 UI**: 各テーマをカード型（角丸・枠線・ホバー・選択時アクセント枠）で表示。説明文は折り返し表示に対応し、スクロール不要の全件展開レイアウト
- **MANUAL.md にテーマセクション追加**: テーマの選び方・エクスポート・カスタマイズ手順と、初期テーマ全20種の一覧表を記載

## [0.13.0] - 2026-03-01 : ライブ・テーマ・エンジン

### Added
- **ライブ・テーマ切替**: テーマ選択時に新しい `SolidColorBrush` インスタンスをトップレベルリソースに設定し、`DynamicResource` 参照が自動解決することで再起動なしに UI 全体の配色を即座に反映。メイン画面からダイアログまで全 49 Color キー + 対応する 47 Brush を同期更新
- **テーマ自動検出**: `themes/` フォルダ内の全 `.json` ファイルをスキャンし、設定画面のリストに自動表示。テーマの追加はファイルを置くだけで完了
- **テーマ選択 UI**: 設定画面（Ctrl+Shift+O）→「5. テーマ」にテーマ選択リストを配置。上下に選ぶだけで配色が即座に切り替わる「試着」体験を提供
- **テーマ名の永続化**: 選択中のテーマ名を `settings.json` の `ThemeName` に保存し、次回起動時に自動ロード
- **起動時テーマ高速読み込み**: `settings.json` から `ThemeName` のみを `JsonDocument` で軽量パースし、フル設定 Load を待たずにテーマを適用

### Changed
- **ThemeService を全面刷新**: テーマ Discovery / Live Hot-Swap / Color→Brush マッピングを追加。`LoadAndApply` にテーマ名パラメータを追加し、複数テーマに対応
- **設定画面テーマセクション拡充**: 単一エクスポートボタンからテーマ選択リスト＋エクスポートボタンのレイアウトに変更

### Fixed
- **テーマ切替が反映されない不具合を修正**: 全 19 XAML ファイル（466箇所）の Brush 参照を `StaticResource` から `DynamicResource` に移行。`StaticResource` はロード時に一度だけ解決されるため、実行時のリソース変更が UI に反映されなかった。`DynamicResource` に変更することで、`ThemeService` が新しい `SolidColorBrush` インスタンスをトップレベルリソースに設定するだけで自動的に全 UI が更新されるようになった

## [0.12.43] - 2026-03-01 : テーマシステム（外部 JSON 管理）

### Added
- **テーマシステム**: 配色情報を `themes/standard.json` で外部管理できる仕組みを導入。起動時に JSON を読み込み、Color リソースを上書き適用。ファイルが存在しない場合や不正な場合はデフォルト配色にフォールバック
- **テーマエクスポート機能**: 設定画面（Ctrl+Shift+O）→「5. テーマ」から現在の配色を `themes/standard.json` にエクスポート可能。テキストエディタで自由にカスタマイズし、次回起動時に自動反映
- **ThemeModel / ThemeService**: 全 49 Color キーをカテゴリ別（Base/List/Search/Accent/ContextMenu/Misc）に管理する DTO とサービスクラスを新規作成
- **コメント付き JSONC 出力**: エクスポートされるテーマファイルに各カラーキーの用途を `//` コメントで付与。読み込み時は `JsonCommentHandling.Skip` でコメントを自動スキップ

## [0.12.42] - 2026-03-01 : テーマ配色リソース一元化

### Changed
- **28箇所のハードコードカラーをリソース化**: AppResources.xaml に Color + SolidColorBrush を新規定義し、全 XAML ファイルのインラインカラー値を `StaticResource` 参照に置換。テーマ切替機能の実装基盤を整備
- **新規リソースキー追加**: ホバー・選択色（ListHover, Selection, ButtonHover, InactiveSelection, OptionSelected）、エラー・破壊的アクション色（ErrorText, DestructiveIcon, DestructiveHoverBackground）、コンテキストメニュー色 8 項目、テレメトリーパネル色 4 項目、GlowBar・ローディング色、チェックボックス派生色を一括定義
- **ダイアログ背景・テキスト色のリソース化**: 全ダイアログ（RenameDialog, ZenithDialog, InputBox 等 8 ファイル）の `Background="#F5F1E3"` / `Foreground="#1A1A1A"` を BackgroundBrush / TextBrush 参照に統一
- **ContextMenuItemStyle の色リソース化**: AppResources.xaml 内のメニューテンプレートにおけるハイライト色・無効色・ショートカット色を全て StaticResource 参照に移行
- **App.xaml の Popup 制約を文書化**: ContextMenu テンプレート内のハードコード色に対応するリソースキー名をコメントで明記

## [0.12.41] - 2026-03-01 : 検索履歴ドロップダウンのプロ仕様化

### Added
- **プリセット一致の動的判定**: 検索実行時、現在のフィルタ条件（サイズ・日付）を保存済みプリセットの定義とバイト/日付レベルで自動比較し、一致するプリセット名を履歴に記録。手動設定でもプリセットと同値なら名前が表示される
- **検索履歴に条件カラムを表示**: 検索履歴ドロップダウンを「プリセット: [名前]」「サイズ: [略称]」「日付: [略称]」の明文ラベル+値カラムで表示。SharedSizeGroup による縦整列で過去の条件を素早くスキャン可能に
- **検索履歴からのフィルタ完全復旧**: 履歴項目を選択すると、キーワード・検索モードに加え、サイズフィルタ・日付フィルタも検索時の状態に自動復旧。アイコンのアンダーラインも連動して正しい状態に同期

### Changed
- **★アイコンを廃止しラベル形式に変更**: 左端の「★」インジケーターを撤去し、右端カラムに「プリセット: [名前]」と明文表示。ラベル部(#AAAAAA)と値(#888888)のコントラストで計器ツールの品格を確立
- **検索履歴 DB スキーマ V2 マイグレーション**: SearchHistoryRecord テーブルに PresetName, MinSizeText, MaxSizeText, StartDateText, EndDateText 列を追加。既存データはデフォルト空文字で正常動作

## [0.12.39] - 2026-03-01 : 検索条件の多層的リセット機能

### Added
- **プリセット／サイズ／日付アイコンの右クリックリセット**: ★（プリセット）を右クリックでキーワード・サイズ・日付・スコープのすべてを一括クリア。天秤（サイズ）・カレンダー（日付）の各アイコンを右クリックすると該当フィルタのみを即座にクリア。アンダーラインも連動して消去
- **Esc キー多段リセット**: 検索テキストボックスで Esc を押すと、1回目はテキストのみ消去、2回目（テキストが空の場合）はサイズ・日付・スコープのフィルタもすべて消去して検索を解除
- **フィルタ操作時のステータスバー通知・ログ**: サイズチップ適用・日付プリセット適用・フィルタクリア・プリセット保存/適用/削除/全リセットの全操作にステータスバー通知と FileLogger 記録を追加
- **ツールチップに右クリック操作の説明を追記**: サイズ・日付・プリセットの各アイコンのツールチップに右クリックでのクリア操作を2行目に記載

### Changed
- **ResetAllFilters の通知一括化**: `_isLoading` フラグで中間通知を抑制し、`NotifyAllFilterProperties` で全プロパティの変更を一括通知。Storyboard アニメーションによるアンダーラインの残留を解消

## [0.12.37] - 2026-03-01 : 設定保存のデバウンス・アトミック・スワップ方式刷新

### Changed
- **デバウンス保存**: 設定変更ごとに即座にファイルI/Oを行わず、800ms のデバウンス待機後にバックグラウンドスレッドで一括書き出し。連続操作時のディスクI/Oと UI ブロックを大幅に削減
- **アトミック・スワップ書き込み**: `.tmp` に書き出し → `File.Replace` で本体と一括置換する方式に変更。書き込み中のクラッシュでも `settings.json` が破損しない
- **自動バックアップ (.bak)**: `File.Replace` が旧 `settings.json` を `.bak` として自動保持。起動時に本体が破損（0バイト等）していた場合は `.bak` から自動復旧
- **バックグラウンドI/O**: 部分保存（お気に入り・フィルタ・プリセット等）のファイルI/Oをすべて `Task.Run` 経由で実行し UI スレッドを一切ブロックしない
- **シャットダウン時フラッシュ**: ウィンドウ終了時にデバウンス待機中の未保存変更を即座にフラッシュしてから最終設定を保存

## [0.12.36] - 2026-03-01 : Quick Preview ナビゲーション（←/→ キー）

### Added
- **Quick Preview ナビゲーション**: プレビュー表示中に ← / → キーで前後のファイルに切り替え可能に。閉じる→選び直す→開くの手順を1キーに集約。ディレクトリは自動スキップ、リスト境界では停止
- **ポジションカウンター**: プレビューヘッダーにファイル位置（例: 3 / 15）を表示
- **スライド＋フェードアニメーション**: ナビゲーション時にコンテンツが移動方向からスライドイン（マイクロアニメーション設定に連動）
- **プレビュー閉じるボタン**: ヘッダー右上に × ボタンを追加。マウス操作でもプレビューを閉じられるように

### Changed
- **ShowQuickPreview リファクタリング**: コンテンツ更新ロジックを UpdateQuickPreviewContent に分離し、初回表示とナビゲーションで共通化

## [0.12.35] - 2026-03-01 : Quick Preview — 画像/Excel/CSV/HTML/PDF 対応

### Added
- **Quick Preview で画像表示に対応**: .jpg/.png/.bmp/.gif/.tif/.ico/.webp ファイルを WPF BitmapImage でデコードし表示。DecodePixelWidth=1920 でメモリ使用量を制限、バックグラウンド読み込みで UI ブロックなし
- **Quick Preview でスプレッドシート表示に対応**: Excel (.xlsx/.xls/.xlsm) および CSV/TSV ファイルを DataGrid でネイティブ表示。ExcelDataReader による軽量読み取り（先頭 500 行 / 50 列）、シート名・行数・シート数をフッターに表示
- **Quick Preview で HTML レンダリングに対応**: HTML (.html/.htm) ファイルを WebView2 でレンダリング表示（従来のテキスト表示から変更）

### Changed
- **PDF プレビューを PDFtoImage 画像レンダリング方式に変更**: WebView2 依存を廃止し、PDFtoImage (PDFium) でページを 144 DPI の画像としてレンダリング。ScrollViewer 内で最大 20 ページを縦スクロール表示。ツールバーなしのクリーンな表示を実現
- **Office プレビューも PDFtoImage 経由に変更**: Office→PDF 変換後の表示を WebView2 から画像レンダリングに統一。WebView2 は HTML プレビュー専用に
- **Excel プレビューを DataGrid 方式に変更**: Excel ファイルのプレビューを COM 経由の PDF 変換から ExcelDataReader によるネイティブ DataGrid 表示に切り替え。Office 未インストール環境でもプレビュー可能に

## [0.12.34] - 2026-03-01 : Quick Preview — PDF / Office 対応

### Added
- **Quick Preview で PDF 表示に対応**: Space キーで PDF ファイルを WebView2 のネイティブビューアで表示
- **Quick Preview で Office 文書に対応**: Word (.docx/.doc)、Excel (.xlsx/.xls)、PowerPoint (.pptx/.ppt) を一時 PDF に変換して表示（Microsoft Office が必要）

## [0.12.33] - 2026-03-01 : Quick Preview (Peek) 機能

### Added
- **Quick Preview**: ファイルリストで Space キーを押すとファイルの中身をオーバーレイ表示。テキスト系ファイル（.txt, .cs, .json, .md, .xml 等 40種以上）の先頭 100KB を等幅フォントで表示。再度 Space または Esc で閉じる

## [0.12.32] - 2026-03-01 : 検索フィルタ機能のマニュアル詳細化

### Added
- **マニュアル追記**: サイズフィルタ（3-3-3）・日付フィルタ（3-3-4）・検索プリセット（3-3-5）の詳細セクションを MANUAL.md に追加。入力形式・クイックチップ・カレンダー連動・プリセットの保存/適用/プレビュー/削除/リセットの操作手順を網羅

## [0.12.31] - 2026-03-01 : ステータスバー通知の色統一・重複表示修正

### Fixed
- **通知メッセージ色の統一**: ファイル操作ステータスの文字色を `#4FC3F7`（シアン）から `#90A4AE`（グレー）に変更し、ステータスバー中央エリアの色調を統一
- **通知メッセージの重複表示防止**: 通知が表示中に新しい通知が届いた場合、既存メッセージのフェードアウト完了を待機してから最新メッセージを表示する queue-of-one 方式に変更

## [0.12.30] - 2026-03-01 : プリセットポップアップに全設定リセットボタン追加

### Added
- **検索条件リセットボタン**: プリセットポップアップ下部にリセットボタンを追加。キーワード・サイズ・日付・ソート・ファイルタイプフィルタを一括でデフォルト状態に戻せるように。検索モードとスコープは維持される

## [0.12.29] - 2026-03-01 : ポップアップ外部クリック貫通防止

### Fixed
- **ポップアップ外クリック貫通防止**: プリセット・サイズ・日付・スコープの各フィルタポップアップを外部クリックで閉じる際、`e.Handled = true` を設定しイベント伝播を遮断。ポップアップ外を誤クリックした際にファイルリストが反応する問題を修正

## [0.12.28] - 2026-03-01 : プリセット保存確認ダイアログ

### Changed
- **プリセット保存時の確認ポップアップ**: 検索プリセットの新規保存時、設定一覧（モード・キーワード・サイズ・日付・拡張子・スコープ・ソート）を表示する確認ダイアログを挟むように変更。意図しない保存を防止
- **ZenithDialog にカスタムコンテンツ領域追加**: ZenithDialog.Show にオプショナルな FrameworkElement パラメータを追加し、メッセージとボタンの間に任意の UI 要素を表示可能に

## [0.12.27] - 2026-03-01 : 検索プリセット永続化修正

### Fixed
- **プリセット消失バグ修正**: アプリ終了時の設定保存処理で SearchPresets が新規空リストで上書きされ、再起動後にプリセットが消失する致命的バグを修正。MainWindow_Closing の WindowSettings 構築に SearchPresets を明示的に含めるよう対応
- **診断ログ追加**: プリセットの保存・読み込み時にログを出力し、永続化の信頼性を検証可能に

## [0.12.26] - 2026-02-28 : 検索プリセット プレビュー + アイコン同期

### Added
- **プリセット内容プレビュー**: プリセットリストの各項目にホバーすると、キーワード・サイズ・日付・フィルタ・スコープ・ソートの設定内容をサブポップアップで事前確認可能に。Zenith スタイルの高密度レイアウトで、値のない行は自動非表示
- **サイズ値の自動変換表示**: プリセット内のサイズ指定（例: `1g`）をプレビュー内で人間可読形式（例: `1 GB`）に自動変換表示
- **日付値の自動変換表示**: プリセット内の日付指定（例: `20260228`）をプレビュー内で `yyyy/MM/dd` 形式に自動変換表示

### Changed
- **アイコン状態の即時同期**: プリセット適用後、サイズ・日付フィルタの変更通知を一括発火し、検索バーのアイコンアンダーライン（天秤・カレンダー）が即座に正しい状態を反映するように改善
- **削除ボタン UI 改善**: × ボタンをホバー時のみ表示する方式を維持しつつ、18×18 の明確なヒットエリアとホバー背景（半透明白）を追加。プレビュー操作との干渉を解消

## [0.12.25] - 2026-02-28 : 検索プリセット機能

### Added
- **検索プリセット保存・復元**: 検索条件（キーワード・サイズ・日付・拡張子フィルタ・ソート・スコープ）を名前付きプリセットとして保存し、ワンクリックで復元・再検索する機能を追加。検索バーの★アイコンからポップアップで管理
- **モード別フィルタリング**: 通常検索とインデックス検索のプリセットを自動分類し、現在のモードに合致するプリセットのみを表示
- **プリセット永続化**: settings.json に保存され、アプリ再起動後も復元可能
- **ポップアップ UI**: サイズ・日付フィルタと同一デザインパターン（スライドインアニメーション、ステルススクロールバー、ホバーハイライト、相互排他）を採用

## [0.12.24] - 2026-02-28 : 日付指定ポップアップ 業務特化プリセット拡充 + 双方向同期

### Changed
- **業務特化型プリセット 9 種**: 従来の 3 チップを廃止し、短期（今日 / 昨日 / 直近 3 日）・中期（直近 7 日 / 直近 14 日 / 直近 30 日）・期間（今月 / 先月 / 今年）の 9 プリセットを 3 行配置。ワンクリックで開始・終了に即反映
- **テキスト → カレンダー双方向同期**: TextBox 入力をリアルタイムに解析し、有効な日付であれば Calendar の SelectedDates と DisplayDate を自動更新。カレンダー → テキスト方向の同期と合わせて完全な双方向連動を実現
- **Calendar Zenith Style 化**: 選択色をデフォルトの青から漆黒 (#1A1A1A) + 白文字に変更。本日セルは淡色 (#E8E6E0)、非当月日は黒の Opacity=0.3 で表示。全テキストを #000000 に統一
- **再帰ガード**: Calendar ↔ TextBox の同期ループを `_isSyncingCalendar` フラグで防止し、操作の淀みを解消

## [0.12.23] - 2026-02-28 : 日付指定ポップアップ刷新

### Changed
- **日付フィルタ テキスト入力方式化**: DatePicker + RadioButton プリセットを廃止し、サイズフィルタと同一の TextBox + リアルタイムプレビュー方式に刷新。スマート日付パーサー（yyyyMMdd / MMdd / yyyy/M/d 等）で柔軟な入力を受け付け、変換結果をリアルタイム表示
- **Calendar 常時表示**: Popup 下部に WPF Calendar (SingleRange) を常時表示。TextBox とカレンダーが双方向に連動し、カレンダークリックで TextBox に yyyyMMdd が反映、範囲ドラッグで開始・終了が同時に設定
- **クイックチップ化**: RadioButton プリセットをクリッカブルチップ（今日 / 直近 7 日 / 直近 30 日）に変更。クリックで開始・終了 TextBox に自動入力
- **整合性チェック**: 開始日が終了日を超えている場合に赤テキスト警告を表示しフィルタを無効化
- **オートフォーカス + キーボード操作**: Popup 展開時に開始 TextBox に自動フォーカス、Tab で終了へ遷移、Enter/Escape で閉じて検索バーに復帰
- **フリーズ修正**: `Dispatcher.Invoke` → `Dispatcher.InvokeAsync` に変更し、フィルタ変更時の UI フリーズを解消

### Fixed
- 日付フィルタ変更時に `Dispatcher.Invoke` の同期呼び出しが UI スレッドをブロックしうる問題を修正

## [0.12.22] - 2026-02-28 : 全ポップアップ デザイン統一

### Changed
- **スコープ Popup #000000 化**: ヘッダー「検索スコープ」、「すべて選択 / すべて解除」ボタン、区切り「|」のテキストを #000000 に統一。ボタンは Opacity 0.55→hover 1 + アンダーラインの 2 段階フィードバック。フォルダ名を #000000 に変更
- **日付ボタン アンダーライン化**: 青ドット (Ellipse) を廃止し、サイズボタンと同一の 1px charcoal gray (#333333) アンダーライン + #1A1A1A アイコン色 + 0.1s フェードに統一
- **日付 Popup チップ化**: RadioButton リストを廃止し、チップ型 RadioButton（白背景 + 黒枠 1px + CornerRadius=2）に刷新。選択中は漆黒 (#1A1A1A) 反転で現在のプリセットを明示。ホバー時も同一の反転表現。「指定なし」はヘッダーのリセットボタンに統合
- **日付 Popup #000000 化**: ヘッダー「日付指定」、リセットボタン、カスタム入力ラベル（開始/終了）を #000000 に統一

## [0.12.21] - 2026-02-28 : サイズ指定ポップアップ 視認性最終ブラッシュアップ

### Changed
- **全テキスト #000000 統一**: ヘッダー、リセットボタン、ガイドテキスト、Min/Max ラベル、TextBox 入力文字、プレビューの Foreground をすべて純粋な黒 (#000000) に統一。淡色背景に対する最高コントラストを確保
- **チップボタン再設計**: 背景を白 (#FFFFFF)、境界線を黒 (#000000, 1px)、CornerRadius=2 の精密なボタン形状に変更。ホバー時は漆黒 (#1A1A1A) 背景 + 白文字に反転し、クリック可能な道具として直感的に認識可能に
- **セパレータ微調整**: 区切り線を #000000 Opacity=0.1 に変更し、黒ベースのカラーパレットと統一
- **リセットボタン洗練**: 通常時 Opacity=0.55、ホバーで Opacity=1 + アンダーラインの 2 段階フィードバック

## [0.12.20] - 2026-02-28 : サイズフィルタ スマート入力リデザイン

### Changed
- **サイズフィルタ マニュアル・ファースト化**: ラジオボタン/プリセット方式を廃止し、Min/Max テキスト入力 + サフィックスパーサー（k=KB, m=MB, g=GB、未指定時は MB）に刷新。「500k[Tab]2g[Enter]」のようなキーボード主体の高速操作が可能に
- **リアルタイムプレビュー**: 入力中に「→ 2.5 GB」のように変換結果をリアルタイム表示。不正入力時は赤枠で視覚的にフィードバック
- **クイックチップ**: プリセット選択の代わりにクリッカブルチップ（< 100 KB / 100 KB ~ 10 MB / 10 MB ~ 1 GB / > 1 GB）を配置。クリックで Min/Max に自動入力
- **アクティブ・インジケーター改善**: 青ドット（Ellipse）を廃止し、1px charcoal gray (#333333) アンダーラインに変更。アクティブ時のアイコン色を #1A1A1A に統一、0.1s フェードアニメーション
- **ポップアップ自動フォーカス**: サイズ Popup 展開時に Min テキストボックスへ自動フォーカス + 全選択。Enter/Escape キーで即座にクローズ
- **Min/Max 整合性チェック**: Min が Max を超えている場合、赤テキストで警告を表示しフィルタを無効化（結果が 0 件になることを防止）

## [0.12.19] - 2026-02-28 : 検索バー サイズ・日付フィルタ

### Added
- **サイズフィルタ**: 検索バーにサイズフィルタボタンを追加。プリセット（Small/Medium/Large/Huge）およびカスタム範囲（MB 単位）でファイルサイズを絞り込み可能。フィルタ適用時はアクセントカラー + ドットインジケーターで視覚的にフィードバック
- **日付フィルタ**: 検索バーに日付フィルタボタンを追加。クイック選択（今日/直近7日/直近30日）およびカスタム日付範囲（DatePicker）で更新日時を絞り込み可能
- **Lucene クエリ段階フィルタ**: インデックス検索モードではサイズ（NumericRangeQuery）・日付（TermRangeQuery）をクエリレベルで適用し、検索精度と速度を両立
- **ICollectionView フィルタ**: 通常検索モードでもポスト検索フィルタとして一貫して動作
- **フィルタ永続化**: サイズ・日付フィルタの状態を settings.json に保存。アプリ再起動後も復元される
- **Popup UI**: スコープセレクターと同一の Zenith Modern Style（CornerRadius=8、DropShadow、スライドインアニメーション、スマートクローズ）を踏襲。相互排他的に開閉

## [0.12.18] - 2026-02-28 : スコープセレクター — 視認性最適化 + スマート・クローズ

### Changed
- **スコープ Popup テキスト高コントラスト化**: フォルダ名を TextBrush（#1A1A1A）、パスを #666666、件数を #555555 に変更。ベージュ背景に対しくっきりとした可読性を実現。ホバー時は白系（#FFFFFF / #C0BBAC / #CCCCCC）に反転
- **スマート・クローズ**: Popup 外クリックに加え、ウィンドウ Deactivated（他アプリへのフォーカス移動）時に自動クローズ。イベント購読の解除を CloseScopePopup() に集約

## [0.12.17] - 2026-02-28 : インデックス検索スコープセレクター

### Added
- **検索スコープセレクター**: インデックス検索モードの検索バーにスコープボタンを追加。登録フォルダの中から検索対象を任意に絞り込める Popup セレクターを実装。バッジ（例: 3/10）で選択状態を即座に把握でき、絞り込み中はアンバー色で強調表示。高コントラストなフォントカラーで長時間作業の可読性を確保し、ホバー時は白系に反転
- **スマート・クローズ**: スコープセレクターの Popup は外部クリックに加え、ウィンドウ非アクティブ化（他アプリへの切り替え）時にも自動的に閉じる
- **リアルタイム再検索**: スコープ選択を変更すると、表示中の検索結果が自動的に再描画される
- **複数パス検索**: IndexService に複数ルートパスを対象とする Search オーバーロードを追加。BooleanQuery で複数 PrefixQuery(SHOULD) を結合し効率的に絞り込み
- **スコープ永続化**: 選択状態を settings.json の IndexSearchScopePaths に保存。アプリ再起動後も復元される
- **バックアップ対応**: SettingsBackupService のバックアップ差分表示にスコープ情報を追加

## [0.12.16] - 2026-02-28 : インデックスロック（アーカイブ）機能のメタデータ消失修正

### Fixed
- **ロック済みアイテムのメタデータ消失修正**: ロック（アーカイブ）されたインデックスアイテムのドキュメント件数・最終更新日時が「未作成」に戻ってしまう不具合を修正。ロック時にドキュメント件数をスナップショットし `indexed_roots.json` に永続化するようにした
- **ValidateIndexedRoots のロック済みデータ保護**: Lucene インデックスファイル不在時の整合性チェックでロック済みアイテムの `_indexedRoots` / `_indexedTimestamps` が消去されないよう保護を追加
- **UnmarkAsIndexed のロック済みガード**: ロック済みアイテムの `_indexedRoots` エントリが間接的に削除されるのを防止
- **ステータス表示の是正**: ロック済みアイテムのステータスが「未作成」と表示されていたのを、件数と日時を維持した正しい表示に修正。`StatusKind` に `Locked` 状態を追加し、XAML トリガーで日時・件数カラムを表示

## [0.12.15] - 2026-02-28 : 包括的リファクタリング（保守性・安定性・パフォーマンス改善）

### Fixed
- **メモリリーク修正（UI層）**: FilePaneControl の TabListPopup.Opened イベント解除漏れ、TabContentControl の LayoutUpdated/IsVisibleChanged 解除漏れ、MainWindow の telemetryPopupTimer/itemTelemetryPopupTimer の Closing 時停止漏れを修正
- **アニメーションクロック解放**: Welcome アニメーション退場時および ContentRendered のフェードイン完了後に BeginAnimation(null) でクロックを解放し、メモリリークを防止
- **CancellationTokenSource の Cancel 漏れ**: TabItemViewModel.Dispose で _loadCts / _iconLoadCts の Cancel() を Dispose() 前に追加
- **DB 初期化 faulted リセット**: DatabaseService の _initTask が faulted の場合にリセットしてリトライ可能に
- **DB Upsert の TOCTOU 競合排除**: SaveHistoryAsync / SaveSearchHistoryCoreAsync / SaveRenameHistoryAsync の SELECT+INSERT/UPDATE を RunInTransactionAsync でアトミック化
- **検索履歴保存 catch の限定化**: SaveSearchHistoryCoreAsync の catch をスキーマ不整合（no such column/table）限定にし、UNIQUE 違反での誤マイグレーションを防止
- **IndexService CTS 競合修正**: _globalIndexingCts.Token の読み取りを lock(_lockObj) で保護し CancelIndexing との競合を解消
- **ScanSubtree 個別ファイル例外でスキャン継続**: try-catch をループ内に移動し、アクセス拒否等の個別ファイル例外でもスキャンが中断しないように改善
- **CSV/Excel 出力の IOException 個別キャッチ**: ファイルが使用中の場合に日本語メッセージで通知

### Changed
- **IndexService per-item catch にスロットル付きログ**: フォルダ/ファイルのインデックス作成で例外発生時に最初の5件をログ出力し、完了後に合計スキップ数をログ
- **PathHelper Box 判定の共通化**: DetermineSourceType 内のインライン IndexOf を IsInsideBoxDrive() 呼び出しに統一
- **デッドコード TryMigrateAndSave() を削除**: WindowSettings.Load() から不要な分岐を除去
- **不要な .ToList() を .ToArray() に変更**: IndexService / IndexSearchSettingsViewModel / TabItemViewModel のサムネイルバッチ処理でメモリ効率を改善

## [0.12.14] - 2026-02-28 : 超高速マイクロアニメーション統一

### Changed
- **ファイルリスト行ホバーを ColorAnimation に変更**: Details/Icon 両ビューのホバー背景色を即座の Setter 方式から ColorAnimation（60ms in / 80ms out）に変更し、滑らかなフェードイン・アウトを実現
- **タブ切替フェードインを高速化**: フォルダ切り替え時のフェードインを 0→1 / 400ms から 0.8→1.0 / 100ms に短縮し、「パチッ」感を解消しつつ瞬間的な視覚補完を維持
- **TabListPopup ホバーの Exit を短縮**: ホバー解除時のフェードアウトを 120ms → 80ms に短縮し切れ味を向上
- **テレメトリPopup の出現を 100ms フェードインに変更**: WPF ビルトイン `PopupAnimation="Fade"` を廃止し、code-behind で 100ms DoubleAnimation による制御されたフェードインに統一

### Added
- **`EnableMicroAnimations` 設定フラグ**: settings.json で `"EnableMicroAnimations": false` を指定するとタブ切替フェードイン・テレメトリPopup フェードを即座表示にスキップ可能

## [0.12.13] - 2026-02-28 : 静かなステータスバー + テレメトリポップアップ

### Changed
- **ステータスバーのインデックス進捗表示を安定化**: フォルダ名を除去し「インデックス更新中: 12,345 件」の数値のみ表示に変更。テキスト幅の頻繁な変動によるガタつきを解消
- **テレメトリポップアップの追加**: ステータスバーのスピナーアイコンにホバーすると、ターミナル風ポップアップで詳細情報（Scan / Speed / DB / Thread / Time）を表示。インデックスパネルの各フォルダのスピナーにも同様のポップアップを追加。ポップアップ表示中のみ1秒間隔でテレメトリを計算し、通常時のバックグラウンド負荷をゼロに保持

## [0.12.12] - 2026-02-28 : インデックス更新中のスキャンフォルダ表示

### Changed
- **インデックス更新中のスキャンフォルダ名を表示**: 巨大フォルダのインデックス更新時、ステータスバーに「インデックス更新中: SubFolder (X 件済)」形式で現在スキャン中のサブフォルダ名を表示。インデックス管理パネルの「作成中...」横にもグレーのサブフォルダ名を表示し、処理が進行中であることを視覚的に伝達。フォルダ名の更新は1秒に1回のスロットルで CPU/UI 負荷を排除

## [0.12.11] - 2026-02-28 : インデックス管理パネルの自動ソート

### Added
- **インデックス管理パネルのアイテム自動ソート**: フォルダ一覧を「作成中/待機中 → 未ロック(古い順) → ロック済み(古い順)」の3グループに自動ソートし、管理効率を向上。インデックス完了・ロック切替時に自動で再配置される

## [0.12.10] - 2026-02-28 : ファイル一覧の行高圧縮

### Changed
- **ファイル一覧の行間を圧縮し表示密度を向上**: 全5カラム（名前・場所・日付・種類・サイズ）のセル上下パディングを 4px→2px に縮小し、1画面あたり2〜3行多く表示可能に。アイコンに VerticalAlignment="Center" を追加し行内の垂直バランスを維持

## [0.12.9] - 2026-02-27 : インデックス個別ロック（アーカイブ）機能の修正

### Fixed
- **起動時にロック済みフォルダが再スキャンされる不具合を修正**: `MainWindow.xaml.cs` の起動時自動更新で `GetPathsForSave()` の全パスをロックチェックなしで `TriggerUpdateNow` に渡していた問題を、`IsRootLocked` によるフィルタリングを追加して修正
- **`TriggerUpdateNow` にサービス層のロックガードを追加**: 呼び出し元に依存せず、サービス自体がロック済みパスをスキップする防御的チェックを追加。手動の `RebuildRoot` / `UpdateDirectoryDiffAsync` は別経路のため影響なし
- **`RequestFullRebuild` でロック済みパスの `UnmarkAsIndexed` を防止**: フルリビルド時にロック済みフォルダのインデックス済み状態が消去される問題を修正
- **ロック切り替え時に待機中/作成中の表示が残る不具合を修正**: `ToggleLock` でロック時に `IsWaiting`/`IsInProgress` を即座にクリアするよう変更
- **`RefreshItemsStatus` でロック済みアイテムの待機中/作成中を抑止**: `IsLocked` を先に評価し、ロック済みアイテムの `IsInProgress`/`IsWaiting` を常に `false` に設定
- **`IsLocked` 変更時に `StatusKind`/`StatusText` の PropertyChanged が発火しない不具合を修正**: `NotifyPropertyChangedFor` に `StatusKind` と `StatusText` を追加

## [0.12.8] - 2026-02-27 : TabListPopup 高密度ブラックホバー刷新

### Changed
- **タブ一覧を「高密度・モダン・ブラック」スタイルに刷新**: 行高 MinHeight=32px + Padding 上下 2px でコンパクト化し、本体ファイルリストと同等の密度感に
- **ブラックホバーエフェクト**: マウスオーバーで背景を本体共通のチャコールブラック(#545B64 = OnePointDarkColor)に即座切替(60ms)、テキスト・アイコンを白に反転。タイトルバー・お気に入り第1階層・アクティブタブと同一色で全体調和。離脱時は 120ms でフェードアウト
- **選択行の左アクセントバーを 2px に細線化**: 黒背景でもシャープに光るよう AccentBrush 2px + CornerRadius 1 に調整
- **閉じるボタンを暗色テーマ対応に変更**: 行ホバーで白アイコン表示、ボタン自体のホバーで暗赤背景(#3D0000)+赤アイコン(#FF6B6B)に。サイズ 18px でコンパクト化
- **スライドインアニメーションを短縮**: 移動距離 5px / フェード 120ms / スライド 140ms でキビキビした動作に
- **ポップアップ外装の引き締め**: CornerRadius 8→6、Padding 4,4 に縮小。ドロップシャドウはコンテキストメニュー統一仕様を維持

### Fixed
- **選択中タブ(1件目)をホバーしても黒背景にならない不具合を修正**: 選択背景(`SelectedBg`)とホバー背景(`HoverBg`)を別 Border に分離し、ColorAnimation の競合を根本排除。ホバー Border が前面で選択色を完全に覆う構造に変更
- **ホバー時のテキスト白反転が選択行で効かない不具合を修正**: DataTemplate.Triggers の定義順を整理し、ホバートリガーを選択トリガーの後に定義することで優先度を保証
- **Frozen TranslateTransform 例外の回避**: Opened イベントで新規 TranslateTransform(0, -5) を毎回割り当て
- **Frozen SolidColorBrush の回避**: HoverBg に明示的 SolidColorBrush 要素、選択背景は Setter のみで制御

## [0.12.5] - 2026-02-27 : ファイル一覧出力の修正

### Fixed
- **出力対象をファイルのみに修正**: フォルダ行を出力から除外し、ファイルのみの一覧を生成するよう変更
- **カレントフォルダ直下ファイルの相対パスが絶対パスになる不具合を修正**: 直下のファイルは `.\` を相対パスとして正しく出力するよう修正

## [0.12.4] - 2026-02-27 : ファイル一覧出力の納品資料グレードへのブラッシュアップ

### Changed
- **ファイル一覧出力（Excel/CSV）を全7列の納品資料グレードに刷新**: カラム構成を「名称, サイズ, 種類, 更新日時, 相対フォルダパス, フォルダパス (共有), フォルダを開く」に統一
- **共有用パス列（F列）を追加**: Box (`C:\Users\...\Box\Proj` → `Box\Proj`)・OneDrive (`C:\Users\...\OneDrive - Company\...` → `OneDrive - Company\...`) のパスを環境非依存のルート名ベースに自動変換。`PathHelper.GetShareablePath` メソッドを新設
- **Excel ハイパーリンクを絶対パス（file:/// URI）に変更**: G列のリンクをクリックするとエクスプローラーで該当フォルダが即座に開く。URI エスケープによりスペースや日本語パスにも対応
- **Excel スタイルを報告書品質に統一**: ヘッダー行は薄グレー背景+太字+ウィンドウ枠固定、全セルに格子罫線（Thin）、外枠に Medium 線、列幅の AutoFit＋最低/最大幅保証、オートフィルタ適用
- **CSV 出力を Excel 版と完全一致**: 7列のヘッダー名・順序・パス生成ロジックを統一。UTF-8（BOM付き）で文字化け防止。G列はフォルダフルパス（テキスト）
- **再帰スキャンを EnumerateFiles に統一**: アクセス不能フォルダを IgnoreInaccessible でスキップ

## [0.12.3] - 2026-02-27 : インデックスロック状態の永続化修正

### Fixed
- **インデックスロック状態が settings.json に保存されない不具合を修正**: ロック状態を `settings.json` の `IndexSearchLockedPaths` に永続化するよう変更。アプリ再起動時に `settings.json` からロック済みパスを復元し `IndexService` に適用。従来の `indexed_roots.json` の `|locked` サフィックスによる保存は補助的に維持
- **`UnmarkAsIndexed` がロック状態を消去していた不具合を修正**: 手動リビルドやインターバル再構築時に `UnmarkAsIndexed` が `_lockedRoots` からもパスを除去しており、ロック状態が失われていた。ロックはユーザー設定でありインデックス状態とは独立のため、`_lockedRoots` の除去を削除

## [0.12.2] - 2026-02-27 : リネームダイアログ ブラッシュアップ

### Added
- **カスタム定型ボタン機能**: リネームダイアログに「＋」ボタンを追加。現在の入力内容をカスタムボタンとして SQLite に保存し、次回以降ワンクリックで呼び出し可能。右クリック「このボタンを削除」で個別削除。永続化テーブル `CustomRenameButtons` を新設

### Changed
- **履歴表示のポップアップ化**: 入力欄右端に控えめな履歴（▼）アイコンを配置。履歴リストはデフォルト非表示とし、アイコンクリックまたは↓キーで表示。項目選択で即反映・即閉じ。Enter/↑キーのキーボード操作にも対応
- **議事ログ生成ロジック改善**: 親フォルダ名が「8桁数字_」で始まる場合（例: `20260227_プロジェクト名`）、日付部分を自動除去して `本日の日付_議事ログ_プロジェクト名` を生成するよう改善
- **ボタン群のデザイン刷新**: 日付・コンテキスト・カスタムボタンを単一の WrapPanel で折り返し表示。共通テンプレートに統一し、ウィンドウ肥大化を抑制。MaxWidth 制限で横幅の安定性を確保

## [0.12.1] - 2026-02-27 : フォルダサイズ表示（インデックスDB活用）

### Added
- **フォルダサイズ表示機能**: インデックス済みフォルダを開くと、各サブフォルダのサイズ列にインデックスDBから集計した合計サイズを表示。ファイルシステムへの再帰的アクセスは行わず、Lucene.NET インデックスの PrefixQuery で配下ファイルのサイズを高速集計。フォルダサイズはイタリック・薄色で通常ファイルと視覚的に区別。アイコンビューでは「フォルダ • 1.2 GB」形式で表示。インデックス未登録フォルダでは従来通り空欄。フォルダ間を素早く移動しても CancellationToken で前回の集計を即キャンセルし、UI フリーズや結果混入を防止

## [0.12.0] - 2026-02-27 : インデックス個別ロック（アーカイブ）機能

### Added
- **インデックス個別ロック（アーカイブ）機能**: 更新が不要な古いフォルダを一括更新・定期更新の対象外にするロック機能を追加。右クリックメニュー「インデックス更新をロック」でフォルダ単位でロック/解除を切替可能。ロック済みフォルダは一覧に南京錠アイコン（灰色）で表示され、「すべて更新」「フル再構築」「再開」「定期更新」の一括操作から自動的に除外される。手動での個別操作（右クリック→差分更新/再作成）はロック中も許容。24時間超過の鮮度アラート（アンバー表示）もロック済みフォルダでは抑制。ロック状態は `indexed_roots.json` に永続化されアプリ再起動後も維持

## [0.11.5] - 2026-02-27 : コンテキストメニュー開閉時の GlowBar / DragAdorner 誤発火の根治修正

### Fixed
- **コンテキストメニュー開閉に伴う GlowBar キャンセルアニメーション・DragAdorner の誤発火を根治修正**: 根本原因は 3 つ: (1) Shift+右クリック時のパッシブ Box 監視で GlowBar が不要に開始されていた、(2) Win32 `TrackPopupMenuEx` のメニュー外クリックが WPF に転送されドラッグ状態が汚染されていた、(3) `CancelMonitoringSilently` が無条件に `IsFileOperationActive = false` を設定し進行中ファイル操作の GlowBar を破壊していた。以下の多層防御で対策:
  - **GlowBar 除去**: `StartSharedLinkClipboardMonitor`（パッシブ監視）から `StartGlowBar` を完全除去。URL 検知時のトースト通知＋グリーンフラッシュは維持
  - **GlowBar ガード**: `StopMonitoring` / `CancelMonitoringSilently` に `_progressTimer / _glowStopwatch / _busyToken` の null チェックを追加。BoxDriveService が GlowBar を開始していない場合は `IsFileOperationActive` 等の UI プロパティに一切触れない
  - **統一コンテキストメニューフラグ**: `_explorerMenuActive`（Explorer 専用）を `_isContextMenuActive`（Zenith / Explorer 統一）に置換。`ShowContextMenuForItems` 冒頭で ON、全メニュー閉鎖パスで OFF
  - **クールダウン期間**: メニュー閉鎖時にタイムスタンプ（`_contextMenuClosedTick`）を記録し、閉鎖後 150ms 以内のマウスイベントを `IsContextMenuCooldown()` で抑制。Win32 メニュー外クリックの WPF 転送タイミング競合を防止
  - **マウスイベントガード**: `PreviewMouseLeftButtonDown` / `PreviewMouseRightButtonDown` / `ListView_MouseMove` の 3 箇所にクールダウンガードを設置。メニュー閉鎖直後のドラッグ状態汚染（`_mouseDownItem` / `_dragStartPoint` セット）を完全遮断
  - **ドラッグ状態の確実なリセット**: メニュー開始時と全閉鎖パス（Zenith `Closed` / Explorer `onMenuClosed`）で `_mouseDownItem` / `_isRightDragPossible` / `_rightMouseDownItem` を明示リセット
  - **IO パスの保護**: `MainViewModel.CancelFileOperation` / `BeginFileOperation` / `EndFileOperation` / `FileOperationToken` および `DropFilesInternal` / `TurboCopyFileAsync` は一切変更なし

## [0.11.3] - 2026-02-27 : コンテキストメニュー閉鎖時の GlowBar 通知サイレント化

### Fixed
- **コンテキストメニュー閉鎖時に GlowBar が不自然にフェードアウトする問題を修正**: Shift+右クリックで Box Drive アイテムに対して Explorer コンテキストメニューを表示した際、`BoxDriveService.StartSharedLinkClipboardMonitor` が即座に GlowBar を開始し、メニューを何も選ばず閉じてもタイムアウトまで GlowBar が動作していた。メニュー閉鎖時に `CancelMonitoringSilently()` で GlowBar を即リセット（アニメーション・通知なし）するよう変更。Zenith メニューでも `Closed` イベントで `CancellationTokenSource` をキャンセルし、`CloudShellMenuService.ExtractCloudMenuItemsAsync` の非同期タスクをサイレント中断。ファイルコピー/移動/削除の IO 処理パス（`MainViewModel.FileOperationToken` / `CancelFileOperation`）には一切変更なし

## [0.11.2] - 2026-02-27 : コンテキストメニュー配置のエクスプローラー準拠リオーダー

### Changed
- **コンテキストメニュー項目配置をエクスプローラー準拠に再構成**: 全4ブランチ（背景・単一フォルダ・単一ファイル・複数選択）の項目順序を Windows エクスプローラーの標準的な「開く→クリップボード→編集→新規作成→プロパティ」の流れに準拠するよう並べ替え。背景右クリックでは「貼り付け」を先頭に移動し「新規作成」グループをまとめて配置。アイテム選択時は「コピー/切り取り/貼り付け」の直後に「削除/名前の変更/ショートカットの作成」を配置し、その後にパスコピー・アプリ固有機能、最後に「プロパティ→リンク連携コピー→クラウドメニュー→エクスプローラのコンテキストメニュー」の順で統一

## [0.11.1] - 2026-02-27 : コンテキストメニュー構造整理・GlowBar 進捗修正

### Changed
- **コンテキストメニュー構造整理＋「リンク連携コピー」独立化**: クラウドシェル拡張メニュー項目をフラット配置から「リンク連携コピー」（独立項目）＋「クラウドメニュー ▶」（サブメニュー）の2段構成に分離。「リンク連携コピー」は Box 領域でリッチ HTML コピー（`StartRichShareLinkMonitor`）、OneDrive 領域で標準 Copy link を実行し、ローカルフォルダでは非活性。「クラウドメニュー ▶」は全シェル拡張項目を標準動作（`InvokeCloudMenuCommand`）で格納するサブメニューとして固定配置し、プレースホルダーや項目の追加/削除がサブメニュー内部で完結するためルートメニューのリフロー（ジャンプ）を防止。`CloudShellMenuService.FindCopyLinkItem` を追加し、Box「リンクをコピー」/ OneDrive「リンクのコピー」/ 英語「Copy link」を再帰検索で統一判定

### Fixed
- **Box共有リンクコピーの GlowBar 進捗が 100% に到達しない問題を修正**: `StopGlowBar()` がタイマーを即座に停止していたため、ダンピングアニメーション中の進捗値（例: 70%）で表示が凍結し、そのまま `EndFileOperation()` でフェードアウトされて「キャンセルされた」ように見えていた。修正後は進捗目標を 100% に引き上げた上でタイマーに 350ms の追従猶予を与え、最低表示時間（800ms）と合わせて待機後に `FileOperationProgress = 100` を明示セットしてからフェードアウトを実行

## [0.11.0] - 2026-02-27 : RenameDialog 新設・Box共有リンク HTML コピー・GlowBar 進捗表示

### Changed
- **クラウドメニュー確実表示**: Box / SharePoint フォルダでの右クリック時、クラウドシェル拡張メニューが初回から確実に表示されるよう改善。`ConcurrentDictionary` キャッシュ（TTL 60秒、最大20エントリ）により2回目以降は同期的に即表示。初回はメニューを async 化し最大 300ms の短い await で COM 完了を待機、間に合わない場合は「読み込み中...」プレースホルダーを表示して後から差し替え。フォルダナビゲーション時にバックグラウンドプリフェッチを実施し、右クリック時のキャッシュヒット率を向上。デフォルトタイムアウトを 2000ms → 3000ms に引き上げ

### Added
- **Box共有リンクの個別URL取得＋HTML形式コピー＋GlowBar進捗表示**: 「共有 ▶ リンクをコピー」で選択アイテムごとに個別の Box 共有 URL を順次取得し、HTML（CF_HTML）形式とプレーンテキスト形式の両方をクリップボードにセット。Teams / Outlook / Slack 等のリッチエディタではアイテム名がクリック可能なハイパーリンクとして貼り付けられ、メモ帳等のプレーンエディタでは `アイテム名 ( URL )` 形式でフォールバック。親フォルダの Box 相対パス＋親フォルダ URL＋各アイテムのリンク付き名前の3層構成。各アイテムの URL は `CloudShellMenuService.ExtractCloudMenuItemsAsync` で IContextMenu を再構築し「リンクをコピー」コマンドを自動検出・実行して取得。URL 取得失敗時は名前のみ（リンクなし）でグレースフルに出力。DispatcherTimer 追従方式の GlowBar 進捗表示を導入し、フォルダ URL 取得（2→9%）→ 各アイテム URL 取得（10→90%、N/Total 件表示）→ クリップボード書き込み（95%）→ 自動フェードアウトの全フェーズをスムーズに可視化。Shift+右クリック時の単発 URL 取得でも GlowBar を表示。最低表示時間 800ms を Stopwatch で保証
- **RenameDialog（専用リネームダイアログ）**: ファイル/フォルダの名前変更に特化した専用ダイアログ `RenameDialog` を新設。InputBox と同一パターン（シングルトン + DispatcherFrame モーダル）で `WindowStyle="None"` + `AllowsTransparency="True"` + 角丸 + DropShadow のプレミアムデザイン。定型入力ボタン3種（`YYYYMMDD` / `YYYYMMDD_` / `_YYYYMMDD`）で本日の日付を即挿入。コンテキスト候補ボタン2種（親フォルダ名 / `YYYYMMDD_議事ログ_親フォルダ名`）を表示（ドライブルートでは自動非表示）。SQLite 連携の入力履歴ポップアップ（最大100件、LastUsed DESC）を TextBox フォーカス時に自動表示し、履歴選択で即反映。拡張子分離表示は InputBox と同一ロジックを継承。F2 / 右クリック「名前の変更」/ ナビペインのフォルダ名前変更で使用。お気に入り等の汎用入力は従来の InputBox をそのまま使用

## [0.10.1] - 2026-02-27 : 安定性向上パック（ZenithDialog・RestartManager・FSW バックオフ・メモリリーク修正・ログ強化）

### Added
- **ZenithDialog（カスタム MessageBox）**: 独自デザインのモーダルダイアログ（`ZenithDialog`）を実装。InputBox と同一パターン（シングルトン + DispatcherFrame モーダル）で、`WindowStyle="None"` + `AllowsTransparency="True"` + 角丸 + DropShadow のプレミアムデザイン。Info / Warning / Error / Question の4種アイコン（PackIconLucide）対応。OK / OKCancel / YesNo の3種ボタン構成。App.xaml.cs のクラッシュハンドラ2箇所を除く全 MessageBox.Show（約40箇所）を ZenithDialog に置換
- **RestartManager プロセス検出**: ファイル操作（リネーム・削除・コピー・移動・D&D）の IOException 発生時に、Windows Restart Manager API（`RmStartSession` / `RmRegisterResources` / `RmGetList`）を使用してファイルをロックしているプロセス名を特定し、「Excel が使用中のため操作できません」等のユーザーフレンドリーなエラーメッセージを表示。UnauthorizedAccessException / ディスク容量不足にも対応
- **起動診断ログ**: アプリ起動時にバージョン・OS・.NET ランタイム・PID の診断情報をログに記録（`LogStartupDiagnostics`）
- **インデックスビュー ダブルクリックで登録内容確認**: インデックス検索対象フォルダの一覧で項目をダブルクリックすると、右クリックメニューの「インデックス登録確認」と同じ動作（Aペインに検索結果タブを開いてインデックス登録内容を一覧表示）を実行
- **インデックスビュー 4カラム単一行レイアウト**: フォルダ一覧を [場所アイコン | フォルダ名 | 完了日時 | 件数/ステータス] の4カラム構成に刷新。Col 2（日時）と Col 3（件数/ステータス）を `SharedSizeGroup` で全行揃えの独立カラムに分離。完了時は Col 2 に日時（`yyyy/MM/dd HH:mm`）、Col 3 に件数（`(12,345)` 形式）を表示し、24時間以上経過で鮮度アラート（アンバー #C68958）、以内はグレー（#888888）。作成中は日時を隠しスピナー＋ステータス表示、待機中はイタリック薄色、未作成はアンバー。Col 2・Col 3 のフォントサイズをフォルダ名と同じ 12pt に統一。`CompactDateText`・`CountBadgeText`・`IsStale` プロパティを `IndexSearchTargetItemViewModel` に追加。場所アイコンはナビペインの4種類（`PathHelper.DetermineSourceType`）と同期。`LastIndexedDateTime` を `indexed_roots.json` に Dictionary 形式（パス→ISO 8601 日時）で永続化（旧 List 形式との後方互換あり）

### Changed
- **マニュアルウィンドウのタイトルからバージョン文字列を削除**: 単独起動時のウィンドウタイトルに含まれていた `(v0.x.x)` 表記を削除し、バージョンアップ毎の更新を不要に
- **FileSystemWatcher バッファサイズ統一**: クラウド同期フォルダ判定による分岐（65536 / 8192）を廃止し、全環境で 64KB に統一
- **FileSystemWatcher 指数バックオフ再接続**: エラー発生時の即座再接続を、1秒→2秒→4秒→...→最大30秒の指数バックオフに変更（整数ビットシフトで高速計算）。最大10回のリトライ後は「フォルダ監視が停止しました」通知を表示し手動 F5 待ち。再接続成功時に「フォルダ監視を再開しました」通知。ログに HResult と試行回数を記録。CTS の原子的スワップ（`Interlocked.Exchange`）でスレッド競合を排除、`BeginInvoke` でスレッドプールブロッキングを解消
- **例外ログの HResult 記録**: `FormatException` の先頭行と InnerException 行に `HResult=0x{HResult:X8}` を追記し、Windows エラーコードの特定を容易に
- **ZenithDialog ブラシキャッシュ**: `SolidColorBrush` を `static readonly` + `Freeze()` で事前生成し、表示のたびにブラシオブジェクトを再生成する GC 圧力を排除。アイコンマッピングを配列インデックス O(1) ルックアップに変更。ボタンレイアウトが前回と同一の場合は再構築をスキップ
- **RestartManager の堅牢性強化**: null / 空パスのガード節追加、P/Invoke 失敗時のログ出力、`HashSet` による重複排除、`GetFriendlyErrorMessage` の null 安全化。削除エラーの RM 問い合わせで `string.Join` した結合パスを渡していた問題を修正し、先頭ファイルパスのみを渡すよう変更

### Fixed
- **FileTypeFilterItem の PropertyChanged リーク修正**: 匿名ラムダで購読していた `PropertyChanged` ハンドラを名前付きメソッド（`OnFileTypeFilterItemPropertyChanged`）に変更し、`Dispose()` でハンドラを解除。タブ開閉を繰り返した際のメモリリークを解消
- **ZenithDialog の二重呼び出し防止**: `CloseWithResult` で `_frame` を先にクリアして再入を防止。`DragMove()` を `IsVisible` ガードで保護し、ウィンドウ非表示遷移中の `InvalidOperationException` を排除

## [0.10.0] - 2026-02-27 : PC ビュー・新規作成メニュー・クラウド連携・Excel/CSV 出力強化・お気に入りバックアップ

### Added
- **仮想「PC」ルートナビゲーション**: ドライブルート（C:\ 等）の「上へ」ボタンで、全ドライブを一覧表示する仮想「PC（マイコンピュータ）」ビューに移動可能に。PC ビューではドライブ名・容量・種別・アイコンを表示し、ダブルクリックでドライブルートに移動。パンくずリストの先頭に「PC」セグメントを追加し、クリックで PC ビューに移動。パンくずの PC ドロップダウンからドライブ一覧を選択可能。アドレスバーに「PC」と入力して Enter で PC ビューに移動
- **「新規作成」サブメニュー**: 背景右クリックメニューに「新規作成 ▶」サブメニューを追加。Windows のレジストリ（HKCR の ShellNew エントリ）をスキャンし、Word / Excel / PowerPoint 等のインストール済みアプリケーションのテンプレートファイルをアイコン付きで一覧表示。選択するとテンプレートコピーまたは空ファイルを作成し、リネームダイアログを自動表示。起動時にバックグラウンドでレジストリスキャンを実施しキャッシュ
- **クラウドシェル拡張メニュー統合**: Box / SharePoint / OneDrive フォルダでの右クリック時に、クラウドサービス固有のシェル拡張メニュー項目（「Box で共有」等）をフィルタ抽出して独自コンテキストメニューに統合。STA スレッドで IContextMenu を構築しキーワードフィルタで関連項目のみを表示。1.5秒のタイムアウト付きで非同期取得
- **お気に入りExcelバックアップ**: お気に入りビュー下部に FileSpreadsheet アイコンを追加。お気に入りデータ（No・グループ・表示名・フルパス・種類・開くリンク）を Excel 形式でバックアップ保存可能に。グループ階層を「親 > 子」形式で展開し、整理用フォルダも含む全項目を出力。F列は D列のフルパスを参照する HYPERLINK 関数で出力。格子罫線・ヘッダー固定・オートフィルタ・AutoFit 付き。GlowBar による滑らか進捗表示対応
- **Box 共有リンクのリッチクリップボードコピー**: Box フォルダ内で「共有 ▶ 共有リンクをコピー」を実行した際、Box パス + 共有 URL + 対象ファイル名一覧の3層リッチ形式でクリップボードに格納。背景右クリック時はフォルダ内全アイテム名、アイテム選択時は選択ファイル名を自動付与。WM_CLIPBOARDUPDATE 監視で Box シェル拡張の URL 書き込みを検知し即座に整形

### Fixed
- **「新規作成」サブメニューの ShellNew レジストリスキャン**: Office 等のアプリは `.ext\{ProgID}\ShellNew`（例: `.docx\Word.Document.12\ShellNew`）のネスト構造にテンプレートを登録しているが、直下の `.ext\ShellNew` のみを探索していたため項目が検出されなかった問題を修正。ProgID サブキー配下も探索するよう変更
- **クラウドメニューのサブメニュー未展開**: Box の「共有」等、シェル拡張側でサブメニューを持つ項目がフラットな単一項目として表示されていた問題を修正。サブメニューを持つ項目を検出した場合、子項目を全て収集して WPF の入れ子 MenuItem として構築するよう変更
- **コンテキストメニューの画面オーバーフロー**: メニュー項目が多い場合に画面下部の項目が選択不能になる問題を修正。ContextMenu テンプレートに ScrollViewer を追加し、画面高さに基づく動的 MaxHeight でスクロール可能に。影マージン（28px）を考慮した正確な位置補正と、マルチモニタ対応のワークエリア取得（WindowHelper.GetWorkArea）に改善。サブメニュー Popup にも ScrollViewer を追加し万一のオーバーフローに対応

### Changed
- **Excel/CSV出力の共有・視認性強化**: Excel・CSV 両方の列構成を統一。パス列を絶対パスからカレントフォルダ起点の相対パス（`.\Sub\` 形式）に変更し、Box 等でユーザー名が異なる環境でもフィルタが共通利用可能に。サイズ列を人間可読形式（`1.5 MB` 等）に統一。CSV のヘッダーを「項番・ファイル名・相対フォルダパス・更新日時・サイズ」に変更。Excel の G 列ハイパーリンクを `=HYPERLINK(".\相対パス\","開く")` の関数形式に書き換え、フォルダ構造ごと共有した際にどの PC からでもリンクが機能するよう改善。Excel の全データ範囲（ヘッダー含む）に格子罫線を適用し外周は Medium 線で報告書風に。ヘッダー背景色をテーマ配色のセピア（#E6E1D3）に変更。列幅を AutoFit に変更し最低幅を保証
- **独自コンテキストメニューのプレミアムプレートデザイン刷新**: 二重ボーダー構造（外枠 #B0B0B0 + 1px #FEFEFE ライトリム）で「精密に削り出されたエッジ」を表現。DropShadow（Blur=24, Depth=6, Opacity=0.22, Dir=315）を右下方向に流し、Margin="0,0,28,28" で右下のみ描画領域を確保し左上の影をクリップ。テキスト色を濃いチャコール（#222222）、アイコンをダークトーン（#444444）に引き上げフォントの重みを確保。ホバー時は背景を本体と同一のチャコールブラック（#545B64 = OnePointDark）に反転し文字・アイコン・矢印・ショートカットを白系に切り替える高コントラスト・ダークスタイルで、シャープな選択感を演出。グローバル TextBlock スタイルの Foreground が継承より優先される問題を、Header の ContentPresenter を明示的 TextBlock（Foreground=TemplateBinding）に置き換えることで解決。破壊アクションのホバー色を #FF6B6B（ダーク背景対応）に変更。セパレータを #D5D5D0 に調整。右ドラッグカスタムメニューは ClipToBounds ラッパーで左上影をカット、サブメニュー Popup にも同一構造を適用
- **クラウドシェル拡張メニューのキーワード拡充と探索強化**: OneDrive/SharePoint 固有の「リンクのコピー」「アクセス許可の管理」「オンラインで表示」「バージョン履歴」等のキーワードをフィルタに追加し、日本語・英語両対応の部分一致で確実に抽出。`MIIM_SUBMENU` フラグを使った正確なサブメニュー検出と、depth 3 までの再帰スキャンでネストされた項目もフラット展開
- **独自コンテキストメニューの表示密度向上**: エクスプローラー同等のコンパクトな行間に変更。MenuItem の MinHeight を 34→22px、アイコン列幅を 44→30px に縮小、上下 Padding を大幅削減。セパレーターのマージンも `40,8,16,8` → `32,2,12,2` に圧縮。サブメニュー矢印（Chevron）を ControlTemplate に組み込み、Role=SubmenuHeader トリガーで自動表示。ContextMenu 外枠の Padding も `6,8` → `4,4` に縮小。コードビハインドの BuildZenithContextMenu で ItemContainerStyle を明示適用し、「新規作成」サブメニューやクラウドメニューのサブメニューにも統一スタイルを伝播

## [0.9.7] - 2026-02-26 : お気に入りツリー関係線の追加・レイアウト最適化・完全連続化

### Added
- **お気に入りツリーの関係線（Tree Lines）**: お気に入りビューの子アイテムに縦線＋水平コネクタのガイド線を追加。末尾の子は L 字型（縦線が中央で停止）、非末尾の子は全高の縦線を表示。Level 0 カテゴリヘッダーには線を表示しない。セピア背景に馴染むウォームグレー（#C4BFB0）で描画

### Changed
- **お気に入りアイテムの左側余白を排除**: 環境アイコン列を廃止しアイコン直接配置に変更、FullRowBackground の左マージン（2→0px）・Padding（3→1px）・アイコン Grid の左マージン（2→0px）を削減し計6px圧縮。水平コネクタを Width=8→27 に延長し `Panel.ZIndex="1"` で Border 上に描画することで関係線がアイコン左端まで密着。パス不正時は名前の右側に TriangleAlert アイコン（#C4A03C）を表示、正常時は Collapsed で横幅を占有しない
- **関係線の完全連続化と描画精度向上**: 縦線の軸を3px右へシフト（Margin `-10`→`-7`）しアイコンとの一体感を向上。`TreeLineVertFull` の `Grid.RowSpan="2"` を廃止し Row 0 専用に分離、新設の `TreeLineContinuation`（Row 1）で子要素領域の継続縦線を明示的に描画することで第2・第3階層以降でも線が途切れない構造に刷新。全 Rectangle に `SnapsToDevicePixels="True"` を追加し高DPIでの1px線のボケ・消失を防止
- **関係線スタイルをアプリ全体の TreeView に統一適用**: フォルダツリー（DirectoryTree）およびフォルダ選択ダイアログ（SelectFolderDialog）にも同一の ControlTemplate（縦線・水平コネクタ・継続線・ChevronRight/Down エキスパンダー・CornerRadius 3 背景）を適用。Padding を `4,2`→`1,1`、アイコンマージンを `6`→`4px` に圧縮し高密度表示に統一

## [0.9.6] - 2026-02-26 : 起動時挙動改善（位置先行決定＋フェード＆スライド演出）

### Added
- **インデックスビューへのドラッグ＆ドロップ登録**: A/B ペインからフォルダをドラッグしてサイドバーのインデックスビュー領域にドロップするだけで検索対象フォルダを登録可能に。ドロップゾーンにマウスが入るとハイライト表示でフィードバック。フォルダのみ受付（ファイルは無視）、重複チェック、パス存在確認を非同期で実施しフリーズを防止。空状態のプレースホルダーにアイコン付きの案内を表示
- ClosedXML を使用した Excel (.xlsx) 出力機能を追加（コンテキストメニュー「このフォルダ以下の一覧をExcel出力」）

### Changed
- **起動時挙動の改善**: settings.json を MainWindow 生成前に同期読み込みし、ウィンドウ位置を即適用することで「中央に一度表示→保存位置にジャンプ」する二段階表示を解消。コンテンツ Grid に 400ms のフェード＆スライドアニメーション（Opacity 0→1, Y 20→0）を追加し、滑らかな起動演出を実現
- **ウェルカムメッセージの最優先表示**: 起動時のウェルカムアニメーション中は `NotificationService.IsWelcomeActive` フラグで通知メッセージのステータスバー表示を抑制（ログのみ記録）。中央通知グループ（GlowBar ステータステキスト含む）も `Collapsed` にし、CSV/Excel 出力等のメッセージが割り込んでもウェルカムメッセージのみが表示されるよう保証
- **お気に入りビューの表示密度向上**: TreeViewItem の Padding を `4,2` → `3,1`、行間マージンを `2,1,10,1` → `2,0,10,0`、Level 0 カテゴリの MinHeight を `28` → `24`・マージンを `2,3,10,3` → `2,2,10,2`、アイコン左右マージンを `4,0,6,0` → `2,0,4,0` に圧縮し、スクロールなしで表示できるアイテム数を約 1.3 倍に増加
- 検索処理（インデックス検索・ローカル検索）の進捗表示を `IsLoading` オーバーレイから GlowBar（DispatcherTimer 追従方式）に切り替え、検索フェーズに応じた進捗をリアルタイム表示。高速パス（即結果あり）では 2→50→95%、プログレッシブ再検索パス（インデックス作成中）では再検索ループごとに +5% ずつ進行。キャンセル時は EndFileOperation のフェードと競合しないよう直接プロパティリセット
- CSV出力の保存先をカレントフォルダに自動決定するよう変更（SaveFileDialog を廃止）。進捗表示を `IsLoading` オーバーレイから GlowBar（DispatcherTimer 追従方式）に切り替え、スキャン 2→9%・書き込み 10→90%・保存 95% の件数ベース進捗をリアルタイム表示
- Excel出力処理の進捗表示を `IsLoading` オーバーレイから GlowBar（シアン進捗バー）に切り替え、件数ベースの進捗率をリアルタイム表示するよう改善
- GlowBar のアニメーション持続時間をジャンプ幅に応じて 150〜800ms に伸縮する適応型に変更し、大きな進捗変化でも滑らかに伸びる演出を実現
- Excel出力のスキャンフェーズを逐次列挙に変更し 2%→8% の仮想進捗を報告、書き込みフェーズの報告間隔を全件数の1%刻みに細分化
- Excel出力の保存先をカレントフォルダに自動決定するよう変更（SaveFileDialog を廃止）
- Excel出力完了時のポップアップダイアログを廃止（トースト通知のみに簡素化）
- 初回起動時（settings.json 未存在）のデフォルトペイン数を 2 → 1（1画面モード）に変更

### Fixed
- **パンくずリスト・タブヘッダーの表示欠け・消失を修正**: ペイン幅変更時に `ScrollViewer.InvalidateMeasure/InvalidateArrange` を強制呼び出しし描画追従性を強化。パス変更後に `OnPropertyChanged(PathSegments)` を Render 優先度で手動通知し View の再描画を確実に発火。パンくずセグメントに `TextTrimming="CharacterEllipsis"` + `MaxWidth="260"` を追加し長いフォルダ名を美しく省略。タブヘッダーを固定幅 `Width="150"` から `MinWidth="60"` / `MaxWidth="150"` に変更しペイン幅に追従。スライドインジケータ幅をタブの `ActualWidth` に動的追従
- **インデックスビューのフォルダ名表示を修正**: `GridView` の `Width="Auto"` を `SizeChanged` イベントでサイドバー幅に動的追従させ、`TextTrimming="CharacterEllipsis"` が正しく機能するよう修正。セルテンプレート内を `DockPanel` に変更しフォルダ名の `TextBlock` に有限幅を保証
- **インデックスビューのフォルダクリック時に検索バーの履歴ドロップダウンが一瞬表示される不具合を修正**: 3段階の防御で根絶 — ①`Sidebar_PreviewMouseLeftButtonDown` のインデックスビュー分岐で `FocusSearchBox()` （ペイン検索バーへのフォーカス移動）を廃止し `IndexSearchTargetList.Focus()` に変更、②`SearchTextBox_GotFocus` を `_userInitiatedSearchFocus` フラグで制御しプログラム的フォーカスでは履歴を開かない、③`OpenSearchHistory()` 内で `await` 後に `SearchTextBox.IsKeyboardFocusWithin` を検証し、非同期待機中にフォーカスが離れた場合はポップアップを抑制
- **入力用サブウィンドウが真っ黒に表示される問題を根本修正**: 全9ダイアログ（InputBox / AddFolderDialog / AddToFavoritesDialog / DescriptionEditDialog / SelectFolderDialog / SelectExplorerWindowsDialog / BackupListDialog / ChangelogWindow / ManualWindow）の `Background` / `Foreground` を `StaticResource` から直接カラーコード（`#F5F1E3` / `#1A1A1A`）にハードコード化し、リソース辞書の読み込みタイミングに依存しない即時描画を保証。`FocusManager.FocusedElement` を全入力ダイアログに追加しキャレットの即時点滅を保証。InputBox の `Opacity=0→1` ハック（一部 GPU で黒フレームの原因）を撤去。`AppResources.xaml` に `BaseDialogStyle` を新設し共通プロパティを一括管理

## [0.9.5] - 2026-02-25 : 起動時パス検証の完全非同期化

### Fixed
- **起動時パス検証の完全非同期化**: settings.json にネットワークパス（UNC やマップドドライブ）が含まれている場合に `DriveInfo.IsReady` や `Directory.Exists` が OS レベルのタイムアウト（数十秒）を引き起こし起動がフリーズする問題を修正。`PathHelper.IsNetworkPath` から `DriveInfo` 呼び出しを除去し UNC プレフィックスのみで判定、`DirectoryExistsSafeAsync` を全パスでタイムアウト付き非同期に変更、`DirectoryTreeViewModel.LoadDrivesAsync` を新設して各ドライブの `IsReady` を 500ms タイムアウト付きで検査、`MainViewModel.InitializeAsync`・サイドバーモード切替・ツリーリフレッシュの全呼び出し箇所を非同期版に置換
- **起動時の白フラッシュ・段階的表示を完全根絶**: Window に `Visibility="Hidden"` + `Background="#F5F1E3"` をハードコード指定し、`mainWindow.Show()` を廃止。`Dispatcher.BeginInvoke(DispatcherPriority.Render)` でレンダリング準備完了後に `Visibility.Visible` + `Activate()` を一発実行し、完成済みの画面だけが出現する方式に刷新
- **起動プロセスの根本再構築（フリーズ根絶・1〜2秒起動保証）**: UI スレッド I/O を完全排除（`EnsureAppFolders` をバックグラウンド化）。`InitializeAsync` から DB 待機（`await StartupInitTask`）を削除し、DB は履歴表示時に遅延待機。設定パスの `DirectoryExistsSafeAsync` 実在確認を廃止し文字列解決のみに変更（無効パスは `NavigateAsync` で処理）。ツリー読み込みを fire-and-forget 化。設定ファイル読み込みに 1 秒タイムアウト、`InitializeAsync` に 2 秒ウォッチドッグを追加し、超過時はデフォルト設定 / 部分初期化で強制続行

## [0.9.4] - 2026-02-25 : 安定性・信頼性の商用レベル化リファクタリング

### Changed
- **MANUAL.md を読みやすさ・スキャン性重視でリライト**: 全見出しにアプリアイコン対応の絵文字を追加、主要セクション冒頭に要約引用を配置、アプリ設定オプション・ナビゲーション操作・タブ操作・ツリービュー操作・フォルダ構成・ファイル構成等をテーブル形式に変換、長文段落をサブ見出し＋箇条書きに分割、冗長な語尾を簡潔化し全体の視覚的ヒエラルキーを強化
- エラーハンドリング共通化: SafeExecuteHelper 新設、Debug.WriteLine → App.FileLogger 統一
- サービス層に .ConfigureAwait(false) 追加（デッドロック防止）
- NotificationService の ContinueWith を async/await に書き換え
- FavoritesViewModel.TriggerLockWarning を async Task 化
- IsDescendantOf を VisualTreeExtensions に統合（3ファイル重複解消）

### Fixed
- **ファイル一覧のスクロールバー出現時の列ガクつきを解消**: `FileListView` に `ScrollViewer.VerticalScrollBarVisibility="Visible"` を設定し、垂直スクロールバーの表示領域を常に確保。Fluent Design スクロールバー（12px、透明背景）がスクロール不要時はサムを非表示にしつつスペースを維持するため、ファイル数の増減によるカラム幅・位置の変動を完全に防止
- DatabaseService テーブル初期化の未保護例外を修正
- IndexService.InitializeWriter のインデックス破損時クラッシュを防止
- SettingsBackupService.Restore/SetLock のファイルロック時クラッシュを防止
- TabContentControl の _incrementalSearchTimer 停止漏れを修正
- MainWindow の _driveRefreshDebounceTimer 停止漏れを修正
- TabItemViewModel の UpdateDirectoryDiffAsync 例外握りつぶしを修正
- DatabaseService の空 catch ブロックにログ記録を追加

## [0.9.3] - 2026-02-25 : 初期起動デフォルト設定の最適化

### Fixed
- **新規作成後に名前を変更せず確定した場合のフォーカス消失を修正**: `RenameItem` で InputBox に OK を押したが名前が変更されなかった場合（`newName == item.Name`）、即座に `return` していたためフォーカスが消失していた問題を修正。名前未変更でも OK 確定時は `RequestFocusAfterRefresh` + `PastedFileNamesToSelect` を設定して `LoadDirectoryAsync()` を実行し、作成されたアイテムの選択・スクロール・フォーカスを保証
- **OneDrive 等クラウド同期フォルダでの新規作成の堅牢化**: デスクトップやマイドキュメント（OneDrive 同期フォルダ）での新規フォルダ/ファイル作成において、同期ソフトによる一時的な `IOException`（ファイルロック）に対応する指数バックオフ付きリトライを導入。クラウド同期パスでは最大 5 回（500ms 基底）の強化リトライ + 作成後の存在確認ループを実施し、同期完了前の不整合を防止。失敗時は HResult・InnerException 含む詳細ログを `App.FileLogger` に記録し、ユーザーには「同期ソフトがファイルをロックしている可能性があります」の具体的なエラーメッセージを表示。`FileSystemWatcher` に `Error` イベントハンドラを追加し、監視断絶時に自動再セットアップ。クラウド同期パスでは `InternalBufferSize` を 64KB に拡大してイベント欠落を防止し、スロットリング遅延を 1000ms に拡大して「作成→削除→再作成」の同期サイクルを吸収。新規作成の開始から UI 反映までの全ステップに `[DEBUG]` ログを追加し、再発時の追跡を可能に
- **インデックス検索の精度改善（100% 部分一致ヒット）**: `name`（JapaneseAnalyzer）と `name_raw`（ワイルドカード `*keyword*`）のハイブリッド検索を常時実行し、日本語ファイル名の部分一致で漏れが発生しない設計に刷新。複数単語 AND 検索で 0 件の場合は OR 検索に自動フォールバックし候補を必ず提示
- **初回検索時の空振りバグ修正とプログレッシブ検索**: 未インデックスのフォルダで検索した際、インデックス作成を非同期で開始しつつ 2 秒ごとにポーリング再検索して結果を段階的に表示。スキャンバー（IsLoading）で「インデックス作成中」を視覚化し、結果が「パラパラと増えていく」動的な体験を実現
- **検索モード切り替え時の即時検索実行**: 検索バー左端のアイコン（虫眼鏡⇔⚡）クリックで通常検索/インデックス検索を切り替えた際、検索ボックスにテキストがあれば即座に再検索を実行。モード切り替え後の再入力・再 Enter が不要に
- **フィルタ適用後の件数表示の動的更新**: 検索バー右端の「XX items」表示を、フィルターバー（フォルダ、Excel、Word 等）の ON/OFF に連動して更新するよう変更。全 100 件ヒットでも Excel フィルタのみ ON なら「10 items」のように、ユーザーの視界にある件数と一致する表示に改善
- **InputBox（名前の変更ダイアログ）のフォーカス 100% 保証**: 二段構えで根絶。①InputBox 側: `Loaded`・`IsVisibleChanged`・`ContentRendered` の 3 イベント × `Loaded`/`Input`/`ContextIdle`/`ApplicationIdle` の 4 優先度で多段フォーカス。`ShowAsModal()` 直後に `Topmost=true` トリックで OS レベル最前面化し即座に戻す。②呼び出し側: `ApplyFocusAfterRefresh` 先頭に `InputBox.IsOpen` ガードを追加し、FileSystemWatcher 等による 2 回目以降の Refresh でフォーカスが奪われることを物理的に遮断。リネーム予定時は `list.Focus()` を実行せず、`RenameItemCommand` を `DispatcherPriority.Input` で 1 フレーム遅延実行して UI ノイズが収まってからダイアログを表示。`ApplySelectionRestore`・`FocusFirstSelectedItem`・Back ナビゲーション復元・`OnRefreshCompleted` 内復元の全箇所にも `InputBox.IsOpen` ガードを適用
- **新規作成アイテムの自動選択・スクロール保証**: `CreateNewFolder`/`CreateNewTextFile` で fire-and-forget の `Refresh()` を `await LoadDirectoryAsync()` に変更し、MergeItems 完了後に確実にアイテムがリストに存在する状態で `ApplyFocusAfterRefresh` が実行されるよう保証。`ScrollIntoView` の前に `UpdateLayout()` を強制してレイアウト確定後にスクロール。リネームパスでは 1 フレーム遅延後に再 `ScrollIntoView` + InputBox 表示で「画面がスッと動いて新規アイテムがハイライト → 即座にキーボード入力可能」な体験を実現。アイテム未発見時は自動リトライ
- **名前変更後のアイテム追跡・フォーカス復帰**: `RenameItem` を `async Task` 化し、リネーム確定後に新しい名前で `RequestFocusAfterRefresh` + `PastedFileNamesToSelect` を設定して `await LoadDirectoryAsync()` で完了を待機。ソート順が変わっても新名前のアイテムが確実に選択・スクロール・フォーカスされ、Enter でフォルダに入る / Ctrl+C でコピーする等のキーボード操作が即座に継続可能。キャンセル時も元のアイテムにフォーカスを復帰
- **削除後のフォーカス迷子を解消**: `DeleteItems` で削除前に選択アイテムの最小インデックスを保存し、削除完了後に `SelectionIndexToRestore` を設定して `await LoadDirectoryAsync()` で確実にリスト更新を待機。`OnRefreshCompleted` に `SelectionIndexToRestore` チェックを追加し、リフレッシュ完了後に `ApplySelectionRestore` を ContextIdle 優先度で実行。末尾アイテム削除時は `Math.Min` でインデックスをクランプし直前のアイテムにフォーカス。仮想化による ItemContainer 未生成時は Input 優先度でリトライし ScrollIntoView + Focus を保証
- **お気に入り登録の重複チェック・フィードバックを統一**: 重複チェックとフィードバック（MessageBox + ステータスバー通知 + ログ 1 件）を `FavoritesViewModel.NotifyDuplicate` に集約。★ボタン・D&D・コンテキストメニューのすべての登録経路で同一の 3 段フィードバックが発火し、二重ログ出力を解消。パスが空・存在しない場合の★ボタン固有エラーも赤シェイクアニメーション付きで通知
- **超速起動: 企業ネットワーク環境での起動フリーズを解消**: `App.OnStartup` で DB/IndexService の初期化完了を待たずに MainWindow を即座に表示するよう変更。重い初期化はバックグラウンドで fire-and-forget 実行し、`Window_Loaded` 側で必要に応じて await する設計に刷新。どんな環境でも 1 秒以内にウィンドウが表示され操作可能に
- **ネットワークパスのタイムアウト付き存在確認**: `PathHelper.DirectoryExistsSafeAsync` を新設し、ネットワークパス（UNC/マップドドライブ）の `Directory.Exists` を 300ms タイムアウトで打ち切り。`MainViewModel.InitializeAsync`、`FilePaneViewModel.ResolvePathForTabRestoreAsync`、`TabItemViewModel.NavigateAsync`、`TabItemViewModel.LoadDirectoryAsync` の計 4 箇所で使用し、ネットワーク無応答時のフリーズを根絶
- **IndexService の起動遅延実行**: `IndexService.ConfigureIndexUpdate` を起動 2 秒後に遅延実行するよう変更し、起動直後の UI 応答性を改善

### Added
- **多重起動防止（Single Instance）**: `OnStartup` の最初に `Mutex` で二重起動を検知。既にインスタンスが起動中の場合、`Process.GetProcessesByName` で既存プロセスの `MainWindowHandle` を取得し、最小化時は `ShowWindow(SW_RESTORE)` で復元、`SetForegroundWindow` で最前面に表示してから新プロセスを即座に `Shutdown()`。Mutex チェックは設定ファイル・DB 読み込みの前に実行し、ファイル競合を完全に回避。`App_Exit` で Mutex を安全に解放
- **クラウド同期パス判定 (`PathHelper.IsCloudSyncedPath`)**: OneDrive Personal / Business、SharePoint、Box Drive 配下のパスを環境変数およびキーワードベースで判定するメソッドを新設。リトライ強度や監視遅延の分岐に使用
- **ファイル作成リトライヘルパー (`FileIoRetryHelper.CreateDirectoryWithRetryAsync` / `CreateFileWithRetryAsync`)**: Polly ベースの指数バックオフリトライ + 作成後存在確認を組み合わせた非同期メソッドを新設。通常パスは 3 回/200ms、クラウド同期パスは 5 回/500ms の二段構成
- **ドライブ接続・切断の自動検知によるツリービュー更新**: WM_DEVICECHANGE (DBT_DEVICEARRIVAL / DBT_DEVICEREMOVECOMPLETE) をフックし、USB ドライブや SD カード等の抜き差し時にナビペインのツリービューを自動更新。600ms デバウンスで連続通知を集約し、バックグラウンドでのドライブ情報取得＋差分更新（Add/Remove のみ）により超低負荷で動作
- **タブ D&D をモダンブラウザ風に抜本改善**: `TabControlDragDropBehavior` を全面改修。DockPanel の `TabHeader_DragOver`/`TabHeader_Drop` がタブ D&D イベントを横取りしていたバグを修正し、全イベントを TabControl Behavior に一元化。TabPanel 内の各 TabItem 境界を走査しマウス X 座標と中心点を比較する精密な挿入インデックス計算 (`CalcInsertionInfo`) を実装。同一ペイン内では `ObservableCollection.Move` で確実にコレクションを更新し、クロスペイン移動では `Insert(targetIndex)` で指定位置に挿入。ドラッグ閾値を 10px に拡大し誤操作を防止。全コレクション操作を Dispatcher 経由に変更。ヘッダー領域 (36px) 判定により、コンテンツ領域では同一ペイン D&D をキャンセル、クロスペインは末尾追加として動作
- **タブ D&D ゴーストイメージ＋半透明化**: ドラッグ中にタブ名を表示する `DragAdorner` をカーソル追従で表示。同時にドラッグ元タブを Opacity 0.4 に半透明化し Chrome 風の視覚フィードバックを実現。`GiveFeedback` イベントで位置更新、`finally` ブロックで確実に復元
- **TabPanel レベル挿入インジケーター**: 従来の TabItem 個別アドーナーを廃止し、TabPanel 上に X 座標指定で青い縦線＋丸ドットを描画する `TabPanelInsertionAdorner` に刷新。120ms CubicEase フェードインアニメーション付き。位置更新時は `InvalidateVisual` で即座に再描画し、子要素間遷移による DragLeave フリッカーをバウンドチェックで防止
- **タブ移動の監査ログ**: `[Tab] Moved:` / `[Tab] Reordered:` 形式で FileLogger に記録

### Changed
- **ローディング表示をアプリ標準インジケーターに統一**: ペイン内の独自ローディングオーバーレイ（`IsTabLoading` + 回転アニメーション）と全画面ローディングオーバーレイ（`LoadingOverlay` コントロール：半透明背景 + 操作ブロック）を廃止。フォルダ読み込み・検索時はステータスバー最上部のスキャンバー（`IsLoading` 連動、水平走査ライン）、ドライブ自動更新時はスピナー（`BeginBusy`）を使用するよう変更。画面フラッシュを排除し表示の一貫性を向上
- **`PathHelper.IsNetworkPath` の共通化**: `IndexService` のプライベート `IsNetworkPath` を `PathHelper.IsNetworkPath` へ移設・統合し、コードの重複を解消
- **初期起動デフォルト設定の最適化**: `settings.json` が存在しない初回起動時の体験を改善。ウィンドウサイズを 1400×850 に縮小し画面中央に配置（ハードコード座標を廃止し、`NormalizeWindowPlacement` で NaN 検出時にプライマリスクリーン中央へ自動配置）。ファイル一覧のソートデフォルトを名前昇順に統一（`PaneSettings.SortProperty` を `LastModified` → `Name`、`SortDirection` を `Descending` → `Ascending` に変更し、`TabItemViewModel` の実デフォルトとの乖離を解消）。サイドバー幅を 330px → 260px に縮小し、縮小されたウィンドウでのファイル一覧の作業領域を確保

## [0.9.2] - 2026-02-23 : ステータスバー表示精度向上と起動演出の洗練

### Fixed
- **ステータスバー表示精度向上（起動演出時のガタつき解消）**: ステータスバーのコンテンツ領域の高さを 28px に固定し、内部の `StackPanel` を `Grid` へ置換。表示切替時のレイアウト再計算による「枠の上下移動」を完全に排除。
- **起動アニメーションの初期配置ずれの修正**: アニメーション開始直前に `UpdateLayout()` を強制実行し、初期オフセットを調整することで、演出開始時に文字が1段下がって表示される問題を解決。
- **ステータスバー表示重複バグの物理的排除**: 起動ウェルカムアニメーション中に通常のステータス情報（アイテム数や通知メッセージ）が重なって表示される問題を修正。起動から30秒間は `NotificationService.Notify` による書き込みを物理的にバイパス（完全に Return）し、一切の書き込みを禁止。また、ウェルカムパネル表示中は通常用 TextBlock の `Visibility` を `Collapsed` に設定して物理的に排除。

### Changed
- **起動演出の維持時間延長とクロスフェード復帰**: 起動時の「Ready...Complete」メッセージを5秒間維持し、その後に通常のステータス表示へ1秒かけてゆっくりとクロスフェードするように変更。メッセージ色を通常のステータス色（#90A4AE）に統一し、シームレスな移行を実現。
- **ステータスバーのカラーパレット洗練（脱・蛍光色）**: 従来の蛍光色（Cyan等）を廃止し、ダークテーマに馴染むプロフェッショナルなブルーへ変更。起動メッセージ（`#4FC3F7` Light Sky Blue）、通常ステータス文字（`#90A4AE` Blue Grey）に調整。グロウバーの発光を邪魔しない上品な彩度へ刷新。
- **起動ウェルカム演出のブラッシュアップ**: 退場時の演出を Opacity 100%→0%、TranslateY 0→-10 (上方スライド) に変更し、所要時間を 0.5s に調整。退場完了後に通常用 TextBlock を `Visibility="Visible"` に戻し、より洗練されたユーザー体験を実現。
- **起動ウェルカムアニメーション（Fade-In & Slide-Up、コードビハインド一元制御）**: ステータスバー中央に専用の `WelcomePanel`（StackPanel + TranslateTransform）を配置。アニメーションは `StartWelcomeAnimation()` メソッドで管理し、XAML `EventTrigger` は持たない。`_welcomeStarted` フラグで二重発火を防止。`Window_Loaded` 完了後（`InitializeAsync` 完了・フォーカス設定後）にアニメーションを開始し、① 登場 0.6s（Opacity 0→1・TranslateY 10→0・PowerEase(2) EaseOut）② 静止 2.4s ③ 退場 0.5s（Opacity 1→0・TranslateY 0→-10・PowerEase(2) EaseIn）の3フェーズを `Storyboard.Begin(this, isControllable:true)` で実行。左側アイテム数エリアを `x:Name="StatusInfoGroup"` でラップし初期 `Opacity="0"` に設定。旧実装の `App.Notification.Notify("Welcome to Zenith Filer...")` 呼び出しを削除し二重表示問題を根絶。隠し演出として「Turbo Engine Ready」に DropShadowEffect ゆらぎ発光（0.8〜1.6s）を追加。
- **起動メッセージの最優先表示（30秒間の通知ブロック）およびステータスバーのブルー統一デザインを実装**: コンストラクターで `_startupPhaseEnd = DateTime.UtcNow.AddSeconds(30)` を設定し、起動後30秒間はアイテム数エリア（`StatusInfoGroup`）を非表示に固定。ウェルカムアニメーション完了後（約3.5秒）に `DispatcherTimer` で残り時間を計算し、30秒経過後に `FadeInStatusInfoGroup()`（0.6s PowerEase フェードイン）で `StatusInfoGroup` を表示。退場アニメーションに TranslateY 0→-10 の上昇スライドを追加し、フェードアウトと同時にパネルが上方へ消えるリッチな退場演出を実現。カラーパレットをブルー系に統一：`WelcomePanel`「Welcome to Zenith Filer」= `#4FC3F7`（LightSkyBlue）、通常ステータステキスト（`StatusInfoGroup`）= `#90A4AE`（BlueGrey）。

## [0.9.1] - 2026-02-23 : 設定永続化修復・PDF対象絞り込み

### Added
- **PDF変換機能の拡張（保存先を同一フォルダへ変更・不要な UI 除去）**: PDF 変換の保存先を「反対ペインの現在フォルダ」から「操作しているカレントフォルダ（同一フォルダ）」に変更。PDF 変換中に不要な全画面ローディングオーバーレイ（`IsLoading` / スキャンバー）が表示されていた問題を修正し、GlowBar＋スピナーのみに整理。右クリックメニューの文言を「同フォルダへ保存」に統一
- **PDF変換機能の追加（画像/Office文書→PDF、反対ペインへ保存、複数ファイルは Combined_日時.pdf に結合）**: ファイル一覧の右クリックメニューから、選択した画像（`.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`）または Office 文書（`.docx`, `.xlsx`, `.pptx` など）を PDF に変換して反対ペインの現在フォルダへ保存する機能を追加。単一ファイルは「元のファイル名.pdf」で保存。複数ファイルは各ファイルを個別に PDF へ変換後、PDFsharp でページ結合して「Combined_yyyyMMddHHmmss.pdf」として保存。画像変換は PDFsharp 6.x、Office 変換は dynamic COM（Word.Application / Excel.Application / PowerPoint.Application）を使用。Office 未インストール時はダイアログで通知。変換中はグロウバー・スキャンバー・スピナーで進捗を表示する
- **CSV出力時のデフォルト保存先を、対象フォルダ自身に変更して利便性を向上**: `ExportSubtreeToCsvAsync` の `SaveFileDialog` に `InitialDirectory = folderPath` を追加。ダイアログを開いた際に対象フォルダが初期表示されるため、「保存」を押すだけで同階層に CSV を生成できる
- **同一フォルダコピーおよび衝突時の自動リネーム機能（エクスプローラー互換）を実装**: `GetUniquePath()` ヘルパーを追加し、コピー/移動先に同名ファイル・フォルダが存在する場合にエラーなく「ファイル名 - コピー.ext」→「ファイル名 - コピー (2).ext」と連番でリネームして保存する。同一フォルダ内コピー（Ctrl+C → Ctrl+V）も同ロジックで吸収し、エラーダイアログなしに複製を作成。移動時も衝突があれば同形式でリネーム。貼り付け後のフォーカス選択は自動リネーム後の実際のファイル名（`actualDestNames`）を使用するよう改善。フォルダは拡張子なしで「フォルダ名 - コピー」形式
- **Windows 自然順ソートの導入、およびベースファイルを優先するヒューマン・ソート・ロジックの実装**: `FileItemComparer`（`IComparer`）クラスを新設し、`ListCollectionView.CustomSort` に適用。`StrCmpLogicalW`（shlwapi.dll）による自然順ソートで「file2 が file10 より前」に表示されるよう改善。名前ソート時は「ベースファイル優先」判定を追加し、同一ベース名（拡張子除く）＋区切り文字（`_` `-` ` ` `.`）＋接尾語のバリアントファイルよりベースファイルが直上に固定される。ベース優先はソート方向（昇順/降順）に依存しないため、方向切り替え後もベースが変異体の直上に留まる。フォルダ先頭グループ（IsGroupFoldersFirst）はコンパレーター内に内包
- **進捗表示インジケーターの命名規則を制定（スピナー／スキャンバー／グロウバー）**: アプリ内の処理中インジケーターを3種類に分類・命名。①**スピナー** = `IsGeneralBusy` が真の間、ステータスバー右側に表示される回転アニメーション。CSV 出力スキャンや検索など所要時間が不定の一般的な非同期処理に使用。②**スキャンバー** = `IsLoading` が真の間、ステータスバー最上部に表示される横伸びライン。`LoadingMessage` でメッセージを伴い、完了時間が読めない大規模スキャン処理で使用。スピナーと同時表示される場合がある。③**グロウバー** = `IsFileOperationActive` が真の間、ステータスバー最上部に表示されるバイト数ベースの発光プログレスバー（`AccentBrush` + Cyan グロー）。キャンセル時は 0.3s の逆行アニメーション（`CubicEase.EaseIn`）で 0% へ「吸い込まれ」演出の後にフェードアウト

### Fixed
- **アプリ状態永続化: 起動時タブ復元バグ修正**: `Window_Loaded` に設定読み込みを統合し、常に実設定で `InitializeAsync` を呼ぶよう変更。従来は `MainWindow_Loaded`（コード登録）が `Window_Loaded`（XAML登録）より後に実行されるため、デフォルト設定でタブが初期化されていた問題を修正
- **アプリ状態永続化: 終了時ワーキングセット消失バグ修正**: `MainWindow_Closing` に `WorkingSets = vm?.ProjectSets.Items.ToList()` を追加。従来は終了時に `WorkingSets` が空配列で上書きされていた問題を修正
- **設定保存/読み込み失敗時の詳細エラーを `App.FileLogger` へ出力**: `WindowSettings.Save()` のcatchブロック・`Window_Loaded` の設定読み込み失敗・`MainWindow_Closing` の保存失敗にそれぞれログ出力を追加
- **PDF変換: GlowBar の表示タイミング改善**: `BeginFileOperation` 呼び出し直後に `Dispatcher.InvokeAsync(DispatcherPriority.Background)` を挿入し、WPF のデータバインディング（priority 8）・レンダリング（priority 7）サイクルが完了してから `Task.Run` へ移行するよう修正。GlowBar が重い処理の開始前に確実に描画される

### Changed
- **PDF変換: テキスト系ファイルを対象から除外**: PDFsharp によるテキスト→PDF変換機能（`.txt`, `.log`, `.md`, `.csv`, `.json` 等）を廃止。PDF変換の対象を**画像**（`.jpg`, `.png` 等）と **Office 文書**（`.docx`, `.xlsx`, `.pptx` 等）のみに絞り込み、関連コード（`TextFileExts`, `IsTextFile`, `CreatePdfFromTextFile`, `WindowsJapaneseFontResolver`, `Initialize` 等）を削除してコードをシンプルに保つ

### Changed
- **CSV出力を強制物理スキャン方式に変更（Lucene インデックス非依存化）**: `ExportSubtreeToCsvAsync` をインデックス経由取得から `DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)` による直接ファイルシステムスキャンに変更。`App.IndexService.IsPathIndexed` / `AddDirectoryToIndexAsync` / `Search` の呼び出しを完全に除去。スキャン中は**スキャンバー**（`IsLoading=true`、`LoadingMessage="[CSV出力準備中] フォルダの最新情報を物理スキャンしています..."`）と**スピナー**（`BeginBusy()`）を同時表示し、インデックスの有無・鮮度に依存しない常に最新のファイルシステム状態を CSV に反映する。タイムスタンプは `FileInfo.LastWriteTime` から直接取得

## [0.9.0] - 2026-02-22 : Zenith Turbo Engine・空間認識バー・高速転送 UX 刷新

### Added
- **空間認識プログレスバー**: ファイル転送の物理方向にバーが追従する。左→右転送（AペインからBペイン）は `LeftToRight`、右→左転送（BペインからAペイン）は `RightToLeft` で `GlowProgressBar.FlowDirection` を切り替え、視覚と操作の方向を一致させる。外部ドロップ等の方向不明操作は `LeftToRight` にフォールバック。`MainViewModel.ProgressFlowDirection` プロパティを追加し、`BeginFileOperation(flowDirection:)` で操作開始時に設定
- **キャンセル逆行（リトラクション）アニメーション**: キャンセルボタン押下時、グロウバーが現在位置から 0% へ 0.3s で急加速しながら戻る「吸い込まれる」演出を実装。`CancelRetractionRequested` イベント経由でコードビハインドの `Vm_CancelRetractionRequested` が `CubicEase.EaseIn` の `DoubleAnimation` を起動し、アニメーション完了後（350ms）にバーがフェードアウト。正常完了時（100% → フェードアウト）と完全に分岐管理し、演出が競合しない設計
- **Zenith Turbo Engine**: ファイルコピーエンジンを超高速実装に刷新（32 MB バッファ `FileStream` + `FileOptions.Asynchronous | SequentialScan` による完全非同期 I/O、`Parallel.ForEachAsync` で最大 3 並列コピー、コピー完了後に作成日時・更新日時・属性を元ファイルから確実に復元）
- **モダン・グロウ・バー**: ステータスバー上端に 2px 超極細プログレスバーを追加（`AccentBrush` + Cyan `DropShadowEffect` (BlurRadius=8) で淡く発光、`DoubleAnimation` (0.2s `CubicEase`) で滑らかに補間、操作中フェードイン・完了後フェードアウト）
- **キャンセルボタン**: ファイル操作中のみステータスバー右端に「×」ボタンを表示。`CancellationToken` 経由で物理書き込みを即時停止し、不完全な中間ファイルを自動削除
- **操作ステータス表示**: コピー/移動中はステータスバー中央に「[コピー中] ○○% [ファイル名]」を 200ms スロットルで更新表示。完了後は通常の通知メッセージ表示に復帰

### Changed
- **FileSystemWatcher をデバウンス(800ms)からスロットル(500ms)に変更**: `OnFileSystemChanged` のリフレッシュ制御を「毎イベントでタイマーリセット（デバウンス）」から「最初のイベントで 500ms 後の Refresh をスケジュールし、ウィンドウ内の後続イベントは無視（スロットル）」に変更。大量ファイルのコピー中でも約 500ms ごとにコピー先一覧が段階的に更新される。Dispose 時のキャンセル連携はそのまま維持

### Fixed
- **大規模転送時の低負荷・高効率進捗管理システムの導入**: コピーチャンクを 32MB 固定バッファから `ArrayPool<byte>.Shared`（4MB・再利用）に切り替え、GC 圧力をほぼゼロに削減（3 並列時のバッファ使用量 192MB → 24MB）。`TurboCopyFileAsync` に `onBytesWritten` バイト単位コールバックを追加し、10GB 超の単一ファイル転送でも 4MB 書き込みごとにプログレスバーが更新されるよう変更。`DropFilesInternal` でコピー前に `Task.Run` で総バイト数を非同期計算し、10 件ごとに「[準備中] N/M 件スキャン済み」を表示して転送開始まで「動いている感」を維持。スロットリング閾値を「200ms かつ 1% 変化」に引き締め、描画 CPU 消費を更に抑制。`MaxDegreeOfParallelism` を `Math.Clamp(ProcessorCount / 2, 1, 3)` に変更してコア数に追従し、HDD でも SSD でもディスクシーク過負荷を防ぐ
- **大規模ファイル転送時における UI 更新の安定化とスロットリング処理の導入**: `ReportFileOperationProgress` のスロットル条件を「前回報告から 100ms 経過」または「進捗率 0.5% 以上変化」の複合判定に改良。`_lastProgressReportTick` / `_lastReportedProgressBits` を `Interlocked` + CAS で原子的に管理し、800 件超・17GB 級の大量転送時でもスレッドプールが UI スレッドを圧迫しない設計に。`InvokeAsync` に `DispatcherPriority.Background` を設定してファイル転送速度を優先し、UI 更新を隙間時間に滑り込ませる。`EndFileOperation` も `DispatcherPriority.Normal` + `try-catch/finally` で保護。コピー開始直後に「[準備中] N 件のファイルを...」メッセージをグロウバーと同時に即時表示（`physicalFiles` 件数確定後に `BeginFileOperation` を呼ぶ構造に変更）。進捗報告呼び出し部分を `try-catch` でラップし、報告失敗がコピー処理を中断しないよう保護
- **スレッドアクセス違反の修正・ファイル転送機能の安定化**: `Parallel.ForEachAsync` 内のスレッドプールスレッドから `MainVM` プロパティ（`MainWindow.DataContext` へのアクセスを含む）を呼び出していた問題を修正。UIスレッドで参照を `mainVmRef` にキャプチャしてからラムダに渡すよう変更。`Vm_PropertyChanged_GlowBar` に `Dispatcher.CheckAccess()` ガードを追加し、非UIスレッドから `PropertyChanged` が発火した場合も安全にマーシャリング。これによりコピー開始時のグロウバー表示と、処理完遂の両方を復旧
- **スピナー不整合の解消**: `IsGeneralBusy = IsBusy && !IsFileOperationActive` を導入し、ファイル操作中はスピナーを非表示にしてグロウバーに一本化。`finally` ブロックで `EndFileOperation` を確実に呼び出し、操作完了後にスピナーとプログレスバーの両方が必ず消えることを保証
- 不安定な独自コピーエンジンおよび関連 UI を一括削除し、機能をリセット（試験的に導入した `ShellFileOperations` サイレントコピーエンジン・`CancellationToken`/`cancelReg`/`_activeShellOp` による複合非同期制御・`MainViewModel` のファイル転送ステートマシン一式・ステータスバーの進捗 StackPanel をすべて削除。`DropFilesInternal` を `File.Copy` / `File.Move` / `Directory.Move` + 再帰ヘルパー `CopyDirectoryRecursive` のみを使うシンプルな BCL 実装に置換し、Undo 登録・例外ダイアログ・Refresh を維持）
- 不正な領域（背景・ウィンドウ端など）へのドラッグ＆ドロップ時に発生する COMException (0x80070490) の予防措置を追加（`DropFilesInternal` 先頭の `Directory.Exists` 事前チェックでサイレントスキップ）

## [0.8.0] - 2026-02-22 : 検索 UX 強化・ソート分離・フォルダ CSV 出力

### Added
- 検索結果 0 件時のユーザー通知を強化（ファイルリスト中央に「該当する項目が見つかりませんでした」Watermark を表示。件数テキストが 0 件の場合はアンバー色＋警告アイコンで強調）
- 通常フォルダ表示のデフォルトソートを名前順（昇順）に、検索結果のデフォルトソートを更新日時（降順）に分離。ビュー切り替え時にカラムヘッダー矢印が正しく同期する（`ActiveSortProperty` / `ActiveSortDirection` 導入）
- 独自コンテキストメニューに「このフォルダ以下の一覧をCSV出力」機能を追加（インデックス連携）。背景右クリック（現在フォルダ）＋フォルダ単体右クリックの両方に対応。Lucene インデックスから全ファイルを取得し、未登録の場合は一時スキャンを実行。出力列：項番・ファイル名・フルパス・タイムスタンプ・サイズ（バイト）、UTF-8 BOM 付き、最大 100,000 件

### Changed
- `MANUAL.md` のワーキングセットセクションを詳細化（プレビュー・ロールバック仕様、ガイドテキスト、管理操作を明記）
- ローディングアニメーションをモダンな横伸びラインに刷新（円形スピナーを廃止し、チャコールブラック半透明背景＋水平プログレスラインに変更。300ms 以内の処理では視覚表示をスキップしフリッカーを防止）

### Fixed
- 検索演出アニメーションの完全削除、および検索解除ボタンの挙動を通常表示復帰に統一（`IsSearchSucceeded`・`IsSearchResultEmpty` プロパティ、演出 Storyboard・GlowBorder 要素、`TriggerSearch*Async` メソッドをすべて削除。`ClearSearch` を `IsSearchResultTab=false` → `SearchText=""` → `IsSearching=false` → `NavigateAsync(CurrentPath)` の共通フローに一本化し `CloseTabCommand` 呼び出しを廃止）
- 検索バーの「×」ボタンの挙動を、タブ削除ではなく「検索解除・通常表示への復帰」に統一（タブ枚数・検索挙動設定に関わらず `SearchText` クリア → `IsSearchResultTab=false` → フォルダ再読込を共通実行。`CloseTabCommand` の呼び出しを廃止）
- 検索件数 StackPanel 内の `PackIconLucide Kind="AlertCircle"` が無効な値であったため XAML 解析エラーが発生していた問題を修正（正しい Lucide アイコン名 `CircleAlert` に変更）
- 新規検索タブ生成時に Success Sweep 演出が空振りしていた問題を修正（`Dispatcher.InvokeAsync(DispatcherPriority.Background)` で `IsSearchSucceeded = true` を Loaded イベント完了後まで遅延。`TabContentControl.Loaded` ハンドラにフォールバックを追加）
- 検索演出をペイン外周のモダンなグローエフェクトに刷新し、新規タブ生成時の動作を確実化（水平ラインを廃止し、ペイン境界が白く鋭く光る Success Glow と 2 回点滅するアンバー Empty Glow に変更。`HasFreshSearchSuccess` / `HasFreshSearchEmpty` の 2 秒タイムスタンプにより、同一ペイン新規タブ・反対ペイン新規タブ含む全パターンで演出を確実に発火）
- 不安定な検索演出アニメーション（GlowBorder・Storyboard・演出フラグ類）を削除し、実用的な 0 件メッセージ表示のみを維持（`ShowEmptySearchResult` バインディング・Watermark・件数アンバー強調は存続）
- 同一タブ検索時に `SearchResults.Clear()` 直後のゼロ件瞬間に Watermark・アンバー強調が一瞬フラッシュしていた問題を修正（`IsSearchRunning` フラグを導入し、検索実行中（デバウンス含む）は `ShowEmptySearchResult` を強制 false に。キャンセル済みの旧検索からのフラグリセットも防止）

## [0.7.4] - 2026-02-21 : 非同期処理・IsLoading 品質監査修正

### Added
- **ワーキングセット（Ctrl+Shift+5）**: A/B ペインの全タブ状態（パス・ロック・表示モード・ソート）を名前付きで保存・復元できる新ビューをナビペインに追加。読み込み時は即座にプレビュー状態となり、「適用」で確定、「元に戻す」または他ビューへの切替で自動ロールバック。データは settings.json に永続化

### Changed
- **ワーキングセットビューの UI 改善**: ビュー最上部に機能説明テキストを追加。プレビューフッターの背景色をチャコールブラック（#545B64）に刷新し、上端に境界線・ボタンホバーに暗色トーン（#4A5059）を適用して視認性と操作感を向上

### Fixed
- `CreateNewFolder` / `CreateNewTextFile` 実行中に `BeginBusy` が呼ばれておらずローディングオーバーレイが表示されなかった問題を修正（`MainVM?.BeginBusy()` を `try` ブロック先頭に追加）
- `RestoreTabsAsync` 内の `NavigateAsync` 呼び出しが fire-and-forget になっており例外が握りつぶされていた問題を修正（`await` に変更、メソッドを `async Task` 化）
- `BoxDriveService.StartSharedLinkClipboardMonitor` が `Dispatcher.Invoke`（同期）を使用しており呼び出しスレッドをブロックしていた問題を修正（`InvokeAsync` に変更）
- `SettingsBackupService` の I/O が UI スレッドをブロックする可能性があった問題を修正（非同期ラッパー `CreateBackupAsync` / `CleanupOldBackupsAsync` を追加）
- `AppSettingsViewModel.BackupNow` を `AsyncRelayCommand` に変更し、手動バックアップ中の UI ブロックを解消

## [0.7.3] - 2026-02-21 : 「名前の変更」ダイアログ高速化

### Fixed
- **「名前の変更」ダイアログの起動遅延・白フラッシュを修正**: `ShowDialog()` でウィンドウを毎回破棄していたキャッシュ不具合を解消。`DispatcherFrame` によるカスタムモーダルループに切り替え、完了後は `Hide()` のみでウィンドウを再利用する。`EnsureHandle()` + `UpdateLayout()` をコンストラクタで呼んで HWND を事前生成し、初回表示は `Opacity=0 → ContentRendered → Opacity=1` で白フラッシュを防止。フォーカスは `IsVisibleChanged` から `DispatcherPriority.Input`（描画完了後）で確実に設定

## [0.7.2] - 2026-02-21 : お気に入り D&D のスナップショット＆ロールバック

### Fixed
- **お気に入り整理時のデータ消失防止（トランザクション処理の導入）**: D&D 移動の冒頭でスナップショットを取得し、設定ファイル保存の成否を確認してから確定する方式に変更。例外または保存失敗時はリストを自動ロールバックし、消失ゼロを保証。監査ログに Move Started / Move Completed / ERROR を記録

## [0.7.1] - 2026-02-21 : 監査ログへの刷新

### Changed
- **監査ログへの刷新**: `LoadDirectoryAsync` 内の内部トレース（`[Refresh]` プレフィックスの約15行）を廃止。代わりに「いつ・どのペインで・何をしたか」が一目でわかる監査ログを実装。フォルダ移動（`[A] Open:`）、検索（`[A] Search:`）、コピー/移動（`[A] Copy/Move (n):`）、削除（`[A] Delete (n):`）、リネーム（`[A] Rename:`）、新規フォルダ/ファイル作成（`NewFolder:`/`NewFile:`）、お気に入り操作（`[Favorites] Add/Remove:`）、設定保存（`[Settings] Saved`）、設定リカバリ（`[Settings] Recovery:`）をログ出力。エラーは `[ERR]` プレフィックスで統一

## [0.7.0] - 2026-02-21 : 設定バックアップ・リカバリ・新規ファイル作成

### Added
- **新しいテキストファイルをコンテキストメニューから作成**: ファイルペインの背景右クリックメニューに「新しいテキストファイル」を追加。作成直後にリネームダイアログを自動表示し、キャンセルした場合は作成ファイルを自動削除して元の状態に戻す（Ctrl+Z でのアンドゥにも対応）
- **設定バックアップ・リカバリシステム**: アプリ設定画面に「4. 設定ファイルリカバリ」セクションを追加。アプリ終了時に自動バックアップ（`backups/settings_yyyyMMdd_HHmmss.json`）を作成し、辞書ベースの差分要約（例：「Aペインホーム、検索挙動を更新」）を `.desc` ファイルに保存。「今すぐバックアップ」ボタンで手動バックアップも可能。「復元...」ボタンでバックアップ一覧ダイアログを開き、ダブルクリックで選択した時点の設定に復元（再起動を提案）。右クリックでロック設定が可能で、ロックしたバックアップは 30 日保持ポリシーの自動削除から除外される

### Changed
- **新規フォルダ作成の UX を改善（Windows Explorer 互換）**: コンテキストメニュー・Ctrl+Shift+N でフォルダを作成した際、事前にダイアログで名前を聞く代わりに「新しいフォルダ」として即時作成し、直後にリネームダイアログを自動表示するように変更。キャンセルした場合は作成したフォルダを自動削除して元の状態に戻す
- **設定バックアップのタイミングを最適化**: 従来はお気に入り更新・フィルター変更など `Save()` のたびに毎回バックアップを生成していたが、アプリ終了時と手動ボタン押下時のみに変更。不要なバックアップの増加を抑制

### Fixed
- **新規作成後のリネームキャンセルで作成アイテムが残る問題を修正**: `_isRenameForNewItem` フラグで新規作成フローを確実に識別するよう変更し、パス文字列の大文字小文字比較が失敗するケースを解消。キャンセル時のロールバック対象をフォルダ・ファイルの両方に対応

## [0.6.3] - 2026-02-20 : インクリメンタルサーチ・お気に入り検索修正

### Added
- **ファイルペインのインクリメンタルサーチ**: 詳細ビューのファイルリストでアルファベット・数字キーを押すと、その文字で始まるファイル/フォルダへ即座に選択を移動。同一キー連打で同頭文字の候補間をサイクル、500ms 以内に異なる文字を続けて打つとプレフィックス検索でジャンプ

### Fixed
- **お気に入りビュー: 検索クリア後に選択が消える問題を修正**: 検索結果でアイテムを選択後にクリアすると、ツリービューに戻った際そのアイテムへ選択が復元されスクロールするようになった。ネストされたフォルダ内のアイテムについても祖先ノードを自動展開して可視化する

## [0.6.2] - 2026-02-20 : マニュアル仕様同期

### Changed
- **MANUAL.md の仕様同期（ソースコード全体との整合性チェック）**: 以下の記述を最新の実装に合わせて修正・追記した
  - **スプラッシュ画面 (1-0)**: スプラッシュは「アプリ起動時」ではなく「初回起動時のみ」表示される旨に修正。2回目以降は `settings.json` の存在確認でスキップし爆速起動する動作を明記
  - **タブ右クリックメニュー (2-2)**: タブ右クリックで表示される「反対ペインに移動」メニュー項目の説明を追加
  - **アプリ設定・検索 UI (1-3 / 3-3-1-1)**: 「検索時に自動的に1画面モードに切り替える」オプション（`AutoSwitchToSinglePaneOnSearch`）の説明を追加。検索結果タブを全て閉じると元のペイン数に自動復元される動作も明記
  - **ナビペイン幅の自動拡張 (1-3)**: インデックス検索設定・アプリ設定への切替時もナビペインがアニメーションで拡張される動作を追記（ツリー・参照履歴と同様の挙動）

## [0.6.1] - 2026-02-20 : ZenithDocViewer発行エラー修正

### Fixed
- **ZenithDocViewer の Release 発行エラーを解消**: `RuntimeIdentifiers` を追加して NETSDK1047 を解消し、`PublishReadyToRun` を無効化して NETSDK1094 を回避。Release 発行時に ZenithDocViewer が単一 EXE として正しく同梱されるようにした

## [0.6.0] - 2026-02-20 : マニュアル別アプリ化とSuccessHighlight

### Added
- **マニュアル表示の別アプリ化（ZenithDocViewer）**: ドキュメント閲覧専用の独立アプリ `ZenithDocViewer.exe` を新設。本体ビルド時に自動で同梱され、同一フォルダに `ZenithFiler.exe` と `ZenithDocViewer.exe` が並ぶ。DB やインデックスを読み込まないため爆速起動で、アプリ起動前にマニュアルや更新履歴をさっと確認できる。共有プロジェクト（ZenithFiler.Shared.Docs）で UI とロジックを共通化し、本体内ヘルプと別アプリの両方に自動反映される疎結合な構造を確立
- **登録後の自動遷移とSuccessHighlight演出**: 「お気に入りに追加」（ツールバー★ボタン・コンテキストメニュー）実行時、お気に入りビューへ自動切り替えし、追加された項目を選択・スクロールして表示。同時にチャコールブラック（#545B64）の背景フラッシュとパルス効果で一時的に強調表示する SuccessHighlight アニメーションを1回発火
- **インデックス検索登録時の自動遷移とSuccessHighlight演出**: コンテキストメニュー「インデックス検索でこのフォルダを検索」実行時、未登録なら IndexService に登録後、インデックス検索設定ビューへ自動切り替えし、該当フォルダを選択・スクロールして SuccessHighlight で強調表示

### Changed

### Removed

### Fixed

## [0.5.19] - 2026-02-20 : スクロールバー誤発火修正とインデックス・起動改善

### Added
- **インデックス登録確認機能**: インデックス検索設定ビューにおいて、登録フォルダの右クリックメニューに「インデックス登録確認」項目を追加。選択すると、A ペインに「結果確認：（フォルダ名）」という名前の検索結果タブが開き、そのフォルダのインデックス登録内容（ファイル・フォルダ）が自動で検索・一覧表示されます。インデックスに登録されているデータのみをソースとして、実際のファイルシステムスキャンを行わずに即座に確認できます。検索結果ビュー（SearchGridView）の仕組みを再利用し、名前、場所アイコン、パス、更新日時、サイズを表示します。ロード完了時にステータスバーで「（フォルダ名）のインデックス内容を表示しました」と通知されます。

### Changed
- **インデックス登録確認の自動実行とUI改善**: タブ作成直後に検索を自動実行するよう変更（空タブのままになる不具合を解消）。タブタイトルを「結果確認：（フォルダ名）」に統一し、検索結果ロード完了時にステータスバーで「（フォルダ名）のインデックス内容を表示しました」と通知するようにした
- **Box 連携メニューの整理**: 独自メニューから「BOXパス＋実アドレスをコピー」項目を削除。実アドレス（URL）の取得は、エクスプローラの「共有」→「共有リンクをコピー」実行時のクリップボード監視（AddClipboardFormatListener）による自動整形に一本化し、安定動作を維持
- タブの幅を 120px から 150px に拡大し、長いファイル名でも視認性を向上させるとともに、閉じるボタン周辺の操作スペースをよりゆったりと確保
- **スプラッシュ画面の初回起動時限定表示**: `settings.json` の存在有無で初回起動を判定し、初回起動時のみスプラッシュ画面（`splash.png`）を表示するように変更。2回目以降の起動ではスプラッシュを完全にスキップし、メインウィンドウを即座に表示することで「爆速起動」を実現。初回起動時は「漆黒のカード」のスプラッシュが表示され、初期化完了後に 0.5秒 かけてフェードアウトしてメイン画面へ遷移する。判定は `AppDomain.CurrentDomain.BaseDirectory` 内の `settings.json` の存在確認で行い、ポータブル性を維持
- **起動シーケンスの最適化**: ネイティブスプラッシュ表示中に `DatabaseService.InitializeAsync()`、`IndexService` の設定、`PathHelper.EnsureSpecialFoldersCached()` をバックグラウンドで並列実行し、準備完了後にスプラッシュをフェードアウトさせながら `MainWindow` を表示することで起動時のフリーズを解消

### Removed
- **SplashWindow の完全削除**: 初回起動用の動的スプラッシュウィンドウ（`SplashWindow.xaml` / `SplashWindow.xaml.cs`）を削除。ネイティブスプラッシュに統一することで、起動シーケンスを簡素化し、コードベースをクリーンに保った
- **レガシー移行ロジックの完全削除**: `WindowSettings.TryMigrateAndSave()` から旧バージョン（SettingsVersion < 1）へのマイグレーション処理を削除。現在は SettingsVersion = 1 のみをサポートし、AppData や favorites.json への依存を完全に排除したポータブル性重視の構造に純化

### Fixed
- **インデックス登録確認で結果が表示されない問題を修正**: 「インデックス登録確認」選択時に空クエリで検索が実行されず A ペインに一覧が出ない不具合を修正。検索結果タブで空キーワードかつインデックス検索モード・パス指定の場合は全件検索を実行するよう `ExecuteSearch` の早期 return 条件を調整
- タブの閉じるボタン（×）をホバーした際、円形のハイライトがタブの右端境界線にぶつかって右側が欠けてしまう問題を、ボタン配置の余白（Margin）と描画範囲（MinWidth）の確保、および ClipToBounds="False" の徹底により修正し、綺麗な真円のフィードバックが表示されるようにした
- **Box ドライブ内のフォルダが白い汎用アイコンになる問題を修正**: ツリービュー（ナビペイン）において、Box ドライブ内のフォルダが白い紙のような汎用アイコンではなく、通常のフォルダアイコンで表示されるように修正。`PathHelper` に `WarmUpBoxPath` メソッドを追加し、Box 領域のディレクトリ属性確定を促す暖気運転ロジックを共通化。`ShellIconHelper.GetInfo` で Box 領域のフォルダ・ファイルの両方に対して `SHGFI_USEFILEATTRIBUTES` を強制適用し、スタブ未生成状態でも確実に標準フォルダアイコンが取得されるように変更。`DirectoryItemViewModel.InitializeAsync` で Box 領域の場合は暖気運転後に `GetFolderIcon` を使用してアイコンを取得するよう改善。`IndexService.WarmUpBoxDirectoryAsync` も共通の `PathHelper.WarmUpBoxPath` を利用するように変更し、コードの重複を排除
- **スクロールバー操作時のドラッグ＆ドロップ誤発火を修正**: ファイル一覧でスクロールバー（Thumb や RepeatButton）をドラッグした際に、背後のファイル項目のドラッグが開始されてしまう問題を修正。`PreviewMouseLeftButtonDown`／`PreviewMouseRightButtonDown`／`MouseMove` の冒頭で `e.OriginalSource` から `FindAncestor<ScrollBar>` および `FindAncestor<GridViewColumnHeader>` によりヒットテストし、スクロールバー・カラムヘッダー上での操作の場合はドラッグ開始ロジックを実行せず WPF 標準のスクロール・リサイズに委ねるようにした

## [0.5.18] - 2026-02-19 : UI帯統一と列挙最適化

### Added

### Changed

- お気に入りビュー第1階層で採用したスリムなチャコールブラック帯のデザインを、アクティブなファイルタブおよびアドレスバー行にも適用し、非選択タブには薄い境界線とわずかなホバー色でクリック領域を示しつつ、フォーカスタブだけが周囲の仕切り線から独立した黒い帯として浮かび上がるようにしてアプリ全体で「黒のワンポイント」の使いどころを統一
- メインウィンドウのタイトルバー背景をワンポイント用ダーク帯と同じチャコールブラック（#545B64）にそろえ、アドレスバー帯・お気に入り第1階層・アクティブタブと連続する「黒のヘッダーライン」として視覚的な一貫性を高めるとともに、アドレスバー内部のテキスト・アイコン・パス入力欄の文字色を白で統一してダーク背景でもパス情報がくっきり読み取れるように調整
- ファイル/フォルダの名前変更や移動、設定バックアップなどのファイル操作に Polly ベースのリトライポリシーを導入し、一時的なロックや共有違反による失敗時に数回まで自動再試行することで、ネットワークドライブやバックアップ処理での一時的な I/O エラーに対する耐性を高めた
- ファイル一覧のローカルディレクトリ列挙を .NET 8 の `FileSystemEnumerable<T>` ベースに刷新し、`EnumerationOptions` による属性スキップとアクセス不能フォルダの無視を組み合わせることで、大量ファイルを含むローカルフォルダでもメモリ効率とスキャン安定性を向上
- パンくずドロップダウンおよびナビペインツリーのサブフォルダ取得処理を `EnumerationOptions` を用いた列挙に統一し、システム属性フォルダやアクセス権限のないフォルダでの例外を内部でスキップして UI の応答性を維持

### Fixed

- アクティブ／非アクティブいずれのファイルタブにおいても閉じるボタン（×）の下端が黒い帯やタブ境界線でわずかに欠けて見えていた問題を、タブ本体のパディングと内部グリッドの MinHeight を調整しつつ閉じるボタンとホバー円のサイズを 14px・中央揃えに統一することで解消し、スリムな帯デザインを維持したままアイコン全体がきれいに表示されるようにした
- ファイル一覧で複数項目を選択した状態でそのうちの1件からドラッグを開始した際に、マウスダウンの瞬間に単一選択へ絞り込まれてしまいドラッグ対象が1件に限定されることがある問題を修正し、すでに選択されている項目上からドラッグを始めた場合は複数選択を維持したまま `DataFormats.FileDrop` 形式で選択中すべてのフルパスをドラッグデータとして外部アプリへ渡せるようにした
- ドラッグ中のガイドメッセージを「コピー/移動先にドロップしてください」から「n件の項目をコピー/移動先にドロップしてください」（例: 「5件の項目をコピー/移動先にドロップしてください」）の形式に変更し、現在ドラッグしている件数が一目で分かるようにした
- ファイル一覧で複数選択している状態からいずれかの項目をダブルクリックした際に、ドラッグ抑止用の PreviewMouseLeftButtonDown が先に `Handled` を奪ってしまい OpenItem 処理が発火しないことがある問題を修正し、マウスダウンの先頭で `e.ClickCount == 2` を評価して ViewModel の `OpenItemCommand` を即実行することで、フォルダやフォルダショートカット（.lnk）は中へ移動・ファイルは既定アプリで開くという標準動作と複数選択ドラッグ維持の両立を図った

## [0.5.17] - 2026-02-19 : お気に入りUIとロギング基盤の強化

### Added
- **将来導入予定ライブラリのドキュメント化**: `.cursor/rules/future-libraries.mdc` を新設。Serilog、Polly、FileSystemEnumerableOptions、Microsoft.Data.Sqlite、Lucene.Net の導入計画を記録し、該当機能の改修時に参照・提案するよう開発フローに組み込み。導入完了時は項目を消し込む運用とする。

### Changed
- **お気に入りビュー第1階層の仮想フォルダを「スリムな全幅帯」として強調**: `FavoriteItem` の階層レベル情報に基づき、ナビペインのお気に入りツリーにおいて「第1階層かつ仮想フォルダ」の行を、ナビペイン左端から右端手前（スクロールバー領域を除く）まで貫くスリムな全幅帯として描画するように変更。スクロールバーと干渉しないよう右端にマージンを確保しつつ、Windows 11 風の角丸デザインでカテゴリ区切りを上品に際立たせた。
- **視認性の最適化**: カテゴリ見出しの背景色を落ち着いたスレートグレー（#545B64）に変更し、その上のテキスト（フォルダ名）を白のセミボールド、概要説明を明るいグレーに自動調整することで、細身の帯でも高いコントラストと読みやすさを確保。
- **アクセントラインと余白の調整**: 第1階層カテゴリの左端にアクセントカラーのスリムな縦ラインを追加し、帯全体の上下マージンと高さ（MinHeight）を通常行よりわずかに高い程度に抑えることで、情報密度を維持しながらもカテゴリの親としての存在感を直感的に伝えられるようにした。
- **ロギング基盤の Serilog 化**: `FileLoggerService` を Serilog ベースに再構成し、日次ローテーション付きのファイル出力・デバッグ出力をライブラリに委譲。致命的な例外ハンドラでは従来どおり同期書き込み（`LogSync`）でクラッシュ直前までログを確実に残しつつ、通常の操作ログは Serilog の非同期ファイルシンク経由で効率的に記録するようにした。

### Fixed
- **Shift キー押下時のマウスカーソル点滅の解消**: `MainWindow` のキーボード操作モード判定を見直し、Shift/Ctrl/Alt などの修飾キー単体ではカーソルを非表示にしないよう変更。Shift＋右クリックでエクスプローラ互換メニューを開く際も、メニュー表示前にキーボード操作モードを解除し、カーソルが消えずにスムーズにメニューが表示されるようにした。

## [0.5.16] - 2026-02-19 : Box連携・コンテキストメニューの安定化

### Fixed
- **Box リンク取得後にエクスプローラーのコンテキストメニューが動作しなくなる問題**: IContextMenu 等の COM オブジェクトを `Marshal.FinalReleaseComObject` で厳格に解放し、シェル拡張のリソースリークを解消。Box Drive 連携後も 2 回目以降の Shift+右クリックで正常にメニューが動作するようにした。

### Changed
- **ShellContextMenu の COM 解放**: メニュー表示終了後に `Marshal.FinalReleaseComObject` で COM を完全解放。`finally` で hMenu 破棄とフック解除を確実に実行。ShellItem の明示的 Dispose を追加。
- **BoxDriveService のクリップボード監視をイベント駆動に変更**: ポーリングから `AddClipboardFormatListener`／`RemoveClipboardFormatListener` 方式へ移行。URL 検知後および 20 秒タイムアウト時にリスナーを確実に解除し、2 回目以降の Shift+右クリックが前回セッションと干渉しないようにした。

## [0.5.15] - 2026-02-19 : Box インデックス暖気運転

### Added
- **Box フォルダのディレクトリ・ウォーミング（暖気運転）**: インデックス作成（フル再構築・未作成・差分更新）の開始直前に、Box 領域内のパスのみ `Directory.EnumerateDirectories` で再帰的に構造をなぞり、Box Drive のスタブ生成を促す処理を追加。手動でフォルダを開かなくても「未インデックスを作成」でインデックスが完了するようにした。

### Changed
- **Box 専用スロットリング**: Box 領域のスキャン中は 50 件ごとに 200ms 待機。ネットワーク軽め設定がオンの場合はさらに待機を厚くしてタイムアウトを防止。
- **暖気運転中の進捗表示**: ステータスバーに「Box フォルダを準備中（暖気運転中）...」と表示し、フリーズしていないことをユーザーに伝える。

## [0.5.14] - 2026-02-19 : 初回起動の高速化

### Removed
- **レガシー移行ロジックの完全削除**: v0.4.2 で導入された `%AppData%\ZenithFiler` からのデータ自動移行（zenith.db、Lucene インデックス）を削除。実行フォルダ内の `index` と `settings.json` を正とするため、古いパスをチェックする処理は不要となった。
- **旧形式 favorites.json のマイグレーション削除**: v0.3.54 以前の `%AppData%\ZenithFiler\favorites.json` を `settings.json` へ統合する変換処理を削除。現在は `settings.json` のみを参照する。

### Changed
- **初回起動の高速化**: `index` / `backups` / `logs` フォルダの生成を `Task.Run` でバックグラウンド実行し、UI スレッドをブロックしないようにした。
- **DatabaseService の遅延初期化**: 履歴ビュー（Ctrl+Shift+3）表示時または初回の履歴保存時にのみ DB 初期化を実行し、起動時の I/O 負荷を軽減した。
- **スプラッシュ Tips の遅延ロード**: 機能紹介テキストを `Lazy<string[]>` に変更し、Loaded イベント時（タイマー開始時）まで読み込みを遅延した。

### Fixed
- **インデックス検索結果でファイルサイズが「0.0 B」と固定表示される問題**: インデックス作成時に `size` フィールドを保存するようにし、検索結果取得時に `long` 型でパースして `FileItem.Size` に割り当てるように修正した。
- **インデックス検索結果で全ファイルが汎用テキストアイコンで表示される問題**: 拡張子ごとに `ShellIconHelper.GetGenericInfo` でキャッシュし、PDF・Excel・画像などファイル種別に応じた適切なアイコンが表示されるように修正した。

### Changed
- **検索結果から拡張子のないファイルを除外**: 通常検索・インデックス検索のいずれにおいても、`Path.HasExtension` が false となるファイル（一時ファイル・キャッシュ・PreviewCache 内のハッシュ名ファイルなど）を検索対象から除外。インデックス登録時と検索結果生成時の両方でフィルタリングし、ノイズを排除して検索結果の質を向上させた。
- **インデックス検索の更新日時を Lucene DateTools 形式で保存**: `modified` フィールドを DateTools の標準フォーマット（`DateResolution.SECOND`）で保存するように変更。旧 Int64 Ticks 形式との読み出し互換を維持した。

## [0.5.13] - 2026-02-19 : Box連携のクリーンアップ

### Removed
- **Box 連携まわりの動作しなかった実験的コードを削除**: `BoxDriveService` から UI Automation（`TryGetBoxSharedLinkByUIAutomation`・メニュー強制クリック）、Verb 直接実行（`ForceKickBoxVerb`）、デバッグ用ウィンドウスキャン（`DebugScanBoxWindows`）・`SendCommandToWindow` を削除。`ShellContextMenu` から `IObjectWithSite`／`IServiceProvider`／`ExplorerImpersonationSite` のなりすまし実装、メニュー全項目ダンプ（`DumpRawMenuItems`・`DumpContextMenuItems`）、過剰な `[ShellDebug]` ログを削除。

### Changed
- **Box 連携を「20秒スナイパー監視」と「モダンなローディング表示」に特化**: `BoxDriveService` は Shift+右クリック起点のクリップボード監視（box.com 検知→2行整形）のみを担当するよう軽量化。`ShellContextMenu` は OS 標準コンテキストメニュー表示のシンプルなラッパーに整理。`BoxAddressService` は URL 取得を行わず Box パスのみ返すように変更（実アドレスは Shift+右クリック→「共有リンクをコピー」で監視により取得）。メニューからの「BOXパス＋実アドレスをコピー」は Box パスのみコピーし、通知は「BOXパスをコピーしました」／「BOXパス＋実アドレスをコピーしました」を用途に応じて表示。

## [0.5.12] - 2026-02-19 : Box共有リンクのスナイパー監視強化

### Added
- **Box フォルダ内での Shift+右クリック起点の共有リンク自動整形**: Box Drive 領域（パスに `\Box\` を含む）の項目で **Shift＋右クリック** をすると、エクスプローラメニュー表示と同時に最大20秒間クリップボードを監視。ユーザーがメニューから「共有」→「共有リンクをコピー」を選び、`box.com` を含む URL がクリップボードに書き込まれたタイミングで、1行目に Box 相対パス・2行目に URL の2行形式でクリップボードを上書き。整形成功時はステータスバーに「BoxパスとURLを結合しました」と表示し、ペインにグリーンフラッシュを表示。20秒経過時は通知なしで監視終了。`PathHelper` に `IsInsideBoxDrive` を追加。

## [0.5.11] - 2026-02-19 : Box共有リンク取得とUI調整

### Changed
- **標準ローディング・オーバーレイの導入**: `BaseViewModel` を新設し、`IsLoading` と `LoadingMessage` プロパティを共通化。検索実行時や Box 連携時の実アドレス取得時など、待ち時間のある処理で画面全体を覆うモダンなローディング・オーバーレイを表示するようにした。デザインは起動スプラッシュと同じ「グローイング・パルス・リング」と回転するアークを採用し、アプリ全体のブランドイメージを統一。検索処理では「インデックスから検索中...」「最新の状態を確認中...」など、処理内容に応じたメッセージを動的に表示する。
- **リソース参照の安全性向上**: `MainWindow.xaml` の重要なリソース参照（`BackgroundBrush`、`TextBrush`、`TitleBarBrush`）を `StaticResource` から `DynamicResource` に変更し、`AppResources.xaml` の遅延読み込み時でも確実にリソースが解決されるようにした。
- **Box Drive の共有リンク取得ロジックの堅牢化**: サブメニュー探索を改善し、動的生成されるメニュー項目に対してリトライロジック（最大3回、50ms間隔）を追加。キーワード検索に "Box" を追加し、多言語環境でも確実に「共有」サブメニューを特定できるようにした。クリップボード待機を最適化し、タイムアウトを3秒、ポーリング間隔を100msに変更。元のクリップボード内容と比較して変化を検知することで、Box アプリがクリップボードを更新するまで確実に待機するようにした。

### Fixed
- **起動時の XAML 解析エラー（`TypeConverterMarkupExtension` 例外）**: `LoadingOverlay.xaml` で使用していた `PackIconLucide` の `Kind="Loader2"` が Lucide アイコンライブラリに存在しないため、`Kind="Loader"` に修正した。これにより、`MainWindow` の初期化時に `LoadingOverlay` を解析する際に発生していた型変換エラーが解消された。
- **Box Drive の「BOXパス＋実アドレスをコピー」で URL が確実に取得できない問題**: クリップボード書き込みの待機時間が不十分で、Box アプリがクラウドから URL を取得してクリップボードに書き込む前に処理が完了していた。クリップボードの内容変化を検知するロジックを追加し、URL 形式の文字列が現れるまで最大3秒間、100ms間隔で監視するように改善。また、複数選択時のエラーハンドリングを強化し、一部の項目で URL 取得に失敗した場合でも Box パスは確実にコピーされ、失敗を通知するようにした。
- **ナビペインおよびドキュメント目次のテキストカラーを黒基調に統一**: お気に入り・ツリービュー・参照履歴・インデックス・アプリ設定の各ナビペインとドキュメントビューアの目次リストについて、メインテキストを黒系 (`TextBrush`)・補足テキストを中間グレー (`SubTextBrush`) に整理し、選択・ホバー時もコントラストを維持しながら情報の優先度を一目で分かるようにした。
- **Aペイン／Bペインのツールバー区切り線のコントラストを改善**: アクティブペインで背景色と文字色を黒基調に変更したことにより見えづらくなっていたツールバー内の縦線セパレーターを、専用ブラシ（中程度の濃いグレー）とやや太めの描画に変更し、機能グループの区切りがどのペインでもはっきり判別できるようにした。
- **名前の変更ウィンドウ（InputBox）のゼロレイテンシ仕様化**: InputBox をアプリ全体で共有するキャッシュインスタンスに変更し、コンストラクタ内の処理を最小限に整理したうえで、`DispatcherPriority.Send` でレイアウト確定後にモーダル表示・`FocusManager.FocusedElement` と `ContentRendered` での二重フォーカス取得により、Ctrl+Shift+N／F2 押下直後からほぼ遅延なく入力できるようにした。新規フォルダ作成時の `Directory.CreateDirectory` はバックグラウンドスレッドで実行し、InputBox の表示・クローズがネットワークドライブ等のファイルシステム待ちに巻き込まれないようにした。
- **フォルダショートカット（.lnk）の内部タブでの展開**: ファイル一覧およびお気に入りでフォルダを指すショートカット（`.lnk`）をダブルクリック／Enterした場合、Vanara.Windows.Shell でリンク先を解析し、外部エクスプローラーを起動せずに現在のアクティブペイン内で新しいタブを作成してフォルダを開くように変更し、作業コンテキストを維持したままシームレスに移動できるようにした。
- **名前の変更ウィンドウ（InputBox）の null 参照警告を解消**: `InputBox.ShowDialog` 内で `Application.Current.MainWindow` を直接参照していたため、アプリケーションコンテキストがまだ初期化されていない状況で null 参照警告が出ていた。`Application.Current?.MainWindow` に変更し、アプリケーション未初期化時でも安全に動作するようにした。
- **右ドラッグ「ここにショートカットを作成」でフォルダ対象時に失敗する問題**: ShortcutHelper が `new ShellLink(path)` で既存の .lnk を読み込むコンストラクタを使用していたため、フォルダやファイルを対象にした場合に失敗していた。`ShellLink.Create` で新規ショートカットを作成するように変更し、フォルダ・ドライブルート・UNCパス等も正しく処理。あわせて作成失敗時にエラー詳細をダイアログに表示するよう改善した。
- **マルチディスプレイ環境変更後にウィンドウが画面外に残る問題**: 起動時に保存済みのウィンドウ位置とサイズを現在接続されているディスプレイの表示領域と突き合わせ、いずれのディスプレイとも交差しない場合は無効な座標とみなしてメインディスプレイ中央へ救出するように変更。あわせて、保存されたウィンドウサイズが画面より極端に大きい場合は画面の約80%まで自動縮小し、どの環境でも必ず「今見えている画面内」にウィンドウが現れるようにした。
- **最大化状態からドラッグでウィンドウを移動した際に縦長の不自然なサイズになる問題**: タイトルバーをドラッグして最大化を解除したときに、直前の通常サイズまたは現在のディスプレイ作業領域の約75%幅・16:10付近の比率に自動リサイズし、マウスカーソルがタイトルバー中央付近に来るよう位置を補正することで、引き剥がし直後から自然なサイズと位置関係でドラッグを続けられるようにした。


## [0.5.10] - 2026-02-18 : 名前変更・タブ一覧の即応性とInputBox修正

### Changed
- **新規フォルダ作成（Ctrl+Shift+N）で名前の変更ウィンドウを先に表示**: フォルダ作成後に Refresh を待ってからリネームを開くのではなく、先に名前の変更ウィンドウを表示し、OK でフォルダを作成するように変更。Refresh を待たずにすぐ入力できる。
- **名前の変更ウィンドウ（InputBox）の表示を速く**: フォーカスと Owner 設定を Loaded/ContentRendered に移し、描画完了後に確実にフォーカスするように変更。ペースト後などでリネームを開く際の Dispatcher 段数を減らし、ダイアログが開くまでの遅延を短縮。
- **名前の変更ウィンドウの「本日の日付」押下後のカーソル位置**: 本日の日付ボタン押下後、日付文字列全体が選択されていたため続きの入力が上書きになっていた。カーソルを日付の末尾に置き選択を解除するように変更し、押下後すぐに追加入力できるようにした。
- **タブ一覧ポップアップの表示を即時表示に変更**: タブ一覧ボタン押下時、フェードアニメーションを廃止し、クリックと同時に一覧が表示されるようにした。

### Fixed
- **Box の「BOXパス＋実アドレスをコピー」で URL が取得できない問題**: Box Drive のコンテキストメニューで「共有リンクをコピー」が「共有」サブメニュー内にありトップレベルのみ検索していたため見つからなかった。親「共有」サブメニューを探索し、その中の「共有リンクをコピー」のコマンド ID (wID) で InvokeCommand するよう改修し、エクスプローラの「共有＞共有リンクをコピー」と同等に URL を取得できるようにした。
- **名前の変更ウィンドウが真っ白で入力できるまで時間がかかることがある問題**: 新規フォルダは名前入力を先に表示するフローに変更し、InputBox は ContentRendered でフォーカス・Loaded で Owner 設定を行うようにし、リネーム表示時の Dispatcher 段数削減により改善した。
- **名前の変更ウィンドウ表示時に「Dialog の表示後に Owner プロパティを設定することはできません」で落ちる問題**: Owner と Topmost を Loaded ではなくコンストラクタで設定するように修正した（表示後に Owner を設定できない WPF の制約に合わせる）。

## [0.5.9] - 2026-02-18 : タブ一覧表示位置の改善

### Added
- **タブ一覧表示機能**: タブ列の右端（＋ボタンの右）にタブ一覧表示ボタン（Listアイコン）を追加。クリックするとプルダウンメニューが表示され、全タブの一覧から選択や削除が可能です。現在選択中のタブはハイライト表示され、ロックされたタブは削除ボタンが非表示になります。
- **検索時の1画面モード自動切り替えオプション**: アプリ設定に「検索時に自動的に1画面モードに切り替える」オプションを追加。有効にすると、検索実行時に自動的に1画面モードに切り替わり、検索結果を広く表示できます。Bペインで検索した場合は、検索タブがAペインに表示されます。

### Changed
- **タブ一覧の表示位置をリスト表示ボタン下で×ボタンが揃うように変更**: タブ一覧ポップアップを CustomPopupPlacementCallback で右端揃えにし、一覧内の×ボタン列がリスト表示ボタン（Listアイコン）の真下に来るようにした。
- **検索バーの×押下時の挙動を検索モード別に統一**: アプリ設定「検索の挙動」に応じて、検索バー右端の × ボタンの動作を明確化した。「同一ペイン・新規タブ」「反対ペイン・新規タブ」では検索結果タブ上で × を押すとそのタブを閉じて元のタブに戻り、「同一ペイン・現在タブで即時検索」では × を押すと検索結果ビューを解除して現在フォルダの一覧を自動再読込し、空白画面にならず即座に通常ビューへ戻れるようにした。
- **検索結果ビュー解除時に元のペイン数に自動復元**: 「検索時に自動的に1画面モードに切り替える」オプションが有効な場合、検索実行時に1画面モードに切り替えた後、検索結果タブを閉じるか検索バーの×ボタンで検索をクリアすると、自動的に元のペイン数（2画面モードなど）に戻るようになりました。
- **アプリ設定ビューの余白調整**: 「検索時の表示設定」セクションの上の余白を他のサブセクションと同様に統一し、「3. インデックス」セクションとの間に適切な間隔を設けて、セクション間の視覚的な区切りを明確にしました。

### Fixed
- **アプリ設定ビューでチェックボックスをホバーした際に視認性が低下する問題**: WPF UI のデフォルトスタイルがセピア背景に対してコントラスト不足で、チェックボックスが消えたように見える問題を修正。カスタムスタイルを追加し、ホバー時に枠線を `TextBrush`（濃い色）で強調し、背景をわずかに濃くすることで視認性を改善しました。
- **チェック状態のチェックボックスをホバーした際にチェックマークが消える問題**: チェック状態のチェックボックスをホバーした際に、背景色が変更され、さらに `Opacity` が下がることで白いチェックマークが見えなくなる問題を修正。チェック状態かつホバー時は背景を `AccentBrush` のまま維持し、未チェック時のみホバーエフェクトを適用するように変更しました。
- **チェック状態のチェックボックスをホバーした際に視覚的フィードバックが不足する問題**: チェック状態のチェックボックスをホバーしても変化が見えず、ホバーしていることが分かりにくい問題を修正。ホバー時に背景色を `AccentBrush`（`#268BD2`）からより濃い色（`#1E7AB8`）に変更し、軽い影を追加することで、ホバー状態が明確に分かるように改善しました。
- **タブ一覧ボタンを押下しても一覧が一瞬で消えてしまう問題**: タブ列右端のタブ一覧ボタン押下時、Command と Click の二重実行によりトグルが2回走って即座に閉じていた問題を修正。Command バインディングを削除し Click ハンドラのみで制御するようにした。あわせて ParentWindow_PreviewMouseDown におけるボタン判定を、ControlTemplate 内の FindName と IsDescendantOf による判定に変更し、一覧外クリック時の閉じる動作を確実にした。

## [0.5.8] - 2026-02-18 : フォーカス中ペインの文字色統一

### Changed
- **フォーカス中ペインの文字色・アイコン色を黒に統一**: Aペイン／Bペインでフォーカスがあるペインについて、アクション行のツールバーアイコン・一覧ヘッダ・一覧のテキスト・アイコンビューのファイル名などを `FocusedPaneTextBrush`（黒）で表示するようにし、「作業中のペイン」が一目で分かるように視認性を向上した。
- **表示モード・列設定トグルアイコンも黒に統一**: アクション行右側の6つのトグル（一覧ビュー／小・中・大アイコン／列を自動補正／フォルダを先に）についても、フォーカス中ペインでは `FocusedPaneTextBrush`（黒）で表示されるようにし、ペイン内のアイコン色を一貫させた。
- **一覧ヘッダ・検索ヘッダ・パンくずも黒に統一**: 詳細一覧と検索結果一覧の各ヘッダ（名前／更新日時／種類／サイズなど）およびパンくずの各フォルダ名についても、フォーカス中ペインでは `FocusedPaneTextBrush`（黒）で表示されるようにし、「どのペインで操作しているか」をヘッダとアドレスバーからも一目で判別できるようにした。
- **一覧セル（名前／更新日時／種類／サイズ）の文字色も黒に統一**: 詳細一覧の各列セル（名前／更新日時／種類／サイズ）の TextBlock にアクティブペイン連動スタイルを適用し、ヘッダだけでなく行データもフォーカス中ペインでは黒で表示されるようにして、作業中ペインの視認性をさらに高めた。
- **検索結果ビュー「名前」列の文字色もフォーカス中ペインで黒に統一**: 検索結果タブの GridView（SearchGridView）における「名前」列セルの TextBlock にも `ActivePaneCellTextStyle` を適用し、通常の詳細一覧と同様にフォーカス中ペインでは黒で表示されるようにした。

### Fixed
- **詳細一覧の「名前」列だけフォーカス中ペインで黒にならない問題**: 「名前」列セルの TextBlock に `ActivePaneCellTextStyle` を適用し忘れていたため、フォーカス中ペインでも文字色がグレーのままだった不具合を修正した。

## [0.5.7] - 2026-02-17 : ツールバーアイコンとホバー視認性の改善

### Changed
- **ツールバーアイコンの色を統一**: 右側6個（一覧ビュー・小中大アイコン・列を自動補正・フォルダを先に）のアイコンが左側より薄く表示されていた問題を修正。IconToggleButton の Foreground を SubTextBrush から TextBrush に変更し、左側アイコンと同じ濃いグレーで表示するようにした。
- **フォーカス中ペインでのホバー視認性を改善**: フォーカス中のペインではヘッダ背景（#DFDAC8）とアイコン/ボタンのホバー背景が同一でホバーが判別しづらかった。IconButton / IconToggleButton のホバー背景を #C9C4B3 に変更し、ホバー時に濃い色で表示されるようにした。

### Fixed
- **お気に入りで階層により仮想フォルダのダブルクリック展開が有効/無効を繰り返す問題**: TreeViewItem ごとのハンドラだとバブリングで親のハンドラが走り、奇数階層で親が開閉していた。TreeView にハンドラを1つだけ配置し、e.OriginalSource から TreeViewItem を取得する方式に変更。GetParent&lt;ToggleButton&gt; チェックがテンプレート構造で常に真となり全フォルダで展開されなくなっていたため削除し、全階層でダブルクリックした仮想フォルダが展開するように修正。
- **お気に入りで仮想フォルダのダブルクリック展開に3～4回クリックが必要になる問題**: WPF の TreeView で MouseDoubleClick が1回のダブルクリックで複数回発火し、トグルが2回実行されて元に戻っていた。PreviewMouseLeftButtonDown で e.ClickCount==2 を検出して処理する方式に変更し、1回のダブルクリック（2クリック）で確実に展開するように修正。

## [0.5.6] - 2026-02-17 : Box領域の実アドレスコピーとUI改善

### Added
- **Box領域で「BOXパス＋実アドレスをコピー」を追加**: 現在のフォルダがBoxドライブ内のとき、「連携用BOXパスをコピー」の下に「BOXパス＋実アドレスをコピー」を表示。Box Drive の Shell 拡張（共有リンクをコピー）を呼び出し、連携用パス（`Box\～`）と実アドレス（`https://app.box.com/～`）の2行形式でクリップボードにコピーする。取得中はローディングオーバーレイ（「実アドレスを取得中...」）を表示。背景・項目選択いずれも対応。複数選択時は `---` 区切りでコピー。
- **コンテキストメニューに「フォルダ内の一覧をコピー」を追加**: ファイル一覧の背景（空きスペース）を右クリックしたとき、「名前・パスのコピー」グループに「フォルダ内の一覧をコピー（名前）」と「フォルダ内の一覧をコピー（フルパス）」を表示。表示中のフォルダ内の項目一覧（現在の表示・フィルタ・ソートに従う）を、名前のみまたはフルパスを改行区切りでクリップボードにコピーする。検索結果タブのときは検索結果一覧をコピーする。0件のときはステータスバーに「一覧は0件でした」と表示する。

### Changed
- **お気に入りビューの整理用フォルダ（仮想フォルダ）をダブルクリックで展開**: お気に入りツリーで、整理用の仮想フォルダ（子をまとめるフォルダ）の行をダブルクリックすると展開/折りたたみされるようにした。物理フォルダ・ファイルのダブルクリック（ペイン表示・アプリで開く）は従来どおり。
- **フォーカス中のペインを視覚的に強調**: フォーカスが当たっているペイン（A/B ペインのみ、ナビペインは対象外）の背景を標準よりわずかに濃い色に、一覧・ツールバー等の文字色を黒に変更して判別しやすくした。上部メニュー（タブヘッダー・アクション行・アドレスバー）は一覧部分よりわずかに濃い色で表示し、操作範囲を視覚的に区別できるようにした。

## [0.5.5] - 2026-02-17 : 同一場所ドロップエラー解消とメニュー改善

### Added
- **Box領域で「連携用BOXパスをコピー」を追加**: 現在のフォルダがBoxドライブ内（例: `C:\Users\(ユーザ名)\Box\～`）のとき、名前・パスのコピーグループに「連携用BOXパスをコピー」を表示。他ユーザーと共有する際に使う「Box\～」形式のパスをクリップボードにコピーする。背景・項目選択いずれも対応。複数選択時は改行区切りでコピー。
- **コンテキストメニューに「ファイル名をコピー」「フルパスをコピー」を追加**: ファイル・フォルダを選択して右クリックしたとき、選択項目の名前またはフルパスをクリップボードにコピーする項目を表示。複数選択時は改行区切りでコピー。背景（空きスペース）右クリック時は「フルパスをコピー」で現在のフォルダのフルパスをコピー。

### Changed
- **コンテキストメニューの区分け**: アプリ独自メニューを「開く・表示」「クリップボード」「名前・パスのコピー」「編集・変更」「アプリ固有」「その他・システム」の6グループに区切り線で区分けし、項目を判別しやすくした。ファイル名をコピー・フルパスをコピーはクリップボードから独立した「名前・パスのコピー」として表示。区切り線には既存の ContextMenuSeparatorStyle を適用。
- **PathHelper.GetPhysicalPath の OneDrive 変換ロジックを廃止**: Desktop/Documents 等の特殊フォルダを OneDrive の物理パスへ変換する処理を削除し、パスの正規化（フルパス解決・末尾スラッシュ整理）のみを行うようにした。環境（個人用/業務用 OneDrive、フォルダ構成）によって変換結果が異なり一般性を欠いていたため、入力パスをそのまま正規化して使用する方針に統一。OneDrive 領域での新規フォルダ作成失敗などの不具合を解消。

### Fixed
- **パンくずドロップダウンでサブフォルダ取得時にUIがフリーズする問題**: ネットワークドライブ等で `Directory.GetDirectories` がUIスレッドで実行されていたため、`GetSubfoldersAsync` に変更し `Task.Run` で非同期取得するようにした。
- **ツリー／一覧からのドラッグ＆ドロップでファイル移動中にUIがフリーズする問題**: `ShellFileOperations.PerformOperations` をUIスレッドで同期的に実行していたため、`Task.Run` 内で実行し完了時のみ Dispatcher で通知するようにした。
- **起動時に設定ファイル読み込みでUIがブロックされる問題**: コンストラクタで `WindowSettings.Load()` と `FavoritesViewModel` の `LoadFavorites()` を呼んでいたため、起動時は `CreateDefault()` で表示し、`Loaded` で非同期に `WindowSettings.Load()` とお気に入り `LoadFromSettings` を実行するようにした。
- **FileSystemWatcherイベントハンドラでのスレッド違反エラー**: `OnFileSystemChanged`メソッド内で`MainVM`プロパティ（UIスレッド所有オブジェクト）にアクセスしていたため、バックグラウンドスレッドから呼び出された際に「このオブジェクトは別のスレッドに所有されているため、呼び出しスレッドはこのオブジェクトにアクセスできません」というエラーが発生していた問題を修正。処理全体を`Dispatcher.BeginInvoke`でUIスレッドにディスパッチするように変更し、スレッド違反を解消した。
- **ドラッグ＆ドロップで同じ場所にドロップしたときにエラー（0x80270000・ファイルが大きすぎます）が発生する問題**: 一覧で自分自身のフォルダの上へドロップした場合、および一覧からツリーへ現在表示中のフォルダにドロップした場合に、同一フォルダへの移動をスキップするようにした。Shell の移動処理を呼ばずに済むため、エラーが発生しなくなる。

## [0.5.4] - 2026-02-16 : 戻り後のフォーカス復元

### Added
（なし）

### Changed
（なし）

### Fixed
- **戻る操作後にフォーカスが失われ、キーボード上下が先頭から操作になる問題**: フォルダに入って戻ったとき、戻り先一覧で「入ったフォルダ」にフォーカスを復元するようにした。キーボードの上下キーでその位置から操作を続けられる。
- **BackSpace（上へ）で戻ったときも同様にフォーカスが復元されない問題**: Up コマンドでも `PathToFocusAfterNavigation` を設定するようにし、BackSpace で親フォルダへ戻ったときも「入ったフォルダ」にフォーカスするようにした。

## [0.5.3] - 2026-02-15 : アイコンビュー改善と縦スクロールバー位置統一

### Added
（なし）

### Changed
- **アイコンビューの視覚的洗練と角丸表示の修正**: サムネイルカードの角丸を 4 に統一し、カード間 Margin を 3 に。ファイル名ラベル背景をすりガラス風（#A0000000）に変更。角丸・シャドウが正しく表示されるよう、CardBorder の ClipToBounds を False にし、内側 Grid に RectangleGeometry（RadiusX/Y=4）の Clip を追加。CardGrid に明示的な Width/Height を設定し DataTrigger で中・小アイコン時も同一サイズに。CardBorder に HorizontalAlignment="Left" と VerticalAlignment="Top" を付与してカードが親サイズに伸びないようにし、SizeToRoundedRectGeometryConverter で Grid.Clip を ActualWidth/ActualHeight にバインドして角丸を描画サイズに連動。列幅計算を Margin=3 に合わせ（160/116/80 に +6）て右上・右下の角丸が切れないようにした。
- **アイコンビューの枠をデザインに調和**: フォーカス枠を SubtleFocusVisualStyle（BorderBrush #D3D0C3）、ホバー枠を AccentBrush から BorderBrush に変更し、白・グレー基調の UI に合わせた。
- **アイコンビュー画像サムネイルのツールチップ**: フルファイル名のツールチップを、サムネイル全体のホバーから下部グラスモーフィズム・オーバーレイ（InfoOverlay）のホバー時のみ・約2秒遅延に変更し、閲覧体験を損なわないようにした。
- **アイコンビューのホバー挙動**: ホバー時の ScaleTransform（1.03 倍）を廃止してフォントのぼやけを解消。枠の強調・マイクロシャドウ・下部オーバーレイのフェードインは維持。
- **アイコンビューの縦スクロールバー位置を一覧ビューと同一に**: アイコンビュー用 Border の右 Padding を 0（Padding="12,10,0,10"）にし、縦スクロールバーがペイン右端に表示され一覧ビューと同じ位置に揃うようにした。

### Fixed
（なし）

## [0.5.2] - 2026-02-15 : アイコンモード選択をツールバーアイコン群に変更

### Added
- **Aペイン・Bペイン間でのタブ移動**: タブをドラッグして反対ペインのタブヘッダーまたはタブ上にドロップすると、そのタブが反対ペインに移動する。同一ペイン内の並べ替えは従来どおり。移動元が最後の1タブの場合は自動で空のタブが1つ追加される。1ペイン表示時は「反対ペインに移動」で2ペインに切替えてから移動。タブの右クリックメニューに「反対ペインに移動」を追加。

### Changed
- **アイコンビューで選択ハイライトとクリック領域をカード部分に統一**: 大・中・小アイコン表示時、選択時のハイライトとクリックで選択される範囲を、サムネイル＋ファイル名のカード部分のみに限定した。カード外の余白をクリックしても選択されず、ハイライトもカード部分だけに表示される。
- **中アイコン・大アイコン時のアイテム間隔を狭く調整**: アイテム間の余白（Margin）を大アイコン 4→2、中アイコン 3→2 に変更し、より多くのアイテムを一覧表示できるようにした。
- **アイコンビューの表示域改善**: 中・大アイコンのサイズを拡大（小80→中116→大160）して3段階の差を明確化。ペイン端との余白を追加（Padding 12,10）し、よりモダンでゆとりのある表示にした。
- **アイコンビューのレイアウトを右端調整方式に変更**: アイテム間の Margin を 0 にし、余白を右端に集約。Border でラップしてペイン端の余白を確実に反映（ListBox の Padding が効かない対策）。UniformGrid を固定幅＋左寄せとし、余剰スペースを右端に配置するようにした。
- **アイコンビューのアイテム間余白を 2px に調整**: 右端調整方式は維持しつつ、アイテム間の Margin を 2 に設定して適度な視認性を確保した。
- **アイコンモード選択をポップアップからツールバーアイコン群に変更**: 大・中・小の選択をポップアップメニューで行う方式をやめ、ツールバーに小・中・大の3つの ToggleButton を並べる方式に変更。選択中のモードが一目で判別でき、1クリックで切り替え可能に。アイコンは小＝LayoutList・中＝LayoutGrid・大＝PanelTop とし、左から右へサムネイルが大きくなるイメージで配置した。

### Fixed
- **タブメニュー「反対ペインに移動」追加後に起動しない問題**: タブコンテキストメニューのアイコンに指定した `PanelLeftRight` が MahApps.Metro.IconPacks.Lucide に存在せず XAML パースで例外になっていた。既存で利用実績のある `PanelRight` に変更した。
- **ドラッグ中の「Aペイン、もしくはBペインにドロップしてください」等のアドーナーが消えず残る事象**: 削除時に AdornedElement から AdornerLayer を再取得していたため、ドラッグ中にナビペインを切り替える等で要素がビジュアルツリーから外れると GetAdornerLayer が null になり Remove されず残っていた。Add 時に AdornerLayer を保持し Remove ではその参照で削除するよう変更。あわせて DoDragDrop 前後の例外でも確実に削除するため try/finally で Remove を実行するよう修正（TreeViewDragDropBehavior・MainWindow 履歴ドラッグ・TabContentControl 右ドラッグ）。
- **アイコンビューでサムネイルが表示されず「ZenithFiler.FileItem」と表示される問題**: ListBoxItem にカスタム ControlTemplate を指定した際、ItemTemplateSelector の結果が ContentPresenter に渡らなくなっていた。ContentPresenter で ContentTemplate の代わりに ContentTemplateSelector を親 ListBox の ItemTemplateSelector にバインドするよう変更し、サムネイル・カード表示が復旧するようにした。

## [0.5.1] - 2026-02-14 : 検索対象0件時のインデックス件数表示修正

### Added
（なし）

### Changed
（なし）

### Fixed
- **検索対象フォルダが0件のときも前回のインデックス件数が表示される問題**: 登録フォルダが無い状態では永続インデックスを参照せず、インデックス件数を0として表示するようにした（RefreshIndexedDocumentCount で Items.Count == 0 のときは 0 を代入して return）。
- **新規フォルダ作成後にリネームが開始されない問題**: CollectionChanged はリフレッシュ中（IsRefreshInProgress == true）に発火するため OnItemsViewCollectionChanged で ApplyFocusAfterRefresh がスケジュールされず、リネーム処理に到達していなかった。OnRefreshCompleted のコールバック内で RequestFocusAfterRefresh が true のときに ApplyFocusAfterRefresh を Loaded でスケジュールするようにし、リフレッシュ完了後に選択・リネームが実行されるようにした。
- **お気に入りリネーム・概要編集・インデックスフォルダ追加等で「Owner を設定できません」エラーが発生する問題**: 初回起動でスプラッシュを閉じた後も Application.Current.MainWindow が SplashWindow のままだったため、InputBox／SelectFolderDialog／DescriptionEditDialog 等で Owner に未表示ウィンドウを設定して例外になっていた。SplashWindow の InitializationComplete でスプラッシュを閉じる前に Application.Current.MainWindow = mainWindow を設定するよう修正。あわせて各ダイアログ（InputBox、SelectFolderDialog、DescriptionEditDialog、AddFolderDialog、AddToFavoritesDialog、SelectExplorerWindowsDialog）で、Owner を main != null && main.IsLoaded のときのみ設定するようにし、万が一 MainWindow が未表示のままでも落ちないようにした。

## [0.5.0] - 2026-02-14 : アイコンビュー安定化とコード洗浄

### Added
（なし）

### Changed
- **アイコンビュー安定化に伴うコード洗浄（TabContentControl）**: ListView_KeyDown の冗長な FileListBox 用ガードを削除し、先頭コメントで「修飾キーなし無効化・更新ボタン有効・Ctrl+C/V/X 許可」を明示。試行錯誤で追加した [Refresh]／[F5] 系のデバッグログを OnRefreshStarting／OnRefreshCompleted／OnItemsViewCollectionChanged／ApplySelectionRestore／ApplyFocusAfterRefresh／ListView_KeyDown から削除。列数計算まわりの _isUpdatingIconColumns／ApplyIconColumnCountFromPending のコメントを「再入防止：列数変更→レイアウト→SizeChanged の無限ループを防ぐ」に整理。
- **リファクタリング（信頼性・パフォーマンス・型安全性）**: TabItemViewModel で LoadDirectoryAsync 冒頭にパス未設定ガードを追加、LoadItemsFromLocal／LoadItemsFromUnc の例外握りつぶしをログ記録に変更。ShellThumbnailService.GetThumbnailAsync のパス null/空ガードを明示化。BreadcrumbOverflowBehavior で Dispatcher に null 条件演算子を適用。UpdateSelectionInfo で Cast を OfType に変更し null/空ガードを整理。_loadCts／_iconLoadCts の新規生成前に既存インスタンスを Dispose。TabContentControl でアイテム幅を GetItemWidthForMode に集約、UpdateColumnWidths の InvokeAsync 重複を _updateColumnWidthsPending で抑制。LoadIconsAsync でクラウド/オフライン判定を ShouldSkipThumbnail に共通化。
- **アイコンビューの制限解除**: アイコンビューでも F5 で更新・矢印・Enter 等のキー操作が可能に。列数計算は 30ms デバウンス＋ApplicationIdle 適用でレイアウト連鎖を防止し、フォーカス復元は ContextIdle で列数更新より後に実行するよう変更。
- **アイコンビューの安定性最優先（差し戻し）**: PresentationCore での StackOverflow を根絶するため、アイコンビューではキーボード操作（矢印・Enter・F5 等）とツールバー更新ボタンを無効化。検証用のフォーカス退避・ScrollIntoView 遅延・100ms サスペンド等の複雑なコードを削除し、シンプルで安全な実装に戻した。

### Fixed
- **アイコンビューにおいて、WPFレイアウト競合による StackOverflow を防ぐため、キーボード操作をマウス操作に限定する安定化措置を実施**。修飾キーなしのキー（F5・矢印・Delete 等）は無効化し、ツールバー「更新」ボタンおよび Ctrl+C／Ctrl+V／Ctrl+X 等の修飾キー付き操作は利用可能。

## [0.4.28] - 2026-02-14 : お気に入りクイックアクセスインポート

### Added
- お気に入りビューにクイックアクセスインポートボタンを追加。押下でエクスプローラのクイックアクセス（ピン留め等）の項目をお気に入りに取り込める。

### Changed
- クイックアクセスインポートボタンのアイコン色を他アイコンと同様に SubTextBrush に統一しました。
- クイックアクセスインポート先を、名前「インポート」・概要「エクスプローラのクイックアクセスからインポート」のフォルダ内に変更しました。既存の同名フォルダがあればその中に追記されます。

### Fixed
- **アイコンモードでたまにアイテム間隔が非常に広く表示される**: SizeChanged が小さい幅で先に発火すると列数1が遅延コールバックで確定し、その後の正しい幅での再計算が再入防止で無視されていた問題を修正。遅延コールバック内で `FileListBox.ActualWidth` を再取得して列数を再計算するようにし、小さい幅で列数1が確定する競合を防止しました。

## [0.4.27] - 2026-02-14 : 表示形式UI改善（各ペイン配置・メニュー閉じる・選択時演出統一）

### Added
（なし）

### Changed
- **アイコンモード選択メニューの改善**: 大・中・小アイコンを選択した際にポップアップが閉じるようにしました。また、一覧ビュー・アイコンビュー切替ボタンの選択時表示を「列を自動補正」などと同じ薄い青背景（#D0E6F8）＋アクセント色アイコンに統一しました（ToggleButton + IconToggleButton に変更）。
- **一覧ビュー／アイコンビュー切替を各ペインのアイコン群へ移動**: これまでステータスバー右端の共通ボタンでフォーカス中のペインを切り替えていましたが、各ペインのツールバー（「フォルダを取り込む」の右、「列を自動補正」の左）に一覧ビュー・アイコンビュー切替ボタンを配置し、そのペイン専用で切り替えるようにしました。MANUAL.md の説明を同様に更新しています。
- **表示形式ボタンのツールチップを変更**: ステータスバー右端の一覧ビュー用ボタン（Rows3 アイコン）のツールチップを「一覧ビューへの切り替え」、アイコンビュー用ボタン（LayoutGrid アイコン）のツールチップを「アイコンビューへの切り替え」に変更しました。MANUAL.md の説明も同様に更新しています。
- **ツリービューのダブルクリックで展開とペイン表示を両立**: 展開用の矢印（▶／▼）上でダブルクリックした場合はツリーの開閉のみ行いペインには表示せず、行本体（アイコン・フォルダ名）上でダブルクリックした場合のみペインに表示するようにしました。これにより、子フォルダがある項目でも「展開だけ」「ペインに表示だけ」を意図どおりに操作できます。
- **ツリービューに Shift+クリック／Shift+Ctrl+クリックでアイコンビュー表示を追加**: フォルダを Shift+クリックするとフォーカス中のペインに大アイコンで表示、Shift+Ctrl+クリックすると反対側ペインに大アイコンで表示（1ペイン時は2ペインに自動切替）するようにしました。ツールチップと MANUAL.md に仕様を追記しました。
- **ツリービューのクリック動作を変更**: ワンクリックでは選択のみとし、ダブルクリックまたは Enter でフォーカス中のペインにフォルダを表示するように変更しました。Ctrl+ダブルクリックで反対側ペインに表示（1ペイン時は2ペインに自動切替）。右クリックメニューの「Aペインに表示」「Bペインに表示」で表示先を指定できます。各フォルダにツールチップ（ダブルクリックでペインに表示、Ctrl+ダブルクリックで反対ペインに表示）を追加しました。
- **アイコンビュー（非画像）でファイル名を4行まで表示**: フォルダや文書など非画像アイテムのファイル名を、従来の2行から4行まで表示するように変更しました。長い名前は4行を超える部分を「...」で省略します。
- **アイコンビュー（非画像）でカード高さ・アイコン/タイトル位置を固定**: フォルダや文書など非画像アイテムで、カード高さを固定し、アイコン領域とタイトル開始位置を揃えました。エクスプローラーと同様に調和のとれた表示になります。

### Fixed
- **ツリーのダブルクリックで親フォルダのパスが表示される**: TreeViewItem の MouseDoubleClick が親へバブルするため sender がルート側の項目になり、クリックしたフォルダではなく親のパスでペインに表示されていた問題を修正。e.OriginalSource から GetParent で実際にクリックされた TreeViewItem を取得するようにしました。
- **裏タブに切り替えたときに真っ白で手動更新が必要だった**: タブ復元時は選択タブのみロードしており、裏タブは初回表示時に LoadDirectoryAsync が呼ばれていなかった。RefreshIfNeededOnTabFocus で「未ロードの裏タブ」（パスはあるが Items が空）にフォーカスした場合も LoadDirectoryAsync を実行するようにし、裏タブ表示時に一覧が正しく表示されるようにしました。

## [0.4.26] - 2026-02-14 : アイコンビュー改善（サムネイル即表示・フリーズ解消）

### Added
（なし）

### Changed
- **アイコンビューをエクスプローラー風の仕様に統一**: 各ファイル間の余白を狭くし、文字サイズを 12pt に統一。非画像アイテムはアイコン領域とテキスト領域を固定行で分け、タイトルの開始位置を揃えました。長いファイル名は「...」で省略表示（2行まで表示しそれ以上は省略）。画像・非画像ともにカードサイズとマージンを詰め、一覧性を高めています。
- **アイコンビューの見た目・演出を改善**: 画像を主役としたギャラリーグリッド風に刷新しました。サムネイルはカード全面に表示（UniformToFill）、BitmapScalingMode を HighQuality に変更して視認性を向上。ホバー時のぼかし（DropShadowEffect）とカード拡大を廃止し、画像のみのスムーズなズーム（1.08倍）とアクセント枠強調、下部オーバーレイでファイル名・詳細をフェードイン表示する構成に変更。ファイル名は通常時非表示で、ホバー時に半透明黒オーバーレイ上に表示。小アイコンではオーバーレイを省略しツールチップで名称を確認できます。
- **アイコンビューで非画像ファイルの識別性を向上**: 画像以外のファイル（フォルダ・文書など）は、四角枠（白背景）と境界線を廃止して透明化。アイコンを中央に適切なサイズで配置し、ファイル名を常時アイコン下部に表示するように変更しました。画像は従来通りサムネイルタイル＋ホバーで名前表示、非画像は OS 標準に近いクリーンな見た目で即座に識別可能です。
- **アイコンビューでサムネイルをすぐに表示**: 画像フォルダを開いたときにサムネイルが 5～10 秒遅れて表示される問題を解消しました。ShellThumbnailService に非ブロックの GetThumbnailAsync を追加し、LoadIconsAsync を「第1パスでアイコン・TypeName のみ先に UI 反映」「第2パスで画像のみサムネイルを非同期取得し 10 件ずつ完了次第 UI に反映」する二段階に変更。一覧は開いた直後にアイコン表示され、サムネイルは数百 ms～1 秒程度で順次表示されます。

### Fixed
- **アイコンモードでファイル数が少ないとレイアウトが間延びする**: UniformGrid に `VerticalAlignment="Top"` を指定し、アイテムが少ない場合も上詰めで表示されるようにしました。ファイル数が多い場合と同じ密度感で表示されます。
- **アイコンモードでパス遷移時にフリーズ・異常終了（StackOverflowException）**: パンくずの `SizeChanged` → `UpdateVisibleSegments` → レイアウト更新の同期的な再入ループと、アイコン列数変更のレイアウト中 SetValue が原因と特定。修正: (1) `BreadcrumbOverflowBehavior` で `UpdateVisibleSegments` を `Dispatcher.BeginInvoke(Loaded)` で遅延実行しレイアウト中の再入を防止、(2) `UpdateIconColumnCount` で `IconColumnCount` の設定を `BeginInvoke(Loaded)` で非同期化、(3) `UpdatePathSegments` 末尾の `UpdateVisibleSegments` 呼び出しを Loaded で遅延しパス変更直後の連鎖を防止。
- **アイコンビューでお気に入り・他ペインからフォルダをドロップして新規タブを作成するとペインがフリーズしアイコンビューで固定される**: ドロップイベント内で同期的にタブ追加・選択変更を行うとドラッグ＆ドロップと UI 更新が競合するため、タブ追加を `Dispatcher.BeginInvoke(Loaded)` で遅延実行するよう変更。あわせて `SyncListItemsSources` で表示モードに応じたリストの ItemsSource を常に現在タブの View に同期するようにし、タブ切替・DataContext 変更時も正しく表示されるようにしました。

## [0.4.25] - 2026-02-14 : リファクタリング（処理効率・信頼性）

### Added
（なし）

### Changed
- **アイコンビュー状態の永続化**: Aペイン・Bペインそれぞれの表示モード（詳細／大・中・小アイコン）を settings.json に保存し、次回起動時に復元するようにしました。新規タブを開いた際も、そのペインの表示モードが自動で適用されます。
- **リファクタリング（処理効率と信頼性）**: TabItemViewModel の LoadIconsAsync を最適化し、リストのスナップショット作成（ToList）と辞書化の繰り返しを排除して O(N²) から O(N) へ計算量を削減しました。DeleteItems を非同期化してファイル削除時の UI フリーズを解消し、CreateShortcut での COM オブジェクト解放を徹底してリソースリークを防止しました。

### Fixed
（なし）

## [0.4.24] - 2026-02-14 : アイコンビュー安定化（更新・キー無効化）

### Added
- **画像ファイルのサムネイル表示**: アイコンビュー（大・中・小）で画像ファイル（jpg, png, gif, bmp, webp 等）を表示する際、エクスプローラと同様に実際の画像のサムネイルを表示するようになりました。WindowsAPICodePack-Shell ライブラリを用いて安定したサムネイル取得を実現しています。詳細表示では従来通りアイコン表示です。Box やオフラインファイルは負荷軽減のためサムネイル取得をスキップします。
- **ファイル一覧の表示モード切り替え**: ステータスバー右端のグリッドアイコンをクリックするとポップアップメニューが表示され、「詳細」「大アイコン」「中アイコン」「小アイコン」を選択できます。選択するとフォーカス中のペインの表示モードが切り替わります。アイコンビューはモダンなカードレイアウトで、ホバー時のアニメーションや角丸デザインを採用しています。
- **ペインのタブをまとめてお気に入りに追加**: タブを右クリックして「このペインのタブを全てお気に入りに追加」を選択すると、そのペインの全タブのパスを新規仮想フォルダにまとめてお気に入りに登録できます。フォルダ名はダイアログで入力してから保存します。
- **お気に入りのホイールクリックで新しいタブを開く**: お気に入り（ツリー・検索結果）の項目をホイールクリック（中クリック）すると、そのフォルダ（またはファイルの場合は親フォルダ）をアクティブペインの新しいタブで開くようになりました。
- **ファイル一覧からタブヘッダーへのフォルダドロップで新しいタブを開く**: ファイル一覧でフォルダをドラッグしてタブ表示部分（タブアイテム上または空白部分）にドロップすると、そのフォルダを新しいタブで開くようになりました。
- **F5 更新時の診断ログ**: 異常終了の原因特定のため、Refresh 処理の各段階で `[Refresh]` プレフィックス付きのログを出力するようにしました。ログは `logs` フォルダの日付別ファイルに記録され、最後に出力されたログから異常終了の発生箇所を特定できます。DispatcherUnhandledException と UnhandledException にはスレッド情報・IsTerminating 等のコンテキストを追加しました。

### Changed
- **サムネイル取得を WindowsAPICodePack-Shell に移行**: 自前の BitmapImage によるサムネイル生成を廃止し、実績のある WindowsAPICodePack-Shell ライブラリを用いたサムネイル取得に変更しました。専用 STA スレッド・LRU キャッシュ（上限 300 件）・フォルダあたり 200 件上限により、安定性とメモリ使用量を改善しています。
- **アイコンビューの VirtualizingWrapPanel を再導入（方針F）**: 方針A～E の段階的診断により、非仮想化 WrapPanel では大量アイテム時にスタック消費がレイアウト再帰で溢れることが判明。テンプレート構造の改善（ContentControl 廃止・MultiBinding 化）と CollectionChanged 最適化（SilentObservableCollection・DeferRefresh・IsRefreshInProgress ガード）が適用済みの状態で VirtualizingWrapPanel を再導入。`VirtualizingPanel.IsVirtualizing="True"`, `VirtualizationMode="Recycling"`, `CacheLengthUnit="Page"`, `CacheLength="1,1"` を設定し、画面外アイテムのコンテナ生成を抑制して StackOverflowException を根本解消。
- **表示モード切り替えのレイアウトを元に戻しました**: 「詳細」を個別ボタン（Rows3 アイコン）として左側に配置し、その右側に「表示モードを切り替え」ボタン（LayoutGrid アイコン）を配置しました。ポップアップメニューは「大アイコン」「中アイコン」「小アイコン」の3種のみ表示するよう変更しました。
- **アイコンビューでの更新・キーボード操作の無効化**: アイコンビュー（大・中・小アイコン）表示中は、F5 および一覧へのキーボード操作（矢印・Enter・Back・Delete・F2 等）を無効にしました。更新ボタンはグレーアウトされ、一覧の操作はマウスのみとなります。詳細表示に切り替えると従来通り利用可能です。これによりアイコンビュー時の StackOverflowException を誘発する操作を回避しつつ、サムネイル表示やフォルダ移動はそのまま利用できます。
- **検索結果フィルターバーの設定を永続化**: フォルダ・Excel・Word などのファイル種類フィルターの選択状態を settings.json に保存し、次回の検索時およびアプリ再起動後に自動で復元するようにしました。同じ傾向で検索することが多い場合に、毎回フィルターを設定し直す手間を省けます。

### Fixed
- **表示モードポップアップで Style 例外が発生する**: 表示モード切り替えポップアップのボタンに `ContextMenuItemStyle`（TargetType=MenuItem）を適用していたため、Button への型不一致で例外が発生していました。該当スタイルを削除し、インラインの Background/Foreground と ControlTemplate で表示するよう修正しました。
- **ファイル一覧からタブヘッダーへのフォルダドロップが動作しない**: `TabControlDragDropBehavior` がタブ並べ替え以外のデータ形式（`DataFormats.FileDrop` 等）でも `DragOver` / `Drop` を処理して `e.Handled = true` を設定していたため、`DockPanel` の `TabHeader_Drop` が呼ばれていませんでした。`"ZenithFilerTabItem"` 以外の場合は `e.Handled = false` にしてイベントを伝播させるよう修正しました。
- **左クリックドラッグでタブヘッダーにフォルダをドロップできない**: 左クリックドラッグ時に `DragDropEffects.Link` を許可していなかったため、タブヘッダー（`Link` を要求）へのドロップが拒否されていました。右クリックドラッグと同様に `Link` を許可するよう修正しました。
- **タブ右クリック時のコンテキストメニューエラー**: タブのコンテキストメニューに `Separator` を追加した際、`ItemContainerStyle`（MenuItem 用）が Separator に適用されて「型 'MenuItem'用のスタイルは、型 'Separator' に適用できません」エラーが発生する問題を修正しました。Separator を削除して解消しています。
- **インデックス設定の ComboBox で選択肢を選べない**: 「一定間隔でまとめて更新する」「フル再インデックス最短間隔」のドロップダウンで、選択肢をクリックしても反映されずデフォルトのままになる問題を根本的に修正しました。ComboBox を廃止し、RadioButton 式の選択 UI に変更しました。選択肢が一目で分かり、確実に選択が反映されます。
- **キーボードショートカットが効かなくなる**: `MainWindow_PreviewKeyDown` で修飾キー（Ctrl/Alt/Shift）が押されていない場合に `e.Handled = true` を設定していたため、`InputBindings` で定義されたショートカットキー（Ctrl+T, Ctrl+W, Ctrl+Shift+E など）が処理されなくなる問題を修正しました。修飾キーが押されている場合は `e.Handled` を設定しないように変更し、`InputBindings` が正常に動作するようになりました。
- **F2・Delete・Enter がたまに効かない**: ペイン内で一覧にフォーカスがない状態でキーを押した際、`PreviewKeyDown` で無条件に `e.Handled = true` を設定していたため `KeyDown` が発生せず、`ListView_KeyDown` で処理される F2（リネーム）・Delete・Enter 等が効かないことがありました。`KeyDown` イベントハンドラーを追加し、転送先で処理された場合のみ `e.Handled` を設定するように変更し、常に確実に動作するようになりました。
- **アイコンビューでフォルダ移動後にサムネイルが表示されない**: モード切替直後はサムネイルが表示されるが、フォルダ移動後は一切表示されない問題を修正しました。LoadIconsAsync 完了後に補完読み込みを実行するよう変更し、UI 更新を DispatcherPriority.Normal で確実に反映するようにしました。
- **更新時の異常終了**: フォルダ移動後にサムネイルが表示されない状態で更新（F5）するとアプリが異常終了する問題を修正しました。LoadDirectoryAsync の直列化（セマフォ）、Refresh の二重実行防止、例外のログ出力、OnRefreshCompleted の防御的コーディング（ToList によるスナップショット・try-catch）、ShellThumbnailService の例外ログ強化を実施しました。
- **F5 更新時の異常終了（追加対策）**: コレクション変更中の列挙による InvalidOperationException を防ぐため、OnRefreshStarting（SelectedItems の try-catch）、ApplyFocusAfterRefresh（list.Items の ToList スナップショット）、LoadThumbnailsForCurrentItemsAsync（Items の ToList スナップショット）、LoadIconsAsync（Items の ToList スナップショット）、UpdateSelectionInfo（selectedItems の try-catch とスナップショット）の各所で防御的コーディングを追加しました。
- **F5 更新時の異常終了（根本対策）**: WPF レンダリングスレッドでのネイティブクラッシュが原因と特定。サムネイル取得時に WindowsAPICodePack の COM オブジェクトとの依存を完全に切るため WriteableBitmap にピクセルデータをコピーしてから Freeze するよう変更。BitmapSource の妥当性検証（PixelWidth/PixelHeight > 0）をサムネイル取得・アイコン取得・ThumbnailOrIconConverter の各所に追加。アイコンビューの Image 要素に ImageFailed イベントハンドラを追加し、描画失敗時は Source を null にクリアして WPF レンダリングスレッドのクラッシュを防止。
- **アイコンビューで F5 更新時にクラッシュ（仮想化対策）**: アイコンビューの ListBox で仮想化が無効（`VirtualizingStackPanel.IsVirtualizing="False"`）だったため、フォルダ内の全ファイルの Image 要素が同時に生成・描画され、大量のサムネイル/アイコンの同時レンダリングで WPF レンダリングスレッドがネイティブクラッシュする問題を修正しました。`VirtualizingWrapPanel` NuGet パッケージ（v2.3.2）を導入し、仮想化を有効化（`VirtualizingPanel.IsVirtualizing="True"` + `VirtualizingPanel.VirtualizationMode="Recycling"`）。画面外の要素は生成されなくなり、メモリ使用量とレンダリング負荷が大幅に削減されます。BitmapScalingMode を HighQuality から LowQuality に変更し、レンダリングスレッドの負荷をさらに軽減しました。
- **F5 更新時の異常終了（診断強化+安全性強化）**: ログが非同期（Channel 経由）のためクラッシュ前にファイルに書き込まれず原因特定が困難だった問題を解消。F5 キーハンドラ・Refresh()・LoadDirectoryAsync() の全ログと App_DispatcherUnhandledException のログを `LogSync`（同期書き込み）に変更し、クラッシュ直前まで確実にログが残るようにしました。サムネイル取得の `WriteableBitmap` 構築を `new WriteableBitmap(bmp)` コンストラクタから `CopyPixels` + `WritePixels` 方式に変更し、COM オブジェクトのネイティブバッファへの暗黙的アクセスを排除。ThumbnailOrIconConverter に try-catch を追加し Frozen でない BitmapSource を除外するガードを追加。コンバーター例外が WPF レンダリングパイプラインに波及してネイティブクラッシュを引き起こすことを防止しました。
- **F5 更新時の異常終了（根本原因解消: StackOverflowException）**: Windows イベントビューアにより、クラッシュの正体が `System.StackOverflowException`（PresentationFramework 内の無限再帰）と判明。MergeItems で Items を個別に Add/Remove するたびに CollectionChanged が連続発火し、WPF のレイアウト再計算（MeasureOverride/ArrangeOverride）が再帰的に呼ばれてスタックが溢れていました。(1) `SilentObservableCollection` を新設し、MergeItems で `ReplaceAll` を使用して CollectionChanged を Reset 1 回のみに抑制。(2) ApplySort を `ICollectionView.DeferRefresh()` で囲み、SortDescriptions の Clear+Add をまとめて 1 回の Refresh に統合。(3) OnItemsViewCollectionChanged にリフレッシュ中ガード（`IsRefreshInProgress`）を追加し、MergeItems+ApplySort 実行中のフォーカス復元スケジュールを抑制。これにより CollectionChanged の連鎖的な発火が根絶され、StackOverflowException が解消されます。
- **アイコンビューの StackOverflowException（方針H-2: UniformGrid 化 + 再入防止）**: 4 ファイルでも F5 クラッシュが再現し、アイテム数と無関係な無限再帰と確定。ItemsPanel を `UniformGrid`（固定列数・幅非依存レイアウト）に置き換え。列数は `FileListBox.SizeChanged` で `availableWidth / itemWidth` から動的に計算。クラッシュモジュールが PresentationCore から ZenithFiler 自身に移行（P4=ZenithFiler, P7=660）したため、`SizeChanged → UpdateIconColumnCount → IconColumnCount 変更 → レイアウト → SizeChanged` の再入ループが原因と特定。修正: (1) `_isUpdatingIconColumns` 再入防止フラグで `FileListBox_SizeChanged` と `UpdateIconColumnCount` の再帰呼び出しを遮断、(2) 計算した列数と現在値が同じ場合は `IconColumnCount` を更新しない（不要なレイアウト invalidate を防止）。

## [0.4.23] - 2026-02-13 : お気に入り環境アイコン表示と2ペインタブ最新化

### Changed
- **2画面表示時のタブ最新化**: 2ペイン表示時はAペイン・Bペインの両方の表示タブをファイル変更の即時最新化対象とするように変更しました。従来はアクティブペインのタブのみ対象でした。
- **お気に入りのファイル表示に環境アイコンを追加**: お気に入りのツリーおよび検索結果で、ファイルの左側に環境（場所）アイコン（ローカル／ネットワーク／Box／SPO）を表示するようにしました。どのストレージのファイルか一目で判別できます。

### Fixed
- **お気に入りファイルの環境アイコンが縦方向にずれる**: 環境アイコンとファイルアイコンの垂直方向のセンタリングを揃えました。

## [0.4.22] - 2026-02-13 : タブの自動最新化と負荷軽減

### Changed
- **タブのフォルダ監視と最新化の最適化**: 表示中のタブのみ FileSystemWatcher の変更検知で即時更新し、裏のタブはタブがフォーカスされたタイミング（タブ切り替え・ペイン切り替え）で最新化するように変更しました。複数タブ利用時のシステム負荷を抑えつつ、利便性を維持します。

## [0.4.21] - 2026-02-13 : 検索結果の即時表示とUI改善

### Added
- **検索結果のファイル種類フィルタ**: 検索実行時にアドレスバーをフィルターバーに置換し、フォルダ・Excel・Word・PPT・PDF・TXT・EXE・BAT・JSON・画像・その他のチェックボックスで絞り込み可能にしました。「全選択」「全解除」ボタンで効率的にフィルタを切り替えられます。

### Changed
- **検索結果の即時表示**: インデックス検索時に、ファイルシステムやシェルAPIへの逐次アクセスをやめ、インデックス内の path・name・modified・is_dir から直接 FileItem を生成するようにしました。汎用アイコンは事前に2回取得するのみで、検索完了から結果表示までの待ち時間を大幅に短縮しています。ソートは全件追加後にまとめて適用するよう変更し、追加ごとの再ソート負荷を削減しました。
- **検索フィルターバーのモダンリデザイン**: フィルタートグルボタンをピル型（CornerRadius 10）に変更。ON時は薄青背景＋青色枠・文字で視認性を向上。OFF時は白背景＋グレー枠＋濃いグレー文字でボタンとして認識しやすく改善。ボタン間は Margin 3px、Padding 8,3 でほどよいゆとりを確保。
- **インデックス検索のシステムファイル除外**: インデックス作成時に、ユーザーが通常使用しないシステム関連ファイルやフォルダを自動的に除外するようにしました。除外対象: `$Recycle.Bin`, `System Volume Information`, `$WINDOWS.~BT` などのシステムフォルダ、`desktop.ini`, `Thumbs.db`, `NTUSER.DAT` などのシステムファイル、`~$` で始まる Office 一時ファイル、`.tmp`, `.temp`, `.bak`, `.swp` などの一時ファイル。

### Fixed
- **オプション設定ビューで選択済み項目にホバーした際に枠線が見づらくなる**: 選択済みの RadioButton にマウスホバーしたときも、選択状態を示す枠線が維持されるよう `OptionRadioButton` スタイルを追加し、コンテキストメニュー・検索の挙動・検索結果のパスクリック時・インデックス更新タイミングの各 RadioButton に適用しました。
- **オプション設定ビューで非選択項目の区分が見づらい**: 非選択の RadioButton にも薄い枠線（BorderBrush）を表示し、「ここが選択できる箇所である」ことが視覚的に明確になるようにしました。

## [0.4.20] - 2026-02-13 : フォーカス復帰時の応答性向上

### Added
- **お気に入りにファイルを登録**: お気に入りビューにフォルダに加えてファイルも登録できるようになりました。ファイル一覧でファイルを右クリックし「お気に入りに追加」、またはファイルをナビペインのお気に入りビューへドラッグ＆ドロップで登録できます。お気に入りからファイルをダブルクリックすると関連付けられたアプリで開き、「Aペインで開く」「Bペインで開く」では親フォルダを表示します。

### Changed
- **お気に入りの登録対象を拡張**: フォルダのみだった登録対象を、ファイルにも対応しました。AddPath・AddPathWithDialog・ドラッグ＆ドロップのいずれもファイルに対応しています。
- **フォーカス復帰時の応答性向上**: バックグラウンドからアプリにフォーカスを戻した際のフリーズや白画面を防ぐため、複数の改善を実施しました。GC の実行タイミングを 5 秒→60 秒に延長し、アクティブ復帰時は保留中の GC をキャンセル。GC モードを Forced から Optimized に変更し、LOH コンパクションは 5 分以上非アクティブ時のみ実行。FileSystemWatcher によるリフレッシュはアクティブタブを最優先に即時実行し、他タブは 500ms 間隔で順次実行するよう変更しました。
- **UI スレッドのブロック軽減**: Dispatcher.Invoke を InvokeAsync に置換（FavoritesViewModel、IndexSearchSettingsViewModel、TabItemViewModel、TreeViewDragDropBehavior、ShellContextMenu）。OwnerWindow 設定など必須ブロック箇所は Invoke のまま維持。
- **リソース管理の強化**: TabContentControl の Unloaded 時に DispatcherTimer の停止とイベント購読解除を実施。アプリ終了時に IndexService の Dispose を呼び出すようにしました。TabItemViewModel の Dispose で Items・SearchResults のクリア、_watcherCts の Cancel を追加しました。

### Fixed
- **フォーカス復帰時のフリーズ・白画面**: アプリにフォーカスを戻した際に一時的に動作しなかったり、真っ白になる現象を軽減しました。GC の最適化、リフレッシュの段階的実行、UI スレッドブロックの削減により、必要な時にいつでも使用できる信頼性を向上させました。

## [0.4.19] - 2026-02-12 : 初回起動スプラッシュとIME状態維持

### Added
- **初回起動スプラッシュ画面**: 初回起動時のみ、準備中であることと次回から素早く起動することを伝えるスプラッシュウィンドウを表示するようになりました。グローイングパルスリングのローディングアニメーション、バージョン表示、機能のランダム紹介（Tips）を表示します。

### Changed
- **スプラッシュ画面を初回起動時のみ表示に戻す**: 毎回起動時に表示していたスプラッシュを、初回起動時（settings.json が存在しない場合）のみ表示するように戻しました。2回目以降は MainWindow を直接表示します。
- **動的スプラッシュの爆速起動**: WPF-UI テーマなど重いリソースを AppResources.xaml に分離し、起動時は軽量な App.xaml のみ読み込んで動的スプラッシュを即座に表示するようにしました。スプラッシュ表示中に MainWindow 用リソースを遅延読み込みするため、起動完了時間はスプラッシュ有無で変化しません。静的スプラッシュ方式（ハイブリッド）は廃止しました。

### Fixed
- **起動時にIMEの状態が変わってしまう**: スプラッシュ表示時に InputMethod.IsInputMethodSuspended を設定し、MainWindow 表示時に ShowActivated=false でフォーカス遷移を抑制することで、起動前の IME 状態が維持されるようにしました。
- **インデックス作成時にローディングアニメーションが2個表示される**: 検索とインデックス作成が同時に走る場合、IsBusy 用のローダーと IsIndexing 用のローダーが両方表示されていた問題を修正しました。インデックス作成中は専用インジケータのみ表示するよう、BusyNotIndexingConverter を導入し、IsBusy インジケータを IsIndexing 時は非表示にしました。

## [0.4.18] - 2026-02-12 : マニュアルの独自コンテキストメニュー詳細

### Added
- **独自コンテキストメニュー**: ファイル一覧の右クリック時に、アプリ独自の軽量メニューかエクスプローラ互換メニューを選択できるようになりました。アプリ設定（基本設定）の「コンテキストメニュー」で、「アプリ独自メニュー（推奨）」／「エクスプローラ互換メニュー」を選択できます。独自メニューでは開く・コピー・削除・名前の変更・お気に入り・インデックス検索など基本操作を即表示し、Shift+右クリックまたはメニュー内の「エクスプローラのコンテキストメニューを表示する」でエクスプローラ互換メニューに遷移できます。デフォルトは独自メニューです。
- **テキストエディタで開く・ペインターで開く**: 独自コンテキストメニューに「テキストエディタで開く」（テキスト系ファイル）と「ペインターで開く」（画像系ファイル）を追加しました。OS で標準設定したアプリ（編集用の関連付け）で開きます。複数選択時は、すべてテキスト系またはすべて画像系の場合に各メニューが表示されます。

### Changed
- **右クリック時のコンテキストメニュー**: デフォルトでアプリ独自メニューを表示するように変更しました。体感のレスポンスを重視し、7-Zip などのシェル拡張が必要な場合は Shift+右クリックでエクスプローラ互換メニューを表示できます。
- **テキストエディタで開く**: 従来はファイルの拡張子ごとの既定アプリで開いていましたが、常に `.txt` の既定アプリ（OS の「既定のアプリ」で設定したメモ帳等）で開くように変更しました。HTML やその他テキスト系ファイルも、テキストエディタとして開く際は一貫して .txt の既定アプリが起動します。
- **マニュアルの独自コンテキストメニュー詳細**: MANUAL.md に「2-4. 独自コンテキストメニュー」セクションを追加しました。メニューの種類と切り替え、アプリ独自メニューの全項目一覧、表示位置とスタイル、エクスプローラ互換メニューについてを詳述しています。2-3 の右クリック関連記述は簡潔化し、2-4 へ誘導する形に変更しました。ドラッグ＆ドロップは 2-5 に繰り下げました。

### Fixed
- **テキストエディタで開くが .txt の既定アプリで開かない**: `.txt` の既定を変更している環境（EmEditor 等）で、`GetDefaultTextEditorPath()` が `txtfile` を固定参照していたため null となり、フォールバックで拡張子ごとの既定アプリ（例: .html なら Edge）が起動していました。HKCR\.txt の ProgId を動的に取得し、その ProgId の `shell\open\command` から実行ファイルを取得するよう修正しました。

## [0.4.17] - 2026-02-12 : マニュアル／更新履歴ビューの安定化とリファクタリング

### Added
- **リファクタリング提案書**: `docs/REFACTORING_PROPOSAL.md` を追加しました。機能劣化なく性能・信頼性を向上させる案を優先度付きで整理しています。

### Changed
- **マニュアル・更新履歴ウィンドウのレイアウト**: ウィンドウのデフォルトサイズを拡大（1100×800 → 1400×900）。左ペイン（目次）の幅を拡充（280 → 340px）し、長い見出しが途切れにくくなりました。左ペインのスクロールバーを右端に寄せるよう調整しました。
- **リファクタリング（性能・信頼性・保守性）**: 提案書に基づき実施。Markdown パイプラインのキャッシュ、FindVisualChild の共通化（VisualTreeHelperExtensions）、DocViewer の ScrollChanged 重複登録防止、例外処理のログ出力、検索ロジックの DocumentSearchHelper 分離、PostProcessDocument の ToList 削除、MainViewModel の TreeFolderOperationHandler 委譲、ManualTocItem の IconKind マッピング整理、TabItemViewModel の region 分割、ManualViewModel の fire-and-forget タスクへの ContinueWith で例外ログを追加しました。

### Fixed
- **更新履歴ページが表示されない**: ManualWindow を「更新履歴」タブで開いた際に空白となる、またはマニュアルから更新履歴に切り替えてもマニュアルの内容が表示され続ける問題を修正しました。初期表示を同期的にロードしてウィンドウ表示前にコンテンツを確保するようにし、パス探索に Environment.ProcessPath（EXE 配置先）を優先して追加、DispatcherPriority.ApplicationIdle で UI 反映を確実にしました。
- **PostProcessDocument の InvalidOperationException**: ドキュメント処理中に EnhanceCategoryHeading が Inlines.Clear() でコレクションを変更するため列挙例外が発生していた問題を修正しました。doc.Blocks の列挙前に ToList() でスナップショットを取得するようにしました。

## [0.4.16] - 2026-02-11 : インデックス更新とマニュアル拡充

### Added
- **インデックス検索の詳細マニュアル**: MANUAL.md に「4. インデックス検索のしくみとチューニング」セクションを追加しました。インデックス検索の概念、ライフサイクル、インデックス検索設定ビュー・アプリ設定の詳細、検索実行時の動き、鮮度警告バー、トラブルシューティング、技術的なしくみ（Lucene.NET・スレッド・例外・パフォーマンス・検索精度）を説明しています。
- **インデックス制御オプション**: インデックス検索時の挙動を制御するオプションを追加しました。オプション設定ビューに「インデックスの更新タイミングと負荷」「検索結果の鮮度」を追加。インデックスビューに「未インデックスを作成」「一時停止」「再開」「フル再構築を実行…」「インデックスの設定を開く」を追加しました。
- **フォルダ別インデックス操作**: インデックス検索設定のフォルダ一覧で右クリック時、「インデックスを再作成」でそのフォルダのインデックスを一度削除してから再作成（削除済みファイルを反映）、「インデックスを差分更新」で削除せずに新規・更新分だけを反映する操作を追加しました。
- **フル再インデックス最短間隔**: アプリ設定ビューの「インデックスの更新タイミングと負荷」内に、フル再構築の最短間隔（6時間／12時間／24時間）を選択するオプションを追加しました。
- **settings.json の自動補完**: 仕様変更前の `settings.json` を引き継いでいる場合、不足しているインデックス関連項目を自動で補完するマイグレーションを実装しました。
- **インデックス設定のベストプラクティス自動設定**: `settings.json` が存在しない場合、負荷を抑えたデフォルト（2時間間隔・省エネモード・並列2・ネットワーク低優先度）で自動設定します。

### Changed
- **インデックス更新タイミングの実装**: アプリ設定の「更新タイミング」が動作するようになりました。「変更を自動で取り込む」は FileSystemWatcher で即時差分更新。「一定間隔でまとめて更新する」は指定間隔ごとに登録フォルダ全体を再スキャン。「必要なときだけ更新する」は「未インデックスを作成」または「フル再構築…」操作時のみ更新。設定変更時は即座に反映されます。MANUAL の 3 モード説明を実装に合わせて修正。IndexService の Dispose で Interval タスクを確実に停止するようにしました。
- **パフォーマンスと負荷の実装**: 「省エネモードでインデックスを更新する」「ネットワークドライブは軽めに処理する」が動作するようになりました。省エネモードでは 100 件ごとの待機時間を長くして CPU・ディスク負荷を抑制。ネットワークドライブ（UNC・マップドドライブ）では負荷設定に応じてさらにゆっくり処理します。設定変更時は即座に反映されます。
- **用語の統一**: 「インデクス」を「インデックス」に統一しました。UI、マニュアル、CHANGELOG、コード内コメント・通知メッセージすべてで表記を揃えています。
- **検索結果の鮮度の実装**: アプリ設定の「検索結果の鮮度」が動作するようになりました。「できるだけ最新の状態を保つ（鮮度優先）」をオンにすると、インデックス検索時に全ターゲットフォルダの差分更新・未インデックス作成をバックグラウンドで開始し、検索結果と並行してインデックスを更新します。「インデックスが古い可能性があるときにメッセージを表示する」をオンにすると、未インデックスまたは作成中のフォルダがある場合に検索結果ビュー上に注意メッセージを表示します。
- **オプション設定ビューの構成**: ホームと検索結果の表示先を統合し、インデックス関連セクションを追加しました。タブは用いず、シンプルな区分けで配置しています。
- **オプション設定ビューのカテゴリ見出し**: 「1. 基本設定 (Home &amp; General)」「2. 検索 (Search UI)」「3. インデックス (Indexing &amp; Freshness)」の3カテゴリで区切り、マニュアル見出し風（左青バー・薄い背景）のデザインで階層を明確にしました。カテゴリ間の余白をさらに広げ、区分を目視で判別しやすくしました。
- **オプション設定ビューの余白調整（B案）**: カテゴリ間40px、セクション間20px、ラジオ・チェックボックス項目間12pxに統一。枠内パディングを10pxに拡大し、説明文とコントロールの行間を広げて、モダンで見やすいレイアウトに調整しました。
- **インデックスビュー**: 対象フォルダの登録・削除に加え、インデックスの状態表示、「未インデックスを作成」・一時停止・再開ボタン、およびメニュー（フル再構築・設定を開く）を追加しました。
- **インデックスビューの手動更新挙動の明確化**: 「未インデックスを作成」ボタンは、まだ一度もインデックスされていないフォルダだけを対象に今すぐインデックスを作成するよう仕様を明確化し、対象フォルダがない場合は「未インデックスのフォルダはありません」と通知を表示するようにしました。

### Fixed
- **検索結果の鮮度まわりのエラー・過剰警告の改善**: 鮮度警告を「未インデックスのフォルダがある場合のみ」に限定し、作成中のみの一時状態では表示しないようにしました。また、鮮度優先モードでバックグラウンド実行する差分更新の例外を適切に処理し、UnobservedTaskException などの未処理例外を防ぐようにしました。
- **鮮度警告バーの XamlParseException**: PackIconLucide の Kind に無効な `AlertTriangle` を指定していたため、検索結果タブ表示時に「AlertTriangle is not a valid value for PackIconLucideKind」が発生していました。Lucide の正しいアイコン名 `TriangleAlert` に修正しました。
- **鮮度警告バーに閉じるボタンを追加**: 警告メッセージをユーザーが手動で閉じられるように、×ボタンを追加しました。
- **アプリ設定ビューのスクロールバー**: アプリ設定ビューの内容が画面下部に表示されない問題を修正しました。ScrollViewer を追加し、コンテンツが収まらない場合は縦スクロールで確認できるようにしました。
- **文字欠けの修正**: アプリ設定のチェックボックス（省エネモード、ネットワークドライブ、検索結果の鮮度）の長い文言が欠けないよう、TextWrapping で折り返すようにしました。
- **インデックス作成のタイミング不具合**: フォルダ追加時に特定の条件下でスキャンが早期 return し、UI が「未作成／作成中」のまま固まる問題を修正しました。親ルート配下としてカバー済みの場合は MarkAsIndexed で UI を同期、追加時・「未インデックスを作成」押下時・IsIndexing 変更時に RefreshItemsStatus を呼んで IndexService の状態と表示を一致させるようにしました。デバッグ用に早期 return の理由をログ出力するようにしました。
- **インデックスビュー「インデックスの設定を開く」が反応しない不具合**: メニュー（⋯）内の「インデックスの設定を開く」をクリックしてもアプリ設定ビューに切り替わらなかった問題を修正しました。Menu を ContextMenu に置き換え、PlacementTarget 経由で Command をバインドするように変更し、左クリックでメニューを開けるようにしました。
- **「インデックスの設定を開く」時のスクロール位置**: アプリ設定ビューに切り替えた際、「3. インデックス」見出しがナビペインのトップに来るよう ScrollToVerticalOffset でスクロールするように改善しました。

## [0.4.15] - 2026-02-11 : 検索デフォルトとアプリ設定マニュアル

### Added
- **検索の挙動（3パターン）**: アプリ設定ビューで検索実行時の表示先を選択できます。「同一ペイン・新規タブ」（Enter で新規タブ）、「同一ペイン・現在タブで即時検索」（入力即検索、Enter 不要）、「反対ペイン・新規タブ」（Enter で反対ペインに表示、1ペイン時は2ペインへ自動切替）。
- **検索結果のパスクリック時の挙動（3パターン）**: アプリ設定ビューで検索結果一覧のパス列をクリックした際の表示先を選択できます。「同一タブで表示」（検索結果ビューから通常一覧に切り替え）、「同一ペイン・新規タブ」（従来どおり）、「反対ペイン・新規タブ」（1ペイン時は2ペインへ自動切替）。
- **反対ペイン・新規タブ時の検索バークリア**: 反対ペインに検索結果を表示した際、検索した側のペインの検索バーを自動でクリアするようにしました。
- **アプリ設定マニュアルへの詳細解説**: 「検索の挙動」「検索結果のパスクリック時の挙動」について、各オプションの動作と向いている用途をマニュアルに追記しました。

### Changed
- **検索の挙動・パスクリック時の挙動のデフォルト**: 両方とも「同一ペイン・新規タブ」をデフォルトとする方針を明示しました。
- **検索結果パス「反対ペイン・新規タブ」時のフォーカス**: 検索結果のパス列クリックで反対ペインに新規タブを開いた際、新規タブのファイル一覧にフォーカスが当たるようにしました。
- **インデックス作成の順次実行**: 複数フォルダを登録している場合、従来は並列でスキャンしていたためステータスバーのメッセージが目まぐるしく切り替わり、サーバ負荷も高くなっていました。これを解消するため、インデックス作成は1件ずつ順次実行するよう変更しました。
- **アプリ設定ビューの固定幅**: アプリ設定ビューを表示時、インデックス検索設定・参照履歴・ツリービューと同様にナビペインが固定幅（340px）に拡張されるようになりました。
- **アプリ設定ビューの入力欄レイアウト**: A/B ペインのホームアドレス入力欄がビュー幅いっぱいに伸びるよう、StackPanel を Grid に変更し、入力欄に残り幅を割り当てました。
- **アプリ設定のショートカット**: `Ctrl+Shift+0` は Windows の入力言語切替に予約されるため、アプリ設定ビューのショートカットを `Ctrl+Shift+O`（オー）のみに統一しました。

## [0.4.14] - 2026-02-11 : アプリ設定ビュー・ホームボタン

### Added
- **インデックス検索設定のフォルダ追加をツリーから選択**: 「フォルダを追加」ボタン押下時に、パス入力ダイアログの代わりにフォルダツリーを表示するダイアログが開き、ツリーからフォルダを選択して直接登録できるようになりました。
- **アプリ設定ビュー**: ナビペイン右側のアイコン群（マニュアルの左）にアプリ設定ボタンを追加。`Ctrl+Shift+O` で切り替え可能。Aペイン・Bペインそれぞれのホームアドレスを設定できます。
- **ホームボタン**: Aペイン・Bペインのツールバー「場所」グループにホームボタンを追加。設定したホームアドレスへワンクリックで移動できます。未設定時は A ペインはデスクトップ、B ペインはダウンロードへフォールバックします。

## [0.4.13] - 2026-02-11 : 日本語検索精度改善・name_raw
### Changed
- **日本語検索の精度改善**: インデックス検索（通常検索・インデックス検索の両方）において、日本語キーワード（例：「自由自在」）の検索精度を改善しました。デフォルト演算子を OR から AND に変更したことで部分一致（「自由」だけ、「自在」だけを含むファイル）の過多を解消し、パースエラー時のフォールバックを、トークン化されずヒットしないワイルドカードから、アナライザーでトークン化した PhraseQuery 構築に変更しました。
- **name_raw フィールドによる検索強化**: インデックスにトークン化しない `name_raw` フィールドを追加し、パースエラー時のフォールバックでワイルドカード検索を行うようにしました。これにより日本語ファイル名の部分一致検索がより確実になります。既存のインデックスを有効活用するには、インデックス検索設定で該当フォルダを一度削除してから再度追加するか、インデックスをクリアして再作成してください。
- **検索結果にフォルダを含める**: インデックス検索および通常検索の結果に、ファイルだけでなくフォルダも表示されるようになりました。既存のインデックス済みフォルダでフォルダを検索結果に含めるには、インデックス検索設定で該当フォルダを一度削除してから再度追加してください。
- **検索結果の並び順**: 検索結果は更新日時降順（新しい順）で表示されるようになりました。

## [0.4.12] - 2026-02-11 : エラーログ出力
### Added
- **エラーログの出力**: 予期せぬエラー（DispatcherUnhandledException）、致命的なエラー（UnhandledException）、バックグラウンドタスクの未処理例外（UnobservedTaskException）の発生時に、例外の型・メッセージ・スタックトレース・内側の例外をログファイルに出力するようにしました。

## [0.4.11] - 2026-02-11 : お気に入りロック時の許可操作拡張
### Changed
- **お気に入りロック時の許可操作拡張**: お気に入りビューをロック中でも、お気に入り登録（★ボタン・ドロップ）、名前の変更、概要の変更が行えるようになりました。仮想フォルダ追加・削除・並び替えは従来どおりロックで制限されます。
- **アドレスバーのボタン配置**: お気に入り登録ボタン（★）をアドレスバー右側・コピーボタンの左に移動、パスを編集ボタン（鉛筆）をアドレスバー左端に移動しました。
- **インデックス作成の再開時における進捗表示**: 中断後に再開した場合、前回までにインデックス済みの件数からカウントを続けて表示するようにしました。途中から再開したことが分かるようになります。
- **インデックス作成の即時反映**: 複数フォルダを登録している場合、各フォルダのインデックス作成が完了するたびにナビペインの「インデックス検索設定」一覧へ即座に反映されるようになりました。これまで通り、すべての対象が完了したときにも反映されます。

### Fixed
- **インデックス検索設定ビューでの件数表示の文字切れ**: ナビペインの「インデックス検索設定」一覧で、インデックス作成完了件数（例: 「5,050 件」）がナビ幅が狭い場合に文字切れする問題を修正しました。ステータス列を左側に配置し固定幅（75px）を確保、フォルダ名列を Auto にするレイアウトに変更し、常に件数が全体表示されるようにしました。
- **アプリ起動時の XAML 型変換エラー**: インデックス検索設定ビューの GridViewColumn に無効な `Width="*"` を指定していたため、起動時に `TypeConverterMarkupExtension` 例外が発生していました。GridViewColumn は `Auto` と数値のみ対応するため、上記レイアウト変更に合わせて修正しました。

## [0.4.10] - 2026-02-11 : インデックス管理強化・検索結果タブ即時検索の復元
### Added
- **インデックス作成の中断・再開**: ステータスバー右側のインデックス作成中アイコン（ぐるぐる）をクリックすると確認ダイアログを表示し、中止を選択した場合にインデックス作成を中断できるようにしました。インデックス作成済みルートを永続化し、アプリ再起動後に未完了分を自動的に再開するようにしました。
- **インデックス作成結果の通知**: フォルダのインデックス作成が完了した際、ステータスバーに「〇〇件をインデックスに追加しました」と件数を表示するようにしました。
- **インデックス総件数の表示**: ナビペインの「インデックス検索設定」ビューに、インデックスに登録されているファイルの総件数を表示するようにしました。作成完了時やビュー表示時に自動更新されます。
- **インデックス作成中の進捗表示**: インデックス作成中、ステータスバーに「インデックス作成中: 〇〇件 (フォルダ名)」と進捗が表示されるようにしました。100件ごとに更新されます。
- **フォルダ別の状態・件数表示**: ナビペインの「インデックス検索設定」一覧で、各フォルダごとに「作成中」「完了」「未作成」の状態と、インデックス済みの場合は件数を表示するようにしました。

### Changed
- **内部リファクタリング（安定性・パフォーマンス）**: Dispatcher.Invoke を InvokeAsync に置き換え UI スレッドのブロックを解消、検索結果の UI 更新をバッチ化（50件ごと）して大量ヒット時の描画負荷を軽減、参照履歴の取得・検索履歴のクリーンアップ処理を非ブロッキング化および一括削除に変更、Levenshtein 距離計算のメモリ使用量を O(n×m) から O(m) に削減、タブ破棄時の CancellationTokenSource 解放および AppActivationService イベント購読解除の漏れを修正しました。
- **インデックス作成中の検索対応**: インデックス作成中でも、100件ごとに Commit を実行することで、作成済み分が検索結果に部分的にヒットするようになりました。
- **検索結果タブの即時検索**: 一覧タブでは Enter 押下で新規タブに検索結果を表示する従来どおりの動作を維持しつつ、**検索結果タブ**では入力と同時に即時検索が実行される仕様に戻しました。

### Fixed
- **インデックスが再作成されない不具合**: インデックス作成の中断機能追加時に導入した「インデックス作成済みルートの永続化」（`indexed_roots.json`）と、実際の Lucene インデックスファイルとの整合性チェックが行われていなかったため、インデックスファイルが存在しない状況でもインデックス済みと判定されて再作成がスキップされる不具合を修正しました。起動時に Lucene インデックスの存在を検証し、不整合があればキャッシュをクリアして再インデックスが走るようにしました。また、インデックス検索対象フォルダの削除時にインデックス済み情報もクリアされるようにし、キャンセル済みトークンで `Task.Run` が即時例外を投げるケースの例外処理も追加しました
- **Release ビルドの NuGet 復元エラー**: `Microsoft.NETCore.App.Runtime.win-x64` 等のバージョン 8.0.24 が NuGet に存在しないため復元に失敗していた問題を、`RuntimeFrameworkVersion` を 8.0.23 に固定して解消しました。
- **インデックス作成中のぐるぐるアイコンが表示されない不具合**: インデックス作成処理がバックグラウンドスレッドで実行されるため、`App.Notification.IsIndexing` の更新が UI スレッド外で行われ、WPF のバインディングが反映されていませんでした。`UpdateIsIndexingOnUiThread` ヘルパーを追加し、`Dispatcher.BeginInvoke` 経由で UI スレッド上でプロパティを更新するように修正しました。

## [0.4.9] - 2026-02-11 : パスコピー機能・検索UI改善・ショートカットキー修正
### Added
- **アドレスバーのパスコピー機能**: アドレスバーの右端に「パスをコピー」ボタンを追加しました。現在のフォルダのフルパスをワンクリックでクリップボードにコピーできるようになりました。

### Changed
- **Release ビルドの出力フォルダ**: `dotnet publish -c Release` 実行時、出力先をバージョン付きフォルダ名（例: `ZenithFiler_v0.4.9`）に変更しました。
- **検索の実行タイミング**: 検索バーへの文字入力時に即座に新規タブを開いて検索を開始する動作をやめ、**Enter キーを押したときのみ検索を実行**するように変更しました。これにより、検索したいキーワードを途中まで入力できない問題を解消しました。
- **検索ボックスのプレースホルダー表示**: 検索バーが空欄でフォーカスがない場合に、現在の検索モードに応じた案内テキスト（「フォルダ以下を検索...」等）を表示するようにしました。フォーカスを合わせるか文字を入力すると自動的に非表示になります。
- **ビルド構成の調整**: 開発時の `dotnet run` ではフレームワーク依存実行とし、Release ビルド時のみ self-contained／単一ファイルで発行するように変更しました（ランタイムパックの取得エラーを回避）。
- **タブのアクティブ状態の視認性向上**: アクティブなタブにマウスカーソルを合わせた際、背景色が変化してアクティブ状態かどうかが分かりづらくなる問題を解消するため、アクティブ時は常に黒背景を維持するように変更しました。
- **ツールバーのレイアウト改善**: ファイルペイン上部のボタン群を機能別（ナビゲーション・場所・作成・管理・表示）の5グループに再編し、セパレーターを追加して誤操作を防止しました。
- **CHANGELOG の記録ルール**: バージョン管理規約（versioning-rules）に「区分毎に追記するルール」を明文化し、`[Unreleased]` 内の重複していた `### Changed` を統合して区分ごとに整理しました。

### Fixed
- **ショートカットキーが無効になる不具合の修正**: ツールバーのボタン操作などでフォーカスがファイルリスト外にある際、ショートカットキー（Ctrl+C, Ctrl+V, F2等）が正しく機能しなくなる問題を修正しました。
- **ペイン切り替え・フォーカス管理の改善**: タブの切り替えやペイン・タブ内容領域のクリック時に、より確実にファイルリストへフォーカスを移すようにし、常にショートカットキーが有効な状態を維持するように改善しました。

## [0.4.8] - 2026-02-10 : お気に入りパス未検出時の自動確認・削除機能の追加
### Added
- **ブラウザからのショートカット保存**: ブラウザから URL をドラッグ＆ドロップして `.url` ファイル（インターネットショートカット）として保存できる機能を追加しました。ファイルリストおよびパンくずリストへのドロップに対応しています。

### Changed
- **お気に入りパス未検出時の対応**: お気に入りを開く際、またはお気に入りをドラッグ＆ドロップして移動・タブ追加する際、対象のパスが見つからない場合に、お気に入りから削除するかをユーザーに確認するメッセージを表示するようにしました。削除を選択した場合は、お気に入りロック状態に関わらず削除が実行されます。

### Fixed
- **リネーム時のドット処理の改善**: ファイル名のリネーム時に、名前に「.」（ドット）が含まれていても、その後の文字列が拡張子として誤認されて消去されないように修正しました。
- **シェル経由の新規フォルダ作成時のフォーカス対応**: エクスプローラーのコンテキストメニューから「新しいフォルダ」を作成した際、自動的にそのフォルダを選択・フォーカスし、リネーム（名前の変更）を開始するように改善しました。
- **複数項目のドラッグ＆ドロップ対応**: ファイルリストで複数項目を選択してドラッグした際、選択されているすべてのファイル・フォルダがコピー／移動の対象となるように修正しました（`SelectionMode` を `Extended` に設定）。
- **URLショートカット作成時のエラー表示修正**: ブラウザからURLをドロップしてショートカットを作成する際、バックグラウンドスレッドからUI通知を呼び出していたために発生していたエラー（例外）を修正しました。また、作成完了メッセージが一覧更新メッセージで上書きされないよう通知順序を調整しました。
- **URLドロップ時のタイトル取得改善**: ブラウザからの URL ドロップ時に、可能な限りページのタイトルをファイル名として採用するように改善しました。また、ドロップデータの解析処理を強化し、特定のブラウザやデータ形式でも安定して URL が抽出できるようにしました。
- **ショートカット作成ロジックの刷新**: ショートカット（.lnk）の作成に Windows 標準の `WScript.Shell` (COM) ではなく `Vanara.Windows.Shell` ライブラリを採用し、より安定した作成とリソース管理を実現しました。
- **ブラウザショートカット保存の不具合修正**: ブラウザからの URL ドロップ時に URL が正しく抽出されない問題（テキスト優先やエンコーディングの問題）を修正し、保存後に一覧が自動更新されない問題を解決しました。

## [0.4.6] - 2026-02-09 : ナビペインの表示修正と画面分割ショートカットの追加
### Added
- **画面分割のショートカットキー**: 1画面モード（`Ctrl + Shift + Q`）と2画面モード（`Ctrl + Shift + W`）を切り替えるショートカットキーを追加しました。ステータスバーのボタン操作と同様に素早くレイアウトを変更できます。

### Changed
- **検索結果からの遷移動作変更**: 検索結果のフォルダを開く（ダブルクリック）、またはファイルのパスをクリックした際、現在のタブではなく**新しいタブ**でフォルダを開くように変更しました。これにより、検索結果を残したまま複数のフォルダを確認できるようになります。

### Added
- **検索結果の別タブ表示**: 検索バーに文字を入力した際、現在のタブを上書きせず、自動的に新しいタブを開いて検索結果を表示するようにしました。
- **検索タブのタイトル表示**: 検索結果を表示しているタブのタイトルに、検索キーワードが表示されるようにしました（例：「検索: キーワード」）。

### Fixed
- **ナビペインの表示重複を修正**: お気に入り・ツリー・参照履歴の各ビューにおいて、下部の操作アイコン群（検索モード切替・ロック・削除ボタン等）がリストのコンテンツと重なって表示される問題を修正しました。グリッドレイアウトに行を追加してアイコン専用の領域を確保し、コンテンツが隠れないように改善しました。

## [0.4.5] - 2026-02-09 : 検索結果の利便性向上と操作感の微調整
### Added
- **検索結果一覧のソート機能**: 検索結果のヘッダーをクリックすることで、名前、場所、パス、更新日時、サイズによる並び替えができるようにしました。通常のファイル一覧と同様にソートアイコンも表示されます。
- **インデックス作成中インジケータ**: フォルダのインデックス作成がバックグラウンドで進行している間、ステータスバーのログアイコンの左側に回転するアニメーションアイコンを表示するようにしました。これにより、全文検索用のインデックスが現在構築中であることを視覚的に確認できるようになります。

### Changed
- **検索結果一覧の列構成と優先度**: 検索結果リストの列順序を「名前・場所・パス・更新日時・サイズ」に変更しました。また、ウィンドウ幅が狭くなった際の非表示優先度を見直し、サイズ→更新日時→場所→名前の順に非表示になり、パス列が最後まで残るように調整しました。名前列の幅は固定（240px）とし、余ったスペースはパス列で調整されるようにしました。
- **ファイルリストのコンテキストメニュー変更**: ファイルやフォルダを右クリックした際に、アプリ独自のメニュー（削除・エクスプローラーメニューを表示）を経由せず、直接 Windows 標準のコンテキストメニューを表示するように変更しました。これにより、ワンアクションでプロパティやその他のシェル拡張機能にアクセスできるようになりました。
- **検索結果一覧の列幅調整**: 検索結果リストの列幅をマウスドラッグで自由に調整できるようにしました。また、境界線をダブルクリックすることで列幅の自動調整（AutoFit）も可能です。手動で調整を行うと、ウィンドウ幅に合わせた自動調整モードは解除されます。

### Fixed
- **検索結果一覧の列幅自動調整**: 検索結果ビューにおいて、通常ファイルリスト用の列幅調整ロジックが誤って適用され、名前列が意図せず伸縮してしまう問題を修正しました。検索結果モードを正しく判定し、名前列固定・パス列可変の挙動が確実に適用されるようにしました。
- **列の自動整列（アダプティブ列）の修正**: 「列を自動調整」ボタンをOFFにしても、ウィンドウ幅変更時などに勝手に列幅が調整されてしまう問題を修正しました。ボタンがOFFの場合は自動調整ロジックが完全にスキップされ、手動で設定した幅が維持されるようになります。
- **ファイルリストの右クリック修正**: ファイルやフォルダを右クリックした際に、選択項目のコンテキストメニューではなくフォルダの背景メニュー（プロパティ等）が表示されてしまう問題を修正しました。イベントの伝播（バブリング）を適切に制御し、アイテム上では項目用メニューが、空きスペースでは背景用メニューが正しく表示されるように改善しました。
- **操作ボタンのトグル不具合**: 「列を自動補正」などのトグルボタンが、1回目のクリックでフォーカスを奪われて正常に反応せず、2回押さないと切り替わらない問題を修正しました。ペイン活性化時のフォーカス移動ロジックを改善し、ボタン操作を妨げないようにしました。
- **検索結果一覧 of ソート表示**: 検索結果リストのヘッダーをクリックしてもソートアイコンが表示されず、機能していないように見える問題を修正しました。
- **検索結果一覧の列幅自動調整（機能不全）**: 検索結果ビューにおいて「列を自動調整」ボタンが機能せず、列幅が固定のままになっていた問題を修正しました。検索結果表示中もウィンドウ幅に応じて、優先度の低い列（サイズ・更新日時・場所）が自動的に非表示になり、パス列が見やすく調整されるようになりました。

## [0.4.4] - 2026-02-09 : 通常検索の高速化とインデックス作成の効率化
### Fixed
- 検索バーをマウスでクリックした際に、一瞬履歴が表示された後フォーカスが外れてしまう問題を修正。

### Changed
- **通常検索のパフォーマンス改善**: 検索実行時のインデックス更新フローを刷新しました。
  - **既存インデックス優先**: 更新完了を待たずに、既存のインデックス情報を利用して即座に検索結果を表示する「超高速表示」に対応しました。
  - **差分更新と再検索**: インデックスの更新が必要な場合のみバックグラウンドで行い、完了後に再検索して結果に差分があれば自動更新する方式に変更しました。
  - **更新キャッシュ**: セッション内で一度スキャンしたフォルダは、ファイル変更がない限り再スキャンをスキップするようにし、繰り返し検索時のI/O負荷を大幅に削減しました。
- **検索エンジンの日本語解析精度の向上**: Lucene.NET の日本語解析器を `CJKAnalyzer` から `JapaneseAnalyzer` (Kuromoji) へ切り替えました。これにより、日本語の単語境界を正しく認識できるようになり、「インデックス フィルタ」といった複数ワード検索時の精度が向上しました。

### Fixed
- **インデックス検索設定の削除機能修正**: 登録済みフォルダを右クリックメニューから削除できない問題を修正しました。バインディング設定と ViewModel の実行条件を改善し、項目が選択されていない状態からでもコンテキストメニューからの削除を可能にしました。
- **更新履歴/マニュアルの目次ナビゲーション修正**: 左ペインの目次を選択した際に右ペインの該当箇所へジャンプしない問題を修正しました。原因となっていた ID 生成ロジックの不一致（日付除去処理の有無）と、見出しテキスト取得時の装飾無視を改善しました。
- **更新履歴の概要表示の不具合修正**: `CHANGELOG.md` から日付を除去する際に概要（Summary）まで削除されていた問題を修正しました。また、目次生成処理の順序を最適化し、情報の欠落を防止しました。

### Added
- **インデックス作成時の負荷軽減**: 大規模なフォルダやネットワークドライブのインデックス作成時に、接続先への負荷を抑えるためのスロットリング（一定件数ごとの微小待機）を導入しました。
- **インデックス作成のキャンセル対応**: フォルダ検索中に別の場所へ移動したり検索をキャンセルした際、進行中のインデックス作成処理も即座に中断されるようにし、不要なI/O負荷を防止しました。
- **インデックス対象外フォルダの自動スキップ**: `.git`, `node_modules`, `.vs`, `obj`, `bin` などの大規模なプロジェクトフォルダや中間フォルダを自動的にインデックス対象から除外し、作成時間の短縮と負荷軽減を図りました。
- **AI設定の最適化**: `.cursorignore` を作成し、ビルド成果物、依存ライブラリ、バイナリファイル、ログ、キャッシュなどをAIの読み込み対象から除外しました。これにより、AIによる検索や分析の効率と精度が向上します。

## [0.4.3] - 2026-02-08 : 更新履歴の概要表示に対応
### Added
- **更新履歴の概要表示**: 更新履歴の各バージョンの右側に、そのバージョンの対応概要を表示するようにしました。これにより、目次ペイン（左側）だけで更新内容の全体像を把握できるようになります。`CHANGELOG.md` の見出しに `## [0.4.3] - 2026-02-08 : 概要` の形式で記述することで表示されます。

### Fixed
- **マニュアル/更新履歴のUI改善**: 目次ペインの項目を選択した際、アイコンが背景色と同色になり見えなくなる問題を修正しました。選択時はアイコンも白色に切り替わるよう改善しました。
- **更新履歴のフィルタリング不具合の修正**: `[Unreleased]` セクションのタイトルだけでなく、その配下のコンテンツ（Changed/Added等）も正しく非表示になるように正規表現を改善しました。

### Changed
- **ビルド出力フォルダ名の変更**: `dotnet publish` 実行時の出力先フォルダ名を、デフォルトの `publish` から `ZenithFiler` に変更しました。これにより、配布・パッケージング時のフォルダ名変更の手間が解消されます。

## [0.4.2] - 2026-02-08 : インデックス保存場所の変更、表示フィルタリング改善
### Added
- **インデックス関連ファイルの集約**: 検索インデックス（Lucene）と履歴データベース（SQLite）の保存場所を、`%AppData%` からアプリ実行フォルダ内の `index` フォルダに変更しました。これにより、アプリのフォルダを丸ごとコピーするだけで設定・検索インデックス・履歴のすべてを移行できるようになりました（旧環境からのデータは初回起動時に自動移行されます）。
- **更新履歴の表示フィルタリング**: アプリ内の更新履歴画面で `[Unreleased]` セクションを表示しないように変更しました。

### Documentation
- **ビルド構成の解説追加**: ビルド後のフォルダ構成（backups, logs 等）および主要ファイル（exe, dll, json）の役割についての説明をマニュアルに追記しました。
- **検索機能の詳細化**: 通常検索（フォルダ内）とインデックス検索（登録フォルダの横串検索）の目的の違いを明確化し、インデックスの保存場所や構成についての技術的な詳細をマニュアルに追記しました。

## [0.4.1] - 2026-02-08 : 通常検索へのLucene導入、リアルタイム更新
### Changed
- **通常検索のLucene.Net統合**: ファイルペインでの検索処理を従来の再帰探索から Lucene.Net インデックス検索へ完全に切り替えました。検索実行時にカレントフォルダ以下のインデックスを自動生成・更新し、高速な検索を実現しています。
- **インデックスのリアルタイム更新**: ファイルシステムの変更通知（作成・削除・リネーム）を受け取り、インデックスをバックグラウンドで部分更新するロジックを実装しました。

## [0.4.0] - 2026-02-08
### Added
- **標準検索の高速化（Lucene.Net統合）**: ファイルペインの通常検索を Lucene.Net を用いたインデックス検索に置き換えました。カレントフォルダ以下のインデックスを必要なタイミングで自動構築・更新し、大規模なフォルダでも瞬時に検索結果が表示されるようになりました。
- **リアルタイム・インデックス同期**: ファイルの作成・削除・リネーム・変更をリアルタイムに検知し、検索インデックスを自動更新するようにしました。これにより、明示的な再検索なしで常に最新の状態から検索可能です。
- **高速全文検索エンジンの基盤導入**: Lucene.Net ライブラリ（v4.8.0-beta）を導入し、インデックス作成・検索を行うためのサービス基盤（IndexService）を実装しました。日本語ファイル名に対応するため CJKAnalyzer を採用しています。
- **ナビペインにインデックス検索設定ビュー**: 4 つ目のビューとして「インデックス検索設定」(Ctrl+Shift+4) を追加しました。インデックス検索の対象にするフォルダを登録・削除でき、一覧は設定に保存され次回起動時に復元されます。「フォルダを追加」（パス入力）と「現在のフォルダを追加」で登録できます。

### Fixed
- **シェルコンテキストメニューの「削除」不具合**: OS標準の右クリックメニュー（シェルメニュー）から「削除」を選択しても、アプリ側の削除処理が呼び出されず機能していなかった問題を修正しました。verb が取得できない場合でもメニューのテキスト（「削除」/「Delete」）から判定するようにし、確実にアプリ側で制御（ごみ箱への移動等）が行われるようにしました。

## [0.3.96] - 2026-02-08
### Changed
- **検索履歴の雷アイコン**: 履歴一覧でインデックス検索を示す⚡アイコンを、検索バーと同じデザイン（IndexSearchIconBrush＋発光エフェクト）に揃えました。

## [0.3.95] - 2026-02-08
### Fixed
- **検索履歴が新規登録されない問題（保存時フォールバック）**: 保存に失敗した場合に、マイグレーションを再実行してから保存を1回だけ再試行するようにしました。起動時のマイグレーションが何らかの理由で失敗していた場合でも、初回の検索実行時にテーブルが移行され、以降は新規履歴が登録されるようになります。

## [0.3.94] - 2026-02-08
### Fixed
- **検索履歴が新規登録されない問題**: 検索履歴テーブルを新スキーマ（Key/IsIndexSearch）へ移行する判定に「SELECT Key」を使っていましたが、利用環境によっては例外にならずマイグレーションが走らない場合があり、そのまま旧スキーマのテーブルに対して新スキーマ用の INSERT が行われて例外で失敗し、新規履歴が保存されていませんでした。判定を「一時レコードを新スキーマで INSERT できるか」に変更し、旧スキーマのときだけ確実にマイグレーションが実行されるようにしました。

## [0.3.93] - 2026-02-08
### Added
- **検索履歴の通常/インデックス識別**: 検索履歴に、通常検索時は虫眼鏡アイコン、インデックス検索時は雷アイコンを表示するようにしました。同一キーワードでも通常とインデックスを別履歴として保存します。
- **履歴選択時のモード自動切り替え**: 履歴から項目を選択（クリックまたは Enter）した際、その履歴が記録された検索モード（通常/インデックス）に自動で切り替わるようにしました。

### Changed
- **検索履歴テーブルのスキーマ**: 既存 DB は起動時に自動マイグレーションされ、従来の履歴はすべて「通常検索」として扱われます。

## [0.3.92] - 2026-02-08
### Fixed
- **検索履歴の選択挙動の修正**: 下キーでリストにフォーカスした際、即座に検索が実行されてポップアップが閉じてしまう問題を修正しました。上下キーでの移動時は選択のみ行い、Enterキーまたはクリックで検索を実行するように変更しました。

## [0.3.91] - 2026-02-08
### Changed
- **検索履歴の上下キー仕様の明確化**: 下キーは一覧を上から順にフォーカス／選択し、一番下にいる場合は何もしない。上キーは一覧を上方向にフォーカス／選択し、一番上にいる場合は何もしない。検索ボックスから下キーで入ったときは常に先頭を選択するように統一。

## [0.3.90] - 2026-02-08
### Changed
- **検索履歴の上下キー（案B）**: 履歴表示中は下キーのみ履歴リストにフォーカス（先頭を選択）。上キーは検索ボックスにいる間は無視。リスト内では下キーで上から順に移動・一番下で何もしない、上キーで上へ移動・一番上で何もしない（ラップなし）。

## [0.3.89] - 2026-02-08
### Changed
- **検索履歴と上下キー**: 検索ウィンドウで履歴が表示されているときに上下キーを押すと、検索履歴リストにフォーカスし、下キーで先頭・上キーで末尾（直近）の履歴を選択するようにしました。続けて上下キーで項目を移動し、Enterで決定できます。

## [0.3.88] - 2026-02-07
### Changed
- **インデックス検索＋フォーカス時の強調**: 検索バーにフォーカスがあり、かつインデックス検索モードのときのみ、ボーダーをアンバー色にし、薄いアンバー（#FFF9C4）の外側発光で「警告色＝特殊モード」を演出。

## [0.3.87] - 2026-02-07
### Changed
- **インデックス検索の雷アイコン**: ゴールド色・発光エフェクトを追加し、ホバー時に拡大するアニメーションで雷がほとばしる印象に強化しました。

## [0.3.86] - 2026-02-07
### Added
- **インデックス検索モード（UI）**: A/Bペインの検索バーにインデックス検索モードを追加しました。Ctrl+Shift+F または検索バーの虫眼鏡アイコンをクリックするとインデックス検索モードに入り、虫眼鏡が⚡アイコンに変わり、検索バーの背景が黄色系に変化します。⚡アイコンのクリックまたは Ctrl+F で通常モードに戻ります。

## [0.3.85] - 2026-02-07
### Changed
- **Ctrl+F の挙動**: アクティブなペインの検索バーにフォーカスするように変更しました。ナビペイン（お気に入り・履歴）にフォーカスがあるときは該当ビューの検索バーへ、Aペイン/Bペインにフォーカスがあるときはそれぞれのペインの検索バーへフォーカスします。ツリービュー（検索バーなし）の場合はアクティブなファイルペインの検索バーへフォーカスします。

## [0.3.84] - 2026-02-07
### Changed
- **ログフォルダ名**: ログ出力先のフォルダ名を `Logs` から `logs` に変更しました。

## [0.3.83] - 2026-02-07
### Changed
- **ドキュメントの配置**: 配布時に `CHANGELOG.md` と `MANUAL.md` を `apps` フォルダに格納するように変更しました。publish 時には EXE と同階層でなくてよい `.xml` 等のドキュメント類も `apps` に集約されます。

## [0.3.82] - 2026-02-07
### Fixed
- **マニュアル／更新履歴のスクロール位置の共有**: マニュアルページと更新履歴ページでスクロール位置が共有され、一方でスクロールすると他方もスクロールした状態になっていた不具合を修正しました。各ページの縦スクロール位置を個別に保存・復元するようにし、タブ切り替え後もそれぞれの続きから読めるようにしました。

## [0.3.81] - 2026-02-07
### Fixed
- **お気に入りビュー復帰時のナビペイン幅**: ツリー/参照履歴からお気に入りに切り替えたときに、ユーザーが設定した幅へスムーズに戻らない不具合を修正しました。アニメーション中に毎フレーム `_sidebarWidthFavoritesTree` が中間値で上書きされていたため、アニメーション中はこの値を更新しないフラグ（`IsSidebarWidthAnimating`）を導入し、必ず元の幅へ滑らかに戻るようにしました。

## [0.3.80] - 2026-02-07
### Changed
- **ナビペインのビュー切り替え時の幅アニメーション**: お気に入り⇔ツリー/参照履歴の切り替え時に、現在の表示幅（ActualWidth）から目標幅まで滑らかに補間するよう改善しました。QuarticEase EaseOut（開始は速く・終止は優しく吸い付く）を採用し、0.4秒で完結するモダンな動きに統一。アニメーション中は BitmapCache でレイアウト負荷を軽減し、アニメーション中のモード再切り替えやウィンドウリサイズ時も計算が破綻しない堅牢な実装にしています。

## [0.3.79] - 2026-02-07
### Fixed
- **ペインにフォーカスがあるが一覧にフォーカスがないときのキーボード操作**: 起動直後や、Aペイン・Bペインのタブ・ツールバーなどにフォーカスがありファイル一覧にフォーカスがない状態でキーを押すと、一覧にフォーカスが移り、押したキー（Enter・矢印・Delete・F2 など）がそのまま有効になるようにしました。パス入力欄や検索ボックスにフォーカスがあるときは従来どおり入力に使われます。

## [0.3.78] - 2026-02-07
### Changed
- **ペイン・ナビのフォーカスをクリックのみに統一**: マウスホバーによる自動フォーカス移動を廃止し、以前の仕様に差し戻しました。ナビペイン（お気に入り・ツリー・参照履歴）および Aペイン／Bペインの活性化は、**マウス左クリック**またはキーボード（Tab／←／→）による切り替え時のみ行われます。意図しないペインにフォーカスが奪われることなく、操作対象を確実に選べるようにしました。MANUAL.md の「ホバーでのペイン・ナビ切り替え」の記述を削除し、クリックによるフォーカス操作を標準として明記しています。

## [0.3.77] - 2026-02-07
### Fixed
- **ナビペインのコンテキストメニューが画面左上に表示される不具合**: ツリービュー（およびお気に入りツリー）で右クリックした際、コンテキストメニューが常にマウス位置に表示されるように修正しました。`Placement` を `Relative` に変更し、ターゲット項目基準のマウス位置オフセットを明示的に指定することで、システムによる座標計算の不具合（(0,0)へのフォールバック）を回避しました。ツリーの空白部分を右クリックしたときも同様に処理し、ストレスなく操作できるように改善しました。

## [0.3.76] - 2026-02-07
### Added
- **パンくずリストへのドロップ**: ファイル一覧で選択した項目をドラッグし、パンくずの特定の階層（セグメント）にドロップすると、その階層のフォルダへコピーまたは移動できるようにしました。左ドラッグ時は Ctrl キーでコピー、押さないと移動。右ドラッグでドロップすると、ドロップ後に「コピー」「移動」「キャンセル」のメニューが表示されます。
- **ツリービューロック**: ツリービュー（Ctrl+Shift+2）表示時のみ、サイドバー右下に南京錠アイコン（ツリービューロック）を追加。ロック中はツリービュー内でのフォルダのドラッグ＆ドロップ移動・リネーム（F2・右クリック「名前の変更」）・削除（Delete・右クリック「削除」）を禁止し、操作を試みると「ツリービューがロックされているため、変更できません。」と表示して南京錠付近を点滅で強調。お気に入りロックと独立しており、状態は設定に保存され次回起動時に復元されます。

### Changed
- **一覧→ナビペインのドラッグ＆ドロップの振り分け**: ファイル一覧からナビペインへドラッグ＆ドロップした場合の動作を整理しました。**お気に入りビュー（Ctrl+Shift+1）**にドロップしたときのみお気に入り登録（フォルダのみ）を行い、**ツリービュー（Ctrl+Shift+2）**にドロップしたときは、ドロップ先フォルダへファイル・フォルダを**移動**するように変更しました。ファイルの場合はフォルダ構成を変えず、ドロップ先フォルダ直下へ移動します。ツリービューロックON時は、ツリー内フォルダの移動に加え、一覧からツリーへのドロップも含め**全てのドロップ処理を無効化**し、ステータスバーに「ロック中のため操作不可」を表示して南京錠付近で解除促しアニメーションを表示します。
- **ツールチップのアクション指向化**: 全UIのツールチップを「次の操作で何が起こるか」が分かる短い表現に統一。ロック・固定・表示切替など状態が変わるボタンは、現在状態に応じてツールチップを動的に切り替えるようにしました（例: ツリービューをロック / ツリーロックを解除、ナビ幅を固定 / ナビ幅固定を解除）。「無効化」「解除」だけでは対象が分からないため、**何に対してどうなるか**が判別できるよう文言を修正（例: 削除確認を無効化）。ショートカットキーが設定されている操作にはツールチップでキーを併記する方針とし、`.cursor/rules/tooltip-rules.mdc` にルール（対象の明示を含む）を追加しました。
- **お気に入りロックの適用範囲を限定**: お気に入りロックは**お気に入りビュー（Ctrl+Shift+1）内**での追加・削除・並び替えのみを制限するように変更。これまでツリービューでフォルダをドラッグしている際にもお気に入りロックの警告が出ていた問題を修正し、お気に入りをロックしたままツリービューでフォルダ整理が可能になりました。
- **ナビペインロック仕様の統一**: お気に入りビューとツリービューのロック仕様を完全に一致させ、**ナビペインロック仕様**として統一しました。お気に入りビューを正とし、両ビューともロック中に禁止操作を試みた場合はポップアップではなく**ステータスバー**に「ロック中のため〜できませんでした」と表示し、南京錠付近で矢印＋点滅の解除促しアニメーション（約3秒）を表示します。お気に入り側の登録・移動・削除・名前変更・概要編集の各ブロック時も同様にステータスバー通知に変更しました。MANUAL.md にナビペインロック仕様の説明を追記しています。
- **ステータスバーメッセージの表記統一**: ステータスバーに表示する文章の末尾に「。」を付けないルールを導入し、既存の「ロック中のため〜」系メッセージをすべて句点なしに統一。`.cursor/rules/status-bar-message-rules.mdc` にルールを追加しました。
- **コンテキストメニューの区切り線**: 右クリックメニュー内の区切り線が左に寄って見えていたのを修正。テキスト開始位置に揃えた左右の余白（左32px・右12px）を設け、モダンな見た目に統一しました。
- **コンテキストメニューのモダン化**: 横の区切り線が短く表示される問題を、Separator に ControlTemplate（幅いっぱいにストレッチする Border）を指定して解消。さらに MenuItem に専用スタイル（ContextMenuItemStyle）を追加し、アイコン列とテキスト列でモダンなメニュー表示に統一しました。のちに各項目の縦線を削除し、アイコン列を 44px に拡張してアイコンを左右均等の余白で中央配置するよう調整しました。
- **コンテキストメニューの Windows 11 風スタイル刷新**: 右クリックメニュー全体を Windows 11 のシステムメニューに近いモダンなデザインに改善しました。角丸（8px）・薄い境界線・ドロップシャドウを適用し、オフホワイト背景で透明感を付与。各項目の高さ（34px）と余白を拡張し、アイコンは主張しすぎないグレー、ホバー時はアクセントカラーの薄い透過背景を適用。区切り線を細く薄く調整し、「削除」など破壊的アクションはホバー時のみ文字色をかすかに赤く表示するようにしました。ショートカットキー（InputGestureText）がある場合は右側に薄いグレーで表示します。

### Fixed
- **コンテキストメニュー表示時の StaticResource 例外**: コンテキストメニュー（ContextMenu）は Popup 内で表示されるため、MenuItem のテンプレート内の `StaticResource`（TextBrush、SubTextBrush）が解決できず「予期せぬエラー」が多発していました。Popup 内でリソースが解決できない問題に対応するため、ContextMenuItemStyle の色参照を直接指定（#586E75、#93A1A1）に変更して解消しました。
- **ツリー右クリックでエラーになる問題**: コンテキストメニュー用区切り線スタイルで `StaticResource BorderBrush` を参照していたため、Popup 内でリソースが解決できず `Background` が UnsetValue になり例外が発生していました。区切り線の色を直接指定（#D3D0C3）に変更して解消しました。
- **ツリービューでフォルダをコピーした際に即時反映されない問題**: 一覧やツリーからフォルダをコピーした場合に、移動と同様に `FileOperationService` の `FolderCreated` を発火するようにし、コピー先の親ノードが展開済みならツリーに即座にコピー先フォルダが表示されるようにしました。
- **ツリービューの自動展開が動作しない問題**:
  - ファイルペインのパス（例: C:\Users\sulky\Downloads）に合わせてツリーが展開されない原因を修正しました。
  - 原因1: `IsExpanded = true` を先に設定すると OnIsExpandedChanged が `EnsureChildrenLoadedAsync()` を fire-and-forget で呼び、直後の `await EnsureChildrenLoadedAsync()` が `_isLoading` で即 return して完了を待てていませんでした。**先に子を読み込んでから IsExpanded を true にする**順序に変更しました。
  - 原因2: 展開処理でコレクションの参照や IsExpanded の設定が UI スレッドで行われておらず、WPF の表示が追従しない場合がありました。ノードの検索・IsExpanded の設定・Children の取得を **Dispatcher 経由で UI スレッド上で実行**するようにしました。
- **特殊フォルダの認識とタブ復元の安定化**:
  - 起動直後にデスクトップ・ダウンロード・ドキュメントの物理パスを取得・キャッシュする `PathHelper.EnsureSpecialFoldersCached()` を追加し、タブ復元やツリー展開で確実に参照するようにしました。
  - タブ復元時、保存パスが空・無効・ドライブルートのみの場合は特殊フォルダ（Aペイン先頭=デスクトップ、Bペイン先頭=ダウンロード）へ解決し、**デスクトップがCドライブに化ける**ことを防ぎました。
  - ツリーのプログラム的展開中はツリー選択によるナビゲーションを発火させないようにし、起動直後にツリーの最初のノード（C:\）が選ばれてタブがC:\に書き換わる競合を解消しました。
- **重複排除ロジックの見直し**:
  - 重複チェックに `PathHelper.NormalizePathForComparison()`（`Path.GetFullPath` ベースの正規化）を利用するようにし、ツリーの同一親ノード内およびドライブ一覧での重複登録を確実に防ぎました。
- **ツリービューの自動展開の確実化**:
  - ファイルペインのパスに合わせてツリーを展開する `ExpandToPathAsync` を強化しました。ドライブ未読み込み時は先に `LoadDrives()` を実行し、各階層の比較に正規化パスを用いてもれなく展開・選択するようにしました。
  - 遅延読み込みされた階層へも、親を順に展開してから目的ノードを選択する流れを維持しました。
- **起動プロセスの順序制御**:
  - 初期化の順序を「特殊フォルダキャッシュ → タブ復元用パス確定 → タブ復元 → ツリー表示時は Loaded 後にドライブ読み込み・展開」に整理し、起動直後の数秒間の不安定な挙動を抑えました。
- **ツリービューロック中のドラッグ時のメッセージ表示**: ツリービューがロック状態のときに、ツリービュー内でフォルダをドラッグして別の場所にドロップしようとしても無効だったがメッセージが出ていなかった問題を修正。ドラッグオーバー時にステータスバーに「ロック中のため操作不可」を表示し、南京錠付近の解除促しアニメーションも行うようにしました。
- **カレントフォルダがツリーで展開されない場合がある問題**: ツリービューはパス上の親フォルダまで自動展開するが、カレントフォルダ（表示中のフォルダ）ノード自体は展開されず折りたたまれたままになることがありました。`ExpandToPathAsync` でカレントフォルダに対応するノードも子を読み込んだうえで `IsExpanded = true` にするように修正し、カレントフォルダが必ず展開された状態で表示されるようにしました。
- **一覧でフォルダを移動・コピー・リネームした際にツリービューに反映されない場合がある問題**: Aペイン／Bペインの一覧でフォルダをドラッグ＆ドロップで移動・コピーしたり、F2でリネームしたりした場合、ツリービューが更新されず古い表示のままになることがありました。移動・コピー完了時およびフォルダのリネーム完了時に、該当する親ノードの子一覧を再読み込みしてツリーと一覧が連動するように修正しました。
- **一覧でフォルダを移動した際にツリーが歪に表示される問題**: 移動元の親ノードを「子を全クリアして再読み込み」していたため、移動先ノード（移動元の兄弟）がツリーから消え、移動元のみ更新されるか重複表示になる不具合を修正しました。移動時は移動したフォルダのノードだけを移動元の親から削除し、移動先の親の子一覧のみ再読み込みする方式に変更し、ツリー構造が正しく保たれるようにしました。
- **ファイル一覧とツリービューの同期ロジックの根本改善**: `FileOperationService` を新設し、一覧でのフォルダ作成・リネーム・移動・削除をイベント駆動でツリーに即反映するようにしました。ツリー全体のリロードではなく、該当親ノードへの差分更新（追加・削除・名前変更）を行うため、階層構造が崩れずファイルシステムと一致し続けます。親が未展開の場合は次回展開時に最新状態が反映され、`FileSystemWatcher` との二重更新も防止しています。
- **ツリービューのコンテキストメニューで「名前の変更」「削除」が常に無効になる問題**: `CanModifyTreeView` を RelayCommand の CanExecute 用にパラメータ付きメソッドに変更し、ツリービューロック解除時にメニューが有効になるように修正。あわせて、名前の変更（InputBox → Directory.Move、Undo 登録・FileOperationService 通知）と削除（ShellFileOperations でごみ箱へ送る・FileOperationService 通知）の実行処理を実装し、ツリー上からフォルダのリネーム・削除が行えるようにしました。
- **名前の変更ダイアログでマウスカーソルが非表示になりボタンが押せない問題**: キーボード操作モード（矢印キー等でカーソル非表示）中に F2 で名前変更ダイアログを開くと、ダイアログ内でもカーソルが非表示のままになる不具合を修正。MainWindow が非アクティブ化されたときにキーボード操作モードを解除するようにし、InputBox 表示時にもカーソルを明示的に復元するようにしました。
- **ツリービューでロックOFF時にフォルダのドラッグ＆ドロップができない問題**: ツリービューロック解除時もドロップ先の許可（Effects）と実際のフォルダ移動処理が実装されておらず、ドラッグ＆ドロップで移動できませんでした。ロックOFF時に、ツリー上でフォルダを別フォルダの上・前後へドロップすると ShellFileOperations で実フォルダを移動し、Undo・FileOperationService でツリーと一覧に反映されるように修正しました。

## [0.3.75] - 2026-02-07
### Fixed
- **ツリービュー（Ctrl+Shift+2）の重複表示の解消**:
  - 同一フォルダ・同一パスがツリーに二重登録されないよう、フルパスをキーとした重複チェックを追加しました（`DirectoryItemViewModel`：子追加前に同一親内でフルパス一致を厳格にチェック、`DirectoryTreeViewModel.LoadDrives`：ルート登録時に HashSet で重複を排除）。
  - 動的展開時の多重読み込みを防ぐため、各ノードに「読み込み中」フラグ（`_isLoading`）を導入し、展開の連打やバインディングの二重発火で同じ子要素が複数回追加されるレースコンディションを防止しました。
  - 一度読み込みが完了したノードには「読み込み済み」フラグ（`_isLoaded`）を立て、明示的な更新がない限り再読み込みを行わないようにしました。
  - `GetDirectories` 結果をフルパスで一意化（`Distinct`）してから表示するようにし、同一パスがもれなく一意に表示されるようにしました。

## [0.3.74] - 2026-02-07
### Changed
- **ファイル一覧読み込みの並列化・高速化**:
  - フォルダ読み込み処理（`LoadDirectoryAsync`）をリファクタリングし、ディレクトリとファイルの取得を並列（Parallel.Invoke）で行うように変更しました。これにより、特に項目数が多いフォルダでの表示速度が向上しました。
  - リスト更新処理（`MergeItems`）を最適化し、UIスレッドでの負荷を軽減しました（Dictionary生成をバックグラウンドで行い、差分検出ロジックを高速化）。
  - コード構造を整理し、ローカル/ネットワークパスの処理を分離して保守性を向上させました。

## [0.3.73] - 2026-02-07
### Added
- **ドキュメントビューア（マニュアル・更新履歴）の検索機能強化**:
  - **ヒット件数の表示**: 検索ボックス内に「1 / 12 items」のような形式で、現在のヒット位置と全件数をリアルタイム表示するようにしました。
  - **順次ジャンプ機能**: 検索ボックス横の上へ（前へ）・下へ（次へ）ボタンで、検索ヒット箇所をスムーズに巡回できるようになりました（最後の項目の次は先頭に戻るループ対応）。
  - **ハイライトの強化**:
    - すべてのヒット箇所を黄色でハイライト表示し、文書全体での分布を一目で確認できるようにしました。
    - 現在フォーカスしているヒット箇所は**アクセントカラー（オレンジ/青）**で強調表示し、どこを見ているか明確にしました。
  - **目次との連動**: 検索ジャンプ時に、該当セクションに合わせて左側の目次（ToC）の選択状態も自動的に同期するようにしました。

## [0.3.72] - 2026-02-07
### Added
- **元に戻す（Undo）機能**: `Ctrl + Z` ショートカットで、直前のファイル操作を元に戻せるようになりました。
  - 対応している操作: 名前の変更、新規フォルダ作成、ファイルの移動、コピー、ショートカットの作成。
  - 削除操作（ごみ箱への移動）の取り消しは、Windows標準のごみ箱機能をご利用ください。
### Fixed
- ファイルコピー・移動完了時の Undo 登録処理における Nullable 関連のビルド警告を解消しました。

## [0.3.71] - 2026-02-07
### Fixed
- **ローディングアイコンによるクラッシュ修正**: ステータスバーのローディングインジケータで使用していたアイコン（Loader2）が一部環境またはライブラリバージョンで読み込めず、起動時や表示時に `TypeConverterMarkupExtension` 例外でアプリがクラッシュする問題を修正しました（Loader アイコンに変更）。

## [0.3.70] - 2026-02-07
### Added
- **ステータスバーへのローディングインジケータ**: 処理に時間がかかるタスク（ファイル操作、フォルダ検索、ドキュメント読み込みなど）の実行中、ステータスバー右側に控えめな回転アニメーションを表示するようにしました。ユーザー操作をブロックせず、バックグラウンドで処理が進行していることを視覚的に伝えます。
### Changed
- **ローディングオーバーレイの廃止**: 画面全体を覆うローディング表示を廃止し、操作を妨げないステータスバー表示に統合しました。

## [0.3.69] - 2026-02-07
### Fixed
- **新規フォルダ作成時のキャンセル挙動の修正**:
  - 新規フォルダ作成後に表示される名前設定ウィンドウで「キャンセル」を押した場合、作成された「新しいフォルダ」を自動的に削除し、作成前の状態に戻すようにしました。
  - お気に入り（ナビペイン）で仮想フォルダを新規作成した際も同様に、キャンセル時にアイテムを削除するようにしました。

## [0.3.68] - 2026-02-07
### Fixed
- **ドラッグ＆ドロップ操作の安定化（滑り防止）**:
  - ファイルをドラッグする際、マウスボタンを押した瞬間の項目が確実に操作対象となるようロジックを強化しました。クリック後にマウスが僅かに動いて隣のファイルに移動してしまっても、最初に掴んだファイルへの選択が強制的に維持されます。
  - ドラッグ開始判定（10px移動）までの間にカーソル下の項目が変わっても、意図しないファイルが巻き込まれる事故（フォーカスの滑り）を完全に防止しました。
  - ドラッグ中はホバーエフェクト（行のハイライト）を抑制し、掴んでいるファイル（青色選択）だけが明確に見えるよう視覚フィードバックを改善しました。

## [0.3.67] - 2026-02-07
### Added
- アプリのアクティブ/非アクティブ状態を管理する `AppActivationService` を導入。
- 非アクティブ時にバックグラウンドでメモリ（GC）をクリーンアップするロジックを追加し、長時間放置後のフリーズを軽減。

### Fixed
- ペースト完了後にリストのスクロール位置がフォルダ先頭に戻り、貼り付けたファイルが画面外になる問題を修正。貼り付け先ペインで `ScrollIntoView` を確実に実行し、リロード時のスクロール復元をペースト時のみスキップするようにした（A/B 両ペインで対称的に動作）。
- 再活性化時のハングアップ（数秒〜十数秒の無反応）を解消。
- ログシステムを非ブロッキングなキュー（Channel）方式に刷新し、ファイルI/OによるUIスレッドの詰まりを防止。
- 通知サービス内のスレッドスリープ（Thread.Sleep）を非同期待機（Task.Delay）に修正し、スレッドプール枯渇を防止。

### Changed
- フォルダ監視（FileSystemWatcher）を最適化。アプリ非アクティブ時は更新を保留し、アクティブ復帰時にまとめて反映するように変更。
- ファイルリストおよびツリービューの読み込みをバッチ化し、優先度（DispatcherPriority）を調整。大量ファイル環境での操作性を向上。
- ツリービューのサブフォルダ展開を非同期化し、ネットワークドライブ等での応答待ち時間を改善。

## [0.3.66] - 2026-02-06
### Added
- **マニュアルウィンドウの目次ナビゲーション強化**:
  - 目次クリックで該当セクションへスムーズにジャンプする機能を完全実装しました。
  - 本文をスクロールすると、現在読んでいるセクションが目次で自動的にハイライトされる双方向連動を追加しました。
  - 目次ペインの幅を拡張（260px→320px）し、長い項目名や階層構造が視認しやすくなるようレイアウトを調整しました。
 - **総合ドキュメントビューア（マニュアル＋更新履歴）の統合**:
   - これまで別ウィンドウだった「マニュアル」と「更新履歴」を一つの「総合ドキュメントビューア」に統合しました。
   - ウィンドウ上部のセグメントボタン（マニュアル／更新履歴）で、左ペインと右ペインの内容を切り替えて閲覧できます。
   - 左ペインは共通の目次エンジンを使用し、マニュアル時はセクション見出し、更新履歴時はバージョン番号（v0.3.66, v0.3.65...）を一覧表示します。どちらのモードでも項目クリックで右ペインの該当箇所へスムーズにジャンプできます。
### Changed
- **マニュアルウィンドウのUI完成**:
  - **ヘッダーアライメント**: マニュアルアイコン・タイトル・検索アイコンを同一水平中心線上に整列し、黄金比ベースの余白（10px）で視覚バランスを調整しました。
  - **右ペインへのアクセントカラー拡充**: 左ペインの選択色と同じ青色を右ペインに戦略的に適用しました。
    - リストの行頭記号（●）・番号部分にアクセントカラーを適用（本文は黒のまま）。
    - テーブル上部に2pxのアクセントラインを追加。
    - 引用・補足ボックスの左ボーダーをアクセントカラーにし、ごく薄い青のグラデーション背景に変更。
  - ハイパーリンクをアクセントカラーでリンク性を明示。引き算のデザインで長文の視認性を維持しました。
- **マニュアルウィンドウのスクロール・配色調整**:
  - **スクロール量の増加**: マウスホイールのスクロール量を80px程度に設定し、長文の閲覧を快適にしました。
  - **右ペインのワンポイント配色統一**: インラインコード（`settings.json` 等）の背景をグレー（#F2F2F2）から左ペインと同じアクセントカラー系統の薄い青（#EBF2FA）に変更しました。テーブルの境界線も青系（#D0E0F0）に統一し、全体の一体感を高めました。

### Fixed
- **ドキュメントビューアの更新履歴表示不具合修正**:
  - 更新履歴（CHANGELOG）タブを選択してもマニュアル（MANUAL.md）の内容が表示され続ける問題を修正しました。
  - `PostProcessDocument` 内でのコレクション操作エラー（InvalidOperationException）によるクラッシュを修正し、リスト化してから列挙する安全な実装に変更しました。
  - 更新履歴ファイルが見つからない場合に詳細なエラー情報ではなく、ユーザーフレンドリーなメッセージを表示するように改善しました。

## [0.3.65] - 2026-02-06
### Added
- **お気に入り検索モード切替**: ナビペイン右下のナビ幅ロックボタンの左に、検索モード切替ボタンを追加しました。
  - **名前・概要モード**（デフォルト）: キーワードがフォルダ名または概要説明に含まれる場合にヒットします。アイコンは「Type」で表示されます。
  - **フルパス・概要モード**: キーワードがフルパスまたは概要説明に含まれる場合にヒットします。アイコンは「Route」で表示されます。
  - トグルで切り替え可能で、選択したモードは次回起動時に復元されます。切り替え時にステータスバーで通知します。
### Fixed
- **ブレッドクラム表示の改善**: OneDriveなどの特殊フォルダが長い記号（GUID形式）で表示される問題を修正し、フォルダ名が正しく表示されるように改善しました。
- **お気に入り検索機能の改善**:
  - 検索結果に整理用フォルダ（仮想フォルダ）が含まれないように変更し、実際に移動可能なフォルダのみがヒットするようにしました。
  - 検索結果リストの表示不具合（透明化・圧縮）を解消し、テキストとアイコンが正しく描画されるようにしました。
### Changed
- **settings.json の日本語保存**: お気に入り名・パス・概要などの日本語を、Unicode エスケープ（\uXXXX）ではなく UTF-8 の文字としてそのまま保存するように変更しました。テキストエディタでそのまま読める形式になります。
- **初期表示の変更**: 設定未保存時（初回起動時など）の初期状態を、デュアルペイン・左ペイン＝デスクトップ・右ペイン＝ダウンロード・ナビ＝お気に入りビューに統一しました。
- **ログ記録中アイコンの表示位置**: 鉛筆アイコンを通知メッセージと一緒に中央に出すのではなく、ステータスバー右側のログフォルダボタン（ログアイコン）の左側に表示するように変更しました。
- **お気に入り検索モードトグルの説明改善**: 検索モード切替ボタンのツールチップ文言を、現在の検索対象から「次にクリックするとどう切り替わるか」が分かる動的な説明に見直しました。
- **キーボード操作時のマウス干渉排除**: 矢印キーやEnterキーでファイルリストを操作している間はマウスカーソルを自動的に非表示にし、ホバーによる選択変更やフォーカス移動が発生しないようにしました。マウスを前回位置から3ピクセル以上動かしたときにのみカーソルを再表示し、シームレスにマウス操作へ復帰します。
- **ホバーでのフォーカス移動**: マウス操作中は、ナビペイン上にマウスカーソルを載せるだけでナビペインにキーボードフォーカスが移動し、Aペイン/Bペイン上に載せるとそれぞれのペインがアクティブペインとして自動的に選択されファイルリストにフォーカスが移動するように変更しました（キーボード操作モード中は従来どおりホバーではフォーカスが移動しません）。

## [0.3.64] - 2026-02-06
### Fixed
- **お気に入りビューの検索機能の修正**: お気に入りビューで検索を行った際、登録済みのフォルダや整理用フォルダ（仮想フォルダ）がヒットしない場合がある問題を修正しました。
  - 検索ロジックを見直し、フォルダ名（Name）、概要説明（Description）、およびパス（Path）を確実に対象として検索するように改善しました。
  - 特に、パスを持たない整理用フォルダ（仮想フォルダ）も正しく検索結果に含まれるようになりました。

## [0.3.63] - 2026-02-06
### Added
- **お気に入りビューの検索フォーム**: お気に入りリスト上部に検索ボックスを追加しました（履歴ビューと同様のUI・挙動に統一）。
  - **インクリメンタルサーチ**: キーワード入力に応じて即座にお気に入りを絞り込み表示します（検索中はツリー階層を解除し、フラットなリスト形式で表示します）。
  - **検索対象**: フォルダ名（Name）、概要説明（Description）、パス（Path）を対象とし、大文字・小文字を区別しない部分一致とファジー検索でヒットします。
  - **UI**: 虫眼鏡アイコン・テキスト入力欄・クリアボタン（×）の構成で、プレースホルダー「お気に入りを検索...」を表示します。Esc キーまたは×ボタンで検索をクリアできます。
  - **表示形式**: 検索中は専用のリストビューに切り替わり、項目名と概要説明が見やすく表示されます。項目の左側には種類に応じたアイコン（フォルダ、ファイル、ストレージ種別）が表示され、右クリックメニューから「Aペイン/Bペインで開く」などの操作も可能です。
- **お気に入りフォルダの概要説明（Description）機能**: 各お気に入りフォルダに任意の概要説明を追加・表示できるようになりました。
  - **データモデル**: `FavoriteItem` に `Description` プロパティを追加し、`settings.json` に永続化します。既存のお気に入りは `Description` が null/空として安全に扱われます。
  - **UI表示**: フォルダ名の横に `(概要内容)` を控えめなデザイン（フォントサイズ11、Opacity 0.7、SubTextBrush）で表示します。幅が狭い場合は `TextTrimming="CharacterEllipsis"` で省略されます。
  - **ToolTip**: お気に入り項目にマウスホバーすると、パスと概要の全文を表示します。
  - **お気に入り追加ダイアログ**: 「★」ボタンでフォルダをお気に入りに追加する際、名前と「概要説明（任意）」を入力できるダイアログが表示されます。
  - **コンテキストメニュー**: お気に入り項目を右クリックして「概要を編集」を選択すると、概要専用の編集ダイアログが開きます（「概要説明を入力してください:」のメッセージ、本日の日付ボタンなし）。
- **概要編集の専用ダイアログ化**: 「概要を編集」は名前変更用の InputBox ではなく、概要専用の DescriptionEditDialog を使用するように変更しました。メッセージを「概要説明を入力してください:」に統一し、名前編集との混同を防ぎました。
- **新規フォルダ作成時の名前・概要入力**: 「新しいフォルダ」作成時、名前と概要説明（任意）を同時に入力できる AddFolderDialog を表示するようにしました。概要は空欄でも登録可能です。

## [0.3.62] - 2026-02-06
### Added
- **操作ログ記録システム**: ステータスバー通知と連動したログ記録機能を実装しました。
  - **ログ出力**: `Logs` フォルダに `yyyy-MM-dd.log` 形式で操作ログを日別に自動保存します。
  - **視覚演出**: ログ記録中（通知表示時）にステータスバーに鉛筆アイコンが点滅表示され、記録されていることを視覚的にフィードバックします。
  - **クイックアクセス**: ステータスバー右側にログフォルダをワンクリックで開くボタンを追加しました。
  - **自動メンテナンス**: 起動時に30日以上前の古いログファイルを自動的に削除します。

### Changed
- **ステータスバー通知とログの分離**: UI上の通知は「〜件のコピーが完了しました」のように簡潔にし、ファイルログにはファイルパスを含む詳細な情報を記録するように改善しました。
- **ログのタイムスタンプ**: 秒単位（HH:mm:ss）まで正確に記録するように変更しました。
- **詳細なエラーログ**: 操作失敗時にエラーの詳細情報をログに残すようにしました。
- **全操作の通知・ログ対象化**: お気に入り（開く・追加・削除・名前変更・ロック・削除確認）、ペイン表示切替・履歴・ツリーからの表示、タブの追加・閉じる・切り替え、フォルダ表示・戻る・進む・並び替え・検索・設定トグル（最前面・ナビ表示・ナビ幅ロック・フォルダ優先・アダプティブ列）、ウィンドウ配置、マニュアル・更新履歴・ログフォルダを開くなど、アプリで行える操作を原則としてステータスバーに簡潔に表示し、ログに詳細を記録するようにしました。起動時の設定復元時には通知しないよう初期化完了フラグで制御しています。

## [0.3.61] - 2026-02-06
### Added
- **ドラッグ操作時のガイドメッセージ表示**: ファイルリストからアイテムをドラッグする際、マウスカーソルに追従するガイドメッセージを表示するようにしました。
  - お気に入りビューや参照履歴からのドラッグ時と同様に「コピー/移動先にドロップしてください」というメッセージが表示されます。
  - デザインや挙動を既存のドラッグメッセージと統一し、ユーザーが次に何をすべきか直感的に理解できるようになりました。
- **ステータスバー中央への通知メッセージ表示**: 操作完了などのフィードバックをステータスバーの中央にさりげなく表示する機能を追加しました。
  - 表示時にふわっと現れ、3秒後に自然に消えるフェードイン・フェードアウトアニメーションを実装。
  - 短時間に連続して発生した場合は、最新のメッセージで表示時間を延長します。
  - UIに溶け込むモダンなデザイン（アクセントカラー、メインよりわずかに小さいフォント）を採用。

## [0.3.60] - 2026-02-06
### Added
- **タイトルバーに現在のフォルダパスを表示**: メインウィンドウのタイトルバーに、現在フォーカスがあるペインのフォルダのフルパスを表示するようにしました（例: `Zenith Filer v0.3.60 - C:\Users\...\Desktop`）。パスが空のときや起動直後は「Zenith Filer v0.3.60」のみ表示されます。左右ペインの切り替えに合わせて表示が更新されます。
- **右クリックドラッグ＆ドロップ（右ドラッグ）で「ショートカットを作成」**: ファイルを右クリックしたままドラッグしてドロップすると、その場にカスタムコンテキストメニューが表示されます。
  - メニュー項目: 「ここにコピー」「ここに移動」「ここにショートカットを作成」「キャンセル」
  - 「ここにショートカットを作成」を選ぶと、元のファイルを参照する .lnk ショートカット（例: `元の名前 - ショートカット.lnk`）がドロップ先に生成されます。
  - 同一ペイン内・左右（または上下）ペイン間のいずれでも利用可能です。左クリックでの通常のドラッグ＆ドロップ（移動/コピー）の挙動は従来どおり維持されています。
### Changed
- **ツールバー（アイコン群）から「★」お気に入りボタンを削除**: 1行目のアクション行（戻る・進む・更新などのアイコン群）にあったお気に入り登録用のスターアイコンボタンを削除しました。お気に入りへの登録は、アドレスバー（2行目）左端にある「★」ボタンから引き続き利用できます。

## [0.3.59] - 2026-02-05
### Fixed
- **検索結果表示中のお気に入りドロップで正常に遷移しない問題**: ファイルリストに検索結果（または履歴）が表示されているときに、ナビゲーションペインからお気に入りをドロップすると、検索状態をクリアし通常モードに戻したうえで、ドロップしたお気に入りのフォルダへ正しく遷移するように修正しました。お気に入り・参照履歴・フォルダツリーからのドロップをナビゲーション用と識別し、検索解除→ヘッダ復帰→遷移の順で処理するようにしています。
- **検索履歴を1回クリックで非表示**: 履歴アイテムまたは「履歴はありません」を1回クリックしたらポップアップを閉じるようにしました。
- **検索履歴ポップアップがフォーカス移動で閉じない問題**: 検索フォーム・履歴以外にフォーカスが移ったときにポップアップを閉じるように、LostKeyboardFocus ハンドラを追加しました。
- **検索履歴ポップアップの黒い枠**: ポップアップに AllowsTransparency を有効にし、ListBox/ListBoxItem の FocusVisualStyle を無効化して黒い枠が表示されないようにしました。
- **検索履歴ポップアップの表示**: 検索ボックスにフォーカスした場合のみ履歴ポップアップを表示するようにしました（下キー不要）。
- **検索履歴が登録されない問題**:
  - 検索キーワードを入力して検索が実行されたとき（Enter キー以外）にも履歴に登録するようにしました。
- **パフォーマンス最適化**:
  - ドロップダウンの透過処理（AllowsTransparency）と影エフェクトを無効化し、描画速度を最大化しました。これによりクリック時のレスポンスがさらに向上しました。

## [0.3.59] - 2026-02-05
### Fixed
- **検索履歴ドロップダウンの表示を確実化**:
  - 検索バーをクリックした際に、履歴データがロードされるまで待機してからドロップダウンを表示するように修正し、タイミングによって表示されない問題を解消しました。
  - 履歴が0件の場合でも「履歴はありません」と表示するようにし、クリック時の動作フィードバックを明確にしました。
  - `PreviewMouseDown` イベントにも対応し、フォーカスがある状態でのクリックでも確実に反応するようにしました。
- **パフォーマンス最適化**:
  - ドロップダウンの透過処理（AllowsTransparency）と影エフェクトを無効化し、描画速度を最大化しました。これによりクリック時のレスポンスがさらに向上しました。

## [0.3.58] - 2026-02-05
### Added
- **検索履歴ドロップダウン機能**: 検索バーをクリックした際に、過去の検索キーワード（最大100件）を即座に表示するドロップダウン機能を追加しました。
  - **超高速レスポンス**: アニメーションを排除し、フォーカスした瞬間に遅延なく履歴を表示します。
  - **履歴の永続化**: 検索実行時（Enterキーまたは履歴選択）にキーワードを保存し、次回起動時にも利用可能です。
  - **自動クリーンアップ**: 最新の100件を保持し、古い履歴は自動的に削除されます。
### Fixed
- **検索履歴が表示されない問題を修正**: 検索バーをクリックしても履歴ドロップダウンが表示されない、または保存されない場合がある問題を修正しました（Popupの表示位置設定の強化、Enterキー押下時の保存ロジックの確実化）。

## [0.3.57] - 2026-02-05
### Added
- **検索結果件数のリアルタイム表示**: 検索バーの右端（内側）に、ヒットした件数（例: `12 items`）をリアルタイムで表示する機能を追加しました。
  - 検索ワードを入力するたびに即座に件数が更新されます。
  - 検索ボックスが空（検索していない状態）のときは非表示になります。

## [0.3.56] - 2026-02-05
### Added
- **検索結果ビューへの「場所」アイコン列の追加**: 検索結果の一覧に、ファイルの保存場所（Local, Server, Box, SPO）を示すアイコンを表示する列を追加しました。名前列とパス列の間に配置され、ファイルがどこにあるかを視覚的に識別しやすくなりました。
### Changed
- **検索結果ビューのアダプティブレイアウト強化**: ペインの幅に応じて、優先順位の低い列から動的に非表示にする機能を実装しました。
  - **表示優先順位**: 名前 > 場所 > パス > 更新日時 > サイズ の順で最後まで表示されます。
  - **動的制御**: 幅が狭くなるとまずサイズと更新日時が非表示になり、次にパス列が幅を縮小し、最終的に場所アイコンと名前のみが表示される、業務利用に特化した粘り強いレイアウトを実現しました。
  - 非表示になった列の分のスペースは、自動的にパス列と名前列に割り当てられ、有効活用されます。

## [0.3.55] - 2026-02-05
### Added
- **settings.json の自動バックアップ機能**: 設定保存のたびに、既存の `settings.json` を `backups` フォルダへ自動でコピーするようにしました。ファイル名は `settings_yyyyMMdd_HHmmss.json` 形式（例: `settings_20260205_092500.json`）で、過去の状態をいつでも手動で復元できます。お気に入り移行時やウィンドウクローズ時など、すべての保存タイミングでバックアップが作成されます。

## [0.3.54] - 2026-02-05
### Changed
- **お気に入りの保存先を settings.json に統一**:
  - お気に入り情報をアプリ実行フォルダの `settings.json` に保存するように変更しました。これにより、`settings.json` をバックアップするだけで、お気に入りを含むユーザー設定をバージョンアップ時に容易に引き継げます。
  - 追加・削除・並べ替えが発生するたびに `settings.json` が即座に更新されます。
  - 初回起動時、旧形式の `%AppData%\ZenithFiler\favorites.json` が存在する場合は自動的に `settings.json` へ移行します。
  - お気に入りと履歴のデータ管理を分離（お気に入り: settings.json、履歴: SQLite に継続保存）。

## [0.3.53] - 2026-02-05
### Changed
- **検索結果ビューのパス列表示を改善**:
  - パス列の幅に応じて表示内容を動的に切り替えるように変更。十分な幅がある場合はフルパスを、幅が狭い場合は親フォルダ名のみを表示するようにし、限られたスペースでも重要な情報を確認しやすくしました。
  - パス列のテキスト配置を左寄せに変更し、長いパスの視認性を向上。
  - 省略表示時でも確認できるよう、ツールチップには常にフルパスを表示するように変更。

## [0.3.52] - 2026-02-05
### Changed
- **起動パフォーマンスの劇的改善（爆速起動）**:
  - **遅延初期化の徹底**: データベース接続や検索サービス、サイドバーコンテンツ（履歴・ツリー）の初期化を、実際に必要になる瞬間まで遅延させることで、起動直後のメモリ使用量とCPU負荷を最小化しました。
  - **非同期ロードの最適化**: 起動時の前回タブ復元処理において、フォルダの中身（ファイル一覧）の読み込みをバックグラウンドで行うように変更。これにより、起動直後にウィンドウ枠が即座に表示され、操作可能になるまでの待機時間（Time to Interactive）を大幅に短縮しました。
  - **コンパイル設定の強化**: ReadyToRun (R2R) に加え、`TieredCompilation` を最適化し、JITコンパイルのオーバーヘッドを削減しました。
- Box Drive へのアクセスパフォーマンスを改善（実体アクセスを回避し、レスポンスを向上）
- ファイル一覧の読み込み処理を刷新し、段階的に表示されるように変更（フォルダ移動時の体感速度向上）

### Fixed
- Box Drive 内のファイルが白いアイコンで表示される問題を修正

## [0.3.50] - 2026-02-05
### Changed
- **検索実行時のファイルリスト表示を最適化**:
  - 検索中（IsSearching）のリストのカラム構成を「名前」「パス」「更新日時」「サイズ」の4列に切り替えるように変更しました。
  - **パス表示の最適化**: 検索結果の「パス」列は残りのスペースを最大限使用して表示され、長いパスでも末尾（ファイル名側）が隠れないように右寄せで表示されます。
  - 検索ワードをクリアして通常モードに戻ると、即座に標準の5列構成（名前・場所・更新日時・種類・サイズ）に復帰します。

## [0.3.49] - 2026-02-05
### Added
- **フォルダ内検索機能**: ペインヘッダーに検索バーを追加し、現在のフォルダおよび配下のサブフォルダを対象とした**Fuzzy Search（曖昧検索）**を実装しました。入力したキーワードを含むファイル・フォルダを再帰的に検索し、インクリメンタルに一覧表示します。検索中は検索結果リストに切り替わり、クリアすると元の表示に戻ります。
- **ペインヘッダーの刷新**: 1行目のレイアウトを整理し、左側に操作系アイコンをグループ化（ナビゲーション、操作、表示）して配置、右側の空きスペースに検索バーを配置しました。セパレーターを追加して視認性を向上させました。

## [0.3.48] - 2026-02-05
### Changed
- **コンテキストメニューをOS標準（Windows）スタイルに統一**: 右クリックメニューの表示を、Windows純正エクスプローラーに近い挙動に変更しました。
  - **アニメーションの削除**: 表示時のスライド／フェードなどの視覚効果を無効化し、右クリックした瞬間に待機時間ゼロで即座に表示されるようにしました（`PopupAnimation.None` をアプリ全体で適用）。
  - **表示位置の厳密化**: マウスカーソル位置（右クリック座標）を起点にメニューを表示し、画面右端・下端にはみ出す場合は左側・上側に展開する見切れ防止ロジックを実装しました（WPFのコンテキストメニューは Opened 時にオフセット補正、シェルメニューは `TrackPopupMenuEx` の TPM_RIGHTALIGN／TPM_BOTTOMALIGN を利用）。

## [0.3.47] - 2026-02-05
### Changed
- **ペインヘッダーを2行レイアウトに変更**: 戻る／進む／上へ、更新、新規フォルダ、お気に入り登録（★）、パス編集トグルと右端の各種ツールアイコンを1行目（アクション行）に集約し、2行目にはアドレスバーのみを横幅いっぱいに配置しました。アイコン列とアドレスバーの左端位置を揃えることで視線移動を抑えています。
- **アドレスバーのホバー伸縮を廃止**: マウスホバーやドロップダウン表示に応じてアドレスバーが伸縮するアニメーションとレイアウト調整ロジックを削除し、常に一定幅で表示される安定したUIに変更しました。これにより、操作中にヘッダー内の要素が「ぴょこぴょこ」動かず、アドレスバーとツールアイコンの位置が固定されます。

## [0.3.46] - 2026-02-04
### Fixed
- **ロック解除ボタンがアニメーション中に押せない問題を修正**: お気に入りビューがロック中に登録を試みるとロック解除を促す点滅アニメーションが表示されますが、アニメーション中も南京錠ボタンのクリックを常に受け付けるようにしました。クリックした瞬間にロックが解除され、アニメーションが即停止するようロジックを変更しました（警告用フラグの即時オフ・遅延キャンセル、UIブロックの削除）。
### Changed
- **お気に入りビューのフォルダ作成フローを改善**: お気に入りビューで「新しいフォルダ」を実行した際、即座に名前変更ダイアログを表示し、デフォルトで「新しいフォルダ」または「新しいフォルダ (n)」を入力した状態（全選択済み）で編集できるようにしました。`OK` / Enter 確定時に初めて仮想フォルダが作成され、キャンセルした場合は空のフォルダが作成されないため、意図しないダミー項目が残らなくなります。
- **お気に入りビューの新規フォルダ挿入位置を最適化**: お気に入りビューで特定の項目を右クリックして「新しいフォルダ」を実行した場合、その項目の「中」ではなく**同じ階層の直下**（`index + 1` の位置）に仮想フォルダを挿入するように変更しました。作成直後のフォルダは自動的に選択されてツリービュー内までスクロールされ、そのまま名前変更ダイアログが開くため、画面外に隠れず直感的にリネームできます。

## [0.3.45] - 2026-02-04
### Fixed
- **戻る／進むで履歴が1つ前までしか遡れない問題を修正**: ツールバーの「戻る」ボタン（Alt+Left）やマウスの戻るボタン（XButton1）を押した際、`saveToHistory` が常に `true` のまま呼ばれていたため履歴スタックが破損し、連続して10個以上のフォルダを遡れませんでした。戻る／進むによる遷移では `saveToHistory: false` を使用するように修正し、少なくとも10個以上の履歴を正しく保持・遷移できるようにしました。

## [0.3.44] - 2026-02-04
### Added
- **フォルダ新規作成時の「本日の日付」ボタン**: 新規フォルダ作成後に表示される名前変更ダイアログに**「本日の日付」ボタン**を追加しました。クリックすると `YYYYMMDD` 形式（例: `20260204`）の当日日付が自動入力され、日付ベースのフォルダを素早く作成できます（通常のリネーム時にも利用可能）。ボタンはテキストボックス下のボタン行に配置され、テキストエリアはウィンドウ幅いっぱいに表示されます。
### Fixed
- **名前の変更ダイアログの高さを調整**: ウィンドウの縦幅が不足しており、「本日の日付」ボタンが表示されない場合があった問題を修正しました。ボタンと OK / キャンセル が常に表示されるよう、高さを拡大しています。
### Changed
- **名前の変更ダイアログのボタンレイアウトを改善**: 「本日の日付」「OK」「キャンセル」の3ボタンをテキストボックス直下の右下に横一列で整列し、余白とボタンサイズ・高さ・パディングを統一することで、日本語テキストがつぶれない、よりモダンで視認性の高いレイアウトにしました。

## [0.3.43] - 2026-02-04
### Fixed
- **タブのコンテキストメニューの表示位置を修正**: タブを右クリックした際、メニューが上からスライドしてくるのではなく、マウスカーソルの位置に即座に表示されるように修正しました（`Placement="MousePoint"` と `PopupAnimation="None"` を明示的に指定）。
- **フォルダ行へのドラッグ＆ドロップでファイルが移動／コピーされない問題を修正**: 同一ペイン内でファイルをフォルダの行にドラッグ＆ドロップした際、ドロップ先フォルダではなく現在のフォルダに対して処理されていたため、期待どおり配下フォルダに移動／コピーされないケースがありました。ドロップ位置のフォルダ行を正しく検出し、そのフォルダを転送先としてシェル操作を行うように修正しました。

## [0.3.42] - 2026-02-04
### Fixed
- **起動エラーの修正**:
  - `MainWindow.xaml` の移動に伴い発生していた「リソースが見つからない」エラーを修正（`StartupUri` を更新）。
  - データベースサービスの初期化処理（ディレクトリ作成）を静的コンストラクタから `InitializeAsync` へ移動し、起動時のファイルI/Oエラーによるクラッシュを防止。
- **例外ハンドリングの強化**: グローバル例外ハンドラ（`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`）を実装し、予期せぬエラー発生時に無言で終了せず、エラーメッセージを表示するように改善。

## [0.3.41] - 2026-02-04
### Added
- **タブのロック機能**: タブを右クリックして「ロック」を選択することで、そのタブを閉じられないように固定できるようになりました。ロックされたタブには南京錠アイコンが表示され、次回起動時にもロック状態が復元されます。

## [0.3.40] - 2026-02-04
### Changed
- **プロジェクト構造の刷新**: ルートディレクトリのファイルを責務ごとのフォルダ（ViewModels, Views, Models, Services, Helpers, Converters, Behaviors）に整理し、保守性を向上。
- **リファクタリング**: `MainViewModel` 内のウィンドウ制御ロジックを `WindowHelper` へ、文字列処理を `StringHelper` へ分離。

## [0.3.39] - 2026-02-04
### Changed
- **起動速度の改善**:
  - データベース初期化処理をバックグラウンド実行に変更し、起動時のブロッキングを解消。
  - 設定ファイル読み込みの重複を排除し、I/Oを最適化。
- 起動時のスプラッシュスクリーン（アプリアイコン表示）を削除した。

## [0.3.38] - 2026-02-04
### Added
- **ウィンドウを左下・右下（画面の1/4）に配置**: ステータスバーのウィンドウ配置ボタンに「左下に配置」「右下に配置」を追加。クリックでウィンドウを画面の左下1/4または右下1/4にスナップします。アイコンは CornerDownLeft / CornerDownRight（隅を示すアイコン）を使用。
- **ウィンドウを下半分に配置**: ステータスバーのウィンドウ配置ボタンに「下半分に配置」を追加。最大化ボタンの右側に配置し、クリックでウィンドウを画面の下半分にスナップします。アイコンは PanelBottom を使用。
### Fixed
- **コンテキストメニュー（右クリック）の動作改善**:
  - メニュー表示をバックグラウンドスレッドで実行し、表示までのUIフリーズを解消。
  - 「名前の変更」が機能しなかった問題を修正（アプリ側のリネーム機能を呼び出すように変更）。
  - 「プロパティ」が表示されなかったり消えたりする問題を修正（メインスレッドでプロパティ画面を表示するように変更）。
### Changed
- **表示形式切り替え**: ツールバー右端の「大型アイコン」ボタンを非活性にし、押せないようにした（現在は詳細表示のみ対応）。

## [0.3.37] - 2026-02-04
### Added
- **過去履歴ペインへの「高度な検索機能」**: 履歴リストの上部に検索ボックスを追加。
  - **Fuzzy Search（曖昧検索）**: キーワード入力時、多少のタイポを許容するファジー検索（Levenshtein距離）で履歴を絞り込み表示。
  - **ハイライト**: 検索キーワードと一致する部分をアクセントカラーで強調表示。
  - **ショートカット**: `Ctrl + F` で履歴ペインに切り替え、検索ボックスへ即座にフォーカス。
- **Ctrl+Shift+N（新規フォルダ）**: 現在のフォルダに「新しいフォルダ」を作成し、作成したフォルダにフォーカスして名前の変更（F2 相当）を開始する。
- **ナビペイン幅ロック**: お気に入りビュー時のみ、サイドバー右下にナビペインの幅変更をロックするトグル（パネルアイコン）を追加。ロック中はスプリッターによる任意の幅変更ができず、ツリービュー・履歴ビューへの切り替え時の自動拡張は従来どおり有効。ロック状態は設定に保存され、次回起動時に復元される。
### Changed
- **履歴ビューの日付グループに曜日を表示**: 日付グループの表示を「2026/02/04(水)」のように曜日付きに変更しました（日本語ロケール）。
- **過去履歴ペインの検索挙動を改善**: 
  - 検索ボックス入力時は**日付ごとのグループ分けを解除し、一覧表示**（参照日付の降順）にするように変更しました。これにより、過去の履歴全体から目的のフォルダを素早く見つけやすくなりました。
  - 検索テキストをクリアすると、自動的に通常の日付別グループ表示に戻ります。
- **名前の変更（リネーム）**: ファイルのリネーム時は、ダイアログのテキストボックスには**名前部分のみ**を表示し、拡張子はテキストボックス外にラベルとして表示する。拡張子は選択・編集不可で、確定時に元の拡張子を付与する。フォルダのリネームは従来どおり全体を選択・編集可能。
- **ナビ幅ロックのアイコン**: 左端のトグルを南京錠からパネル（PanelLeftOpen / PanelLeftClose）に変更。幅変更可能時は開いたパネル、固定時は閉じたパネルで表示し、右端のお気に入りロック（南京錠）および右下の最前列固定（ピン）と重ならないようにした。
- **過去履歴ビューのゴミ箱（履歴削除）**: 参照履歴をクリアするボタンを、過去履歴ビューの**右下**に配置するように変更した。

## [0.3.36] - 2026-02-03
### Fixed
- ナビペインのツリービュー（お気に入り・フォルダツリー）で横スクロールバーが表示され横スクロールできていた問題を修正。横スクロールを無効にし、長いフォルダ名は省略記号（…）で表示するようにした。
- お気に入りビューがロック中に☆（お気に入り登録ボタン）を押したとき、エラーメッセージの表示とロックアイコン付近の「解除を促すアニメーション」が行われていなかった問題を修正（機能復旧）。ロック中はスターが赤色になり、ダイアログで「登録できません」を表示し、ナビペインのロックアイコン付近で矢印＋点滅のアニメーションが表示されるようにした。
- マニュアルウィンドウで Markdown 記法（`**` 太字、`` ` `` コード）がそのまま表示され読みづらかった問題を修正。表示時に記法を除去し、プレーンテキストとして表示するようにした。
- アドレスバーで「パスを編集」モードにするとモード切替アイコン（鉛筆）がアドレス欄の下に回り込み、切り替えが困難になる問題を修正。モード切替アイコンを「↑」ボタンの右横（左側固定）に配置し、常に同じ位置で操作できるようにした。
### Added
- **お気に入り登録ボタン（★）の改善**:
  - ボタンをアドレスバーの**左端**に移動しました。
  - **失敗アニメーション**: ロック中はスターが赤色になり、「ロック中のため登録できません」の表示とダイアログ、ロックアイコン付近の矢印＋点滅で解除を促します。
  - **成功アクション**: 登録時に星が大きく動き、「Saved!」が表示されます。3秒経過するとアイコンの色は自動で戻ります。
### Fixed
- パンくずのドロップダウンメニューで、マウスがメニュー外（ファイルリスト等）に移ってもメニューが閉じない問題を修正。メインウィンドウ上でマウス位置をタイマーで監視し、メニュー・▼ボタン外にマウスが出たときにメニューを閉じるようにした。
### Changed
- **アドレスバーとメニューの挙動・視覚の完全統合**:
  - **伸長条件**: 「マウスホバー時」「フォーカス（パス編集）時」「ドロップダウンメニュー展開時」のいずれかでアドレスバーを右方向へ最大まで伸長（左端は固定で文字位置のジャンプを防止）。
  - **メニュー閉鎖**: マウスがメニュー外に出たとき、または **Esc キー**でメニューを閉じる。メニュー閉鎖後もアドレスバー上にマウスがあれば伸長状態を維持し、完全に外れたときのみ滑らかに元のサイズへ戻す。
  - **アニメーション**: cubic-bezier(0.25, 0.8, 0.25, 1) を用いた **300ms** の伸縮。伸長時は z-index でツールアイコン群の上に重ね、軽い影で浮遊感を付与。
- **アドレスバー（パンくずリスト）の挙動改善**:
  - 階層移動メニュー（ドロップダウン）を表示中、マウスが離れてもアドレスバーが**伸長したまま固定**されるように改善。
  - メニュー操作中にレイアウトが動いてしまう問題を解消し、スムーズなフォルダ選択が可能になりました。
- **お気に入りボタン（☆）のホバーとレイアウトを改善**:
  - ☆ボタンはホバー時**アイコンのみ**（最小限の円形背景）が反応し、左右の余白を詰めてコンパクトに配置（MinWidth/Height 20）。
  - ホバー時は四角い背景ではなく、**アイコンに沿った最小限の円形ハイライト**＋アイコン色変化に変更。0.2s のスムーズなトランジションを適用。
  - アイコンは 12×12 で統一し、ヒット領域は維持。

## [0.3.35] - 2026-02-03
### Added
- **アドレス欄は全アイコンの右側で残り全てを占有**: ツールバー行は［全アイコン（移動系＋その他）］［アドレス欄 *］の順で、アドレス欄が全アイコンの右側にありアイコンを除いた全てのスペースを占有します。アドレス欄にマウスを乗せると他アイコン列を0にアニメーションしてアドレス欄が左へ拡大します。
- **エクスプローラーのフォルダを取り込む**機能を追加。アドレスバー左のアイコン群（「エクスプローラーで表示」の右）に取り込みボタンを配置。
  - 開いているエクスプローラーが 1 件のみの場合：そのフォルダを**新規タブ**で表示し、対象のエクスプローラーを閉じます。
  - 2 件以上ある場合：取り込むエクスプローラーを選択するダイアログを表示。選択したフォルダをそれぞれ**新規タブ**で表示し、取り込み完了後に対象のエクスプローラーを閉じます。
  - Shell.Application COM（実績のある方式）でエクスプローラーウィンドウ一覧を取得しています。

## [0.3.34] - 2026-02-03
### Added
- ブラウザ欄（パスバー）のアイコン群に「エクスプローラーで表示」ボタンを追加。現在表示しているフォルダを Windows 標準のエクスプローラーで開けます。
- パンくずリストのエクスプローラー同様のドロップダウンを追加。
  - 各階層の右側の下向き矢印（▼）をクリックすると、そのフォルダ直下のサブフォルダ一覧がポップアップ表示され、選択するとそのフォルダへ移動できます。
### Fixed
- 同一フォルダ内でファイルをドラッグ＆ドロップ（移動）した際に E_UNEXPECTED エラーが表示される問題を修正。同一フォルダへの移動は何も行わず静かにスキップするようにした。
- 最前列固定時、リネーム入力やマニュアル・更新履歴などのポップアップがメインウィンドウの背後に表示される問題を修正。アプリから開くポップアップはメインより前面に表示されるようにした。
- ファイルリストでの `F2` キーによる名前の変更が動作しない問題を修正。
- ファイルリストでの `Ctrl+X` キーによる切り取りが動作しない問題を修正。
- AペインでコピーしてBペインに移動してペーストするとフォーカスが失われ、以後キーボード操作ができなくなる問題を修正。コピー・切り取り・ペースト後もフォーカスがリストに残るようにし、ペースト完了後はリストの先頭項目にフォーカスが当たるようにした。
- コピー・切り取り時は操作した項目（選択中）に、ペースト時はペーストしたファイルにフォーカスが当たるように改善。
- コピー・ペースト後にフォーカスが一瞬で消える問題を修正。FileSystemWatcher による後続の Refresh 後もフォーカスを再適用し、遅延（100ms/600ms）でフォーカスを再度当てることで、他処理や他ペインに奪われないようにした。
- コピー・切り取り後にマウスが当たっているペインで画面がちらつく問題を修正。コピー・切り取り時はリストが変わらないため遅延フォーカス復元を行わず、即時フォーカスのみにした。
- 両ペインで発生していた画面のちらつきを軽減。ペースト後の 100ms/600ms 遅延フォーカス復元タイマーを廃止し、フォーカス復元はリスト更新時（CollectionChanged）のみにした。またフォーカスが既に当たっている場合は再設定しないようにし、不要な描画更新を抑止。
- 通常モード時に A ペインのみ表示されるように修正。デフォルトのペイン数を 1（A ペインのみ）に変更した。
- ファイルリストでタイトル列が二重に表示されていた問題を修正。カスタム ScrollViewer 内の重複していたヘッダー行を削除した。
### Changed
- ファイルリストの垂直スクロールバーをオーバーレイ表示に変更。
  - スクロールバーが表示されても「名前」列などの幅が変動（縮小）しないように改善。
  - スクロールバーは「サイズ」列などのコンテンツの上に重ねて表示されるようになり、レイアウトの安定性が向上。
- 列幅計算ロジック（StarWidthConverter / AdaptiveColumnWidthConverter）を、スクロールバーの有無に依存しないように修正。
- ドラッグ＆ドロップの誤操作防止を改善。
  - ドラッグ開始までの移動しきい値を 2px から 10px に変更。軽いマウス揺れでドラッグが発動するのを抑制。
  - Windows 標準に合わせた動作に変更：**Ctrl なしでドロップ＝移動**、**Ctrl ありでドロップ＝コピー**。
- 処理効率化のリファクタリング（機能変更なし）。
  - MainViewModel / FilePaneViewModel: リスト判定を `Any()` から `Count` パターンに変更、`Last()` をインデックスアクセスに変更。
  - PathHelper: 特殊フォルダ判定を HashSet に、IsUncRoot を Span で、パス結合でキャッシュ済み区切り文字を使用。
  - TabItemViewModel: フォルダ読み込み時の二重ソートを廃止（ICollectionView の ApplySort のみに統一）。
  - DirectoryTreeViewModel: ループ内の `Last()` をインデックス比較に変更。
  - DatabaseService: 履歴クリーンアップを一括 DELETE に変更（N+1 削除を回避）。
  - DirectoryItemViewModel: 子フォルダ一覧のソートをバックグラウンドスレッドで実施。
  - FavoritesViewModel: 初期お気に入りロード時の SaveFavorites を 1 回に集約。
- 起動の高速化。
  - 履歴ビューのデータ読み込みを起動時には行わず、ナビペインで「参照履歴」に切り替えたときに遅延ロードするように変更。
  - ツリービューのドライブ一覧・展開を起動時には行わず、ナビペインで「ツリー」に切り替えたときに遅延ロードするように変更（前回がツリーで終了した場合は、起動をブロックせずバックグラウンドでツリーを構築）。
  - データベースの古い履歴クリーンアップ（CleanupHistoryAsync）を起動時 await せず、バックグラウンドで実行するように変更。

## [0.3.33] - 2026-02-03
### Fixed
- Aペイン/Bペインで不要な横スクロールバーが表示される問題を修正。
  - スクロールバーの表示設定を「非表示（Disabled）」に固定。
  - 「名前」列の幅計算において、垂直スクロールバーの有無を正確に考慮（ViewportWidth を優先使用）するように改善。
- 「サイズ」列の右側余白（Padding）を調整。
  - カスタムスクロールバーの幅（12px）に合わせてパディングを最適化し、デザインの調和を改善。
### Changed
- ファイルリストで垂直スクロールバーが表示されても、各列の表示位置がずれないように改善。
  - 「サイズ」列の右側にスクロールバー分のパディングを確保し、スクロールバーが項目内容と重なるのを防止。

## [0.3.32] - 2026-02-03
### Fixed
- ファイルリストでキーボード（Delete）でファイルを削除した際にフォーカスが失われ、以後のキーボード操作ができなくなる問題を修正。削除後は次の項目に選択とフォーカスが移るようにした。

## [0.3.31] - 2026-02-02
### Added
- タブ切り替え時のコンテンツフェードインアニメーション
  - タブを切り替えると新しいコンテンツが約 0.4 秒でフェードインするようにした（初回表示時はアニメーションしない）
- タブヘッダー下のアクティブインジケータのスライドアニメーション
  - 選択タブに合わせてインジケータ（下線）が約 0.25 秒でスライドするようにした（初回表示時はアニメーションしない）
### Fixed
- TAB キーおよび ← / → キーによる Aペイン・Bペインの切り替えが動作しなくなっていた問題を修正
  - キー処理をメインウィンドウの PreviewKeyDown で行うようにし、フォーカスがリスト以外（パス欄・ブレッドクラムなど）にある場合でもペイン切り替えができるようにした
- ナビペインでお気に入り・ツリー・履歴を切り替えた際、お気に入りビューに戻ったときにユーザーが設定したナビペインの幅に戻らないことがある問題を修正
  - ツリー／履歴に切り替えるたびに「お気に入り用の幅」を上書きしていたため、お気に入り幅はお気に入りビュー表示時のみ（スプリッター操作時）更新するように変更した
- BOX（Box フォルダー）にアクセスした際にタブ・履歴・お気に入りで真っ白なアイコンが表示される問題を修正
  - MahApps.Metro.IconPacks.Lucide の `Box` アイコンが正しく描画されないため、ストレージを表す `Archive` アイコンに差し替えた
### Changed
- タブの幅を約 2/3（160px → 106px）に短縮し、多くのタブを同時に表示できるようにした
- ナビペインの幅をビューに応じて動的に調整
  - 参照履歴ビューのときは横スクロールが出ない幅に自動拡張し、お気に入り・ツリービューに切り替えたときは元の幅に戻すようにした
  - お気に入り・ツリービューでスプリッターにより変更した幅は保持され、設定に保存される
- ツリービューと履歴ビューのナビペイン幅を統一（いずれも 340px）
- ナビペインの幅変更にアニメーションを追加
  - 参照履歴ビュー・ツリービュー表示時に幅がスムーズに広がり、お気に入りに切り替えたときに滑らかに短くなるようにした（約 0.25 秒のイージング）

## [0.3.30] - 2026-02-02
### Fixed
- マウスの右スクロール・左スクロール（ホイールチルト）が効かない問題を修正
  - WM_MOUSEHWHEEL をメインウィンドウで処理し、カーソル下の横スクロール可能な領域（ファイルリスト・ブレッドクラムバーなど）で横スクロールできるようにした

## [0.3.29] - 2026-02-02
### Fixed
- タイトルバーが白く表示される問題を修正（暗い背景色と白文字を明示的に指定して以前の外観に復元）
- ウィンドウを画面端にスナップした際、わずかな隙間が生じる問題を解消
  - スナップ配置コマンドにおいて、DWM（Desktop Window Manager）の拡張フレーム情報を考慮した正確な位置計算を実装
  - 上端（Top）のオフセットを適用し、上下の隙間を解消
  - DWM 取得に失敗した場合にシステムのリサイズ枠の厚さ（ResizeFrame*）で隙間を補正するフォールバックを追加
### Changed
- メインウィンドウのタイトルバー高さを 48px → 32px に変更（マニュアル・履歴ウィンドウのシステムタイトルバーと同じ太さに統一）
- ヘッダーバー・ツールバーをスリム化し、全体の調和を改善
  - ナビペインのビュー切り替えバー・A/Bペインのツールバー高さを 40px → 32px に統一
  - パス入力欄の高さを 28px → 24px に、タブのパディング・アイコンサイズを縮小
  - ツールバー・ステータスバーのアイコンを 14px → 12px、余白を詰めて一貫した見た目に調整
- メインウィンドウのデザインを Windows 11 の Fluent Design に最適化
  - `ui:FluentWindow` への移行とタイトルバーの統合により、モダンな外観と操作性を実現
  - Mica 効果（背景の透過）とウィンドウの角の丸みへの対応

## [0.3.28] - 2026-02-02
### Fixed
- ステータスバーのテキストやアイコンの垂直方向の配置を修正（中央揃えに変更し、デザインの整合性を向上）

## [0.3.27] - 2026-02-02
### Added
- ステータスバー左端にウィンドウ配置制御ボタンを追加
  - 左半分にスナップ、最大化、右半分にスナップの3つのボタンを配置
  - マルチディスプレイ環境やタスクバーの高さを考慮したスナップ機能を実装

## [0.3.26] - 2026-02-02
### Fixed
- ナビペイン非表示時にその領域が空白として残る問題を修正（非表示時に A/B ペインがウィンドウ幅を最大限使用するように改善）
- ネットワークサーバのルート（第一階層、例: `\\server`）への移動ができない不具合を修正（ナビゲーション時のディレクトリ存在チェックを緩和）
- UNCパス（ネットワークパス）におけるパンくずリスト（アドレスバー）の表示が正しく階層化されない問題を修正

## [0.3.25] - 2026-02-02
### Fixed
- マニュアル画面および更新履歴画面のスクロールバーが内容と重なったり、右端から浮いて見えたりする問題を修正（マージン設定をパディング設定に変更し、スクロールバーをウィンドウ右端へ配置）

## [0.3.24] - 2026-02-02
### Changed
- お気に入りビューがロックされている際の警告メッセージに、ロック解除を促す文言を追加
- ロック中の操作試行時に、ロックアイコンが点滅し、矢印が表示される視覚的演出を追加

## [0.3.23] - 2026-02-02
### Fixed
- Cドライブ直下（C:\）への移動時に、現在の作業ディレクトリへリダイレクトされてしまう不具合を修正
- アドレスバーへのドライブレターのみの入力（例: "C:"）をルートディレクトリへの移動として扱うように改善

## [0.3.22] - 2026-02-02
### Added
- お気に入りビューがロックされている状態で登録・移動を試みた際、警告メッセージを表示する機能を追加

## [0.3.21] - 2026-02-02
### Added
- パンくずリスト（アドレスバー）の動的省略表示機能を実装
  - WPF-UI ライブラリの BreadcrumbBar を採用し、パスが長くなった場合に先頭階層を自動省略（...）して末尾（現在地）を常に表示
  - 省略された階層を「...」ボタンからドロップダウンメニューで選択して移動できる機能を追加

## [0.3.20] - 2026-02-02
### Added
- タブをドラッグして前後に移動（並び替え）できる機能を追加

## [0.3.19] - 2026-02-02
### Added
- お気に入りリストのフォルダアイコンを場所（ローカル、ネットワーク、Box、SPO）に応じて自動的に切り替える機能を追加
- タブバーの空白部分をダブルクリックして新しいタブを作成する機能を追加

## [0.3.18] - 2026-02-02
### Fixed
- ネットワークサーバのルート（第一階層、例: `\\server`）にアクセスした際、共有フォルダ一覧が表示されない不具合を修正

## [0.3.17] - 2026-02-02
### Changed
- パス変更モード（トグル式）の状態を、フォルダ移動や新しいタブを開いた際にも維持するように変更
- アプリ終了時のパス変更モードの状態を保存し、次回起動時に復元するように改善

## [0.3.16] - 2026-02-02
### Changed
- パス変更ボタンをトグル式（ToggleButton）に変更し、現在のモード（パンくず表示 / パス入力）を視覚的に把握しやすく改善
- パス変更ボタンをオンにした際、自動的にパス入力欄へフォーカスし全選択するように改善

## [0.3.15] - 2026-02-02
### Fixed
- 高解像度ディスプレイ（High DPI）でのフォントのぼやけを解消するため、マニフェストファイルで DPI Aware (PerMonitorV2) を有効化

## [0.3.14] - 2026-02-02
### Changed
- タイトルバーから「 — by xxxx」を削除

## [0.3.13] - 2026-02-02
### Added
- 更新履歴画面とマニュアル画面を Esc キーで閉じられるように改善
### Changed
- 更新履歴画面とマニュアル画面のウィンドウ幅を拡大し、閲覧性を向上

## [0.3.12] - 2026-02-02
### Added
- お気に入りビューの空きスペースを右クリックした際に、コンテキストメニュー（新しいフォルダ）を表示する機能を追加
- お気に入りビューの右クリックメニューにアイコンを追加し、視認性を向上

## [0.3.11] - 2026-02-02
### Changed
- タブの幅を固定（160px）に変更し、連続してタブを閉じる際に「×」ボタンの位置がずれないように改善

## [0.3.10] - 2026-02-02
### Fixed
- タブの空白部分やナビペインからのドラッグ＆ドロップで新しいタブを開いた際、ファイル一覧が自動的に表示されず空白になる不具合を修正

## [0.3.9] - 2026-02-02
### Added
- ツールバーのお気に入り追加ボタンの横に、頻繁に使用する場所へのショートカットボタンを追加
  - デスクトップに遷移ボタン（モニターアイコン）
  - ダウンロードフォルダに遷移ボタン（ダウンロードアイコン）

## [0.3.8] - 2026-02-02
### Added
- アプリ終了時のフォルダパスおよびタブ状態の保存・復元機能を追加
  - アプリ終了時にAペイン・Bペインで開いていたすべてのタブと選択状態を保存
  - 次回起動時に前回終了時のタブ構成を自動的に復元するように変更

## [0.3.7] - 2026-02-02
### Added
- 現在のフォルダをお気に入りに追加するボタンをナビゲーションバーに追加
  - 「↑（上の階層へ）」ボタンの右側に配置

## [0.3.6] - 2026-02-02
### Fixed
- お気に入り機能において、同じフォルダを重複して登録できないように制限を追加
  - すでに登録済みのフォルダを追加しようとした場合、メッセージを表示してスキップするように改善

## [0.3.5] - 2026-02-02
### Added
- タブの空白部分にフォルダをドロップした際、新しいタブで表示する機能を追加
  - 外部エクスプローラー、お気に入り、ツリービュー、参照履歴からのドロップに対応

## [0.3.4] - 2026-02-02
### Changed
- ViewModel のリファクタリングを実施
  - `CommunityToolkit.Mvvm` の `[RelayCommand]` 属性を活用し、ボイラープレートコードを削減
  - `TabItemViewModel` のコマンド状態管理（戻る・進む・上へ）を `CanExecute` メソッドで最適化
  - プロパティ通知ロジックを整理し、コードの可読性を向上

## [0.3.3] - 2026-02-02
### Changed
- タブの「閉じる」ボタンのデザインを Chrome 風に改善
  - アイコンをシンプルな `X` に最適化（サイズ `9`）
  - ホバー時に円形の背景が表示されるモダンなスタイルに変更
- アクティブなタブの視認性を向上
  - 選択中のタブの上部にアクセントラインを表示し、テキストとアイコンを強調
  - 選択中のタブのフォントをセミボールド化し、判別しやすく調整

## [0.3.0] - 2026-02-02
### Added
- 各ペイン内でのマルチタブ機能を追加
  - `Ctrl + T`: 現在のパスで新しいタブを開く
  - `Ctrl + W`: 現在のタブを閉じる
  - `Ctrl + Tab`: 次のタブへ切り替え
- タブヘッダーにフォルダ名と保存場所の種類を示すアイコンを表示する機能を追加
### Changed
- ペイン構造を刷新し、タブ管理に対応したViewModel構成へリファクタリング

## [0.2.13] - 2026-02-02
### Fixed
- 参照履歴の右クリックメニュー（Aペインに表示 / Bペインに表示）が動作していなかった問題を修正

## [0.2.12] - 2026-02-02
### Fixed
- ツリービューの右クリック処理において、親アイテムがイベントを捕捉してしまう問題を修正（クリックされた要素の直近のアイテムのみ反応するように変更）

## [0.2.11] - 2026-02-02
### Fixed
- ツリービューの右クリックメニューにおいて、対象フォルダのバインディングが正しく機能するように修正（`PlacementTarget.DataContext` を経由するように変更）

## [0.2.10] - 2026-02-02
### Fixed
- ツリービューの右クリック処理を改善（フォーカス移動による自動選択を抑制）
- 右クリックメニューのデータコンテキスト参照を修正し、アクションが正しく実行されるよう調整

## [0.2.9] - 2026-02-02
### Changed
- ナビペインのツリービューにおいて、右クリック時にアイテムを選択しないように変更
- 右クリックメニューの項目を選択した際に初めて対象のペインに表示されるよう挙動を修正

## [0.2.8] - 2026-02-02
### Added
- ツリービューからペインへのドラッグ＆ドロップでフォルダを表示する機能を追加
- ツリービューの右クリックメニューに「Aペインに表示」「Bペインに表示」を追加

## [0.2.7] - 2026-02-02
### Added
- マウスの戻る（XButton1）・進む（XButton2）ボタンによるナビゲーションに対応
- 進むボタン（ArrowRight）をツールバーに追加
- Alt + Right ショートカットによる「進む」操作に対応

## [0.2.6] - 2026-02-02
### Added
- 参照履歴ビューの右下に「履歴クリア」ボタンを追加
- 履歴全削除時の確認メッセージ表示機能を実装

## [0.2.5] - 2026-02-02
### Added
- 参照履歴を日付別にグループ化して表示する機能を追加
- グループごとの折りたたみ（エクスパンダー）機能に対応
### Changed
- 起動時に当日以外の参照履歴グループを自動的に折りたたむように変更

## [0.2.4] - 2026-02-02
### Added
- 履歴アイテムのドラッグ時に、ドロップ先を案内するツールチップを表示する機能を追加

## [0.2.3] - 2026-02-02
### Added
- 参照履歴ビューに「参照日時」と「回数」の列を追加し、詳細情報を確認可能に変更
### Changed
- 参照履歴ビューをマルチカラム表示（名前、参照日時、回数）に刷新

## [0.2.2] - 2026-02-02
### Fixed
- 参照履歴ビューがお気に入りビューと重なって表示される不具合を修正
- フォルダ移動時に参照履歴ビューがリアルタイムに更新されない問題を修正

## [0.2.1] - 2026-02-02
### Added
- ナビペインに参照履歴ビューを追加（Ctrl+Shift+3 で切り替え可能）
- 履歴アイテムのダブルクリック、ドラッグ＆ドロップによるペインへの表示機能
- 履歴アイテムの右クリックメニュー（Aペインに表示 / Bペインに表示）
- 保存場所（Local, Server, Box, SPO）に応じた 4 種類のストレージアイコンを表示
### Changed
- アプリ更新履歴のアイコンを `History` から `ScrollText` に変更し、参照履歴との視認性を向上

## [0.1.0] - 2026-02-02
### Added
- フォルダ参照履歴の保存機能を追加（SQLite を使用）
- パスごとのアクセス回数（AccessCount）と最終アクセス日時（LastAccessed）を記録
- 保存場所（Local, Server, Box, SPO）の自動判別ロジックを統合
- 最終アクセスから1ヶ月以上経過した履歴の自動クリーンアップ機能を実装

## [0.0.9] - 2026-02-01
### Added
- タイトルバーに「更新履歴」表示アイコンを追加
- アプリ内から `CHANGELOG.md` の内容をモダンなレイアウトで確認できる機能を追加

## [0.0.8] - 2026-02-01
### Added
- ファイル一覧に「場所」列を追加し、ファイルの保存場所（Local, Server, Box, SPO）をアイコンで判別可能に変更
### Changed
- `settings.json` の保存形式を、手動編集しやすいようにインデント付きの整形済み JSON に変更

## [0.0.7] - 2026-02-01
### Changed
- SPO/OneDrive同期フォルダにおける一覧表示パフォーマンスを改善
- オフライン属性を持つファイルに対し、アイコン取得時の実体アクセスを回避してダウンロードが発生しないよう変更
- ナビペインの表示切り替えショートカットを `Ctrl+Shift+1`（お気に入り）および `Ctrl+Shift+2`（ツリービュー）に変更

## [0.0.6] - 2026-02-01
### Added
- SQLite (`sqlite-net-pcl`) の導入とデータ永続化の下地を構築
- `DatabaseService` によるデータベース接続とテーブル自動生成機能の追加
- アプリケーション設定保存用の `AppSetting` モデルを追加
### Changed
- パブリッシュ設定を単一ファイル実行形式（Single File）に変更し、配布ファイルを最適化
### Fixed
- アプリケーションアイコン（`app_icon2_1.ico`）をプロジェクトに正しく設定し、コンパイル時に適用されるよう修正

## [0.0.5] - 2026-02-01
### Added
- SQLite FTS5 検索エンジンの統合準備
- ストレージ別モダンアイコンの追加
### Changed
- タイトルバーの表記を「Zenith Filer v0.0.5 — by xxx」に変更（アセンブリ情報から動的に取得）
- `ZenithFiler.csproj` にバージョン情報を追加

