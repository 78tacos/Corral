using System;
using System.IO;
using Corral;

namespace CorralTests;

/// <summary>
/// Organize desktop must not re-import a shortcut that a fence already holds as a stored copy.
/// A .url file is plain text, so this does not need WScript.Shell. Matching is byte-for-byte:
/// the same file name, or the same URL target with different bytes, is still a different shortcut.
/// A regular file is never matched this way.
/// </summary>
internal static class ShortcutImportTests
{
    public static void Run(Action<string, bool, string> check)
    {
        string work = Path.Combine(Path.GetTempPath(), "corral-import-" + Guid.NewGuid().ToString("N"));
        string desktop = Path.Combine(work, "desktop");
        string root = Path.Combine(work, "Links");
        Directory.CreateDirectory(desktop);
        try
        {
            var store = new LinkStore(root);
            const string body = "[InternetShortcut]\r\nURL=https://example.com/\r\n";
            string url = Path.Combine(desktop, "Site.url");
            File.WriteAllText(url, body);

            string copy = store.Adopt(url, Array.Empty<string>());
            string[] held = { copy };

            string? found = store.FindStoredCopy(url, held);
            check("a desktop .url with the same bytes as a stored copy is already imported",
                found == copy, found ?? "null");

            string reused = store.Adopt(url, held);
            check("adopting that .url again reuses the stored copy",
                reused == copy && Directory.GetFiles(root).Length == 1, reused);

            // Same URL target, extra field: a target compare would skip this. Byte compare must not.
            string sameTarget = Path.Combine(desktop, "Site-extra.url");
            File.WriteAllText(sameTarget, body + "IconIndex=0\r\n");
            check("a .url with the same target but different bytes is not already imported",
                store.FindStoredCopy(sameTarget, held) == null, "");

            string alias = Path.Combine(desktop, "Alias.url");
            File.WriteAllText(alias, body);
            check("a differently named .url with the same bytes is already imported",
                store.FindStoredCopy(alias, held) == copy, "");

            File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://elsewhere.example/\r\n");
            check("the same shortcut name with different bytes is not already imported",
                store.FindStoredCopy(url, held) == null, "");

            string notes = Path.Combine(desktop, "notes.txt");
            File.WriteAllText(notes, body);
            string notesStored = Path.Combine(root, "notes.txt");
            File.WriteAllText(notesStored, body);
            check("a regular file is not matched by stored bytes",
                store.FindStoredCopy(notes, new[] { notesStored }) == null, "");

            string loose = Path.Combine(desktop, "Loose.url");
            string looseTwin = Path.Combine(desktop, "Loose-copy.url");
            File.WriteAllText(loose, body);
            File.WriteAllText(looseTwin, body);
            check("an identical shortcut outside the link store is not already imported",
                store.FindStoredCopy(loose, new[] { looseTwin }) == null, "");
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }
}
