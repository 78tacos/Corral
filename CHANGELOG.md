# Changelog

All notable changes to Corral. The version number lives in `Corral.csproj` (`<Version>`).

## 1.2.0

**Double-click the desktop: icons and fences can be toggled separately.**
- New tray submenu **Double-click on the desktop** sets what a plain double-click on empty desktop does: *Hide / show
  fences* (the default, as before), *Hide / show desktop icons* only, *Hide / show both*, or *Do nothing*.
- **Shift + double-click** on empty desktop always toggles the real desktop icons only, leaving the fences alone,
  whichever setting is chosen. With the fences hidden it hides or shows the icons temporarily, so a clean
  desktop is one Shift + double-click away.
- This replaces the two earlier checkboxes. Existing settings upgrade automatically (the old pair of
  switches becomes the matching mode) and nothing changes unless you change it.

**Developer**
- `AppConfig.DoubleClickAction` (string enum) replaces `DoubleClickDesktop` / `DoubleClickHidesIcons`; the old keys
  are read once and dropped. Test project renamed `tests\CorralTests` and extended to 43 assertions.
- `tools\desktop-toggle-test.ps1` replaces `dblclick-test.ps1`: it checks the real desktop's icon and fence
  visibility after every gesture in every mode.

## 1.1.0

**Shortcuts no longer depend on the original.**
- A shortcut (`.lnk`, `.url`, `.appref-ms`) put into a fence is now **copied** into Corral's own folder
  (`%APPDATA%\Corral\Links`) and the fence uses the copy. Deleting or moving the original (for example the icon on the
  desktop) no longer breaks the fence. Real files and folders are still referenced, not copied.
- *Organize desktop into fences* copies shortcuts the same way, so the originals can be deleted afterwards.
- Existing fences are upgraded automatically on first start: shortcuts whose originals still exist are copied in.
  (Shortcuts already deleted before upgrading cannot be recovered this way; restore them from the Recycle Bin and
  drag them in again.)
- *Remove from fence* and *Remove this fence* send Corral's own shortcut copies to the Recycle Bin (restorable).
- *Open file location* on a shortcut now opens the folder of its target, as Explorer does.

**Developer**
- New `LinkStore` class with a dependency-free test program (`tests\LinkStoreTests`) and an end-to-end script
  (`tools\e2e-copy-test.ps1`).

## 1.0.0

First release.

**Fences**
- Translucent, blurred (acrylic) fences on the desktop that hold shortcuts, files and folders. They sit behind
  normal windows and never appear in Alt-Tab.
- Add items by dragging from Explorer or the desktop; reorder within a fence; move between fences. Items are
  references only: files are never moved, renamed or deleted.
- Double-click to open; right-click for Open, Run as administrator, Open file location, Properties, Remove.
- Move and resize with snapping to screen edges and neighbouring fences; roll up to the title bar, with
  peek-on-hover; lock the layout.
- Rename, sort by name, three icon sizes (32 / 48 / 64 px), image and video thumbnails.
- Colors: ten presets and a custom picker per fence, plus a shared default; five transparency levels.
- **Folder portals** that mirror a folder live.
- **Organize desktop into fences**: groups desktop items into Programs, Folders, Documents, Pictures & Media
  and Other.

**Desktop integration**
- Hide the real desktop icons (always restored on exit).
- Double-click empty desktop to hide or show all fences, with an option to hide the desktop icons too for a
  clean desktop.
- Runs from the system tray with its own icon (no taskbar button); optional start with Windows.
- Single instance; fences are pulled back on-screen after display changes.

**Packaging**
- Per-user Inno Setup installer with optional start-at-sign-in, .NET 10 Desktop Runtime check, graceful close
  of a running copy for upgrades and uninstalls (`Corral.exe --quit`), and an option to keep or delete your
  settings on uninstall.
- `--config <file>` switch for running against a separate settings file.
