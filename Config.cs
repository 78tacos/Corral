using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Corral;

public sealed class FenceData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New Fence";

    // Physical screen pixels (virtual desktop coordinates).
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 240;

    public bool RolledUp { get; set; }
    public int IconSize { get; set; } = 48;

    /// <summary>Per-fence tint (#RRGGBB). Null means use the app default.</summary>
    public string? Tint { get; set; }

    /// <summary>When set, the fence mirrors this folder instead of holding its own item list.</summary>
    public string? PortalPath { get; set; }

    public List<string> Items { get; set; } = new();
}

public sealed class AppConfig
{
    public bool Locked { get; set; }
    public bool HideDesktopIcons { get; set; }
    public bool SnapToEdges { get; set; } = true;
    public bool PeekOnHover { get; set; } = true;

    /// <summary>Double-click empty desktop to hide/show all fences.</summary>
    public bool DoubleClickDesktop { get; set; } = true;

    /// <summary>When a desktop double-click hides the fences, hide the real desktop icons too (a fully clean desktop).</summary>
    public bool DoubleClickHidesIcons { get; set; }

    /// <summary>Background tint strength, 0-100.</summary>
    public int Opacity { get; set; } = 55;
    public string Tint { get; set; } = "#12141A";

    public List<FenceData> Fences { get; set; } = new();
}

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Path { get; }

    public ConfigStore(string? path)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Corral", "config.json");
    }

    public string Directory => System.IO.Path.GetDirectoryName(Path)!;

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path), Json) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            Log.Write("Could not read config, starting fresh", ex);
            try { File.Copy(Path, Path + ".bad", true); } catch { /* best effort */ }
        }
        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(config, Json));
            File.Move(tmp, Path, true);
        }
        catch (Exception ex)
        {
            Log.Write("Could not save config", ex);
        }
    }
}

internal static class Log
{
    public static string? FilePath { get; set; }

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            if (FilePath == null) return;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"{DateTime.Now:s} {message}{(ex != null ? "\n" + ex : "")}\n");
        }
        catch { /* logging must never throw */ }
    }
}
