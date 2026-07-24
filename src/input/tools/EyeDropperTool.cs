using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.tools;

// aka color picker
class EyeDropperTool(ToolManager toolManager, AppContext context) : DrawTool(toolManager)
{
    private InputManager InputManager => context.InputManager;
    
    private readonly SKPaint previewPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = 0.04f,
    };

    private void SampleColor(Canvas canvas, SKPoint point)
    {
        int x = (int)MathF.Floor(point.X);
        int y = (int)MathF.Floor(point.Y);

        // avoid sampling oob
        if (x >= 0 && x < canvas.Width && y >= 0 && y < canvas.Height)
        {
            SKColor sampledColor = canvas.Bitmap.GetPixel(x, y);

            // update primary or secondary color, giving priority to left (left + right = left)
            if (InputManager.RightBtnDown)
                toolManager.SetSecondaryColor(sampledColor);
            else
                // updates the rgbwheel, sliders and hex input automatically through events
                toolManager.SetPrimaryColor(sampledColor);
        }
    }

    public override void OnDown(Canvas canvas, SKPoint point, SKColor color)
    {
        SampleColor(canvas, point);
    }

    public override void OnMove(Canvas canvas, SKPoint end)
    {
        SampleColor(canvas, end); // allow drag to sample colors
    }

    public override void OnDraw(SKCanvas r, SKPoint canvasPos)
    {
        // TODO: draw cursor

        // same as pencil preview (a square)
        previewPaint.Color = SKColors.White;
        r.DrawRect(canvasPos.X + 0.05f, canvasPos.Y + 0.05f, 0.9f, 0.9f, previewPaint);

        previewPaint.Color = SKColors.Black;
        r.DrawRect(canvasPos.X, canvasPos.Y, 1, 1, previewPaint);
    }

    public override void Dispose()
    {
        previewPaint.Dispose();
    }
}