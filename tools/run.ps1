<#
.SYNOPSIS
    Lance le build Windows de Rempart (build/windows/Rempart.exe). Les arguments sont transmis au jeu.

.EXAMPLE
    ./tools/run.ps1
    ./tools/run.ps1 --windowed --resolution 1280x720
#>
$ErrorActionPreference = "Stop"

$Exe = Join-Path $PSScriptRoot "../build/windows/Rempart.exe"
if (-not (Test-Path $Exe)) {
    Write-Error "Executable introuvable: $Exe. Lancez d'abord ./tools/build.ps1"
    exit 1
}

$Exe = (Resolve-Path $Exe).Path
& $Exe @args
exit $LASTEXITCODE
