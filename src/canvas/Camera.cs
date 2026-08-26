using SkiaSharp;

namespace Vidre.src.canvas;

class Camera(AppContext context)
{
    private readonly AppContext AppContext = context;
    
    public Canvas? Canvas => AppContext.ActiveCanvas;
    public SKMatrix CamMatrix { get; private set; } = SKMatrix.Identity;

    // for undoredo
    public void SetCamMatrix(SKMatrix matrix)
        => this.CamMatrix = matrix;

    public float CurrentZoom => CamMatrix.ScaleX;

    private const float MinZoom = 0.1f;
    private const float MaxZoom = 100f;

    public void SetZoom(float factor, SKPoint lastMousePos)
    {
        if (Canvas == null) return;

        float newScale = CurrentZoom * factor;
        if (newScale < MinZoom || newScale > MaxZoom) return; // clamp

        var scaleMatrix = SKMatrix.CreateScale(factor, factor, lastMousePos.X, lastMousePos.Y);
        CamMatrix = CamMatrix.PostConcat(scaleMatrix);
    }

    // no null check because it's only used in Canvas.cs itself
    public SKRect GetCanvasOnScreen()
        => CamMatrix.MapRect(new SKRect(0, 0, Canvas!.Width, Canvas.Height));
    
    public SKPoint ScreenToCanvasPos(SKPoint screenPos)
        => CamMatrix.Invert().MapPoint(screenPos).Floor(); // TODO optimize? e.g: save CamMatrix.Invert()
    
    public SKPoint CanvasToScreenPos(SKPoint canvasPos)
        => CamMatrix.MapPoint(canvasPos); // not heavy operation
    
    public SKPoint CanvasToScreenPos(float x, float y) // overload
        => CanvasToScreenPos(new SKPoint(x, y));

    public void Move(SKPoint delta)
    {
        if (Canvas == null) return;
        
        var translateMatrix = SKMatrix.CreateTranslation(delta.X, delta.Y);
        CamMatrix = CamMatrix.PostConcat(translateMatrix);
    }

    // center the camera
    public void Focus()
    {
        if (Canvas == null) return;

        var screen = AppContext.UIManager.Screen;
        
        // calc zoom (512 is an arbitrary value that looks decent for most canvas sizes)
        float scale = 512f / Math.Max(Canvas.Width, Canvas.Height);
        scale = Math.Clamp(scale, MinZoom, MaxZoom);

        // calc translation (centering)
        float offsetX = (screen.Width - Canvas.Width * scale) / 2f;
        float offsetY = (screen.Height - Canvas.Height * scale) / 2f;

        // apply translation and zoom
        CamMatrix = SKMatrix.CreateScaleTranslation(scale, scale, offsetX, offsetY);
    }
}