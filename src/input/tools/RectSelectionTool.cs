using SkiaSharp;
using Vidre.src.canvas;
using Vidre.src.input.cmdStack;

namespace Vidre.src.input.tools;

class RectSelectionTool(ToolManager toolManager, AppContext context) : DrawTool(toolManager)
{
    private InputManager InputManager => context.InputManager;

    private float startX, startY;
    private float endX, endY;

    private bool startedWithCtrl = false;
    private bool startedWithAlt = false;

    private bool initiatedSelect = false; // true on mouse down, false on commit

    private SKRegion? oldSelectionSnapshot; // for undo/redo

    public override void OnModifier(Canvas canvas, Modifier modifiers)
    {
        if (initiatedSelect)
            this.OnMove(canvas, new SKPoint(endX, endY));
    }

    public override void OnDown(Canvas canvas, SKPoint point, SKColor _)
    {
        initiatedSelect = true;

        oldSelectionSnapshot?.Dispose();
        oldSelectionSnapshot = new SKRegion(canvas.CommittedSelection);

        startedWithAlt = InputManager.Modifiers.HasFlag(Modifier.Alt);
        startedWithCtrl = InputManager.Modifiers.HasFlag(Modifier.Ctrl) && !startedWithAlt; // alt has priority (so they're mutually exclusive)

        // alt to deselect, ctrl to add selection, so if either are pressed, keep current selection
        if (!startedWithAlt && !startedWithCtrl)
        // {
            // this isn't necessary as when rect selection is chosen while dragging a floating layer, it gets committed
            // if (canvas.FloatingExists)
            //     canvas.MergeFloatingToMain();
            canvas.ClearAllSelection();
        // }

        startX = point.X;
        startY = point.Y;

        ClampToCanvas(canvas, ref startX, ref startY);
    }

    public override void OnMove(Canvas canvas, SKPoint end)
    {
        endX = end.X;
        endY = end.Y;

        ClampToCanvas(canvas, ref endX, ref endY);

        // if was pressing alt/ctrl and now not, it becomes the normal selection (replaces whole selection with current)
        if ((!InputManager.Modifiers.HasFlag(Modifier.Alt) && startedWithAlt) || (!InputManager.Modifiers.HasFlag(Modifier.Ctrl) && startedWithCtrl))
        {
            canvas.ClearAllSelection();

            // (if the first condition is true, this becomes false)
            startedWithAlt = InputManager.Modifiers.HasFlag(Modifier.Alt);

            // (if the second condition is true, this becomes false)
            startedWithCtrl = InputManager.Modifiers.HasFlag(Modifier.Ctrl);
        }

        // min and max to normalize, floor to round to pixel corners
        float left = MathF.Floor(Math.Min(startX, endX));
        float top = MathF.Floor(Math.Min(startY, endY));
        float right = MathF.Floor(Math.Max(startX, endX)) + 1;
        float bottom = MathF.Floor(Math.Max(startY, endY)) + 1;

        // square selection
        if (InputManager.Modifiers.HasFlag(Modifier.Shift))
        {
            float size = Math.Max(right - left, bottom - top);

            // during square selection, two faces of the rectangle are moving, this clamp below
            // makes it so if one of the two faces is outside the canvas, the other gets clamped ("stops moving" effect)
            size = Math.Min(size, startX < endX ? canvas.Width - left : right);
            size = Math.Min(size, startY < endY ? canvas.Height - top : bottom);

            // maintain direction (if width > height, height = width and viceversa)
            right = startX < endX ? left + size : right;
            bottom = startY < endY ? top + size : bottom;
            left = startX < endX ? left : right - size;
            top = startY < endY ? top : bottom - size;
        }

        // it's already clamped to canvas because both start and end positions were clamped
        // but as "+1" is added to right and bottom, it can go out of bounds when selecting bottom and right edges
        right = Math.Min(right, canvas.Width);
        bottom = Math.Min(bottom, canvas.Height);

        canvas.UpdateSelection(
            new SKRectI((int)left, (int)top, (int)right, (int)bottom),

            // NOTE: startedWithAlt is true when Inputs.AltDown is true as well, same with ctrl
            startedWithAlt, // deselection
            startedWithCtrl // add selection, always false if startedWithAlt is true
        );
    }

    private static void ClampToCanvas(Canvas canvas, ref float x, ref float y)
    {
        x = Math.Clamp(x, 0, canvas.Width);
        y = Math.Clamp(y, 0, canvas.Height);
    }

    public override void OnUp(Canvas canvas, SKPoint point)
    {
        initiatedSelect = false;
        canvas.CommitSelection();

        // register undo/redo
        if (oldSelectionSnapshot != null)
        {
            if (!oldSelectionSnapshot.Equals(canvas.CommittedSelection))
                canvas.UndoManager.PushAction(new UndoSelection(oldSelectionSnapshot, canvas.CommittedSelection));

            oldSelectionSnapshot.Dispose();
            oldSelectionSnapshot = null;
        }
    }
}