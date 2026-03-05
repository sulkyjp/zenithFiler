using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZenithFiler.Models;

namespace ZenithFiler.Controls
{
    /// <summary>
    /// キーキャプチャコントロール。クリックで録音モードに入り、キー入力でバインドを変更する。
    /// </summary>
    public class HotkeyRecorderControl : Control
    {
        private bool _isRecording;
        private TextBlock? _displayText;
        private Border? _rootBorder;

        public static readonly DependencyProperty BindingDefinitionProperty =
            DependencyProperty.Register(nameof(BindingDefinition), typeof(KeyBindingDefinition),
                typeof(HotkeyRecorderControl), new PropertyMetadata(null, OnBindingDefinitionChanged));

        public KeyBindingDefinition? BindingDefinition
        {
            get => (KeyBindingDefinition?)GetValue(BindingDefinitionProperty);
            set => SetValue(BindingDefinitionProperty, value);
        }

        /// <summary>キーが変更されたときに発火するイベント。</summary>
        public event EventHandler<KeyChangedEventArgs>? KeyChanged;

        static HotkeyRecorderControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyRecorderControl),
                new FrameworkPropertyMetadata(typeof(HotkeyRecorderControl)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _rootBorder = GetTemplateChild("PART_Border") as Border;
            _displayText = GetTemplateChild("PART_DisplayText") as TextBlock;
            UpdateDisplay();
        }

        private static void OnBindingDefinitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyRecorderControl ctrl)
                ctrl.UpdateDisplay();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!_isRecording)
            {
                StartRecording();
            }
            e.Handled = true;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!_isRecording)
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Escape でキャンセル
            if (key == Key.Escape)
            {
                StopRecording();
                return;
            }

            // 修飾キー単体の押下は無視
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                return;
            }

            var modifiers = Keyboard.Modifiers;

            // キーをセット
            var def = BindingDefinition;
            if (def == null)
            {
                StopRecording();
                return;
            }

            // 競合チェック
            var conflict = App.KeyBindings.FindConflict(def.ActionId, key, modifiers);
            if (conflict != null)
            {
                def.ConflictWarning = $"「{conflict.DisplayName}」と競合";
            }
            else
            {
                def.ConflictWarning = string.Empty;
            }

            App.KeyBindings.SetCustomBinding(def.ActionId, key, modifiers);
            KeyChanged?.Invoke(this, new KeyChangedEventArgs(def.ActionId, key, modifiers));
            StopRecording();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            if (_isRecording)
                StopRecording();
        }

        private void StartRecording()
        {
            _isRecording = true;
            Focus();
            Focusable = true;
            Keyboard.Focus(this);

            if (_displayText != null)
                _displayText.Text = "キーを入力...";

            if (_rootBorder != null)
                _rootBorder.BorderBrush = (Brush?)FindResource("AccentBrush") ?? Brushes.DodgerBlue;
        }

        private void StopRecording()
        {
            _isRecording = false;
            UpdateDisplay();

            if (_rootBorder != null)
                _rootBorder.BorderBrush = (Brush?)FindResource("BorderBrush") ?? Brushes.Gray;
        }

        private void UpdateDisplay()
        {
            if (_displayText == null) return;
            var def = BindingDefinition;
            _displayText.Text = def?.DisplayText ?? string.Empty;
        }
    }

    public class KeyChangedEventArgs : EventArgs
    {
        public string ActionId { get; }
        public Key Key { get; }
        public ModifierKeys Modifiers { get; }

        public KeyChangedEventArgs(string actionId, Key key, ModifierKeys modifiers)
        {
            ActionId = actionId;
            Key = key;
            Modifiers = modifiers;
        }
    }
}
