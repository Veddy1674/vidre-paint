using SkiaSharp;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Vidre.src;

sealed class VidreApp : IDisposable
{
    public static VidreApp Instance { get; private set; } = null!;
    public static double TotalElapsedTime => Instance.window.Time; // in seconds

    private readonly IWindow window;
    private GL? gl;
    private IInputContext? input;
    private GRContext? grContext;
    private SKSurface? skSurface;

    private void UpdateSKSurface()
    {
        var fbInfo = new GRGlFramebufferInfo(0, 0x8058); // GL_RGBA8
        var target = new GRBackendRenderTarget(window.FramebufferSize.X, window.FramebufferSize.Y, 0, 8, fbInfo);

        skSurface = SKSurface.Create(grContext, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    private readonly AppContext AppContext = new(); // contains all the manager classes such as ToolManager, UIManager, etc...

    public VidreApp()
    {
        Instance = this;

        // init app settings
        var options = WindowOptions.Default;
        options.WindowState = WindowState.Maximized;
        options.Title = "Vidre";

        options.PreferredDepthBufferBits = 24;
        options.PreferredStencilBufferBits = 8;
        
        window = Window.Create(options);

        // bind events
        window.Load += OnLoad;
        window.FramebufferResize += OnWinResize;
        window.Render += OnRender;
        window.Closing += OnClose;
        window.FileDrop += OnFileDrop;
    }

    public void Run()
    {
        // load data (variables in Config.cs)
        Config.Load();

        // start app (blocking)
        window.Run();
    }

    #region 'window' events

    private void OnLoad()
    {
        // opengl init
        gl = GL.GetApi(window);

        var glInterface = GRGlInterface.Create();
        grContext = GRContext.CreateGl(glInterface);

        UpdateSKSurface();

        // init all managers
        AppContext.Init(window, ref input);
    }

    private void OnClose()
    {
        Config.Save();
    }

    private void OnRender(double dt)
    {
        if (skSurface == null || gl == null || grContext == null) return;

        SKCanvas r = skSurface.Canvas; // renderer

        // background
        gl.Clear(ClearBufferMask.ColorBufferBit);
        r.Clear(Config.AppBGColor);

        // render canvas if exists
        AppContext.CanvasManager.DrawActive(r);

        // render ui always
        AppContext.UIManager.DrawAll(r, dt);

        // process input manager update (e.g: for repeatable keys)
        AppContext.InputManager.OnUpdate(dt);

        grContext.Flush();
    }

    // gets called on startup too
    private void OnWinResize(Vector2D<int> size)
    {
        // update surface
        grContext?.Flush();
        skSurface?.Dispose();
        UpdateSKSurface();

        // recalculate UIs ("Screen" is updated)
        // must be done before anything else that might use AppContext.UIManager.Screen
        AppContext.UIManager.OnWinResize(size.X, size.Y);

        AppContext.Camera.Focus();
    }

    // called when files are dropped onto the window
    private void OnFileDrop(string[] files)
    {
        if (files.Length == 0) return;
        
        // only take the first file, TODO load multiple files in different canvases
        string filePath = files[0];
        
        // open as canvas
        if (AppContext.CanvasManager.OpenFileAsCanvas(filePath))
            AppContext.Camera.Focus();
    }

    #endregion

    #region Dispose methods

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            Console.Error.WriteLine("Attempt to dispose VidreApp while already disposed (ignored)");
            return;
        }

        // dispose SkiaSharp
        skSurface?.Dispose();
        grContext?.Dispose();

        // dispose Silk.NET
        input?.Dispose();
        gl?.Dispose();
        window?.Dispose();

        // dispose other resources
        AppContext.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~VidreApp() => Dispose();

    #endregion
}
