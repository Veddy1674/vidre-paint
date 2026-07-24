using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;
using Vidre.src.canvas;

namespace Vidre.src;

static class Config
{
    // canvas:
    public static int DefaultCanvasWidth = 512;
    public static int DefaultCanvasHeight = 512;
    public static CanvasType? DefaultCanvasType = CanvasType.White; // null = no canvas

    public static bool InvertedPanning = false;

    public static int UndoStackSize = 128; // how many operations to keep in RAM for undo/redo

    // design:
    public static SKColor AppBGColor = new(34, 34, 34); // dark gray
    public static SKColor AppTopBarColor = new(25, 25, 25); // dark gray

    public static SKColor AppUIsBGColor = new(55, 55, 55); // gray
    public static SKColor AppUIsBGColor_Highlight = new(64, 64, 64); // light gray
    public static SKColor AppUIsHoverColor = new(64, 64, 64); // light gray
    public static SKColor AppUIsSelectedColor = new(73, 73, 73); // light gray

    public static SKColor SelectionColor1 = new(10, 10, 25); // outline: dark blue
    public static SKColor SelectionColor2 = new(230, 230, 245); // outline: light gray/blue
    public static SKColor SelectionColorFill = new(200, 200, 255, 40); // transparent light blue
    public static SKColor SelectionSubColorFill = new(255, 200, 200, 40); // transparent light red
    public static SKColor SelectionAddColorFill = new(200, 255, 200, 40); // transparent light green
    public static SKColor SelectionFloatingLayerColorFill = new(0, 0, 0, 20); // transparent black

    public static SKColor TextSelectionColor = new(120, 120, 120, 120); // transparent light gray

    // other:
    public static bool AutoSave = false; // whether to save all canvases with a path on sudden closure (otherwise shows a popup)

    // where dialogs open by default (if null wherever the executable is)
    // used internally, cannot be set by user, but it must be saved and loaded regardless
    public static string? DefaultDialogPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    // for load and save:
    public static bool ConfigExists { get; private set; } // whether config.json is found on Load()
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // in AppData
        "Vidre", 
        "config.json"
    );

    private static Dictionary<string, object?> AllConfigVariables => new()
    {
        { nameof(DefaultCanvasWidth), DefaultCanvasWidth },
        { nameof(DefaultCanvasHeight), DefaultCanvasHeight },
        { nameof(DefaultCanvasType), DefaultCanvasType.ToString() },
        { nameof(InvertedPanning), InvertedPanning },
        { nameof(UndoStackSize), UndoStackSize },
        { nameof(AutoSave), AutoSave },
        { nameof(DefaultDialogPath), DefaultDialogPath },
    };

    // called on closure!
    public static void Save()
    {
        if (!Program.AllowLoadSave) return;

        try
        {
            // try creating directory first
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

            // string json = JsonSerializer.Serialize(AllConfigVariables, ConfigJsonContext.Default.DictionaryStringObject);
            // File.WriteAllText(ConfigPath, json);

            // stream avoids allocating string json and is faster!
            using var stream = File.Create(ConfigPath);
            JsonSerializer.Serialize(stream, AllConfigVariables, ConfigJsonContext.Default.DictionaryStringObject);

            Debug.WriteLine($"Saved config to: {ConfigPath}");
        }
        catch
        {
            Debug.WriteLine($"Unable to save config: {ConfigPath}");
        }
    }

    // called on startup, before window.Run() in VidreApp.cs
    public static void Load()
    {
        if (!Program.AllowLoadSave) return;

        // TODO use ConfigExists to load defaults in Keybinds.cs
        
        ConfigExists = false;
        try
        {
            if (!File.Exists(ConfigPath))
                return;

            string json = File.ReadAllText(ConfigPath);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;

            root.SetIfConfigured(nameof(DefaultCanvasWidth), ref DefaultCanvasWidth);
            root.SetIfConfigured(nameof(DefaultCanvasHeight), ref DefaultCanvasHeight);
            root.SetIfConfigured(nameof(DefaultCanvasType), ref DefaultCanvasType);
            root.SetIfConfigured(nameof(InvertedPanning), ref InvertedPanning);
            root.SetIfConfigured(nameof(UndoStackSize), ref UndoStackSize);
            root.SetIfConfigured(nameof(AutoSave), ref AutoSave);
            root.SetIfConfigured(nameof(DefaultDialogPath), ref DefaultDialogPath);

            ConfigExists = true;
            
            Debug.WriteLine($"Loaded config from: {ConfigPath}");
        }
        catch
        {
            Debug.WriteLine($"Unable to load config: {ConfigPath}");
        }
    }
}

// config
[JsonSourceGenerationOptions(
    WriteIndented = true
)]
// type of AllConfigVariables
[JsonSerializable(typeof(Dictionary<string, object?>))]
// allowed types
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
partial class ConfigJsonContext : JsonSerializerContext {}