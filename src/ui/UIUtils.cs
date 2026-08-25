using SkiaSharp;

namespace Vidre.src.UI;

static class UIUtils
{
    public static SKRect DrawDebug(this SKRect rect, SKCanvas r, SKPaint paint)
    {
        // save settings
        var c = paint.Color;
        var sw = paint.StrokeWidth;
        var s = paint.Style;

        // apply debug settings
        paint.Color = SKColors.Red;
        paint.StrokeWidth = 1f;
        paint.Style = SKPaintStyle.Stroke;

        r.DrawRect(rect, paint);

        // restore settings
        paint.Color = c;
        paint.StrokeWidth = sw;
        paint.Style = s;

        return rect;
    }
    
    private static readonly SKColor ButtonFGColor = new(70, 70, 70);
    private static readonly SKColor ButtonBGColor = new(90, 90, 90);

    public static void DrawButton(this SKCanvas r, SKPaint paint, string? text, float x, float y, float width, float height, bool shadow = true)
    {
        if (shadow)
        {
            paint.Color = ButtonBGColor;
            r.DrawRoundRect(x, y, width, height + 2, 2, 2, paint);
        }
        
        paint.Color = ButtonFGColor;
        r.DrawRoundRect(x, y, width, height, 2, 2, paint);

        if (text == null) return;

        var textFont = UIManager.MainTextFont;

        // center vertically
        float textY = y + (height / 2f) - (textFont.Metrics.Ascent + textFont.Metrics.Descent) / 2f;

        textFont.Size = 14f;
        UIManager.MainTextPaint.Color = SKColors.White;
        r.DrawText(text, x + width / 2f, textY, SKTextAlign.Center, textFont, UIManager.MainTextPaint);
    }

    public static void DrawButton(this SKCanvas r, SKPaint paint, string text, SKRect rect, bool shadow = true)
        => DrawButton(r, paint, text, rect.Left, rect.Top, rect.Width, rect.Height, shadow);

    public static void DrawInput(this SKCanvas r, SKPaint paint, string text, float x, float y, float width, float height, float offsetSpace = 0)
    {
        float halfos = offsetSpace / 2f;
        float o = width - offsetSpace; // offset for the "-" and "+"

        paint.Color = ButtonFGColor;
        r.DrawRect(x, y, o, height, paint); // main border

        if (offsetSpace > 0)
        {
            r.DrawRect(x + o, y, halfos, height, paint); // border of "-"
            r.DrawRect(x + o + halfos, y, halfos, height, paint); // border of "+"
        }

        paint.Color = Config.AppUIsBGColor;
        r.DrawRect(x + 2, y + 2, width - 4 - offsetSpace, height - 4, paint); // main inner

        if (offsetSpace > 0)
        {
            r.DrawRect(x + o + 2, y + 2, halfos - 4, height - 4, paint); // inner of "-"
            r.DrawRect(x + o + 2 + halfos, y + 2, halfos - 4, height - 4, paint); // inner of "+"
        }

        var textFont = UIManager.MainTextFont;

        // center vertically
        float textY = y + (height / 2f) - (textFont.Metrics.Ascent + textFont.Metrics.Descent) / 2f;

        textFont.Size = 14f;
        UIManager.MainTextPaint.Color = SKColors.White;
        r.DrawText(text, x + 4, textY, SKTextAlign.Left, textFont, UIManager.MainTextPaint);

        if (offsetSpace == 0) return;

        // "-"
        float minusCenterX = x + o + halfos / 2f;
        r.DrawText("-", minusCenterX, textY, SKTextAlign.Center, textFont, UIManager.MainTextPaint);

        // "+"
        float plusCenterX = x + o + halfos + halfos / 2f;
        r.DrawText("+", plusCenterX, textY, SKTextAlign.Center, textFont, UIManager.MainTextPaint);
    }

    public static void DrawShadowRect(this ref SKRect rect, SKCanvas r, SKPaint paint, float rx = 0f, float ry = 0f, float offset = 2f)
    {
        rect.Offset(0, offset);
        
        if (rx > 0 || ry > 0)
            r.DrawRoundRect(rect, rx, ry, paint);
        else
            r.DrawRect(rect, paint);

        rect.Offset(0, -offset);
    }
}