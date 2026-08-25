using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.UI;

class UIStatusBar(AppContext context) : IDisposable
{
    private Canvas? Canvas => context.ActiveCanvas;

    private static readonly SKPaint MainPaint = new()
    {
        Color = new SKColor(140, 140, 140), // light gray
        IsAntialias = true,
    };

    private SKRect statusRect;
    private readonly AppContext context = context;

    public const float StatusBarWidth = 400f;
    public const float StatusBarHeight = 50f;
    public const float CornerRadius = 12f;

    public void CalcStatusBar(SKRectI Screen)
    {
        // Position at bottom right of screen
        float x = Screen.Right - StatusBarWidth;
        float y = Screen.Bottom - StatusBarHeight;
        statusRect = new SKRect(x, y, x + StatusBarWidth, y + StatusBarHeight);
    }

    public void DrawStatusBar(SKCanvas r)
    {
        // rounded rect so that the top left corner is rounded
        r.DrawRoundRect(statusRect, CornerRadius, CornerRadius, MainPaint);

        // adjust other corners to not be rounded
        var sharpRect = new SKRect(statusRect.Left, statusRect.Top + CornerRadius, statusRect.Right, statusRect.Bottom);
        r.DrawRect(sharpRect, MainPaint);

        sharpRect = new SKRect(statusRect.Left + CornerRadius, statusRect.Top, statusRect.Right, statusRect.Bottom);
        r.DrawRect(sharpRect, MainPaint);

        // Draw canvas size text
        if (Canvas != null)
        {
            string sizeText = $"{Canvas.Width} x {Canvas.Height}";
            
            UIManager.MainTextFont.Size = 16f;
            UIManager.MainTextPaint.Color = SKColors.Black;
            
            // Center text in the status bar
            r.DrawText(sizeText, statusRect.MidX, statusRect.MidY + 5, SKTextAlign.Center, UIManager.MainTextFont, UIManager.MainTextPaint);
        }
    }

    #region Events (UNUSED)

    public bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        return false;
    }

    public bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        return false;
    }

    public bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        return false;
    }

    #endregion

    public void Dispose()
    {
        MainPaint.Dispose();
    }
}