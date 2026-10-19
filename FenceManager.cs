using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Corral;

/// <summary>Colour choices offered in the menus. All are dark enough for white text over the acrylic blur.</summary>
public static class FenceColors
{
    public static readonly (string Name, string Hex)[] Presets =
    {
        ("Graphite", "#12141A"),
        ("Slate", "#3A4660"),
        ("Blue", "#1B4FB8"),
        ("Teal", "#0E7C86"),
        ("Green", "#1F7A3E"),
        ("Gold", "#9A7B14"),
        ("Orange", "#C26A12"),
        ("Red", "#B02A2A"),
        ("Pink", "#B83D8A"),
        ("Purple", "#6A3DB8"),
    };

    public static bool Same(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Owns every fence window, the saved configuration, and the app-wide switches.</summary>
public sealed class FenceManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "Corral";

    private readonly ConfigStore _store;
    private readonly List<FenceWindow> _windows = new();
    private readonly DispatcherTimer _saveTimer;
    private bool _fencesVisible = true;
    private DesktopClickWatcher? _desktopWatcher;
    private bool _cleanDesktop;   // while the fences are hidden, the real desktop icons are hidden too

    public AppConfig Config { get; }
    public DragPayload? CurrentDrag { get; set; }
    public bool IsDraggingFromFence => CurrentDrag != null;
    public bool FencesVisible => _fencesVisible;
    public int FenceCount => _windows.Count;
    public string ConfigDirectory => _store.Directory;

    /// <summary>Corral's own copies of shortcuts, so fences never depend on the originals.</summary>
    public LinkStore Links { get; }

    public FenceManager(ConfigStore store)
    {
        _store = store;
        Config = store.Load();
        Links = new LinkStore(Path.Combine(store.Directory, "Links"));

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => SaveNow();

        SystemEvents.DisplaySettingsChanged += (_, _) =>
            Application.Current?.Dispatcher.BeginInvoke(KeepOnScreen);
    }

    // ======================================================================================
    // Lifecycle
    // ======================================================================================

    public void Start()
    {
        AdoptExistingShortcuts();

        foreach (var data in Config.Fences.ToList())
            Open(data);

        if (_windows.Count == 0)
            CreateFence("My Fence");

        KeepOnScreen();
        ApplyDesktopIcons();
        _desktopWatcher = new DesktopClickWatcher(() => Config.DoubleClickAction != DoubleClickMode.Off, OnDesktopDoubleClick);
    }

    public void Shutdown()
    {
        _desktopWatcher?.Dispose();
        SaveNow();
        // Never leave the user's desktop icons hidden once we're not around to show fences.
        if (!Native.AreDesktopIconsVisible()) Native.SetDesktopIconsVisible(true);
    }

    public void RequestSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        foreach (var w in _windows) w.CaptureBounds();
        _saveTimer.Stop(); // CaptureBounds re-arms it
        _store.Save(Config);
    }

    private FenceWindow Open(FenceData data)
    {
        var window = new FenceWindow(this, data);
        _windows.Add(window);
        window.Show();
        if (!_fencesVisible) window.Hide();
        return window;
    }

    // ======================================================================================
    // Creating / removing fences
    // ======================================================================================

    private static double SystemScale => Native.GetDpiForSystem() / 96.0;

    public FenceWindow CreateFence(string title = "New Fence", string? portalPath = null)
    {
        double s = SystemScale;
        var area = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                   ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

        int offset = (int)(34 * s) * (_windows.Count % 8);
        var data = new FenceData
        {
            Title = title,
            PortalPath = portalPath,
            Width = (int)(320 * s),
            Height = (int)(240 * s),
        };
        data.X = area.Left + (int)(80 * s) + offset;
        data.Y = area.Top + (int)(80 * s) + offset;

        Config.Fences.Add(data);
        var window = Open(data);
        RequestSave();
        return window;
    }

    public void CreatePortal()
    {
        var dlg = new OpenFolderDialog { Title = "Choose a folder to show in a portal fence" };
        if (dlg.ShowDialog() != true) return;
        string name = new DirectoryInfo(dlg.FolderName).Name;
        if (string.IsNullOrEmpty(name)) name = dlg.FolderName;
        CreateFence(name, dlg.FolderName);
    }

