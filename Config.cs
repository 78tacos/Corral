using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>What a double-click on empty desktop does.</summary>
public enum DoubleClickMode
{
    /// <summary>Nothing (also turns off Shift + double-click).</summary>
    Off,
    /// <summary>Hide / show the fences. Your real desktop icons come back while the fences are hidden.</summary>
    Fences,
    /// <summary>Hide / show the real desktop icons only. The fences stay put.</summary>
    Icons,
    /// <summary>Hide / show the fences and the real desktop icons together (a clean desktop).</summary>
    Both,
}

public sealed class AppConfig
{
    public bool Locked { get; set; }
    public bool HideDesktopIcons { get; set; }
    public bool SnapToEdges { get; set; } = true;
    public bool PeekOnHover { get; set; } = true;

    /// <summary>
    /// What a plain double-click on empty desktop does. Shift + double-click always toggles the real desktop
    /// icons on their own (unless this is Off). Null only while an older file is being read; see <see cref="Normalize"/>.
    /// </summary>
    public DoubleClickMode? DoubleClickAction { get; set; }

    // Settings from versions 1.0 and 1.1. They are read so an old file upgrades, and never written back.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DoubleClickDesktop { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DoubleClickHidesIcons { get; set; }

    /// <summary>Background tint strength, 0-100.</summary>
    public int Opacity { get; set; } = 55;
    public string Tint { get; set; } = "#12141A";

    public List<FenceData> Fences { get; set; } = new();

    /// <summary>Fills in settings that older files did not have and converts the legacy double-click booleans.</summary>
    public void Normalize()
    {
        if (DoubleClickAction == null)
        {
            DoubleClickAction = DoubleClickDesktop == false ? DoubleClickMode.Off
                : DoubleClickHidesIcons == true ? DoubleClickMode.Both
                : DoubleClickMode.Fences;
        }
        DoubleClickDesktop = null;
        DoubleClickHidesIcons = null;
    }
}

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

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
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path), Json) ?? new AppConfig();
                loaded.Normalize();
                return loaded;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Could not read config, starting fresh", ex);
            try { File.Copy(Path, Path + ".bad", true); } catch { /* best effort */ }
        }
        var fresh = new AppConfig();
        fresh.Normalize();
        return fresh;
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
