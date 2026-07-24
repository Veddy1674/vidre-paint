using SkiaSharp;
using Vidre.src.canvas;
using Vidre.src.input.cmdStack;

namespace Vidre.src.input.tools;

class PencilTool(ToolManager toolManager) : DrawTool(toolManager)
{
    private readonly SKPaint paint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1,
        IsAntialias = false,
        StrokeCap = SKStrokeCap.Square
    };
    private readonly SKPaint previewPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = 0.04f,
    };

    private SKPoint lastPoint;

    // for undo/redo
    private float minX, minY, maxX, maxY;
    private bool isDrawing;
    private SKBitmap? lastBitmap; // snapshot of the canvas before the stroke (necessary as pencil draws directly on canvasctx)

    public override void OnDown(Canvas canvas, SKPoint point, SKColor color)
    {
        // merge floating layer before drawing
        if (canvas.FloatingExists)
            canvas.MergeFloatingToMain();

        isDrawing = true;
        paint.Color = color;

        minX = maxX = point.X;
        minY = maxY = point.Y;

        lastBitmap?.Dispose();
        lastBitmap = canvas.Bitmap.Copy();

        lastPoint = point;

        // clip to selection or everywhere if no selection
        if (canvas.HasCommittedSelection)
        {
            canvas.TempCanvasCtx.Save();
            canvas.TempCanvasCtx.ClipRegion(canvas.CommittedSelection);
        }

        canvas.TempCanvasCtx.DrawPoint(point, paint); // on temp!
    }

    public override void OnMove(Canvas canvas, SKPoint end)
    {
        if (!isDrawing) return;
        
        minX = Math.Min(minX, end.X);
        minY = Math.Min(minY, end.Y);
        maxX = Math.Max(maxX, end.X);
        maxY = Math.Max(maxY, end.Y);

        canvas.TempCanvasCtx.DrawLine(lastPoint, end, paint); // write the lines directly in temp
        lastPoint = end;
    }

    public override void OnUp(Canvas canvas, SKPoint point)
    {
        // logic to undo/redo
        // if (!isDrawing) return; // ? - when a tool is being used, keybinds and tools should be blocked (written in one of the TODOs)
        isDrawing = false;

        canvas.TempCanvasCtx.DrawLine(lastPoint, point, paint); // ?

        // remove canvas clip
        if (canvas.HasCommittedSelection)
            canvas.TempCanvasCtx.Restore();

        int left = Math.Clamp((int)MathF.Floor(minX) - 1, 0, canvas.Width);
        int top = Math.Clamp((int)MathF.Floor(minY) - 1, 0, canvas.Height);
        int right = Math.Clamp((int)MathF.Ceiling(maxX) + 1, 0, canvas.Width);
        int bottom = Math.Clamp((int)MathF.Ceiling(maxY) + 1, 0, canvas.Height);

        var bounds = new SKRectI(left, top, right, bottom);

        if (bounds.Width > 0 && bounds.Height > 0)
        {
            var action = new UndoBitmap(canvas, bounds);
            canvas.MergeTempToMain(); // temp to main, like brush/eraser

            action.PostUpdate(canvas);
            canvas.UndoManager.PushAction(action);
        }
        else
        {
            canvas.TempCanvasCtx.Clear(SKColors.Transparent); // cleanup temp manually?
        }

        lastBitmap?.Dispose();
        lastBitmap = null;
    }

    public override void OnDraw(SKCanvas r, SKPoint canvasPos)
    {
        // TODO: draw cursor

        // draw pencil preview (a square)
        previewPaint.Color = SKColors.White;
        r.DrawRect(canvasPos.X + 0.05f, canvasPos.Y + 0.05f, 0.9f, 0.9f, previewPaint);

        previewPaint.Color = SKColors.Black;
        r.DrawRect(canvasPos.X, canvasPos.Y, 1, 1, previewPaint);
    }

    public override void Dispose()
    {
        paint.Dispose();
        previewPaint.Dispose();
    }
}