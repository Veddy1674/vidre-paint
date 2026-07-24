using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

// simply undo selection drag movement
class UndoDrag(int deltaX, int deltaY) : UndoRedo
{
    private readonly int deltaX = deltaX;
    private readonly int deltaY = deltaY;

    public override void Undo(Canvas canvas)
    {
        canvas.TranslateFloating(-deltaX, -deltaY);
    }

    public override void Redo(Canvas canvas)
    {
        canvas.TranslateFloating(deltaX, deltaY);
    }
}