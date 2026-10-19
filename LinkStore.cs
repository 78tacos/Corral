using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Corral;

/// <summary>
/// Corral's own copies of shortcut files.
///
/// When a shortcut (.lnk, .url, .appref-ms) is put into a fence it is copied into <see cref="Root"/>, and the
/// fence refers to the copy. That way deleting or moving the original (say, the icon on the desktop) cannot
/// break the fence. Real files and folders are not copied: they are large, and a second copy would silently
/// drift from the original, so fences keep pointing at them.
/// </summary>
public sealed class LinkStore
{
    private static readonly HashSet<string> ShortcutExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url", ".appref-ms" };

    private const long MaxCompareBytes = 1024 * 1024;

    /// <summary>Folder that holds the copies (normally %APPDATA%\Corral\Links).</summary>
    public string Root { get; }

    /// <summary>How a stored copy is discarded. Defaults to the Recycle Bin; tests replace it.</summary>
    public Func<string, bool> Recycle { get; set; } = RecycleToBin;

    public LinkStore(string rootDirectory)
    {
        Root = Path.GetFullPath(rootDirectory);
    }

    /// <summary>True for an existing shortcut file (not a folder, not a regular file).</summary>
    public static bool IsShortcut(string path) =>
        ShortcutExtensions.Contains(Path.GetExtension(path)) && File.Exists(path);

    /// <summary>True when the path is one of Corral's own copies.</summary>
    public bool IsStored(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            return full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the path a fence should keep for <paramref name="source"/>: a private copy for shortcuts, the
    /// original path for everything else (or if copying fails). <paramref name="existing"/> are the paths the
    /// fence already holds, so adding the same shortcut twice reuses the copy instead of making another.
    /// </summary>
    public string Adopt(string source, IEnumerable<string> existing)
    {
        if (!IsShortcut(source) || IsStored(source)) return source;

        foreach (string held in existing)
        {
            if (IsStored(held) && File.Exists(held) && SameContent(held, source))
                return held;
        }

        try
        {
            Directory.CreateDirectory(Root);
            string dest = UniquePath(Path.GetFileName(source));
            File.Copy(source, dest, overwrite: false);
            File.SetAttributes(dest, FileAttributes.Normal); // a read-only original must not make the copy undeletable
            return dest;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Could not copy shortcut into the link store: " + source, ex);
            return source; // better a reference than nothing
        }
    }

    /// <summary>Removes one of Corral's copies. Anything that is not a stored copy is left alone.</summary>
    public void Discard(string path)
    {
        if (!IsStored(path) || !File.Exists(path)) return;
        try
        {
            if (!Recycle(path)) Log.Write("Could not discard stored shortcut: " + path);
        }
        catch (Exception ex)
        {
            Log.Write("Could not discard stored shortcut: " + path, ex);
        }
    }

    /// <summary>
    /// Where a shortcut points, or null. Used so "Open file location" shows the target's folder, as Explorer does.
    /// </summary>
    public static string? ResolveTarget(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic link = shell.CreateShortcut(path);
                string target = link.TargetPath;
                return string.IsNullOrWhiteSpace(target) ? null : target;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Could not resolve shortcut target: " + path, ex);
            return null;
        }
    }

    private string UniquePath(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        string candidate = Path.Combine(Root, fileName);
        for (int n = 2; File.Exists(candidate); n++)
            candidate = Path.Combine(Root, $"{name} ({n}){ext}");
        return candidate;
    }

    private static bool SameContent(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length || fa.Length > MaxCompareBytes) return false;
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool RecycleToBin(string path)
    {
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
            path,
            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        return !File.Exists(path);
    }
}
