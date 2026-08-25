using SkiaSharp;
using Silk.NET.Input;
using Vidre.src.input;

namespace Vidre.src.UI.components;

enum TextAlignment { Left, Center, Right }

class UITextInput : UIComponent
{
    private readonly float fontSize;
    private readonly int maxTextLength;
    private readonly TextAlignment textAlignment;

    public UITextInput(float fontSize = 14f, string initText = "", int maxTextLength = 10, TextAlignment textAlignment = TextAlignment.Left)
    {
        this.fontSize = fontSize;
        this.maxTextLength = maxTextLength;
        this.textAlignment = textAlignment;

        CurrText = initText;

        UpdateTextCache(); // must be called everytime CurrText changes
    }

    private SKRect Container; // input is considered inside this rect, must be updated every frame

    // using a method just to be clear
    public void UpdateContainer(SKRect rect)
        => Container = rect;

    public string CurrText { get; private set; }

    private int anchor = -1; // index (-1 = not selecting)
    private int caret = -1;

    private bool TextFocused => caret != -1;
    private bool HasSelection => anchor != caret && TextFocused;
    private int SelectionStart => Math.Min(anchor, caret);
    private int SelectionEnd => Math.Max(anchor, caret);

    private bool isSelecting = false; // aka isDragging/isLeftDown, true on mouse down and false on mouse up

    // callbacks:
    public Func<char, char> AdjustKeyChar { get; set; } = (_) => _; // e.g: force char to their uppercase variant
    public Func<char, bool> KeyTypeCondition { get; set; } = (_) => true; // e.g: only allow numbers (called in OnKeyChar)
    // NOTE: AdjustKeyChar is called BEFORE KeyTypeCondition
    public Func<string, string> OnEnter { get; set; } = (_) => _; // edit CurrText when Enter is pressed
    public Func<string, string> OnDeleteAll { get; set; } = (_) => _; // edit CurrText when CurrText.Length == 0

    public Action OnFocusLostAction { get; set; } = () => {}; // additional operations when focus is lost
    public Action OnKeyCharAction { get; set; } = () => {}; // additional operations when typing a key
    public Action OnDeleteAction { get; set; } = () => {}; // additional operations when backspace or canc are pressed
    public Action OnTabPress { get; set; } = () => {}; // what to do when Tab is pressed (repeatable key)
    public Action OnTextExceed { get; set; } = () => {}; // what to do when a character is typed but text chars limit is reached

    // for blinking cursor effect:
    private double blinkTimer = 0;
    const float blinkInterval = 0.6f; // for how much cursor is visible/invisible before "blinking"
    const float blinkInterval2 = blinkInterval * 2;

    // for "double click = select all" feature:
    private double lastClickTime = 0; // time of last left click
    private int lastClickIndex = -1; // last cursor position (index)
    private const double doubleClickThreshold = 0.4; // max time between clicks to be considered a double click

    // cached values for drawing (usually MeasureText related)
    private float cachedCursorX = 0; // offset
    private float cachedSelX = 0; // offset
    private float cachedSelWidth = 0;
    private float cachedTextWidth = 0;

    private double keyTimer = 0; // for key repeat (backspace, canc, arrows...)
    private Key? pressingKey = null;
    private bool isKeyRepeating = false;

    // HandleRepeatableKeys() is called by Draw(), so it's necessary a 'cache' of these; set by OnKeyDown/Up
    private bool globalOnShiftDown = false;
    private bool globalOnCtrlDown = false;

    public override void Compute(SKRect win) {}

