# Development-time bake, matching the existing monochrome barrage icon set.
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 64,64
for ($y=0; $y -lt 64; $y++) {
    for ($x=0; $x -lt 64; $x++) {
        $coverage=0
        for ($sy=0; $sy -lt 4; $sy++) {
            for ($sx=0; $sx -lt 4; $sx++) {
                $px=$x+($sx+.5)/4-32
                $py=$y+($sy+.5)/4-32
                $r=[Math]::Sqrt($px*$px+$py*$py)
                $angle=[Math]::Atan2($py,$px)
                $disc=($px-14.14)*($px-14.14)+($py+14.14)*($py+14.14) -lt 49 -or
                      ($px+14.14)*($px+14.14)+($py-14.14)*($py-14.14) -lt 49
                $arc=[Math]::Abs($r-20) -lt 1.5 -and [Math]::Abs([Math]::Sin($angle+[Math]::PI/4)) -gt .4
                if ($disc -or $arc) { $coverage++ }
            }
        }
        $bitmap.SetPixel($x,$y,[System.Drawing.Color]::FromArgb([int](255*$coverage/16),255,255,255))
    }
}
$target=Join-Path $PSScriptRoot '../Assets/UI/BarrageIcons/PrismShot.png'
$bitmap.Save([IO.Path]::GetFullPath($target),[System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
