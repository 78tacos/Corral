# Dev helper: end-to-end check that shortcuts in a fence survive deletion of the original.
#   1. a fence references a shortcut on a fake "desktop"
#   2. start Corral: the shortcut must be copied into <config dir>\Links and the config rewritten to point at the copy
#   3. delete the original, start again: the item must still be there with its icon (screenshot via PrintWindow)
# Uses a throwaway config, so your real fences are not touched. It briefly quits any running Corral
# (single-instance rule) and starts it again at the end.
param([Parameter(Mandatory)][string]$Exe, [Parameter(Mandatory)][string]$WorkDir, [string]$RealConfig)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class Win {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    public static List<IntPtr> ForProcess(uint pid) {
        var l = new List<IntPtr>();
        EnumWindows((h, x) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero);
        return l;
    }
}
'@

function Check($name, $ok, $detail = '') { '{0,-62} {1}{2}' -f $name, $(if ($ok) { 'PASS' } else { 'FAIL' }), $(if (-not $ok -and $detail) { "  -> $detail" } else { '' }) }
function Quit-Corral {
    $running = Get-Process Corral -ErrorAction SilentlyContinue
    if (-not $running) { return }
    $path = $running[0].Path
    Start-Process $path -ArgumentList '--quit' -Wait
    foreach ($p in $running) { $null = $p.WaitForExit(8000) }
    if (Get-Process Corral -ErrorAction SilentlyContinue) { throw 'Corral did not quit gracefully' }
    $path
}
function Start-Test($cfg) { $p = Start-Process $Exe -ArgumentList '--config', "`"$cfg`"" -PassThru; Start-Sleep 4; $p }
function Stop-Test($p) { Start-Process $Exe -ArgumentList '--quit' -Wait; $null = $p.WaitForExit(8000) }
function Shot($p, $out) {
    foreach ($h in [Win]::ForProcess([uint32]$p.Id)) {
        $r = New-Object Win+RECT; [Win]::GetWindowRect($h, [ref]$r) | Out-Null
        $w = $r.R - $r.L; $ht = $r.B - $r.T; if ($w -le 0 -or $ht -le 0) { continue }
        $bmp = New-Object System.Drawing.Bitmap $w, $ht
        $g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
        [Win]::PrintWindow($h, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
        $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); return
    }
}

$installed = Quit-Corral          # remember where the user's own copy lives so we can restart it
try {
    # ---------------- A. synthetic scenario -------------------------------------------------------
    if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
    $desktop = Join-Path $WorkDir 'desktop'; New-Item -ItemType Directory -Force $desktop | Out-Null
    $folder = Join-Path $desktop 'A Folder'; New-Item -ItemType Directory -Force $folder | Out-Null
    $txt = Join-Path $desktop 'notes.txt'; Set-Content $txt 'hi'
    $sh = New-Object -ComObject WScript.Shell
    $lnk = Join-Path $desktop 'Notepad.lnk'
    $l = $sh.CreateShortcut($lnk); $l.TargetPath = "$env:WINDIR\notepad.exe"; $l.Save()

    $cfgPath = Join-Path $WorkDir 'config.json'
    [ordered]@{ Fences = @([ordered]@{ Title = 'Test'; X = 300; Y = 200; Width = 480; Height = 240; IconSize = 48; Items = @($lnk, $txt, $folder) }) } |
        ConvertTo-Json -Depth 5 | Set-Content $cfgPath -Encoding utf8

    $p = Start-Test $cfgPath; Stop-Test $p
    $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
    $items = @($cfg.Fences[0].Items)
    $links = Join-Path $WorkDir 'Links'
    "items after first start: " + ($items -join ' | ')
    Check 'shortcut now points into the Links folder' ($items[0] -like "$links\*") $items[0]
    Check 'the copy exists on disk' (Test-Path $items[0])
    Check 'text file still referenced in place (not copied)' ($items[1] -eq $txt)
    Check 'folder still referenced in place (not copied)' ($items[2] -eq $folder)
    Check 'original shortcut untouched' (Test-Path $lnk)
    Check 'Links folder holds exactly the one copy' ((Get-ChildItem $links).Count -eq 1)

    Remove-Item $lnk -Force        # <- the user's action: delete the old desktop icon
    $p = Start-Test $cfgPath
    Shot $p (Join-Path $WorkDir 'after-delete.png')
    Stop-Test $p
    $cfg2 = Get-Content $cfgPath -Raw | ConvertFrom-Json
    Check 'after deleting the original, the fence still holds the item' (@($cfg2.Fences[0].Items)[0] -eq $items[0])
    Check 'the copy is still there after the original was deleted' (Test-Path $items[0])
    "screenshot: $(Join-Path $WorkDir 'after-delete.png')"

    # ---------------- B. dry run on a COPY of the real config -----------------------------------------
    if ($RealConfig -and (Test-Path $RealConfig)) {
        $dry = Join-Path $WorkDir 'real-copy'; New-Item -ItemType Directory -Force $dry | Out-Null
        Copy-Item $RealConfig (Join-Path $dry 'config.json')
        $before = @((Get-Content (Join-Path $dry 'config.json') -Raw | ConvertFrom-Json).Fences.Items)
        $p = Start-Test (Join-Path $dry 'config.json'); Stop-Test $p
        $after = @((Get-Content (Join-Path $dry 'config.json') -Raw | ConvertFrom-Json).Fences.Items)
        $existed = @($before | Where-Object { Test-Path $_ })
        "real config copy: $($before.Count) items, $($existed.Count) originals currently exist"
        $stored = @($after | Where-Object { $_ -like "$dry\Links\*" })
        Check 'every existing original was copied into Links' ($stored.Count -eq $existed.Count) "stored=$($stored.Count) existed=$($existed.Count)"
        Check 'every migrated copy exists' (@($stored | Where-Object { -not (Test-Path $_) }).Count -eq 0)
        Check 'originals untouched by the dry run' (@($existed | Where-Object { -not (Test-Path $_) }).Count -eq 0)
    }
}
finally {
    Get-Process Corral -ErrorAction SilentlyContinue | ForEach-Object { Start-Process $_.Path -ArgumentList '--quit' -Wait }
    if ($installed -and -not (Get-Process Corral -ErrorAction SilentlyContinue)) { Start-Process $installed }
}
