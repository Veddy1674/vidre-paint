// unused (ONLY FloatingWin extended this class)

/*using SkiaSharp;
using Silk.NET.Input;
using Vidre.src.input;

namespace Vidre.src.UI.windows;

abstract class NormalWin
{
    public virtual void Init(SKRect screen, SKPaint paint) {}
    public abstract void DrawAll(SKCanvas r, in SKRect screen, double deltaTime, SKPaint paint);

    // when true is returned it means a UI element
    // is active, thus the canvas won't be affected by input

    // mousePos is in screen space
    public virtual bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos) => false;
    // NOTE: returning true in OnMouseUp is NOT reccomended (atleast in the case where it's just resetting values)
    // Because it might affect the canvas drawing by not calling OnMouseUp on the active tool
    public virtual bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos) => false;
    public virtual bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos) => false;

    // only called if focused
    public virtual bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers) => false;
    public virtual bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers) => false;
    public virtual void OnKeyChar(IKeyboard keyboard, char c) {} // for text inputs, only called if focused

    // when another window is focused or ESC
    public virtual void OnFocusLost() {}
}*/