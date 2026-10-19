# Dev helper: renders the icon at tray sizes (zoomed 6x, nearest-neighbour) on dark and light, to eyeball legibility.
param([string]$Ico, [string]$Exe, [string]$Out)
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 48
$zoom = 6
$w = 20 + ($sizes | ForEach-Object { $_ * $zoom + 20 } | Measure-Object -Sum).Sum
$h = 2 * (48 * $zoom + 30)
$sheet = New-Object System.Drawing.Bitmap ([int]$w), ([int]$h)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'

$y = 0
foreach ($bgColor in @([System.Drawing.Color]::FromArgb(32, 32, 36), [System.Drawing.Color]::FromArgb(243, 243, 243))) {
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $bgColor), 0, $y, $w, 48 * $zoom + 30)
    $x = 20
    foreach ($s in $sizes) {
        $icon = New-Object System.Drawing.Icon $Ico, (New-Object System.Drawing.Size $s, $s)
        $bmp = $icon.ToBitmap()
        $g.DrawImage($bmp, $x, $y + 15, $s * $zoom, $s * $zoom)
        $x += $s * $zoom + 20
    }
    $y += 48 * $zoom + 30
}
$sheet.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)

if ($Exe) {
    $ex = [System.Drawing.Icon]::ExtractAssociatedIcon($Exe)
    "exe icon extracted: $($ex.Width)x$($ex.Height)"
}
