$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Xcmd.Desktop/Assets'
[xml]$drawing = Get-Content -Raw -LiteralPath (Join-Path $assetPath 'app.svg')
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    foreach ($rect in $drawing.svg.rect) {
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($rect.fill))
        [single]$x = $rect.x; [single]$y = $rect.y; [single]$width = $rect.width; [single]$height = $rect.height
        if ($rect.rx) {
            [single]$diameter = [single]$rect.rx * 2
            $shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
            $shape.AddArc($x, $y, $diameter, $diameter, 180, 90)
            $shape.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
            $shape.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
            $shape.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
            $shape.CloseFigure(); $graphics.FillPath($brush, $shape); $shape.Dispose()
        } else { $graphics.FillRectangle($brush, $x, $y, $width, $height) }
        $brush.Dispose()
    }
    $buffer = [System.IO.MemoryStream]::new()
    $bitmap.Save($buffer, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@($size, $buffer.ToArray())
    if ($size -eq 256) { $bitmap.Save((Join-Path $assetPath 'app.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $buffer.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
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
