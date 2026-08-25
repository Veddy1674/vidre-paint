using SkiaSharp;
using Silk.NET.Input;
using Vidre.src.UI;
using Vidre.src.canvas;

namespace Vidre.src.input;

[Flags]
public enum Modifier : byte
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4
}

class InputManager : IDisposable
{
    private readonly AppContext AppContext;
    private Camera Camera => AppContext.Camera;
    private UIManager UIManager => AppContext.UIManager;
    private ToolManager ToolManager => AppContext.ToolManager;
    private Keybinds Keybinds => AppContext.Keybinds;

    private SKPoint lastScreenPos = SKPoint.Empty; // last mouse position on screen
    public SKPoint GetScreenPos() => lastScreenPos; // the exact same thing as lastScreenPos but public and fancy

    private bool isPanning = false; // holding middle mouse button

    public bool LeftBtnDown { get; private set; } = false; // holding left mouse button
    public bool RightBtnDown { get; private set; } = false; // holding right mouse button

    // manage ctrl, shift and alt down
    public Modifier Modifiers { get; private set; } = Modifier.None;

    // repeatable keys:
    private Key? pressingKey = null;
    private double keyTimer = 0;
    private bool isKeyRepeating = false;

    public IKeyboard MainKeyboard { get; private set; }
    public IMouse MainMouse { get; private set; }

    public InputManager(AppContext context, IInputContext input)
    {
        this.AppContext = context;

        this.MainKeyboard = input.Keyboards[0];
        this.MainMouse = input.Mice[0];
        
        MainKeyboard.KeyDown += OnKeyDown;
        MainKeyboard.KeyUp += OnKeyUp;
        MainKeyboard.KeyChar += OnKeyChar;

        MainMouse.MouseDown += OnMouseDown;
        MainMouse.MouseUp += OnMouseUp;
        MainMouse.MouseMove += OnMouseMove;
        MainMouse.Scroll += OnMouseScroll;
    }
    
    public void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        if (key is Key.ShiftLeft or Key.ShiftRight) Modifiers |= Modifier.Shift;
        if (key is Key.ControlLeft or Key.ControlRight) Modifiers |= Modifier.Ctrl; // TODO: add Mac support which uses Super
        if (key is Key.AltLeft or Key.AltRight) Modifiers |= Modifier.Alt;

        // global key to unfocus UIs
        if (key == Key.Escape)
        {
            // this could make sense but it's kind of annoying because you must select the tool again, esc is usually used just to unfocus the UI alone
            // toolManager.SetActiveTool(EnumTool.Drag); // the default "safe" tool that does nothing

            if (UIManager.OnFocusLost()) return; // if something was unfocused, return
            // otherwise nothing focused = continue, and NOTE that uiManager.OnKeyDown below
            // will always return false because it only fires the event to the focused window
        }

        // if a UI element is FOCUSED, don't affect canvas
        if (UIManager.OnKeyDown(keyboard, key, scancode, Modifiers)) return;

        // update canvas stuff
        ToolManager.OnModifier(Modifiers);

