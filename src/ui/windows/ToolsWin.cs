using SkiaSharp;
using Vidre.src.input;

namespace Vidre.src.UI.windows;

class ToolsWin(SKRectI screen, ToolManager toolManager) : FloatingWin(screen,
    "Tools",
    x: 8,
    y: 8,
    width: 80,
    height: 500,
    centerTitleHorizontally: true
)
{
    private static readonly SKBitmap toolsSheet = Utils.LoadImage("tools_sheet.png");
    private readonly SKRect[] toolsRects = new SKRect[Enum.GetNames<EnumTool>().Length];
    private int hoveringToolIndex = -1;

    private float toolRectSize; // width = 80 means each tool icon is 40x40 (affected by rectOffset below)

    public override void Init(SKRect win, SKPaint paint)
    {
        toolRectSize = this.Width / 2;
    }

    protected override void DrawContent(SKCanvas r, SKRect win, bool windowMoved, double deltaTime, SKPaint paint)
    {
        // create rects and draw hovering rect
        for (int i = 0; i < toolsRects.Length; i++)
        {
            const float rectOffset = 4f; // higher = smaller rect
            const float rectOffset2 = rectOffset * 2f;

            toolsRects[i] = SKRect.Create(
                x: win.Left + rectOffset + (i % 2 * toolRectSize), // 0, 40, 0, 40...
                y: win.Top + rectOffset + (i / 2 * toolRectSize), // +40 each row (2 tools per row)
                width: toolRectSize - rectOffset2,
                height: toolRectSize - rectOffset2
            );

            if ((EnumTool)i == toolManager.GetActiveTool())
            {
                paint.Color = Config.AppUIsSelectedColor;
                r.DrawRect(toolsRects[i], paint);
            }
            else if (i == hoveringToolIndex)
            {
                paint.Color = Config.AppUIsHoverColor;
                r.DrawRect(toolsRects[i], paint);
            }
        }

        // draw all tools
        r.DrawBitmap(toolsSheet, win.Left, win.Top);
    }

    public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (base.OnMouseDown(leftDown, rightDown, mousePos)) return true;

        if (leftDown && hoveringToolIndex != -1 && toolManager.GetActiveTool() != (EnumTool)hoveringToolIndex)
        {
            toolManager.SetActiveTool((EnumTool)hoveringToolIndex);
            return true;
        }

        if (base.ContentRect.Contains(mousePos))
            return true; // always return true if click is inside UI

        return false;
    }

    public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (base.OnMouseUp(leftDown, rightDown, mousePos)) return true;

        return false;
    }

    public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        if (base.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;

        // NOTE: there's minor visual glitches related to this approach (and in other classes like QuickColors in ColorsWin)
        // but fixing is not worth it: drag tool window on top of colors window, trigger OnMouseMove on a quick color button,
        // then move the mouse to the tools window - the hover effect will remain on the quick color button until mouse moves out of all the windows
        hoveringToolIndex = -1;
        
        // update hovering tool index
        for (int i = 0; i < toolsRects.Length; i++)
        {
            if (toolManager.GetActiveTool() == (EnumTool)i) continue;

            if (toolsRects[i].Contains(mousePos))
            {
                hoveringToolIndex = i;
                return true; // so that windows behind don't process hover animations
            }
        }

        // always return if mouse is inside window (so windows behind aren't processed)
        if (base.HeaderRect.Contains(mousePos) || base.ContentRect.Contains(mousePos))
            return true;

        return false;
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
    }
}