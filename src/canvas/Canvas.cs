using SkiaSharp;
using Vidre.src.input.cmdStack;
using Vidre.src.UI;

namespace Vidre.src.canvas;

sealed class Canvas : IDisposable
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    // cached just in case it's called very often
    // (manually updated when Width or Height change)
    public float AspectRatio { get; private set; }

    public SKBitmap Bitmap { get; private set; } // the pixels
    public SKCanvas CanvasCtx { get; private set; } // context to draw in

    // lazy-loaded to avoid double allocation on startup (e.g., 4K image = 66MB instead of current 33MB)
    // only allocated when first needed
    private SKBitmap? _tempBitmap;
    public SKBitmap TempBitmap => _tempBitmap ??= new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);

    private SKCanvas? _tempCanvasCtx;
    public SKCanvas TempCanvasCtx
    {
        get
        {
            if (_tempCanvasCtx == null)
            {
                _tempCanvasCtx = new SKCanvas(TempBitmap);
                _tempCanvasCtx.Clear(SKColors.Transparent);
            }
            return _tempCanvasCtx;
        }
    }

    private SKPaint? tempLayerPreviewPaint = null; // for brush & eraser preview on canvas

    // managing temp floating image/selection
    public SKBitmap? FloatingBitmap { get; private set; }
    public int FloatingX { get; private set; }
    public int FloatingY { get; private set; }
    public bool FloatingExists => FloatingBitmap != null;
    // if position of committed selection is the same as floating bitmap
    // this is to color the selection area gray or blue (gray when moved out of its original position)
    public bool IsSelectionFloating =>
        FloatingExists && 
        (FloatingX != initialFloatingBounds.Left || FloatingY != initialFloatingBounds.Top);
    
    // font and paint with difference effect (e.g: colored white when background is black and viceversa)
    private static readonly SKFont MainTextFont;
    private static readonly SKPaint MainTextPaint;

    // the classic white and gray pattern to represent transparency
    public readonly static SKPaint TransparencyPaint;

    // a paint made to look exactly like the background color, used elsewhere (e.g: CanvasResizerTool.cs)
    public readonly static SKPaint AppBGColorPaint = new()
    {
        Color = Config.AppBGColor
    };

    // dotted-looking selection
    public readonly static SKPaint SelectionPaint;
    public readonly static SKPaint SelectionFillPaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = Config.SelectionColorFill // color changed dynamically
    };

    public readonly UndoManager UndoManager; // set of undoable/redoable actions

    // init static variables only
    static Canvas()
    {
        // init transparency paint
        using var tempBitmap = new SKBitmap(16, 16);
        using (var tempCanvas = new SKCanvas(tempBitmap))
        {
            tempCanvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = new SKColor(180, 180, 180) };

            tempCanvas.DrawRect(0, 0, 8, 8, paint);
            tempCanvas.DrawRect(8, 8, 8, 8, paint);
        }
        
        using var transparencyShader = SKShader.CreateBitmap(tempBitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        TransparencyPaint = new() { Shader = transparencyShader };

        // init selection paint
        SelectionPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Butt
            // color and stroke width are changed dynamically
        };

        // init font and text
        MainTextFont = new SKFont(SKTypeface.Default, 13f);
        MainTextPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
            Color = SKColors.White,
            BlendMode = SKBlendMode.Difference // invert color so it's dark on light colored background and viceversa
        };
    }

    // manual constructor
    public Canvas(int width, int height, CanvasType type)
    {
        UndoManager = new(this);

        Width = width;
        Height = height;
        AspectRatio = (float)Width / Height;

        Bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        CanvasCtx = new SKCanvas(Bitmap);

        Canvas.UpdateGridPath(this);
        Reset(type); // fills CanvasCtx with whatever and TempCanvasCtx with transparency
    }

    // constructor from bitmap
    public Canvas(SKBitmap bitmap)
    {
        UndoManager = new(this);

        Width = bitmap.Width;
        Height = bitmap.Height;
        AspectRatio = (float)Width / Height;

        Bitmap = bitmap;
        CanvasCtx = new SKCanvas(Bitmap);

        Canvas.UpdateGridPath(this);
    }

    public void Reset(CanvasType type = CanvasType.White)
    {
        CanvasCtx.Clear(type switch
        {
            CanvasType.White => SKColors.White,
            CanvasType.Black => SKColors.Black,
            _ => SKColors.Empty,
        });
        _tempCanvasCtx?.Clear(SKColors.Transparent);
    }

    // to save
    public SKData Encode(SKEncodedImageFormat format, int quality)
        => Bitmap.Encode(format, quality);

    // "Canvas" to differentiate it by ResizeImage (which uses interpolation and such)
    // meanwhile this simply adjusts the canvas dimensions without affecting the bitmap (other than eventually cropping it)
    // this method ALWAYS puts transparency on the newly added area
    public void ResizeCanvas(SKRectI bounds)
    {
        int newWidth = bounds.Width;
        int newHeight = bounds.Height;

        var newBitmap = new SKBitmap(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var newCanvas = new SKCanvas(newBitmap);

        // clear the new canvas with transparency!
        newCanvas.Clear(SKColors.Empty);

        // only copy the visible part of the bitmap to the new canvas
        newCanvas.DrawBitmap(Bitmap, -bounds.Left, -bounds.Top);

        Bitmap.Dispose();
        CanvasCtx.Dispose();
        _tempBitmap?.Dispose();
        _tempCanvasCtx?.Dispose();

        Bitmap = newBitmap;
        CanvasCtx = new SKCanvas(Bitmap);

        _tempBitmap = null;
        _tempCanvasCtx = null;

        Width = newWidth;
        Height = newHeight;

        Canvas.UpdateGridPath(this);
    }

    // draw canvas on screen based on camera
    public void DrawAll(SKCanvas r, AppContext context)
    {
        var camera = context.Camera;

        // canvas area on screen
        var rect = camera.GetCanvasOnScreen();

        // 1. draw transparency shader
        r.Save();

        r.ClipRect(rect);
        r.DrawRect(rect, TransparencyPaint);

        r.Restore();

        r.Save();
        r.SetMatrix(camera.CamMatrix); // canvas space:

        // 2. draw all pixels on screen
        r.SaveLayer();
        r.DrawBitmap(Bitmap, 0, 0);

        // 3. draw temporary layer (pixels that are being drawn)
        r.DrawBitmap(TempBitmap, 0, 0, tempLayerPreviewPaint);
        r.Restore(); // the layer

        // 4. draw floating layer (selection/pasted image)
        if (FloatingExists)
        {
            // clip inside canvas
            r.Save();
            r.ClipRect(new SKRect(0, 0, Width, Height));

            r.DrawBitmap(FloatingBitmap, FloatingX, FloatingY); // draw

            r.Restore();
        }

        // 5. draw grid if enabled
        Canvas.DrawGrid(r, camera);

        // 6. draw selection
        this.DrawSelection(r, camera);

        // 7. draw active tool preview and cursor FIXME: it should not be drawn in canvas space?
        context.InputManager.OnDraw(r); // has camera internally
        
        r.Restore(); // screen space:

        // 8. draw preview selection size
        this.DrawSelectionSize(r, camera);
    }

    #region Grid management

    // static because it's a visual effect
    public static bool ShowGrid { get; private set; } = false;
    public static int GridSkip { get; private set; } = 1;

    public static SKPath? GridPath { get; private set; }

    public static readonly SKPaint GridPaint = new()
    {
        Color = new(0, 0, 0, 80),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0f,
        IsAntialias = false
    };

    public static void UpdateGridPath(Canvas instance)
    {
        GridPath?.Dispose();
        GridPath = null;

        if (!ShowGrid) return;
        
        GridPath = new SKPath();

        for (int x = 0; x <= instance.Width; x += GridSkip) {
            GridPath.MoveTo(x, 0);
            GridPath.LineTo(x, instance.Height);
        }

        for (int y = 0; y <= instance.Height; y += GridSkip) {
            GridPath.MoveTo(0, y);
            GridPath.LineTo(instance.Width, y);
        }
    }

    public static void EnableGrid(Canvas instance, bool active)
    {
        ShowGrid = active;
        Canvas.UpdateGridPath(instance);
    }

    public static void DrawGrid(SKCanvas r, Camera camera)
    {
        if (!ShowGrid || camera.CurrentZoom < 4f) return; // canvas too small, no sense to render grid (avoid Moiré effect)

        byte opacity = (byte)Math.Clamp((camera.CurrentZoom - 4f) * 15, 0, 80);
        GridPaint.Color = new(0, 0, 0, opacity);

        r.DrawPath(GridPath, GridPaint);
    }
    #endregion

    public void DrawSelectionSize(SKCanvas r, Camera camera)
    {
        if (previewSelection.IsEmpty) return;

        var bounds = previewSelection.Bounds;
        string text = $"{bounds.Width}x{bounds.Height}";
        
        var canvasPoint = new SKPoint(bounds.Right, bounds.Top);
        var screenPoint = camera.CanvasToScreenPos(canvasPoint);

        float textX = screenPoint.X + 6f;
        float textY = screenPoint.Y - 6f;

        DrawTextScreenSpace(r, text, textX, textY);
    }

    // to draw any text in SCREEN SPACE using Canvas.MainTextFont with the difference effect
    public static void DrawTextScreenSpace(SKCanvas r, string text, float x, float y, SKTextAlign textAlign = SKTextAlign.Left)
    {
        r.Save();
        r.ResetMatrix(); // to make sure it's being drawn in screen space

        r.DrawText(text, x, y, textAlign, MainTextFont, MainTextPaint); // draw

        r.Restore();
    }

    #region selection on canvas

    public SKRegion CommittedSelection { get; private set; } = new(); // ACTUAL selection
    private SKRegion previewSelection = new(); // CommittedSelection + current selection

    public bool HasCommittedSelection => !CommittedSelection.IsEmpty;

    private SKRectI currentAdditionalRect; // selection when pressing ctrl
    private SKRectI currentDeselectionRect;

    // called on mouse move
    public void UpdateSelection(in SKRectI rect, bool altDown = false, bool ctrlDown = false)
    {
        // NOTE: the clamp must be implemented by the caller
        
        // to draw the special fill of deselection
        currentDeselectionRect = altDown ? rect : SKRectI.Empty;
        // to draw the special fill of additional selection
        currentAdditionalRect = ctrlDown ? rect : SKRectI.Empty;

        previewSelection.Dispose();

        using var rectRegion = new SKRegion();
        rectRegion.SetRect(rect);

        previewSelection = new SKRegion(HasCommittedSelection ? CommittedSelection : rectRegion);

        // don't apply deselection or additional selection before commit
    }

    // on mouse up after selecting
    public void CommitSelection()
    {
        // apply deselection (difference)
        if (!currentDeselectionRect.IsEmpty)
        {
            using var rectRegion = new SKRegion(currentDeselectionRect);

            previewSelection.Op(rectRegion, SKRegionOperation.Difference);

            currentDeselectionRect = SKRectI.Empty;
        }

        // apply additional selection (union)
        if (!currentAdditionalRect.IsEmpty)
        {
            using var rectRegion = new SKRegion(currentAdditionalRect);

            previewSelection.Op(rectRegion, SKRegionOperation.Union);

            currentAdditionalRect = SKRectI.Empty;
        }

        CommittedSelection.Dispose();
        CommittedSelection = new SKRegion(previewSelection); // apply preview (replace whole with preview)
    }

    public void ClearAllSelection()
    {
        CommittedSelection.Dispose();
        CommittedSelection = new();
        
        previewSelection.Dispose();
        previewSelection = new();

        currentDeselectionRect = currentAdditionalRect = SKRectI.Empty;
    }

    // used in Keybinds.cs and such
    public void SetSelection(SKRegion region)
    {
        CommittedSelection.Dispose();
        CommittedSelection = new SKRegion(region);
        
        previewSelection.Dispose();
        previewSelection = new SKRegion(region);
        
        currentDeselectionRect = currentAdditionalRect = SKRectI.Empty;
    }

    private readonly float[] _dashIntervals = new float[2]; // tiny optimization via cache
    private float dashOffset = 0;

    private void DrawSelection(SKCanvas r, Camera camera)
    {
        if (previewSelection.IsEmpty) return;

        // only draw preview (which contains whole and current selection!)
        using var path = previewSelection.GetBoundaryPath();

        // specifically DON'T draw fill if it's deselecting and there's no previous selection (useless)
        if (!(!currentDeselectionRect.IsEmpty && !HasCommittedSelection))
        {
            // semi transparent fill
            SelectionFillPaint.Color = IsSelectionFloating ? Config.SelectionFloatingLayerColorFill : Config.SelectionColorFill;
            r.DrawPath(path, SelectionFillPaint);
        }

        DrawSelectionOutline(r, camera, path);

        // draw outline of deselection
        if (!currentDeselectionRect.IsEmpty)
        {
            using var deselRegion = new SKRegion(currentDeselectionRect);

            // the subregion of ACTUAL deselection when committing
            deselRegion.Op(CommittedSelection, SKRegionOperation.Intersect);

            if (!deselRegion.IsEmpty)
            {
                // apply deselection fillcolor
                using var deselFillPath = deselRegion.GetBoundaryPath();

                SelectionFillPaint.Color = Config.SelectionSubColorFill;
                r.DrawPath(deselFillPath, SelectionFillPaint);
            }

            DrawSelectionOutline(r, camera, currentDeselectionRect);
        }
        // the second condition makes it so the fill color is the default one (blue) instead of additional one (green)
        // in the case where it's the first selection and ctrl is being pressed (makes no sense visually, everything is being added anyways)
        else if (!currentAdditionalRect.IsEmpty && HasCommittedSelection)
        {
            using var addselRegion = new SKRegion(currentAdditionalRect);

            // the subregion of ACTUAL added selection when committing
            addselRegion.Op(CommittedSelection, SKRegionOperation.Difference);

            if (!addselRegion.IsEmpty)
            {
                // apply additional selection fillcolor
                using var addselFillPath = addselRegion.GetBoundaryPath();

                SelectionFillPaint.Color = Config.SelectionAddColorFill;
                r.DrawPath(addselFillPath, SelectionFillPaint);
            }

            DrawSelectionOutline(r, camera, currentAdditionalRect);
        }
    }

    // used in DrawSelection but also externally, e.g: in CanvasResizerTool.cs
    public void DrawSelectionOutline(SKCanvas r, Camera camera, SKPath path)
    {
        const float selectionVisualSize = 3f;
        const float dotsLength = 2f;

        float strokeWidth = selectionVisualSize / camera.CurrentZoom;
        float dotSize = strokeWidth * dotsLength;

        SelectionPaint.StrokeWidth = strokeWidth;

        _dashIntervals[0] = strokeWidth;
        _dashIntervals[1] = dotSize;

        using var dashEffect = SKPathEffect.CreateDash(_dashIntervals, dashOffset -= 0.02f * dotSize);

        // still outline
        SelectionPaint.PathEffect = null;
        SelectionPaint.Color = Config.SelectionColor1;
        r.DrawPath(path, SelectionPaint);

        // animated outline
        SelectionPaint.PathEffect = dashEffect;
        SelectionPaint.Color = Config.SelectionColor2;
        r.DrawPath(path, SelectionPaint);
    }

    // overload to convert SKRectI to a temporary SKPath
    public void DrawSelectionOutline(SKCanvas r, Camera camera, SKRectI rect)
    {
        using var path = new SKPath();
        path.AddRect(rect);
        DrawSelectionOutline(r, camera, path);
    }

    private SKRectI initialFloatingBounds; // to save bounds

    // manage floating selection (a separate layer)
    public void StartFloatingSelection()
    {
        if (!HasCommittedSelection || FloatingExists) return;

        var bounds = CommittedSelection.Bounds;
        initialFloatingBounds = bounds;
        FloatingX = bounds.Left;
        FloatingY = bounds.Top;

        // extract image
        FloatingBitmap = GetSelectionAsBitmap();

        // delete area on canvas!
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        paint.Color = SKColors.Transparent; // (default)
        
        using var path = CommittedSelection.GetBoundaryPath();
        CanvasCtx.DrawPath(path, paint);
    }

    // floating layer AND selection
    public void TranslateFloating(int dx, int dy)
    {
        if (!FloatingExists) return;

        // move floating layer
        FloatingX += dx;
        FloatingY += dy;

        // move selection
        TranslateSelection(dx, dy);
    }

    // ONLY selection
    public void TranslateSelection(int dx, int dy)
    {
        var bounds = CommittedSelection.Bounds;
        ClearAllSelection();
        
        var newBounds = new SKRectI(
            bounds.Left + dx, 
            bounds.Top + dy, 
            bounds.Right + dx, 
            bounds.Bottom + dy
        );
        UpdateSelection(newBounds);
        CommitSelection();
    }

    // to "commit" selection, when Esc is pressed
    public void MergeFloatingToMain()
    {
        if (!FloatingExists) return;

        // calc modified area
        var bounds = CommittedSelection.Bounds;
        var totalBounds = SKRectI.Union(initialFloatingBounds, bounds);

        int left = Math.Clamp(totalBounds.Left, 0, Width);
        int top = Math.Clamp(totalBounds.Top, 0, Height);
        int right = Math.Clamp(totalBounds.Right, 0, Width);
        int bottom = Math.Clamp(totalBounds.Bottom, 0, Height);

        var undoAction = new UndoMergeFloating(
            this,
            new SKRectI(left, top, right, bottom),
            FloatingBitmap!, // if FloatingExists, FloatingBitmap can't be null
            FloatingX,
            FloatingY,
            initialFloatingBounds
        );

        // final draw
        CanvasCtx.DrawBitmap(FloatingBitmap, FloatingX, FloatingY);

        // register undo/redo
        undoAction.PostUpdate(this);
        UndoManager.PushAction(undoAction);

        // free memory TODO make sure everything got disposed, in DragTool too
        FloatingBitmap?.Dispose();
        FloatingBitmap = null;
    }

    // to access private floating layer fields
    public void RestoreFloatingState(SKBitmap bitmap, int fx, int fy, SKRectI initialBounds, SKRegion selection)
    {
        FloatingBitmap?.Dispose();

        FloatingBitmap = bitmap;
        FloatingX = fx;
        FloatingY = fy;
        initialFloatingBounds = initialBounds;

        // restore selection
        ClearAllSelection();
        UpdateSelection(selection.Bounds);
        CommitSelection();
    }

    #endregion

    #region public methods for Keybinds.cs and such

    // a method to access to CanvasCtx
    public void DrawWithContext(Action<SKCanvas> drawAction)
        => drawAction(CanvasCtx);
    
    public void DrawPoint(SKPoint point, SKPaint paint)
        => CanvasCtx.DrawPoint(point, paint);

    public void DrawLine(SKPoint from, SKPoint to, SKPaint paint)
    {
        CanvasCtx.DrawLine(from, to, paint);
    }

    public void UpdateBrushStroke(SKPath path, SKPaint paint)
    {
        tempLayerPreviewPaint = null; // SrcOver
        TempCanvasCtx.Clear(SKColors.Transparent);

        // clip drawing to selection or everywhere if no selection
        if (HasCommittedSelection)
        {
            TempCanvasCtx.Save();
            TempCanvasCtx.ClipRegion(CommittedSelection);
        }

        TempCanvasCtx.DrawPath(path, paint);
        
        if (HasCommittedSelection)
            TempCanvasCtx.Restore();
    }

    public void UpdateEraserStroke(SKPath path, SKPaint paint)
    {
        tempLayerPreviewPaint ??= new SKPaint { BlendMode = SKBlendMode.DstOut };
        TempCanvasCtx.Clear(SKColors.Transparent);
        TempCanvasCtx.DrawPath(path, paint);
    }

    public void MergeTempToMain()
    {
        CanvasCtx.DrawBitmap(TempBitmap, 0, 0, tempLayerPreviewPaint);
        
        TempCanvasCtx.Clear(SKColors.Transparent);
        tempLayerPreviewPaint = null;
    }
    
    public void FillSelectionRegion(SKPaint paint)
        => CanvasCtx.DrawRegion(CommittedSelection, paint);
    
    private SKBitmap GetSelectionAsBitmap()
    {
        var bounds = CommittedSelection.Bounds;

        var result = new SKBitmap(bounds.Width, bounds.Height); // NOTE: MUST BE DISPOSED!
        using var canvas = new SKCanvas(result);

        using var path = CommittedSelection.GetBoundaryPath();
        path.Offset(-bounds.Left, -bounds.Top);

        // clip to include only the selection
        canvas.ClipPath(path);

        canvas.DrawBitmap(Bitmap, -bounds.Left, -bounds.Top);

        return result;
    }

    // general method to paste a SKBitmap into CanvasCtx
    private void PasteIntoCanvas(SKBitmap bitmap, int offsetX = 0, int offsetY = 0)
    {
        // get x y to paste image at left-top of current selection OR top-left of canvas
        int x = 0, y = 0;
        if (HasCommittedSelection)
        {
            x = CommittedSelection.Bounds.Left;
            y = CommittedSelection.Bounds.Top;
        }

        x += offsetX;
        y += offsetY;

        /*
        // draw bitmap
        CanvasCtx.DrawBitmap(bitmap, x, y);

        // select new area
        ClearAllSelection();

        var newBounds = new SKRectI(x, y, x + bitmap.Width, y + bitmap.Height);
        UpdateSelection(newBounds);
        CommitSelection();
        */

        // paste in floating layer instead of directly on canvas:
        if (FloatingExists)
            MergeFloatingToMain(); // commit previous
        
        FloatingBitmap = new SKBitmap(bitmap.Info);
        bitmap.CopyTo(FloatingBitmap);

        FloatingX = x;
        FloatingY = y;
        
        initialFloatingBounds = new SKRectI(x, y, x + bitmap.Width, y + bitmap.Height);

        ClearAllSelection();
        UpdateSelection(initialFloatingBounds);
        CommitSelection();
    }

    // CTRL+C: method to copy selection to clipboard
    public void CopySelToClipboard()
    {
        if (!HasCommittedSelection) return;

        var bitmap = GetSelectionAsBitmap();
        Utils.SetImageToClipboard(bitmap);

        bitmap.Dispose();
    }

    // CTRL+V: method to paste image from clipboard and select it
    public void PasteFromClipboard()
    {
        // if (!HasCommittedSelection) return; // pastes on top left of canvas if there is no selection

        using var pasted = Utils.GetImageFromClipboard();
        if (pasted == null) return;

        PasteIntoCanvas(pasted);
    }

    // CTRL+D: a method to duplicate selection area (basically a quick copy-paste without copying)
    public void DuplicateSelection()
    {
        var bitmap = GetSelectionAsBitmap();
        PasteIntoCanvas(bitmap, 0, 0);
        // NOTE: in some softwares there are offsets to make it clear it's duplicated,
        // i decided to implement the offset logic but keep it to 0, 0, for now

        bitmap.Dispose();
    }

    #endregion

    #region undo/redo methods

    // only used by undo/redo
    public void RestoreSelection(SKRegion savedRegion)
    {
        CommittedSelection.Dispose();
        CommittedSelection = new SKRegion(savedRegion);

        previewSelection.Dispose();
        previewSelection = new SKRegion(CommittedSelection);

        // currentDeselectionRect = currentAdditionalRect = SKRectI.Empty;
    }

    public void RegisterBrushUndo(SKPath brushPath, float brushSize)
    {
        brushPath.GetBounds(out SKRect pathBounds);

        float padding = brushSize / 2f + 2f; // +2 pixel for antialiasing
        var strokeRect = SKRect.Create(
            pathBounds.Left - padding,
            pathBounds.Top - padding,
            pathBounds.Width + (padding * 2),
            pathBounds.Height + (padding * 2)
        );

        // clip inside canvas
        int left = Math.Clamp((int)MathF.Floor(strokeRect.Left), 0, Width);
        int top = Math.Clamp((int)MathF.Floor(strokeRect.Top), 0, Height);
        int right = Math.Clamp((int)MathF.Ceiling(strokeRect.Right), 0, Width);
        int bottom = Math.Clamp((int)MathF.Ceiling(strokeRect.Bottom), 0, Height);

        var bounds = new SKRectI(left, top, right, bottom);

        if (HasCommittedSelection)
        {
            var selBounds = CommittedSelection.Bounds;

            // if bounds is touching selection bounds, clip bounds to selection
            // this avoids allocating a huge bitmap for the undo action
            if (bounds.IntersectsWith(selBounds))
                bounds.Intersect(selBounds);
            else
                return; // drawn completely outside
        }

        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            return; // don't even consider the action undoable if it's 0 pixels

        var action = new UndoBitmap(Bitmap, TempBitmap, bounds);
        UndoManager.PushAction(action);
    }

    // pencil is just like brush
    public void RegisterPencilUndo(SKPoint startPoint, SKPoint endPoint)
    {
        // the "+2 pixel for antialiasing" isn't necessary here

        int left = (int)MathF.Floor(Math.Min(startPoint.X, endPoint.X)) - 1;
        int top = (int)MathF.Floor(Math.Min(startPoint.Y, endPoint.Y)) - 1;
        int right = (int)MathF.Ceiling(Math.Max(startPoint.X, endPoint.X)) + 2;
        int bottom = (int)MathF.Ceiling(Math.Max(startPoint.Y, endPoint.Y)) + 2;

        left = Math.Clamp(left, 0, Width);
        top = Math.Clamp(top, 0, Height);
        right = Math.Clamp(right, 0, Width);
        bottom = Math.Clamp(bottom, 0, Height);

        var finalBounds = new SKRectI(left, top, right, bottom);
        if (finalBounds.Width <= 0 || finalBounds.Height <= 0)
            return;
    }

    public void SetBitmap(SKBitmap bitmap)
    {
        // reset
        CanvasCtx?.Dispose();
        Bitmap?.Dispose();

        // recreate
        Bitmap = bitmap;
        CanvasCtx = new SKCanvas(Bitmap);
        
        Width = Bitmap.Width;
        Height = Bitmap.Height;

        AspectRatio = (float)Width / Height; // recalc

        _tempCanvasCtx?.Dispose();
        _tempBitmap?.Dispose();

        _tempBitmap = null;
        _tempCanvasCtx = null;

        Canvas.UpdateGridPath(this);
    }

    #endregion

    public void Dispose()
    {
        Bitmap?.Dispose();
        CanvasCtx?.Dispose();

        _tempBitmap?.Dispose();
        _tempCanvasCtx?.Dispose();

        CommittedSelection?.Dispose();
        previewSelection?.Dispose();
    }
}