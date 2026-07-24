using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

// created inside each Canvas, not AppContext!
class UndoManager(Canvas canvas) : IDisposable
{
    private readonly Canvas canvas = canvas; // ref to parent, can't be null

    private readonly LinkedList<UndoRedo> undoStack = new();
    private readonly LinkedList<UndoRedo> redoStack = new();

    public void PushAction(UndoRedo action)
    {
        // Debug.WriteLine(action.GetType().Name);

        undoStack.AddLast(action);
        redoStack.Clear(); // new command clears redo stack!

        if (undoStack.Count > Config.UndoStackSize)
        {
            var oldest = undoStack.First!; // First cannot be null if Count > 0
            oldest.Value.Dispose(); // free memory
            undoStack.RemoveFirst();
        }
    }

    public void Undo()
    {
        if (undoStack.Count == 0) return;

        var action = undoStack.Last!.Value; // Last cannot be null if Count == 0
        undoStack.RemoveLast();

        action.Undo(canvas);
        redoStack.AddLast(action);
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;
        
        var action = redoStack.Last!.Value; // Last cannot be null if Count == 0
        redoStack.RemoveLast();

        action.Redo(canvas);
        undoStack.AddLast(action);
    }

    public void Dispose()
    {
        foreach (var action in undoStack)
            action.Dispose();
        
        foreach (var action in redoStack)
            action.Dispose();
        
        undoStack.Clear();
        redoStack.Clear();
    }
}