    public override void Draw(SKCanvas r, double deltaTime, SKPaint paint)
    {
        var font = UIManager.MainTextFont;
        var metrics = font.Metrics;
        font.Size = fontSize;

        var x = GetTextX();
        var y = GetTextY(metrics); // always centered vertically

        // draw text selection (if exists)
        if (HasSelection)
        {
            var selRect = SKRect.Create(x + cachedSelX, y + metrics.Ascent, cachedSelWidth, metrics.Descent - metrics.Ascent);

            paint.Color = Config.TextSelectionColor;
            r.DrawRect(selRect, paint);
        }

        // draw text (on top of selection, no alpha needed)
        UIManager.MainTextPaint.Color = SKColors.White;
        r.DrawText(CurrText, x, y, SKTextAlign.Left, font, UIManager.MainTextPaint);

        // draw blinking cursor (only when focused and no selection, or while selecting)
        if (TextFocused && (isSelecting || !HasSelection))
        {
            blinkTimer += deltaTime;
            
            // as an example blinkInterval = 1f:
            // from 0 to 1 cursor is visible, from 1 to 2 cursor is invisible, then repeat
            if (blinkTimer <= blinkInterval)
            {
                // draw line "cursor"
                paint.Color = SKColors.White;
                r.DrawLine(x + cachedCursorX, y + metrics.Ascent, x + cachedCursorX, y + metrics.Descent, paint);
                
                // (from 1 to 2 do nothing)

            } else if (blinkTimer >= blinkInterval2)
            {
                blinkTimer = 0; // reset timer
            }
        }

        // update keyTimer
        if (pressingKey != null)
        {
            keyTimer += deltaTime;

            // keyTimer goes from 0 to keyRepeatDelay (initial delay)
            // then is set to 0 and starts repeating every keyRepeatInterval
            if (keyTimer >= (isKeyRepeating ? Utils.KeyRepeatInterval : Utils.KeyRepeatDelay))
            {
                keyTimer = 0;
                isKeyRepeating = true;

                HandleRepeatableKeys(pressingKey.Value);
            }
        }
    }

    private void SetCaret(int pos, bool extendSelection)
    {
        caret = Math.Clamp(pos, 0, CurrText.Length);

        if (!extendSelection)
            anchor = caret;
        
        UpdateSelectionCache();
        blinkTimer = 0;
    }
 
    private void DeleteSelection(bool updateCache = true)
    {
        int start = SelectionStart;
        CurrText = CurrText.Remove(start, SelectionEnd - start);
        anchor = caret = start;

        if (!updateCache) return;

        UpdateSelectionCache();
        UpdateTextCache();
    }

    private void HandleRepeatableKeys(Key key)
    {
        switch (key)
        {
            case Key.Backspace:
            
                if (HasSelection)
                    DeleteSelection();
    
                // remove char BEFORE cursor
                else if (caret > 0)
                {
                    CurrText = CurrText.Remove(caret - 1, 1);

                    SetCaret(caret - 1, extendSelection: false); // shift + backspace = backspace

                    UpdateSelectionCache(); // because caret was edited
                    UpdateTextCache(); // because CurrText was edited
                }

                this.OnDeleteAction();

                break;
 
            case Key.Delete:
            
                if (HasSelection)
                    DeleteSelection();
                
                // remove char AFTER cursor
                else if (caret < CurrText.Length)
                {
                    CurrText = CurrText.Remove(caret, 1);

                    UpdateSelectionCache();
                    UpdateTextCache();

                    // (don't update caret because it stays still)
                }

                this.OnDeleteAction();

                break;
 
            case Key.Left:

                if (globalOnCtrlDown)
                    // NOTE: this directly jumps to the start of the whole text,
                    // while usually it jumps to the start of the current word that the caret is on
                    SetCaret(0, extendSelection: globalOnShiftDown);
                
                else if (!globalOnShiftDown && HasSelection)
                    SetCaret(SelectionStart, extendSelection: false); // set caret at the left of the selection
                else
                    // simple left press
                    SetCaret(caret - 1, extendSelection: globalOnShiftDown);
                
                break;
 
            case Key.Right:

                if (globalOnCtrlDown)
                    // NOTE: this directly jumps to the end of the whole text,
                    // while usually it jumps to the end of the current word that the caret is on
                    SetCaret(CurrText.Length, extendSelection: globalOnShiftDown);

                else if (!globalOnShiftDown && HasSelection)
                    SetCaret(SelectionEnd, extendSelection: false); // set caret at the right of the selection
                else
                    // simple right press
                    SetCaret(caret + 1, extendSelection: globalOnShiftDown);
                
                break;
        }
        
        blinkTimer = 0;
    }

    // public methods:

    // returns success (whether the text was changed from old)
    public bool SetText(string text, bool loseFocus = true)
    {
        if (CurrText == text) return false;

        if (text.Length > maxTextLength)
        {
            Console.Error.WriteLine($"UITextInput.SetText: '{text}' exceeds maxLength {maxTextLength}");
            return false; // set truncated text as fallback?
        }

        CurrText = text;

        // by default, set caret at the end of the text to avoid exceptions
        anchor = caret = CurrText.Length;

        if (loseFocus)
            OnFocusLost(); // simulate focus lost so that the cursor and selection disappear
        
        UpdateSelectionCache();
        UpdateTextCache();
        return true;
    }

    private void GainFocus(int clickedIdx, double now)
    {
        anchor = caret = clickedIdx;

        lastClickTime = now;
        lastClickIndex = clickedIdx;
    }

