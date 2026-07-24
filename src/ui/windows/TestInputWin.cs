using Silk.NET.Input;
using SkiaSharp;
using Vidre.src.input;
using Vidre.src.UI.components;

namespace Vidre.src.UI.windows;

class TestInputWin(SKRectI screen) : FloatingWin(screen,
    "Colors",
    x: screen.Width / 2,
    y: screen.Height / 2,
    width: 200,
    height: 200
)
{
    private UIComponent[] UIComponents = [];

    public override void Init(SKRect _screen, SKPaint _paint)
    {
        // init here components (so that "this" is allowed)
        UIComponents = [
            // order matters for priority

            new TestInput(20, 30, 81, 20, initText: "FFFFFF"),
        ];

        foreach (var ui in UIComponents)
            ui.Init(_screen, _paint);
    }

    private bool firstDraw = true;

    protected override void DrawContent(SKCanvas r, SKRect win, bool windowMoved, double deltaTime, SKPaint paint)
    {
        if (windowMoved || firstDraw)
        {
            firstDraw = false;

            foreach (var ui in UIComponents)
                ui.Compute(win);
        }

        foreach (var ui in UIComponents)
            ui.Draw(r, deltaTime, paint);
    }

    private class TestInput(float x, float y, float width, float height, string initText) : UIComponent
    {
        private static readonly SKColor inputBoxColor = new(41, 41, 41);
        private static readonly SKColor shadowColor = new(65, 65, 65); // used for input box and sliders

        private SKRect wholeRect, textHitbox;
        private readonly UITextInput textInput = new(14f, initText, textAlignment: TextAlignment.Center);

        public override void Compute(SKRect win)
        {
            wholeRect = textHitbox = SKRect.Create(win.Left + x, win.Top + y, width, height);
            textHitbox.Left += 12; // x offset of text (so it's on the right of the unselectable "#" character)

            textInput.UpdateContainer(textHitbox);
        }

        public override void Draw(SKCanvas r, double deltaTime, SKPaint paint)
        {
            // draw rect
            paint.IsAntialias = true;

            // shadow
            paint.Color = shadowColor;
            wholeRect.DrawShadowRect(r, paint, 0, 0, 2f);

            // input box
            paint.Color = inputBoxColor;
            
            // if (rx > 0 || ry > 0)
            //     r.DrawRoundRect(wholeRect, 0, 0, paint);
            // else
                r.DrawRect(wholeRect, paint);

            paint.IsAntialias = false;

            // draw unselectable "#" before text input
            var y = wholeRect.MidY - (UIManager.MainTextFont.Metrics.Ascent + UIManager.MainTextFont.Metrics.Descent) / 2; // centered
            r.DrawText("#", wholeRect.Left + 5, y, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);

            // draw text on rect
            textInput.Draw(r, deltaTime, paint);
        }

        public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            if (textInput.OnMouseDown(leftDown, rightDown, mousePos)) return true;

            return false;
        }

        public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            if (textInput.OnMouseUp(leftDown, rightDown, mousePos)) return true;
            return false;
        }

        public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
        {
            if (textInput.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;
            
            return false;
        }

        public override bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
        {
            if (textInput.OnKeyDown(keyboard, key, scancode, modifiers)) return true;
            return false;
        }

        public override bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
        {
            if (textInput.OnKeyUp(keyboard, key, scancode, modifiers)) return true;
            return false;
        }

        public override void OnKeyChar(IKeyboard keyboard, char c)
        {
            textInput.OnKeyChar(keyboard, c);
        }

        public override void OnFocusLost()
        {
            textInput.OnFocusLost();
        }
    }

    public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (base.OnMouseDown(leftDown, rightDown, mousePos)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnMouseDown(leftDown, rightDown, mousePos)) return true;

        if (base.ContentRect.Contains(mousePos))
            return true; // always return true if click is inside UI
        
        return false;
    }

    public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (base.OnMouseUp(leftDown, rightDown, mousePos)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnMouseUp(leftDown, rightDown, mousePos)) return true;

        return false;
    }

    public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        if (base.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;

        return false;
    }

    public override bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        // FloatingWindow doesn't use OnKeyDown at all as for now
        // if (base.OnKeyDown(key, scancode)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnKeyDown(keyboard, key, scancode, modifiers)) return true;

        return false;
    }

    public override bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        // FloatingWindow doesn't use OnKeyUp at all as for now
        // if (base.OnKeyUp(key, scancode)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnKeyUp(keyboard, key, scancode, modifiers)) return true;

        return false;
    }

    public override void OnKeyChar(IKeyboard keyboard, char c)
    {
        // FloatingWindow doesn't use OnKeyChar at all as for now
        // base.OnKeyChar(c);

        foreach (var ui in UIComponents)
            ui.OnKeyChar(keyboard, c);
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();

        // manage ui components event
        foreach (var ui in UIComponents)
            ui.OnFocusLost();
    }
}