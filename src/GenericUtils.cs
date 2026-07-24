using SkiaSharp;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using NativeFileDialogNET;
using System.Text.Json;

#if Windows
    using System.Runtime.InteropServices;
#endif

namespace Vidre.src;

static class Utils
{
    public static SKPoint Subtract(Vector2 a, SKPoint b)
        => new(a.X - b.X, a.Y - b.Y);
    
    public static SKPoint Negate(this SKPoint a)
        => new(a.X * -1f, a.Y * -1f);
    
    public static SKPoint Floor(this SKPoint a)
        => new((float)Math.Floor(a.X), (float)Math.Floor(a.Y));
    
    public static SKPointI ToInt(this SKPoint a)
        => new((int)a.X, (int)a.Y);

    public static bool Contains(this SKRectI rect, SKPoint p)
        => rect.Contains(p.ToInt());

    // load embedded resources (DO NOT USE IN A LOOP)
    public static SKBitmap LoadImage(string path, bool useSlashes = false)
    {
        if (useSlashes) 
            path = path.Replace('/', '.');
        
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Vidre.resources." + path);

        if (stream != null)
            return SKBitmap.Decode(stream);
        
        Debug.WriteLine($"[WARN] Failed to load image: {path}");
        return EmptyBitmap;
    }

    #region constants

    // should be in Config but it will have to implement Dispose() like here (which is already bad practice)
    public static readonly SKPaint ShadowPaint = new() {
        Color = new SKColor(0, 0, 0, 100),
        IsAntialias = true,
        ImageFilter = SKImageFilter.CreateBlur(5, 5)
    };
    
    private static readonly SKBitmap EmptyBitmap = new(1, 1); // used as fallback

    public static readonly double KeyRepeatDelay = 0.5; // delay before starting to repeat (usually 500ms)
    public static readonly double KeyRepeatInterval = 1.0 / 30.0; // interval between repeats after the delay (usually 33ms)

    #endregion

    #region utils to save/load

    public static void SetIfConfigured(this JsonElement root, string objName, ref int obj)
    {
        if (root.TryGetProperty(objName, out var p))
            obj = p.GetInt32();
    }

    public static void SetIfConfigured(this JsonElement root, string objName, ref bool obj)
    {
        if (root.TryGetProperty(objName, out var p))
            obj = p.GetBoolean();
    }

    public static void SetIfConfigured(this JsonElement root, string objName, ref string? obj)
    {
        if (root.TryGetProperty(objName, out var p))
        {
            var s = p.GetString();
            if (s != null)
                obj = s;
        }
    }

    public static void SetIfConfigured<T>(this JsonElement root, string key, ref T? obj) where T : struct, Enum
    {
        if (root.TryGetProperty(key, out var p))
        {
            var s = p.GetString();
            if (Enum.TryParse<T>(s, out var parsedEnum))
                obj = parsedEnum;
        }
    }

    #endregion

    #region cross-platform clipboard get and set (images)

    // cross-platform method to get image from clipboard as skbitmap
    public static SKBitmap? GetImageFromClipboard()
    {
#if Windows
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsClipboard();
        }
#endif
        if (OperatingSystem.IsMacOS())
        {
            return GetCommandOutput("pbpaste", "-ProposedAnisType public.png");
        }
        if (OperatingSystem.IsLinux())
        {
            // try wayland and then x11
            var img = GetCommandOutput("wl-paste", "-t image/png");
            return img ?? GetCommandOutput("xclip", "-selection clipboard -t image/png -o");
        }
        return null;
    }

    // cross-platform method to copy a skbitmap to clipboard (as png because it's lossless and easily shareable)
    public static void SetImageToClipboard(SKBitmap bitmap)
    {
#if Windows
        if (OperatingSystem.IsWindows())
        {
            SetWindowsClipboard(bitmap);
            return;
        }
#endif

        using var ms = new MemoryStream();

        // if encoding fails
        if (!bitmap.Encode(ms, SKEncodedImageFormat.Png, 100))
        {
            Debug.WriteLine("[WARN] Failed to encode SKBitmap to PNG."); // is warn necessary?
            return;
        }
        
        byte[] bytes = ms.ToArray();

        if (OperatingSystem.IsMacOS())
        {
            SendCommandInput("pbcopy", "", bytes);
        }
        else if (OperatingSystem.IsLinux())
        {
            // try wayland and then x11
            if (!SendCommandInput("wl-copy", "-t image/png", bytes))
                SendCommandInput("xclip", "-selection clipboard -t image/png -i", bytes);
        }
    }

