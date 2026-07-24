using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src.input.tools;

abstract class DrawTool(ToolManager toolManager) : IDisposable
{
    protected readonly ToolManager toolManager = toolManager;
    
    public virtual void OnDown(Canvas canvas, SKPoint point, SKColor color) {} // color refers to what color paint should have (if it's a drawing tool)
    public virtual void OnUp(Canvas canvas, SKPoint point) {}
    public virtual void OnMove(Canvas canvas, SKPoint end) {}

    // called when shift, ctrl or alt key states change
    public virtual void OnModifier(Canvas canvas, Modifier modifiers) {}

    // called every frame when mouse is NOT over any UI
    public virtual void OnDraw(SKCanvas r, SKPoint canvasPos) {}
    
    public virtual void Dispose() {} // must be implemented if a subclass has a SKPaint or whatever needs disposal
}