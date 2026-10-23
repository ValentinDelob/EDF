<#
.SYNOPSIS
    Prepare et publie une release de Rempart.

.DESCRIPTION
    - Met a jour config/version dans project.godot (et file/product_version dans export_presets.cfg)
    - Commit "Release vX.Y.Z", tag annote vX.Y.Z
    - Pousse main puis le tag (le workflow GitHub "Release" construit et publie le zip Windows)

.EXAMPLE
    ./tools/release.ps1 -Version 0.1.2
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Remote = "origin",
    [string]$Branch = "main",
    [switch]$NoPush
)

$ErrorActionPreference = "Stop"

$Version = $Version.Trim().TrimStart('v')
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version invalide '$Version' (attendu: X.Y.Z)."
    exit 1
}
$Tag = "v$Version"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') a echoue (code $LASTEXITCODE)" }
}

# --- Verifications --------------------------------------------------------------
$current = (& git rev-parse --abbrev-ref HEAD).Trim()
if ($current -ne $Branch) {
    Write-Error "Branche courante '$current', attendu '$Branch'."
    exit 1
}
if (& git status --porcelain) {
    Write-Error "Le working tree n'est pas propre. Commitez ou stashez d'abord."
    exit 1
}
& git rev-parse -q --verify "refs/tags/$Tag" *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Le tag $Tag existe deja."
    exit 1
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# --- project.godot : config/version ----------------------------------------------
$projectFile = Join-Path $RepoRoot "project.godot"
$text = [IO.File]::ReadAllText($projectFile)
$pattern = '(?m)^config/version="[^"]*"'
if ($text -match $pattern) {
    $text = [regex]::Replace($text, $pattern, "config/version=`"$Version`"")
} else {
    # Ajoute la cle juste apres config/name si absente
    $text = [regex]::Replace($text, '(?m)^(config/name="[^"]*")', "`$1`nconfig/version=`"$Version`"", 1)
}
[IO.File]::WriteAllText($projectFile, $text, $utf8NoBom)

# --- export_presets.cfg : versions de fichier Windows -----------------------------
$presetsFile = Join-Path $RepoRoot "export_presets.cfg"
if (Test-Path $presetsFile) {
    $p = [IO.File]::ReadAllText($presetsFile)
    $p = [regex]::Replace($p, '(?m)^application/file_version="[^"]*"', "application/file_version=`"$Version.0`"")
    $p = [regex]::Replace($p, '(?m)^application/product_version="[^"]*"', "application/product_version=`"$Version.0`"")
    [IO.File]::WriteAllText($presetsFile, $p, $utf8NoBom)
}

# --- Commit, tag, push ----------------------------------------------------------------
Invoke-Git add project.godot
if (Test-Path $presetsFile) { Invoke-Git add export_presets.cfg }
Invoke-Git commit -m "Release $Tag"
Invoke-Git tag -a $Tag -m "Rempart $Tag"

if ($NoPush) {
    Write-Host "Commit + tag $Tag crees localement (pas de push)." -ForegroundColor Yellow
    exit 0
}

Invoke-Git push $Remote $Branch
Invoke-Git push $Remote $Tag
Write-Host "Release $Tag poussee. Le workflow GitHub 'Release' va publier Rempart-$Tag-windows.zip." -ForegroundColor Green
