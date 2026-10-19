# Generates Assets\corral.ico (multi-size, PNG-compressed) from code, so the icon is reproducible and editable.
# Usage: pwsh tools\make-icon.ps1 [-Preview path.png]
param([string]$OutDir = (Join-Path $PSScriptRoot '..\Assets'), [string]$Preview)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $d = $r * 2
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

# Draws the whole mark on a 256-unit canvas; `$detail` adds title bands/dots that only read at larger sizes.
function Draw-Mark([System.Drawing.Graphics]$g, [bool]$detail) {
    $g.SmoothingMode = 'AntiAlias'

    # tile
    $tile = New-RoundedPath 8 8 240 240 58
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(30, 0)), ([System.Drawing.PointF]::new(226, 256)), ([System.Drawing.Color]::FromArgb(255, 94, 140, 255)), ([System.Drawing.Color]::FromArgb(255, 38, 52, 150))
    $g.FillPath($bg, $tile)

    # glossy top highlight, clipped to the tile
    $g.SetClip($tile)
    $gloss = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 8)), ([System.Drawing.PointF]::new(0, 140)), ([System.Drawing.Color]::FromArgb(80, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.FillRectangle($gloss, 0, 8, 256, 132)
    $g.ResetClip()

    # three "fences": one tall on the left, two stacked on the right
    $panes = @(
        @{ X = 46; Y = 46; W = 78; H = 164; Accent = $false },
        @{ X = 136; Y = 46; W = 74; H = 76; Accent = $false },
        @{ X = 136; Y = 134; W = 74; H = 76; Accent = $true }
    )
    foreach ($p in $panes) {
        # soft shadow
        $sh = New-RoundedPath ($p.X + 0) ($p.Y + 5) $p.W $p.H 14
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 10, 16, 60))), $sh)

        $path = New-RoundedPath $p.X $p.Y $p.W $p.H 14
        $top = if ($p.Accent) { [System.Drawing.Color]::FromArgb(255, 255, 214, 102) } else { [System.Drawing.Color]::FromArgb(255, 255, 255, 255) }
        $bot = if ($p.Accent) { [System.Drawing.Color]::FromArgb(255, 255, 177, 66) } else { [System.Drawing.Color]::FromArgb(255, 214, 226, 255) }
        $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, $p.Y)), ([System.Drawing.PointF]::new(0, $p.Y + $p.H)), $top, $bot
        $g.FillPath($fill, $path)

        if ($detail) {
            # title band
            $g.SetClip($path)
            $band = if ($p.Accent) { [System.Drawing.Color]::FromArgb(70, 160, 80, 0) } else { [System.Drawing.Color]::FromArgb(60, 40, 70, 190) }
            $g.FillRectangle((New-Object System.Drawing.SolidBrush $band), $p.X, $p.Y, $p.W, 20)
            $g.ResetClip()
            # little icon dots
            $dot = if ($p.Accent) { [System.Drawing.Color]::FromArgb(120, 150, 70, 0) } else { [System.Drawing.Color]::FromArgb(110, 50, 80, 200) }
            $db = New-Object System.Drawing.SolidBrush $dot
            $cols = [math]::Floor(($p.W - 14) / 24)
            $rows = [math]::Min(3, [math]::Floor(($p.H - 36) / 26))
            for ($r = 0; $r -lt $rows; $r++) {
                for ($c = 0; $c -lt $cols; $c++) {
                    $g.FillPath($db, (New-RoundedPath ($p.X + 10 + $c * 24) ($p.Y + 32 + $r * 28) 16 16 5))
                }
            }
        }
    }

    # inner rim on the tile
    $rim = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 255, 255, 255)), 2
    $g.DrawPath($rim, (New-RoundedPath 9.5 9.5 237 237 57))
}

function Render([int]$size) {
    # supersample from 1024 for clean edges at tiny sizes
    $big = New-Object System.Drawing.Bitmap 1024, 1024
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.ScaleTransform(4, 4)
    Draw-Mark $g ($size -ge 32)
    $g.Dispose()

    $out = New-Object System.Drawing.Bitmap $size, $size
    $g2 = [System.Drawing.Graphics]::FromImage($out)
    $g2.InterpolationMode = 'HighQualityBicubic'
    $g2.PixelOffsetMode = 'HighQuality'
    $g2.CompositingQuality = 'HighQuality'
    $g2.DrawImage($big, 0, 0, $size, $size)
    $g2.Dispose(); $big.Dispose()
    $out
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = Render $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($s -eq 256 -and $Preview) { $bmp.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    , @($s, $ms.ToArray())
}

# ICO container: ICONDIR + ICONDIRENTRY[] + PNG blobs
$file = Join-Path $OutDir 'corral.ico'
$fs = [System.IO.File]::Create($file)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($entry in $pngs) {
    $s = $entry[0]; $data = $entry[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$data.Length); $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($entry in $pngs) { $bw.Write($entry[1]) }
$bw.Close(); $fs.Close()
"wrote $file ($((Get-Item $file).Length) bytes, $($pngs.Count) sizes)"
