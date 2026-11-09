using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Corral;

namespace CorralTests;

/// <summary>
/// LinkStore checks. Creating a <c>.lnk</c> needs <c>WScript.Shell</c>; the other checks do not.
/// The name arrays are the list both the runner and the skip path use, in the order the checks run.
/// </summary>
internal static class LinkStoreChecks
{
    internal const string SkipReason = "WScript.Shell is unavailable; .lnk shortcut setup was not run";

    internal static readonly string[] LnkCheckNames =
    {
        "shortcut is copied into the store",
        "copy is a different file from the original",
        "original is gone",
        "copy still exists after the original is deleted",
        "copy is untouched by the deletion",
        "copy still points at the same target",
        "an already-stored path is returned unchanged",
        "the same shortcut added to the same fence reuses the copy",
        "...and made no extra file",
        "a separately created shortcut to the same target is kept separately",
        "different shortcut with the same name gets its own copy",
        "...named with a number suffix",
        "a third collision becomes (3)",
        "copy of a read-only original is not read-only",
        "a sibling folder with the same prefix is not 'stored'",
        "Discard removes a stored copy",
        "Discard never touches files outside the store",
        "if the copy cannot be made, the original path is kept",
    };

    internal static readonly string[] ShellFreeCheckNames =
    {
        "a regular file is kept as a reference",
        "a folder is kept as a reference",
        "a missing shortcut is kept as-is (nothing to copy)",
        ".url shortcuts are copied too",
        "ResolveTarget ignores non-.lnk files",
        "a path using .. to escape the store is not 'stored'",
        "Discard of a missing copy is harmless",
    };