    public void RemoveFence(FenceWindow window)
    {
        var held = window.Data.PortalPath == null ? window.Data.Items.ToList() : new List<string>();
        _windows.Remove(window);
        Config.Fences.Remove(window.Data);
        window.Close();
        foreach (string path in held) DiscardIfUnused(path);
        RequestSave();
    }

    public void TransferItems(FenceWindow source, FenceWindow target, List<string> paths, int index)
    {
        target.AddPaths(paths, index);
        // The copies move with the items, so they must not be discarded from the source.
        if (!source.IsPortal) source.RemovePaths(paths, discard: false);
    }

    /// <summary>Discards a stored shortcut copy once no fence refers to it any more (to the Recycle Bin).</summary>
    public void DiscardIfUnused(string path)
    {
        bool inUse = Config.Fences.Any(f => f.PortalPath == null &&
            f.Items.Contains(path, StringComparer.OrdinalIgnoreCase));
        if (!inUse) Links.Discard(path);
    }

    /// <summary>
    /// One-time upgrade for fences saved before shortcuts were copied: any shortcut a fence still points at
    /// outside Corral's store is copied in now, while the original exists. Safe to run on every start.
    /// </summary>
    private void AdoptExistingShortcuts()
    {
        bool anyChanged = false;
        foreach (var fence in Config.Fences.Where(f => f.PortalPath == null))
        {
            var adopted = new List<string>();
            bool fenceChanged = false;
            foreach (string item in fence.Items)
            {
                string kept = Links.Adopt(item, adopted);
                if (!string.Equals(kept, item, StringComparison.OrdinalIgnoreCase)) fenceChanged = true;
                if (!adopted.Contains(kept, StringComparer.OrdinalIgnoreCase)) adopted.Add(kept);
                else fenceChanged = true; // collapsed a duplicate
            }
            if (fenceChanged)
            {
                fence.Items = adopted;
                anyChanged = true;
            }
        }
        if (anyChanged) RequestSave();
    }

    // ======================================================================================
    // Settings
    // ======================================================================================

    public void SetLocked(bool locked)
    {
        Config.Locked = locked;
        RequestSave();
    }

    public void SetSnap(bool snap)
    {
        Config.SnapToEdges = snap;
        RequestSave();
    }

    public void SetPeek(bool peek)
    {
        Config.PeekOnHover = peek;
        RequestSave();
    }

    public void SetOpacity(int percent)
    {
        Config.Opacity = percent;
        foreach (var w in _windows) w.ApplyBackdrop();
        RequestSave();
    }

    public void SetDesktopIconsHidden(bool hidden)
    {
        Config.HideDesktopIcons = hidden;
        ApplyDesktopIcons();
        RequestSave();
    }

    // Icons are hidden when the user asked for that while fences are showing. While the fences are tucked
    // away the real icons come back - unless the "clean desktop" option hid everything on purpose.
    private void ApplyDesktopIcons()
    {
        bool hide = _fencesVisible ? Config.HideDesktopIcons : _cleanDesktop;
        Native.SetDesktopIconsVisible(!hide);
    }

    /// <param name="alsoHideIcons">
    /// When this hides the fences, hide the real desktop icons too (a clean desktop). Showing the fences again
    /// always clears it. The tray commands pass false, so they never hide the real icons themselves.
    /// </param>
    public void ToggleFencesVisible(bool alsoHideIcons = false)
    {
        _fencesVisible = !_fencesVisible;
        _cleanDesktop = !_fencesVisible && alsoHideIcons;
        foreach (var w in _windows)
        {
            if (_fencesVisible) w.Show();
            else w.Hide();
        }
        ApplyDesktopIcons();
    }

    public void SetDoubleClickMode(DoubleClickMode mode)
    {
        Config.DoubleClickAction = mode;
        RequestSave();
    }

    /// <summary>A double-click landed on empty desktop. Shift always means "icons only".</summary>
    private void OnDesktopDoubleClick(bool shift)
    {
        if (shift)
        {
            ToggleDesktopIconsOnly();
            return;
        }

        switch (Config.DoubleClickAction)
        {
            case DoubleClickMode.Fences: ToggleFencesVisible(); break;
            case DoubleClickMode.Icons: ToggleDesktopIconsOnly(); break;
            case DoubleClickMode.Both: ToggleFencesVisible(alsoHideIcons: true); break;
        }
    }

