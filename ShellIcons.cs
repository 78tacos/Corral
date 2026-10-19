using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Corral;

/// <summary>Loads shell icons/thumbnails on a dedicated STA thread so slow paths never block the UI.</summary>
internal static class ShellIcons
{
    private sealed record Job(string Path, int Size, TaskCompletionSource<BitmapSource?> Result);

    private static readonly BlockingCollection<Job> Queue = new();
    private static readonly ConcurrentDictionary<(string, int), BitmapSource> Cache = new();

    static ShellIcons()
    {
        var thread = new Thread(Worker) { IsBackground = true, Name = "ShellIcons" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static Task<BitmapSource?> GetIconAsync(string path, int size)
    {
        if (Cache.TryGetValue((path.ToLowerInvariant(), size), out var hit))
            return Task.FromResult<BitmapSource?>(hit);

        var tcs = new TaskCompletionSource<BitmapSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Add(new Job(path, size, tcs));
        return tcs.Task;
    }

    public static void ClearCache() => Cache.Clear();

    public static string GetDisplayName(string path)
    {
        string? name = Native.GetShellDisplayName(path);
        if (name != null) return name;
        string trimmed = path.TrimEnd('\\', '/');
        string file = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(file) ? trimmed : file;
    }

    private static void Worker()
    {
        foreach (var job in Queue.GetConsumingEnumerable())
        {
            BitmapSource? bmp = null;
            try
            {
                bmp = Load(job.Path, job.Size);
                if (bmp != null) Cache[(job.Path.ToLowerInvariant(), job.Size)] = bmp;
            }
            catch (Exception ex)
            {
                Log.Write("Icon load failed for " + job.Path, ex);
            }
            job.Result.SetResult(bmp);
        }
    }

    // Only pictures and video get real thumbnails (like the desktop); everything else gets its icon.
    private static readonly HashSet<string> ThumbnailExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff", ".ico",
        ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".pdf",
    };

    private static BitmapSource? Load(string path, int size)
    {
        Guid iid = typeof(Native.IShellItemImageFactory).GUID;
        if (Native.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0)
            return null;

        IntPtr hbm = IntPtr.Zero;
        try
        {
            const int IconOnly = 0x4;
            int flags = ThumbnailExt.Contains(Path.GetExtension(path)) ? 0 : IconOnly;
            if (factory.GetImage(new Native.SIZE { cx = size, cy = size }, flags, out hbm) != 0 || hbm == IntPtr.Zero)
                return null;

            byte[]? pixels = Native.ReadBitmapPixels(hbm, out int w, out int h);
            if (pixels == null || w == 0 || h == 0) return null;

            // Some thumbnail handlers hand back 32bpp bitmaps with no alpha at all.
            bool anyAlpha = false;
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0) { anyAlpha = true; break; }
            }
            if (!anyAlpha)
            {
                for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            }

            var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
            source.Freeze();
            return source;
        }
        finally
        {
            if (hbm != IntPtr.Zero) Native.DeleteObject(hbm);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(factory);
        }
    }
}
