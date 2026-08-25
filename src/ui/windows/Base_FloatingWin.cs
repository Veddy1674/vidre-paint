using Silk.NET.Input;
using SkiaSharp;
using Vidre.src.input;

namespace Vidre.src.UI.windows;

abstract class FloatingWin
{
    private int X, Y;
    protected int Width, Height;

    private readonly string Title;
    private readonly bool CenterTitleHorizontally;

    public FloatingWin(SKRectI screen, string title, int x, int y, int width, int height, bool centerTitleHorizontally = false)
    {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
        this.Title = title;
        this.CenterTitleHorizontally = centerTitleHorizontally;

        UpdateScreenLimited(screen);

        // clamp window pos (emit warning?)
        if (x < ScreenLimited.Left) X = ScreenLimited.Left;
        if (y < ScreenLimited.Top) Y = ScreenLimited.Top;
    }

    protected SKRect ContentRect, HeaderRect;

    private bool dragging = false;
    private int dragOffsetX = 0, dragOffsetY = 0;

    private bool windowMoved = false; // true each frame the window is moved

    // not 100% reccomended to use, except in this class, to clamp (look OnMouseMove)
    protected SKRectI ScreenLimited { get; private set; }

    // TODO update on screen resize, not on draw
    private void UpdateScreenLimited(SKRect screen)
    {
        this.ScreenLimited = new SKRectI(
            (int)screen.Left + 6, 
            (int)screen.Top + (int)UITopBar.TopBarHeight + 6, // account for top bar to avoid overlap
            (int)screen.Right - Width - 6, 
            (int)screen.Bottom - Height - 6
        );
    }

    // consts
    private const float HeaderHeight = 25f;
    private const float ShadowOffset = 4f;
    private const float TitleSize = 14f;

    public abstract void Init(SKRect screen, SKPaint paint);

    // sealed override
    public void DrawAll(SKCanvas r, in SKRect screen, double deltaTime, SKPaint paint)
    {
        UpdateScreenLimited(screen);

        // note that ContentRect is used dynamically to draw shadow and such,
        // and only after this.DrawAll it becomes the actual content rect

        ContentRect = SKRect.Create(
            // 15 is left and bottom margin, 4f is shadow offset (then removed)
            x: X + ShadowOffset,
            y: Y + ShadowOffset,
            width: Width,
            height: Height
        );

        HeaderRect = new SKRect(
            left: ContentRect.Left - ShadowOffset,
            top: ContentRect.Top,
            right: ContentRect.Right - ShadowOffset,
            bottom: ContentRect.Top + HeaderHeight
        );

        // soft shadow
        r.DrawRoundRect(ContentRect, 6, 6, Utils.ShadowPaint);

        ContentRect.Offset(-ShadowOffset, -ShadowOffset); // remove shadow offset

        paint.IsAntialias = true;

        // container
        paint.Color = dragging ? Config.AppUIsBGColor_Highlight : Config.AppUIsBGColor;
        r.DrawRoundRect(ContentRect, 6, 6, paint);

        // header (drag area)
        paint.Color = dragging ? Config.AppUIsBGColor_Highlight : Config.AppUIsBGColor;
        r.DrawRect(HeaderRect, paint);

        paint.IsAntialias = false;

        // title on header
        UIManager.MainTextFont.Size = TitleSize;
        UIManager.MainTextPaint.Color = SKColors.White;
        
        if (CenterTitleHorizontally)
            r.DrawText(Title, HeaderRect.Left + (HeaderRect.Width / 2f), HeaderRect.Top + 18, SKTextAlign.Center, UIManager.MainTextFont, UIManager.MainTextPaint);
        else
            r.DrawText(Title, HeaderRect.Left + 10, HeaderRect.Top + 18, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);

        // content
        ContentRect.Top += HeaderHeight; // exclude header
        DrawContent(r, ContentRect, windowMoved, deltaTime, paint);

        windowMoved = false; // reset each draw frame
    }

    protected abstract void DrawContent(SKCanvas r, SKRect win, bool windowMoved, double deltaTime, SKPaint paint);

    public bool Contains(SKPoint mousePos)
    {
        return HeaderRect.Contains(mousePos) || ContentRect.Contains(mousePos);
    }

    public virtual bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (HeaderRect.Contains(mousePos) && leftDown)
        {
            dragging = true;

            dragOffsetX = (int)mousePos.X - X;
            dragOffsetY = (int)mousePos.Y - Y;

            return true;
        }

        // NOTE: this must be implemented in subclasses, NOT HERE!
        // otherwise all the floating windows' OnMouseDown won't work anymore due to early return
        // it should instead be the last thing to be checked (again, in the subclasses, not here)
        // if (WholeRect.Contains(mousePos))
        //     return true; // always return true if click is inside UI

        return false;
    }

    public virtual bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (!leftDown && dragging)
            dragging = false;
        
        return false;
    }

    public virtual bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        if (dragging)
        {
            // this method is NOT called every frame! only when (lastMousePos - mousePos).Length >= 1

            int newX = (int)Math.Clamp(mousePos.X - dragOffsetX, ScreenLimited.Left, ScreenLimited.Right);
            int newY = (int)Math.Clamp(mousePos.Y - dragOffsetY, ScreenLimited.Top, ScreenLimited.Bottom);

            windowMoved = true;
            // NOTE: this behavior makes windowMoved flick to true and false whenever the window is being dragged continuosly
            // this is because the rate at which this and the draw method are called is very different, it is visually okay,
            // but to be accurate, windowMoved could be set in the draw loop, although I rather keep this logic

            X = newX;
            Y = newY;

            return true;
        }

        // NOTE: this must be implemented in subclasses, NOT HERE!
        // otherwise all the floating windows' OnMouseDown won't work anymore due to early return
        // it should instead be the last thing to be checked (again, in the subclasses, not here)
        // if (HeaderRect.Contains(mousePos) || ContentRect.Contains(mousePos))
        //     return true;

        return false;
    }

    // only called if focused
    public virtual bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers) => false;
    public virtual bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers) => false;
    public virtual void OnKeyChar(IKeyboard keyboard, char c) {} // for text inputs, only called if focused

    public virtual void OnFocusLost()
    {
        dragging = false;
    }
}