# Generates FluxKit's icons as multi-size .ico files and PNGs.
# Usage: pwsh scripts/generate-icons.ps1
#
# App mark: a gradient tile holding a 2x2 grid of tool tiles, where the fourth tile is a spark ("flux").
# The grid stands for the collection of tools; new tools don't need a new app icon.
# Tool marks: one PNG per tool, shown in the navigation, on Home and on the tool's page.
#   DragToSwitch: two offset desktop tiles with a curved arrow from one to the other.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\FluxKit.App\Assets'
New-Item -ItemType Directory -Force $out | Out-Null

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

# Four-point spark centred on (cx, cy).
function New-Spark([float]$cx, [float]$cy, [float]$r) {
    $inner = $r * 0.3
    $pts = for ($i = 0; $i -lt 8; $i++) {
        $a = [Math]::PI / 4 * $i - [Math]::PI / 2
        $len = if ($i % 2 -eq 0) { $r } else { $inner }
        [System.Drawing.PointF]::new($cx + [Math]::Cos($a) * $len, $cy + [Math]::Sin($a) * $len)
    }
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $p.AddPolygon([System.Drawing.PointF[]]$pts)
    return $p
}

function Color([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function Draw-Grid($g, [float]$s, [float]$origin, [float]$tile, [float]$gap, [float]$radius, $c1, $c2, $c3, $spark) {
    $far = $origin + $tile + $gap
    $g.FillPath([System.Drawing.SolidBrush]::new($c1), (New-RoundRect ($origin * $s) ($origin * $s) ($tile * $s) ($tile * $s) ($radius * $s)))
    $g.FillPath([System.Drawing.SolidBrush]::new($c2), (New-RoundRect ($far * $s) ($origin * $s) ($tile * $s) ($tile * $s) ($radius * $s)))
    $g.FillPath([System.Drawing.SolidBrush]::new($c3), (New-RoundRect ($origin * $s) ($far * $s) ($tile * $s) ($tile * $s) ($radius * $s)))
    $centre = ($far + $tile / 2) * $s
    $g.FillPath([System.Drawing.SolidBrush]::new($spark), (New-Spark $centre $centre (($tile / 2 + 1.2) * $s)))
}

function New-Frame([int]$size, [string]$variant) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 32.0
    switch ($variant) {
        'app' {
            $bg = New-RoundRect (1 * $s) (1 * $s) (30 * $s) (30 * $s) (7.5 * $s)
            $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
                [System.Drawing.PointF]::new(0, 0), [System.Drawing.PointF]::new($size, $size),
                (Color 255 99 91 255), (Color 255 0 178 216))
            $g.FillPath($brush, $bg)
            $white = [System.Drawing.Color]::White
            Draw-Grid $g $s 7.5 7.5 2 2.2 $white (Color 215 255 255 255) (Color 215 255 255 255) $white
        }
        { $_ -in 'traydark', 'traylight', 'trayoff' } {
            $fg = switch ($variant) { 'traydark' { Color 255 255 255 255 } 'traylight' { Color 255 28 28 28 } 'trayoff' { Color 170 128 128 128 } }
            $soft = [System.Drawing.Color]::FromArgb([int]($fg.A * 0.7), $fg.R, $fg.G, $fg.B)
            Draw-Grid $g $s 3 12 2.5 3 $fg $soft $soft $fg
            if ($variant -eq 'trayoff') {
                $slash = [System.Drawing.Pen]::new((Color 255 196 43 28), [Math]::Max(1.5, 2.6 * $s)); $slash.StartCap = 'Round'; $slash.EndCap = 'Round'
                $g.DrawLine($slash, 4 * $s, 4 * $s, 28 * $s, 28 * $s)
            }
        }
        'dragtoswitch' {
            $g.FillPath([System.Drawing.SolidBrush]::new((Color 255 96 205 255)), (New-RoundRect (12 * $s) (3 * $s) (17 * $s) (14 * $s) (3.5 * $s)))
            $g.FillPath([System.Drawing.SolidBrush]::new((Color 255 0 103 192)), (New-RoundRect (3 * $s) (12 * $s) (19 * $s) (16 * $s) (4 * $s)))
            $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, [Math]::Max(1.5, 2.4 * $s))
            $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
            $g.DrawBezier($pen, 7 * $s, 24 * $s, 9 * $s, 17 * $s, 13 * $s, 17 * $s, 17 * $s, 19.5 * $s)
            $g.DrawLines($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(13.5 * $s, 21 * $s), [System.Drawing.PointF]::new(17.5 * $s, 19.8 * $s), [System.Drawing.PointF]::new(16.6 * $s, 15.8 * $s)))
        }
    }
    $g.Dispose(); return $bmp
}

function Save-Ico([string]$path, [int[]]$sizes, [string]$variant) {
    $pngs = foreach ($sz in $sizes) { $ms = [IO.MemoryStream]::new(); (New-Frame $sz $variant).Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); , $ms.ToArray() }
    $fs = [IO.File]::Create($path); $w = [IO.BinaryWriter]::new($fs)
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz = $sizes[$i]; $dim = if ($sz -ge 256) { 0 } else { $sz }
        $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $w.Write($png) }
    $w.Dispose()
}

Save-Ico (Join-Path $out 'FluxKit.ico') @(16, 20, 24, 32, 40, 48, 64, 256) 'app'
foreach ($v in 'traydark', 'traylight', 'trayoff') { Save-Ico (Join-Path $out "Tray-$v.ico") @(16, 20, 24, 32, 40, 48) $v }
(New-Frame 256 'app').Save((Join-Path $out 'FluxKit.png'), [System.Drawing.Imaging.ImageFormat]::Png)
(New-Frame 128 'dragtoswitch').Save((Join-Path $out 'DragToSwitch.png'), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Icons written to $out"