        // manage keybinds related to canvas
        if (Keybinds.TryGetAction(key, Modifiers, out var action))
        {
            // allow repeatable keys on specific actions:
            if (action
                is KeybindAction.UndoAction
                or KeybindAction.RedoAction
                or KeybindAction.MoveSelectionUp1px
                or KeybindAction.MoveSelectionDown1px
                or KeybindAction.MoveSelectionLeft1px
                or KeybindAction.MoveSelectionRight1px
                or KeybindAction.MoveSelectionUp10px
                or KeybindAction.MoveSelectionDown10px
                or KeybindAction.MoveSelectionLeft10px
                or KeybindAction.MoveSelectionRight10px
            )
            {
                pressingKey = key;
                keyTimer = 0;
                isKeyRepeating = false;
            }

            // execute first press right away
            Keybinds.ExecuteAction(action);
        }
    }

    public void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        if (key is Key.ShiftLeft or Key.ShiftRight) Modifiers &= ~Modifier.Shift;
        if (key is Key.ControlLeft or Key.ControlRight) Modifiers &= ~Modifier.Ctrl; // TODO: add Mac support which uses Super
        if (key is Key.AltLeft or Key.AltRight) Modifiers &= ~Modifier.Alt;

        // if a UI element is FOCUSED, don't affect canvas (although rare that a window uses OnKeyUp)
        if (UIManager.OnKeyUp(keyboard, key, scancode, Modifiers)) return;

        // update canvas stuff
        ToolManager.OnModifier(Modifiers);

        // toggle repeatable key
        if (pressingKey == key)
            pressingKey = null;
    }

    public void OnKeyChar(IKeyboard keyboard, char keyChar)
    {
        // OnKeyChar is pretty much only used for text input, not canvas
        UIManager.OnKeyChar(keyboard, keyChar);
    }

    public void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left) LeftBtnDown = true;
        if (button == MouseButton.Right) RightBtnDown = true;
        if (button == MouseButton.Middle) isPanning = true;

        // if a UI element is active, don't affect canvas
        if (UIManager.OnMouseDown(LeftBtnDown, RightBtnDown, lastScreenPos)) return;

        // update canvas
        var lastCanvasPos = Camera.ScreenToCanvasPos(lastScreenPos);
        ToolManager.OnMouseDown(LeftBtnDown, RightBtnDown, lastCanvasPos);

        // if the code reached here, it means the click is not on an UI, so unfocus any focused window
        UIManager.OnFocusLost();
    }

    public void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left) LeftBtnDown = false;
        if (button == MouseButton.Right) RightBtnDown = false;
        if (button == MouseButton.Middle) isPanning = false;

        // if a UI element is active, don't affect canvas
        if (UIManager.OnMouseUp(LeftBtnDown, RightBtnDown, lastScreenPos)) return;

        var lastCanvasPos = Camera.ScreenToCanvasPos(lastScreenPos);
        ToolManager.OnMouseUp(LeftBtnDown, RightBtnDown, lastCanvasPos);
    }

    public void OnMouseMove(IMouse mouse, System.Numerics.Vector2 pos)
    {
        // if a UI element is active, don't affect canvas
        if (UIManager.OnMouseMove(LeftBtnDown, RightBtnDown, lastScreenPos, pos))
        {
            lastScreenPos = pos;
            return;
        }

        // camera dragging
        if (isPanning)
        {
            // not using '-' operator because pos is Vector2 and lastScreenPos is SKPoint
            var delta = Utils.Subtract(pos, lastScreenPos);
            Camera.Move(Config.InvertedPanning ? delta.Negate() : delta);

            lastScreenPos = pos;
            return;
        }

        var canvasPos = Camera.ScreenToCanvasPos(pos);
        ToolManager.OnMouseMove(LeftBtnDown, RightBtnDown, canvasPos);

        lastScreenPos = pos;
    }

    public void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        float factor = wheel.Y > 0 ? 1.1f : (1f / 1.1f);
        Camera.SetZoom(factor, lastScreenPos);
    }

    public void OnDraw(SKCanvas r)
    {
        ToolManager.ActiveTool?.OnDraw(r, Camera.ScreenToCanvasPos(lastScreenPos));
    }

    // called from OnRender from VidreApp
    public void OnUpdate(double deltaTime)
    {
        // manage repeatable keys
        if (pressingKey != null)
        {
            keyTimer += deltaTime;

            double targetTime = isKeyRepeating ? Utils.KeyRepeatInterval : Utils.KeyRepeatDelay;
            
            if (keyTimer >= targetTime)
            {
                keyTimer = 0;
                isKeyRepeating = true;

                if (Keybinds.TryGetAction(pressingKey.Value, Modifiers, out var action))
                    Keybinds.ExecuteAction(action);
            }
        }
    }

    public void Dispose()
    {
        Keybinds.Dispose();
    }
}