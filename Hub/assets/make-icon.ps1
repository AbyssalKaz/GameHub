Add-Type -AssemblyName System.Drawing

$output = Join-Path $PSScriptRoot 'app.ico'
$sizes = 256, 64, 48, 32, 24, 16

function New-RoundedPath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconImage([int]$size) {
    $bitmap = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $inset = if ($size -ge 48) { $size * 0.06 } else { 0 }
    $box = $size - 2 * $inset
    $tile = New-RoundedPath $inset $inset $box $box ($box * 0.24)
    $gradient = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.PointF $inset, $inset), (New-Object Drawing.PointF ($inset + $box), ($inset + $box)), ([Drawing.Color]::FromArgb(255, 111, 134, 255)), ([Drawing.Color]::FromArgb(255, 154, 107, 255))
    $graphics.FillPath($gradient, $tile)

    $glyph = $box * 0.6
    $scale = $glyph / 24
    $offset = $inset + ($box - 24 * $scale) / 2
    $solid = New-Object Drawing.SolidBrush ([Drawing.Color]::White)
    $soft = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(140, 255, 255, 255))

    foreach ($square in @(@(3.5, 3.5, $solid), @(13.5, 3.5, $soft), @(3.5, 13.5, $soft))) {
        $path = New-RoundedPath ($offset + $square[0] * $scale) ($offset + $square[1] * $scale) (7 * $scale) (7 * $scale) (2 * $scale)
        $graphics.FillPath($square[2], $path)
    }

    $triangle = [Drawing.PointF[]]@(
        (New-Object Drawing.PointF ($offset + 14.4 * $scale), ($offset + 13.6 * $scale)),
        (New-Object Drawing.PointF ($offset + 14.4 * $scale), ($offset + 20.4 * $scale)),
        (New-Object Drawing.PointF ($offset + 20.6 * $scale), ($offset + 17.0 * $scale))
    )
    $graphics.FillPolygon($solid, $triangle)
    $graphics.Dispose()
    return $bitmap
}

function ConvertTo-IconFrame([Drawing.Bitmap]$bitmap) {
    $size = $bitmap.Width
    $stream = New-Object IO.MemoryStream
    if ($size -ge 256) {
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }

    $writer = New-Object IO.BinaryWriter $stream
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $writer.Write([uint32]40)
    $writer.Write([int32]$size)
    $writer.Write([int32]($size * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)
    $writer.Write([uint32]($size * $size * 4 + $maskStride * $size))
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $pixel = $bitmap.GetPixel($x, $y)
            $writer.Write([byte]$pixel.B)
            $writer.Write([byte]$pixel.G)
            $writer.Write([byte]$pixel.R)
            $writer.Write([byte]$pixel.A)
        }
    }
    $writer.Write((New-Object byte[] ($maskStride * $size)))
    $writer.Flush()
    return $stream.ToArray()
}

$images = foreach ($size in $sizes) {
    $bitmap = New-IconImage $size
    if ($size -eq 256 -and $args[0]) { $bitmap.Save($args[0], [Drawing.Imaging.ImageFormat]::Png) }
    [byte[]]$data = ConvertTo-IconFrame $bitmap
    $bitmap.Dispose()
    [pscustomobject]@{ Size = $size; Data = $data }
}

$file = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter $file
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Data.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Data.Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image.Data, 0, $image.Data.Length) }
$writer.Flush()
[IO.File]::WriteAllBytes($output, $file.ToArray())
Write-Host "Wrote $output"
