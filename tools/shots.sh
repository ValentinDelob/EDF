#!/bin/sh
# Lance le jeu avec des arguments de test et enregistre les captures dans build/shots.
# Exemple : tools/shots.sh --menu-shots --quit-after=18
cd "$(dirname "$0")/.."
mkdir -p build/shots
rm -f build/shots/*.png
GODOT="${GODOT_MONO:-C:/Users/ValentinDelobel/AppData/Local/Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
export PATH="/c/Program Files/dotnet:$PATH"
timeout 150 "$GODOT" --path . -- --shots="$(pwd -W)/build/shots" "$@" 2>&1 | grep -iE "error|exception" | grep -v "^\s*at " | sort | uniq -c | head -20
ls build/shots
