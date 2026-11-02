param(
    [string]$CaptureDirectory = 'Builds/ConstellationValidation/ValidationOutput',
    [string]$OutputPath = 'Tests/ui-theme-preview.png'
)

# Compose actual Unity captures for review; this does not generate gameplay assets.
Add-Type -AssemblyName System.Drawing
$items = @(
    @('01 / SETTINGS', 'ui-settings.png'),
    @('02 / BARRAGE COLLECTION', 'ui-deck-2.png'),
    @('03 / ONLINE LOBBY', 'ui-online.png'),
    @('04 / OPENING DRAFT', 'ui-draft.png'),
    @('05 / TRAINING CONSOLE', 'ui-training.png'),
    @('06 / MATCH RESULT', 'ui-result.png')
)
$sheet = [System.Drawing.Bitmap]::new(1440, 1388)
$graphics = [System.Drawing.Graphics]::FromImage($sheet)
$titleFont = [System.Drawing.Font]::new('Segoe UI', 24, [System.Drawing.FontStyle]::Bold)
$labelFont = [System.Drawing.Font]::new('Segoe UI', 12, [System.Drawing.FontStyle]::Bold)
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(239, 243, 255))
$cyan = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(87, 222, 255))
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(7, 9, 19))
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.DrawString('GLOWGLOW / UI SYSTEM', $titleFont, $white, 20, 12)
    for ($i = 0; $i -lt $items.Count; $i++) {
        $x = 16 + ($i % 2) * 712
        $y = 70 + [Math]::Floor($i / 2) * 438
        $graphics.DrawString($items[$i][0], $labelFont, $cyan, $x, $y)
        $capturePath = [IO.Path]::GetFullPath((Join-Path $CaptureDirectory $items[$i][1]))
        $capture = [System.Drawing.Image]::FromFile($capturePath)
        try { $graphics.DrawImage($capture, [System.Drawing.Rectangle]::new($x, ($y + 28), 696, 392)) }
        finally { $capture.Dispose() }
    }
    $sheet.Save([IO.Path]::GetFullPath($OutputPath), [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose(); $sheet.Dispose(); $titleFont.Dispose(); $labelFont.Dispose(); $white.Dispose(); $cyan.Dispose()
}
Write-Output ([IO.Path]::GetFullPath($OutputPath))
