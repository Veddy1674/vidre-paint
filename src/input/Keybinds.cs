using Silk.NET.Input;
using SkiaSharp;
using Vidre.src.canvas;
using Vidre.src.input.cmdStack;
using Vidre.src.UI;
using static Vidre.src.input.KeybindAction;
using static Vidre.src.input.Modifier;

namespace Vidre.src.input;

// global actions that can be triggered by a keybind or top bar UI
enum KeybindAction
{
    DoNothing = 0, // temp?

    NewCanvas = 1,
    OpenFile = 2,
    OpenRecent = 3,
    SaveFile = 4,
    SaveFileAs = 5,

    DeselectAll = 100,
    DeleteSelection = 101,
    FillSelection = 102,
    FocusCamera = 103,
    ToggleGrid = 104,
    SelectAll = 105,
    InvertSelection = 106,
    CopySelection = 107,
    CropSelection = 108,
    CutSelection = 109,
    DuplicateSelection = 110,
    PasteFromClipboard = 111,
    UndoAction = 112,
    RedoAction = 113,
    MoveSelectionDown1px = 114,
    MoveSelectionUp1px = 115,
    MoveSelectionLeft1px = 116,
    MoveSelectionRight1px = 117,
    MoveSelectionDown10px = 118,
    MoveSelectionUp10px = 119,
    MoveSelectionLeft10px = 120,
    MoveSelectionRight10px = 121,
    MoveCenterSelection = 122, // selection to floating layer and to canvas center
    CenterSelection = 123, // simply moves selection to canvas center if !FloatingExists

    SwitchToolPencil = 200,
    SwitchToolBrush = 201,
    SwitchToolEraser = 202,
    SwitchToolRectSelect = 203,
    SwitchToolDrag = 204,
    SwitchToolEyeDropper = 205,
}

// a keybind is identified by a key and modifiers (e.g: Ctrl+C)
readonly struct KeybindKey(Key key, Modifier modifiers) : IEquatable<KeybindKey>
{
    public readonly Key Key = key;
    public readonly Modifier Modifiers = modifiers;

    #region implementing Equals and Hashcode due to IEquatable, which avoids slow boxing

    public bool Equals(KeybindKey other)
        => Key == other.Key && Modifiers == other.Modifiers;

    public override bool Equals(object? obj)
        => obj is KeybindKey other && this.Equals(other);

    public override int GetHashCode()
        => (int) Key ^ ((int)Modifiers << 16); // combine

    #endregion

    public override string ToString()
    {
        var parts = new List<string>();

        // priority in this order (e.g: Ctrl+Shift+Alt+C)
        if (Modifiers.HasFlag(Ctrl))
            parts.Add("Ctrl");
        
        if (Modifiers.HasFlag(Shift))
            parts.Add("Shift");
        
        if (Modifiers.HasFlag(Alt))
            parts.Add("Alt");
        
        parts.Add(Key.ToString());

        return string.Join("+", parts);
    }
}

class Keybinds
{
    private readonly AppContext AppContext;

    private readonly Dictionary<KeybindKey, KeybindAction> keyMap = [];
    private readonly SKPaint commonPaint = new(); // shared