    // public only method that can be used to force focus without OnMouseDown
    public void GainFocus()
    {
        GainFocus(CurrText.Length, 0);

        UpdateSelectionCache();
        blinkTimer = 0;
    }

    // update cache related to text selection
    private void UpdateSelectionCache()
    {
        // avoid AsSpan which could cause ArgumentOutOfRangeException
        if (!TextFocused)
        {
            cachedCursorX = cachedSelX = cachedSelWidth = 0;
            return;
        }

        var font = UIManager.MainTextFont;
        font.Size = fontSize;

        cachedCursorX = font.MeasureText(CurrText.AsSpan(0, caret));
        cachedSelX = font.MeasureText(CurrText.AsSpan(0, SelectionStart));
        cachedSelWidth = font.MeasureText(CurrText.AsSpan(SelectionStart, SelectionEnd - SelectionStart));
    }

    // update cache related to text in general (so it should be called in Init)
    private void UpdateTextCache()
    {
        if (CurrText.Length == 0)
        {
            CurrText = this.OnDeleteAll(CurrText);
            SetCaret(CurrText.Length, extendSelection: false); // update caret to the end
        }

        var font = UIManager.MainTextFont;
        font.Size = fontSize;
        
        cachedTextWidth = font.MeasureText(CurrText);
    }

    private const float xOffset = 5; // x distance from container sides (when textAlignment is left or right)

    private float GetTextX()
        => textAlignment switch
        {
            TextAlignment.Center => Container.MidX - cachedTextWidth / 2f,
            TextAlignment.Right => Container.Right - xOffset - cachedTextWidth, // unlike left, right considers textWidth
            _ => Container.Left + xOffset
        };

    private float GetTextY(SKFontMetrics metrics)
        => Container.MidY - (metrics.Ascent + metrics.Descent) / 2;

    // return the index in the text based on mouse position (x only)
    private int GetIndexFromMouse(float relX)
    {
        var font = UIManager.MainTextFont;
        font.Size = fontSize;

        float currentX = 0;
        int i;
        for (i = 0; i < CurrText.Length; i++)
        {
            float charWidth = font.MeasureText(CurrText.AsSpan(i, 1));
            if (relX < currentX + (charWidth / 2)) break;

            currentX += charWidth;
        }
        return i;
    }

    private float GetRelX(float absX)
        => absX - GetTextX(); // same offset as in Draw

    #region Mouse events

    public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (!leftDown || !Container.Contains(mousePos))
        {
            if (TextFocused)
                OnFocusLost();
            return false;
        }
        
        int idx = GetIndexFromMouse(GetRelX(mousePos.X));

        double now = VidreApp.TotalElapsedTime;
        bool isDoubleClick = (now - lastClickTime <= doubleClickThreshold) && (idx == lastClickIndex);
        // second condition is to make sure the mouse is still during double click

        if (isDoubleClick)
        {
            isSelecting = false;
            anchor = 0;
            caret = CurrText.Length;

            lastClickTime = 0; // prevent triple click
        }
        else // single click
        {
            isSelecting = true;
            GainFocus(idx, now);

            // basically:
            // isSelecting = true;
            // anchor = caret = idx;

            // lastClickTime = now;
            // lastClickIndex = idx;
        }

        UpdateSelectionCache();
        blinkTimer = 0;

