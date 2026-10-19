using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Corral;

public partial class FenceWindow : Window
{
    private const double RolledHeightDip = 28;   // 26 title + 2 border
    private const double MinExpandedHeightDip = 80;

    private readonly FenceManager _mgr;
    private IntPtr _hwnd;
    private bool _peeking;
    private readonly DispatcherTimer _unpeekTimer;
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _reloadTimer;

    // drag/resize state
    private string? _dragMode;
    private UIElement? _dragSource;
    private POINT _dragStartCursor;
    private RECT _dragStartRect;

    // item drag state
    private Point _itemDragStart;
    private bool _itemDragArmed;

    public FenceData Data { get; }
    public ObservableCollection<FenceItem> Items { get; } = new();

    public static readonly DependencyProperty IconPxProperty =
        DependencyProperty.Register(nameof(IconPx), typeof(double), typeof(FenceWindow), new PropertyMetadata(48.0));
    public static readonly DependencyProperty CellWidthProperty =
        DependencyProperty.Register(nameof(CellWidth), typeof(double), typeof(FenceWindow), new PropertyMetadata(84.0));

    /// <summary>Icon edge length in DIPs.</summary>
    public double IconPx { get => (double)GetValue(IconPxProperty); set => SetValue(IconPxProperty, value); }
    public double CellWidth { get => (double)GetValue(CellWidthProperty); set => SetValue(CellWidthProperty, value); }

    public bool IsPortal => Data.PortalPath != null;
    public IntPtr Hwnd => _hwnd;

    public static double CellWidthFor(int iconSize) => iconSize + 36;

    public FenceWindow(FenceManager manager, FenceData data)
    {
        _mgr = manager;
        Data = data;

        _unpeekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _unpeekTimer.Tick += (_, _) =>
        {
            _unpeekTimer.Stop();
            if (IsMouseOver || _mgr.IsDraggingFromFence || (List.ContextMenu?.IsOpen ?? false)) return;
            SetPeeking(false);
        };

        _reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _reloadTimer.Tick += (_, _) => { _reloadTimer.Stop(); ReloadItems(); };

        InitializeComponent();

        TitleText.Text = Data.Title;
        ApplyIconSize();
        ReloadItems();
        SetupWatcher();
    }

    private double Scale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    // ======================================================================================
    // Window plumbing
    // ======================================================================================

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        var source = HwndSource.FromHwnd(_hwnd)!;
        source.AddHook(WndProc);
        source.CompositionTarget.BackgroundColor = Colors.Transparent;

        Native.AddExStyle(_hwnd, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);

