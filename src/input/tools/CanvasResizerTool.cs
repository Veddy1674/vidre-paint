using SkiaSharp;
using Silk.NET.Input;
using Vidre.src.canvas;
using Vidre.src.input.cmdStack;

namespace Vidre.src.input.tools;

class CanvasResizerTool(ToolManager toolManager, AppContext context) : DrawTool(toolManager)
{
    private InputManager InputManager => context.InputManager;
    private Camera Camera => context.Camera;
    
    // handles size
    private const float HandleSize = 10f;

    private HandlePosition? hoveredHandle = null;
    private HandlePosition? draggedHandle = null;
    private SKPoint dragStartPoint;
    private SKRectI? previewRect = null;
    private SKPoint lastMousePoint;

    // remove selection to avoid conflicts (not undoable)
    public override void OnSelect(Canvas canvas)
    {
        if (canvas.HasCommittedSelection)
            canvas.ClearAllSelection();
    }

    public override void OnDeselect(Canvas canvas)
    {
        hoveredHandle = null;
        draggedHandle = null;
        previewRect = null;
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
            dragStartPoint = lastMousePoint = point;
        }
    }

    public override void OnUp(Canvas canvas, SKPoint point)
    {
        // apply resize
        if (previewRect != null)
        {
            var undoAction = new UndoCrop(canvas, Camera, previewRect.Value);

            canvas.ResizeCanvas(previewRect.Value);
            // Camera.Focus(); // this is optional, as it is annoying in some cases
            canvas.ClearAllSelection();
            
            canvas.UndoManager.PushAction(undoAction);
        }

        draggedHandle = null;
        previewRect = null;

        OnHover(canvas, point); // to reset cursor
    }

    public override void OnHover(Canvas canvas, SKPoint point)
    {
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
        if (draggedHandle == null) return;

        lastMousePoint = end;

        bool shift = InputManager.Modifiers.HasFlag(Modifier.Shift);
        bool ctrl = InputManager.Modifiers.HasFlag(Modifier.Ctrl);

        var delta = new SKPoint(end.X - dragStartPoint.X, end.Y - dragStartPoint.Y);

        float dX = delta.X;
        float dY = delta.Y;

        if (!shift)
        {
            // ignore delta for the axis that doesn't correspond to the handle
            if (draggedHandle is HandlePosition.Top or HandlePosition.Bottom)
                dX = 0;
            else if (draggedHandle is HandlePosition.Left or HandlePosition.Right)
                dY = 0;
        }
        else // shift press logic (square selection)
        {
            // resize based on the fixed aspect ratio
            float ratio = canvas.AspectRatio;

            switch (draggedHandle)
            {
                case HandlePosition.Right:
                case HandlePosition.Left:
                    dY = dX / ratio;
                    break;

                case HandlePosition.Top:
                case HandlePosition.Bottom:
                    dX = dY * ratio;
                    break;

                case HandlePosition.BottomRight:
                case HandlePosition.TopLeft:
                    if (MathF.Abs(dX) > MathF.Abs(dY * ratio))
                        dY = dX / ratio;
                    else
                        dX = dY * ratio;
                    break;

                case HandlePosition.TopRight:
                case HandlePosition.BottomLeft:
                    if (MathF.Abs(dX) > MathF.Abs(dY * ratio))
                        dY = -dX / ratio;
                    else
                        dX = -dY * ratio;
                    break;
            }
        }

        // TODO: fix shift/square selection logic, every handle including top bottom left right should activate it
        // and fix its odd behavior when dragging while shift is pressed...

        int left = 0;
        int top = 0;
        int right = canvas.Width;
        int bottom = canvas.Height;

        // apply delta based on which handle is dragged
        switch (draggedHandle)
        {
            case HandlePosition.Right:
                right += (int)dX;
                break;
            case HandlePosition.Bottom:
                bottom += (int)dY;
                break;
            case HandlePosition.BottomRight:
                right += (int)dX;
                bottom += (int)dY;
                break;

            case HandlePosition.Left:
                left += (int)dX;
                break;
            case HandlePosition.Top:
                top += (int)dY;
                break;
            case HandlePosition.TopLeft:
                left += (int)dX;
                top += (int)dY;
                break;

            case HandlePosition.TopRight:
                right += (int)dX;
                top += (int)dY;
                break;
            case HandlePosition.BottomLeft:
                left += (int)dX;
                bottom += (int)dY;
                break;
        }

        if (ctrl) // ctrl press logic (expand other direction)
        {
            switch (draggedHandle)
            {
                case HandlePosition.Right:
                    left -= (int)dX;
                    break;
                case HandlePosition.Left:
                    right -= (int)dX;
                    break;
                
                case HandlePosition.Bottom:
                    top -= (int)dY;
                    break;
                case HandlePosition.Top:
                    bottom -= (int)dY;
                    break;

                case HandlePosition.BottomRight:
                    left -= (int)dX;
                    top -= (int)dY;
                    break;
                case HandlePosition.TopLeft:
                    right -= (int)dX;
                    bottom -= (int)dY;
                    break;
                case HandlePosition.TopRight:
                    left -= (int)dX;
                    bottom -= (int)dY;
                    break;
                case HandlePosition.BottomLeft:
                    right -= (int)dX;
                    top -= (int)dY;
                    break;
            }
        }

        // avoid negative dimensions (1px minimum)
        if (right - left > 1 && bottom - top > 1)
            previewRect = new SKRectI(left, top, right, bottom);
    }

    public override void OnModifier(Canvas canvas, Modifier modifiers)
    {
        if (draggedHandle != null)
            OnMove(canvas, lastMousePoint);
    }

    public override void OnDraw(SKCanvas r, SKPoint canvasPos)
    {
        var canvas = Camera.Canvas;
        if (canvas == null) return;

        // alias for previewRect
        var rect = new SKRectI(0, 0, canvas.Width, canvas.Height);

        if (draggedHandle != null && previewRect.HasValue)
        {
            var canvasRect = rect; // reset (full canvas)
            rect = previewRect.Value;

            // draw what is going to be added as transparency
            r.Save();
            r.ClipRect(canvasRect, SKClipOperation.Difference);
            r.DrawRect(previewRect.Value, Canvas.TransparencyPaint);
            r.Restore();

            // draw what is going to be cut out (with AppBGColor so it looks like cropping)
            r.Save();
            r.ClipRect(previewRect.Value, SKClipOperation.Difference);
            r.DrawRect(canvasRect, Canvas.AppBGColorPaint);
            r.Restore();

            // draw an outline to not lose track of what the original canvas size was
            canvas.DrawSelectionOutline(r, Camera, canvasRect);
        }

        // draw stuff in screen space:
        r.Save();
        r.ResetMatrix();
        
        float midX = rect.Left + rect.Width / 2f;
        float midY = rect.Top + rect.Height / 2f;

        var pTop = Camera.CanvasToScreenPos(midX, rect.Top);
        var pBottom = Camera.CanvasToScreenPos(midX, rect.Bottom);
        var pLeft = Camera.CanvasToScreenPos(rect.Left, midY);
        var pRight = Camera.CanvasToScreenPos(rect.Right, midY);
        var pTopLeft = Camera.CanvasToScreenPos(rect.Left, rect.Top);
        var pTopRight = Camera.CanvasToScreenPos(rect.Right, rect.Top);
        var pBottomRight = Camera.CanvasToScreenPos(rect.Right, rect.Bottom);
        var pBottomLeft = Camera.CanvasToScreenPos(rect.Left, rect.Bottom);

        // draw handles around previewRect
        DrawHandle(r, pTop.X, pTop.Y, HandlePosition.Top);
        DrawHandle(r, pBottom.X, pBottom.Y, HandlePosition.Bottom);
        DrawHandle(r, pLeft.X, pLeft.Y, HandlePosition.Left);
        DrawHandle(r, pRight.X, pRight.Y, HandlePosition.Right);
        DrawHandle(r, pTopLeft.X, pTopLeft.Y, HandlePosition.TopLeft);
        DrawHandle(r, pTopRight.X, pTopRight.Y, HandlePosition.TopRight);
        DrawHandle(r, pBottomRight.X, pBottomRight.Y, HandlePosition.BottomRight);
        DrawHandle(r, pBottomLeft.X, pBottomLeft.Y, HandlePosition.BottomLeft);

        // draw text of canvas size (fixed in place) at top right
        string text = $"{canvas.Width}x{canvas.Height}";
        var canvasTopRight = Camera.CanvasToScreenPos(canvas.Width, 0);

        Canvas.DrawTextScreenSpace(r, text, canvasTopRight.X + 8f, canvasTopRight.Y - 8f);

        // draw text of previewRect size at top left
        string previewText = $"{rect.Width}x{rect.Height}";
        
        Canvas.DrawTextScreenSpace(r, previewText, pTopLeft.X - 8f, pTopLeft.Y - 8f, SKTextAlign.Right);

        r.Restore();
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