using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

// undo crop selection (canvas size, bitmap, selection and camera)
class UndoCrop(Canvas canvas, Camera camera, SKRectI cropBounds) : UndoRedo
{
    private readonly SKBitmap oldBitmap = canvas.Bitmap.Copy();
    private readonly SKRegion oldSelection = new(canvas.CommittedSelection);
    private readonly SKRectI cropBounds = cropBounds;

    public override void Undo(Canvas canvas)
    {
        // regenerate whole bitmap
        canvas.SetBitmap(oldBitmap.Copy());
        
        // undo selection
        canvas.ClearAllSelection();
        canvas.UpdateSelection(oldSelection.Bounds); // NOTE or set commitedselection directly somehow, as this only allows square selections
        canvas.CommitSelection();

        camera.Focus();
    }

    public override void Redo(Canvas canvas)
    {
        canvas.CropToRect(cropBounds);
        canvas.ClearAllSelection();

        camera.Focus();
    }

    public override void Dispose()
    {
        oldBitmap.Dispose();
        oldSelection.Dispose();
    }
}