using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Corral;

namespace CorralTests;

internal static class Program
{
    private static int _passed, _failed;

    internal static void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++; else _failed++;
        Console.WriteLine("{0,-4} {1}{2}", ok ? "PASS" : "FAIL", name, ok || detail == "" ? "" : "  -> " + detail);
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

    private static int Main()
    {
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
            Check("shortcut is copied into the store", store.IsStored(kept) && File.Exists(kept), kept);
            Check("copy is a different file from the original", !string.Equals(kept, original, StringComparison.OrdinalIgnoreCase));
            byte[] before = File.ReadAllBytes(kept);

            File.Delete(original);
            Check("original is gone", !File.Exists(original));
            Check("copy still exists after the original is deleted", File.Exists(kept));
            Check("copy is untouched by the deletion", File.ReadAllBytes(kept).SequenceEqual(before));
            Check("copy still points at the same target", string.Equals(LinkStore.ResolveTarget(kept), notepad, StringComparison.OrdinalIgnoreCase),
                LinkStore.ResolveTarget(kept) ?? "null");

            // ---- not everything is copied -------------------------------------------------------------------------
            string textFile = Path.Combine(desktop, "notes.txt");
            File.WriteAllText(textFile, "hello");
            Check("a regular file is kept as a reference", store.Adopt(textFile, Array.Empty<string>()) == textFile);
            Check("a folder is kept as a reference", store.Adopt(desktop, Array.Empty<string>()) == desktop);
            string missing = Path.Combine(desktop, "gone.lnk");
            Check("a missing shortcut is kept as-is (nothing to copy)", store.Adopt(missing, Array.Empty<string>()) == missing);
            Check("an already-stored path is returned unchanged", store.Adopt(kept, Array.Empty<string>()) == kept);

            // ---- duplicates and name collisions ---------------------------------------------------------------------
            // The same original dropped again is byte-identical to the copy we made from it.
            string again = Path.Combine(desktop, "Notepad.lnk");
            File.Copy(kept, again);
            string reused = store.Adopt(again, new[] { kept });
            Check("the same shortcut added to the same fence reuses the copy", reused == kept, reused);
            Check("...and made no extra file", Directory.GetFiles(root).Length == 1, Directory.GetFiles(root).Length.ToString());

            // A separately created shortcut is a different file (Windows stamps each one), even with the same target.
            string lookalike = MakeLnk(desktop, "Notepad2.lnk", notepad);
            string lookalikeKept = store.Adopt(lookalike, new[] { kept });
            Check("a separately created shortcut to the same target is kept separately", lookalikeKept != kept && File.Exists(lookalikeKept));
            store.Discard(lookalikeKept);

            string other = MakeLnk(desktop, "Notepad.lnk", Path.Combine(Environment.SystemDirectory, "calc.exe"));
            string second = store.Adopt(other, new[] { kept });
            Check("different shortcut with the same name gets its own copy", second != kept && File.Exists(second), second);
            Check("...named with a number suffix", Path.GetFileName(second) == "Notepad (2).lnk", Path.GetFileName(second));

            string third = store.Adopt(MakeLnk(desktop, "Notepad.lnk", Path.Combine(Environment.SystemDirectory, "mspaint.exe")), new[] { kept, second });
            Check("a third collision becomes (3)", Path.GetFileName(third) == "Notepad (3).lnk", Path.GetFileName(third));

            // ---- read-only originals ------------------------------------------------------------------------------
            string ro = MakeLnk(desktop, "ReadOnly.lnk", notepad);
            File.SetAttributes(ro, FileAttributes.ReadOnly);
            string roCopy = store.Adopt(ro, Array.Empty<string>());
            Check("copy of a read-only original is not read-only", (File.GetAttributes(roCopy) & FileAttributes.ReadOnly) == 0);
            File.SetAttributes(ro, FileAttributes.Normal);

            // ---- other shortcut kinds ---------------------------------------------------------------------------
            string url = Path.Combine(desktop, "Site.url");
            File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.com\r\n");
            string urlCopy = store.Adopt(url, Array.Empty<string>());
            Check(".url shortcuts are copied too", store.IsStored(urlCopy) && File.Exists(urlCopy));
            Check("ResolveTarget ignores non-.lnk files", LinkStore.ResolveTarget(urlCopy) == null);

            // ---- IsStored must not be fooled by look-alike folders --------------------------------------------------
            string sibling = Path.Combine(work, "Links-other");
            Directory.CreateDirectory(sibling);
            string impostor = MakeLnk(sibling, "X.lnk", notepad);
            Check("a sibling folder with the same prefix is not 'stored'", !store.IsStored(impostor));
            Check("a path using .. to escape the store is not 'stored'", !store.IsStored(Path.Combine(root, "..", "desktop", "notes.txt")));

            // ---- discard -------------------------------------------------------------------------------------------
            recycled.Clear();
            store.Discard(second);
            Check("Discard removes a stored copy", !File.Exists(second) && recycled.Count == 1);

            recycled.Clear();
            store.Discard(textFile);
            store.Discard(impostor);
            Check("Discard never touches files outside the store", File.Exists(textFile) && File.Exists(impostor) && recycled.Count == 0);

            store.Discard(Path.Combine(root, "does-not-exist.lnk"));
            Check("Discard of a missing copy is harmless", recycled.Count == 0);

            // ---- failure fallback ---------------------------------------------------------------------------------
            string blocker = Path.Combine(work, "not-a-folder");
            File.WriteAllText(blocker, "i am a file, so a store rooted here cannot be created");
            var broken = new LinkStore(blocker);
            string fallbackSource = MakeLnk(desktop, "Fallback.lnk", notepad);
            Check("if the copy cannot be made, the original path is kept", broken.Adopt(fallbackSource, Array.Empty<string>()) == fallbackSource);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }

        ConfigTests.Run(Check);

        Console.WriteLine();
        Console.WriteLine("{0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }
}
