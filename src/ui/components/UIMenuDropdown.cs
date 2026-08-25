using System.Diagnostics;
using SkiaSharp;
using Vidre.src;
using Vidre.src.input;
using Vidre.src.UI;

class UIMenuDropdown // sorta of UIComponent
{
    private readonly Keybinds keybinds;

    private readonly SKBitmap[] itemsIcon;
    private readonly string[] itemsDesc;
    private readonly KeybindAction[] itemsAction;
    private readonly string[] itemsShortcut;

    private readonly bool hasSubDropdowns = false;
    
    public UIMenuDropdown(Keybinds keybinds, in (string, string, KeybindAction?)[] itemsIconAndDescAndAction)
    {
        this.keybinds = keybinds;
        
        int len = itemsIconAndDescAndAction.Length;
        this.itemRects = new SKRect[len];

        this.itemsIcon = new SKBitmap[len];
        this.itemsDesc = new string[len];
        this.itemsAction = new KeybindAction[len];
        this.itemsShortcut = new string[len];

        // init arrays
        for (int i = 0; i < len; i++)
        {
            this.itemsIcon[i] = Utils.LoadImage(itemsIconAndDescAndAction[i].Item1, useSlashes: true);
            this.itemsDesc[i] = itemsIconAndDescAndAction[i].Item2;

            var action = itemsIconAndDescAndAction[i].Item3;

            this.itemsAction[i] = action ?? KeybindAction.DoNothing;
            this.SetShortcutFor(i, keybinds.GetKeybind(action)); // GetKeybind returns null if action is null

            if (action == null)
            {
                // if null, it has sub dropdowns
                this.hasSubDropdowns = true;
            }
        }
    }

    // must be called at the beginning and on shortcut change
    public void SetShortcutFor(int index, KeybindKey? keybind)
    {
        itemsShortcut[index] = keybind?.ToString() ?? "";
    }

    public void AddSubDropdowns(int index)
    {
        if (!hasSubDropdowns)
        {
            Debug.WriteLine($"AddSubDropdowns(index: {index}, ...) was called but this dropdown doesn't allow sub dropdowns (action set to this index should be explicitly null, found {itemsAction[index]})");
            return;
        }
        // TODO manage multiple dropdown chain?

    }

    private SKRect dropdownRect;
    private readonly SKRect[] itemRects;
    private int selectedItemIndex = -1;
    
    private bool isVisible = false;

    private const float verticalPadding = 8f; // empty space on top and bottom
    private const float horizontalPadding = 8f; // empty space on left and right
    private const float shortcutWidth = 100f; // distance between desc text and shortcut text, must be a one-size-fits-all
    private const float distToIcon = 8f; // distance between icon and text
    private const float itemHeight = 24f; // NOTE: must be exact size of both width and height of icons!
    private const float textSize = 16f; // of desc

    public void ShowDropdown() // aka setvisible? or setter?
    {
        isVisible = true;
    }

    public void CalcDropdown(SKRect rect)
    {
        // use rect to extract the position (left, bottom)
        // the width depends on the longest text, the height on how many elements
        UIManager.MainTextFont.Size = textSize;
        float longestTextWidth = 0;

        foreach (var desc in itemsDesc)
        {
            float textWidth = UIManager.MainTextFont.MeasureText(desc);

            if (textWidth > longestTextWidth)
                longestTextWidth = textWidth;
        }

        // if there isn't any shortcut to any of the items of this dropdown, ignore shortcutWidth!
        bool hasShortcut = false;
        foreach (var shortcut in itemsShortcut)
        {
            if (shortcut != "")
            {
                hasShortcut = true;
                break;
            }
        }

        dropdownRect = SKRect.Create(
            rect.Left,
            rect.Bottom,
            horizontalPadding * 2 + itemHeight + distToIcon + longestTextWidth + (hasShortcut ? shortcutWidth : 0),
            verticalPadding * 2 + itemRects.Length * itemHeight
        );

        // calc items rects
        for (int i = 0; i < itemRects.Length; i++)
        {
            // whole item rect including icon and text and padding
            itemRects[i] = SKRect.Create(
                dropdownRect.Left,
                dropdownRect.Top + verticalPadding + i * itemHeight,
                dropdownRect.Width,
                itemHeight
            );
        }
    }

    public bool Contains(SKPoint point)
    {
        return dropdownRect.Contains(point);
    }

    public void CheckItemHighlight(SKPoint point)
    {
        selectedItemIndex = -1;

        for (int i = 0; i < itemRects.Length; i++)
            if (itemRects[i].Contains(point)) 
            {
                selectedItemIndex = i;
                return;
            }
    }
    
    public void Draw(SKCanvas r, SKPaint paint)
    {
        if (!isVisible) return;
        
        // draw dropdown background TODO change color
        paint.Color = Config.AppUIsHoverColor;
        r.DrawRect(dropdownRect, paint);

        // draw items of this dropdown
        for (int i = 0; i < itemRects.Length; i++)
        {
            // draw highlight
            if (selectedItemIndex == i)
            {
                paint.Color = Config.AppUIsSelectedColor; // TODO change color and make brighter
                r.DrawRect(itemRects[i], paint);
            }

            float x = dropdownRect.Left + horizontalPadding;
            float y = dropdownRect.Top + (verticalPadding + i * itemHeight);
            
            r.DrawBitmap(itemsIcon[i], x, y); // must be itemHeight x itemHeight

            // approximate y center
            float textY = y + itemHeight / 2 + 6;

            UIManager.MainTextFont.Size = textSize;
            UIManager.MainTextPaint.Color = SKColors.White;
            r.DrawText(itemsDesc[i], x + itemHeight + distToIcon, textY, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);
            
            // draw shortcut text if valid
            if (itemsShortcut[i] != "")
                r.DrawText(itemsShortcut[i], dropdownRect.Right - horizontalPadding, textY, SKTextAlign.Right, UIManager.MainTextFont, UIManager.MainTextPaint);
        }
        
        // NOTE: no need to paint.Reset() as long as only the color is changed
    }

    public void DoItemAction()
    {
        if (selectedItemIndex == -1) return;

        // no exception as long as the init in UITopBar doesn't assign null to any item's action
        keybinds.ExecuteAction(itemsAction[selectedItemIndex]);
    }
}
