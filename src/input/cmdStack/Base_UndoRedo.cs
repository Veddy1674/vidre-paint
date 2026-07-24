using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

abstract class UndoRedo : IDisposable // or "ReversibleAction"
{
    public abstract void Undo(Canvas canvas);
    public abstract void Redo(Canvas canvas);
    
    public virtual void Dispose() {}
}