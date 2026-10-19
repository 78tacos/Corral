# Dev helper: exercises the installer end to end (silent) in a throwaway folder, then cleans up after itself.
#   install -> files/shortcut/uninstall entry exist -> run app -> upgrade over the running app -> uninstall.
param([Parameter(Mandatory)][string]$Setup, [Parameter(Mandatory)][string]$WorkDir, [switch]$Force)
$ErrorActionPreference = 'Stop'

# The test installs with the real AppId, so on a PC that already has Corral installed it would overwrite and
# then delete that install's uninstall entry and start-at-sign-in setting. Refuse unless explicitly forced.
$realKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{5F1B2C4E-7A3D-4E8B-9C21-6D0A8B7E3F54}_is1'
if ((Test-Path $realKey) -and -not $Force) {
    throw 'Corral is installed on this PC. Uninstall it first (or run on a clean VM), or pass -Force if you accept that this test will replace and then remove its uninstall entry.'
}

New-Item -ItemType Directory -Force $WorkDir | Out-Null
$dir = Join-Path $WorkDir 'installed'
$cfg = Join-Path $WorkDir 'config.json'
$log = Join-Path $WorkDir 'setup.log'
$appId = '{5F1B2C4E-7A3D-4E8B-9C21-6D0A8B7E3F54}_is1'
$uninstKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appId"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Corral.lnk'

function Check($name, $ok) { '{0,-58} {1}' -f $name, $(if ($ok) { 'PASS' } else { 'FAIL' }) }

Get-Process Corral -ErrorAction SilentlyContinue | Stop-Process -Force

# ---- 1. fresh install (startup task NOT selected) --------------------------------------------------
$p = Start-Process $Setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$dir`"", '/TASKS=""', "/LOG=`"$log`"" -Wait -PassThru
Check 'setup exit code 0' ($p.ExitCode -eq 0)
Check 'Corral.exe installed' (Test-Path "$dir\Corral.exe")
Check 'uninstaller installed' (Test-Path "$dir\unins000.exe")
Check 'Start menu shortcut created' (Test-Path $lnk)
Check 'Add/Remove Programs entry created' (Test-Path $uninstKey)
Check 'Run-at-sign-in NOT set (task unchecked)' (-not (Get-ItemProperty $runKey -Name Corral -ErrorAction SilentlyContinue))
$ver = (Get-Item "$dir\Corral.exe").VersionInfo.ProductVersion
"   installed exe version: $ver"

# ---- 2. app runs from the install, then --quit exits it gracefully -----------------------------------
$app = Start-Process "$dir\Corral.exe" -ArgumentList '--config', "`"$cfg`"" -PassThru
Start-Sleep 3
Check 'installed app runs' (-not $app.HasExited)
$sw = [Diagnostics.Stopwatch]::StartNew()
Start-Process "$dir\Corral.exe" -ArgumentList '--quit' -Wait
$null = $app.WaitForExit(8000)
$sw.Stop()
Check "--quit exits the running app gracefully ($([int]$sw.ElapsedMilliseconds) ms)" ($app.HasExited -and $sw.ElapsedMilliseconds -lt 4000)

# ---- 3. upgrade over a RUNNING app (installer must stop it itself) -----------------------------------
$app = Start-Process "$dir\Corral.exe" -ArgumentList '--config', "`"$cfg`"" -PassThru
Start-Sleep 3
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $Setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$dir`"", '/TASKS=startup', "/LOG=`"$log`"" -Wait -PassThru
$sw.Stop()
Check "upgrade over running app succeeded (exit $($p.ExitCode), $([int]$sw.Elapsed.TotalSeconds)s)" ($p.ExitCode -eq 0)
Check 'running app was closed by the installer' ($app.HasExited)
Check 'Run-at-sign-in set (startup task selected)' ([bool](Get-ItemProperty $runKey -Name Corral -ErrorAction SilentlyContinue))

# ---- 4. uninstall --------------------------------------------------------------------------------
$p = Start-Process "$dir\unins000.exe" -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
Start-Sleep 2
Check 'uninstall exit code 0' ($p.ExitCode -eq 0)
Check 'Corral.exe removed' (-not (Test-Path "$dir\Corral.exe"))
Check 'Start menu shortcut removed' (-not (Test-Path $lnk))
Check 'Add/Remove Programs entry removed' (-not (Test-Path $uninstKey))
Check 'Run-at-sign-in entry removed' (-not (Get-ItemProperty $runKey -Name Corral -ErrorAction SilentlyContinue))

Get-Process Corral -ErrorAction SilentlyContinue | Stop-Process -Force