    /// <summary>
    /// Flips the real desktop icons without touching the fences. With the fences showing it flips the saved
    /// "Hide desktop icons" setting (exactly like the tray checkbox); with the fences hidden it flips the temporary
    /// clean-desktop state, so icons and fences can each be toggled on their own at any time.
    /// </summary>
    public void ToggleDesktopIconsOnly()
    {
        if (_fencesVisible)
        {
            Config.HideDesktopIcons = !Config.HideDesktopIcons;
            RequestSave();
        }
        else
        {
            _cleanDesktop = !_cleanDesktop;
        }
        ApplyDesktopIcons();
    }

    /// <summary>The colour used by every fence that does not have its own.</summary>
    public void SetDefaultTint(string hex)
    {
        Config.Tint = hex;
        foreach (var w in _windows) w.ApplyBackdrop();
        RequestSave();
    }

    /// <summary>Set (or with null, clear) one fence's own colour.</summary>
    public void SetFenceTint(FenceWindow window, string? hex)
    {
        window.Data.Tint = hex;
        window.ApplyBackdrop();
        RequestSave();
    }

    public bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) != null;
        }
        set
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (value) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }

    // ======================================================================================
    // Layout helpers
    // ======================================================================================

    /// <summary>Snap a proposed rectangle to screen edges and neighbouring fences.</summary>
    public RECT Snap(FenceWindow self, RECT r, string mode, double scale)
    {
        if (!Config.SnapToEdges) return r;

        int threshold = (int)(10 * scale);
        int gap = (int)(8 * scale);
        var screen = System.Windows.Forms.Screen.FromRectangle(
            new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height)).WorkingArea;

        var lefts = new List<int> { screen.Left };
        var rights = new List<int> { screen.Right };
        var tops = new List<int> { screen.Top };
        var bottoms = new List<int> { screen.Bottom };

        foreach (var o in _windows.Where(w => w != self && w.IsVisible))
        {
            var or = o.GetRect();
            lefts.Add(or.Left); lefts.Add(or.Right + gap);
            rights.Add(or.Right); rights.Add(or.Left - gap);
            tops.Add(or.Top); tops.Add(or.Bottom + gap);
            bottoms.Add(or.Bottom); bottoms.Add(or.Top - gap);
        }

        bool move = mode == "M";

        if (move || mode.Contains('L') || mode.Contains('R'))
        {
            int dl = move || mode.Contains('L') ? Nearest(r.Left, lefts, threshold) : int.MaxValue;
            int dr = move || mode.Contains('R') ? Nearest(r.Right, rights, threshold) : int.MaxValue;
            int d = Math.Abs(dl) <= Math.Abs(dr) ? dl : dr;
            if (d != int.MaxValue)
            {
                if (move) r.Offset(d, 0);
                else if (d == dl && mode.Contains('L')) r.Left += d;
                else if (mode.Contains('R')) r.Right += d;
            }
        }

        if (move || mode.Contains('T') || mode.Contains('B'))
        {
            int dt = move || mode.Contains('T') ? Nearest(r.Top, tops, threshold) : int.MaxValue;
            int db = move || mode.Contains('B') ? Nearest(r.Bottom, bottoms, threshold) : int.MaxValue;
            int d = Math.Abs(dt) <= Math.Abs(db) ? dt : db;
            if (d != int.MaxValue)
            {
                if (move) r.Offset(0, d);
                else if (d == dt && mode.Contains('T')) r.Top += d;
                else if (mode.Contains('B')) r.Bottom += d;
            }
        }

        return r;
    }

    private static int Nearest(int edge, List<int> candidates, int threshold)
    {
        int best = int.MaxValue;
        foreach (int c in candidates)
        {
            int d = c - edge;
            if (Math.Abs(d) <= threshold && (best == int.MaxValue || Math.Abs(d) < Math.Abs(best)))
                best = d;
        }
        return best;
    }

    /// <summary>Pull any fence that ended up off every monitor (unplugged display) back onto the primary one.</summary>
    private void KeepOnScreen()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var primary = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        double s = SystemScale;

        foreach (var w in _windows)
        {
            var r = w.GetRect();
            var rect = new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
            if (screens.Any(sc => sc.WorkingArea.IntersectsWith(rect))) continue;

            Native.SetWindowPos(w.Hwnd, IntPtr.Zero, primary.Left + (int)(60 * s), primary.Top + (int)(60 * s),
                r.Width, r.Height, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            w.CaptureBounds();
        }
    }

    // ======================================================================================
    // Desktop import
    // ======================================================================================

    private static readonly string[] ProgramExt = { ".lnk", ".exe", ".url", ".appref-ms", ".bat", ".cmd", ".msi", ".ps1" };
    private static readonly string[] DocumentExt =
        { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".rtf", ".md", ".csv", ".odt", ".ods", ".one", ".json", ".xml" };
    private static readonly string[] MediaExt =
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".heic", ".mp4", ".mkv", ".mov", ".avi", ".mp3", ".wav", ".flac", ".m4a" };

    private static string Categorise(string path)
    {
        if (Directory.Exists(path)) return "Folders";
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ProgramExt.Contains(ext)) return "Programs";
        if (DocumentExt.Contains(ext)) return "Documents";
        if (MediaExt.Contains(ext)) return "Pictures & Media";
        return "Other";
    }

    public IReadOnlyList<string> GetDesktopEntries()
    {
        var already = new HashSet<string>(
            Config.Fences.Where(f => f.PortalPath == null).SelectMany(f => f.Items),
            StringComparer.OrdinalIgnoreCase);

        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };

        var result = new List<string>();
        foreach (string folder in folders.Where(f => !string.IsNullOrEmpty(f) && Directory.Exists(f)))
        {
            try
            {
                foreach (var info in new DirectoryInfo(folder).EnumerateFileSystemInfos())
                {
                    if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    if (info.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    if (already.Contains(info.FullName)) continue;
                    result.Add(info.FullName);
                }
            }
            catch (Exception ex)
            {
                Log.Write("Could not read " + folder, ex);
            }
        }
        return result;
    }

    /// <summary>
    /// Creates category fences from the current desktop contents. Shortcuts are copied into Corral's store;
    /// other files and folders are referenced. Nothing on the desktop is moved or deleted.
    /// Returns the number of items placed.
    /// </summary>
    public int OrganizeDesktop(IReadOnlyList<string> entries)
    {
        var groups = entries
            .GroupBy(Categorise)
            .OrderBy(g => Array.IndexOf(new[] { "Programs", "Folders", "Documents", "Pictures & Media", "Other" }, g.Key))
            .ToList();
        if (groups.Count == 0) return 0;

        double s = SystemScale;
        var area = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        int margin = (int)(20 * s), gap = (int)(14 * s);
        int cell = (int)(FenceWindow.CellWidthFor(48) * s);
        int cellH = (int)(92 * s);
        int x = area.Left + margin, y = area.Top + margin, rowH = 0;

        foreach (var g in groups)
        {
            var ordered = g.OrderBy(p => ShellIcons.GetDisplayName(p), StringComparer.CurrentCultureIgnoreCase);
            var paths = new List<string>();
            foreach (string source in ordered)
            {
                string kept = Links.Adopt(source, paths);
                if (!paths.Contains(kept, StringComparer.OrdinalIgnoreCase)) paths.Add(kept);
            }
            int cols = Math.Clamp((int)Math.Ceiling(Math.Sqrt(paths.Count * 1.6)), 3, 6);
            int rows = Math.Min((int)Math.Ceiling(paths.Count / (double)cols), 5);
            int w = cols * cell + (int)(34 * s);
            int h = (int)(30 * s) + rows * cellH + (int)(14 * s);

            if (x + w > area.Right - margin && x > area.Left + margin)
            {
                x = area.Left + margin;
                y += rowH + gap;
                rowH = 0;
            }

            var data = new FenceData { Title = g.Key, X = x, Y = y, Width = w, Height = h, Items = paths };
            Config.Fences.Add(data);
            Open(data);

            x += w + gap;
            rowH = Math.Max(rowH, h);
        }

        RequestSave();
        return entries.Count;
    }
}
