# Dev helper: launches Corral with a throwaway config and captures each fence window via PrintWindow
# (no need to minimise anything on the real desktop).
param([string]$Exe, [string]$OutDir, [int]$WaitSeconds = 4)

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Win {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    public static List<IntPtr> ForProcess(uint pid) {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
}
'@

New-Item -ItemType Directory -Force $OutDir | Out-Null
$cfgPath = Join-Path $OutDir 'config.json'

function F($t, $x, $y, $w, $h, $roll, $sz, $portal, $items, $tint = $null) {
    [ordered]@{ Title = $t; X = $x; Y = $y; Width = $w; Height = $h; RolledUp = $roll; IconSize = $sz; PortalPath = $portal; Items = @($items); Tint = $tint }
}
$cfg = [ordered]@{ Opacity = 55; Tint = '#12141A'; DoubleClickHidesIcons = $true; SnapToEdges = $true; PeekOnHover = $true; Fences = @(
    (F 'Programs' 560 140 440 270 $false 48 $null @('C:\Windows\notepad.exe', 'C:\Windows\System32\calc.exe', 'C:\Windows\explorer.exe', 'C:\Windows\System32\mspaint.exe', 'C:\Windows\System32\cmd.exe', 'C:\Windows\System32\taskmgr.exe', 'C:\Windows\regedit.exe') '#1B4FB8'),
    (F 'Folders' 1040 140 320 200 $false 32 $null @('C:\Windows', 'C:\Users', 'C:\Program Files') '#B02A2A'),
    (F 'Rolled up' 1040 380 320 200 $true 48 $null @('C:\Windows\notepad.exe') '#1F7A3E'),
    (F 'Windows\Web (portal)' 560 450 440 270 $false 64 'C:\Windows\Web' @())
) }
$cfg | ConvertTo-Json -Depth 5 | Set-Content $cfgPath -Encoding utf8

Get-Process Corral -ErrorAction SilentlyContinue | Stop-Process -Force
$proc = Start-Process $Exe -ArgumentList '--config', "`"$cfgPath`"" -PassThru
Start-Sleep $WaitSeconds

$i = 0
foreach ($h in [Win]::ForProcess([uint32]$proc.Id)) {
    $r = New-Object Win+RECT
    [Win]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.R - $r.L; $ht = $r.B - $r.T
    if ($w -le 0 -or $ht -le 0) { continue }
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    [Win]::PrintWindow($h, $dc, 2) | Out-Null   # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($dc)
    $bmp.Save((Join-Path $OutDir "fence$i.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    "fence$i.png  ${w}x${ht} at $($r.L),$($r.T)"
    $i++
}
$log = Join-Path $OutDir 'corral.log'
if (Test-Path $log) { "--- LOG ---"; Get-Content $log | Select-Object -First 15 }
