<#
.SYNOPSIS
    Tests de non-régression : lance chaque mission sans affichage (bot + ennemis supplémentaires),
    puis une partie LAN hôte + client, et échoue si une erreur ou une exception apparaît.
.PARAMETER Seconds
    Durée de chaque mission (défaut 10 s).
#>
param(
    [int]$Seconds = 10,
    [switch]$SkipLan
)

# Godot écrit ses avertissements sur stderr : ne pas les traiter comme des erreurs PowerShell.
$ErrorActionPreference = "Continue"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot
$dotnetDir = "C:\Program Files\dotnet"
if ((Test-Path $dotnetDir) -and -not (($env:PATH -split ';') -contains $dotnetDir)) { $env:PATH = "$dotnetDir;$env:PATH" }
$DefaultGodot = Join-Path $env:LOCALAPPDATA "Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
$Godot = if ($env:GODOT_MONO) { $env:GODOT_MONO } else { $DefaultGodot }

# Nombre de missions lu dans les données (entrées « new(N, " de Missions.cs).
$missionCount = (Select-String -Path "scripts/Data/Missions.cs" -Pattern '^\s+new\(\d+, "' ).Count
$pattern = 'ERROR|Exception|Unhandled'
$failures = @()

function Run-Godot([string]$label, [string[]]$gameArgs) {
    $all = @("--headless", "--path", ".", "--") + $gameArgs
    $out = & $Godot @all 2>&1 | ForEach-Object { $_.ToString() }
    $bad = $out | Where-Object { $_ -match $pattern }
    if ($bad) {
        Write-Host "ECHEC  $label" -ForegroundColor Red
        $bad | Select-Object -First 12 | ForEach-Object { Write-Host "   $_" }
        return $false
    }
    Write-Host "OK     $label" -ForegroundColor Green
    return $true
}

for ($m = 0; $m -lt $missionCount; $m++) {
    foreach ($cls in @(($m % 4))) {
        if (-not (Run-Godot "mission $m (classe $cls)" @("--autostart=$m", "--class=$cls", "--bot", "--stress=25", "--quit-after=$Seconds"))) { $failures += "mission $m" }
    }
}
if (-not (Run-Godot "char de combat" @("--autostart=0", "--bot", "--tank", "--quit-after=$Seconds"))) { $failures += "char" }
if (-not (Run-Godot "fin de mission + bilan" @("--autostart=0", "--end-after=4", "--quit-after=$($Seconds + 2)"))) { $failures += "bilan" }

if (-not $SkipLan) {
    # Partie LAN locale : hôte en arrière-plan, client au premier plan.
    $hostLog = Join-Path $env:TEMP "rempart_smoke_host.log"
    $hostArgs = @("--headless", "--path", ".", "--", "--autohost=2", "--bot", "--name=Hote", "--quit-after=$($Seconds + 12)")
    $proc = Start-Process -FilePath $Godot -ArgumentList $hostArgs -RedirectStandardOutput $hostLog -RedirectStandardError "$hostLog.err" -PassThru -NoNewWindow
    Start-Sleep -Seconds 3
    $clientOk = Run-Godot "LAN client" @("--autojoin=127.0.0.1", "--bot", "--name=Client", "--quit-after=$($Seconds + 8)")
    $proc.WaitForExit(60000) | Out-Null
    $hostBad = Get-Content $hostLog, "$hostLog.err" -ErrorAction SilentlyContinue | Where-Object { $_ -match $pattern }
    if ($hostBad) { Write-Host "ECHEC  LAN hôte" -ForegroundColor Red; $hostBad | Select-Object -First 12 | ForEach-Object { Write-Host "   $_" }; $failures += "LAN hôte" }
    else { Write-Host "OK     LAN hôte" -ForegroundColor Green }
    if (-not $clientOk) { $failures += "LAN client" }
}

if ($failures.Count -gt 0) {
    Write-Error "Tests de non-régression en échec : $($failures -join ', ')"
    exit 1
}
Write-Host "Tous les tests de non-régression sont passés." -ForegroundColor Green
exit 0

