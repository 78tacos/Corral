# Corral

Corral is a desktop organizer for Windows in the spirit of Stardock Fences. It puts translucent, blurred
**fences** on your desktop that hold shortcuts, files and folders, so a cluttered desktop becomes a few
tidy, labelled boxes.

Built with WPF on .NET 10. Runs on Windows 10 and 11 (x64). It lives in the system tray and never moves,
renames or deletes your files. Shortcuts you put in a fence are copied into Corral, so you can delete the
originals from the desktop; real files and folders stay where they are and the fence points to them.

## Documentation

| Read this | If you want to |
|---|---|
| [User guide](docs/USER-GUIDE.md) | Install and use Corral, every menu and setting, troubleshooting |
| [Developer guide](docs/DEVELOPER-GUIDE.md) | Build it, understand how it works, change it, ship a release |
| [Changelog](CHANGELOG.md) | See what is in each version |

## Quick start

**Install it.** Run `installer\Output\Corral-Setup-1.0.0.exe` (build it first if it is missing, see below).
It needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64); the installer
tells you if it is missing.

**Or run from source.**

```
dotnet run
```

**Build the installer.**

```
pwsh installer\build.ps1
```

That needs Inno Setup 6 (`winget install JRSoftware.InnoSetup`) and produces
`installer\Output\Corral-Setup-<version>.exe`.

## First minute

1. Corral starts in the notification area (tray) and creates one empty fence. On Windows 11 new tray icons
   start behind the `^` arrow; drag the Corral icon out if you want it always visible.
2. Drag files, folders or shortcuts from Explorer or your desktop onto the fence.
3. Double-click an item to open it. Right-click for more. Drag the title bar to move the fence.
4. Right-click the tray icon, choose **Organize desktop into fences**, and Corral sorts everything on your
   desktop into Programs, Folders, Documents, Pictures & Media and Other fences for you.
5. Double-click an empty patch of desktop to hide all the fences; double-click again to bring them back.
   Hold **Shift** while you double-click to toggle just the real desktop icons instead.

## Project layout

```
Corral/
  App.xaml(.cs)             startup, single instance, --quit / --config switches
  FenceWindow.xaml(.cs)     one fence: window, move/resize, items, drag and drop, menus
  FenceManager.cs           all fences, settings, snapping, desktop import, colors
  DesktopClickWatcher.cs    double-click-the-desktop detection (mouse hook + UI Automation)
  TrayController.cs         tray icon and menu
  ShellIcons.cs, FenceItem.cs   icon loading and the item view-model
  LinkStore.cs              Corral's private copies of shortcuts (%APPDATA%\Corral\Links)
  Native.cs                 all P/Invoke (DWM/acrylic, desktop list view, shell icons, hooks)
  Config.cs                 settings model, JSON store, log
  InputDialog.xaml(.cs)     the rename prompt
  Assets/corral.ico         application icon (generated, see tools/make-icon.ps1)
  installer/                Inno Setup script and build/test scripts
  tests/                    unit tests (LinkStore and the settings file)
  tools/                    developer helper scripts (screenshots, icon generation, UI tests)
  docs/                     user guide and developer guide
```

## Safety, in one paragraph

A shortcut (`.lnk`, `.url`) put in a fence is **copied** into Corral's own folder, so deleting the original
from the desktop does not affect the fence. Real files and folders are *not* copied: the fence points to them.
Corral never moves, renames or deletes your files, and dragging an item out of a fence into Explorer can only
copy or link, never move. Desktop icons that Corral hides are always shown
again when it exits. If they are ever stuck hidden, right-click the desktop and choose
View -> Show desktop icons.
