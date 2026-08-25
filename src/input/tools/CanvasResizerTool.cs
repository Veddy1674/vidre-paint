using SkiaSharp;
using Silk.NET.Input;
using Vidre.src.canvas;

namespace Vidre.src.input.tools;

class CanvasResizerTool(ToolManager toolManager, AppContext context) : DrawTool(toolManager)
{
    private InputManager InputManager => context.InputManager;
    private Camera Camera => context.Camera;
    
    // handles size
    private const float HandleSize = 10f;

    public override void OnDeselect(Canvas canvas)
    {
        hoveredHandle = null;
        draggedHandle = null;
    }
    
    private enum HandlePosition
    {
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }
    
    private HandlePosition? hoveredHandle = null;
    private HandlePosition? draggedHandle = null;
    
    private readonly SKPaint handleOutlinePaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = SKColors.Black,
        IsAntialias = true
    };
    
    private readonly SKPaint handleFillPaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = SKColors.Blue,
        IsAntialias = true
    };

    public override void OnDown(Canvas canvas, SKPoint point, SKColor color)
    {
        if (hoveredHandle != null)
        {
            draggedHandle = hoveredHandle;
        }
    }

    public override void OnUp(Canvas canvas, SKPoint point)
    {
        draggedHandle = null;

        OnHover(canvas, point); // to reset cursor
    }

    public override void OnHover(Canvas canvas, SKPoint point)
    {
        // Update hover state when mouse moves without button pressed
        hoveredHandle = GetHandleAtPosition(canvas, point);

        if (hoveredHandle != null)
            InputManager.MainMouse.Cursor.StandardCursor = hoveredHandle switch
            {
                // as there aren't diagonal arrows for cross-platform, vertical arrow is used
                // for top and bottom and other handles, including diagonals, use horizontal arrow
                HandlePosition.Top or HandlePosition.Bottom => StandardCursor.VResize,
                _ => StandardCursor.HResize,
            };
        else
            InputManager.MainMouse.Cursor.StandardCursor = StandardCursor.Arrow;
    }

    public override void OnMove(Canvas canvas, SKPoint end)
    {
        if (draggedHandle != null)
        {
            // TODO: Implement resize logic
            
            // Show resize cursor for dragged handle
            InputManager.MainMouse.Cursor.StandardCursor = draggedHandle switch
            {
                HandlePosition.Top or HandlePosition.Bottom => StandardCursor.VResize,
                HandlePosition.Left or HandlePosition.Right => StandardCursor.HResize,
                _ => StandardCursor.Arrow
            };
        }
    }

    public override void OnDraw(SKCanvas r, SKPoint canvasPos)
    {
        var canvas = Camera.Canvas;
        if (canvas == null) return;
        
        // draw handles around canvas
        DrawHandle(r, 0, 0, HandlePosition.TopLeft);
        DrawHandle(r, canvas.Width / 2f, 0, HandlePosition.Top);
        DrawHandle(r, canvas.Width, 0, HandlePosition.TopRight);
        DrawHandle(r, canvas.Width, canvas.Height / 2f, HandlePosition.Right);
        DrawHandle(r, canvas.Width, canvas.Height, HandlePosition.BottomRight);
        DrawHandle(r, canvas.Width / 2f, canvas.Height, HandlePosition.Bottom);
        DrawHandle(r, 0, canvas.Height, HandlePosition.BottomLeft);
        DrawHandle(r, 0, canvas.Height / 2f, HandlePosition.Left);
    }
    
    private void DrawHandle(SKCanvas r, float x, float y, HandlePosition position)
    {
        bool isHovered = hoveredHandle == position;
        bool isDragged = draggedHandle == position;
        float size = isHovered || isDragged ? HandleSize + 4f : HandleSize;
        float halfSize = size / 2f;
        
        // black outline
        r.DrawRect(x - halfSize - 1f, y - halfSize - 1f, size + 2f, size + 2f, handleOutlinePaint);
        
        // fill color: red when dragging, blue otherwise
        handleFillPaint.Color = isDragged ? SKColors.Red : SKColors.Blue;
        r.DrawRect(x - halfSize, y - halfSize, size, size, handleFillPaint);
    }
    
    private static HandlePosition? GetHandleAtPosition(Canvas canvas, SKPoint point)
    {
        float halfSize = HandleSize / 2f + 2f; // padding for easier selection
        
        // check each handle
        if (IsPointInHandle(point, 0, 0, halfSize)) return HandlePosition.TopLeft;
        if (IsPointInHandle(point, canvas.Width / 2f, 0, halfSize)) return HandlePosition.Top;
        if (IsPointInHandle(point, canvas.Width, 0, halfSize)) return HandlePosition.TopRight;
        if (IsPointInHandle(point, canvas.Width, canvas.Height / 2f, halfSize)) return HandlePosition.Right;
        if (IsPointInHandle(point, canvas.Width, canvas.Height, halfSize)) return HandlePosition.BottomRight;
        if (IsPointInHandle(point, canvas.Width / 2f, canvas.Height, halfSize)) return HandlePosition.Bottom;
        if (IsPointInHandle(point, 0, canvas.Height, halfSize)) return HandlePosition.BottomLeft;
        if (IsPointInHandle(point, 0, canvas.Height / 2f, halfSize)) return HandlePosition.Left;
        
        return null;
    }
    
    private static bool IsPointInHandle(SKPoint point, float handleX, float handleY, float halfSize)
        => point.X >= handleX - halfSize && point.X <= handleX + halfSize &&
           point.Y >= handleY - halfSize && point.Y <= handleY + halfSize;

    public override void Dispose()
    {
        handleOutlinePaint.Dispose();
        handleFillPaint.Dispose();
    }
}