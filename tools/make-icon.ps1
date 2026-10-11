# Generate assets/app.ico from assets/controller-source.png:
# the source gamepad glyph (icons8 style) is pixel-cloned at each size, recolored to
# the charcoal ramp, and a white vertical outline battery (~72% fill, wide terminal
# cap on top) is composited over the left grip. Sizes: 16/20/24/32/48/64/128/256.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1
# NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less UTF-8 as
# ANSI/GBK, which corrupts non-ASCII comments and breaks script execution.
# NOTE: PowerShell variables are CASE-INSENSITIVE -- never name two variables $S/$s.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repo = Split-Path -Parent $PSScriptRoot
$srcPng = Join-Path $repo 'assets\controller-source.png'
$outIco = Join-Path $repo 'assets\app.ico'
$outPreview = Join-Path $env:TEMP 'app-icon-preview.png'

# charcoal ramp: reference purple body (114,89,255) -> Base, light details (191,179,255) -> Light
$baseR = 58;   $baseG = 62;   $baseB = 70
$lightR = 165; $lightG = 171; $lightB = 181

$ref = New-Object System.Drawing.Bitmap($srcPng)

function RR($g, $color, $x, $y, $w, $h, $r) {
    if ($w -le 0 -or $h -le 0) { return }
    $brush = New-Object System.Drawing.SolidBrush($color)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * [Math]::Min($r, [Math]::Min($w / 2, $h / 2))
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)
    $path.Dispose(); $brush.Dispose()
}

# battery on its own transparent layer; the window is punched out so the
# controller shows through; drawn in 96-base coords (source is 96px)
function New-BatteryLayer([int]$size) {
    $zoom = $size / 96.0
    $k = (33.0 * $zoom) / 348.0        # shell length 33/96 px in source space
    $shellW = 185 * $k
    $stroke = 22 * $k
    $cx = 16.0 * $zoom
    $y1 = 66.0 * $zoom
    $y0 = $y1 - 33.0 * $zoom

    $layer = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($layer)
    try {
        $g.SmoothingMode = 'AntiAlias'
        $white = [System.Drawing.Color]::White
        $x0 = $cx - $shellW / 2
        # shell
        RR $g $white $x0 $y0 $shellW ($y1 - $y0) (45 * $k)
        # punch window to transparent
        $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $trans = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
        RR $g $trans ($x0 + $stroke) ($y0 + $stroke) ($shellW - 2 * $stroke) (($y1 - $y0) - 2 * $stroke) (26 * $k)
        $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        # fill bar from bottom, ~72% of window height
        $fillLen = 0.72 * (($y1 - $y0) - 2 * $stroke)
        $inset = 6 * $k
        RR $g $white ($x0 + $stroke + $inset * 0.5) ($y1 - $stroke - $fillLen) ($shellW - 2 * $stroke - $inset) ($fillLen - $inset * 0.5) (18 * $k)
        # terminal cap: wide (113 across), protrudes 22 above the shell, detached by 15
        $nubW = 113 * $k
        $nubH = [Math]::Max(22 * $k, 1.2)
        RR $g $white ($cx - $nubW / 2) ($y0 - 15 * $k - $nubH) $nubW $nubH ($nubH / 2)
    }
    finally { $g.Dispose() }
    return $layer
}

function New-AppIconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.SmoothingMode = 'AntiAlias'
        $g.PixelOffsetMode = 'HighQuality'
        $g.DrawImage($ref, 0, 0, $size, $size)
    }
    finally { $g.Dispose() }

    # recolor: reference ramp R 114..191 maps onto charcoal Base..Light
    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) {
            $px = $bmp.GetPixel($x, $y)
            if ($px.A -lt 8) { continue }
            $t = [Math]::Max(0.0, [Math]::Min(1.0, ($px.R - 114) / 77.0))
            $nr = [int]($baseR + $t * ($lightR - $baseR))
            $ng = [int]($baseG + $t * ($lightG - $baseG))
            $nb = [int]($baseB + $t * ($lightB - $baseB))
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($px.A, $nr, $ng, $nb))
        }
    }

    $bat = New-BatteryLayer $size
    $g2 = [System.Drawing.Graphics]::FromImage($bmp)
    try { $g2.DrawImage($bat, 0, 0) }
    finally { $g2.Dispose(); $bat.Dispose() }
    return $bmp
}

# --- assemble the ICO file (PNG-compressed entries, supported since Vista) ---
$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-AppIconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += ,([PSCustomObject]@{ Size = $s; Bytes = $ms.ToArray() })
    $ms.Dispose()
}

$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ico)
$bw.Write([uint16]0)                    # reserved
$bw.Write([uint16]1)                    # type: icon
$bw.Write([uint16]$sizes.Count)         # entry count
$offset = 6 + 16 * $sizes.Count
foreach ($p in $pngs) {
    $dim = if ($p.Size -ge 256) { 0 } else { $p.Size }
    $bw.Write([byte]$dim)               # width
    $bw.Write([byte]$dim)               # height
    $bw.Write([byte]0)                  # palette colors
    $bw.Write([byte]0)                  # reserved
    $bw.Write([uint16]1)                # color planes
    $bw.Write([uint16]32)               # bits per pixel
    $bw.Write([uint32]$p.Bytes.Length)  # data length
    $bw.Write([uint32]$offset)          # data offset
    $offset += $p.Bytes.Length
}
foreach ($p in $pngs) { $bw.Write($p.Bytes) }
$bw.Flush()

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outIco) | Out-Null
[System.IO.File]::WriteAllBytes($outIco, $ico.ToArray())
$bw.Dispose(); $ico.Dispose()
Write-Output "ICO: $outIco ($((Get-Item $outIco).Length) bytes, $($sizes.Count) sizes)"

# --- preview sheet (all sizes in one row, for manual inspection) ---
$sum = ($sizes | Measure-Object -Sum).Sum
$previewW = [int](16 + $sum + 12 * $sizes.Count)
$preview = New-Object System.Drawing.Bitmap($previewW, 272)
$pg = [System.Drawing.Graphics]::FromImage($preview)
try {
    $pg.Clear([System.Drawing.Color]::FromArgb(0xFF, 0x30, 0x30, 0x30))
    $x = 8
    foreach ($s in $sizes) {
        $b = New-AppIconBitmap $s
        $pg.DrawImage($b, $x, ((256 - $s) / 2 + 8), $s, $s)
        $b.Dispose()
        $x += $s + 12
    }
}
finally { $pg.Dispose() }
$preview.Save($outPreview, [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
Write-Output "Preview: $outPreview"
