# Dev helper: live test of the desktop double-click gestures, for every "Double-click on the desktop" mode.
#
# For each mode it starts Corral on a throwaway config, double-clicks an empty patch of the real desktop
# (plain, and with Shift held), and after EVERY step asks Windows two questions:
#   - are the real desktop icons visible?   (the SysListView32 behind the wallpaper)
#   - are any fence windows visible?
# and compares them with what the mode should produce. Prints PASS/FAIL per step.
#
# SIDE EFFECTS - read before running:
#   * minimizes all your windows for ~15 seconds per mode, then restores them
#   * moves the mouse and clicks; don't touch the PC while it runs
#   * quits your running Corral (gracefully) and starts it again at the end
# Your real settings are never touched (a throwaway config is used).
param(
    [Parameter(Mandatory)][string]$Exe,
    [Parameter(Mandatory)][string]$WorkDir,
    [string[]]$Modes = @('Fences', 'Icons', 'Both', 'Off'),
    # Where to double-click, in this PowerShell host's own screen units. Default: a point 75% across and 45% down
    # the primary screen, which on the author's wallpaper is empty desktop. Pass -X/-Y if yours has an icon there.
    [int]$X = -1, [int]$Y = -1
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$primary = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
if ($X -lt 0) { $X = [int]($primary.Left + $primary.Width * 0.75) }
if ($Y -lt 0) { $Y = [int]($primary.Top + $primary.Height * 0.45) }
"click point: $X,$Y   (primary screen $($primary.Width)x$($primary.Height), $([System.Windows.Forms.Screen]::AllScreens.Count) monitor(s))"
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class Desk {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    public static bool IconsVisible() {
        IntPtr dv = FindWindowEx(FindWindow("Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null);
        if (dv == IntPtr.Zero) {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) => { IntPtr d = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null); if (d != IntPtr.Zero) { found = d; return false; } return true; }, IntPtr.Zero);
            dv = found;
        }
        IntPtr lv = FindWindowEx(dv, IntPtr.Zero, "SysListView32", null);
        return lv == IntPtr.Zero || IsWindowVisible(lv);
    }
    public static int VisibleWindows(uint pid) {
        int n = 0;
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) n++; return true; }, IntPtr.Zero);
        return n;
    }
    public static void DoubleClick(bool shift) {
        if (shift) keybd_event(0x10, 0, 0, UIntPtr.Zero);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        if (shift) keybd_event(0x10, 0, 2, UIntPtr.Zero);
    }
}
'@

# step = gesture, then the state it must produce:  fences visible?, icons visible?
$plans = @{
    Fences = @(@('shift', 1, 0), @('shift', 1, 1), @('plain', 0, 1), @('shift', 0, 0), @('shift', 0, 1), @('plain', 1, 1))
    Icons  = @(@('plain', 1, 0), @('plain', 1, 1), @('shift', 1, 0), @('shift', 1, 1))
    Both   = @(@('plain', 0, 0), @('plain', 1, 1), @('shift', 1, 0), @('plain', 0, 0), @('plain', 1, 0), @('shift', 1, 1))
    Off    = @(@('plain', 1, 1), @('shift', 1, 1))
}

function Quit-Corral {
    $running = Get-Process Corral -ErrorAction SilentlyContinue
    if (-not $running) { return $null }
    $path = $running[0].Path
    Start-Process $path -ArgumentList '--quit' -Wait
    foreach ($p in $running) { $null = $p.WaitForExit(8000) }
    if (Get-Process Corral -ErrorAction SilentlyContinue) { throw 'Corral did not quit gracefully' }
    $path
}

$failures = 0
$installed = Quit-Corral
$shell = New-Object -ComObject Shell.Application
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$orig = [System.Windows.Forms.Cursor]::Position
try {
    foreach ($mode in $Modes) {
        $cfg = Join-Path $WorkDir "$mode.json"
        [ordered]@{ DoubleClickAction = $mode; HideDesktopIcons = $false; Fences = @(
            [ordered]@{ Title = 'T1'; X = 560; Y = 140; Width = 400; Height = 220; Items = @() },
            [ordered]@{ Title = 'T2'; X = 1000; Y = 140; Width = 300; Height = 220; Items = @() }) } |
            ConvertTo-Json -Depth 5 | Set-Content $cfg -Encoding utf8

        $app = Start-Process $Exe -ArgumentList '--config', "`"$cfg`"" -PassThru
        Start-Sleep 3
        "== mode: $mode =="
        $shell.MinimizeAll(); Start-Sleep -Milliseconds 800

        $f0 = [Desk]::VisibleWindows([uint32]$app.Id) -gt 0; $i0 = [Desk]::IconsVisible()
        '   {0,-12} fences={1} icons={2}   (start)' -f '', [int]$f0, [int]$i0

        foreach ($step in $plans[$mode]) {
            $gesture, $wantF, $wantI = $step
            [Desk]::SetCursorPos($X, $Y) | Out-Null
            $p = [System.Windows.Forms.Cursor]::Position
            if ($p.X -ne $X -or $p.Y -ne $Y) { '   cursor moved by something else; skipping step'; $failures++; continue }
            [Desk]::DoubleClick($gesture -eq 'shift')
            Start-Sleep -Milliseconds 900
            $gotF = [int]([Desk]::VisibleWindows([uint32]$app.Id) -gt 0); $gotI = [int][Desk]::IconsVisible()
            $ok = ($gotF -eq $wantF) -and ($gotI -eq $wantI)
            if (-not $ok) { $failures++ }
            '   {0,-12} fences={1} icons={2}   want fences={3} icons={4}   {5}' -f "$gesture dbl-click", $gotF, $gotI, $wantF, $wantI, $(if ($ok) { 'PASS' } else { 'FAIL' })
        }
        $shell.UndoMinimizeALL(); Start-Sleep -Milliseconds 500
        Start-Process $Exe -ArgumentList '--quit' -Wait; $null = $app.WaitForExit(8000)
    }
}
finally {
    [System.Windows.Forms.Cursor]::Position = $orig
    try { $shell.UndoMinimizeALL() } catch {}
    Get-Process Corral -ErrorAction SilentlyContinue | ForEach-Object { Start-Process $_.Path -ArgumentList '--quit' -Wait }
    if ($installed -and -not (Get-Process Corral -ErrorAction SilentlyContinue)) { Start-Process $installed }
    "desktop icons visible at the end: $([Desk]::IconsVisible())"
}
if ($failures -eq 0) { 'ALL PASSED' } else { "$failures FAILED"; exit 1 }
