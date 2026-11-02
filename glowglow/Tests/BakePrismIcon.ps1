# Compatibility entry point: Prism now belongs to the shared authored icon atlas.
# Close the project's Unity Editor first, or use GlowGlow/Bake Weapon Icons there.
param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.0.68f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$taskProject = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
$taskLog = Join-Path $taskProject 'Logs/weapon-icons-bake.log'
New-Item -ItemType Directory -Path (Split-Path -Parent $taskLog) -Force | Out-Null
$taskProcess = Start-Process -FilePath $UnityPath -WindowStyle Hidden -PassThru -Wait -ArgumentList @(
    '-batchmode', '-projectPath', ('"' + $taskProject + '"'),
    '-executeMethod', 'WeaponIconAuthoring.BakeBatch', '-logFile', ('"' + $taskLog + '"'))
if ($taskProcess.ExitCode -ne 0) { throw "Icon bake failed. See $taskLog" }
Write-Output "Shared weapon icons baked. Log: $taskLog"