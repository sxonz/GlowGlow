param(
    [string]$PlayerPath = "$PSScriptRoot/../Temp/OnlineValidation/Builds/ClassicOnline/GlowGlow.exe",
    [int[]]$Latency = @(0, 50, 100),
    [switch]$RenderWindow
)
$ErrorActionPreference = 'Stop'
$onlinePlayerPath = (Resolve-Path -LiteralPath $PlayerPath).Path
$onlineRoot = (Resolve-Path -LiteralPath "$PSScriptRoot/..").Path
$onlineResults = Join-Path $onlineRoot 'Logs/ClassicOnlineSmoke'
New-Item -ItemType Directory -Path $onlineResults -Force | Out-Null
$allPassed = $true
foreach ($delay in $Latency) {
    $runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $hostResult = Join-Path $onlineResults "$runId-$delay-host.txt"
    $clientResult = Join-Path $onlineResults "$runId-$delay-client.txt"
    $hostArgs = @('-batchmode', '-screen-width', '1280', '-screen-height', '720', '-classic-host', '-classic-smoke',
        '-online-result', ('"' + $hostResult + '"'), '-logFile', ('"' + $hostResult + '.log"'))
    $clientArgs = @('-batchmode', '-screen-width', '1280', '-screen-height', '720', '-classic-client', '-classic-smoke',
        '-online-result', ('"' + $clientResult + '"'), '-logFile', ('"' + $clientResult + '.log"'))
    if ($delay -gt 0) { $hostArgs += @('-online-latency', $delay); $clientArgs += @('-online-latency', $delay) }
    if ($RenderWindow) {
        $hostArgs = @($hostArgs | Where-Object { $_ -ne '-batchmode' })
        $clientArgs = @($clientArgs | Where-Object { $_ -ne '-batchmode' })
    }
    $hostProcess = $null; $clientProcess = $null
    try {
        Write-Output "Starting host/client test: configured delay $delay ms."
        $hostProcess = Start-Process -FilePath $onlinePlayerPath -ArgumentList $hostArgs -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
        $clientProcess = Start-Process -FilePath $onlinePlayerPath -ArgumentList $clientArgs -WindowStyle Hidden -PassThru
        $classicDeadline = [DateTime]::UtcNow.AddSeconds(100)
        while (!$hostProcess.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $classicDeadline) { throw 'Host process timed out.' } }
        if (!$clientProcess.WaitForExit(10000)) { throw 'Client process timed out.' }
        $hostText = Get-Content -LiteralPath $hostResult -Raw
        $clientText = Get-Content -LiteralPath $clientResult -Raw
        Write-Output "Host: $hostText"
        Write-Output "Client: $clientText"
        foreach ($classicLog in @(($hostResult + '.log'), ($clientResult + '.log'))) {
            $classicErrors = Select-String -LiteralPath $classicLog -Pattern 'Exception:|error CS|CLASSIC_SMOKE FAIL|Packet.*exceeds|exceeds.*MTU'
            if ($classicErrors) { Write-Output ($classicErrors | Select-Object -First 4); $allPassed = $false }
        }
        $hostWinner = [regex]::Match($hostText, 'winnerSlot=(-?\d+)').Groups[1].Value
        $clientWinner = [regex]::Match($clientText, 'winnerSlot=(-?\d+)').Groups[1].Value
        if (!$hostText.StartsWith('PASS') -or !$clientText.StartsWith('PASS') -or $hostWinner -ne $clientWinner) {
            $allPassed = $false
        }
    }
    catch { Write-Output $_; $allPassed = $false }
    finally {
        foreach ($ownedProcess in @($hostProcess, $clientProcess)) {
            if ($null -ne $ownedProcess -and !$ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id -Force }
        }
    }
    Start-Sleep -Seconds 2
}
if (!$allPassed) { throw "Online smoke validation failed. See $onlineResults" }
Write-Output 'ONLINE_TWO_PROCESS_CHECKS_PASSED'