        int height = Data.RolledUp ? (int)Math.Round(RolledHeightDip * Scale) : Data.Height;
        Native.SetWindowPos(_hwnd, Native.HWND_BOTTOM, Data.X, Data.Y, Data.Width, height, Native.SWP_NOACTIVATE);
        ApplyBackdrop();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Native.WM_WINDOWPOSCHANGING:
                // Fences live on the desktop: never let anything raise us above normal windows.
                var pos = Marshal.PtrToStructure<Native.WINDOWPOS>(lParam);
                if ((pos.flags & Native.SWP_NOZORDER) == 0)
                {
                    pos.hwndInsertAfter = Native.HWND_BOTTOM;
                    pos.flags |= Native.SWP_NOACTIVATE;
                    Marshal.StructureToPtr(pos, lParam, false);
                }
                break;
            case Native.WM_MOUSEACTIVATE:
                handled = true;
                return new IntPtr(Native.MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    public void ApplyBackdrop()
    {
        if (_hwnd == IntPtr.Zero) return;
        string hex = Data.Tint ?? _mgr.Config.Tint;
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { c = Color.FromRgb(0x12, 0x14, 0x1A); }
        byte alpha = (byte)Math.Clamp(_mgr.Config.Opacity * 255 / 100, 0, 255);
        Native.ApplyGlass(_hwnd, c.R, c.G, c.B, alpha);
    }

    public RECT GetRect()
    {
        Native.GetWindowRect(_hwnd, out var r);
        return r;
    }

    /// <summary>Persist the current on-screen bounds into <see cref="Data"/>.</summary>
    public void CaptureBounds()
    {
        if (_hwnd == IntPtr.Zero) return;
        var r = GetRect();
        Data.X = r.Left;
        Data.Y = r.Top;
        Data.Width = r.Width;
        if (!Data.RolledUp && !_peeking) Data.Height = r.Height;
        _mgr.RequestSave();
    }

    private void SetBounds(RECT r)
    {
        Native.SetWindowPos(_hwnd, IntPtr.Zero, r.Left, r.Top, r.Width, r.Height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    // ======================================================================================
    // Roll-up
    // ======================================================================================

    public void ToggleRollUp()
    {
        if (!Data.RolledUp) CaptureBounds();
        Data.RolledUp = !Data.RolledUp;
        _peeking = false;
        ApplyHeight();
        _mgr.RequestSave();
    }

    private void ApplyHeight()
    {
        if (_hwnd == IntPtr.Zero) return;
        var r = GetRect();
        int h = Data.RolledUp && !_peeking ? (int)Math.Round(RolledHeightDip * Scale) : Data.Height;
        Native.SetWindowPos(_hwnd, IntPtr.Zero, r.Left, r.Top, r.Width, h, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    private void SetPeeking(bool peeking)
    {
        if (_peeking == peeking) return;
        _peeking = peeking;
        ApplyHeight();
    }

    private void Fence_MouseEnter(object sender, MouseEventArgs e)
    {
        _unpeekTimer.Stop();
        if (Data.RolledUp && _mgr.Config.PeekOnHover && _dragMode == null) SetPeeking(true);
    }

    private void Fence_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_peeking) _unpeekTimer.Start();
    }

    // ======================================================================================
    // Move & resize (manual, in physical pixels, so snapping and multi-DPI behave)
    // ======================================================================================

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            ToggleRollUp();
            return;
        }
        BeginDrag("M", (UIElement)sender);
    }

    private void Grip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var el = (FrameworkElement)sender;
        string mode = (string)el.Tag;
        if (Data.RolledUp && !_peeking) return;
        BeginDrag(mode, el);
    }

    private void BeginDrag(string mode, UIElement source)
    {
        if (_mgr.Config.Locked) return;
        Native.GetCursorPos(out _dragStartCursor);
        _dragStartRect = GetRect();
        _dragMode = mode;
        _dragSource = source;
        source.CaptureMouse();
    }