    // NOTE that some actions aren't triggered by keybinds, by default (e.g: OpenRecent)
    // Default keybinds:
    public Keybinds(AppContext context)
    {
        this.AppContext = context;
        
        // File
        Register(Key.N, NewCanvas, Ctrl);
        Register(Key.O, OpenFile, Ctrl);
        Register(Key.S, SaveFile, Ctrl);
        Register(Key.S, SaveFileAs, Ctrl | Shift);
        
        Register(Key.Escape, DeselectAll);
        Register(Key.Backspace, DeleteSelection);
        Register(Key.Delete, DeleteSelection);
        Register(Key.Enter, FillSelection);

        Register(Key.F, FocusCamera);
        Register(Key.G, ToggleGrid);

        Register(Key.A, SelectAll, Ctrl);
        Register(Key.C, CopySelection, Ctrl);
        Register(Key.X, CropSelection, Ctrl | Shift);
        Register(Key.X, CutSelection, Ctrl);
        Register(Key.V, PasteFromClipboard, Ctrl);
        Register(Key.Z, UndoAction, Ctrl);
        Register(Key.Y, RedoAction, Ctrl);
        Register(Key.I, InvertSelection, Ctrl);

        Register(Key.P, SwitchToolPencil);
        Register(Key.B, SwitchToolBrush);
        Register(Key.E, SwitchToolEraser);
        Register(Key.R, SwitchToolRectSelect);
        Register(Key.D, SwitchToolDrag);
        Register(Key.S, SwitchToolRectSelect);
        Register(Key.K, SwitchToolEyeDropper);

        Register(Key.Up, MoveSelectionUp10px, Ctrl);
        Register(Key.Down, MoveSelectionDown10px, Ctrl);
        Register(Key.Left, MoveSelectionLeft10px, Ctrl);
        Register(Key.Right, MoveSelectionRight10px, Ctrl);
        
        Register(Key.Up, MoveSelectionUp1px);
        Register(Key.Down, MoveSelectionDown1px);
        Register(Key.Left, MoveSelectionLeft1px);
        Register(Key.Right, MoveSelectionRight1px);

        // ironically, "H" is about at the center of the keyboard
        Register(Key.H, CenterSelection);
        Register(Key.H, MoveCenterSelection, Shift);
    }

    private void Register(Key key, KeybindAction action, Modifier modifiers = None)
    {
        keyMap[new KeybindKey(key, modifiers)] = action;
    }

    public bool TryGetAction(Key key, Modifier modifiers, out KeybindAction action)
    {
        return keyMap.TryGetValue(new KeybindKey(key, modifiers), out action);
    }

    // returns the keybind that triggers the action, or null
    public KeybindKey? GetKeybind(KeybindAction? action)
    {
        if (action == null) return null;
        
        foreach (var kvp in keyMap)
        {
            if (kvp.Value == action)
                return kvp.Key;
        }
        return null;
    }

