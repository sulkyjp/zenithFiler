using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
namespace ZenithFiler
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        /// <summary>初回起動時のスプラッシュ向け。InitializeAsync 完了時に発火する。</summary>
        public event EventHandler? InitializationComplete;

        private const int HOTKEY_ID = 9000;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int WHEEL_DELTA = 120;
        private const double KeyboardToMouseSwitchThreshold = 3.0; // キーボード操作からマウス操作へ戻す際のデッドゾーン（ピクセル）

        private WindowSettings _settings;

        // ContentRendered まで画面外に退避するための保存位置
        private double _savedLeft;
        private double _savedTop;
        private WindowState _savedWindowState;

        private WindowState _lastWindowState;
        private bool _isTitleBarDragFromMaximized;
        private Rect? _lastNormalBoundsBeforeMaximize;

        // キーボード操作中フラグとマウス位置の記録
        private bool _isKeyboardOperating;
        private Point _keyboardModeMouseStartPoint;
        private bool _hasKeyboardMouseStartPoint;

        // 起動ウェルカムアニメーション二重発火防止フラグ
        private bool _welcomeStarted = false;

        // Quick Preview
        private bool _isQuickPreviewOpen;
        private CancellationTokenSource? _quickPreviewCts;

        private static readonly HashSet<string> _previewableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".ini", ".cfg", ".conf", ".toml",
            ".cs", ".py", ".js", ".ts", ".jsx", ".tsx",
            ".json", ".xml", ".yaml", ".yml", ".md", ".markdown",
            ".css", ".scss", ".less",
            ".bat", ".cmd", ".ps1", ".sh",
            ".sql",
            ".java", ".cpp", ".c", ".h", ".hpp",
            ".rb", ".go", ".rs", ".php", ".swift", ".kt",
            ".xaml", ".csproj", ".sln", ".props", ".targets",
            ".gitignore", ".editorconfig", ".env",
        };

        // Quick Preview - PDF/Office support
        private static readonly HashSet<string> _pdfExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        private static readonly HashSet<string> _htmlExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".html", ".htm"
        };

        private static readonly HashSet<string> _spreadsheetExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".xls", ".xlsm", ".csv", ".tsv"
        };

        private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".webp"
        };

        private static readonly HashSet<string> _officePreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".doc", ".docm",
            ".pptx", ".ppt", ".pptm"
        };

        private bool _isWebView2Initialized;
        private bool _isWebView2InitFailed;
        private string? _tempPreviewPdfPath;
        private DispatcherTimer? _previewLoadingSpinnerTimer;

        /// <summary>
        /// 現在キーボード操作モードかどうか（他の View から参照用）。
        /// </summary>
        internal static bool IsKeyboardOperating
        {
            get
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    return mw._isKeyboardOperating;
                }
                return false;
            }
        }

        public MainWindow(WindowSettings settings)
        {
            InitializeComponent();

            // App.OnStartup で先読みした設定を即適用（中央→保存位置ジャンプを防止）
            _settings = settings;
            ApplySettingsToWindowAndViewModel(_settings);

            // ContentRendered まで画面外に退避（白画面防止）
            _savedLeft = this.Left;
            _savedTop = this.Top;
            _savedWindowState = this.WindowState;
            this.WindowState = WindowState.Normal;
            this.Left = -100000;
            this.Top = -100000;

            _lastWindowState = _savedWindowState;
            this.StateChanged += MainWindow_StateChanged;

            if (this.DataContext is MainViewModel vm)
            {
                vm.FocusRequested += Vm_FocusRequested;
                vm.FocusHistorySearchRequested += Vm_FocusHistorySearchRequested;
                vm.FocusActiveSearchRequested += Vm_FocusActiveSearchRequested;
                vm.FocusIndexSearchRequested += Vm_FocusIndexSearchRequested;
                vm.RequestScrollToFavorite = Vm_RequestScrollToFavorite;
                vm.RequestScrollToIndexSearchTarget = Vm_RequestScrollToIndexSearchTarget;
                vm.PropertyChanged += Vm_PropertyChanged_GlowBar;
                vm.CancelRetractionRequested += Vm_CancelRetractionRequested;
                vm.AnimatePaneFadeOut = AnimatePaneFadeOutAsync;
                vm.AnimatePaneFadeIn = AnimatePaneFadeInAsync;
                vm.AnimateControlDeckOpen = AnimateControlDeckOpenAsync;
                vm.AnimateControlDeckClose = AnimateControlDeckCloseAsync;
                vm.AppSettings.OnBeforeThemeChangeAsync = AnimateThemeTransitionBeginAsync;
                vm.AppSettings.OnAfterThemeChangeApplied = AnimateThemeTransitionEnd;
                vm.Notification.PropertyChanged += Notification_PropertyChanged;
                vm.MarkInitializationComplete();
            }

            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            this.KeyDown += MainWindow_KeyDown;
            this.PreviewMouseMove += MainWindow_PreviewMouseMove;
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
            this.Closing += MainWindow_Closing;
            this.DpiChanged += MainWindow_DpiChanged;
            this.ContentRendered += MainWindow_ContentRendered;

            // キーバインドの動的構築
            RebuildInputBindings();
            UpdateShortcutTooltips();
            App.KeyBindings.BindingsChanged += (_, _) =>
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    RebuildInputBindings();
                    UpdateShortcutTooltips();
                    RegisterGlobalHotKey();
                });
            };
        }

        /// <summary>KeyBindingService から動的に InputBindings を構築する。</summary>
        private void RebuildInputBindings()
        {
            InputBindings.Clear();
            if (DataContext is not MainViewModel vm) return;

            var kb = App.KeyBindings;

            void AddBinding(string actionId, ICommand command, object? parameter = null)
            {
                var def = kb.Get(actionId);
                if (def == null) return;
                var binding = new KeyBinding(command, def.ActiveKey, def.ActiveModifiers);
                if (parameter != null) binding.CommandParameter = parameter;
                InputBindings.Add(binding);
            }

            AddBinding("Global.Undo", vm.UndoCommand);
            AddBinding("Global.FocusActivePane", vm.FocusActivePaneCommand);
            AddBinding("Global.OpenControlDeck", vm.OpenControlDeckCommand);
            AddBinding("Global.FocusSearch", vm.FocusActiveSearchCommand);
            AddBinding("Global.FocusIndexSearch", vm.FocusIndexSearchCommand);
            AddBinding("Global.SetPaneCount1", vm.SetPaneCountCommand, "1");
            AddBinding("Global.SetPaneCount2", vm.SetPaneCountCommand, "2");
            AddBinding("Global.SidebarFavorites", vm.SetSidebarModeCommand, SidebarViewMode.Favorites);
            AddBinding("Global.SidebarTree", vm.SetSidebarModeCommand, SidebarViewMode.Tree);
            AddBinding("Global.SidebarHistory", vm.SetSidebarModeCommand, SidebarViewMode.History);
            AddBinding("Global.SidebarIndexSearch", vm.SetSidebarModeCommand, SidebarViewMode.IndexSearch);
            AddBinding("Global.SidebarWorkingSet", vm.SetSidebarModeCommand, SidebarViewMode.WorkingSet);
        }

        /// <summary>ツールチップのショートカット表示を KeyBindingService から動的に設定する。</summary>
        private void UpdateShortcutTooltips()
        {
            var kb = App.KeyBindings;

            void SetTip(FrameworkElement? el, string label, string actionId)
            {
                if (el == null) return;
                var shortcut = kb.GetShortcutText(actionId);
                el.ToolTip = string.IsNullOrEmpty(shortcut) ? label : $"{label} ({shortcut})";
            }

            SetTip(SidebarFavoritesBtn, "お気に入り", "Global.SidebarFavorites");
            SetTip(SidebarTreeBtn, "ツリービュー", "Global.SidebarTree");
            SetTip(SidebarHistoryBtn, "参照履歴", "Global.SidebarHistory");
            SetTip(SidebarIndexSearchBtn, "インデックス検索設定", "Global.SidebarIndexSearch");
            SetTip(SidebarWorkingSetBtn, "ワーキングセット", "Global.SidebarWorkingSet");
            SetTip(OpenSettingsBtn, "アプリ設定を開く", "Global.OpenControlDeck");
        }

        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
            // コンテンツ描画完了 → 保存位置に戻して一発表示（白画面を防止）
            this.Left = _savedLeft;
            this.Top = _savedTop;
            this.WindowState = _savedWindowState;
            this.Activate();
        }

        // ── ワーキングセット切り替え：ペインコンテナのフェードアウト/フェードイン ──

        /// <summary>PaneContentArea を 150ms で Opacity 0 にフェードアウトし、描画確定まで待つ。</summary>
        private async Task AnimatePaneFadeOutAsync()
        {
            if (!WindowSettings.ShowPaneTransitionsEnabled)
            {
                PaneContentArea.IsHitTestVisible = false;
                PaneContentArea.Opacity = 0;
                return;
            }

            var tcs = new TaskCompletionSource();
            PaneContentArea.IsHitTestVisible = false;
            var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (_, _) => tcs.SetResult();
            PaneContentArea.BeginAnimation(UIElement.OpacityProperty, anim);
            await tcs.Task;
            // Completed はタイムラインクロック完了で発火する。
            // 最終フレーム（Opacity=0）が実際にレンダリングされるまで待つ。
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        /// <summary>PaneContentArea を 180ms で Opacity 1 にフェードインし、完了を待つ。</summary>
        private Task AnimatePaneFadeInAsync()
        {
            if (!WindowSettings.ShowPaneTransitionsEnabled)
            {
                PaneContentArea.BeginAnimation(UIElement.OpacityProperty, null);
                PaneContentArea.Opacity = 1;
                PaneContentArea.IsHitTestVisible = true;
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource();
            var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (_, _) =>
            {
                // アニメーション保持を解除し、ベース値に復帰
                PaneContentArea.BeginAnimation(UIElement.OpacityProperty, null);
                PaneContentArea.Opacity = 1;
                PaneContentArea.IsHitTestVisible = true;
                tcs.SetResult();
            };
            PaneContentArea.BeginAnimation(UIElement.OpacityProperty, anim);
            return tcs.Task;
        }

        // ── テーマ切り替えトランジション ──

        /// <summary>テーマ適用前: オーバーレイを 130ms でフェードイン（カバー）。</summary>
        private async Task AnimateThemeTransitionBeginAsync()
        {
            if (!WindowSettings.ShowThemeEffectsEnabled) return;

            ThemeTransitionOverlay.Visibility = Visibility.Visible;
            ThemeTransitionOverlay.Opacity = 0;
            var tcs = new TaskCompletionSource();
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (_, _) => tcs.SetResult();
            ThemeTransitionOverlay.BeginAnimation(UIElement.OpacityProperty, anim);
            await tcs.Task;
        }

        /// <summary>テーマ適用後: オーバーレイを 250ms でフェードアウト（reveal）。30ms の BeginTime で新テーマ初回描画を待つ。</summary>
        private void AnimateThemeTransitionEnd()
        {
            if (!WindowSettings.ShowThemeEffectsEnabled)
            {
                ThemeTransitionOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                ThemeTransitionOverlay.Opacity = 0;
                ThemeTransitionOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(30)
            };
            anim.Completed += (_, _) =>
            {
                ThemeTransitionOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                ThemeTransitionOverlay.Opacity = 0;
                ThemeTransitionOverlay.Visibility = Visibility.Collapsed;
            };
            ThemeTransitionOverlay.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // ── テーマトースト表示/非表示アニメーション ──

        private void Notification_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(NotificationService.IsThemeToastVisible)) return;
            var vm = DataContext as MainViewModel;
            if (vm == null) return;
            if (vm.Notification.IsThemeToastVisible)
                AnimateThemeToastIn();
            else
                AnimateThemeToastOut();
        }

        private void AnimateThemeToastIn()
        {
            ThemeToastBorder.Visibility = Visibility.Visible;
            if (!WindowSettings.ShowThemeEffectsEnabled)
            {
                ThemeToastBorder.Opacity = 1;
                if (ThemeToastBorder.RenderTransform is TranslateTransform ttOff)
                {
                    ttOff.BeginAnimation(TranslateTransform.YProperty, null);
                    ttOff.Y = 0;
                }
                return;
            }
            ThemeToastBorder.Opacity = 0;
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease };
            var slideIn = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease };
            ThemeToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ((TranslateTransform)ThemeToastBorder.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideIn);
        }

        private void AnimateThemeToastOut()
        {
            if (!WindowSettings.ShowThemeEffectsEnabled)
            {
                ThemeToastBorder.BeginAnimation(UIElement.OpacityProperty, null);
                ThemeToastBorder.Opacity = 0;
                ThemeToastBorder.Visibility = Visibility.Collapsed;
                return;
            }
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500)) { EasingFunction = ease };
            fadeOut.Completed += (_, _) =>
            {
                ThemeToastBorder.BeginAnimation(UIElement.OpacityProperty, null);
                ThemeToastBorder.Opacity = 0;
                ThemeToastBorder.Visibility = Visibility.Collapsed;
            };
            ThemeToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // ── Control Deck オーバーレイ：開閉アニメーション ──

        private bool _isControlDeckOpen;

        /// <summary>Control Deck を開くアニメーション。PaneContentArea を Scale(0.95) + 暗転し、オーバーレイをフェードイン。</summary>
        private async Task AnimateControlDeckOpenAsync()
        {
            // QuickPreview が開いていたら先に閉じる
            if (_isQuickPreviewOpen) HideQuickPreview();

            _isControlDeckOpen = true;

            if (!WindowSettings.ShowPaneTransitionsEnabled)
            {
                PaneContentArea.IsHitTestVisible = false;
                PaneContentArea.Opacity = 0.4;
                ControlDeckOverlay.Visibility = Visibility.Visible;
                ControlDeckOverlay.Opacity = 1;
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(200);

            // PaneContentArea に ScaleTransform を設定
            if (PaneContentArea.RenderTransform is not ScaleTransform)
            {
                PaneContentArea.RenderTransform = new ScaleTransform(1, 1);
                PaneContentArea.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            var scale = (ScaleTransform)PaneContentArea.RenderTransform;

            // PaneContentArea: Scale 1→0.95, Opacity 1→0.4
            var scaleXAnim = new DoubleAnimation(0.95, duration) { EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation(0.95, duration) { EasingFunction = ease };
            var paneOpacityAnim = new DoubleAnimation(0.4, duration) { EasingFunction = ease };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            PaneContentArea.BeginAnimation(UIElement.OpacityProperty, paneOpacityAnim);
            PaneContentArea.IsHitTestVisible = false;

            // ControlDeckOverlay: Visibility=Visible → Opacity 0→1
            ControlDeckOverlay.Visibility = Visibility.Visible;
            var tcs = new TaskCompletionSource();
            var overlayAnim = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            overlayAnim.Completed += (_, _) => tcs.SetResult();
            ControlDeckOverlay.BeginAnimation(UIElement.OpacityProperty, overlayAnim);
            await tcs.Task;
        }

        /// <summary>Control Deck を閉じるアニメーション。オーバーレイをフェードアウトし、PaneContentArea を復元。</summary>
        private async Task AnimateControlDeckCloseAsync()
        {
            if (!WindowSettings.ShowPaneTransitionsEnabled)
            {
                ControlDeckOverlay.Opacity = 0;
                ControlDeckOverlay.Visibility = Visibility.Collapsed;
                PaneContentArea.BeginAnimation(UIElement.OpacityProperty, null);
                PaneContentArea.Opacity = 1;
                PaneContentArea.IsHitTestVisible = true;
                if (PaneContentArea.RenderTransform is ScaleTransform st)
                {
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    st.ScaleX = 1;
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    st.ScaleY = 1;
                }
                _isControlDeckOpen = false;
                return;
            }

            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            // ControlDeckOverlay: Opacity 1→0
            var tcsOverlay = new TaskCompletionSource();
            var overlayAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)) { EasingFunction = easeIn };
            overlayAnim.Completed += (_, _) => tcsOverlay.SetResult();
            ControlDeckOverlay.BeginAnimation(UIElement.OpacityProperty, overlayAnim);
            await tcsOverlay.Task;
            ControlDeckOverlay.Visibility = Visibility.Collapsed;

            // PaneContentArea を復元: Scale 0.95→1, Opacity 0.4→1
            var duration = TimeSpan.FromMilliseconds(200);
            if (PaneContentArea.RenderTransform is ScaleTransform scale)
            {
                var scaleXAnim = new DoubleAnimation(1, duration) { EasingFunction = easeOut };
                var scaleYAnim = new DoubleAnimation(1, duration) { EasingFunction = easeOut };
                scaleXAnim.Completed += (_, _) =>
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.ScaleX = 1;
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    scale.ScaleY = 1;
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            }

            var tcsPane = new TaskCompletionSource();
            var paneOpacityAnim = new DoubleAnimation(1, duration) { EasingFunction = easeOut };
            paneOpacityAnim.Completed += (_, _) =>
            {
                PaneContentArea.BeginAnimation(UIElement.OpacityProperty, null);
                PaneContentArea.Opacity = 1;
                PaneContentArea.IsHitTestVisible = true;
                tcsPane.SetResult();
            };
            PaneContentArea.BeginAnimation(UIElement.OpacityProperty, paneOpacityAnim);
            await tcsPane.Task;
            _isControlDeckOpen = false;
        }

        /// <summary>
        /// FileOperationProgress 変化時に GlowProgressBar を DoubleAnimation（0.2s CubicEase）で滑らかに更新する。
        /// BeginAnimation で直接制御するため XAML バインディングは使用しない。
        /// PropertyChanged が非UIスレッドから発火した場合も Dispatcher.InvokeAsync で安全にマーシャリングする。
        /// </summary>
        private void Vm_PropertyChanged_GlowBar(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.FileOperationProgress)) return;
            if (sender is not MainViewModel vm) return;
            if (!WindowSettings.ShowGlowBarEnabled) return;

            // 非UIスレッドからの呼び出しを安全にUIスレッドへ転送する
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => Vm_PropertyChanged_GlowBar(sender, e));
                return;
            }

            double target = vm.FileOperationProgress;
            // 現在のアニメーション描画値との差分に応じて持続時間を伸縮させ、
            // 大きなジャンプでもスーッと滑らかに伸びる演出を実現する。
            // 小ジャンプ(〜5%): 200ms、中ジャンプ(〜20%): 400ms、大ジャンプ(50%+): 700ms
            double current = GlowProgressBar.Value;
            double delta = Math.Abs(target - current);
            double durationMs = Math.Clamp(150 + delta * 12, 150, 800);

            var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            GlowProgressBar.BeginAnimation(ProgressBar.ValueProperty, anim);
        }

        /// <summary>
        /// キャンセル時の逆行（リトラクション）アニメーション。
        /// グロウバーが現在位置から 0% へ 0.3s で加速しながら戻り、「吸い込まれる」演出を実現する。
        /// CubicEase EaseIn: ゆっくり始まって末尾に向かって急加速 → シュルシュル感。
        /// </summary>
        private void Vm_CancelRetractionRequested(object? sender, EventArgs e)
        {
            if (!WindowSettings.ShowGlowBarEnabled) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => Vm_CancelRetractionRequested(sender, e));
                return;
            }

            // From は指定しない → WPF が現在のアニメーション位置を自動的に From として使う
            var anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            GlowProgressBar.BeginAnimation(ProgressBar.ValueProperty, anim);
        }

        /// <summary>
        /// 起動ウェルカムアニメーションをコードビハインドで一元制御する。
        /// _welcomeStarted フラグで Loaded の二重発火を防止する。
        /// async/await でステップ実行と待機を行う。
        /// </summary>
        private async void StartWelcomeAnimation()
        {
            try
            {
            if (_welcomeStarted) return;
            _welcomeStarted = true;

            // ウェルカム中は通知メッセージのステータスバー表示を抑制
            App.Notification.IsWelcomeActive = true;

            // 初期状態を確実にリセット
            WelcomePanel.Opacity    = 0;
            WelcomeTrans.Y          = 0;
            WelcomePanel.Visibility = Visibility.Visible;
            WelcomeProgressText.Text = ""; // 最初は空

            // 通常用 TextBlock は物理的に Collapsed にして重複を排除
            StatusInfoGroup.Visibility = Visibility.Collapsed;
            StatusMessageGrid.Visibility = Visibility.Collapsed;
            CenterNotificationGroup.Visibility = Visibility.Collapsed;

            // 重要：レイアウトの再計算を強制し、「座席」を確定させてからアニメーションを開始する
            this.UpdateLayout();

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var easeIn  = new CubicEase { EasingMode = EasingMode.EaseIn };

            // 初回起動時は長めのタイミング、通常起動時は短縮
            bool isFirst = App.IsFirstLaunch;
            double enterSec   = isFirst ? 1.5 : 0.6;
            int    enterWait  = isFirst ? 1600 : 500;
            int    dotWait    = isFirst ? 500 : 250;
            int    holdWait   = isFirst ? 5000 : 800;
            double exitSec    = isFirst ? 2.0 : 0.8;

            // 1. 登場: Opacity 0->1, Y 8->0
            var sbEnter = new Storyboard();
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(enterSec)) { EasingFunction = easeOut };
            Storyboard.SetTarget(fadeIn, WelcomePanel);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            var slideUp = new DoubleAnimation(8, 0, TimeSpan.FromSeconds(enterSec)) { EasingFunction = easeOut };
            Storyboard.SetTarget(slideUp, WelcomeTrans);
            Storyboard.SetTargetProperty(slideUp, new PropertyPath(TranslateTransform.YProperty));

            sbEnter.Children.Add(fadeIn);
            sbEnter.Children.Add(slideUp);
            sbEnter.Begin();

            // 登場待ち + 最初の "Ready" 表示維持
            await Task.Delay(enterWait);

            // 2. ステップ実行
            WelcomeProgressText.Text = ".";
            await Task.Delay(dotWait);

            WelcomeProgressText.Text = "..";
            await Task.Delay(dotWait);

            WelcomeProgressText.Text = "...";
            await Task.Delay(dotWait);

            WelcomeProgressText.Text = "...Complete";

            // 3. 維持
            await Task.Delay(holdWait);

            // 4. 退場＆通常表示へのクロスフェード
            // 通常表示の準備
            CenterNotificationGroup.Visibility = Visibility.Visible;
            StatusInfoGroup.Opacity = 0;
            StatusInfoGroup.Visibility = Visibility.Visible;
            StatusMessageGrid.Opacity = 0;
            StatusMessageGrid.Visibility = Visibility.Visible;

            var sbExit = new Storyboard();

            // 起動メッセージ: Opacity 1->0, Y 0->-10
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(exitSec)) { EasingFunction = easeIn };
            Storyboard.SetTarget(fadeOut, WelcomePanel);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var slideOut = new DoubleAnimation(0, -10, TimeSpan.FromSeconds(exitSec)) { EasingFunction = easeIn };
            Storyboard.SetTarget(slideOut, WelcomeTrans);
            Storyboard.SetTargetProperty(slideOut, new PropertyPath(TranslateTransform.YProperty));

            // 通常ステータス: Opacity 0->1
            var statusFadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(exitSec)) { EasingFunction = easeOut };
            Storyboard.SetTarget(statusFadeIn, StatusInfoGroup);
            Storyboard.SetTargetProperty(statusFadeIn, new PropertyPath(UIElement.OpacityProperty));

            // StatusMessageGrid もフェードイン
            var msgFadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(exitSec)) { EasingFunction = easeOut };
            Storyboard.SetTarget(msgFadeIn, StatusMessageGrid);
            Storyboard.SetTargetProperty(msgFadeIn, new PropertyPath(UIElement.OpacityProperty));

            sbExit.Children.Add(fadeOut);
            sbExit.Children.Add(slideOut);
            sbExit.Children.Add(statusFadeIn);
            sbExit.Children.Add(msgFadeIn);

            sbExit.Completed += (s, e) =>
            {
                // 完了後: WelcomePanel を物理的に隠し、StatusInfoGroup を表示状態に確定させる
                WelcomePanel.Visibility = Visibility.Collapsed;

                // 退場アニメーションのクロックを解放
                WelcomePanel.BeginAnimation(UIElement.OpacityProperty, null);
                WelcomeTrans.BeginAnimation(TranslateTransform.YProperty, null);

                StatusInfoGroup.BeginAnimation(UIElement.OpacityProperty, null);
                StatusInfoGroup.Opacity = 1;
                StatusInfoGroup.Visibility = Visibility.Visible;

                StatusMessageGrid.BeginAnimation(UIElement.OpacityProperty, null);
                StatusMessageGrid.Opacity = 1;
                StatusMessageGrid.Visibility = Visibility.Visible;

                // ウェルカム完了 — 通知メッセージの表示を解禁
                App.Notification.IsWelcomeActive = false;
            };

            sbExit.Begin();
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] StartWelcomeAnimation: {ex.Message}"); }
        }

        /// <summary>ウェルカムアニメーションをスキップし、ステータスバーを即表示する。</summary>
        private void ShowStatusBarImmediate()
        {
            _welcomeStarted = true;
            WelcomePanel.Visibility = Visibility.Collapsed;
            StatusInfoGroup.Opacity = 1;
            StatusInfoGroup.Visibility = Visibility.Visible;
            StatusMessageGrid.Opacity = 1;
            StatusMessageGrid.Visibility = Visibility.Visible;
            CenterNotificationGroup.Visibility = Visibility.Visible;
        }

        private void ApplySettingsToWindowAndViewModel(WindowSettings settings)
        {
            NormalizeWindowPlacement(
                settings,
                out var left,
                out var top,
                out var width,
                out var height,
                out var state);

            this.Width = width;
            this.Height = height;
            this.Left = left;
            this.Top = top;
            this.WindowState = state;

            if (this.DataContext is MainViewModel vm)
            {
                vm.SetNormalSidebarWidth(settings.SidebarWidth);
                vm.SidebarWidth = new GridLength(settings.SidebarWidth);
                vm.IsSidebarVisible = settings.IsSidebarVisible;
                vm.SidebarMode = settings.SidebarMode;
                vm.SidebarWidth = new GridLength(vm.TargetSidebarWidth);
                vm.PaneCount = settings.PaneCount;
                vm.IsAlwaysOnTop = settings.IsAlwaysOnTop;
                vm.Favorites.IsLocked = settings.IsFavoritesLocked;
                vm.Favorites.ConfirmDelete = settings.ConfirmDeleteFavorites;
                vm.Favorites.SearchMode = settings.FavoritesSearchMode;
                vm.IsNavWidthLocked = settings.IsNavWidthLocked;
                vm.IsTreeViewLocked = settings.IsTreeViewLocked;
                vm.IndexSearchSettings.LoadPaths(settings.IndexSearchTargetPaths ?? new());
                vm.IndexSearchSettings.LoadItemSettings(settings.IndexItemSettings);
                // settings.json からロック状態を復元
                foreach (var lockedPath in settings.IndexSearchLockedPaths ?? new())
                {
                    if (!string.IsNullOrWhiteSpace(lockedPath))
                        App.IndexService.SetLocked(lockedPath, true);
                }
                vm.IndexSearchSettings.RefreshStatus();
                vm.IndexSearchSettings.RebuildScopeItems(settings.IndexSearchScopePaths);
                vm.AppSettings.LoadHomePaths(settings.LeftPane.HomePath ?? string.Empty, settings.RightPane.HomePath ?? string.Empty);
                vm.AppSettings.LoadSearchBehavior(settings.SearchBehavior);
                vm.AppSettings.LoadSearchResultPathBehavior(settings.SearchResultPathBehavior);
                vm.AppSettings.LoadContextMenuMode(settings.ContextMenuMode);
                vm.AppSettings.LoadAutoSwitchToSinglePaneOnSearch(settings.AutoSwitchToSinglePaneOnSearch);
                vm.AppSettings.LoadIndexSettings(settings.IndexSettings);
                vm.AppSettings.LoadSearchResultFileTypeFilters(settings.SearchResultFileTypeFilterEnabled);
                // 適用済みテーマでUIを初期化（Autoモード時はランダム選択後のテーマ名に同期）
                vm.AppSettings.LoadTheme(App.StartupAppliedThemeName ?? settings.ThemeName);
                vm.AppSettings.LoadThemeSettings(settings);  // モード・カテゴリ復元

                // ペイン個別テーマ: ResourceDictionary を登録し、Pane モード起動時に復元
                vm.AppSettings.RegisterPaneResources(
                    SidebarBorder.Resources,
                    LeftPaneControl.Resources,
                    RightPaneControl.Resources);
                vm.AppSettings.LoadShowStartupToast(settings.ShowStartupToast);
                vm.AppSettings.LoadDisplayAndGeneralAndSearchSettings(settings);
                vm.AppSettings.LoadPaneThemeNames(settings);
                if (settings.CurrentThemeMode == "Pane")
                {
                    void ApplyIfSet(string name, ResourceDictionary dict)
                    {
                        if (!string.IsNullOrEmpty(name))
                            App.ThemeService.ApplyThemeLive(name, dict);
                    }
                    ApplyIfSet(settings.NavPaneThemeName, SidebarBorder.Resources);
                    ApplyIfSet(settings.APaneThemeName,   LeftPaneControl.Resources);
                    ApplyIfSet(settings.BPaneThemeName,   RightPaneControl.Resources);
                }

                vm.SearchFilter.LoadFromSettings(settings);
                vm.SearchPresets.LoadFromSettings(settings);

                App.IndexService.ConfigureIndexUpdate(settings.IndexSettings, () => vm.IndexSearchSettings.GetPathsForSave() ?? new System.Collections.Generic.List<string>(), settings.IndexItemSettings);

                if (settings.IndexSettings?.UpdateMode != IndexUpdateMode.Manual)
                {
                    var paths = (vm.IndexSearchSettings.GetPathsForSave() ?? new System.Collections.Generic.List<string>())
                        .Where(p => !string.IsNullOrEmpty(p) && !App.IndexService.IsRootLocked(p))
                        .ToList();
                    if (paths.Count > 0)
                    {
                        var progress = new System.Progress<IndexingProgress>(p =>
                        {
                            App.Notification.IndexingStatusMessage =
                                $"インデックス更新中: {p.ProcessedCount:N0} 件";
                        });
                        App.IndexService.TriggerUpdateNow(paths, progress);
                    }
                }

                vm.LeftPane.IsGroupFoldersFirst = settings.LeftPane.IsGroupFoldersFirst;
                vm.LeftPane.IsAdaptiveColumnsEnabled = settings.LeftPane.IsAdaptiveColumnsEnabled;
                vm.LeftPane.SortProperty = settings.LeftPane.SortProperty;
                vm.LeftPane.SortDirection = settings.LeftPane.SortDirection;
                vm.LeftPane.IsPathEditMode = settings.LeftPane.IsPathEditMode;

                vm.RightPane.IsGroupFoldersFirst = settings.RightPane.IsGroupFoldersFirst;
                vm.RightPane.IsAdaptiveColumnsEnabled = settings.RightPane.IsAdaptiveColumnsEnabled;
                vm.RightPane.SortProperty = settings.RightPane.SortProperty;
                vm.RightPane.SortDirection = settings.RightPane.SortDirection;
                vm.RightPane.IsPathEditMode = settings.RightPane.IsPathEditMode;
            }
        }

        private static void NormalizeWindowPlacement(
            WindowSettings settings,
            out double left,
            out double top,
            out double width,
            out double height,
            out WindowState state)
        {
            width = double.IsNaN(settings.Width) || settings.Width <= 0 ? 800 : settings.Width;
            height = double.IsNaN(settings.Height) || settings.Height <= 0 ? 600 : settings.Height;
            // 初回起動時は Left/Top が NaN（未設定）→ スクリーン中央に配置
            bool centerOnScreen = double.IsNaN(settings.Left) || double.IsNaN(settings.Top);
            left = double.IsNaN(settings.Left) ? 0 : settings.Left;
            top = double.IsNaN(settings.Top) ? 0 : settings.Top;
            state = settings.State;

            var windowRect = new Rect(left, top, width, height);

            Rect primaryWorkArea = Rect.Empty;
            if (System.Windows.Forms.Screen.PrimaryScreen is System.Windows.Forms.Screen primary)
            {
                var wa = primary.WorkingArea;
                primaryWorkArea = new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
            }

            bool intersectsAny = false;
            if (!centerOnScreen)
            {
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    var wa = screen.WorkingArea;
                    var waRect = new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
                    if (windowRect.IntersectsWith(waRect))
                    {
                        intersectsAny = true;
                        break;
                    }
                }
            }

            if (centerOnScreen || !intersectsAny)
            {
                if (primaryWorkArea.IsEmpty)
                {
                    primaryWorkArea = new Rect(
                        SystemParameters.VirtualScreenLeft,
                        SystemParameters.VirtualScreenTop,
                        SystemParameters.VirtualScreenWidth,
                        SystemParameters.VirtualScreenHeight);
                }

                double maxWidth = primaryWorkArea.Width * 0.8;
                double maxHeight = primaryWorkArea.Height * 0.8;
                width = Math.Min(width, maxWidth);
                height = Math.Min(height, maxHeight);

                width = Math.Max(width, 400);
                height = Math.Max(height, 300);

                left = primaryWorkArea.Left + (primaryWorkArea.Width - width) / 2;
                top = primaryWorkArea.Top + (primaryWorkArea.Height - height) / 2;

                state = WindowState.Normal;
            }
            else
            {
                var workArea = primaryWorkArea.IsEmpty
                    ? new Rect(
                        SystemParameters.VirtualScreenLeft,
                        SystemParameters.VirtualScreenTop,
                        SystemParameters.VirtualScreenWidth,
                        SystemParameters.VirtualScreenHeight)
                    : primaryWorkArea;

                double maxWidth = workArea.Width * 0.8;
                double maxHeight = workArea.Height * 0.8;

                if (width > maxWidth) width = maxWidth;
                if (height > maxHeight) height = maxHeight;
            }
        }

        /// <summary>
        /// ウィンドウがアクティブになったとき、フォーカスを持つ要素がなければアクティブペインに復元する。
        /// ControlDeck / ダイアログ等の Collapse・Hide 後にフォーカスが失われた場合の安全網。
        /// </summary>
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // _isControlDeckOpen が true の間はオーバーレイが管理するためスキップ
            if (_isControlDeckOpen) return;

            Dispatcher.BeginInvoke(() =>
            {
                // ペイン内のどこかに既にフォーカスがあれば何もしない
                var focused = Keyboard.FocusedElement as DependencyObject;
                bool inPane = focused != null &&
                              (IsDescendantOf(focused, LeftPaneControl) ||
                               IsDescendantOf(focused, RightPaneControl));
                if (inPane) return;

                Vm_FocusRequested();
            }, DispatcherPriority.Input);
        }

        /// <summary>
        /// ウィンドウが非アクティブになったとき（ダイアログ表示など）にキーボード操作モードを解除し、マウスカーソルを復元する。
        /// ダイアログ内でマウス操作が可能になる。
        /// </summary>
        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            if (_isQuickPreviewOpen) HideQuickPreview();
            ExitKeyboardOperationMode();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // グローバルホットキーの登録（KeyBindingService の設定に従う）
            RegisterGlobalHotKey();

            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndHook);
        }

        /// <summary>KeyBindingService の Global.FocusActivePane 設定に基づいてグローバルホットキーを登録する。</summary>
        private void RegisterGlobalHotKey()
        {
            var helper = new WindowInteropHelper(this);
            var hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            // 既存のホットキーを解除
            UnregisterHotKey(hwnd, HOTKEY_ID);

            // KeyBindingService から現在のキー設定を取得
            var def = App.KeyBindings.Get("Global.FocusActivePane");
            if (def == null) return;

            var (vk, mods) = ConvertToWin32HotKey(def.ActiveKey, def.ActiveModifiers);
            if (vk == 0) return;

            if (!RegisterHotKey(hwnd, HOTKEY_ID, mods, (uint)vk))
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalHotKey] RegisterHotKey failed: vk=0x{vk:X}, mods={mods}");
            }
        }

        /// <summary>WPF の Key/ModifierKeys を Win32 の仮想キーコード/HotKeyModifiers に変換する。</summary>
        private static (int vk, HotKeyModifiers mods) ConvertToWin32HotKey(Key key, ModifierKeys modifiers)
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            HotKeyModifiers mods = 0;
            if (modifiers.HasFlag(ModifierKeys.Control)) mods |= HotKeyModifiers.MOD_CONTROL;
            if (modifiers.HasFlag(ModifierKeys.Shift))   mods |= HotKeyModifiers.MOD_SHIFT;
            if (modifiers.HasFlag(ModifierKeys.Alt))      mods |= HotKeyModifiers.MOD_ALT;
            if (modifiers.HasFlag(ModifierKeys.Windows))  mods |= HotKeyModifiers.MOD_WIN;
            return (vk, mods);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ActivateApp();
                handled = true;
            }
            else if (msg == WM_MOUSEHWHEEL)
            {
                if (HandleHorizontalMouseWheel(wParam, lParam))
                {
                    handled = true;
                    return (IntPtr)1;
                }
            }
            else if (msg == WM_DEVICECHANGE)
            {
                int evt = wParam.ToInt32();
                if (evt == DBT_DEVICEARRIVAL || evt == DBT_DEVICEREMOVECOMPLETE)
                {
                    ScheduleDriveRefresh();
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// マウスの左右スクロール（ホイールチルト）を処理し、カーソル下の ScrollViewer を横スクロールする。
        /// </summary>
        private bool HandleHorizontalMouseWheel(IntPtr wParam, IntPtr lParam)
        {
            // HIWORD(wParam) = ホイールの回転量（正=右、負=左）
            int delta = (short)((long)wParam >> 16);
            if (delta == 0) return false;

            // lParam = カーソル位置（画面座標）
            int x = (short)(lParam.ToInt32() & 0xffff);
            int y = (short)((lParam.ToInt32() >> 16) & 0xffff);
            var screenPoint = new Point(x, y);

            if (!this.IsLoaded || this.Content == null) return false;

            var windowPoint = this.PointFromScreen(screenPoint);
            var hit = VisualTreeHelper.HitTest(this, windowPoint);
            if (hit?.VisualHit is not DependencyObject dep) return false;

            ScrollViewer? scrollViewer = null;
            for (var current = dep; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is ScrollViewer sv && sv.ScrollableWidth > 0)
                {
                    scrollViewer = sv;
                    break;
                }
            }

            if (scrollViewer == null) return false;

            int steps = Math.Max(1, Math.Abs(delta) / WHEEL_DELTA);
            for (int i = 0; i < steps; i++)
            {
                if (delta > 0)
                    scrollViewer.LineRight();
                else
                    scrollViewer.LineLeft();
            }
            return true;
        }

        private DispatcherTimer? _driveRefreshDebounceTimer;

        private void ScheduleDriveRefresh()
        {
            if (_driveRefreshDebounceTimer == null)
            {
                _driveRefreshDebounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(600)
                };
                _driveRefreshDebounceTimer.Tick += async (s, e) =>
                {
                    _driveRefreshDebounceTimer.Stop();
                    if (this.DataContext is MainViewModel vm)
                    {
                        await vm.DirectoryTree.RefreshDrivesAsync();
                    }
                };
            }
            _driveRefreshDebounceTimer.Stop();
            _driveRefreshDebounceTimer.Start();
        }

        private void ActivateApp()
        {
            // Hide() でトレイに格納された場合、Show() で再表示する
            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();

            Vm_FocusRequested();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // お気に入り・プロジェクトセットの復元（設定は App.OnStartup で先読み済み）
            if (this.DataContext is MainViewModel vmLoad)
            {
                vmLoad.Favorites.LoadFromSettings(_settings);
                vmLoad.ProjectSets.LoadFromSettings(_settings);
            }

            // VM 初期化（2 秒ウォッチドッグ付き）
            if (this.DataContext is MainViewModel vm)
            {
                var initTask = vm.InitializeAsync(_settings);
                var initCompleted = await Task.WhenAny(initTask, Task.Delay(2000));
                if (initCompleted != initTask)
                    _ = App.FileLogger.LogAsync("[Startup] InitializeAsync が 2 秒を超過 — 部分初期化で続行します");

                await Dispatcher.InvokeAsync(() => Vm_FocusRequested(), DispatcherPriority.Loaded);
                InitializationComplete?.Invoke(this, EventArgs.Empty);
                if (WindowSettings.ShowStartupEffectsEnabled)
                    StartWelcomeAnimation();
                else
                    ShowStatusBarImmediate();

                // 起動時テーマトースト（ウィンドウ描画完了・GlowBar 消灯後に表示）
                if (App.StartupAppliedThemeName is string toastTheme && vm.AppSettings.ShowStartupToast)
                {
                    _ = Dispatcher.BeginInvoke(async () =>
                    {
                        await Task.Delay(900);
                        if (vm.AppSettings.ActiveThemeMode == ThemeApplyMode.Pane)
                        {
                            var parts = new System.Collections.Generic.List<string>();
                            if (!string.IsNullOrEmpty(vm.AppSettings.NavPaneThemeName))
                                parts.Add($"Navi: {vm.AppSettings.NavPaneThemeName}");
                            if (!string.IsNullOrEmpty(vm.AppSettings.APaneThemeName))
                                parts.Add($"A: {vm.AppSettings.APaneThemeName}");
                            if (!string.IsNullOrEmpty(vm.AppSettings.BPaneThemeName))
                                parts.Add($"B: {vm.AppSettings.BPaneThemeName}");
                            var text = parts.Count > 0 ? string.Join("  |  ", parts) : toastTheme;
                            vm.Notification.ShowThemeToast(text, "Applied Themes");
                        }
                        else
                        {
                            vm.Notification.ShowThemeToast(toastTheme);
                        }
                    }, DispatcherPriority.Background);
                }
            }
        }

        /// <summary>お気に入り登録後に追加項目を表示するようスクロールする。</summary>
        private void Vm_RequestScrollToFavorite(FavoriteItem item)
        {
            var tree = FavoritesTree;
            var list = FavoritesFilteredList;
            var mainVm = DataContext as MainViewModel;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (mainVm?.Favorites.IsSearching == true && list != null)
                {
                    list.ScrollIntoView(item);
                }
                else if (tree != null)
                {
                    var container = tree.ItemContainerGenerator.ContainerFromItem(item);
                    if (container is System.Windows.Controls.TreeViewItem tvi)
                        tvi.BringIntoView();
                }
            }), DispatcherPriority.Loaded);
        }

        /// <summary>インデックス登録フォルダをダブルクリックで登録内容確認を実行する。</summary>
        private void IndexSearchTargetItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem lbi && lbi.DataContext is IndexSearchTargetItemViewModel item)
            {
                if (this.DataContext is MainViewModel vm)
                {
                    vm.IndexSearchSettings.ConfirmIndexingCommand.Execute(item);
                    e.Handled = true;
                }
            }
        }

        /// <summary>インデックス検索登録後に追加項目を表示するようスクロールする。</summary>
        private void Vm_RequestScrollToIndexSearchTarget(IndexSearchTargetItemViewModel item)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IndexSearchTargetList?.ScrollIntoView(item);
            }), DispatcherPriority.Loaded);
        }


        private void IndexMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // IndexSearchTargetList は ListBox に移行済み。GridView カラム幅調整は不要。

        private static SolidColorBrush CreateDropHighlightBrush()
        {
            if (Application.Current.Resources["OnePointDarkColor"] is Color c)
                return new SolidColorBrush(Color.FromArgb(0x30, c.R, c.G, c.B));
            return new SolidColorBrush(Color.FromArgb(0x30, 0x54, 0x5B, 0x64));
        }
        private static readonly SolidColorBrush _dropHighlightBrush = CreateDropHighlightBrush();

        private void IndexDropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && sender is Border border)
            {
                border.Background = _dropHighlightBrush;
                border.BorderBrush = (Brush)FindResource("AccentBrush");
                border.BorderThickness = new Thickness(2);
                border.CornerRadius = new CornerRadius(6);
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void IndexDropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
            e.Handled = true;
        }

        private void IndexDropZone_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = Brushes.Transparent;
                border.BorderBrush = null;
                border.BorderThickness = new Thickness(0);
            }
        }

        private async void IndexDropZone_Drop(object sender, DragEventArgs e)
        {
            // 視覚ハイライトを解除
            if (sender is Border border)
            {
                border.Background = Brushes.Transparent;
                border.BorderBrush = null;
                border.BorderThickness = new Thickness(0);
            }

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
            if (DataContext is not MainViewModel vm) return;

            await vm.IndexSearchSettings.AddFoldersByDropAsync(paths);
        }

        private void Vm_FocusHistorySearchRequested()
        {
            HistorySearchBox.Focus();
        }

        private void Vm_FocusActiveSearchRequested()
        {
            if (DataContext is not MainViewModel vm) return;

            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            bool inLeft = IsDescendantOf(focused, LeftPaneControl);
            bool inRight = IsDescendantOf(focused, RightPaneControl);

            // A/Bペイン内でインデックス検索モードかつ検索バーにフォーカスがある場合、通常モードに戻す
            if (inLeft || inRight)
            {
                var pane = inLeft ? LeftPaneControl : RightPaneControl;
                var tabContent = pane.GetActiveTabContent();
                if (tabContent != null && tabContent.DataContext is TabItemViewModel tabVm && tabVm.IsIndexSearchMode)
                {
                    if (tabContent.SearchTextBox != null &&
                        (focused == tabContent.SearchTextBox || IsDescendantOf(focused, tabContent.SearchTextBox)))
                    {
                        tabVm.ExitIndexSearchModeCommand.Execute(null);
                        return;
                    }
                }
            }

            if (inLeft)
            {
                LeftPaneControl.FocusSearchBox();
            }
            else if (inRight)
            {
                RightPaneControl.FocusSearchBox();
            }
            else if (IsDescendantOf(focused, SidebarBorder))
            {
                // ナビペイン: お気に入り or 履歴の検索バーにフォーカス（ツリービューには検索バーなし → アクティブペインの検索バーへ）
                switch (vm.SidebarMode)
                {
                    case SidebarViewMode.Favorites:
                        FavoritesSearchBox.Focus();
                        break;
                    case SidebarViewMode.History:
                        HistorySearchBox.Focus();
                        break;
                    case SidebarViewMode.Tree:
                    case SidebarViewMode.IndexSearch:
                    case SidebarViewMode.AppSettings:
                        // ツリー・インデックス検索設定・アプリ設定ビューには検索バーがないため、アクティブペインの検索バーにフォーカス
                        if (vm.ActivePane == vm.LeftPane)
                            LeftPaneControl.FocusSearchBox();
                        else
                            RightPaneControl.FocusSearchBox();
                        break;
                }
            }
            else
            {
                // フォーカスがウィンドウ全体にあるなど、どれにも該当しない場合はアクティブペインの検索バーへ
                if (vm.ActivePane == vm.LeftPane)
                    LeftPaneControl.FocusSearchBox();
                else
                    RightPaneControl.FocusSearchBox();
            }
        }

        private void Vm_FocusIndexSearchRequested()
        {
            if (DataContext is not MainViewModel vm) return;

            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            bool inLeft = IsDescendantOf(focused, LeftPaneControl);
            bool inRight = IsDescendantOf(focused, RightPaneControl);

            if (inLeft)
            {
                LeftPaneControl.FocusSearchBoxAndEnterIndexMode();
            }
            else if (inRight)
            {
                RightPaneControl.FocusSearchBoxAndEnterIndexMode();
            }
            else if (IsDescendantOf(focused, SidebarBorder))
            {
                // ナビペインにフォーカスがある場合はアクティブペインの検索バーへフォーカスしインデックスモードへ
                if (vm.ActivePane == vm.LeftPane)
                    LeftPaneControl.FocusSearchBoxAndEnterIndexMode();
                else
                    RightPaneControl.FocusSearchBoxAndEnterIndexMode();
            }
            else
            {
                if (vm.ActivePane == vm.LeftPane)
                    LeftPaneControl.FocusSearchBoxAndEnterIndexMode();
                else
                    RightPaneControl.FocusSearchBoxAndEnterIndexMode();
            }
        }

        private void Vm_FocusRequested()
        {
            if (this.DataContext is MainViewModel vm)
            {
                if (vm.ActivePane == vm.LeftPane)
                {
                    LeftPaneControl.FocusList();
                }
                else if (vm.ActivePane == vm.RightPane)
                {
                    RightPaneControl.FocusList();
                }
            }
        }

        /// <summary>
        /// TAB / ← / → で Aペイン・Bペインを切り替える。フォーカスがペイン内のどこにあっても動作する。
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 矢印キーや Enter などの実質的な操作キーでのみ「キーボード操作モード」に入り、マウスカーソルを非表示にする。
            // Shift/Ctrl/Alt などの修飾キー単体の押下ではカーソルを隠さない。
            if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System))
            {
                EnterKeyboardOperationMode();
            }

            // Control Deck: 開いていれば Esc で閉じる
            if (_isControlDeckOpen)
            {
                if (e.Key == Key.Escape)
                {
                    if (DataContext is MainViewModel vmDeck)
                        _ = vmDeck.CloseControlDeckAsync();
                    e.Handled = true;
                    return;
                }
                // Control Deck 内の操作は通す（RadioButton、CheckBox 等）
                return;
            }

            // Quick Preview: 開いていれば Space/Esc で閉じる、Left/Right でナビゲーション
            if (_isQuickPreviewOpen)
            {
                if (e.Key == Key.Space || e.Key == Key.Escape)
                {
                    HideQuickPreview();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Right || e.Key == Key.Left)
                {
                    NavigateQuickPreview(e.Key == Key.Right ? 1 : -1);
                    e.Handled = true;
                    return;
                }
                // プレビュー開放中は他のキーを無視（TextBox スクロールは PreviewKeyDown で処理）
                return;
            }

            if (DataContext is not MainViewModel vm) return;

            // Quick Preview 表示（カスタマイズ可能なキー、TextBox 外のとき）
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            if (App.KeyBindings.Matches("Window.QuickPreview", actualKey, Keyboard.Modifiers))
            {
                var focusedForPreview = Keyboard.FocusedElement as DependencyObject;
                if (focusedForPreview != null && !(focusedForPreview is System.Windows.Controls.TextBox or System.Windows.Controls.RichTextBox))
                {
                    var activePaneControl = vm.ActivePane == vm.LeftPane ? LeftPaneControl : RightPaneControl;
                    var tabContent = activePaneControl.GetActiveTabContent();
                    var listControl = tabContent?.GetActiveFileList();
                    if (listControl?.SelectedItem is FileItem selectedFile && !selectedFile.IsDirectory)
                    {
                        string ext = System.IO.Path.GetExtension(selectedFile.FullPath).ToLowerInvariant();
                        string fname = System.IO.Path.GetFileName(selectedFile.FullPath).ToLowerInvariant();
                        if (_previewableExtensions.Contains(ext) || _previewableExtensions.Contains("." + fname)
                            || _pdfExtensions.Contains(ext) || _htmlExtensions.Contains(ext)
                            || _spreadsheetExtensions.Contains(ext) || _imageExtensions.Contains(ext)
                            || _officePreviewExtensions.Contains(ext))
                        {
                            ShowQuickPreview(selectedFile);
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            bool inLeft = IsDescendantOf(focused, LeftPaneControl);
            bool inRight = IsDescendantOf(focused, RightPaneControl);

            // ペイン切替（カスタマイズ可能なキー）
            if (App.KeyBindings.Matches("Window.SwitchPanes", actualKey, Keyboard.Modifiers))
            {
                if (!(inLeft || inRight)) return;
                if (RightPaneControl.Visibility != Visibility.Visible) return;
                _ = App.Stats.RecordAsync("Nav.SwitchPane");

                // ActivePane に応じて反対側のペインへ切り替え（確実に往復する）
                if (vm.ActivePane == vm.LeftPane)
                {
                    vm.ActivePane = vm.RightPane;
                    // RightPaneControl.FocusActiveTab(); // ActivePane変更イベントで処理されるため不要
                    e.Handled = true;
                }
                else
                {
                    vm.ActivePane = vm.LeftPane;
                    // LeftPaneControl.FocusActiveTab(); // ActivePane変更イベントで処理されるため不要
                    e.Handled = true;
                }
                return;
            }

            // ←: Bペインにいる場合は Aペインへ
            if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (inRight)
                {
                    vm.ActivePane = vm.LeftPane;
                    // LeftPaneControl.FocusActiveTab(); // ActivePane変更イベントで処理されるため不要
                    e.Handled = true;
                }
                return;
            }

            // →: Aペインにいる場合は Bペインへ（Bペインが表示されている場合のみ）
            if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (inLeft && RightPaneControl.Visibility == Visibility.Visible)
                {
                    vm.ActivePane = vm.RightPane;
                    // RightPaneControl.FocusActiveTab(); // ActivePane変更イベントで処理されるため不要
                    e.Handled = true;
                }
                return;
            }

            // Aペイン・Bペインにフォーカスがあるが一覧にフォーカスがない場合、一覧へフォーカスを移し当該キーを転送する
            // ただし、修飾キー（Ctrl/Alt/Shift）が押されている場合はInputBindingsの処理を妨げないよう、e.Handledを設定しない
            if (inLeft || inRight)
            {
                var activePaneControl = vm.ActivePane == vm.LeftPane ? LeftPaneControl : RightPaneControl;
                var listView = activePaneControl.GetActiveFileListView();
                bool focusInList = listView != null && IsDescendantOf(focused, listView);
                bool focusInTextBox = focused is System.Windows.Controls.TextBox or System.Windows.Controls.RichTextBox;
                // 修飾キーが押されている場合はInputBindingsの処理を妨げないよう、この処理をスキップ
                bool hasModifiers = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) != ModifierKeys.None;
                // アイコンビュー時は修飾キーなしのキーをウィンドウ側で握りつぶす（アドレスバー等にフォーカスがあっても転送経路に依存せず無効化）
                // Quick Preview キーは除外
                var qpDef = App.KeyBindings.Get("Window.QuickPreview");
                if (!hasModifiers && !(qpDef != null && actualKey == qpDef.ActiveKey && Keyboard.Modifiers == qpDef.ActiveModifiers))
                {
                    var tabContent = activePaneControl.GetActiveTabContent();
                    if (tabContent?.DataContext is TabItemViewModel tabVm && tabVm.FileViewMode != FileViewMode.Details)
                    {
                        e.Handled = true;
                        return;
                    }
                }
                if (!focusInList && !focusInTextBox && listView != null && !hasModifiers)
                {
                    activePaneControl.FocusList();
                    var source = PresentationSource.FromVisual(listView);
                    var keyEventArgs = new KeyEventArgs(e.KeyboardDevice, source ?? PresentationSource.FromVisual(this), e.Timestamp, e.Key)
                    {
                        RoutedEvent = UIElement.PreviewKeyDownEvent
                    };
                    listView.RaiseEvent(keyEventArgs);
                    if (keyEventArgs.Handled) e.Handled = true;
                }
            }
        }

        /// <summary>
        /// KeyDownイベントで、リストにフォーカスがない場合にキーを転送し、転送先で処理された場合のみe.Handledを設定する。
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            bool inLeft = IsDescendantOf(focused, LeftPaneControl);
            bool inRight = IsDescendantOf(focused, RightPaneControl);

            if (!(inLeft || inRight)) return;

            var activePaneControl = vm.ActivePane == vm.LeftPane ? LeftPaneControl : RightPaneControl;
            var listView = activePaneControl.GetActiveFileListView();
            bool focusInList = listView != null && IsDescendantOf(focused, listView);
            bool focusInTextBox = focused is System.Windows.Controls.TextBox or System.Windows.Controls.RichTextBox;
            bool hasModifiers = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) != ModifierKeys.None;

            // アイコンビュー時は修飾キーなしのキーをウィンドウ側で握りつぶす（KeyDown フェーズでも確実に無効化）
            // Quick Preview キーは除外
            var actualKeyDown = e.Key == Key.System ? e.SystemKey : e.Key;
            var qpDefDown = App.KeyBindings.Get("Window.QuickPreview");
            if (!hasModifiers && !(qpDefDown != null && actualKeyDown == qpDefDown.ActiveKey && Keyboard.Modifiers == qpDefDown.ActiveModifiers))
            {
                var tabContent = activePaneControl.GetActiveTabContent();
                if (tabContent?.DataContext is TabItemViewModel tabVm && tabVm.FileViewMode != FileViewMode.Details)
                {
                    e.Handled = true;
                    return;
                }
            }

            if (!focusInList && !focusInTextBox && listView != null && !hasModifiers)
            {
                var source = PresentationSource.FromVisual(listView);
                var keyEventArgs = new KeyEventArgs(e.KeyboardDevice, source ?? PresentationSource.FromVisual(this), e.Timestamp, e.Key)
                {
                    RoutedEvent = UIElement.KeyDownEvent
                };
                listView.RaiseEvent(keyEventArgs);
                if (keyEventArgs.Handled)
                {
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// キーボード操作モードに入り、マウスカーソルを非表示にする。
        /// </summary>
        private void EnterKeyboardOperationMode()
        {
            if (_isKeyboardOperating)
            {
                return;
            }

            _isKeyboardOperating = true;
            _keyboardModeMouseStartPoint = Mouse.GetPosition(this);
            _hasKeyboardMouseStartPoint = true;
            Mouse.OverrideCursor = Cursors.None;
        }

        /// <summary>
        /// マウス操作モードに戻し、カーソル表示とフラグを復元する。
        /// </summary>
        internal void ExitKeyboardOperationMode()
        {
            if (!_isKeyboardOperating)
            {
                return;
            }

            _isKeyboardOperating = false;
            _hasKeyboardMouseStartPoint = false;

            // 他所で OverrideCursor が使われている可能性も考慮し、None のときだけ戻す
            if (Mouse.OverrideCursor == Cursors.None)
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// 物理的なマウス移動を監視し、一定以上動いたらマウス操作モードへ自動復帰する。
        /// </summary>
        private void MainWindow_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isKeyboardOperating)
            {
                return;
            }

            var currentPos = e.GetPosition(this);

            if (!_hasKeyboardMouseStartPoint)
            {
                _keyboardModeMouseStartPoint = currentPos;
                _hasKeyboardMouseStartPoint = true;
                return;
            }

            double dx = currentPos.X - _keyboardModeMouseStartPoint.X;
            double dy = currentPos.Y - _keyboardModeMouseStartPoint.Y;

            if (Math.Abs(dx) >= KeyboardToMouseSwitchThreshold || Math.Abs(dy) >= KeyboardToMouseSwitchThreshold)
            {
                ExitKeyboardOperationMode();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized &&
                e.ChangedButton == MouseButton.Left &&
                e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                _isTitleBarDragFromMaximized = true;
            }
        }

        private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isTitleBarDragFromMaximized = false;
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_lastWindowState == WindowState.Normal && this.WindowState == WindowState.Maximized)
            {
                var bounds = this.RestoreBounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    _lastNormalBoundsBeforeMaximize = bounds;
                }
            }

            if (_lastWindowState == WindowState.Maximized &&
                this.WindowState == WindowState.Normal &&
                _isTitleBarDragFromMaximized &&
                Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                AdjustWindowAfterDragRestore();
            }

            _lastWindowState = this.WindowState;
        }

        private void AdjustWindowAfterDragRestore()
        {
            var workArea = WindowHelper.GetWorkArea(this);

            double targetWidth;
            double targetHeight;

            Rect? candidate = _lastNormalBoundsBeforeMaximize;
            if (candidate.HasValue && IsReasonableNormalBounds(candidate.Value, workArea))
            {
                targetWidth = candidate.Value.Width;
                targetHeight = candidate.Value.Height;
            }
            else
            {
                const double widthRatio = 0.75;
                const double goldenLikeAspect = 16.0 / 10.0;

                targetWidth = workArea.Width * widthRatio;
                targetHeight = targetWidth / goldenLikeAspect;

                double maxHeight = workArea.Height * 0.9;
                if (targetHeight > maxHeight)
                {
                    targetHeight = maxHeight;
                    targetWidth = targetHeight * goldenLikeAspect;
                }
            }

            targetWidth = Math.Max(targetWidth, this.MinWidth);
            targetHeight = Math.Max(targetHeight, this.MinHeight);

            if (targetWidth > workArea.Width)
                targetWidth = workArea.Width;
            if (targetHeight > workArea.Height)
                targetHeight = workArea.Height;

            var screenPos = System.Windows.Forms.Cursor.Position;
            var source = PresentationSource.FromVisual(this);
            var matrix = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var cursorInDiu = matrix.Transform(new Point(screenPos.X, screenPos.Y));

            double left = cursorInDiu.X - (targetWidth / 2.0);

            if (left < workArea.Left)
                left = workArea.Left;
            if (left + targetWidth > workArea.Right)
                left = workArea.Right - targetWidth;

            double top = this.Top;
            if (top < workArea.Top)
                top = workArea.Top;
            if (top + targetHeight > workArea.Bottom)
                top = workArea.Bottom - targetHeight;

            this.Width = targetWidth;
            this.Height = targetHeight;
            this.Left = left;
            this.Top = top;
        }

        private static bool IsReasonableNormalBounds(Rect bounds, Rect workArea)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            if (bounds.Width < workArea.Width * 0.3 || bounds.Height < workArea.Height * 0.3)
                return false;
            if (bounds.Width > workArea.Width || bounds.Height > workArea.Height)
                return false;

            if (bounds.Right < workArea.Left - workArea.Width * 0.2) return false;
            if (bounds.Left > workArea.Right + workArea.Width * 0.2) return false;
            if (bounds.Bottom < workArea.Top - workArea.Height * 0.2) return false;
            if (bounds.Top > workArea.Bottom + workArea.Height * 0.2) return false;

            return true;
        }

        private static bool IsDescendantOf(DependencyObject? element, DependencyObject? ancestor)
            => Helpers.VisualTreeExtensions.IsDescendantOf(element, ancestor);

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem tvi)
            {
                tvi.BringIntoView();
                e.Handled = true;
            }
        }

        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem tvi)
            {
                // クリックされた要素から遡って、最初に見つかる TreeViewItem を取得
                var clickedItem = GetParent<TreeViewItem>((DependencyObject)e.OriginalSource);

                // sender と clickedItem が一致する場合のみ処理する
                // （Previewイベントは親から子へ伝播するため、親で止めてしまわないようにする）
                if (clickedItem == tvi)
                {
                    e.Handled = true;

                    // 座標計算の基準を失わないよう、先に項目を選択してからメニューを開く
                    tvi.IsSelected = true;
                    tvi.Focus();

                    if (tvi.ContextMenu != null)
                    {
                        // Placement=MousePoint は環境によって (0,0) に飛ぶため使用しない。
                        // Relative でターゲット基準のマウス位置オフセットを明示的に指定する。
                        var mousePos = e.GetPosition(tvi);
                        
                        tvi.ContextMenu.PlacementTarget = tvi;
                        tvi.ContextMenu.Placement = PlacementMode.Relative;
                        tvi.ContextMenu.HorizontalOffset = mousePos.X;
                        tvi.ContextMenu.VerticalOffset = mousePos.Y;
                        
                        tvi.ContextMenu.IsOpen = true;
                    }
                }
            }
        }

        private static T? GetParent<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T typed)
                {
                    return typed;
                }
                
                if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                else
                {
                    current = LogicalTreeHelper.GetParent(current);
                }
            }
            return null;
        }

        /// <summary>お気に入りツリーのダブルクリック。PreviewMouseLeftButtonDown で ClickCount==2 を検出し、1回のダブルクリックで1回だけ処理する。</summary>
        private void FavoritesTree_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2) return;
            if (sender is not TreeView) return;
            if (DataContext is not MainViewModel vm || vm.Favorites == null) return;

            var source = (DependencyObject)e.OriginalSource;
            var clickedTvi = GetParent<TreeViewItem>(source);
            if (clickedTvi == null || clickedTvi.DataContext is not FavoriteItem item)
                return;

            if (item.IsContainer)
            {
                item.IsExpanded = !item.IsExpanded;
                e.Handled = true;
                return;
            }
            vm.Favorites.NavigateToFavoriteCommand.Execute(item);
            e.Handled = true;
        }

        private void FavoriteItem_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                if (sender is TreeViewItem tvi && tvi.DataContext is FavoriteItem item)
                {
                    if (this.DataContext is MainViewModel vm && vm.Favorites != null)
                    {
                        vm.Favorites.OpenInNewTabCommand.Execute(item);
                        e.Handled = true;
                    }
                }
            }
        }

        private void FavoritesFilteredItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListViewItem lvi && lvi.DataContext is FavoriteItem item)
            {
                if (this.DataContext is MainViewModel vm && vm.Favorites != null)
                {
                    vm.Favorites.NavigateToFavoriteCommand.Execute(item);
                    e.Handled = true;
                }
            }
        }

        private void FavoritesFilteredItem_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                if (sender is ListViewItem lvi && lvi.DataContext is FavoriteItem item)
                {
                    if (this.DataContext is MainViewModel vm && vm.Favorites != null)
                    {
                        vm.Favorites.OpenInNewTabCommand.Execute(item);
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// ナビペインをクリックしたときにのみ、該当コントロールへキーボードフォーカスを移す（ホバーでは切り替えない）。
        /// </summary>
        private void Sidebar_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            switch (vm.SidebarMode)
            {
                case SidebarViewMode.Favorites:
                    if (vm.Favorites.IsSearching)
                    {
                        if (FavoritesFilteredList.IsVisible)
                        {
                            FavoritesFilteredList.Focus();
                        }
                    }
                    else
                    {
                        if (FavoritesTree.IsVisible)
                        {
                            FavoritesTree.Focus();
                        }
                    }
                    break;

                case SidebarViewMode.Tree:
                    if (DirectoryTree.IsVisible)
                    {
                        DirectoryTree.Focus();
                    }
                    break;

                case SidebarViewMode.History:
                    if (HistoryList.IsVisible)
                    {
                        HistoryList.Focus();
                    }
                    break;

                case SidebarViewMode.IndexSearch:
                    // インデックスビュー内のクリックではペインの検索バーにフォーカスを飛ばさない
                    // （検索履歴ポップアップが一瞬表示される問題の防止）
                    if (IndexSearchTargetList.IsVisible)
                        IndexSearchTargetList.Focus();
                    break;

                case SidebarViewMode.AppSettings:
                    if (vm.ActivePane == vm.LeftPane)
                        LeftPaneControl.FocusSearchBox();
                    else
                        RightPaneControl.FocusSearchBox();
                    break;
            }
        }

        private Point _historyStartPoint;
        private DragAdorner? _historyDragAdorner;
        /// <summary>Remove 時に AdornedElement がツリー外だと GetAdornerLayer が null になるため、Add 時の layer を保持する。</summary>
        private AdornerLayer? _historyDragAdornerLayer;

        private void HistoryList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _historyStartPoint = e.GetPosition(null);
        }

        private void HistoryList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _historyStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is ListView listView && listView.SelectedItem is HistoryItemViewModel item)
                    {
                        var data = new DataObject();
                        var paths = new System.Collections.Specialized.StringCollection { item.Path };
                        data.SetFileDropList(paths);
                        
                        // 履歴アイテムであることを示すマーカー（必要に応じて）
                        data.SetData(typeof(HistoryItemViewModel), item);

                        // ドラッグアドーナーの表示
                        ShowHistoryDragAdorner(listView);

                        listView.GiveFeedback += HistoryList_GiveFeedback;
                        try
                        {
                            DragDrop.DoDragDrop(listView, data, DragDropEffects.Link | DragDropEffects.Copy);
                        }
                        finally
                        {
                            listView.GiveFeedback -= HistoryList_GiveFeedback;
                            RemoveHistoryDragAdorner();
                        }
                    }
                }
            }
        }

        private void ShowHistoryDragAdorner(ListView listView)
        {
            if (!WindowSettings.ShowDragEffectsEnabled) return;
            var layer = AdornerLayer.GetAdornerLayer(listView);
            if (layer != null)
            {
                _historyDragAdornerLayer = layer;
                _historyDragAdorner = new DragAdorner(listView, "Aペイン、もしくはBペインにドロップしてください");
                layer.Add(_historyDragAdorner);
                _historyDragAdorner.UpdatePosition(System.Windows.Input.Mouse.GetPosition(listView));
            }
        }

        private void RemoveHistoryDragAdorner()
        {
            if (_historyDragAdorner != null && _historyDragAdornerLayer != null)
            {
                _historyDragAdornerLayer.Remove(_historyDragAdorner);
                _historyDragAdornerLayer = null;
                _historyDragAdorner = null;
            }
            else if (_historyDragAdorner != null)
            {
                _historyDragAdorner = null;
            }
        }

        private void HistoryList_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            _historyDragAdorner?.UpdatePositionFromCursor();
            e.Handled = false;
        }

        private void HistoryItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListViewItem lvi && lvi.DataContext is HistoryItemViewModel item)
            {
                if (this.DataContext is MainViewModel vm)
                {
                    vm.OpenHistoryCommand.Execute(item);
                    e.Handled = true;
                }
            }
        }

        private void FavoritesTreeView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F2)
            {
                if (sender is TreeView treeView && treeView.SelectedItem is FavoriteItem item)
                {
                    if (this.DataContext is MainViewModel vm && vm.Favorites != null)
                    {
                        vm.Favorites.RenameItemCommand.Execute(item);
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == System.Windows.Input.Key.Delete)
            {
                if (sender is TreeView treeView && treeView.SelectedItem is FavoriteItem item)
                {
                    if (this.DataContext is MainViewModel vm && vm.Favorites != null)
                    {
                        vm.Favorites.RemoveItemCommand.Execute(item);
                        e.Handled = true;
                    }
                }
            }
        }

        private void DirectoryTree_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (this.DataContext is not MainViewModel vm) return;
            if (e.Key == System.Windows.Input.Key.Enter && sender is TreeView treeView && treeView.SelectedItem is DirectoryItemViewModel item)
            {
                vm.OpenTreeItemInActivePaneCommand.Execute(item);
                e.Handled = true;
                return;
            }
            if (vm.IsTreeViewLocked && (e.Key == System.Windows.Input.Key.F2 || e.Key == System.Windows.Input.Key.Delete))
            {
                if (sender is TreeView tv && tv.SelectedItem is DirectoryItemViewModel)
                {
                    App.Notification.Notify("ロック中のため変更できませんでした", "ツリービューロック: F2/Delete をブロック");
                    vm.TriggerTreeViewLockWarning();
                    e.Handled = true;
                }
            }
        }

        /// <summary>ツリー項目の Shift+クリック / Shift+Ctrl+クリックでアイコンビュー表示。</summary>
        private void DirectoryTreeItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0) return;
            var source = (DependencyObject)e.OriginalSource;
            var clickedTvi = GetParent<TreeViewItem>(source);
            if (clickedTvi == null || clickedTvi.DataContext is not DirectoryItemViewModel item) return;
            if (DataContext is not MainViewModel vm) return;

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                vm.OpenTreeItemInOppositePaneWithIconViewCommand.Execute(item);
            }
            else
            {
                vm.OpenTreeItemInActivePaneWithIconViewCommand.Execute(item);
            }
        }

        /// <summary>ツリー項目のダブルクリック時、フォーカス中のペインへ表示。Ctrl+ダブルクリックで反対ペインへ表示。展開矢印上ならナビせずツリーの開閉のみ。</summary>
        /// <remarks>MouseDoubleClick は親へバブルするため sender がルート側になることがある。OriginalSource から実際にクリックされた TreeViewItem を取得する。</remarks>
        private void DirectoryTreeItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var source = (DependencyObject)e.OriginalSource;
            // 展開用の矢印（ToggleButton）上ならナビゲーションせず、ツリーの展開/折りたたみのみ行う
            if (GetParent<ToggleButton>(source) != null)
                return;

            var clickedTvi = GetParent<TreeViewItem>(source);
            if (clickedTvi == null || clickedTvi.DataContext is not DirectoryItemViewModel item) return;
            if (DataContext is not MainViewModel vm) return;

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                vm.OpenTreeItemInOppositePaneCommand.Execute(item);
            }
            else
            {
                vm.OpenTreeItemInActivePaneCommand.Execute(item);
            }
            e.Handled = true;
        }

        /// <summary>ナビペインのツリーで空白部分を右クリックしたときに、マウス位置で汎用メニューを表示する。</summary>
        private void DirectoryTree_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not TreeView treeView || treeView.ContextMenu == null)
                return;

            var pos = e.GetPosition(treeView);
            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(treeView, pos);
            if (hitResult?.VisualHit is DependencyObject hit && GetParent<TreeViewItem>(hit) != null)
                return; // 項目上なら TreeViewItem のハンドラに任せる

            e.Handled = true;
            
            // Placement=MousePoint は環境によって (0,0) に飛ぶため使用しない。
            // Relative でターゲット基準のマウス位置オフセットを明示的に指定する。
            treeView.ContextMenu.PlacementTarget = treeView;
            treeView.ContextMenu.Placement = PlacementMode.Relative;
            treeView.ContextMenu.HorizontalOffset = pos.X;
            treeView.ContextMenu.VerticalOffset = pos.Y;
            treeView.ContextMenu.IsOpen = true;
        }

        /// <summary>お気に入りツリーで空白部分を右クリックしたときに、マウス位置でメニューを表示する。</summary>
        private void FavoritesTree_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not TreeView treeView || treeView.ContextMenu == null)
                return;

            var pos = e.GetPosition(treeView);
            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(treeView, pos);
            if (hitResult?.VisualHit is DependencyObject hit && GetParent<TreeViewItem>(hit) != null)
                return; // 項目上なら TreeViewItem のハンドラに任せる

            e.Handled = true;
            
            // Placement=MousePoint は環境によって (0,0) に飛ぶため、Relative で明示的に指定
            treeView.ContextMenu.PlacementTarget = treeView;
            treeView.ContextMenu.Placement = PlacementMode.Relative;
            treeView.ContextMenu.HorizontalOffset = pos.X;
            treeView.ContextMenu.VerticalOffset = pos.Y;
            treeView.ContextMenu.IsOpen = true;
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            // DPIが変更された（ディスプレイ間を移動した）際に、列幅の再計算を強制するために
            // 何らかのプロパティを更新する手法もあるが、
            // 今回は WindowState プロパティへのバインディングが DPI 変更に伴うレイアウト更新で
            // 再評価されることを期待する。
            // 明示的に再計算を促す場合は、ここでダミーのイベントを飛ばすなどの処理を追加できる。
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 常駐モード: トレイに隠して閉じない（Ctrl 押下時は即終了）
            if (App.TrayService?.ShouldCancelClose == true
                && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                e.Cancel = true;
                App.TrayService.HideToTray();
                return;
            }

            // Quick Preview cleanup
            CleanupTempPreviewPdf();
            if (_isWebView2Initialized)
            {
                try { PreviewWebView?.Dispose(); }
                catch { /* ignore */ }
            }

            _driveRefreshDebounceTimer?.Stop();
            _driveRefreshDebounceTimer = null;

            _telemetryPopupTimer?.Stop();
            _telemetryPopupTimer = null;
            _itemTelemetryPopupTimer?.Stop();
            _itemTelemetryPopupTimer = null;

            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);

            var vm = this.DataContext as MainViewModel;

            // デバウンス待機中の部分保存をフラッシュしてから最終保存
            WindowSettings.FlushPendingSaves();

            // 設定の保存（お気に入り含む）
            var settings = new WindowSettings
            {
                Width = this.ActualWidth,
                Height = this.ActualHeight,
                Left = this.Left,
                Top = this.Top,
                State = this.WindowState,
                SidebarWidth = vm?.NormalSidebarWidth ?? 220,
                IsSidebarVisible = vm?.IsSidebarVisible ?? true,
                SidebarMode = vm?.SidebarMode ?? SidebarViewMode.Favorites,
                PaneCount = vm?.PaneCount ?? 1,
                IsAlwaysOnTop = vm?.IsAlwaysOnTop ?? false,
                IsFavoritesLocked = vm?.Favorites.IsLocked ?? false,
                IsTreeViewLocked = vm?.IsTreeViewLocked ?? false,
                IsNavWidthLocked = vm?.IsNavWidthLocked ?? false,
                ConfirmDeleteFavorites = vm?.Favorites.ConfirmDelete ?? true,
                FavoritesSearchMode = vm?.Favorites.SearchMode ?? FavoritesSearchMode.NameAndDescription,
                Favorites = vm?.Favorites.GetFavoritesForSave() ?? new(),
                IndexSearchTargetPaths = vm?.IndexSearchSettings.GetPathsForSave() ?? new(),
                IndexSearchLockedPaths = vm?.IndexSearchSettings.GetLockedPathsForSave() ?? new(),
                IndexSearchScopePaths = vm?.IndexSearchSettings.GetScopePathsForSearch()?.ToList(),
                SearchBehavior = vm?.AppSettings?.SearchBehavior ?? SearchBehavior.SamePaneNewTab,
                SearchResultPathBehavior = vm?.AppSettings?.SearchResultPathBehavior ?? SearchResultPathBehavior.SamePaneNewTab,
                ContextMenuMode = vm?.AppSettings?.ContextMenuMode ?? ContextMenuMode.Zenith,
                AutoSwitchToSinglePaneOnSearch = vm?.AppSettings?.AutoSwitchToSinglePaneOnSearch ?? false,
                SearchResultFileTypeFilterEnabled = vm?.AppSettings?.GetSearchResultFileTypeFiltersForSave(),
                SearchSizeFilter = vm?.SearchFilter != null ? (int)vm.SearchFilter.SizeFilter : null,
                SearchCustomMinSize = vm?.SearchFilter?.CustomMinSizeBytes,
                SearchCustomMaxSize = vm?.SearchFilter?.CustomMaxSizeBytes,
                SearchMinSizeText = vm?.SearchFilter?.MinSizeText,
                SearchMaxSizeText = vm?.SearchFilter?.MaxSizeText,
                SearchDateFilter = vm?.SearchFilter != null ? (int)vm.SearchFilter.DateFilter : null,
                SearchStartDateText = vm?.SearchFilter?.StartDateText,
                SearchEndDateText = vm?.SearchFilter?.EndDateText,
                SearchCustomStartDate = vm?.SearchFilter?.ParsedStartDate?.ToString("o"),
                SearchCustomEndDate = vm?.SearchFilter?.ParsedEndDate?.ToString("o"),
                ThemeName = vm?.AppSettings?.SelectedThemeName ?? "standard",
                CurrentThemeMode  = vm?.AppSettings != null ? AppSettingsViewModel.ToModeStr(vm.AppSettings.ActiveThemeMode)  : "Personalize",
                AutoSelectSubMode = vm?.AppSettings != null ? AppSettingsViewModel.ToSubStr(vm.AppSettings.ThemeRandomizeMode) : "All",
                SelectedCategory  = vm?.AppSettings?.SelectedRandomCategory,
                SavedThemeName    = vm?.AppSettings?.GetThemePersistenceForSave().SavedTheme ?? "standard",
                NavPaneThemeName  = vm?.AppSettings?.NavPaneThemeName ?? string.Empty,
                APaneThemeName    = vm?.AppSettings?.APaneThemeName   ?? string.Empty,
                BPaneThemeName    = vm?.AppSettings?.BPaneThemeName   ?? string.Empty,
                ShowStartupToast  = vm?.AppSettings?.ShowStartupToast ?? true,
                // Display
                ListRowHeight = vm?.AppSettings?.ListRowHeight ?? 32,
                // Effects カテゴリ別 (v0.25.0)
                ShowStartupEffects = vm?.AppSettings?.ShowStartupEffects ?? true,
                ShowGlowBar = vm?.AppSettings?.ShowGlowBar ?? true,
                ShowScanBar = vm?.AppSettings?.ShowScanBar ?? true,
                ShowTabEffects = vm?.AppSettings?.ShowTabEffects ?? true,
                ShowPaneTransitions = vm?.AppSettings?.ShowPaneTransitions ?? true,
                ShowThemeEffects = vm?.AppSettings?.ShowThemeEffects ?? true,
                ShowPreviewEffects = vm?.AppSettings?.ShowPreviewEffects ?? true,
                ShowListEffects = vm?.AppSettings?.ShowListEffects ?? true,
                ShowDragEffects = vm?.AppSettings?.ShowDragEffects ?? true,
                // General 追加分
                SingleClickOpenFolder = vm?.AppSettings?.SingleClickOpenFolder ?? false,
                ConfirmDelete = vm?.AppSettings?.ConfirmDelete ?? true,
                RestoreTabsOnStartup = vm?.AppSettings?.RestoreTabsOnStartup ?? true,
                NotificationDurationMs = vm?.AppSettings?.NotificationDurationMs ?? 3000,
                ShowPathInTitleBar = vm?.AppSettings?.ShowPathInTitleBar ?? true,
                ShowFileExtensions = vm?.AppSettings?.ShowFileExtensions ?? true,
                ShowHiddenFiles = vm?.AppSettings?.ShowHiddenFiles ?? false,
                DownloadsSortByDate = vm?.AppSettings?.DownloadsSortByDate ?? false,
                ResidentMode = vm?.AppSettings?.ResidentMode ?? false,
                // Search デフォルト
                DefaultGroupFoldersFirst = vm?.AppSettings?.DefaultGroupFoldersFirst ?? true,
                DefaultSortProperty = vm?.AppSettings?.DefaultSortProperty ?? "Name",
                DefaultSortDirection = vm?.AppSettings?.DefaultSortDirection ?? ListSortDirection.Ascending,
                SettingsVersion = 1,
                IndexSettings = vm?.AppSettings?.GetIndexSettingsForSave(),
                LeftPane = new PaneSettings
                {
                    IsGroupFoldersFirst = vm?.LeftPane.IsGroupFoldersFirst ?? true,
                    IsAdaptiveColumnsEnabled = vm?.LeftPane.IsAdaptiveColumnsEnabled ?? true,
                    SortProperty = vm?.LeftPane.SortProperty ?? "Name",
                    SortDirection = vm?.LeftPane.SortDirection ?? ListSortDirection.Ascending,
                    CurrentPath = vm?.LeftPane.CurrentPath ?? string.Empty,
                    TabPaths = vm?.LeftPane.Tabs.Select(t => t.CurrentPath).ToList() ?? new(),
                    TabLockStates = vm?.LeftPane.Tabs.Select(t => t.IsLocked).ToList() ?? new(),
                    SelectedTabIndex = vm != null && vm.LeftPane.SelectedTab != null ? vm.LeftPane.Tabs.IndexOf(vm.LeftPane.SelectedTab) : 0,
                    IsPathEditMode = vm?.LeftPane.IsPathEditMode ?? false,
                    HomePath = vm?.AppSettings?.LeftPaneHomePath ?? string.Empty,
                    FileViewMode = vm?.LeftPane.FileViewMode ?? FileViewMode.Details
                },
                RightPane = new PaneSettings
                {
                    IsGroupFoldersFirst = vm?.RightPane.IsGroupFoldersFirst ?? true,
                    IsAdaptiveColumnsEnabled = vm?.RightPane.IsAdaptiveColumnsEnabled ?? true,
                    SortProperty = vm?.RightPane.SortProperty ?? "Name",
                    SortDirection = vm?.RightPane.SortDirection ?? ListSortDirection.Ascending,
                    CurrentPath = vm?.RightPane.CurrentPath ?? string.Empty,
                    TabPaths = vm?.RightPane.Tabs.Select(t => t.CurrentPath).ToList() ?? new(),
                    TabLockStates = vm?.RightPane.Tabs.Select(t => t.IsLocked).ToList() ?? new(),
                    SelectedTabIndex = vm != null && vm.RightPane.SelectedTab != null ? vm.RightPane.Tabs.IndexOf(vm.RightPane.SelectedTab) : 0,
                    IsPathEditMode = vm?.RightPane.IsPathEditMode ?? false,
                    HomePath = vm?.AppSettings?.RightPaneHomePath ?? string.Empty,
                    FileViewMode = vm?.RightPane.FileViewMode ?? FileViewMode.Details
                },
                WorkingSets = vm?.ProjectSets.Items.ToList() ?? new(),
                SearchPresets = vm?.SearchPresets.Presets.ToList() ?? new(),
                IndexItemSettings = vm?.IndexSearchSettings.GetItemSettingsForSave(),
            };

            // EULA 同意状態・自動更新・ライセンス等のグローバル設定を既存値から引き継ぐ
            var existing = WindowSettings.Load();
            settings.EulaAcceptedVersion = existing.EulaAcceptedVersion;
            settings.AutoUpdate = existing.AutoUpdate;
            settings.LastUpdateCheck = existing.LastUpdateCheck;
            settings.SkippedVersion = existing.SkippedVersion;
            settings.CustomKeyBindings = existing.CustomKeyBindings;

            // インデックス更新モード・定例スケジュールを反映（保存前に適用）
            App.IndexService.ConfigureIndexUpdate(settings.IndexSettings,
                () => vm?.IndexSearchSettings.GetPathsForSave() ?? new System.Collections.Generic.List<string>(),
                settings.IndexItemSettings);

            try
            {
                settings.Save();
                Services.SettingsBackupService.CreateBackup();
                _ = App.FileLogger.LogAsync("[Settings] 終了時の設定を保存しました");
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync(
                    $"[Settings] 終了時の設定保存に失敗しました: {FileLoggerService.FormatException(ex)}");
            }
        }

        // === ステータスバー テレメトリPopup ===

        private DispatcherTimer? _telemetryPopupTimer;
        private int _lastTelemetrySampleCount;

        private void IndexSpinner_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!App.Notification.IsIndexing) return;
            _lastTelemetrySampleCount = App.IndexService.GetTelemetrySnapshot().ProcessedCount;
            UpdateTelemetryPopup();
            IndexTelemetryBorder.Opacity = 0;
            IndexTelemetryPopup.IsOpen = true;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            IndexTelemetryBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _telemetryPopupTimer?.Stop();
            _telemetryPopupTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _telemetryPopupTimer.Tick += TelemetryPopupTimer_Tick;
            _telemetryPopupTimer.Start();
        }

        private void IndexSpinner_MouseLeave(object sender, MouseEventArgs e)
        {
            IndexTelemetryBorder.BeginAnimation(UIElement.OpacityProperty, null);
            IndexTelemetryPopup.IsOpen = false;
            _telemetryPopupTimer?.Stop();
            _telemetryPopupTimer = null;
        }

        private void TelemetryPopupTimer_Tick(object? sender, EventArgs e)
        {
            if (!IndexTelemetryPopup.IsOpen || !App.Notification.IsIndexing)
            {
                IndexTelemetryPopup.IsOpen = false;
                _telemetryPopupTimer?.Stop();
                _telemetryPopupTimer = null;
                return;
            }
            UpdateTelemetryPopup();
        }

        private void UpdateTelemetryPopup()
        {
            var t = App.IndexService.GetTelemetrySnapshot();
            int throughput = Math.Max(0, t.ProcessedCount - _lastTelemetrySampleCount);
            _lastTelemetrySampleCount = t.ProcessedCount;

            TelemetryScanFolder.Text = string.IsNullOrEmpty(t.CurrentScanFolder) ? "---" : t.CurrentScanFolder;
            TelemetryThroughput.Text = $"{throughput:N0} items/sec";
            TelemetryDbStats.Text = $"{t.TotalDbRecords:N0} records";
            TelemetryThreads.Text = $"{t.ActiveThreads}/{t.MaxParallelDegree}";
            TelemetryElapsed.Text = t.Elapsed.TotalHours >= 1
                ? t.Elapsed.ToString(@"h\:mm\:ss")
                : t.Elapsed.ToString(@"m\:ss");
        }

        // === インデックスパネル テレメトリPopup ===

        private DispatcherTimer? _itemTelemetryPopupTimer;
        private IndexSearchTargetItemViewModel? _currentTelemetryItem;

        private void ItemSpinner_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not IndexSearchTargetItemViewModel item) return;
            if (!item.IsInProgress) return;

            item.LastTelemetrySampleCount = App.IndexService.GetTelemetrySnapshotForRoot(item.Path).ProcessedCount;
            _currentTelemetryItem = item;
            UpdateItemTelemetryPopup(item);
            item.IsTelemetryPopupOpen = true;

            _itemTelemetryPopupTimer?.Stop();
            _itemTelemetryPopupTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _itemTelemetryPopupTimer.Tick += ItemTelemetryPopupTimer_Tick;
            _itemTelemetryPopupTimer.Start();
        }

        private void ItemSpinner_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_currentTelemetryItem != null)
                _currentTelemetryItem.IsTelemetryPopupOpen = false;
            _currentTelemetryItem = null;
            _itemTelemetryPopupTimer?.Stop();
            _itemTelemetryPopupTimer = null;
        }

        private void ItemTelemetryPopupTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentTelemetryItem == null || !_currentTelemetryItem.IsInProgress)
            {
                if (_currentTelemetryItem != null)
                    _currentTelemetryItem.IsTelemetryPopupOpen = false;
                _currentTelemetryItem = null;
                _itemTelemetryPopupTimer?.Stop();
                _itemTelemetryPopupTimer = null;
                return;
            }
            UpdateItemTelemetryPopup(_currentTelemetryItem);
        }

        private void UpdateItemTelemetryPopup(IndexSearchTargetItemViewModel item)
        {
            var t = App.IndexService.GetTelemetrySnapshotForRoot(item.Path);
            int throughput = Math.Max(0, t.ProcessedCount - item.LastTelemetrySampleCount);
            item.LastTelemetrySampleCount = t.ProcessedCount;

            item.TelemetryScanText = string.IsNullOrEmpty(t.CurrentScanFolder) ? "---" : t.CurrentScanFolder;
            item.TelemetrySpeedText = $"{throughput:N0} items/sec";
            item.TelemetryDbText = $"{t.TotalDbRecords:N0} records";
            item.TelemetryThreadText = $"{t.ActiveThreads}/{t.MaxParallelDegree}";
            item.TelemetryElapsedText = t.Elapsed.TotalHours >= 1
                ? t.Elapsed.ToString(@"h\:mm\:ss")
                : t.Elapsed.ToString(@"m\:ss");
        }

        // ═══════════════════════════════════════════════════════════════
        // Quick Preview (Peek)
        // ═══════════════════════════════════════════════════════════════

        private enum QuickPreviewType { Text, Pdf, Html, Spreadsheet, Image, Office, Unsupported }

        private static QuickPreviewType GetPreviewType(string path)
        {
            string ext = Path.GetExtension(path);
            if (_pdfExtensions.Contains(ext)) return QuickPreviewType.Pdf;
            if (_htmlExtensions.Contains(ext)) return QuickPreviewType.Html;
            if (_spreadsheetExtensions.Contains(ext)) return QuickPreviewType.Spreadsheet;
            if (_imageExtensions.Contains(ext)) return QuickPreviewType.Image;
            if (_officePreviewExtensions.Contains(ext)) return QuickPreviewType.Office;
            if (_previewableExtensions.Contains(ext)) return QuickPreviewType.Text;
            return QuickPreviewType.Unsupported;
        }

        private async void ShowQuickPreview(FileItem file)
        {
            try
            {
            _ = App.Stats.RecordAsync("Preview.QuickLook");
            _isQuickPreviewOpen = true;

            // サイズ設定 (80%)
            QuickPreviewPanel.MaxWidth = RootContent.ActualWidth * 0.8;
            QuickPreviewPanel.MaxHeight = RootContent.ActualHeight * 0.8;

            // 表示 + フェードイン
            QuickPreviewOverlay.Visibility = Visibility.Visible;
            if (!WindowSettings.ShowPreviewEffectsEnabled)
            {
                QuickPreviewOverlay.Opacity = 1;
            }
            else
            {
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                QuickPreviewOverlay.BeginAnimation(OpacityProperty, fade);
            }

            await UpdateQuickPreviewContent(file, 0);
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] ShowQuickPreview: {ex.Message}"); }
        }

        /// <summary>
        /// Quick Preview のコンテンツを更新する。direction: -1=左, 0=初回, 1=右（アニメーション方向）。
        /// </summary>
        private async Task UpdateQuickPreviewContent(FileItem file, int direction)
        {
            _quickPreviewCts?.Cancel();
            _quickPreviewCts = new CancellationTokenSource();
            var token = _quickPreviewCts.Token;

            // ヘッダー即時更新
            PreviewFileIcon.Source = file.Icon;
            PreviewFileName.Text = file.Name;
            PreviewFileInfo.Text = $"{file.DisplaySize}  |  {file.LastModified:yyyy/MM/dd HH:mm}";

            // ポジションカウンター更新
            UpdatePreviewPositionCounter();

            // コンテンツエリアをリセット
            PreviewTextBox.Text = string.Empty;
            PreviewTextBox.Visibility = Visibility.Collapsed;
            PreviewPdfScrollViewer.Visibility = Visibility.Collapsed;
            PreviewPdfImagePanel.Children.Clear();
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewLoadingPanel.Visibility = Visibility.Collapsed;
            PreviewSpreadsheetPanel.Visibility = Visibility.Collapsed;
            PreviewDataGrid.ItemsSource = null;
            PreviewUnsupportedText.Visibility = Visibility.Collapsed;

            // WebView2 リソース解放
            if (_isWebView2Initialized && PreviewWebView.CoreWebView2 != null)
            {
                try { PreviewWebView.CoreWebView2.Navigate("about:blank"); }
                catch { /* ignore */ }
            }
            PreviewWebView.Visibility = Visibility.Collapsed;

            // 一時 PDF の削除
            CleanupTempPreviewPdf();

            // スライド + フェード アニメーション
            if (direction == 0 || !WindowSettings.ShowPreviewEffectsEnabled)
            {
                PreviewContentArea.Opacity = 1;
                if (PreviewContentArea.RenderTransform is TranslateTransform tt)
                {
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    tt.X = 0;
                }
            }
            else
            {
                var transform = PreviewContentArea.RenderTransform as TranslateTransform;
                if (transform != null)
                {
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    var slideAnim = new DoubleAnimation(direction * 12, 0, TimeSpan.FromMilliseconds(60))
                    { EasingFunction = ease };
                    transform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
                }
                var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(50))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                PreviewContentArea.BeginAnimation(OpacityProperty, fadeAnim);
            }

            var previewType = GetPreviewType(file.FullPath);

            switch (previewType)
            {
                case QuickPreviewType.Text:
                    await ShowTextPreview(file, token);
                    break;
                case QuickPreviewType.Pdf:
                    await ShowPdfPreview(file.FullPath, token);
                    break;
                case QuickPreviewType.Html:
                    await ShowHtmlPreview(file.FullPath, token);
                    break;
                case QuickPreviewType.Spreadsheet:
                    await ShowSpreadsheetPreview(file, token);
                    break;
                case QuickPreviewType.Image:
                    await ShowImagePreview(file, token);
                    break;
                case QuickPreviewType.Office:
                    await ShowOfficePreview(file, token);
                    break;
                case QuickPreviewType.Unsupported:
                    ShowUnsupportedPreview();
                    break;
            }
        }

        /// <summary>
        /// Quick Preview ナビゲーション: 前後のファイルに切り替える。
        /// </summary>
        private async void NavigateQuickPreview(int direction)
        {
            try
            {
            if (DataContext is not MainViewModel vm) return;

            var activePaneControl = vm.ActivePane == vm.LeftPane ? LeftPaneControl : RightPaneControl;
            var tabContent = activePaneControl.GetActiveTabContent();
            var listControl = tabContent?.GetActiveFileList();
            if (listControl == null || listControl.Items.Count == 0) return;

            int currentIndex = listControl.SelectedIndex;
            int nextIndex = currentIndex;

            // direction 方向にスキャンし、ディレクトリをスキップ
            while (true)
            {
                nextIndex += direction;
                if (nextIndex < 0 || nextIndex >= listControl.Items.Count)
                    return; // 境界到達時は何もしない

                if (listControl.Items[nextIndex] is FileItem fi && !fi.IsDirectory)
                {
                    // プレビュー可能かチェック
                    string ext = System.IO.Path.GetExtension(fi.FullPath).ToLowerInvariant();
                    string fname = System.IO.Path.GetFileName(fi.FullPath).ToLowerInvariant();
                    if (_previewableExtensions.Contains(ext) || _previewableExtensions.Contains("." + fname)
                        || _pdfExtensions.Contains(ext) || _htmlExtensions.Contains(ext)
                        || _spreadsheetExtensions.Contains(ext) || _imageExtensions.Contains(ext)
                        || _officePreviewExtensions.Contains(ext))
                    {
                        break;
                    }
                }
            }

            // 選択を更新し、スクロールで追従
            listControl.SelectedIndex = nextIndex;
            listControl.ScrollIntoView(listControl.Items[nextIndex]);

            var nextFile = (FileItem)listControl.Items[nextIndex];
            await UpdateQuickPreviewContent(nextFile, direction);
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] NavigateQuickPreview: {ex.Message}"); }
        }

        /// <summary>
        /// ポジションカウンターを更新する。
        /// </summary>
        private void UpdatePreviewPositionCounter()
        {
            if (DataContext is not MainViewModel vm)
            {
                PreviewPositionCounter.Text = string.Empty;
                return;
            }

            var activePaneControl = vm.ActivePane == vm.LeftPane ? LeftPaneControl : RightPaneControl;
            var tabContent = activePaneControl.GetActiveTabContent();
            var listControl = tabContent?.GetActiveFileList();
            if (listControl == null)
            {
                PreviewPositionCounter.Text = string.Empty;
                return;
            }

            int index = listControl.SelectedIndex;
            PreviewPositionCounter.Text = $"{index + 1} / {listControl.Items.Count}";
        }

        private async Task ShowTextPreview(FileItem file, CancellationToken token)
        {
            PreviewTextBox.Visibility = Visibility.Visible;
            PreviewTextBox.Text = "読み込み中...";
            PreviewTextBox.Focus();

            try
            {
                string text = await Task.Run(() => ReadFileForPreview(file.FullPath, token), token);
                if (token.IsCancellationRequested) return;
                PreviewTextBox.Text = text;
                PreviewTextBox.ScrollToHome();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    PreviewTextBox.Text = $"ファイルを読み込めませんでした: {ex.Message}";
            }
        }

        private async Task<bool> EnsureWebView2Async()
        {
            if (_isWebView2Initialized) return true;
            if (_isWebView2InitFailed) return false;

            try
            {
                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: Path.Combine(Path.GetTempPath(), "ZenithFiler_WebView2"));
                await PreviewWebView.EnsureCoreWebView2Async(env);

                PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                PreviewWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                PreviewWebView.CoreWebView2.Settings.IsZoomControlEnabled = true;

                _isWebView2Initialized = true;
                return true;
            }
            catch
            {
                _isWebView2InitFailed = true;
                return false;
            }
        }

        private async Task ShowPdfPreview(string pdfPath, CancellationToken token)
        {
            PreviewLoadingPanel.Visibility = Visibility.Visible;
            PreviewLoadingText.Text = "PDF を読み込み中...";
            StartPreviewLoadingSpinner();

            try
            {
                var (images, totalPages) = await Task.Run(() => RenderPdfPages(pdfPath, token), token);
                if (token.IsCancellationRequested) return;

                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                DisplayPdfImages(images, totalPages);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                if (!token.IsCancellationRequested)
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewTextBox.Text = $"PDF を表示できませんでした: {ex.Message}";
                    PreviewTextBox.Focus();
                }
            }
        }

        private const int PdfMaxPreviewPages = 20;
        private const int PdfPreviewDpi = 144;

        private static (List<System.Windows.Media.Imaging.BitmapSource> Images, int TotalPages) RenderPdfPages(
            string pdfPath, CancellationToken token)
        {
            using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            int totalPages = PDFtoImage.Conversion.GetPageCount(stream, leaveOpen: true);
            int renderCount = Math.Min(totalPages, PdfMaxPreviewPages);
            var renderOptions = new PDFtoImage.RenderOptions(Dpi: PdfPreviewDpi);
            var images = new List<System.Windows.Media.Imaging.BitmapSource>(renderCount);

            for (int i = 0; i < renderCount; i++)
            {
                token.ThrowIfCancellationRequested();
                using var bitmap = PDFtoImage.Conversion.ToImage(stream, page: i, leaveOpen: true, options: renderOptions);
                var bitmapSource = ConvertSkBitmapToBitmapSource(bitmap);
                images.Add(bitmapSource);
            }

            return (images, totalPages);
        }

        private static System.Windows.Media.Imaging.BitmapSource ConvertSkBitmapToBitmapSource(SkiaSharp.SKBitmap bitmap)
        {
            var info = bitmap.Info;
            var pixels = new byte[info.BytesSize];
            System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

            var bitmapSource = System.Windows.Media.Imaging.BitmapSource.Create(
                info.Width, info.Height, 96, 96,
                System.Windows.Media.PixelFormats.Bgra32, null,
                pixels, info.RowBytes);
            bitmapSource.Freeze();
            return bitmapSource;
        }

        private void DisplayPdfImages(List<System.Windows.Media.Imaging.BitmapSource> images, int totalPages)
        {
            PreviewPdfScrollViewer.Visibility = Visibility.Visible;
            PreviewPdfImagePanel.Children.Clear();

            foreach (var bitmapSource in images)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = bitmapSource,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                PreviewPdfImagePanel.Children.Add(img);
            }

            if (totalPages > PdfMaxPreviewPages)
            {
                PreviewPdfImagePanel.Children.Add(new TextBlock
                {
                    Text = $"(先頭 {PdfMaxPreviewPages} / {totalPages} ページのみ表示)",
                    Foreground = Application.Current.Resources["SubTextBrush"] as Brush
                                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x77, 0x77, 0x77)),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8),
                });
            }

            PreviewPdfScrollViewer.ScrollToTop();
        }

        private async Task ShowHtmlPreview(string htmlPath, CancellationToken token)
        {
            PreviewLoadingPanel.Visibility = Visibility.Visible;
            PreviewLoadingText.Text = "HTML を読み込み中...";
            StartPreviewLoadingSpinner();

            try
            {
                if (!await EnsureWebView2Async())
                {
                    StopPreviewLoadingSpinner();
                    PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewTextBox.Text = "HTML プレビューには Microsoft Edge WebView2 Runtime が必要です";
                    PreviewTextBox.Focus();
                    return;
                }
                if (token.IsCancellationRequested) return;

                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                PreviewWebView.Visibility = Visibility.Visible;
                PreviewWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                PreviewWebView.Focus();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                if (!token.IsCancellationRequested)
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewTextBox.Text = $"HTML を表示できませんでした: {ex.Message}";
                    PreviewTextBox.Focus();
                }
            }
        }

        private async Task ShowSpreadsheetPreview(FileItem file, CancellationToken token)
        {
            PreviewLoadingPanel.Visibility = Visibility.Visible;
            PreviewLoadingText.Text = "スプレッドシートを読み込み中...";
            StartPreviewLoadingSpinner();

            try
            {
                var (table, sheetName, totalRows, totalSheets) = await Task.Run(
                    () => Services.ExcelPreviewService.ReadForPreview(file.FullPath, token), token);

                if (token.IsCancellationRequested) return;

                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                PreviewSpreadsheetPanel.Visibility = Visibility.Visible;
                PreviewDataGrid.ItemsSource = table.DefaultView;

                // フッター情報
                int displayRows = table.Rows.Count;
                string rowInfo = displayRows < totalRows
                    ? $"先頭 {displayRows:N0} / {totalRows:N0} 行を表示"
                    : $"{totalRows:N0} 行";
                string sheetInfo = totalSheets > 1 ? $" | {totalSheets} シート" : "";
                PreviewSpreadsheetFooter.Text = $"{sheetName} | {rowInfo}{sheetInfo}";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                if (!token.IsCancellationRequested)
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewTextBox.Text = $"スプレッドシートを表示できませんでした: {ex.Message}";
                    PreviewTextBox.Focus();
                }
            }
        }

        private async Task ShowImagePreview(FileItem file, CancellationToken token)
        {
            PreviewLoadingPanel.Visibility = Visibility.Visible;
            PreviewLoadingText.Text = "画像を読み込み中...";
            StartPreviewLoadingSpinner();

            try
            {
                var bitmap = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    using var fs = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.StreamSource = fs;
                    bi.DecodePixelWidth = 1920; // プレビュー用にサイズ制限
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }, token);

                if (token.IsCancellationRequested) return;

                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                if (!token.IsCancellationRequested)
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewTextBox.Text = $"画像を表示できませんでした: {ex.Message}";
                    PreviewTextBox.Focus();
                }
            }
        }

        private void ShowUnsupportedPreview()
        {
            PreviewUnsupportedText.Visibility = Visibility.Visible;
        }

        private async Task ShowOfficePreview(FileItem file, CancellationToken token)
        {
            string ext = Path.GetExtension(file.FullPath).ToLowerInvariant();
            string appName = ext switch
            {
                ".docx" or ".doc" or ".docm" => "Word",
                ".pptx" or ".ppt" or ".pptm" => "PowerPoint",
                _ => "Office"
            };

            PreviewLoadingPanel.Visibility = Visibility.Visible;
            PreviewLoadingText.Text = $"{appName} ファイルを変換中...";
            StartPreviewLoadingSpinner();

            try
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"zenith_preview_{Guid.NewGuid():N}.pdf");

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    Services.PdfConversionService.ConvertOfficeToPdf(file.FullPath, tempPdf);
                }, token);

                if (token.IsCancellationRequested) { TryDeleteFile(tempPdf); return; }
                _tempPreviewPdfPath = tempPdf;

                // PDF→画像レンダリング
                PreviewLoadingText.Text = "プレビューを生成中...";
                var (images, totalPages) = await Task.Run(() => RenderPdfPages(tempPdf, token), token);

                // 画像レンダリング完了後は一時 PDF 不要
                CleanupTempPreviewPdf();

                if (token.IsCancellationRequested) return;

                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                DisplayPdfImages(images, totalPages);
            }
            catch (OperationCanceledException) { CleanupTempPreviewPdf(); }
            catch (InvalidOperationException)
            {
                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                if (!token.IsCancellationRequested)
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewTextBox.Text = $"Microsoft {appName} がインストールされていないためプレビューできません";
                    PreviewTextBox.Focus();
                }
                CleanupTempPreviewPdf();
            }
            catch (Exception ex)
            {
                StopPreviewLoadingSpinner();
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                if (!token.IsCancellationRequested)
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewTextBox.Text = $"ファイルをプレビューできませんでした: {ex.Message}";
                    PreviewTextBox.Focus();
                }
                CleanupTempPreviewPdf();
            }
        }

        private void StartPreviewLoadingSpinner()
        {
            _previewLoadingSpinnerTimer?.Stop();
            _previewLoadingSpinnerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _previewLoadingSpinnerTimer.Tick += (_, _) =>
            {
                PreviewLoadingRotate.Angle = (PreviewLoadingRotate.Angle + 4) % 360;
            };
            _previewLoadingSpinnerTimer.Start();
        }

        private void StopPreviewLoadingSpinner()
        {
            _previewLoadingSpinnerTimer?.Stop();
            _previewLoadingSpinnerTimer = null;
        }

        private void CleanupTempPreviewPdf()
        {
            if (_tempPreviewPdfPath != null)
            {
                TryDeleteFile(_tempPreviewPdfPath);
                _tempPreviewPdfPath = null;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort */ }
        }

        private void HideQuickPreview()
        {
            if (!_isQuickPreviewOpen) return;
            _isQuickPreviewOpen = false;
            _quickPreviewCts?.Cancel();
            StopPreviewLoadingSpinner();

            // WebView2 のファイルロック解放
            if (_isWebView2Initialized && PreviewWebView.CoreWebView2 != null)
            {
                try { PreviewWebView.CoreWebView2.Navigate("about:blank"); }
                catch { /* ignore */ }
            }

            if (!WindowSettings.ShowPreviewEffectsEnabled)
            {
                QuickPreviewOverlay.Opacity = 0;
                QuickPreviewOverlay.Visibility = Visibility.Collapsed;
                ResetPreviewContent();
            }
            else
            {
                var fade = new DoubleAnimation(QuickPreviewOverlay.Opacity, 0, TimeSpan.FromMilliseconds(80))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                fade.Completed += (_, _) =>
                {
                    QuickPreviewOverlay.Visibility = Visibility.Collapsed;
                    QuickPreviewOverlay.BeginAnimation(OpacityProperty, null);
                    ResetPreviewContent();
                };
                QuickPreviewOverlay.BeginAnimation(OpacityProperty, fade);
            }

            // ファイルリストにフォーカスを戻す
            if (DataContext is MainViewModel mvm)
            {
                var activePaneControl = mvm.ActivePane == mvm.LeftPane ? LeftPaneControl : RightPaneControl;
                activePaneControl.FocusList();
            }
        }

        private void ResetPreviewContent()
        {
            PreviewTextBox.Text = string.Empty;
            PreviewTextBox.Visibility = Visibility.Collapsed;
            PreviewWebView.Visibility = Visibility.Collapsed;
            PreviewLoadingPanel.Visibility = Visibility.Collapsed;
            PreviewPdfScrollViewer.Visibility = Visibility.Collapsed;
            PreviewPdfImagePanel.Children.Clear();
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewSpreadsheetPanel.Visibility = Visibility.Collapsed;
            PreviewDataGrid.ItemsSource = null;
            PreviewUnsupportedText.Visibility = Visibility.Collapsed;

            // 一時 PDF の削除
            CleanupTempPreviewPdf();
        }

        private static string ReadFileForPreview(string path, CancellationToken token)
        {
            const int maxBytes = 100 * 1024; // 100KB
            byte[] buffer;
            bool truncated;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                int toRead = (int)Math.Min(fs.Length, maxBytes);
                buffer = new byte[toRead];
                int offset = 0;
                while (offset < toRead)
                {
                    token.ThrowIfCancellationRequested();
                    int read = fs.Read(buffer, offset, toRead - offset);
                    if (read == 0) break;
                    offset += read;
                }
                if (offset < toRead)
                    buffer = buffer[..offset];
                truncated = fs.Length > maxBytes;
            }

            if (buffer.Length == 0)
                return "(空のファイル)";

            // エンコーディング検出
            var encoding = DetectEncoding(buffer);
            string text = encoding.GetString(buffer);

            // BOM 除去
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text[1..];

            if (truncated)
                text += "\n\n--- (先頭 100KB のみ表示) ---";

            return text;
        }

        private static System.Text.Encoding DetectEncoding(byte[] data)
        {
            // 1. BOM 検出
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return System.Text.Encoding.UTF8;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return System.Text.Encoding.Unicode; // UTF-16 LE
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
                return System.Text.Encoding.BigEndianUnicode; // UTF-16 BE

            // 2. UTF-8 バリデーション
            try
            {
                var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                utf8.GetString(data);
                return System.Text.Encoding.UTF8;
            }
            catch (System.Text.DecoderFallbackException) { }

            // 3. Shift_JIS フォールバック（日本語 Windows）
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return System.Text.Encoding.GetEncoding(932);
        }

        private void ControlDeckDimming_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                _ = vm.CloseControlDeckAsync();
            e.Handled = true;
        }

        private void QuickPreviewDimming_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HideQuickPreview();
            e.Handled = true;
        }

        private void QuickPreviewCloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideQuickPreview();
        }

        private void QuickPreviewTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Space/Esc/Left/Right は MainWindow_PreviewKeyDown で処理済み
            // 縦スクロールキーは TextBox にそのまま通す
            if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
                return;
            // その他のキーは握りつぶし（TextBox への入力防止）
            e.Handled = true;
        }
    }
}
