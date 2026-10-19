# Builds the installer: publishes the app, then compiles installer\Corral.iss with Inno Setup.
# Usage:  pwsh installer\build.ps1
# Needs Inno Setup 6:  winget install JRSoftware.InnoSetup
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# --- locate the compiler ---------------------------------------------------------------------------
$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }

# --- version comes from the csproj so the app and installer always agree -------------------------------
[xml]$proj = Get-Content (Join-Path $root 'Corral.csproj')
$version = ($proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw 'No <Version> in Corral.csproj' }

# --- publish (framework-dependent single file; the installer checks for the .NET 10 Desktop Runtime) --------
$dist = Join-Path $root 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
dotnet publish (Join-Path $root 'Corral.csproj') -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o $dist
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# --- compile the installer ------------------------------------------------------------------------------
& $iscc "/DAppVersion=$version" (Join-Path $PSScriptRoot 'Corral.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compile failed' }

$setup = Join-Path $PSScriptRoot "Output\Corral-Setup-$version.exe"
"{0}`n{1:N2} MB" -f $setup, ((Get-Item $setup).Length / 1MB)
