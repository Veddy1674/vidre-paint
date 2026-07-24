using SkiaSharp;
using Vidre.src.canvas;
using Vidre.src.input.tools;

namespace Vidre.src.input;

enum EnumTool
{
    Drag = 0,
    Pencil = 1,
    Brush = 2,
    Eraser = 3,
    Selection = 4,
    EyeDropper = 5,
}

class ToolManager : IDisposable
{
    private readonly AppContext AppContext;
    private Canvas? Canvas => AppContext.ActiveCanvas;

    public DrawTool ActiveTool { get; private set; } = null!; // default Brush (in constructor)
    private int ActiveToolIndex;

    // private set so that SetPrimaryColor is used (which invokes event)
    public SKColor PrimaryColor { get; private set; } = SKColors.Black;
    public SKColor SecondaryColor { get; private set; } = SKColors.White;

    public event Action? OnPrimaryColorChanged;
    public event Action? OnSecondaryColorChanged;

    public void SwapColors()
    {
        (PrimaryColor, SecondaryColor) = (SecondaryColor, PrimaryColor);
        OnPrimaryColorChanged?.Invoke();
        OnSecondaryColorChanged?.Invoke();
    }

    public void SetPrimaryColor(SKColor color)
    {
        PrimaryColor = color;
        OnPrimaryColorChanged?.Invoke();
    }

    public void SetSecondaryColor(SKColor color)
    {
        SecondaryColor = color;
        OnSecondaryColorChanged?.Invoke();
    }

    public float BrushSize = 3.0f;
    public float EraserSize = 10.0f;

    public bool AntiAliasing = true;

    public DrawTool[] AllTools = [];

    // init after toolManager/InputEvents and such are created (because some tool use their static instances)
    public ToolManager(AppContext context)
    {
        this.AppContext = context;

        // only initialize the default tool (Brush) at startup to reduce startup overhead
        // remaining tools are loaded after everything else is processed in AppContext.Init()
        AllTools = new DrawTool[Enum.GetNames<EnumTool>().Length];
        AllTools[(int)EnumTool.Brush] = new BrushTool(this);

        SetActiveTool(EnumTool.Brush); // default
    }

    public void LoadAllTools()
    {
        // each index corresponds to EnumTool NOTE: the order must be exactly the same
        // everytime a new tool is added, it must be added to EnumTool too
        AllTools[(int)EnumTool.Drag] = new DragTool(this, AppContext);
        AllTools[(int)EnumTool.Pencil] = new PencilTool(this);
        // brush is already initialized in constructor!
        AllTools[(int)EnumTool.Eraser] = new EraserTool(this);
        AllTools[(int)EnumTool.Selection] = new RectSelectionTool(this, AppContext);
        AllTools[(int)EnumTool.EyeDropper] = new EyeDropperTool(this, AppContext);
    }

    public void SetActiveTool(EnumTool tool)
    {
        // merge floating layer when switching from drag to selection
        if (Canvas != null && Canvas.FloatingExists && GetActiveTool() == EnumTool.Drag && tool == EnumTool.Selection)
            Canvas.MergeFloatingToMain();

        ActiveToolIndex = (int)tool;
        ActiveTool = AllTools[ActiveToolIndex];

        // setup paint settings - edit: no more needed as every tool got its own paint, but this might come up useful for initialization
        // ActiveTool.OnSelect();

        // TODO: set cursor icon
    }

    public EnumTool GetActiveTool()
        => (EnumTool)ActiveToolIndex;

    private bool isInteracting = false;

    #region Mouse events
    public void OnMouseDown(bool leftDown, bool rightDown, SKPoint canvasPos)
    {
        if ((!leftDown && !rightDown) || Canvas == null || isInteracting) return;

        isInteracting = true;

        // priority to left (left + right = left)

        // draw point/execute tool action
        ActiveTool.OnDown(Canvas, canvasPos, rightDown ? SecondaryColor : PrimaryColor);
    }

    public void OnMouseUp(bool leftDown, bool rightDown, SKPoint canvasPos)
    {
        if (Canvas == null || !isInteracting) return;

        isInteracting = false;

        ActiveTool.OnUp(Canvas, canvasPos);
    }

    public void OnMouseMove(bool leftDown, bool rightDown, SKPoint canvasPos)
    {
        if ((!leftDown && !rightDown) || Canvas == null || !isInteracting) return;

        ActiveTool.OnMove(Canvas, canvasPos);
    }

    public void OnModifier(Modifier modifiers)
    {
        if (Canvas == null) return;

        ActiveTool.OnModifier(Canvas, modifiers);
    }
    #endregion

    public void Dispose()
    {
        foreach (var tool in AllTools)
            tool.Dispose();
    }
}