    /// <summary>
    /// True when Windows scripting can create a <c>.lnk</c>. The ProgID is missing on Linux,
    /// and a failed activation means shortcut setup is unavailable there too.
    /// </summary>
    internal static bool CanCreateLnk()
    {
        object? shell = null;
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return false;
            shell = Activator.CreateInstance(shellType);
            return shell != null;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (shell != null && Marshal.IsComObject(shell))
            {
                try { Marshal.FinalReleaseComObject(shell); }
                catch (Exception) { /* The probe only cares that activation succeeded. */ }
            }
        }
    }

    internal static void SkipRequiringLnk(CheckRunner runner)
    {
        foreach (string name in LnkCheckNames)
            runner.Skip(name, SkipReason);
    }

    internal static void RunWithoutLnk(CheckRunner runner)
    {
        int index = 0;
        void Next(bool ok, string detail = "") => CheckNext(runner, ShellFreeCheckNames, ref index, ok, detail);

        string work = Path.Combine(Path.GetTempPath(), "corral-linkstore-" + Guid.NewGuid().ToString("N"));
        string desktop = Path.Combine(work, "desktop");
        string root = Path.Combine(work, "Links");
        Directory.CreateDirectory(desktop);
        try
        {
            var store = new LinkStore(root);
            var recycled = new List<string>();
            store.Recycle = p => { recycled.Add(p); File.Delete(p); return true; };

            string textFile = Path.Combine(desktop, "notes.txt");
            File.WriteAllText(textFile, "hello");
            Next(store.Adopt(textFile, Array.Empty<string>()) == textFile);
            Next(store.Adopt(desktop, Array.Empty<string>()) == desktop);
            string missing = Path.Combine(desktop, "gone.lnk");
            Next(store.Adopt(missing, Array.Empty<string>()) == missing);

            string url = Path.Combine(desktop, "Site.url");
            File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.com\r\n");
            string urlCopy = store.Adopt(url, Array.Empty<string>());
            Next(store.IsStored(urlCopy) && File.Exists(urlCopy));
            Next(LinkStore.ResolveTarget(urlCopy) == null);

            Next(!store.IsStored(Path.Combine(root, "..", "desktop", "notes.txt")));

            store.Discard(Path.Combine(root, "does-not-exist.lnk"));
            Next(recycled.Count == 0);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }

        ExpectCount(index, ShellFreeCheckNames.Length, "LinkStore checks that do not need .lnk");
    }

    internal static void RunRequiringLnk(CheckRunner runner)
    {
        int index = 0;
        void Next(bool ok, string detail = "") => CheckNext(runner, LnkCheckNames, ref index, ok, detail);

        string work = Path.Combine(Path.GetTempPath(), "corral-linkstore-" + Guid.NewGuid().ToString("N"));
        string desktop = Path.Combine(work, "desktop");
        string root = Path.Combine(work, "Links");
        Directory.CreateDirectory(desktop);

        try
        {
            string notepad = Path.Combine(Environment.SystemDirectory, "..", "notepad.exe");
            notepad = Path.GetFullPath(notepad);

            var store = new LinkStore(root);
            var recycled = new List<string>();
            store.Recycle = p => { recycled.Add(p); File.Delete(p); return true; };

            // ---- the reported scenario: delete the original, the fence copy must survive ------------------
            string original = MakeLnk(desktop, "Notepad.lnk", notepad);
            string kept = store.Adopt(original, Array.Empty<string>());
            Next(store.IsStored(kept) && File.Exists(kept), kept);
            Next(!string.Equals(kept, original, StringComparison.OrdinalIgnoreCase));
            byte[] before = File.ReadAllBytes(kept);

            File.Delete(original);
            Next(!File.Exists(original));
            Next(File.Exists(kept));
            Next(File.ReadAllBytes(kept).SequenceEqual(before));
            Next(string.Equals(LinkStore.ResolveTarget(kept), notepad, StringComparison.OrdinalIgnoreCase),
                LinkStore.ResolveTarget(kept) ?? "null");

            // Kept for the discard check below. The "regular file" assertion itself runs without a .lnk.
            string textFile = Path.Combine(desktop, "notes.txt");
            File.WriteAllText(textFile, "hello");
            Next(store.Adopt(kept, Array.Empty<string>()) == kept);

            // ---- duplicates and name collisions ---------------------------------------------------------------------
            // The same original dropped again is byte-identical to the copy we made from it.
            string again = Path.Combine(desktop, "Notepad.lnk");
            File.Copy(kept, again);
            string reused = store.Adopt(again, new[] { kept });
            Next(reused == kept, reused);
            Next(Directory.GetFiles(root).Length == 1, Directory.GetFiles(root).Length.ToString());

            // A separately created shortcut is a different file (Windows stamps each one), even with the same target.
            string lookalike = MakeLnk(desktop, "Notepad2.lnk", notepad);
            string lookalikeKept = store.Adopt(lookalike, new[] { kept });
            Next(lookalikeKept != kept && File.Exists(lookalikeKept));
            store.Discard(lookalikeKept);

            string other = MakeLnk(desktop, "Notepad.lnk", Path.Combine(Environment.SystemDirectory, "calc.exe"));
            string second = store.Adopt(other, new[] { kept });
            Next(second != kept && File.Exists(second), second);
            Next(Path.GetFileName(second) == "Notepad (2).lnk", Path.GetFileName(second));

            string third = store.Adopt(MakeLnk(desktop, "Notepad.lnk", Path.Combine(Environment.SystemDirectory, "mspaint.exe")), new[] { kept, second });
            Next(Path.GetFileName(third) == "Notepad (3).lnk", Path.GetFileName(third));

            // ---- read-only originals ------------------------------------------------------------------------------
            string ro = MakeLnk(desktop, "ReadOnly.lnk", notepad);
            File.SetAttributes(ro, FileAttributes.ReadOnly);
            string roCopy = store.Adopt(ro, Array.Empty<string>());
            Next((File.GetAttributes(roCopy) & FileAttributes.ReadOnly) == 0);
            File.SetAttributes(ro, FileAttributes.Normal);

            // ---- IsStored must not be fooled by look-alike folders --------------------------------------------------
            string sibling = Path.Combine(work, "Links-other");
            Directory.CreateDirectory(sibling);
            string impostor = MakeLnk(sibling, "X.lnk", notepad);
            Next(!store.IsStored(impostor));

            // ---- discard -------------------------------------------------------------------------------------------
            recycled.Clear();
            store.Discard(second);
            Next(!File.Exists(second) && recycled.Count == 1);

            recycled.Clear();
            store.Discard(textFile);
            store.Discard(impostor);
            Next(File.Exists(textFile) && File.Exists(impostor) && recycled.Count == 0);

            // ---- failure fallback ---------------------------------------------------------------------------------
            string blocker = Path.Combine(work, "not-a-folder");
            File.WriteAllText(blocker, "i am a file, so a store rooted here cannot be created");
            var broken = new LinkStore(blocker);
            string fallbackSource = MakeLnk(desktop, "Fallback.lnk", notepad);
            Next(broken.Adopt(fallbackSource, Array.Empty<string>()) == fallbackSource);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }

        ExpectCount(index, LnkCheckNames.Length, "LinkStore .lnk checks");
    }

    /// <summary>Creates a real .lnk with the same COM object Windows scripting uses.</summary>
    private static string MakeLnk(string dir, string name, string target)
    {
        string path = Path.Combine(dir, name);
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(path);
        link.TargetPath = target;
        link.Save();
        return path;
    }

    private static void CheckNext(CheckRunner runner, string[] names, ref int index, bool ok, string detail)
    {
        if (index >= names.Length)
            throw new InvalidOperationException("more LinkStore checks than names");
        runner.Check(names[index], ok, detail);
        index++;
    }

    private static void ExpectCount(int actual, int expected, string group)
    {
        if (actual != expected)
            throw new InvalidOperationException($"{group} recorded {actual}, expected {expected}.");
    }
}
