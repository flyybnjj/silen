param([string]$PreviewPath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskBrandDirectory = Join-Path $taskRoot 'src\Silen.App\Assets'
[System.IO.Directory]::CreateDirectory($taskBrandDirectory) | Out-Null
# Render the SVG's filled Bezier paths directly at each native size. Shrinking
# the translucent photographic mascot loses its folds at title-bar resolution.
[xml]$taskSvg = Get-Content -LiteralPath (Join-Path $taskRoot 'assets\brand\silen-bloom-icon.svg') -Raw
$taskArtwork = [System.Windows.Media.DrawingGroup]::new()
$taskTransform = [string]$taskSvg.svg.g.transform
if ($taskTransform -notmatch '^rotate\(([-\d.]+) ([-\d.]+) ([-\d.]+)\)$') { throw 'Unsupported icon transform.' }
$taskCulture = [System.Globalization.CultureInfo]::InvariantCulture
$taskArtwork.Transform = [System.Windows.Media.RotateTransform]::new(
    [double]::Parse($Matches[1], $taskCulture), [double]::Parse($Matches[2], $taskCulture), [double]::Parse($Matches[3], $taskCulture))
foreach ($taskPath in $taskSvg.svg.g.path) {
    $taskGeometry = [System.Windows.Media.Geometry]::Parse([string]$taskPath.d)
    $taskBrush = [System.Windows.Media.BrushConverter]::new().ConvertFromString([string]$taskPath.fill)
    $taskArtwork.Children.Add([System.Windows.Media.GeometryDrawing]::new($taskBrush, $null, $taskGeometry))
}
$taskArtwork.Freeze()
$taskBounds = $taskArtwork.Bounds
$taskImages = @()
foreach ($taskSize in @(16,20,24,28,32,40,48,56,64,96,128,256)) {
    $taskVisual = [System.Windows.Media.DrawingVisual]::new()
    $taskDrawing = $taskVisual.RenderOpen()
    try {
        $taskInset = if ($taskSize -le 32) { .35 } else { $taskSize * .015 }
        $taskScale = ($taskSize - 2 * $taskInset) / [Math]::Max($taskBounds.Width, $taskBounds.Height)
        $taskOffsetX = ($taskSize - $taskBounds.Width * $taskScale) / 2 - $taskBounds.X * $taskScale
        $taskOffsetY = ($taskSize - $taskBounds.Height * $taskScale) / 2 - $taskBounds.Y * $taskScale
        $taskMatrix = [System.Windows.Media.Matrix]::new($taskScale, 0, 0, $taskScale, $taskOffsetX, $taskOffsetY)
        $taskDrawing.PushTransform([System.Windows.Media.MatrixTransform]::new($taskMatrix))
        $taskDrawing.DrawDrawing($taskArtwork)
        $taskDrawing.Pop()
    } finally { $taskDrawing.Close() }
    $taskBitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($taskSize, $taskSize, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $taskBitmap.Render($taskVisual)
    $taskEncoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $taskEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($taskBitmap))
    $taskMemory = [System.IO.MemoryStream]::new()
    try {
        $taskEncoder.Save($taskMemory)
        $taskBytes = $taskMemory.ToArray()
        $taskImages += [pscustomobject]@{Size=$taskSize;Bytes=$taskBytes}
        if ($PreviewPath -and $taskSize -eq 256) { [System.IO.File]::WriteAllBytes($PreviewPath, $taskBytes) }
    } finally { $taskMemory.Dispose() }
}
$taskIconFile = [System.IO.File]::Create((Join-Path $taskBrandDirectory 'Silen.ico'))
$taskWriter = [System.IO.BinaryWriter]::new($taskIconFile)
try {
    $taskWriter.Write([uint16]0); $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]$taskImages.Count)
    $taskOffset = 6 + 16 * $taskImages.Count
    foreach ($taskImage in $taskImages) {
        $taskDimension = if ($taskImage.Size -eq 256) {0} else {$taskImage.Size}
        $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([uint16]0)
        $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]32)
        $taskWriter.Write([uint32]$taskImage.Bytes.Length); $taskWriter.Write([uint32]$taskOffset)
        $taskOffset += $taskImage.Bytes.Length
    }
    foreach ($taskImage in $taskImages) { $taskWriter.Write([byte[]]$taskImage.Bytes) }
} finally { $taskWriter.Dispose(); $taskIconFile.Dispose() }
Write-Output 'Silen.ico vectorial y transparente: 16, 20, 24, 28, 32, 40, 48, 56, 64, 96, 128 y 256 px.'
