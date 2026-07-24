using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

// undo any kind of selection
class UndoSelection(SKRegion _old, SKRegion _new) : UndoRedo
{
    private readonly SKRegion _old = new(_old); // copy!
    private readonly SKRegion _new = new(_new); // copy!

    public override void Undo(Canvas canvas) => canvas.RestoreSelection(_old);
    public override void Redo(Canvas canvas) => canvas.RestoreSelection(_new);

    public override void Dispose()
    {
        _old.Dispose();
        _new.Dispose();
    }
}