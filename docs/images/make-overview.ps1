# Builds overview.png, the montage used as the README header, out of the screenshots beside it.
# Re-run it after replacing any of the six it uses, they are listed below.
# Windows only, it draws with System.Drawing.

Add-Type -AssemblyName System.Drawing

# beside this script, so it runs from any working directory and from any clone.
$images = $PSScriptRoot
$out = Join-Path $images 'overview.png'

# the six, in reading order, three across and two down.
$cells = @(
    @{ File = 'fluentui-gallery.png'; Caption = 'Fluent UI gallery' },
    @{ File = 'react-dashboard.png';  Caption = 'React dashboard' },
    @{ File = 'translucid.png';       Caption = 'Translucent window' },
    @{ File = 'host-objects.png';     Caption = '.NET / JS bridge' },
    @{ File = 'direct2d.png';         Caption = 'Direct2D to canvas' },
    @{ File = 'blazor-diskmap.png';   Caption = 'Blazor DiskMap' }
)

$width = 1948
$height = 982
$margin = 42
$gapX = 29
$gapY = 26
$cols = 3
$rows = 2

$cardW = [int](($width - (2 * $margin) - (($cols - 1) * $gapX)) / $cols)
$cardH = [int](($height - (2 * $margin) - (($rows - 1) * $gapY)) / $rows)

$padding = 20
$captionBand = 48
$radius = 14

$background = [System.Drawing.Color]::FromArgb(12, 13, 15)
$card = [System.Drawing.Color]::FromArgb(23, 25, 29)
$cardEdge = [System.Drawing.Color]::FromArgb(36, 39, 44)
$captionColour = [System.Drawing.Color]::FromArgb(201, 206, 214)

function New-RoundedPath([int]$x, [int]$y, [int]$w, [int]$h, [int]$r)
{
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
    $path.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

$bmp = New-Object System.Drawing.Bitmap $width, $height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.InterpolationMode = 'HighQualityBicubic'
$g.PixelOffsetMode = 'HighQuality'
$g.TextRenderingHint = 'ClearTypeGridFit'
$g.Clear($background)

$cardBrush = New-Object System.Drawing.SolidBrush $card
$edgePen = New-Object System.Drawing.Pen $cardEdge, 1
$captionBrush = New-Object System.Drawing.SolidBrush $captionColour
$captionFont = New-Object System.Drawing.Font 'Segoe UI', 15
$format = New-Object System.Drawing.StringFormat
$format.Alignment = 'Center'
$format.LineAlignment = 'Center'

for ($i = 0; $i -lt $cells.Count; $i++)
{
    $col = $i % $cols
    $row = [math]::Floor($i / $cols)
    $x = $margin + $col * ($cardW + $gapX)
    $y = $margin + $row * ($cardH + $gapY)

    $path = New-RoundedPath $x $y $cardW $cardH $radius
    $g.FillPath($cardBrush, $path)
    $g.DrawPath($edgePen, $path)
    $path.Dispose()

    $file = Join-Path $images $cells[$i].File
    if (Test-Path $file)
    {
        $img = [System.Drawing.Image]::FromFile($file)

        # fit inside the content box, keeping the aspect ratio and never scaling a small shot up.
        $boxW = $cardW - (2 * $padding)
        $boxH = $cardH - $padding - $captionBand
        $scale = [math]::Min($boxW / $img.Width, $boxH / $img.Height)
        if ($scale -gt 1) { $scale = 1 }
        $w = [int]($img.Width * $scale)
        $h = [int]($img.Height * $scale)
        $ix = $x + [int](($cardW - $w) / 2)
        $iy = $y + $padding + [int](($boxH - $h) / 2)

        $g.DrawImage($img, $ix, $iy, $w, $h)
        $img.Dispose()
    }
    else
    {
        Write-Warning "missing $file"
    }

    $captionRect = New-Object System.Drawing.RectangleF $x, ($y + $cardH - $captionBand), $cardW, $captionBand
    $g.DrawString($cells[$i].Caption, $captionFont, $captionBrush, $captionRect, $format)
}

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

"wrote $out ({0:N0} KB)" -f ((Get-Item $out).Length / 1KB)
