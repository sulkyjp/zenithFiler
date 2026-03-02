using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ZenithFiler.Services.Interfaces;

namespace ZenithFiler.Services
{
    public partial class UndoService : ObservableObject
    {
        private static UndoService? _instance;
        public static UndoService Instance => _instance ??= new UndoService();

        private readonly Stack<IUndoCommand> _undoStack = new();

        private UndoService() { }

        public void Register(IUndoCommand command)
        {
            _undoStack.Push(command);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoActionName));
        }

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var command = _undoStack.Pop();
                try
                {
                    command.Undo();
                    App.Notification.Notify("元に戻しました", $"Undo: {command.Description}");
                }
                catch (Exception ex)
                {
                    App.Notification.Notify("元に戻せませんでした", $"Undo失敗: {ex.Message}");
                    // 失敗した場合はスタックに戻さない（戻しても次も失敗する可能性が高いため）
                }
                finally
                {
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(UndoActionName));
                }
            }
        }

        public bool CanUndo => _undoStack.Count > 0;
        
        public string UndoActionName => _undoStack.Count > 0 ? _undoStack.Peek().Description : string.Empty;

        public void Clear()
        {
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoActionName));
        }
    }
}
