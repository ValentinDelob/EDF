<#
.SYNOPSIS
    Build et export Windows de Rempart via Godot .NET en mode headless.

.DESCRIPTION
    1. Import des ressources (--headless --import)
    2. Compilation de la solution C# (--headless --build-solutions --quit)
    3. Export du preset "Windows Desktop" (--export-release, ou --export-debug avec -Debug)

    Godot est localise via la variable d'environnement GODOT_MONO, sinon le chemin WinGet par defaut.

.EXAMPLE
    ./tools/build.ps1
    ./tools/build.ps1 -Debug
#>
# NB: pas de [CmdletBinding()] volontairement, sinon -Debug entrerait en conflit avec le parametre commun.
param(
    [switch]$Debug,
    [string]$Preset = "Windows Desktop",
    [string]$Output = "build/windows/Rempart.exe",
    [switch]$SkipImport
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

# --- .NET sur le PATH ---------------------------------------------------------
$dotnetDir = "C:\Program Files\dotnet"
if ((Test-Path $dotnetDir) -and -not (($env:PATH -split ';') -contains $dotnetDir)) {
    $env:PATH = "$dotnetDir;$env:PATH"
}

# --- Localisation de Godot ----------------------------------------------------
$DefaultGodot = Join-Path $env:LOCALAPPDATA "Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
$Godot = if ($env:GODOT_MONO) { $env:GODOT_MONO } else { $DefaultGodot }
if (-not (Test-Path $Godot)) {
    Write-Error "Godot .NET introuvable: '$Godot'. Definissez GODOT_MONO vers Godot_v4.7.2-stable_mono_win64_console.exe."
    exit 1
}
Write-Host "Godot : $Godot"

function Invoke-Godot {
    param([string]$Step, [string[]]$GodotArgs)
    Write-Host ""
    Write-Host "==> $Step" -ForegroundColor Cyan
    Write-Host "    $Godot $($GodotArgs -join ' ')"
    $log = & $Godot @GodotArgs 2>&1 | ForEach-Object { $_.ToString() }
    $log | Where-Object { $_ -notmatch 'savepack|reimport|Stockage' } | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Echec de l'etape '$Step' (code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
    # Godot peut terminer avec le code 0 malgre une erreur de compilation ou d'export .NET.
    $fatal = $log | Where-Object { $_ -match 'ERROR: Export|error CS\d{4}|no solution file|Build failed' }
    if ($fatal) {
        $fatal | Select-Object -First 10 | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Write-Error "Echec de l'etape '$Step' : erreurs detectees dans la sortie de Godot."
        exit 1
    }
}

# --- Dossier de sortie ----------------------------------------------------------
$OutputFull = Join-Path $RepoRoot $Output
$OutputDir = Split-Path $OutputFull -Parent
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
if (Test-Path $OutputFull) { Remove-Item -Force $OutputFull }

# --- Etapes ---------------------------------------------------------------------
if (-not $SkipImport) {
    Invoke-Godot "Import des ressources" @("--headless", "--path", ".", "--import")
}
Invoke-Godot "Compilation C#" @("--headless", "--path", ".", "--build-solutions", "--quit")

$exportFlag = if ($Debug) { "--export-debug" } else { "--export-release" }
Invoke-Godot "Export '$Preset' ($exportFlag)" @("--headless", "--path", ".", $exportFlag, $Preset, $Output)

if (-not (Test-Path $OutputFull)) {
    Write-Error "Export termine mais executable absent: $OutputFull"
    exit 1
}

$item = Get-Item $OutputFull
$sizeMb = [math]::Round($item.Length / 1MB, 2)
Write-Host ""
Write-Host "Build OK : $($item.FullName) ($sizeMb Mo)" -ForegroundColor Green
exit 0
