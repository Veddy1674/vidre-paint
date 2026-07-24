using SkiaSharp;
using Silk.NET.Input;
using Vidre.src.input;

namespace Vidre.src.UI.components;

abstract class UIComponent
{
    public virtual void Init(SKRect screen, SKPaint paint) {}
    
    // re-calc position, size etc... (e.g: when window is moved)
    public abstract void Compute(SKRect win);

    public abstract void Draw(SKCanvas r, double deltaTime, SKPaint paint);

    public virtual bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos) { return false; }
    public virtual bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos) { return false; }
    public virtual bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos) { return false; }

    // only called if parent window is focused
    public virtual bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers) => false;
    public virtual bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers) => false;
    public virtual void OnKeyChar(IKeyboard keyboard, char c) {} // for text inputs, only called if parent window is focused

    // when another window is focused or ESC
    public virtual void OnFocusLost() {}
}