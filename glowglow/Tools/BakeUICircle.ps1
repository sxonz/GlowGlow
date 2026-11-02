# Offline UI artwork. The game only loads the resulting imported sprite.
Add-Type -AssemblyName System.Drawing
$size = 256
$bitmap = [System.Drawing.Bitmap]::new($size, $size)
for ($y = 0; $y -lt $size; $y++) {
    for ($x = 0; $x -lt $size; $x++) {
        $dx = $x + 0.5 - $size / 2
        $dy = $y + 0.5 - $size / 2
        $coverage = [Math]::Min(1, [Math]::Max(0, $size / 2 - [Math]::Sqrt($dx*$dx + $dy*$dy)))
        $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int][Math]::Round(255*$coverage),255,255,255))
    }
}
$bitmap.Save((Join-Path $PSScriptRoot '../Assets/Resources/GameplaySprites/UICircle.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
