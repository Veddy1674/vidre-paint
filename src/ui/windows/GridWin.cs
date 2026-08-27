using Silk.NET.Input;
using SkiaSharp;
using Vidre.src.canvas;
using Vidre.src.input;
using Vidre.src.UI.components;

namespace Vidre.src.UI.windows;

class GridWin(SKRectI screen, AppContext appContext) : FloatingWin(screen,
    "Grid Settings",
    x: screen.Right - 200 - 10,
    y: screen.Bottom - 100 - 50 - 10,
    width: 200,
    height: 100
)
{
    private readonly AppContext AppContext = appContext;
    private UIComponent[] UIComponents = [];

    public override void Init(SKRect _screen, SKPaint _paint)
    {
        // init here components (so that "this" is allowed)
        UIComponents = [
            new GridPaddingInput(100, 38, 50, 20, initText: Canvas.GridPadding.ToString(), AppContext),
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

        // Draw "Show Grid" text
        UIManager.MainTextFont.Size = 14f;
        UIManager.MainTextPaint.Color = SKColors.White;
        r.DrawText("Show Grid", win.Left + 10, win.Top + 28, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);

        // Draw "Grid Padding" text
        r.DrawText("Grid Padding", win.Left + 10, win.Top + 52, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);

        foreach (var ui in UIComponents)
            ui.Draw(r, deltaTime, paint);
    }

    private class GridPaddingInput : UIComponent
    {
        private static readonly SKColor inputBoxColor = new(41, 41, 41);
        private static readonly SKColor shadowColor = new(65, 65, 65);

        private readonly float x, y, width, height;
        private SKRect wholeRect;
        private readonly UITextInput textInput;
        private readonly AppContext appContext;

        public GridPaddingInput(float x, float y, float width, float height, string initText, AppContext appContext)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.appContext = appContext;

            textInput = new UITextInput(14f, initText, maxTextLength: 4, textAlignment: TextAlignment.Center)
            {
                KeyTypeCondition = char.IsDigit,
                OnKeyCharAction = SetAndClampValue
            };

            textInput.OnDeleteAction = () =>
            {
                if (string.IsNullOrWhiteSpace(textInput.CurrText) || textInput.CurrText == "0")
                {
                    // set to minimum
                    textInput.SetText("1", loseFocus: false);
                    UpdateGrid(1);
                }

                SetAndClampValue();
            };

            textInput.OnTextExceed = () =>
            {
                // force current text to max value
                textInput.SetText("4096", loseFocus: false);
                UpdateGrid(4096);
            };
        }

        // update Canvas.GridPadding when text changes
        private void SetAndClampValue()
        {
            if (int.TryParse(textInput.CurrText, out int value))
            {
                value = Math.Clamp(value, 1, 4096); // just in case
                UpdateGrid(value);
            }
        }

        private void UpdateGrid(int padding)
        {
            Canvas.GridPadding = padding;

            if (Canvas.ShowGrid && appContext.ActiveCanvas != null)
                Canvas.UpdateGridPath(appContext.ActiveCanvas);
        }

        public override void Compute(SKRect win)
        {
            wholeRect = SKRect.Create(win.Left + x, win.Top + y, width, height);
            textInput.UpdateContainer(wholeRect);
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
            r.DrawRect(wholeRect, paint);

            paint.IsAntialias = false;

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
            return true;

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
        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnKeyDown(keyboard, key, scancode, modifiers)) return true;

        return false;
    }

    public override bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnKeyUp(keyboard, key, scancode, modifiers)) return true;

        return false;
    }

    public override void OnKeyChar(IKeyboard keyboard, char c)
    {
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
