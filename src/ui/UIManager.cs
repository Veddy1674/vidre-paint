using SkiaSharp;
using Silk.NET.Input;
using Silk.NET.Maths;
using Vidre.src.input;
using Vidre.src.UI.windows;

namespace Vidre.src.UI;

class UIManager : IDisposable
{
    public SKRectI Screen { get; private set; }

    public readonly AppContext AppContext;
    
    private UITopBar UITopBar => AppContext.UITopBar; // implements IDisposable
    private UIStatusBar UIStatusBar => AppContext.UIStatusBar; // implements IDisposable

    // only floating windows such as Colors, Tools...
    private readonly List<FloatingWin> AllWindows = [];

    private FloatingWin? FocusedWin = null; // window that is being interacted with (e.g: typing in an input)

    private readonly SKPaint[] GlobalPaints; // a unique paint for each window
    
    public static readonly SKFont MainTextFont = new(SKTypeface.Default, 14);
    public static readonly SKPaint MainTextPaint = new() {
        Color = SKColors.White,
        IsAntialias = true
    };

    public UIManager(Vector2D<int> winSize, AppContext context)
    {
        this.Screen = new SKRectI(0, 0, winSize.X, winSize.Y);
        this.AppContext = context;
        
        // NOTE: windows are added here

        AllWindows.AddRange([
            new ColorsWin(Screen, context.ToolManager),
            new ToolsWin(Screen, context.ToolManager),
            // new TestInputWin(Screen));
        ]);

        // one SKPaint allocated for each window
        GlobalPaints = new SKPaint[AllWindows.Count];

        // initialize windows
        for (int i = 0; i < AllWindows.Count; i++)
        {
            // init paint
            GlobalPaints[i] = new SKPaint();

            // call init for each window
            AllWindows[i].Init(Screen, GlobalPaints[i]);
        }

        // initialize status bar
        UIStatusBar.CalcStatusBar(Screen);
    }

    public void DrawAll(SKCanvas r, double dt)
    {
        // draw top bar
        UITopBar.DrawTopBar(r);

        // draw all floating windows
        for (int i = 0; i < AllWindows.Count; i++)
            AllWindows[i].DrawAll(r, Screen, dt, GlobalPaints[i]);
        
        // draw top bar dropdowns on top of everything
        UITopBar.DrawDropdown(r);

        // draw status bar
        UIStatusBar.DrawStatusBar(r);
    }

    public void OnWinResize(int width, int height)
    {
        Screen = new SKRectI(0, 0, width, height);

        // recalc topBar
        UITopBar.CalcTopBar(Screen);

        // recalc status bar
        UIStatusBar.CalcStatusBar(Screen);

        // TODO recalc windows? - windows draw() have a boolean to check if they should recalc, but revise
    }

    #region Mouse events
    
    // when true is returned it means a UI element
    // is active, thus the canvas won't be affected by input

    // mousePos is in screen space
    public bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        // NOTE: the fact topBar is given priority here and not in OnMouseMove is intended (as in called first)
        // in OnMouseMove, specifically if you drag a floating window and move your mouse over the topBar,
        // its dropdown is shown even though the floating window is still being dragged, which is not ideal
        // instead here priority is given so that if you click on an item of a dropdown and there is a window behind
        // it gives priority to the dropdown item instead of the window behind (which makes sense as the dropdown is drawn on top)
        if (UITopBar.OnMouseDown(leftDown, rightDown, mousePos))
            return true;
        
        for (int i = AllWindows.Count - 1; i >= 0; i--)
        {
            var win = AllWindows[i];
            if (win.OnMouseDown(leftDown, rightDown, mousePos))
            {
                if (FocusedWin == win) return true; // already focused and on top, early return

                this.OnFocusLost(); // call method for the previously focused win
                FocusedWin = win;

                // take to front so it's drawn on top
                AllWindows.RemoveAt(i);
                AllWindows.Add(win);

                return true;
            }
        }

        if (UIStatusBar.OnMouseDown(leftDown, rightDown, mousePos))
            return true;
        
        return false;
    }

    public bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        // ONLY trigger focused window to avoid unwanted interactions, for
        // performance reasons and to make it so pressing "ESC" while dragging cancels the drag
        if (FocusedWin != null && FocusedWin.OnMouseUp(leftDown, rightDown, mousePos))
            return true;
        
        // no topBar mouseup event!

        if (UIStatusBar.OnMouseUp(leftDown, rightDown, mousePos))
            return true;
        
        return false;
    }

    public bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        // ONLY trigger focused window to avoid unwanted interactions, for
        // performance reasons and to make it so pressing "ESC" while dragging cancels the drag
        if (FocusedWin != null && FocusedWin.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos))
            return true;
        
        // call OnMouseMove for all the other windows without focus, so that animations play (mouse hover effects)
        if (!leftDown && !rightDown) // only if it's an actual mouse hover
            foreach (var win in AllWindows)
                if (win != FocusedWin)
                    if (win.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;
        
        if (UITopBar.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos))
            return true;
        
        if (UIStatusBar.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos))
            return true;

        return false;
    }

    #endregion

    // when ESC is pressed or mouseclicked outside any window (e.g: canvas)
    public bool OnFocusLost()
    {
        if (FocusedWin == null) return false;

        FocusedWin?.OnFocusLost();
        FocusedWin = null;
        
        return true;
    }

    public bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        // key cannot be ESC!

        // only trigger focused window
        if (FocusedWin != null && FocusedWin.OnKeyDown(keyboard, key, scancode, modifiers))
            return true;
        
        return false;
    }

    public void OnKeyChar(IKeyboard keyboard, char keyChar) // NOTE: could be bool but it's only used for text input?
    {
        // only trigger focused window
        FocusedWin?.OnKeyChar(keyboard, keyChar);
    }

    public bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        // only trigger focused window
        if (FocusedWin != null && FocusedWin.OnKeyUp(keyboard, key, scancode, modifiers))
            return true;
        
        return false;
    }

    public void Dispose()
    {
        foreach (var paint in GlobalPaints)
            paint.Dispose();

        MainTextFont.Dispose();
        MainTextPaint.Dispose();
        UITopBar.Dispose();
        UIStatusBar.Dispose();
    }
}