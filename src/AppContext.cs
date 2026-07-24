using System.Diagnostics;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Vidre.src.canvas;
using Vidre.src.input;
using Vidre.src.input.cmdStack;
using Vidre.src.UI;

namespace Vidre.src;

// the main reason a class like this is used is because inside Keybinds.cs
// there are all the global actions that need access to the managers
// (for example Save File, Crop Selection, Change tool to...)
class AppContext : IDisposable
{
    // all references to managers and such
    private CanvasManager _canvasManager = null!; // contains all the Canvas
    private Camera _camera = null!;
    private Keybinds _keybinds = null!;
    private InputManager _inputManager = null!; // manages all kind of inputs
    private UIManager _uiManager = null!; // draws and manages UI elements
    private UITopBar _uiTopBar = null!; // previously inside uiManager
    private ToolManager _toolManager = null!;

    public CanvasManager CanvasManager => _canvasManager;
    public Camera Camera => _camera;
    public Keybinds Keybinds => _keybinds;
    public InputManager InputManager => _inputManager;
    public UIManager UIManager => _uiManager;
    public UITopBar UITopBar => _uiTopBar;
    public ToolManager ToolManager => _toolManager;

    // shortcuts
    public Canvas? ActiveCanvas => _canvasManager.ActiveCanvas;

    public void Init(IWindow window, ref IInputContext? input)
    {
        // canvas init
        _canvasManager = new CanvasManager(this);

        // in config you could set DefaultCanvasType to null
        if (Config.DefaultCanvasType is CanvasType canvasType && Program.ImageToOpen == null)
            _canvasManager.NewCanvas(Config.DefaultCanvasWidth, Config.DefaultCanvasHeight, CanvasType.White);

        // camera init
        _camera = new(this);

        // tool init
        _toolManager = new ToolManager(this);

        // keybinds init (for ui and input)
        _keybinds = new Keybinds(this);

        // ui init
        _uiManager = new UIManager(window.FramebufferSize, this);
        _uiTopBar = new(_keybinds); // _keybinds is passed instead of this because it's only used in the constructor

        _camera.Focus(); // focus camera in center, must be done after UIManager is initialized

        // create manager of input events
        _inputManager = new InputManager(this);
        
        // actual input init
        input = window.CreateInput();
        var keyboard = input.Keyboards[0];
        var mouse = input.Mice[0];
        
        keyboard.KeyDown += _inputManager.OnKeyDown;
        keyboard.KeyUp += _inputManager.OnKeyUp;
        keyboard.KeyChar += _inputManager.OnKeyChar;

        mouse.MouseDown += _inputManager.OnMouseDown;
        mouse.MouseUp += _inputManager.OnMouseUp;
        mouse.MouseMove += _inputManager.OnMouseMove;
        mouse.Scroll += _inputManager.OnMouseScroll;

        // after everything, load the image at arg[0] from command line:

        if (Program.ImageToOpen != null)
            if (_canvasManager.OpenFileAsCanvas(Program.ImageToOpen))
            {
                _camera.Focus(); // as in Keybinds.cs
                Config.DefaultDialogPath = Path.GetDirectoryName(Program.ImageToOpen);
            }
            else
                Debug.WriteLine("Attempted to open a file through args[0] but failed");

        // load remaining tools after image is processed to reduce startup overhead
        _toolManager.LoadAllTools();
    }

    public void Dispose()
    {
        _canvasManager.Dispose(); // saves all
        _inputManager.Dispose();
        _uiManager.Dispose();
        _uiTopBar.Dispose();
        _toolManager.Dispose();
        _keybinds.Dispose();
    }
}