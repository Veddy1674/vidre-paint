using Vidre.src;

namespace Vidre;

class Program
{
    public static bool AllowLoadSave { get; private set; } = true;
    public static string? ImageToOpen { get; private set; } // what to open on startup

    static void Main(string[] args)
    {
        // args
        if (args.Length > 0)
        {
            // first arg must always be image
            
            ImageToOpen = args[0];
            // if it doesn't actually exist, invalidate
            if (!File.Exists(ImageToOpen))
                ImageToOpen = null;

            // additional
            if (args.Any(arg => arg == "--noconf"))
                AllowLoadSave = false;
        }

        // app init
        using var app = new VidreApp();
        app.Run();
    }
}
