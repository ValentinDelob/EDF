# Architecture de Rempart

Rempart est un projet Godot 4.7 .NET entièrement écrit en C#. La quasi-totalité du contenu (ville, modèles, matériaux, sons, musique, interface) est générée par le code : la seule scène est `scenes/Main.tscn`, qui instancie `App`.

## Vue d'ensemble

| Dossier | Rôle |
| --- | --- |
| `scripts/App.cs` | Racine : navigation entre écrans, lancement des missions, options de test (`AutoTest`). |
| `scripts/Core/` | Autoloads `Settings` (touches, souris, vidéo, audio) et `Profile` (armure, armes, niveaux, sauvegarde JSON versionnée). |
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

## Collisions du décor

- Tout élément solide visible a une collision à sa forme réelle ; le feuillage (matériau `Mats.Foliage`) n'est jamais solide.
- `MeshKit.Record` enregistre les triangles « solides » en même temps que le visuel (`MeshKit.Solid = false` exclut une partie : lettrages, vitres et châssis en léger relief). `MeshKit.FacesOf` / `ShapeOf` donnent la forme exacte (triangles, deux faces) d'un maillage produit.
- **Immeubles** : boîte pleine pour le volume principal (+ toit en pente convexe pour les maisons), plus une seule forme exacte « Detail » pour tout ce qui dépasse (acrotère, corniches, socle, piliers, balcons, stores, enseignes, équipements de toit, débords de toiture, cheminées). À l'effondrement, toutes les formes sont désactivées et le tas de gravats devient solide.
- **Accessoires** (`PropScatter`) : les triangles solides des modèles en cache (`Props.Cached`) sont fusionnés en une forme par cellule de 100 m (corps `PropSolids`) ; `Add(..., solid: false)` pour du décor traversable.
- **Assemblages** (garde-corps et muret de la promenade, ponton, poste de secours, places, chantiers, secteurs « Solid » de la banlieue : murets, ponts, tombes, monument) : `CityBuilder.AddSolid`. Véhicules garés : forme exacte partagée par modèle (`CityBuilder.VehicleShape`).
- Identifiants et ordre des immeubles inchangés : les formes ajoutées ne consomment aucun tirage aléatoire.

## Matières du décor

- `Mats.Weather.cs` : shader « Weathered » partagé (façades, vitrines, béton, asphalte, dallage, sol, matériaux de détail, tôles, peintures industrielles). Il reprend l'essentiel d'un `StandardMaterial3D` (albédo × couleur de sommet, relief, rugosité, émission, UV ou triplanaire) et ajoute des salissures en coordonnées monde qui ne se répètent pas d'un immeuble à l'autre : coulures verticales, taches à deux échelles, pied des murs encrassé, poussière et flaques au sol, rouille, désaturation. Réglages par paramètres (`grime`, `streaks`, `foot`, `puddles`, `rust`, `desat`...).
- Textures générées en C# (en parallèle, sans appel moteur par pixel) : façades (vitres brisées, fenêtres incendiées et panaches de suie, impacts, joints de panneaux), vitrines (rideaux de fer, contreplaqué), asphalte (fissures, réparations, huile), dallage (dalles fendues, basculées ou manquantes), herbe morte et boue, dalles de quai.
- Volumes d'immeubles aux arêtes verticales chanfreinées (acrotère, corniche et toit suivent l'octogone) ; teinte propre à chaque immeuble et bande assombrie sous l'acrotère par couleur de sommet. Aucun tirage aléatoire supplémentaire (identifiants inchangés).
- Arbres : majorité d'arbres morts (charpente ramifiée, ramilles ajourées), feuillage restant en touffes découpées (`Mats.Leaves`, alpha scissor) ; seules les charpentières sont solides.

## Insectes grimpeurs

Fourmis, araignée, mille-pattes et charognard (`Enemy.Climbs` : ni volants, ni nid, ni boss) marchent sur toute surface solide (voir `Enemy.Climb.cs`) :

- **Suivi de surface** (hôte) par rayons contre la couche monde : sonde avant à mi-hauteur (angle rentrant sol → mur, mur → plafond ; un obstacle étroit comme un poteau est contourné), sonde d'accroche vers la surface (marches, pentes), sonde de contournement sous l'arête (angle saillant mur → toit → façade opposée), sinon chute. Les sondes sont espacées selon la distance parcourue (0,6 m au sol, 0,3 m sur les parois). Le corps est orienté sur la normale lissée (pattes posées sur le mur) ; le cadavre tombe sur la surface en dessous, où apparaît le butin.
- **Direction** : sur une paroi, vouloir la traverser fait monter, vouloir s'en éloigner fait descendre. Le champ de flux a un second calcul pondéré (`FlowField.ClimbDirection`, Dijkstra) où les immeubles sont franchissables avec un coût selon leur hauteur : les insectes passent par-dessus quand c'est plus court et rejoignent les joueurs sur les toits. La portée d'attaque des grimpeurs est mesurée en 3D.
- **Réseau** : instantané ennemi de 18 octets (id, position 3 × 16 bits au 1/64 m, normale et regard en projection octaédrique sur 2 octets chacun, état, PV, attaque, vitesse) ; les clients interpolent position et orientation (quaternion).

## Véhicules

`Vehicle` (classe de base abstraite) porte l'état commun : identifiant, conducteur, blindage, épave, diffusion d'état à 20 Hz par le conducteur (`Net.SendVehicleState`), interpolation chez les autres pairs, textes du HUD et réglages de caméra. `Tank` et `Helicopter` en héritent. L'hôte attribue les places (`VehicleSeatRequest` → `VehicleDriver`). `World.Vehicles` : char 1, hélicoptère 2.

## Interface

