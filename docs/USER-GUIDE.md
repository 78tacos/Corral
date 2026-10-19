# Corral User Guide

Corral keeps your desktop tidy with **fences**: translucent boxes on the desktop, each holding a group of
shortcuts, files or folders. This guide covers installing and using it. If you want to build or change
the program, see the [Developer guide](DEVELOPER-GUIDE.md).

Contents

1. [Install and start](#1-install-and-start)
2. [Fences at a glance](#2-fences-at-a-glance)
3. [Putting things in a fence](#3-putting-things-in-a-fence)
4. [Working with items](#4-working-with-items)
5. [Moving, resizing and rolling up fences](#5-moving-resizing-and-rolling-up-fences)
6. [Colors and transparency](#6-colors-and-transparency)
7. [Folder portals](#7-folder-portals)
8. [Organize my desktop automatically](#8-organize-my-desktop-automatically)
9. [Hiding the desktop icons and the double-click shortcut](#9-hiding-the-desktop-icons-and-the-double-click-shortcut)
10. [Menu reference](#10-menu-reference)
11. [Where Corral keeps things](#11-where-corral-keeps-things)
12. [Updating and uninstalling](#12-updating-and-uninstalling)
13. [Troubleshooting](#13-troubleshooting)
14. [Safety and privacy](#14-safety-and-privacy)

---

## 1. Install and start

**Requirements:** Windows 10 or 11 (64-bit) and the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).

1. Run `Corral-Setup-1.0.0.exe`.
2. If the runtime is missing, setup says so and offers to open the download page. Install the runtime, then
   run setup again. (You can also choose to install Corral anyway; it will not start until the runtime is there.)
3. Choose a folder (the default, `%LOCALAPPDATA%\Programs\Corral`, needs no administrator rights), then decide
   whether Corral should **start when you sign in to Windows**. This is on by default.
4. Click **Install**, and leave **Launch Corral now** ticked.

Because the installer is not code-signed, Windows SmartScreen may show "Windows protected your PC". Click
**More info**, then **Run anyway**.

### Finding Corral once it is running

Corral has no window and no taskbar button. It lives in the **notification area** next to the clock.
On Windows 11 new icons are tucked behind the `^` arrow. To keep it visible, drag the icon from the overflow
onto the taskbar, or switch it on under **Settings -> Personalization -> Taskbar -> Other system tray icons**.

The first time it runs, Corral creates one empty fence called "My Fence".

| On the tray icon | Does |
|---|---|
| Double-click | Hide or show all fences |
| Right-click | Opens the tray menu ([reference](#tray-menu)) |

---

## 2. Fences at a glance

```
 +--------------------------------+
 |           Programs             |   <- title bar: drag to move, double-click to roll up
 |                                |
 |  [icon]    [icon]    [icon]    |
 |  Notepad   Calc      Edge      |   <- items: double-click to open
 |                                |
 |  [icon]    [icon]              |
 |  Paint     Terminal            |
 +--------------------------------+
        ^ drag any edge or corner to resize
```

- A fence has a **title**, a size and position, a color, an icon size, and a list of items.
- Fences sit *behind* your normal windows, on the desktop layer, and never appear in Alt-Tab.
  A maximized window covers them, just as it covers the desktop. Use the double-click shortcut or
  Win+D to get to your desktop.
- Everything is remembered: positions, sizes, colors, titles and contents survive restarts.

---

## 3. Putting things in a fence

**Drag and drop** (the main way). Drag any file, folder or shortcut from Explorer, the desktop, or another
program onto a fence. A fence shows "Drop files, folders or shortcuts here" while it is empty.

- **Shortcuts are copied.** A shortcut (`.lnk`, `.url` or `.appref-ms`) is copied into Corral's own folder
  (`%APPDATA%\Corral\Links`) and the fence uses that copy. The fence no longer depends on the original, so you
  can **delete the shortcut from the desktop** and the fence keeps working. The original is never moved or
  changed by Corral; deleting it is up to you.
- **Files and folders are referenced, not copied.** A real file or folder is large and a second copy would drift
  from the original, so the fence points at it where it lives. If you delete or move that file, the fence entry
  goes blank, as it would for any shortcut to it.
- The same shortcut will not be added twice to one fence. (Two shortcuts that merely have the same name and
  target but were created separately count as different items.)
- You can drop several things at once.

**Position.** Drop onto the left half of an existing item to insert before it, or the right half to insert
after it. Drop on empty space to append.

**Automatically.** To sort your whole desktop into fences in one go, see
[section 8](#8-organize-my-desktop-automatically).

---

## 4. Working with items

| Action | Result |
|---|---|
| Double-click | Opens the item with its default program |
| Click | Selects it |
| Ctrl-click / Shift-click | Select several |
| Click empty space | Clears the selection |
| Drag within a fence | Reorders the selected items |
| Drag to another fence | Moves them there (they are removed from the first fence) |
| Drag to Explorer or another program | **Copies** or links the real file (never moves it) |
| Hover | Shows the full path in a tooltip |

### Item right-click menu

| Command | Notes |
|---|---|
| **Open** | Opens every selected item |
| **Run as administrator** | One item at a time. Windows asks for confirmation (UAC). If you cancel, nothing happens |
| **Open file location** | Opens Explorer with the item highlighted. For a shortcut this is the folder of the program or file it points to, as in Explorer |
| **Properties** | The standard Windows properties dialog. One item at a time |
| **Remove from fence** | Takes it out of the fence. If it was a shortcut, Corral's private copy goes to the **Recycle Bin** (you can restore it from there); a file or folder it pointed to is never touched. Not shown in folder portals |

### Sorting and icon size

Right-click the fence background (or title bar):

- **Sort by name** sorts the fence's items alphabetically, using the names Windows shows (so it respects
  "hide file extensions").
- **Icon size** switches between Small (32 px), Medium (48 px) and Large (64 px). Images and videos show a
  small preview; everything else shows its normal icon.

Item names follow your Windows setting for file extensions (File Explorer -> View -> Show -> File name extensions).

---

## 5. Moving, resizing and rolling up fences

| Action | How |
|---|---|
| **Move** | Drag the title bar |
| **Resize** | Drag any edge or corner |
| **Roll up / unroll** | Double-click the title bar, or right-click -> **Roll up** / **Unroll** |
| **Rename** | Right-click -> **Rename...** |
| **Lock the layout** | Tray or fence menu -> **Lock layout** |

### Snapping

With **Snap to edges** on (the default), a fence being moved or resized pulls into line with the screen's usable
edges and with neighbouring fences. When it snaps against a neighbour it leaves a small gap, so fences line up
neatly instead of touching. Turn it off from the tray menu if you want fully free placement.

### Rolling up

A rolled-up fence shrinks to just its title bar to save space. With **Peek when rolled up** on (default), it
springs open while you hover over it and collapses again shortly after you move away. Turn that off in the
tray menu if you would rather rolled-up fences only open when you unroll them.

### Locking

**Lock layout** stops fences from being moved or resized, so you cannot nudge one by accident. You can still
add, remove, reorder and open items, rename fences, and change colors while it is on.

### Size limits

A fence cannot be made narrower than 110 units or, when open, shorter than 80 units (a unit is one pixel at
100% display scaling, so these grow with your scaling setting). When a fence holds more items than fit, a thin
scroll bar appears.

---

## 6. Colors and transparency

Each fence is tinted with a color over a blurred copy of whatever is behind it.

**Per fence.** Right-click a fence -> **Color**, then pick one of ten presets:
Graphite, Slate, Blue, Teal, Green, Gold, Orange, Red, Pink, Purple. Or choose **Custom...** for the full
Windows color picker. Choose **Default** to go back to the shared color.

**Shared default.** Tray menu -> **Default color** sets the color for every fence that does not have its own.
(New fences use the default. Graphite is the factory setting.)

**Transparency.** Tray menu -> **Background** controls how strongly the tint covers the blur, for *all* fences:

| Setting | Tint strength |
|---|---|
| Glass | 25% (mostly see-through) |
| Light | 40% |
| Medium | 55% (default) |
| Heavy | 75% |
| Solid | 95% (nearly opaque) |

Dark colors work best, because item labels are white. If you choose a very light custom color the text may be
hard to read.

If Windows has **transparency effects** switched off (Settings -> Personalization -> Colors), the blur may be
replaced by a flat color.

---

## 7. Folder portals

A **portal** is a fence that mirrors a folder instead of holding a list you maintain. New files appear
automatically and deleted ones disappear.

**Create one:** tray menu or fence menu -> **New folder portal...**, then choose a folder (Downloads is a
popular choice). The fence takes the folder's name.

Good to know:

- Folders are listed first, then files, each alphabetically. Hidden and system files are not shown.
- You can open items, view properties, and drag them *out* (copy only).
- You **cannot** drop files *into* a portal, and **Remove from fence** is not offered. A portal never changes
  the folder.
- Right-click -> **Open folder** opens it in Explorer; **Change folder...** points the portal somewhere else.
- A portal still has its own title, color, size and icon size, and can be rolled up like any fence.
- If the folder is deleted or becomes unavailable the portal shows as empty.

---

## 8. Organize my desktop automatically

Tray menu -> **Organize desktop into fences...**

Corral reads everything on your desktop (both your own and the shared "Public" desktop), skips hidden and
system files and anything already in a fence, and creates one fence per category:

| Fence | What goes in |
|---|---|
| **Programs** | `.lnk` shortcuts, `.exe`, `.url`, `.appref-ms`, `.bat`, `.cmd`, `.msi`, `.ps1` |
| **Folders** | Folders |
| **Documents** | Word, Excel, PowerPoint, PDF, text, Markdown, CSV, OneNote, JSON, XML and similar |
| **Pictures & Media** | Common image, video and audio formats |
| **Other** | Everything else |

Fences are only created for categories that have something in them. Items inside each fence are sorted by
name, and the fences are laid out left to right in rows starting at the top-left of your main screen.

Before it does anything, Corral shows how many items it found and asks you to confirm. Shortcuts are
**copied** into Corral (see [section 3](#3-putting-things-in-a-fence)), so once the fences are set up you can
safely delete the originals from the desktop. Real files and folders stay where they are and the fences point to
them. Nothing on your desktop is moved or deleted by Corral. To avoid seeing everything twice, Corral then turns
on **Hide desktop icons** (see below), which you can switch off again from the tray menu.

---

## 9. Hiding the desktop icons and the double-click shortcut

### Hide desktop icons

Tray menu (or fence menu) -> **Hide desktop icons** hides the real desktop icon layer so only your fences show.
Your files are not touched; the icons are simply not drawn. Corral shows them again whenever it exits.

### Double-click the desktop

Double-click an **empty patch of desktop** and Corral toggles something. You choose what, and there is a second
gesture so the fences and the icons can each be toggled on their own.

| Gesture | What it does |
|---|---|
| **Double-click** empty desktop | Does whatever you set under tray menu -> **Double-click on the desktop** (below). Double-click again to undo |
| **Shift + double-click** empty desktop | Always toggles the **real desktop icons only**. The fences stay exactly as they are |

Tray menu -> **Double-click on the desktop** sets what the plain double-click does:

| Setting | Plain double-click |
|---|---|
| **Hide / show fences** (default) | Hides or shows every fence. Your real desktop icons come back while the fences are away (see below) |
| **Hide / show desktop icons** | Hides or shows the real desktop icons only. The fences stay put |
| **Hide / show both** | Hides or shows the fences *and* the real icons together, leaving only your wallpaper. The next double-click restores everything |
| **Do nothing** | Turns the feature off, including Shift + double-click |

Good to know:

- Double-clicking an *icon* is ignored, so opening things from the desktop still works normally.
- **Icons only** (plain double-click in that setting, or Shift + double-click anywhere) works exactly like the
  tray's **Hide desktop icons** checkbox while the fences are showing: it flips that setting and remembers it.
- **While the fences are hidden,** their desktop icons are handled separately. In the default setting hiding the
  fences gives you your real desktop back (icons you had hidden reappear, and are hidden again when the fences
  return). Press Shift + double-click while the fences are hidden to hide the icons too (a clean desktop),
  and again to bring them back. That choice lasts only until the fences return.
- The tray-icon double-click and the tray **Hide fences / Show fences** command also toggle the fences, in the
  default way. They never hide the real icons themselves.

Some examples. Say you normally keep the icons hidden and use fences:

- Double-click (default setting): fences disappear and your real icons appear, handy for a quick look at the
  actual desktop. Double-click again to go back.
- Shift + double-click: the fences stay, and the icons toggle on or off.

---

## 10. Menu reference

### Tray menu

| Item | What it does |
|---|---|
| New fence | Adds an empty fence |
| New folder portal... | Adds a fence that mirrors a folder |
| Organize desktop into fences... | Builds category fences from your desktop ([section 8](#8-organize-my-desktop-automatically)) |
| Hide fences / Show fences | Hides or shows every fence |
| Hide desktop icons | Hides the real desktop icons (checked = hidden) |
| Lock layout | Prevents moving and resizing |
| Snap to edges | Fences snap to screen edges and each other |
| Double-click on the desktop | What a plain double-click on empty desktop does: Do nothing / Hide-show fences / Hide-show desktop icons / Hide-show both. Shift + double-click always toggles the icons only |
| Peek when rolled up | Rolled-up fences open while hovered |
| Background | Glass / Light / Medium / Heavy / Solid transparency |
| Default color | Color for fences without their own; ten presets or Custom... |
| Start with Windows | Run Corral when you sign in |
| Open settings folder | Opens the folder holding your settings and log |
| Exit | Quits Corral and shows the desktop icons again |

### Fence menu (right-click the title bar or the empty part of a fence)

| Item | What it does |
|---|---|
| Rename... | Change the title |
| Roll up / Unroll | Collapse to the title bar, or expand |
| Icon size | Small, Medium or Large |
| Color | Per-fence color, or Default |
| Sort by name | Alphabetize (normal fences only) |
| Open folder / Change folder... | Portal fences only |
| New fence, New folder portal... | Add another fence |
| Remove this fence | Deletes the fence (asks first). Your files are **not** deleted |
| Lock layout | Same as the tray setting |
| Hide desktop icons | Same as the tray setting |

---

## 11. Where Corral keeps things

| What | Where |
|---|---|
| Program | `%LOCALAPPDATA%\Programs\Corral` (or the folder you chose) |
| Fences and settings | `%APPDATA%\Corral\config.json` |
| Copies of the shortcuts in your fences | `%APPDATA%\Corral\Links\` |
| Error log | `%APPDATA%\Corral\corral.log` |
| Start-at-sign-in entry | Registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `Corral` |

Paste `%APPDATA%\Corral` into Explorer's address bar, or use tray menu -> **Open settings folder**.

**Back up or move your setup** by copying `config.json` **and the `Links` folder** to the same place on the other
PC while Corral is not running. (The config points at the copies in `Links`, so it needs both.) Items that are
real files or folders will only show on the other PC if they exist at the same paths there. Fence positions are stored in screen pixels, so a different resolution may need a little rearranging.

**Start over** by closing Corral (tray -> Exit), renaming or deleting `config.json`, and starting Corral again.
If Corral ever finds a `config.json` it cannot read, it saves a copy as `config.json.bad` and starts fresh, so
nothing is silently lost.

The advanced reference for every setting in the file is in the
[Developer guide](DEVELOPER-GUIDE.md#5-configuration-file-reference).

---

## 12. Updating and uninstalling

**Update.** Run the newer `Corral-Setup-x.y.z.exe`. It closes the running Corral cleanly and installs over the
old version. Your fences and settings are kept.

**Upgrading from 1.0.** Version 1.0 only remembered where shortcuts were. The first time 1.1 or later starts, it
copies every shortcut your fences point at into the `Links` folder, as long as the original still exists. A
shortcut that was already deleted before you upgraded cannot be copied; if it is in your Recycle Bin, restore it
and drag it into the fence again, and it will be copied then.

**Uninstall.** Settings -> Apps -> Installed apps -> Corral -> Uninstall (or Control Panel -> Programs).
Corral is closed first, so desktop icons are restored. At the end you are asked whether to also delete your
fences and settings. The default, **No**, keeps them in case you reinstall.

**Scripted installs** (for IT): the installer accepts the standard Inno Setup switches.

```
Corral-Setup-1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
Corral-Setup-1.0.0.exe /VERYSILENT /DIR="C:\Tools\Corral" /TASKS=startup
```

`/TASKS=startup` turns on start-at-sign-in; `/TASKS=""` turns it off. Silent installs do not launch Corral.
Silent uninstalls never delete user settings.

---

## 13. Troubleshooting

**I can't find Corral / there's no tray icon.**
Look behind the `^` arrow in the notification area. If it is not there, start Corral from the Start menu.
Corral only ever runs one copy, so starting it twice does nothing the second time.

**Nothing happens when I start it / it closes immediately.**
The .NET 10 Desktop Runtime (x64) is probably missing. Install it from
<https://dotnet.microsoft.com/download/dotnet/10.0> and try again. Check `%APPDATA%\Corral\corral.log` for details.

**My fences disappeared.**
Something may be covering them (a maximized window), or they were hidden with the double-click shortcut or the
tray's **Hide fences**. Double-click an empty patch of desktop, or choose **Show fences** in the tray menu.

**My desktop icons are gone.**
Either **Hide desktop icons** is on (tray menu), or a desktop double-click hid them (a plain double-click set to
icons or both, or Shift + double-click). Turn the option off or repeat the gesture. Exiting Corral always shows them. If they are still missing, right-click the
desktop -> View -> Show desktop icons.

**A fence ended up off-screen after I changed monitors.**
Corral pulls any fence that is completely off every screen back onto your main display when it starts and
whenever the display setup changes. If one is still missing, restart Corral.

**An item shows a blank icon.**
For a real file or folder, it was probably moved, renamed or deleted, or sits on a drive that is not connected.
For a shortcut, the copy in Corral is fine but the program or file it points to is gone. Right-click ->
**Remove from fence**, and drag the item in again from its new location. A fence entry from before version 1.1
whose original shortcut was already deleted also looks like this; restore the shortcut from the Recycle Bin and
drag it in again.

**A portal is not updating.**
Corral watches the folder for changes. Network folders and some cloud-synced folders do not always report
changes. Right-click -> **Change folder...** and re-select the same folder to force a refresh, or restart Corral.

**The blur looks flat, not frosted.**
Windows transparency effects are off, or Battery Saver is on. Turn on Settings -> Personalization -> Colors ->
Transparency effects.

**"Run as administrator" does nothing.**
If you cancelled the Windows permission prompt, that is expected. Otherwise the target may be missing.

**The double-click on the desktop does nothing, or does the wrong thing.**
Check tray menu -> **Double-click on the desktop**: it must not be on *Do nothing*, and it decides what a plain
double-click controls (fences, icons or both). Shift + double-click always means "icons only". Click on bare
wallpaper, not on an icon, and not on a fence. The two clicks must be quick and close together, as for any double-click.
Some security or remote-control software that blocks system-wide mouse hooks can stop it working; if so, use the
tray icon double-click instead.

**Dragging out to Explorer only copies.**
That is deliberate, so a stray drop cannot move your real files. Hold Alt while dropping to create a shortcut
instead.

---

## 14. Safety and privacy

- Corral never moves, renames or deletes the files and shortcuts you own. Shortcuts you add to a fence are
  **copied** into `%APPDATA%\Corral\Links`; real files and folders are only **pointed at**.
- Dragging items out of a fence offers **Copy** or **Link** only.
- **Remove from fence** and **Remove this fence** send Corral's own shortcut copies to the **Recycle Bin**
  (restorable), and never touch the files and folders your fences point at.
- Hidden desktop icons are restored on a normal exit, on an unhandled error, and by the installer or
  uninstaller. (If the process is force-killed from Task Manager it cannot clean up; see Troubleshooting.)
- Corral makes no network connections and sends nothing anywhere. It has no accounts and no telemetry.
- To react to a desktop double-click, Corral installs a system-wide mouse hook. It only looks at *where* you
  clicked and *when*; it does not record keystrokes or the content of anything.
- Everything it saves is in `%APPDATA%\Corral`.
