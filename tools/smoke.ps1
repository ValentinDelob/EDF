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

# Le jeu lancé depuis l'éditeur utilise l'assemblage Debug : on le compile d'abord.
dotnet build (Join-Path $RepoRoot "Rempart.csproj") -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Compilation C# en échec."; exit 1 }

# Importe les ressources nouvelles ou modifiées (polices, textures) avant de lancer le jeu.
& $Godot --headless --path $RepoRoot --import *> $null

# Lance Godot avec un délai maximal (un jeu bloqué ne doit jamais figer la CI).
function Invoke-Timed([string[]]$allArgs, [int]$timeoutSec) {
    $log = [System.IO.Path]::GetTempFileName()
    $p = Start-Process -FilePath $Godot -ArgumentList $allArgs -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru -NoNewWindow
    if (-not $p.WaitForExit($timeoutSec * 1000)) {
        $p.Kill()
        return @("ERROR: délai dépassé ($timeoutSec s), processus arrêté") + (Get-Content $log, "$log.err" -ErrorAction SilentlyContinue)
    }
    return Get-Content $log, "$log.err" -ErrorAction SilentlyContinue
}

function Run-Godot([string]$label, [string[]]$gameArgs) {
    $all = @("--headless", "--path", ".", "--") + $gameArgs
    $out = Invoke-Timed $all ($Seconds + 60)
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
if (-not (Run-Godot "hélicoptère" @("--autostart=0", "--bot", "--heli", "--quit-after=$($Seconds + 8)"))) { $failures += "hélicoptère" }
if (-not (Run-Godot "fin de mission + bilan" @("--autostart=0", "--end-after=4", "--quit-after=$($Seconds + 2)"))) { $failures += "bilan" }

# Objectifs Défense / Escorte / Survie : l'issue attendue doit apparaître dans le journal (« [mission] victoire » / « échec »).
function Run-Expect([string]$label, [string[]]$gameArgs, [string]$expect, [int]$timeoutSec) {
    $out = Invoke-Timed (@("--headless", "--path", ".", "--") + $gameArgs) $timeoutSec
    $bad = $out | Where-Object { $_ -match $pattern }
    if ($bad -or -not ($out | Where-Object { $_ -match $expect })) {
        Write-Host "ECHEC  $label" -ForegroundColor Red
        if ($bad) { $bad | Select-Object -First 12 | ForEach-Object { Write-Host "   $_" } }
        else { Write-Host "   issue attendue absente : $expect" }
        return $false
    }
    Write-Host "OK     $label" -ForegroundColor Green
    return $true
}
$missionIndex = @{}
$i = 0
foreach ($m in (Select-String -Path "scripts/Data/Missions.cs" -Pattern '^\s+MissionGoal\.(\w+),' )) { $missionIndex[$m.Matches[0].Groups[1].Value] = $i; $i++ }
if ($missionIndex.ContainsKey("Survival")) {
    if (-not (Run-Expect "survie : victoire + extraction" @("--autostart=$($missionIndex.Survival)", "--bot", "--god", "--survive-time=8", "--quit-after=60") '\[mission\] victoire' 90)) { $failures += "survie" }
}
if ($missionIndex.ContainsKey("Defense")) {
    if (-not (Run-Expect "défense : structure détruite = échec" @("--autostart=$($missionIndex.Defense)", "--bot", "--god", "--holdfire", "--obj-hp=0.01", "--quit-after=70") '\[mission\] \S*chec' 100)) { $failures += "défense" }
}
if ($missionIndex.ContainsKey("Escort")) {
    if (-not (Run-Expect "escorte : convoi arrivé" @("--autostart=$($missionIndex.Escort)", "--bot", "--god", "--convoy-speed=6", "--obj-hp=50", "--quit-after=110") '\[mission\] victoire' 140)) { $failures += "escorte" }
}

# Classes : bascule des deux jeux d'armes du Grenadier, appel de véhicule de l'Opérateur (largage, remplacement, montée à bord).
if (-not (Run-Expect "grenadier : deux jeux d'armes" @("--autostart=0", "--class=3", "--bot", "--god", "--heavy-sets", "--quit-after=35") '\[jeux\] ok' 90)) { $failures += "jeux d'armes" }
if (-not (Run-Expect "opérateur : appel de véhicule" @("--autostart=0", "--class=2", "--bot", "--god", "--call-vehicle=tank", "--quit-after=45") '\[appel\] ok' 100)) { $failures += "appel de véhicule" }

if (-not $SkipLan) {
    # Partie LAN locale : hôte en arrière-plan, client au premier plan.
    $hostLog = Join-Path $env:TEMP "rempart_smoke_host.log"
    $hostArgs = @("--headless", "--path", ".", "--", "--autohost=2", "--bot", "--name=Hote", "--quit-after=$($Seconds + 12)")
    $proc = Start-Process -FilePath $Godot -ArgumentList $hostArgs -RedirectStandardOutput $hostLog -RedirectStandardError "$hostLog.err" -PassThru -NoNewWindow
    Start-Sleep -Seconds 3
    $clientOk = Run-Godot "LAN client" @("--autojoin=127.0.0.1", "--bot", "--name=Client", "--quit-after=$($Seconds + 8)")
    if (-not $proc.WaitForExit(90000)) { $proc.Kill(); Add-Content $hostLog "ERROR: délai dépassé, hôte arrêté" }
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


