using SkiaSharp;
using Vidre.src.input;

namespace Vidre.src.UI;

class UITopBar : IDisposable // sorta of UIComponent
{
    private static readonly SKPaint TopBarPaint = new(); // shared

    private readonly string[] topBarNames;
    private readonly SKRect[] topBarRects;
    private readonly UIMenuDropdown[] topBarDropdowns;
    private SKRect topBarMainRect;

    private int selectedTopBarMenu = -1;

    // can be called multiple times with no issues
    public void CloseDropdown()
    {
        selectedTopBarMenu = -1;
    }

    public UITopBar(Keybinds keybinds)
    {
        // NOTE: here are initialized all the menus in the top bar
        topBarNames = ["File", "Edit", "View", "Effects", "Advanced"];
        topBarRects = new SKRect[topBarNames.Length];
        topBarDropdowns = new UIMenuDropdown[topBarNames.Length];
        
        // init dropdowns
        // topBarDropdowns[0] = new(itemsIconAndDesc: [
        //     // ICON ; DESCRIPTION
        //     ("icons/file_menu/new_canvas.png", "New canvas..."),
        //     ("icons/file_menu/open_file.png", "Open file..."),
        //     ("icons/file_menu/open_file_recent.png", "Open recent..."),
        //     ("icons/file_menu/save.png", "Save"),
        //     ("icons/file_menu/save_as.png", "Save as...")
        // ]);

        topBarDropdowns[0] = new(keybinds, itemsIconAndDescAndAction: [
            // ICON ; DESCRIPTION ; ACTION
            ("new.png", "New Canvas...", KeybindAction.NewCanvas),
            ("new.png", "Open File...", KeybindAction.OpenFile),
            ("new.png", "Open Recent...", null),
            ("new.png", "Save", KeybindAction.SaveFile),
            ("new.png", "Save As...", KeybindAction.SaveFileAs)
        ]);
        topBarDropdowns[0].AddSubDropdowns(2); // open recent

        topBarDropdowns[1] = new(keybinds, itemsIconAndDescAndAction: [
            // ICON ; DESCRIPTION ; ACTION
            ("icons/edit_menu/undo.png", "Undo", KeybindAction.DoNothing),
            ("icons/edit_menu/redo.png", "Redo", KeybindAction.DoNothing)
        ]);

        topBarDropdowns[2] = new(keybinds, itemsIconAndDescAndAction: [
            // ICON ; DESCRIPTION ; ACTION
            ("icons/edit_menu/undo.png", "Undo", KeybindAction.DoNothing),
            ("icons/edit_menu/redo.png", "Redo", KeybindAction.DoNothing)
        ]);

        topBarDropdowns[3] = new(keybinds, itemsIconAndDescAndAction: [
            // ICON ; DESCRIPTION ; ACTION
            ("icons/edit_menu/undo.png", "Undo", KeybindAction.DoNothing),
            ("icons/edit_menu/redo.png", "Redo", KeybindAction.DoNothing)
        ]);

        topBarDropdowns[4] = new(keybinds, itemsIconAndDescAndAction: [
            // ICON ; DESCRIPTION ; ACTION
            ("icons/edit_menu/undo.png", "Undo", KeybindAction.DoNothing),
            ("icons/edit_menu/redo.png", "Redo", KeybindAction.DoNothing)
        ]);
    }

    public const float TopBarHeight = 30f;

    // calculate SKRects
    public void CalcTopBar(SKRectI Screen)
    {
        topBarMainRect = new SKRect(Screen.Left, Screen.Top, Screen.Width, TopBarHeight);

        var rect = topBarMainRect; // copy

        rect.Left += 12; // x start
        rect.Right = rect.Left + 52; // width

        topBarRects[0] = rect; // "File"

        rect.Left = rect.Right;
        rect.Right = rect.Left + 52; // width

        topBarRects[1] = rect; // "Edit"

        rect.Left = rect.Right;
        rect.Right = rect.Left + 52; // width

        topBarRects[2] = rect; // "View"

        rect.Left = rect.Right;
        rect.Right = rect.Left + 68; // width

        topBarRects[3] = rect; // "Effects"

        rect.Left = rect.Right;
        rect.Right = rect.Left + 86; // width

        topBarRects[4] = rect; // "Advanced"

        // call all dropdowns recalc
        for (int i = 0; i < topBarDropdowns.Length; i++)
            topBarDropdowns[i].CalcDropdown(topBarRects[i]);
    }

    public void DrawTopBar(SKCanvas r)
    {
        // draw main top bar
        TopBarPaint.Color = Config.AppTopBarColor;
        r.DrawRect(topBarMainRect, TopBarPaint);

        // draw texts
        UIManager.MainTextFont.Size = 16f;

        for (int i = 0; i < topBarNames.Length; i++)
        {
            // highlight if selected
            if (selectedTopBarMenu == i)
            {
                TopBarPaint.Color = Config.AppUIsSelectedColor;
                r.DrawRect(topBarRects[i], TopBarPaint);
            }

            // center vertically (approx.) and horizontally
            r.DrawText(topBarNames[i], topBarRects[i].MidX, topBarRects[i].MidY + 5, SKTextAlign.Center, UIManager.MainTextFont, UIManager.MainTextPaint);
        }
    }

    public void DrawDropdown(SKCanvas r)
    {
        // draw selected menu dropdown separately from DrawTopBar, because it must be on top of floating windows
        if (selectedTopBarMenu != -1)
            topBarDropdowns[selectedTopBarMenu].Draw(r, TopBarPaint);
    }

    #region Events

    public bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        // show dropdown - edit: show directly on mouse move
        // topBarDropdowns[selectedTopBarMenu].ShowDropdown();

        if (selectedTopBarMenu != -1)
        {
            topBarDropdowns[selectedTopBarMenu].DoItemAction(); // if there's no selected item, it does nothing
            return true;
        }

        return false;
    }

    // unused
    // public bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    // {
    //     return false;
    // }

    public bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        // if a top bar is already selected (thus dropdown is open), keep it open if mouse is over dropdown
        if (selectedTopBarMenu != -1 && topBarDropdowns[selectedTopBarMenu].Contains(mousePos))
        {
            CheckItemHover(mousePos);
            return true;
        }

        selectedTopBarMenu = -1;

        for (int i = 0; i < topBarRects.Length; i++)
        {
            if (topBarRects[i].Contains(mousePos))
            {
                selectedTopBarMenu = i;
                topBarDropdowns[i].ShowDropdown(); // set to visible if isn't already

                CheckItemHover(mousePos);
                return true;
            }
        }

        return false;
    }

    private void CheckItemHover(SKPoint mousePos)
    {
        // selectedItemIndex should be checked beforehand!

        // internally iterates for each item in the dropdown and sets
        // the highlight to true for the corresponding item (if mouse is over it)
        topBarDropdowns[selectedTopBarMenu].CheckItemHighlight(mousePos);
    }

    #endregion

    public void Dispose()
    {
        TopBarPaint.Dispose();
    }
}