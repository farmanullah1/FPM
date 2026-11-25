param(
    [string]$OutPath = '.fpm-d4/fpm-new.ico',
    [int[]]$Sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
)

Add-Type -AssemblyName System.Drawing

$frames = @()

foreach ($size in $Sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 24.0

    function P([float[]]$pts) {
        return ,@($pts | ForEach-Object { $_ * $script:s })
    }

    $facets = @(
        @{ Color = '#FDE68A'; Points = @(12,1, 12,12, 4,7) },      # top
        @{ Color = '#FACC15'; Points = @(23,12, 12,12, 17,5) },     # right
        @{ Color = '#D97706'; Points = @(12,23, 12,12, 20,17) },    # bottom
        @{ Color = '#F59E0B'; Points = @(1,12, 12,12, 7,19) },      # left
        @{ Color = '#FFFBEB'; Points = @(12,9.4, 14.6,12, 12,14.6, 9.4,12) } # centre
    )

    foreach ($facet in $facets) {
        $flat = $facet.Points | ForEach-Object { $_ * $s }
        $points = New-Object 'System.Drawing.PointF[]' ($flat.Count / 2)
        for ($i = 0; $i -lt $points.Length; $i++) {
            $points[$i] = New-Object System.Drawing.PointF ([float]$flat[$i * 2]), ([float]$flat[$i * 2 + 1])
        }

        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($facet.Color))
        $g.FillPolygon($brush, $points)
        $brush.Dispose()
    }

    $g.Dispose()

    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@{ Size = $size; Bytes = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

$out = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter $out
$writer.Write([UInt16]0)                 # reserved
$writer.Write([UInt16]1)                 # icon
$writer.Write([UInt16]$frames.Count)

$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dim = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)               # palette
    $writer.Write([byte]0)               # reserved
    $writer.Write([UInt16]1)             # planes
    $writer.Write([UInt16]32)            # bpp
    $writer.Write([Int32]$frame.Bytes.Length)
    $writer.Write([Int32]$offset)
    $offset += $frame.Bytes.Length
}

foreach ($frame in $frames) { $writer.Write($frame.Bytes) }
$writer.Flush()
[IO.File]::WriteAllBytes($OutPath, $out.ToArray())
$writer.Dispose(); $out.Dispose()

Write-Output ("wrote {0} frames to {1}" -f $frames.Count, $OutPath)
