#!/bin/sh
# tools/blender/wingdiver/run.sh JOURNAL [arguments de build_char.py] : Blender en arrière-plan (bureau invisible), MPFB isolé.
B="${BLENDER:-/c/Program Files/Blender Foundation/Blender 5.2/blender.exe}"
LOG="$1"; shift
cd "$(dirname "$0")/../../.."
export BLENDER_USER_RESOURCES="$(cygpath -w "$PWD/.mpfb/res")"
ARGS=""
for a in "$@"; do
    case "$a" in
        /*) ARGS="$ARGS $(cygpath -w "$a")" ;;
        *) ARGS="$ARGS $a" ;;
    esac
done
powershell -NoProfile -ExecutionPolicy Bypass -File tools/hidden_run.ps1 -Exe "$(cygpath -w "$B")" \
    -Args "-b --python tools/blender/wingdiver/build_char.py --$ARGS" -Log "$(cygpath -w "$LOG")" -TimeoutSec ${T:-3600}
grep -E "^\[perso\]|Error|Traceback" "$LOG" | tail -30
