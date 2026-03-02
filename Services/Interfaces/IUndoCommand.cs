namespace ZenithFiler.Services.Interfaces
{
    public interface IUndoCommand
    {
        string Description { get; }
        void Undo();
    }
}
