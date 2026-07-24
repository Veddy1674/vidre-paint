using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

class UndoDuplicate(Canvas canvas, SKRectI cropBounds) : UndoRedo
{
    private readonly UndoBitmap pixelUndo = new(canvas, cropBounds); // (to simplify)
    private readonly SKRegion oldSelection = new(canvas.CommittedSelection);
    private readonly SKRegion newSelection = new();

    // must be called AFTER the canvas bitmap is modified (like UndoBitmap)
    public void PostUpdate()
    {
        pixelUndo.PostUpdate(canvas);

        // set new selection from current
        newSelection.SetRegion(canvas.CommittedSelection);
    }

    public override void Undo(Canvas canvas)
    {
        pixelUndo.Undo(canvas);

        canvas.ClearAllSelection();
        canvas.UpdateSelection(oldSelection.Bounds); // only rectangles supported...
        canvas.CommitSelection();
    }

    public override void Redo(Canvas canvas)
    {
        pixelUndo.Redo(canvas);

        canvas.ClearAllSelection();
        canvas.UpdateSelection(newSelection.Bounds);
        canvas.CommitSelection();
    }

    public override void Dispose()
    {
        pixelUndo.Dispose();
        oldSelection.Dispose();
        newSelection.Dispose();
    }
}