# Architecture de Rempart

Rempart est un projet Godot 4.7 .NET entièrement écrit en C#. La quasi-totalité du contenu (ville, modèles, matériaux, sons, musique, interface) est générée par le code : la seule scène est `scenes/Main.tscn`, qui instancie `App`.

## Vue d'ensemble

| Dossier | Rôle |
| --- | --- |
| `scripts/App.cs` | Racine : navigation entre écrans, lancement des missions, options de test (`AutoTest`). |
| `scripts/Core/` | Autoloads `Settings` (touches, souris, vidéo, audio) et `Profile` (armure, armes, niveaux, sauvegarde JSON). |
| `scripts/Data/` | Données de jeu : classes, armes, ennemis, difficultés, missions et vagues. |
| `scripts/Net/Net.cs` | Autoload réseau : salon LAN (ENet), découverte UDP, toutes les RPC de jeu. |
| `scripts/World/` | Mission en cours : `World`, génération de la ville, champ de flux, directeur de mission, caisses, alliés, bilan. |
| `scripts/Player/` | Soldat local, coéquipiers distants, modèle procédural, projectiles, char. |
| `scripts/Enemies/` | Ennemis (IA hôte, interpolation client) et modèles d'insectes et de drones. |
| `scripts/Fx/` | Effets visuels, sons et musique synthétisés, météo. |
| `scripts/UI/` | Kit d'interface (`UiKit`, `MenuScreen`), menus, HUD, bilan. |

## Autorité réseau

- L'**hôte** (pair 1) simule les ennemis, les alliés PNJ, le déroulé de mission, l'apparition et le ramassage des caisses, et la destruction des immeubles. Il diffuse des instantanés compressés (15 Hz pour les ennemis, 10 Hz pour les alliés).
- Chaque **joueur** simule son soldat (et le char qu'il conduit) et envoie son état à 20 Hz. Ses tirs sont calculés localement ; les dégâts sont envoyés à l'hôte, qui les applique.
- La ville est générée avec une graine fixe : les identifiants d'immeubles sont identiques chez tous les pairs.
- En solo, le jeu utilise `OfflineMultiplayerPeer` : le même code passe par les mêmes RPC.

## Ajouter du contenu

- **Une arme** : une ligne dans `Weapons.All` (`scripts/Data/GameData.cs`). Le comportement dépend de `WeaponKind` et de la catégorie (modèle tenu, projectile, son).
- **Un ennemi** : une valeur de `EnemyType` (toujours à la fin, l'entier circule sur le réseau), une ligne dans `Enemies.All`, un modèle dans `BugModel.Create`.
- **Une mission** : une entrée dans `Missions.All` (phases, vagues, zones d'apparition, météo).

## Tests automatisés

Les arguments placés après `--` pilotent le jeu sans intervention :

| Argument | Effet |
| --- | --- |
| `--autostart=N` `--class=C` `--difficulty=D` | Lance directement la mission N en solo. |
| `--bot` | Le soldat vise et tire seul (le Commando vole). |
| `--shots=dossier --shot-times=4,10` | Captures d'écran aux instants donnés. |
| `--autohost=2` / `--autojoin=IP` | Test LAN à deux instances. |
| `--stress=N` `--novsync` `--fps` | Test de charge et journal d'images par seconde. |
| `--showcase[=big]` | Vitrine des ennemis, IA figée. |
| `--end-after=N` | Termine la mission avec du butin fictif (test du bilan). |
| `--menu-shots` | Captures de tous les menus. |

Les lancements de test utilisent un profil séparé (`profile_test.json`). Le script `tools/shots.sh` enchaîne compilation et captures dans `build/shots`.

## Build et publication

- `tools/build.ps1` : `dotnet build` puis export Godot vers `build/windows/Rempart.exe` (exécutable autonome, .NET embarqué).
- CI GitHub Actions : build à chaque push sur `main`, release avec l'archive Windows à chaque tag `v*`.
