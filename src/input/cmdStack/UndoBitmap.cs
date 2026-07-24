using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.cmdStack;

// undo any kind of change to canvas Bitmap
class UndoBitmap : UndoRedo
{
    private readonly SKBitmap oldPixels;
    private readonly SKBitmap newPixels;
    private readonly SKRectI bounds; // only allocate area inside bounds

    public UndoBitmap(Canvas canvas, SKRectI bounds)
    {
        this.bounds = bounds; // could be canvas.CommittedSelection.Bounds

        oldPixels = new SKBitmap(bounds.Width, bounds.Height);
        
        using (var copyCanvas = new SKCanvas(oldPixels))
            canvas.DrawWithContext(ctx => 
                copyCanvas.DrawBitmap(canvas.Bitmap, -bounds.Left, -bounds.Top)
            );
        
        newPixels = new SKBitmap(bounds.Width, bounds.Height);
    }

    // for pencil tool
    public UndoBitmap(SKBitmap oldBitmap, Canvas canvas, SKRectI bounds)
    {
        this.bounds = bounds;

        // old
        oldPixels = new SKBitmap(bounds.Width, bounds.Height);
        using (var copyCanvas = new SKCanvas(oldPixels))
            copyCanvas.DrawBitmap(oldBitmap, -bounds.Left, -bounds.Top);

        // new
        newPixels = new SKBitmap(bounds.Width, bounds.Height);
        using (var copyCanvas = new SKCanvas(newPixels))
            copyCanvas.DrawBitmap(canvas.Bitmap, -bounds.Left, -bounds.Top);
    }

    // for brush tool!
    public UndoBitmap(SKBitmap mainBitmap, SKBitmap tempBitmap, SKRectI bounds)
    {
        this.bounds = bounds;

        oldPixels = new SKBitmap(bounds.Width, bounds.Height);
        using (var canvas = new SKCanvas(oldPixels))
            canvas.DrawBitmap(mainBitmap, -bounds.Left, -bounds.Top);

        newPixels = new SKBitmap(bounds.Width, bounds.Height);
        using (var canvas = new SKCanvas(newPixels))
        {
            canvas.DrawBitmap(mainBitmap, -bounds.Left, -bounds.Top);
            canvas.DrawBitmap(tempBitmap, -bounds.Left, -bounds.Top);
        }
    }

    // must be called AFTER the canvas bitmap is modified
    public void PostUpdate(Canvas canvas) // TODO rename
    {
        using var copyCanvas = new SKCanvas(newPixels);
        canvas.DrawWithContext(ctx => 
        {
            copyCanvas.DrawBitmap(canvas.Bitmap, -bounds.Left, -bounds.Top);
        });
    }

    public override void Undo(Canvas canvas)
    {
        canvas.DrawWithContext(ctx =>
        {
            using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
            ctx.DrawBitmap(oldPixels, bounds.Left, bounds.Top, paint);
        });
    }

    public override void Redo(Canvas canvas)
    {
        canvas.DrawWithContext(ctx =>
        {
            using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
            ctx.DrawBitmap(newPixels, bounds.Left, bounds.Top, paint);
        });
    }

    public override void Dispose()
    {
        oldPixels.Dispose();
        newPixels.Dispose();
    }
}