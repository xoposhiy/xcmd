$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Xcmd.Desktop/Assets'
$frames = @()
foreach ($size in @(16, 20, 24, 28, 32, 40, 48, 64, 96, 128, 256)) {
    $variant = Join-Path $assetPath ('app-' + $size + '.svg')
    $pixelAligned = Test-Path -LiteralPath $variant
    $source = if ($pixelAligned) { $variant } else { Join-Path $assetPath 'app.svg' }
    [xml]$drawing = Get-Content -Raw -LiteralPath $source
    $samples = if ($pixelAligned) { 1 } else { 4 }
    $canvas = [System.Drawing.Bitmap]::new($size * $samples, $size * $samples)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    $graphics.SmoothingMode = if ($pixelAligned) { [System.Drawing.Drawing2D.SmoothingMode]::None } else { [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias }
    $graphics.ScaleTransform($size * $samples / [single]$drawing.svg.width, $size * $samples / [single]$drawing.svg.height)
    foreach ($element in $drawing.svg.ChildNodes) {
        if ($element -isnot [System.Xml.XmlElement]) { continue }
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($element.fill))
        try {
            $shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
            try {
                switch ($element.LocalName) {
                    'rect' {
                        [single]$x = $element.x; [single]$y = $element.y
                        [single]$width = $element.width; [single]$height = $element.height
                        if ($element.rx) {
                            [single]$diameter = [single]$element.rx * 2
                            $shape.AddArc($x, $y, $diameter, $diameter, 180, 90)
                            $shape.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
                            $shape.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
                            $shape.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
                            $shape.CloseFigure()
                        } else { $shape.AddRectangle([System.Drawing.RectangleF]::new($x, $y, $width, $height)) }
                    }
                    'polygon' {
                        [System.Drawing.PointF[]]$points = foreach ($pair in ($element.points.Trim() -split '\s+')) {
                            $coordinates = $pair -split ','
                            [System.Drawing.PointF]::new([single]$coordinates[0], [single]$coordinates[1])
                        }
                        $shape.AddPolygon($points)
                    }
                    default { throw ('Unsupported icon element: ' + $element.LocalName) }
                }
                $graphics.FillPath($brush, $shape)
            } finally { $shape.Dispose() }
        } finally { $brush.Dispose() }
    }
    $graphics.Dispose()
    $bitmap = $canvas
    if (-not $pixelAligned) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size)
        $resizer = [System.Drawing.Graphics]::FromImage($bitmap)
        $resizer.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $resizer.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $resizer.DrawImage($canvas, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
        $resizer.Dispose(); $canvas.Dispose()
    }
    $buffer = [System.IO.MemoryStream]::new()
    $bitmap.Save($buffer, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@($size, $buffer.ToArray())
    if ($size -eq 256) { $bitmap.Save((Join-Path $assetPath 'app.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $buffer.Dispose(); $bitmap.Dispose()
}
$stream = [System.IO.File]::Create((Join-Path $assetPath 'app.ico'))
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame[0] -eq 256) { 0 } else { $frame[0] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame[1].Length); $writer.Write([uint32]$offset)
        $offset += $frame[1].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame[1]) }
} finally { $writer.Dispose(); $stream.Dispose() }