        return true;
    }

    public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        isSelecting = false;
        return false;
    }

    public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        if (!isSelecting) return false;

        int idx = GetIndexFromMouse(GetRelX(mousePos.X));

        if (idx == caret) // nothing happens
            return true;
 
        caret = idx;
        UpdateSelectionCache();

        return true;
    }

    #endregion

    #region Keyboard events

    public override bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        globalOnShiftDown = modifiers.HasFlag(Modifier.Shift);
        globalOnCtrlDown = modifiers.HasFlag(Modifier.Ctrl);

        // window might be focused but text input might not be (e.g: you click on the UI parent but not the text)
        if (!TextFocused) return false;

        // handling keybinds NOTE: cannot be changed unlike canvas/app keybinds
        switch(key)
        {
            // TODO find a way to not have to add both here and to HandleRepeatableKeys everytime a new special key is added

            case Key.Backspace or Key.Delete or Key.Left or Key.Right: // special keys
                this.pressingKey = key;

                keyTimer = 0;
                isKeyRepeating = false; // reset
                
                // globalOnShift/CtrlDown are used instead of passing the variables directly
                // (because this method is also called by Draw(), which has no access to the variables)
                HandleRepeatableKeys(key); // run first press right away

                break;

            case Key.Enter:
                OnFocusLost();

                // UpdateSelectionCache(); // not necessary because SetText below already does it!

                // adjust current text if necessary
                if (!this.SetText(OnEnter.Invoke(CurrText), loseFocus: false))
                {
                    // if not success (text didn't change), update selection cache manually
                    UpdateSelectionCache();
                    // of course not TextCache because text didn't change
                }

                break;
            
            case Key.Home:
                SetCaret(0, extendSelection: globalOnShiftDown);
                break;

            case Key.End:
                SetCaret(CurrText.Length, extendSelection: globalOnShiftDown);
                break;
            
            case Key.Tab:
                this.OnTabPress();
                // blinkTimer to 0?
                break;
            
            case Key.A when globalOnCtrlDown: // select all text
                anchor = 0;
                caret = CurrText.Length;

                UpdateSelectionCache();
                blinkTimer = 0;

                break;
            
            case Key.C when globalOnCtrlDown: // copy selection to clipboard

                if (HasSelection)
                    keyboard.ClipboardText = CurrText[SelectionStart..SelectionEnd];

                break;
            
            case Key.X when globalOnCtrlDown: // copy selection to clipboard and delete it

                if (HasSelection)
                {
                    keyboard.ClipboardText = CurrText[SelectionStart..SelectionEnd];
                    DeleteSelection();
                }

                break;
            
            // NOTE: in most softwares CTRL+V is a repeatable action (should be in HandleRepeatableKeys),
            // but here, to avoid complications, it's not implemented
            case Key.V when globalOnCtrlDown: // paste selection from clipboard

                // if empty or made up by spaces return: NOTE that this is not always the case in softwares and OS
                // for example Windows and Linux allows copying and pasting empty spaces

                var clipboard = keyboard.ClipboardText;
                if (clipboard.IsWhiteSpace()) return false;

                // delete selection if exists
                if (HasSelection)
                    DeleteSelection(updateCache: false);
                
                // insert char by char and apply conditions to each (like OnKeyChar)
                foreach (char ch in clipboard)
                {
                    // attempted to insert more chars than allowed
                    if (CurrText.Length >= maxTextLength)
                    {
                        this.OnTextExceed();
                        break;
                    }

                    char adjusted = AdjustKeyChar(ch);
                    if (!KeyTypeCondition(adjusted)) continue;

                    CurrText = CurrText.Insert(caret, adjusted.ToString());
                    caret++;
                }

                anchor = caret;
                OnKeyCharAction(/*this*/); // call event once after whole paste

                UpdateSelectionCache();
                UpdateTextCache();
                blinkTimer = 0;

                break;
            
            // return true even when default, just in case, to avoid canvas being affected
            // edit: this might actually be necessary, 
        }

        return true;
    }

    public override bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        globalOnShiftDown = modifiers.HasFlag(Modifier.Shift);
        globalOnCtrlDown = modifiers.HasFlag(Modifier.Ctrl);

        if (pressingKey == key)
        {
            pressingKey = null;
            return true; // doesn't really matter true or false
        }

        return false;
    }

    public override void OnKeyChar(IKeyboard keyboard, char c)
    {
        if (!TextFocused) return;

        // if no selection and text limit exceeded, call OnTextExceed and return
        // check "if no selection" because it gets deleted, so there
        // smight be space if text limit is exceeded but there is a selection
        if (!HasSelection && CurrText.Length >= maxTextLength)
        {
            // attempted to insert more chars than allowed
            this.OnTextExceed();
            return;
        }

        // a copy, not reference!
        c = this.AdjustKeyChar(c); // adjust e.g: make uppercase/lowercase

        // check optional condition
        if (!this.KeyTypeCondition(c)) return; // e.g: only allow numbers

        if (HasSelection) // to replace selection
            DeleteSelection(updateCache: false); // NOTE: cache will be updated ONCE below

        // insert character and move cursor
        CurrText = CurrText.Insert(caret, c.ToString());
        SetCaret(caret + 1, extendSelection: false); // set caret after insertion

        this.OnKeyCharAction(/*this*/); // run optional action

        UpdateSelectionCache(); // TODO avoid calling this more than one time
        UpdateTextCache();
        blinkTimer = 0;
    }

    #endregion

    public override void OnFocusLost()
    {
        // reset selection
        anchor = caret = -1;
        isSelecting = false;

        // reset key repeat state
        pressingKey = null;
        isKeyRepeating = false;
        keyTimer = 0;

        this.OnFocusLostAction(/*this*/);
    }
}