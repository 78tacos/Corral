using System;
using System.IO;
using System.Text.Json;
using Corral;

namespace CorralTests;

/// <summary>Settings-file behavior: upgrading old files, round-tripping, and surviving bad input.</summary>
internal static class ConfigTests
{
    public static void Run(Action<string, bool, string> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), "corral-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "config.json");
            var store = new ConfigStore(file);

            AppConfig LoadFrom(string json)
            {
                File.WriteAllText(file, json);
                return store.Load();
            }

            // ---- upgrading files written by 1.0 / 1.1 (two booleans) ---------------------------------------------
            check("legacy: desktop on, icons off -> Fences",
                LoadFrom("{\"DoubleClickDesktop\":true,\"DoubleClickHidesIcons\":false}").DoubleClickAction == DoubleClickMode.Fences, "");
            check("legacy: desktop on, icons on -> Both",
                LoadFrom("{\"DoubleClickDesktop\":true,\"DoubleClickHidesIcons\":true}").DoubleClickAction == DoubleClickMode.Both, "");
            check("legacy: desktop off -> Off (even if icons flag is on)",
                LoadFrom("{\"DoubleClickDesktop\":false,\"DoubleClickHidesIcons\":true}").DoubleClickAction == DoubleClickMode.Off, "");
            check("a file with no double-click settings at all -> Fences",
                LoadFrom("{\"Opacity\":40}").DoubleClickAction == DoubleClickMode.Fences, "");
            check("no file at all -> Fences", NoFile(store, file).DoubleClickAction == DoubleClickMode.Fences, "");

            // ---- the new setting wins and other settings survive ----------------------------------------------------
            var icons = LoadFrom("{\"DoubleClickAction\":\"Icons\",\"DoubleClickDesktop\":false,\"Opacity\":40,\"HideDesktopIcons\":true}");
            check("new setting is honored over legacy keys", icons.DoubleClickAction == DoubleClickMode.Icons, icons.DoubleClickAction.ToString() ?? "null");
            check("other settings are untouched by the upgrade", icons.Opacity == 40 && icons.HideDesktopIcons, "");
            check("legacy fields are cleared after upgrade", icons.DoubleClickDesktop == null && icons.DoubleClickHidesIcons == null, "");

            // ---- the real shape of an old file, including fences ------------------------------------------------------
            var old = LoadFrom("""
                {
                  "Locked": false, "HideDesktopIcons": true, "SnapToEdges": true, "PeekOnHover": true,
                  "DoubleClickDesktop": true, "DoubleClickHidesIcons": true,
                  "Opacity": 55, "Tint": "#12141A",
                  "Fences": [ { "Title": "My Apps", "X": 10, "Y": 20, "Width": 300, "Height": 200, "RolledUp": true,
                                "IconSize": 48, "Items": [ "C:\\a.lnk", "C:\\b.lnk" ] } ]
                }
                """);
            check("a full 1.1 file: action upgraded to Both", old.DoubleClickAction == DoubleClickMode.Both, "");
            check("a full 1.1 file: fences kept intact",
                old.Fences.Count == 1 && old.Fences[0].Title == "My Apps" && old.Fences[0].Items.Count == 2 && old.Fences[0].RolledUp, "");

            // ---- saving ---------------------------------------------------------------------------------------------
            store.Save(old);
            string written = File.ReadAllText(file);
            check("saved as a readable name, not a number", written.Contains("\"DoubleClickAction\": \"Both\""), "");
            check("legacy keys are not written back", !written.Contains("DoubleClickDesktop") && !written.Contains("DoubleClickHidesIcons"), "");

            foreach (DoubleClickMode mode in Enum.GetValues<DoubleClickMode>())
            {
                var c = new AppConfig { DoubleClickAction = mode };
                store.Save(c);
                check($"round trip: {mode}", store.Load().DoubleClickAction == mode, "");
            }

            // ---- bad input ---------------------------------------------------------------------------------------------
            var bad = LoadFrom("{\"DoubleClickAction\":\"Sideways\"}");
            check("an unknown mode name does not crash; defaults are used", bad.DoubleClickAction == DoubleClickMode.Fences, "");
            check("...and the unreadable file is kept as config.json.bad", File.Exists(file + ".bad"), "");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private static AppConfig NoFile(ConfigStore store, string file)
    {
        if (File.Exists(file)) File.Delete(file);
        return store.Load();
    }
}
