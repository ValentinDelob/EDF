#!/bin/sh
# tools/blender/build.sh JOURNAL <module> [familles…] [--export] [--preview DOSSIER] [--views side,three] [--res 900x420]
# Construit les modèles d'armes avec Blender en arrière-plan : aucune fenêtre (bureau invisible), priorité basse ; la sortie
# de Blender va dans JOURNAL (lignes « [modele] »). Voir tools/blender/build_weapons.py.
B="${BLENDER:-/c/Program Files/Blender Foundation/Blender 5.2/blender.exe}"
LOG="$1"; shift
cd "$(dirname "$0")/../.."
ARGS=""
for a in "$@"; do
    case "$a" in
        /*) ARGS="$ARGS $(cygpath -w "$a")" ;;
        *) ARGS="$ARGS $a" ;;
    esac
done
powershell -NoProfile -ExecutionPolicy Bypass -File tools/hidden_run.ps1 -Exe "$(cygpath -w "$B")" \
    -Args "-b --factory-startup --python tools/blender/build_weapons.py --$ARGS" -Log "$(cygpath -w "$LOG")" -TimeoutSec ${T:-3600}
grep -E "^\[modele\]|Error" "$LOG" | tail -40
