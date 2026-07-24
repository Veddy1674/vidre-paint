using SkiaSharp;
using Vidre.src.input;
using Vidre.src.UI;

namespace Vidre.src.canvas;

enum CanvasType
{
    White, Black, Transparent
}

class CanvasManager(AppContext context) : IDisposable
{
    private readonly AppContext AppContext = context;

    private readonly List<(Canvas canvas, string? path)> AllCanvas = [];
    public Canvas? ActiveCanvas { get; private set; } = null;

    // adds a canvas to AllCanvas list and sets as active
    public void NewCanvas(int width, int height, CanvasType type)
    {
        ActiveCanvas = new Canvas(width, height, type);
        AllCanvas.Add((ActiveCanvas, null)); // new canvas but not saved
    }

    public void DrawActive(SKCanvas r)
    {
        ActiveCanvas?.DrawAll(r, AppContext);
    }

    // aka OpenFile, opens an image file as a new canvas and sets it to active
    public bool OpenFileAsCanvas(string path)
    {
        // NOTE: path must never be null or invalid
        try
        {
            if (!File.Exists(path))
                return false;

            var loaded = SKBitmap.Decode(path);

            if (loaded == null)
                return false;

            // standardize to Rgba8888, Premul, with Alpha
            if (loaded.ColorType != SKColorType.Rgba8888 || loaded.AlphaType != SKAlphaType.Premul)
            {
                var newInfo = new SKImageInfo(loaded.Width, loaded.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
    
                var standardized = new SKBitmap(newInfo);
                
                using (var canvas = new SKCanvas(standardized)) // (used once)
                    canvas.DrawBitmap(loaded, 0, 0);

                loaded.Dispose();
                loaded = standardized;
            }

            // new canvas:
            ActiveCanvas = new Canvas(loaded);
            AllCanvas.Add((ActiveCanvas, path));

            // update grid as active canvas has different size
            Canvas.UpdateGridPath(ActiveCanvas);

            return true;
        }
        catch { return false; }
    }

    // aka Save As, only saves current canvas to file
    public bool SaveActiveCanvasToFile(string path, int quality = 100)
    {
        // NOTE: ActiveCanvas and path must never be null or invalid
        try
        {
            var imageFormat = Utils.GetImageFormat(path);

            // overwrites if existing!
            using var stream = File.Create(path);

            // encode
            using var data = ActiveCanvas!.Encode(imageFormat, quality);
            
            if (data == null)
                return false;

            data.SaveTo(stream);

            // update path of active canvas
            for (int i = 0; i < AllCanvas.Count; i++)
            {
                if (AllCanvas[i].canvas == ActiveCanvas)
                {
                    AllCanvas[i] = (ActiveCanvas, path);
                    break;
                }
            }

            return true;
        }
        catch { return false; }
    }

    // aka Save
    public bool SaveActiveCanvasToFile()
    {
        // NOTE: ActiveCanvas must never be null or invalid

        // get path of active canvas
        var tuple = AllCanvas.FirstOrDefault(c => c.canvas == ActiveCanvas);

        if (string.IsNullOrEmpty(tuple.path))
            return false;

        SaveActiveCanvasToFile(tuple.path);
        
        return true;
    }

    public void Dispose()
    {
        foreach (var (canvas, path) in AllCanvas)
        {
            if (Config.AutoSave && !string.IsNullOrEmpty(path))
            {
                // directly autosave the iterating canvas
                try
                {
                    var imageFormat = Utils.GetImageFormat(path);

                    using var stream = File.Create(path);
                    using var data = canvas.Encode(imageFormat, 100); // quality always 100

                    data?.SaveTo(stream);
                } catch {}
            }
            
            canvas.Dispose();
        }

        AllCanvas.Clear();
        ActiveCanvas = null;

        // dispose shared
        Canvas.TransparencyPaint?.Dispose();
        Canvas.SelectionPaint?.Dispose();
        Canvas.SelectionFillPaint?.Dispose();
        
        Canvas.GridPath?.Dispose();
        Canvas.GridPaint?.Dispose();
    }
}