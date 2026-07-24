using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

// undo "esc" to merge floating layer (bitmap, selection, floating layer values)
class UndoMergeFloating(Canvas canvas, SKRectI totalBounds, SKBitmap floatingBitmap, int fx, int fy, SKRectI initBounds) : UndoRedo
{
    private readonly UndoBitmap pixelUndo = new(canvas, totalBounds);
    private readonly SKBitmap floatingBitmapCopy = floatingBitmap.Copy();
    private readonly int floatingX = fx; // (these could be just left in the primary constructor)
    private readonly int floatingY = fy;
    private readonly SKRectI initialFloatingBounds = initBounds; // copy
    private readonly SKRegion oldSelection = new(canvas.CommittedSelection); // copy

    // must be called AFTER the canvas bitmap is modified (like UndoBitmap)
    public void PostUpdate(Canvas canvas)
    {
        pixelUndo.PostUpdate(canvas);
    }

    public override void Undo(Canvas canvas)
    {
        pixelUndo.Undo(canvas);

        canvas.RestoreFloatingState(
            floatingBitmapCopy.Copy(), 
            floatingX, 
            floatingY, 
            initialFloatingBounds, 
            new SKRegion(oldSelection) // copy
        );
    }

    public override void Redo(Canvas canvas)
    {
        canvas.MergeFloatingToMain(); // just redo merge
    }

    public override void Dispose()
    {
        pixelUndo.Dispose();
        floatingBitmapCopy.Dispose();
        oldSelection.Dispose();
    }
}