#if Windows

    // windows native libraries
    private const uint CF_DIB = 8;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private static SKBitmap? GetWindowsClipboard()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;

        try
        {
            IntPtr hBlob = GetClipboardData(CF_DIB);
            if (hBlob == IntPtr.Zero) return null;

            IntPtr pBlob = GlobalLock(hBlob);
            if (pBlob == IntPtr.Zero) return null;

            try
            {
                using var data = SKData.Create(pBlob, 0);
                if (data == null) return null;
                
                return SKBitmap.Decode(data);
            }
            finally
            {
                GlobalUnlock(hBlob);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void SetWindowsClipboard(SKBitmap bitmap)
    {
        // the most secure way for Windows is to convert the image to bmp and use it as a temporary file or stream.
        
        using var ms = new MemoryStream();
        if (!bitmap.Encode(ms, SKEncodedImageFormat.Png, 100)) return;
        string base64 = Convert.ToBase64String(ms.ToArray());

        // convert Base64 string to image
        // should never fail since it requires no dependency
        string cmd = $"[System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms'); [System.Windows.Forms.Clipboard]::SetImage([System.Drawing.Image]::FromStream([System.IO.MemoryStream]::new([System.Convert]::FromBase64String('{base64}'))))";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[WARN] PowerShell Clipboard Set failed: {e.Message}");
        }
    }
#endif

    // to easily implement get image from clipboard for cross-platform as skbitmap
    private static SKBitmap? GetCommandOutput(string cmd, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            using var ms = new MemoryStream();

            p?.StandardOutput.BaseStream.CopyTo(ms);
            p?.WaitForExit();

            if (ms.Length == 0)
                return null;
            
            ms.Position = 0;

            return SKBitmap.Decode(ms);
        }
        catch { return null; }
    }

    // to easily run a command cross-platform and return success, for set image to clipboard
    private static bool SendCommandInput(string cmd, string args, byte[] data)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo {
                FileName = cmd,
                Arguments = args,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (p == null)
                return false;
            
            p.StandardInput.BaseStream.Write(data, 0, data.Length);
            p.StandardInput.Close();
            p.WaitForExit();

            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    #endregion

    #region file dialog utils

    private static void SetDefaultPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
            Config.DefaultDialogPath = directory;
    }

    // NOTE: async avoids the main window to wait for a file to be chosen

    // list of encodable/decodable supported formats
    private static NativeFileDialog CreateFileDialog(bool isSave)
    {
        var dialog = new NativeFileDialog();

        if (isSave)
            dialog = dialog.SaveFile();
        else
            dialog = dialog.SelectFile();

        return dialog
            .AddFilter("All Images", "png,jpg,jpeg,webp,bmp,ico")
            .AddFilter("PNG Image", "png")
            .AddFilter("JPEG Image", "jpg,jpeg")
            .AddFilter("WebP Image", "webp")
            .AddFilter("BMP Image", "bmp")
            .AddFilter("ICO Icon", "ico");
            // .AddFilter("All Files", "*"); // usually automatic from OS
    }

    public static SKEncodedImageFormat GetImageFormat(string path)
    {
        var extension = Path.GetExtension(path).ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            ".bmp" => SKEncodedImageFormat.Bmp,
            ".ico" => SKEncodedImageFormat.Ico,
            _ => SKEncodedImageFormat.Png // default fallback
        };
    }

    // async
    public static Task<string?> ShowSaveFileDialog(string? initialPath = null)
        => Task.Run(() =>
        {
            using var dialog = CreateFileDialog(isSave: true);

            // default save name is image.png
            var result = dialog.Open(out string[]? output, initialPath);

            // if OK and array is valid...
            if (result == DialogResult.Okay && output != null && output.Length > 0)
            {
                var file = output[0];

                SetDefaultPath(file);
                return file;
            }

            return null;
        });

    // async
    public static Task<string?> ShowOpenFileDialog(string? initialPath = null)
        => Task.Run(() =>
        {
            using var dialog = CreateFileDialog(isSave: false);

            var result = dialog.Open(out string[]? output, initialPath);

            // if OK and array is valid...
            if (result == DialogResult.Okay && output != null && output.Length > 0)
            {
                var file = output[0];
                
                SetDefaultPath(file);
                return file;
            }

            return null;
        });

    #endregion

    public static void Dispose()
    {
        EmptyBitmap?.Dispose();
        ShadowPaint?.Dispose();
    }
}