    public async void ExecuteAction(KeybindAction action)
    {
        var canvas = AppContext.ActiveCanvas; // nullable

        switch (action)
        {
            #region tool switching

            case SwitchToolPencil:
                AppContext.ToolManager.SetActiveTool(EnumTool.Pencil);
                break;
            case SwitchToolBrush:
                AppContext.ToolManager.SetActiveTool(EnumTool.Brush);
                break;
            case SwitchToolEraser:
                AppContext.ToolManager.SetActiveTool(EnumTool.Eraser);
                break;
            case SwitchToolRectSelect:
                AppContext.ToolManager.SetActiveTool(EnumTool.Selection);
                break;
            case SwitchToolDrag:
                AppContext.ToolManager.SetActiveTool(EnumTool.Drag);
                break;
            case SwitchToolEyeDropper:
                AppContext.ToolManager.SetActiveTool(EnumTool.EyeDropper);
                break;
            
            #endregion

            #region All items of dropdowns of topBarUI (nested regions)

            #region File (0)

            case UndoAction:
                if (canvas == null) return;
                canvas.UndoManager.Undo();
                break;
            
            case RedoAction:
                if (canvas == null) return;
                canvas.UndoManager.Redo();
                break;

            case NewCanvas:
            {
                AppContext.UITopBar.CloseDropdown();

                // TODO add action "new canvas default" and "new canvas..." with popup
                // AppContext.CanvasManager.NewCanvas(512, 512, CanvasType.Empty);

                break;
            }

            case OpenFile:
            {
                AppContext.UITopBar.CloseDropdown();

                var openPath = await Utils.ShowOpenFileDialog(Config.DefaultDialogPath);
                if (!string.IsNullOrEmpty(openPath))
                {
                    if (AppContext.CanvasManager.OpenFileAsCanvas(openPath))
                        AppContext.Camera.Focus(); // focus if success
                }

                break;
            }

            // NOTE: this is not called when pressing "Open Recent" button itself, but an item of its subdropdown
            case OpenRecent:
            {
                AppContext.UITopBar.CloseDropdown();

                break;
            }

            case SaveFile:
            {
                if (canvas == null) return;

                AppContext.UITopBar.CloseDropdown();

                // if simple Save fails, go to save as
                if (!AppContext.CanvasManager.SaveActiveCanvasToFile())
                    goto case SaveFileAs;

                break;
            }

            case SaveFileAs:
            {
                if (canvas == null) return;

                AppContext.UITopBar.CloseDropdown();

                var savePath = await Utils.ShowSaveFileDialog(Config.DefaultDialogPath);
                if (!string.IsNullOrEmpty(savePath))
                {
                    // returns success but unused
                    AppContext.CanvasManager.SaveActiveCanvasToFile(savePath);
                }

                break;
            }

            #endregion

            #region Edit (1)

            // TODO

            #endregion

            #region View (2)

            // TODO

            #endregion

            #region Effects (3)

            // TODO

            #endregion

            #region Advanced (4)

            // TODO

            #endregion

            #endregion
            
            #region move selection

            case MoveSelectionDown1px:
                MoveSelection(canvas, 0, 1);
                break;

            case MoveSelectionUp1px:
                MoveSelection(canvas, 0, -1);
                break;

            case MoveSelectionLeft1px:
                MoveSelection(canvas, -1, 0);
                break;

            case MoveSelectionRight1px:
                MoveSelection(canvas, 1, 0);
                break;
            
            case MoveSelectionUp10px:
                MoveSelection(canvas, 0, -10);
                break;
            
            case MoveSelectionDown10px:
                MoveSelection(canvas, 0, 10);
                break;
            
            case MoveSelectionLeft10px:
                MoveSelection(canvas, -10, 0);
                break;
            
            case MoveSelectionRight10px:
                MoveSelection(canvas, 10, 0);
                break;
            
            case MoveCenterSelection: // drag the floating layer to center
            {
                if (canvas == null || !canvas.HasCommittedSelection) return;
                
                if (!canvas.FloatingExists)
                    canvas.StartFloatingSelection();

                int targetX = (canvas.Width - canvas.FloatingBitmap!.Width) / 2;
                int targetY = (canvas.Height - canvas.FloatingBitmap.Height) / 2;

                int dx = targetX - canvas.FloatingX;
                int dy = targetY - canvas.FloatingY;
                
                canvas.TranslateFloating(dx, dy);
                canvas.UndoManager.PushAction(new UndoDrag(dx, dy)); // register right away
                break;
            }
            
            case CenterSelection: // move selection to center
            {
                if (canvas == null || !canvas.HasCommittedSelection || canvas.FloatingExists) return;

                var oldSelection = new SKRegion(canvas.CommittedSelection);
                var bounds = oldSelection.Bounds;

                int targetX = (canvas.Width - bounds.Width) / 2;
                int targetY = (canvas.Height - bounds.Height) / 2;

                int dx = targetX - bounds.Left;
                int dy = targetY - bounds.Top;
                
                canvas.TranslateSelection(dx, dy);
                canvas.UndoManager.PushAction(new UndoSelection(oldSelection, canvas.CommittedSelection)); // register right away
                break;
            }

            #endregion

            // esc to deselect and "commit" floating layer
            case DeselectAll:
            {
                if (canvas == null) return;

                if (canvas.FloatingExists)
                    canvas.MergeFloatingToMain(); // registers a undo/redo action!

                var oldSel = new SKRegion(canvas.CommittedSelection);
                canvas.ClearAllSelection();

                // new selection is just "new()"
                canvas.UndoManager.PushAction(new UndoSelection(oldSel, canvas.CommittedSelection)); // so this is the second action. TODO revise
                break;
            }

            // delete selected area
            case DeleteSelection:
                if (canvas == null || !canvas.HasCommittedSelection) return;

                UndoableBitmapOp(canvas, () =>
                    FillSelectionWith(canvas, SKColors.Transparent)
                );
                
                break;

            // fill selected area with primary color
            case FillSelection:
                if (canvas == null || !canvas.HasCommittedSelection) return;

                UndoableBitmapOp(canvas, () =>
                    FillSelectionWith(canvas, AppContext.ToolManager.PrimaryColor)
                );
                
                break;

            // f
            case FocusCamera:
                AppContext.Camera.Focus();
                break;

            // g
            case ToggleGrid:
                if (canvas != null)
                    Canvas.EnableGrid(canvas, !Canvas.ShowGrid);
                
                break;

            // ctrl a
            case SelectAll:
            {
                if (canvas == null) return;

                if (canvas.FloatingExists)
                    canvas.MergeFloatingToMain();

                var oldSel = new SKRegion(canvas.CommittedSelection);
                
                canvas.ClearAllSelection();
                canvas.UpdateSelection(new SKRectI(0, 0, canvas.Width, canvas.Height));
                canvas.CommitSelection();

                canvas.UndoManager.PushAction(new UndoSelection(oldSel, canvas.CommittedSelection));

                break;
            }

            // ctrl c
            case CopySelection:
                canvas?.CopySelToClipboard();
                break;

            // ctrl shift x
            case CropSelection:
            {
                if (canvas == null || !canvas.HasCommittedSelection) return;
                
                var bounds = canvas.CommittedSelection.Bounds; // to crop
                var undoAction = new UndoCrop(canvas, AppContext.Camera, bounds);

                canvas.CropToRect(bounds);
                AppContext.Camera.Focus();
                canvas.ClearAllSelection();
                
                canvas.UndoManager.PushAction(undoAction);
                
                break;  
            }

            // ctrl x
            case CutSelection:
                if (canvas == null || !canvas.HasCommittedSelection) return;
                
                canvas.CopySelToClipboard();

                UndoableBitmapOp(canvas, () =>
                    FillSelectionWith(canvas, SKColors.Transparent)
                );

                break;
            
            // ctrl d
            case DuplicateSelection:
                if (canvas == null || !canvas.HasCommittedSelection) return;

                canvas.DuplicateSelection();

                break;

            // ctrl v
            case PasteFromClipboard:
                canvas?.PasteFromClipboard();
                break;
            
            // ctrl i
            case InvertSelection:
            {
                if (canvas == null || !canvas.HasCommittedSelection) return;
                
                var oldSel = new SKRegion(canvas.CommittedSelection);
    
                using (var fullCanvas = new SKRegion(new SKRectI(0, 0, canvas.Width, canvas.Height)))
                {
                    using var inverted = new SKRegion(fullCanvas);
                    inverted.Op(oldSel, SKRegionOperation.Difference);

                    canvas.SetSelection(inverted);
                }

                canvas.UndoManager.PushAction(new UndoSelection(oldSel, canvas.CommittedSelection));
                break;
            }
            
            default: break;
        }
    }

    #region private utils

    private void FillSelectionWith(Canvas canvas, SKColor color)
    {
        // commonPaint.Reset();
        commonPaint.Color = color;
        commonPaint.BlendMode = SKBlendMode.Src; // replace pixels

        canvas.FillSelectionRegion(commonPaint);
    }

    private static void UndoableBitmapOp(Canvas canvas, Action action)
    {
        // bounds is area that will be affected, must be known beforehand, in most cases it's the committed selection
        var undoAction = new UndoBitmap(canvas, canvas.CommittedSelection.Bounds);

        action(); // e.g: FillSelectionWith(canvas, AppContext.ToolManager.PrimaryColor);

        undoAction.PostUpdate(canvas);

        // register undo action
        canvas.UndoManager.PushAction(undoAction);
    }

    private static void MoveSelection(Canvas? canvas, int x, int y)
    {
        if (canvas == null || !canvas.HasCommittedSelection) return;

        if (!canvas.FloatingExists)
            canvas.StartFloatingSelection();
        
        canvas.TranslateFloating(x, y);
        canvas.UndoManager.PushAction(new UndoDrag(x, y)); // register right away
    }

    #endregion

    public void Dispose()
    {
        commonPaint.Dispose();
    }
}
