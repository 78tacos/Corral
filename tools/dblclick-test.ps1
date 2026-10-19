# Dev helper: show the desktop, double-click an empty spot twice, screenshot after each, then restore.
# Timing is kept tight (and the cursor position is verified before each click) so other activity can't interfere.
param([int]$X = 1725, [int]$Y = 690, [string]$OutDir)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Mouse {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    public static void Click() { mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); }
}
'@

function Shot($name) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
    $small = New-Object System.Drawing.Bitmap $bmp, ([int]($b.Width / 2)), ([int]($b.Height / 2))
    $small.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
}

function DoubleClickAt($x, $y) {
    [Mouse]::SetCursorPos($x, $y) | Out-Null
    $p = [System.Windows.Forms.Cursor]::Position
    if ($p.X -ne $x -or $p.Y -ne $y) { return $false }   # someone moved the mouse: don't click blind
    [Mouse]::Click(); Start-Sleep -Milliseconds 60; [Mouse]::Click()
    $true
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$orig = [System.Windows.Forms.Cursor]::Position
$shell = New-Object -ComObject Shell.Application
try {
    $shell.MinimizeAll(); Start-Sleep -Milliseconds 900
    Shot 'dc0-before.png'

    "first double-click sent: $(DoubleClickAt $X $Y)"
    Start-Sleep -Milliseconds 1100
    Shot 'dc1-after-first-double.png'

    "second double-click sent: $(DoubleClickAt $X $Y)"
    Start-Sleep -Milliseconds 1100
    Shot 'dc2-after-second-double.png'
}
finally {
    [System.Windows.Forms.Cursor]::Position = $orig
    $shell.UndoMinimizeALL()
}