    private void Drag_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == null || e.LeftButton != MouseButtonState.Pressed) return;

        Native.GetCursorPos(out var p);
        int dx = p.X - _dragStartCursor.X;
        int dy = p.Y - _dragStartCursor.Y;
        var r = _dragStartRect;
        double scale = Scale;
        int minW = (int)(MinWidth * scale);
        int minH = (int)(MinExpandedHeightDip * scale);

        if (_dragMode == "M")
        {
            r.Offset(dx, dy);
        }
        else
        {
            if (_dragMode.Contains('L')) r.Left = Math.Min(r.Left + dx, r.Right - minW);
            if (_dragMode.Contains('R')) r.Right = Math.Max(r.Right + dx, r.Left + minW);
            if (_dragMode.Contains('T')) r.Top = Math.Min(r.Top + dy, r.Bottom - minH);
            if (_dragMode.Contains('B')) r.Bottom = Math.Max(r.Bottom + dy, r.Top + minH);
        }

        r = _mgr.Snap(this, r, _dragMode, scale);
        if (_dragMode != "M" && (_dragMode.Contains('L') || _dragMode.Contains('R')) && r.Width < minW)
        {
            // Snapping must never shrink us below the minimum.
            return;
        }
        SetBounds(r);
    }

    private void Drag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == null) return;
        _dragSource?.ReleaseMouseCapture();
        _dragMode = null;
        _dragSource = null;
        CaptureBounds();
    }

    // ======================================================================================
    // Items
    // ======================================================================================

    public void ApplyIconSize()
    {
        IconPx = Data.IconSize;
        CellWidth = CellWidthFor(Data.IconSize);
    }

    public IReadOnlyList<string> CurrentPaths()
    {
        if (!IsPortal) return Data.Items;
        try
        {
            var dir = new DirectoryInfo(Data.PortalPath!);
            return dir.EnumerateFileSystemInfos()
                .Where(i => (i.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(i => i is DirectoryInfo ? 0 : 1)
                .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(i => i.FullName)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Write("Portal enumeration failed: " + Data.PortalPath, ex);
            return Array.Empty<string>();
        }
    }

    public void ReloadItems()
    {
        double scale = Scale;
        int px = (int)Math.Ceiling(Data.IconSize * scale);
        Items.Clear();
        foreach (string path in CurrentPaths())
            Items.Add(new FenceItem(path, px));
        EmptyHint.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Text = IsPortal ? "This folder is empty" : "Drop files, folders or shortcuts here";
    }

    private void SetupWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        if (!IsPortal || !Directory.Exists(Data.PortalPath)) return;

        try
        {
            _watcher = new FileSystemWatcher(Data.PortalPath!) { EnableRaisingEvents = true };
            FileSystemEventHandler h = (_, _) => Dispatcher.BeginInvoke(() => { _reloadTimer.Stop(); _reloadTimer.Start(); });
            _watcher.Created += h;
            _watcher.Deleted += h;
            _watcher.Changed += h;
            _watcher.Renamed += (_, _) => Dispatcher.BeginInvoke(() => { _reloadTimer.Stop(); _reloadTimer.Start(); });
        }
        catch (Exception ex)
        {
            Log.Write("Could not watch portal folder", ex);
        }
    }

    public void AddPaths(IEnumerable<string> paths, int index = -1)
    {
        if (IsPortal) return;
        var existing = new HashSet<string>(Data.Items, StringComparer.OrdinalIgnoreCase);
        var fresh = new List<string>();
        foreach (string source in paths)
        {
            if (string.IsNullOrWhiteSpace(source)) continue;
            // Shortcuts are copied into Corral's store so deleting the original can't break this fence.
            string kept = _mgr.Links.Adopt(source, Data.Items.Concat(fresh));
            if (existing.Add(kept)) fresh.Add(kept);
        }
        if (fresh.Count == 0) return;

        if (index < 0 || index > Data.Items.Count) index = Data.Items.Count;
        Data.Items.InsertRange(index, fresh);
        ReloadItems();
        _mgr.RequestSave();
    }

    /// <param name="discard">
    /// True (the normal case) when the items are being removed for good, so Corral's private copies of any
    /// shortcuts go to the Recycle Bin. False when they are only moving to another fence.
    /// </param>
    public void RemovePaths(IEnumerable<string> paths, bool discard = true)
    {
        if (IsPortal) return;
        var removed = paths.ToList();
        var set = new HashSet<string>(removed, StringComparer.OrdinalIgnoreCase);
        Data.Items.RemoveAll(set.Contains);
        ReloadItems();
        if (discard)
            foreach (string path in removed) _mgr.DiscardIfUnused(path);
        _mgr.RequestSave();
    }

    private void MoveWithin(List<string> paths, int index)
    {
        var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        int before = Data.Items.Take(index).Count(set.Contains);
        var ordered = Data.Items.Where(set.Contains).ToList();
        Data.Items.RemoveAll(set.Contains);
        Data.Items.InsertRange(Math.Clamp(index - before, 0, Data.Items.Count), ordered);
        ReloadItems();
        _mgr.RequestSave();
    }

    public void SortByName()
    {
        if (IsPortal) return;
        Data.Items = Data.Items
            .OrderBy(p => ShellIcons.GetDisplayName(p), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        ReloadItems();
        _mgr.RequestSave();
    }

    public void SetIconSize(int size)
    {
        Data.IconSize = size;
        ApplyIconSize();
        ReloadItems();
        _mgr.RequestSave();
    }

    public void Rename(string title)
    {
        Data.Title = title;
        TitleText.Text = title;
        _mgr.RequestSave();
    }

    public void ChangePortal(string path)
    {
        Data.PortalPath = path;
        SetupWatcher();
        ReloadItems();
        _mgr.RequestSave();
    }

    protected override void OnClosed(EventArgs e)
    {
        _watcher?.Dispose();
        base.OnClosed(e);
    }

    // ---- item mouse handling ---------------------------------------------------------------

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    private FenceItem? ItemAt(Point listPoint, out ListBoxItem? container)
    {
        container = FindAncestor<ListBoxItem>(List.InputHitTest(listPoint) as DependencyObject);
        return container?.DataContext as FenceItem;
    }

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _itemDragStart = e.GetPosition(List);
        _itemDragArmed = ItemAt(_itemDragStart, out _) != null;
        if (!_itemDragArmed && e.ClickCount == 1)
            List.UnselectAll();
    }

    private void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_itemDragArmed || e.LeftButton != MouseButtonState.Pressed) return;

        Point pos = e.GetPosition(List);
        if (Math.Abs(pos.X - _itemDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _itemDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _itemDragArmed = false;
        var under = ItemAt(_itemDragStart, out var container);
        if (under == null) return;
        if (!container!.IsSelected)
        {
            List.SelectedItems.Clear();
            container.IsSelected = true;
        }

        var paths = List.SelectedItems.OfType<FenceItem>().Select(i => i.Path).ToList();
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, paths.ToArray());

        _mgr.CurrentDrag = new DragPayload(this, paths);
        try
        {
            // Copy|Link only: dragging out to Explorer must never *move* the user's real files.
            DragDrop.DoDragDrop(List, data, DragDropEffects.Copy | DragDropEffects.Link);
        }
        finally
        {
            _mgr.CurrentDrag = null;
        }
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemAt(e.GetPosition(List), out _) is { } item)
            Launch(item.Path);
    }

    private void List_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = ItemAt(e.GetPosition(List), out var container);
        e.Handled = true;
        if (item == null || container == null)
        {
            ShowFenceMenu();
            return;
        }
        if (!container.IsSelected)
        {
            List.SelectedItems.Clear();
            container.IsSelected = true;
        }
        ShowItemMenu(List.SelectedItems.OfType<FenceItem>().ToList());
    }

    private void Fence_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowFenceMenu();
    }

    // ---- drag & drop target ------------------------------------------------------------------

    private void Fence_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        bool accept = !IsPortal && (_mgr.CurrentDrag != null || e.Data.GetDataPresent(DataFormats.FileDrop));
        e.Effects = accept ? DragDropEffects.Link : DragDropEffects.None;
    }

    private int GetDropIndex(DragEventArgs e)
    {
        Point p = e.GetPosition(List);
        var item = ItemAt(p, out var container);
        if (container == null || item == null) return Items.Count;
        int idx = Items.IndexOf(item);
        if (e.GetPosition(container).X > container.ActualWidth / 2) idx++;
        return idx;
    }

    private void Fence_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (IsPortal) { e.Effects = DragDropEffects.None; return; }

        int index = GetDropIndex(e);
        var drag = _mgr.CurrentDrag;
        if (drag != null)
        {
            if (drag.Source == this) MoveWithin(drag.Paths, index);
            else _mgr.TransferItems(drag.Source, this, drag.Paths, index);
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            AddPaths(files, index);
        }
        e.Effects = DragDropEffects.Link;
    }

    // ======================================================================================
    // Actions & menus
    // ======================================================================================

    private static void Launch(string path, string? verb = null)
    {
        try
        {
            var psi = new ProcessStartInfo(path) { UseShellExecute = true };
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !Directory.Exists(path))
                psi.WorkingDirectory = dir;
            if (verb != null) psi.Verb = verb;
            Process.Start(psi);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            // Cancelled UAC prompts surface here too; only report real failures.
            if (ex is System.ComponentModel.Win32Exception { NativeErrorCode: 1223 }) return;
            MessageBox.Show($"Couldn't open:\n{path}\n\n{ex.Message}", "Corral", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static MenuItem Item(string header, Action action, bool check = false, bool isChecked = false, bool enabled = true)
    {
        var mi = new MenuItem { Header = header, IsCheckable = check, IsChecked = isChecked, IsEnabled = enabled };
        mi.Click += (_, _) => action();
        return mi;
    }

    private void ShowItemMenu(List<FenceItem> selection)
    {
        var menu = new ContextMenu();
        bool single = selection.Count == 1;

        menu.Items.Add(Item("Open", () => selection.ForEach(i => Launch(i.Path))));
        menu.Items.Add(Item("Run as administrator", () => Launch(selection[0].Path, "runas"), enabled: single));
        menu.Items.Add(Item("Open file location", () =>
        {
            foreach (var i in selection)
            {
                // For a shortcut Explorer shows where it points (the target), not the shortcut file itself.
                string? target = LinkStore.ResolveTarget(i.Path);
                string shown = target != null && (File.Exists(target) || Directory.Exists(target)) ? target : i.Path;
                Process.Start("explorer.exe", $"/select,\"{shown}\"");
            }
        }));
        menu.Items.Add(Item("Properties", () => Native.ShowProperties(selection[0].Path), enabled: single));
        if (!IsPortal)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(single ? "Remove from fence" : $"Remove {selection.Count} items from fence",
                () => RemovePaths(selection.Select(i => i.Path).ToList())));
        }
        OpenMenu(menu);
    }

    private void ShowFenceMenu()
    {
        var menu = new ContextMenu();

        menu.Items.Add(Item("Rename…", () =>
        {
            string? name = InputDialog.Ask("Rename fence", "Fence name:", Data.Title);
            if (!string.IsNullOrWhiteSpace(name)) Rename(name.Trim());
        }));
        menu.Items.Add(Item(Data.RolledUp ? "Unroll" : "Roll up", ToggleRollUp));

        var size = new MenuItem { Header = "Icon size" };
        foreach (var (label, value) in new[] { ("Small", 32), ("Medium", 48), ("Large", 64) })
        {
            var mi = Item(label, () => SetIconSize(value), check: true, isChecked: Data.IconSize == value);
            size.Items.Add(mi);
        }
        menu.Items.Add(size);
        menu.Items.Add(BuildColorMenu());

        if (IsPortal)
        {
            menu.Items.Add(Item("Open folder", () => Launch(Data.PortalPath!)));
            menu.Items.Add(Item("Change folder…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { InitialDirectory = Data.PortalPath };
                if (dlg.ShowDialog() == true) ChangePortal(dlg.FolderName);
            }));
        }
        else
        {
            menu.Items.Add(Item("Sort by name", SortByName));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("New fence", () => _mgr.CreateFence()));
        menu.Items.Add(Item("New folder portal…", () => _mgr.CreatePortal()));
        menu.Items.Add(Item("Remove this fence", () =>
        {
            var answer = MessageBox.Show(
                $"Remove the fence \"{Data.Title}\"?\n\n" +
                "Shortcuts stored in this fence go to the Recycle Bin. Files and folders it points to are not touched.",
                "Corral", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.OK) _mgr.RemoveFence(this);
        }));

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Lock layout", () => _mgr.SetLocked(!_mgr.Config.Locked), check: true, isChecked: _mgr.Config.Locked));
        menu.Items.Add(Item("Hide desktop icons", () => _mgr.SetDesktopIconsHidden(!_mgr.Config.HideDesktopIcons),
            check: true, isChecked: _mgr.Config.HideDesktopIcons));
        OpenMenu(menu);
    }

    private static FrameworkElement Swatch(string hex) => new System.Windows.Shapes.Rectangle
    {
        Width = 14,
        Height = 14,
        RadiusX = 3,
        RadiusY = 3,
        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
        Stroke = new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)),
        StrokeThickness = 1,
    };

    private MenuItem BuildColorMenu()
    {
        var root = new MenuItem { Header = "Color" };

        var useDefault = Item("Default", () => _mgr.SetFenceTint(this, null), check: true, isChecked: Data.Tint == null);
        useDefault.Icon = Swatch(_mgr.Config.Tint);
        root.Items.Add(useDefault);
        root.Items.Add(new Separator());

        foreach (var (name, hex) in FenceColors.Presets)
        {
            string h = hex;
            var mi = Item(name, () => _mgr.SetFenceTint(this, h), check: true, isChecked: FenceColors.Same(Data.Tint, h));
            mi.Icon = Swatch(h);
            root.Items.Add(mi);
        }

        root.Items.Add(new Separator());
        bool custom = Data.Tint != null && !FenceColors.Presets.Any(p => FenceColors.Same(p.Hex, Data.Tint));
        root.Items.Add(Item("Custom…", () =>
        {
            string? picked = PickColor(Data.Tint ?? _mgr.Config.Tint);
            if (picked != null) _mgr.SetFenceTint(this, picked);
        }, check: true, isChecked: custom));
        return root;
    }

    /// <summary>Shows the system colour dialog; returns #RRGGBB or null if cancelled.</summary>
    public static string? PickColor(string currentHex)
    {
        using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(currentHex);
            dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
        }
        catch (FormatException) { /* keep the dialog default */ }

        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return null;
        return $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
    }

    private void OpenMenu(ContextMenu menu)
    {
        List.ContextMenu = menu;
        menu.PlacementTarget = List;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
}

public sealed record DragPayload(FenceWindow Source, List<string> Paths);
