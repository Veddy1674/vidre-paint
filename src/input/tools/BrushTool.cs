using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.tools;

class BrushTool(ToolManager toolManager) : DrawTool(toolManager)
{
    private readonly SKPaint paint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        BlendMode = SKBlendMode.SrcOver,
    };
    private readonly SKPaint previewPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = 0.04f,
    };

    private readonly SKPath path = new();
    private SKPoint lastPoint;

    public override void OnDown(Canvas canvas, SKPoint point, SKColor color)
    {
        // merge floating layer before drawing
        if (canvas.FloatingExists)
            canvas.MergeFloatingToMain();

        paint.Color = color;
        paint.StrokeWidth = toolManager.BrushSize;
        paint.IsAntialias = toolManager.AntiAliasing;

        lastPoint = point;

        path.Reset();
        path.MoveTo(point);
        path.LineTo(point); // draw once

        canvas.UpdateBrushStroke(path, paint);
    }

    public override void OnMove(Canvas canvas, SKPoint end)
    {
        var dx = end.X - lastPoint.X;
        var dy = end.Y - lastPoint.Y;
        
        // anti overdraw logic
        if ((dx * dx) + (dy * dy) < paint.StrokeWidth * paint.StrokeWidth)
            return;

        path.LineTo(end);
        lastPoint = end;

        canvas.UpdateBrushStroke(path, paint);
    }

    public override void OnUp(Canvas canvas, SKPoint point)
    {
        if (!path.IsEmpty)
            canvas.RegisterBrushUndo(path, toolManager.BrushSize);
        
        canvas.MergeTempToMain();
        path.Reset();
    }

    public override void OnDraw(SKCanvas r, SKPoint canvasPos)
    {
        // TODO: draw cursor

        // draw brush preview in screen space
        float radius = toolManager.BrushSize / 2f; // whether to use half the size is arbitrary

        previewPaint.Color = SKColors.White;
        r.DrawCircle(canvasPos, radius, previewPaint);

        // draw inner circle
        previewPaint.Color = SKColors.Black;
        r.DrawCircle(canvasPos, radius + .04f, previewPaint);
    }

    public override void Dispose()
    {
        paint.Dispose();
        previewPaint.Dispose();
        path.Dispose();
    }
}
