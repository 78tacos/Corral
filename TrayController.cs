using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Forms;

namespace Corral;

/// <summary>Notification-area icon and its menu.</summary>
internal sealed class TrayController : IDisposable
{
    private readonly FenceManager _mgr;
    private readonly Action _exit;
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu = new();
    private readonly Icon _appIcon;

    public TrayController(FenceManager manager, Action exit)
    {
        _mgr = manager;
        _exit = exit;
        _appIcon = LoadAppIcon();

        _menu.Opening += (_, _) => Rebuild();
        _icon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "Corral",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _mgr.ToggleFencesVisible();
        };
    }

    private void Rebuild()
    {
        _menu.Items.Clear();

        Add("New fence", () => _mgr.CreateFence());
        Add("New folder portal…", _mgr.CreatePortal);
        Add("Organize desktop into fences…", OrganizeDesktop);
        _menu.Items.Add(new ToolStripSeparator());

        Add(_mgr.FencesVisible ? "Hide fences" : "Show fences", () => _mgr.ToggleFencesVisible());
        Add("Hide desktop icons", () => _mgr.SetDesktopIconsHidden(!_mgr.Config.HideDesktopIcons), _mgr.Config.HideDesktopIcons);
        Add("Lock layout", () => _mgr.SetLocked(!_mgr.Config.Locked), _mgr.Config.Locked);
        Add("Snap to edges", () => _mgr.SetSnap(!_mgr.Config.SnapToEdges), _mgr.Config.SnapToEdges);

        var dbl = new ToolStripMenuItem("Double-click on the desktop");
        foreach (var (label, mode) in new[]
        {
            ("Do nothing", DoubleClickMode.Off),
            ("Hide / show fences", DoubleClickMode.Fences),
            ("Hide / show desktop icons", DoubleClickMode.Icons),
            ("Hide / show both", DoubleClickMode.Both),
        })
        {
            var item = new ToolStripMenuItem(label) { Checked = _mgr.Config.DoubleClickAction == mode };
            var m = mode;
            item.Click += (_, _) => _mgr.SetDoubleClickMode(m);
            dbl.DropDownItems.Add(item);
        }
        dbl.DropDownItems.Add(new ToolStripSeparator());
        dbl.DropDownItems.Add(new ToolStripMenuItem("Shift + double-click: desktop icons only") { Enabled = false });
        _menu.Items.Add(dbl);

        Add("Peek when rolled up", () => _mgr.SetPeek(!_mgr.Config.PeekOnHover), _mgr.Config.PeekOnHover);

        var opacity = new ToolStripMenuItem("Background");
        foreach (var (label, value) in new[] { ("Glass", 25), ("Light", 40), ("Medium", 55), ("Heavy", 75), ("Solid", 95) })
        {
            var item = new ToolStripMenuItem(label) { Checked = Math.Abs(_mgr.Config.Opacity - value) < 8 };
            int v = value;
            item.Click += (_, _) => _mgr.SetOpacity(v);
            opacity.DropDownItems.Add(item);
        }
        _menu.Items.Add(opacity);

        var colors = new ToolStripMenuItem("Default color");
        foreach (var (name, hex) in FenceColors.Presets)
        {
            string h = hex;
            var c = ColorTranslator.FromHtml(h);
            var item = new ToolStripMenuItem(name, Swatch(c)) { Checked = FenceColors.Same(_mgr.Config.Tint, h) };
            item.Click += (_, _) => _mgr.SetDefaultTint(h);
            colors.DropDownItems.Add(item);
        }
        var custom = new ToolStripMenuItem("Custom…");
        custom.Click += (_, _) =>
        {
            string? picked = FenceWindow.PickColor(_mgr.Config.Tint);
            if (picked != null) _mgr.SetDefaultTint(picked);
        };
        colors.DropDownItems.Add(new ToolStripSeparator());
        colors.DropDownItems.Add(custom);
        _menu.Items.Add(colors);

        Add("Start with Windows", () => _mgr.StartWithWindows = !_mgr.StartWithWindows, _mgr.StartWithWindows);

        _menu.Items.Add(new ToolStripSeparator());
        Add("Open settings folder", () =>
            System.Diagnostics.Process.Start("explorer.exe", $"\"{_mgr.ConfigDirectory}\""));
        Add("Exit", _exit);
    }

    private void Add(string text, Action action, bool isChecked = false, bool enabled = true)
    {
        var item = new ToolStripMenuItem(text) { Checked = isChecked, Enabled = enabled };
        item.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) { Log.Write("Tray action failed: " + text, ex); }
        };
        _menu.Items.Add(item);
    }

    private static Bitmap Swatch(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRect(new RectangleF(1, 1, 14, 14), 3);
        using var fill = new SolidBrush(color);
        using var pen = new Pen(Color.FromArgb(120, 128, 128, 128));
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
        return bmp;
    }

    private void OrganizeDesktop()
    {
        var entries = _mgr.GetDesktopEntries();
        if (entries.Count == 0)
        {
            System.Windows.MessageBox.Show("There is nothing on the desktop that isn't already in a fence.", "Corral");
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            $"Create fences for the {entries.Count} items on your desktop, grouped by type?\n\n" +
            "Shortcuts are copied into Corral, so you can delete the originals from the desktop afterwards. " +
            "Other files and folders stay where they are and the fences point to them. Nothing on your desktop " +
            "is moved or deleted. Desktop icons will be hidden so the fences take their place " +
            "(you can turn that off from this menu).",
            "Organize desktop", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        _mgr.OrganizeDesktop(entries);
        _mgr.SetDesktopIconsHidden(true);
    }

    /// <summary>Loads the embedded multi-size icon, asking for the size the tray actually draws (DPI-aware).</summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            using var stream = typeof(TrayController).Assembly.GetManifestResourceStream("corral.ico");
            if (stream != null) return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch (Exception ex)
        {
            Log.Write("Could not load tray icon", ex);
        }
        return (Icon)SystemIcons.Application.Clone();
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _appIcon.Dispose();
    }
}
