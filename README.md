# Rempart

**Rempart** est un TPS coopératif de défense terrestre, inspiré de la série *Earth Defense Force* : des hordes d'insectes géants, des armes absurdes, des bâtiments qui s'effondrent et jusqu'à 4 joueurs pour tenir la ligne.

Le projet est actuellement un **POC** jouable :

- **6 missions** sur 2 cartes (ville côtière réaliste et zone portuaire), de jour, au crépuscule et de nuit, avec pluie et orage ;
- **4 classes** (Fusilier, Commando aéroporté, Opérateur d'appui, Grenadier blindé), **32 armes** à statistiques (niveau, rareté, étoiles) ;
- **9 types d'ennemis** (fourmis noires, rouges et acides, guêpes, charognards, araignées, drones, nids, reine) et variantes élites ;
- **butin** : caisses d'armes, d'armure, de soin et de crédits ; arsenal, atelier, recyclage ; niveaux de classe ;
- **char de combat** pilotable et **escouades alliées** ;
- coop en **LAN de 1 à 4 joueurs** (hôte + clients, découverte automatique des parties) ;
- musique et sons procéduraux, menus et HUD modernes, remappage complet clavier/souris, manette ;
- Windows x64 en cible principale.

Voir aussi [CHANGELOG.md](CHANGELOG.md) et [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

Moteur : **Godot 4.7.2 .NET (C#)**.

## Prérequis

- [Godot 4.7.2 .NET](https://godotengine.org/download) (version *mono*/.NET, pas la version standard) + les **export templates .NET 4.7.2** (Éditeur > Gérer les modèles d'export) ;
- [.NET SDK 8](https://dotnet.microsoft.com/download) ou plus récent ;
- PowerShell (Windows PowerShell 5.1 ou PowerShell 7) pour les scripts de `tools/`.

## Développement

Ouvrir `project.godot` dans Godot .NET 4.7.2, puis lancer la scène principale (F5).

## Build local

```powershell
./tools/build.ps1          # export release -> build/windows/Rempart.exe
./tools/build.ps1 -Debug   # export debug (+ Rempart.console.exe avec console)
./tools/run.ps1            # lance build/windows/Rempart.exe (arguments transmis au jeu)
```

Le script importe les ressources, compile la solution C# puis exporte le preset **Windows Desktop** (`export_presets.cfg`) en mode headless. Il cherche Godot via la variable d'environnement `GODOT_MONO` (chemin vers `Godot_v4.7.2-stable_mono_win64_console.exe`), sinon dans le dossier d'installation WinGet par défaut.

Le PCK et les assemblies .NET sont embarqués dans l'exécutable : `build/windows/` contient un exe autonome à distribuer.

## Jouer en LAN

| Usage                  | Port        |
|------------------------|-------------|
| Jeu (hôte)             | **UDP 27015** |
| Découverte des parties | **UDP 27016** (broadcast) |

1. Un joueur **héberge** une partie depuis le menu principal.
2. Les autres joueurs du même réseau local la voient apparaître dans la liste et la **rejoignent** (ou saisissent l'IP de l'hôte).
3. Au premier lancement, autoriser Rempart dans le **pare-feu Windows** (réseau privé), sinon l'hôte ne sera ni visible ni joignable.

## Contrôles par défaut

Les déplacements sont liés aux **touches physiques** : `W A S D` sur un clavier QWERTY correspondent à `Z Q S D` sur un clavier AZERTY.

| Action                | Touche                         |
|-----------------------|--------------------------------|
| Se déplacer           | ZQSD (AZERTY) / WASD (QWERTY)  |
| Saut / vol (jetpack)  | Espace                         |
| Sprint                | Maj                            |
| Roulade               | Ctrl                           |
| Tirer                 | Clic gauche                    |
| Viser                 | Clic droit                     |
| Recharger             | R                              |
| Choisir une arme      | 1 / 2 / 3                      |
| Arme suivante         | Q (touche physique, A en AZERTY) |
| Capacité              | F                              |
| Équipement            | G                              |
| Interagir             | E                              |
| Tableau des scores    | Tab                            |
| Pause                 | Échap                          |
| Capture d'écran       | F12                            |

## Intégration continue et releases

- **`.github/workflows/build.yml`** : à chaque push sur `main` et sur chaque pull request, export Windows sur `windows-latest` ; le zip est disponible en artefact `Rempart-windows-<sha>`.
- **`.github/workflows/release.yml`** : à chaque tag `v*` (ou lancement manuel avec une version), build puis création d'une **GitHub Release** avec `Rempart-<tag>-windows.zip` et notes générées automatiquement.
- Les deux workflows partagent l'action composite `.github/actions/godot-build` (Godot et templates mis en cache).

Publier une version :

```powershell
./tools/release.ps1 -Version 0.1.2
```

Le script vérifie que l'on est sur `main` avec un working tree propre, met à jour `config/version` dans `project.godot` (et la version de fichier dans `export_presets.cfg`), commite `Release v0.1.2`, crée le tag `v0.1.2` et pousse `main` + le tag. Le workflow *Release* fait le reste. Option `-NoPush` pour tout préparer localement sans pousser.

## Structure du projet

```
.
├── project.godot          # configuration du projet Godot
├── Rempart.csproj         # projet C# (Godot.NET.Sdk 4.7.2, net8.0)
├── export_presets.cfg     # preset d'export "Windows Desktop"
├── scenes/                # scènes Godot (.tscn)
├── scripts/               # code C#
├── tools/                 # build.ps1, run.ps1, release.ps1
├── .github/
│   ├── actions/godot-build/   # action composite partagée
│   └── workflows/             # build.yml, release.yml
└── build/                 # sorties d'export (ignoré par git)
```