- `UiKit` regroupe les jetons de design (couleurs, polices, marges) et les fabriques de contrôles ; `MenuScreen` est le gabarit commun (titre, onglets Q/E, barre d'aide, action principale en bas à droite).
- `Stepper` : sélecteur « ‹ valeur › » focalisable (gauche/droite changent la valeur, Entrée fait défiler).
- Focus : chaque écran donne le focus à son premier élément actionnable ; après reconstruction d'une liste, `SetFocusKey` / `RestoreFocus` (dans `MenuScreen`) rendent le focus au même élément.

## Armes et progression

- **Modèle** (`WeaponTemplate`, `Weapons.All`) : valeurs de base au niveau 0, sans étoile. La **catégorie** (`Category`) est le *type d'arme* : l'arsenal affiche une colonne par type de la classe (`Weapons.CategoriesFor`, intitulés pluriels `Weapons.CategoryTitle`).
- **Exemplaire possédé** (`WeaponInstance`) : modèle + **niveau** (0 à 100) + **étoiles par caractéristique** `Stars[WeaponStat]` (dégâts, cadence, chargeur, rechargement, précision, portée). Chaque caractéristique va de 0 (minimum : valeur du modèle au niveau de l'arme) à `WeaponInstance.MaxStars` (constante unique, 5). Chaque étoile ajoute un bonus chiffré, toujours favorable (`DamagePerStar`… : +6 % de dégâts, +4 % de cadence, +8 % de chargeur (au moins +1), −7 % de rechargement, −10 % de dispersion, +5 % de portée). `HasStat` dit quelles caractéristiques ont un sens pour un modèle (pas de cadence pour une balise, etc.) ; `Describe(stat)` renvoie la valeur et le bonus affichés (« 46 » et « +10 »).
- **Aucune rareté, aucun recyclage, aucune amélioration** : la progression vient du niveau et des étoiles tirés au butin. Les crédits restent un simple compteur (caisses jaunes, doublons).
- **Butin** (`Loot.RollWeapon`, fin de mission dans `MissionReport.Build`) : niveau selon la difficulté et l'avancement, puis pour chaque caractéristique utile `MaxStars` tirages indépendants de probabilité `Loot.StarChance(difficulté, niveau)` (≈ 0,15 en Facile au niveau 0 → ≈ 0,7 en Inferno au niveau 100). Garantie : au bout de 30 armes sans caractéristique au maximum, la suivante en reçoit une. `Profile.Grant` ajoute l'arme ; seul un doublon strict (même modèle, niveau et étoiles inférieurs ou égaux) est converti en crédits.
- **Sauvegarde** (`user://profile.json`, `ProfileData.Version` = 2) : migration automatique des profils version 1 (rareté + 5 étoiles de 1 à 5) dans `Profile.Migrate` → `WeaponInstance.MigrateLegacy` : étoile − 2 + bonus de rareté (Standard/Renforcée 0, Élite/Prototype +1, Légendaire +2), bornée à 0..5 puis ramenée à l'échelle de `MaxStars` ; la portée prend la moyenne des anciennes étoiles. Les tableaux d'étoiles de mauvaise longueur sont complétés et bornés (changer `MaxStars` ne casse pas un profil).
- **Réseau** : aucun exemplaire d'arme ne circule ; chaque joueur tire avec ses propres armes et n'envoie que l'indice de l'arme tenue (`PlayerState`) et les dégâts infligés. Le format LAN est inchangé.
- **Arsenal** (`ArsenalMenu`, maquette `docs/maquettes/project/E-Arsenal.dc.html`) : tableau du haut (une colonne par type, colonnes Lv et ☆ = somme des étoiles), armes équipées assombries avec l'étiquette « ÉQUIPÉE n » (non équipables une seconde fois), noms trop longs en bandeau défilant ; panneau du bas (description, lignes « (☆n) Nom : valeur (+bonus) ») ; soldat et carte d'équipement à droite ; aides de touches (Entrée équiper, Échap retour, Q/E emplacement visé, Tab classe). Navigation clavier/manette par le focus Godot (voisins explicites entre colonnes). Dessin autonome provisoire : `ArsenalEStyle.cs` (palette, polices Share Tech Mono / Saira Condensed, `Pen` qui dessine dans l'espace 1280 × 720 de la maquette à l'échelle de l'écran, fond animé `ArsenalEBackground`) et `ArsenalESoldier.cs` (silhouette SVG) — à remplacer par le kit commun.

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
| `--tank` / `--heli` | Monte dans le char ou l'hélicoptère au début de la mission. |
| `--stress=N` `--novsync` `--fps` | Test de charge et journal d'images par seconde (avec le nombre d'insectes sur les murs et en hauteur). |
| `--god` / `--holdfire` | Le soldat ne subit aucun dégât / le bot vise sans tirer (mesures et captures stables). |
| `--roof[=N]` | Le soldat (bot immobile) est posé sur le toit plat le plus proche du départ, N insectes (30) apparaissent autour et l'escaladent. Avec `--autohost`, test de l'escalade vue du client. |
| `--showcase[=big]` | Vitrine des ennemis, IA figée. |
| `--end-after=N` | Termine la mission avec du butin fictif (test du bilan). |
| `--menu-shots` | Captures de tous les menus. |
| `--menu-shots=arsenal` | Profil de test garni d'armes de toutes les classes, puis captures de l'arsenal (navigation, équipement, changement de classe). |

Les lancements de test utilisent un profil séparé (`profile_test.json`). Le script `tools/shots.sh` enchaîne compilation et captures dans `build/shots`.

## Build et publication

- `tools/build.ps1` : `dotnet build` puis export Godot vers `build/windows/Rempart.exe` (exécutable autonome, .NET embarqué).
- CI GitHub Actions : build à chaque push sur `main`, release avec l'archive Windows à chaque tag `v*`.
