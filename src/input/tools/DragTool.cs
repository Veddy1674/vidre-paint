using SkiaSharp;
using Vidre.src.canvas;
using Vidre.src.input.cmdStack;

namespace Vidre.src.input.tools;

// aka color picker
class DragTool(ToolManager toolManager, AppContext context) : DrawTool(toolManager)
{
    private InputManager InputManager => context.InputManager;
    
    private readonly SKPaint previewPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = 0.04f,
    };

    private SKPoint startPoint;
    private bool isDragging = false;

    // for undo/redo
    private int totalDx = 0;
    private int totalDy = 0;

    public override void OnDown(Canvas canvas, SKPoint point, SKColor color)
    {
        // TODO implement resize, (and a multi use tool that is selection + drag
        // for example, check if click is inside selection and there is no modifier ...)
        if (InputManager.LeftBtnDown && canvas.HasCommittedSelection)
        {
            isDragging = true;
            startPoint = point;

            totalDx = totalDy = 0;

            if (!canvas.FloatingExists)
                canvas.StartFloatingSelection();
        }
    }

    public override void OnMove(Canvas canvas, SKPoint end)
    {
        if (!isDragging) return;

        // simple drag logic in pixels
        int dx = (int)MathF.Round(end.X - startPoint.X);
        int dy = (int)MathF.Round(end.Y - startPoint.Y);

        if (dx != 0 || dy != 0)
        {
            canvas.TranslateFloating(dx, dy);

            totalDx += dx;
            totalDy += dy;

            startPoint = new SKPoint(startPoint.X + dx, startPoint.Y + dy);
        }
    }

    public override void OnUp(Canvas canvas, SKPoint point)
    {
        // layer merge isn't done here, but after "committing" (Esc)
        isDragging = false;
        
        if (totalDx != 0 || totalDy != 0)
        {
            // only register undo drag step
            var dragStepAction = new UndoDrag(totalDx, totalDy);
            canvas.UndoManager.PushAction(dragStepAction);
        }
    }

    public override void OnDraw(SKCanvas r, SKPoint canvasPos)
    {
        // TODO: draw cursor NOTE delete previewPaint if not needed
    }

    public override void Dispose()
    {
        previewPaint.Dispose();
    }
}