# Architecture de Rempart

Rempart est un projet Godot 4.7 .NET entièrement écrit en C#. La quasi-totalité du contenu (ville, modèles, matériaux, sons, musique, interface) est générée par le code : la seule scène est `scenes/Main.tscn`, qui instancie `App`.

## Vue d'ensemble

| Dossier | Rôle |
| --- | --- |
| `scripts/App.cs` | Racine : navigation entre écrans, lancement des missions, options de test (`AutoTest`). |
| `scripts/Core/` | Autoloads `Settings` (touches, souris, vidéo, audio) et `Profile` (armure, armes, missions terminées, sauvegarde JSON versionnée). |
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

## Explosions

`Effects.Explosion(world, pos, rayon, kind)` (kind 0 explosif, 2 plasma, 4 acide ; le kind 3 du réseau reste une gerbe d'étincelles) délègue à `ExplosionFx` (`scripts/Fx/Explosions.cs`), un nœud par mission créé au chargement. Il ne crée aucun nœud par explosion : quatre `GPUParticles3D` permanents (nuages, étincelles, éclairs, éclats) sont alimentés par `EmitParticle`, avec un shader de simulation commun (type de particule et durée de vie codés dans `CUSTOM.w`) ; lumières (6), distorsions de l'onde de choc (4) et traces de brûlure (28, 22 s) sont des réserves recyclées. Les nuages (boule de feu qui refroidit en suie, fumée noire, anneau de poussière, gerbes de terre, plasma, gaz acide) partagent un émetteur trié en profondeur, un éclairage simplifié (soleil et ambiance recopiés de la mission chaque seconde) et un fondu contre le décor. Le niveau de détail baisse avec la distance et la cadence (au-delà de 5 explosions/s lissées : moins de particules, fumée plus brève, ni lumière lointaine ni distorsion). Textures (bruit, brûlure) générées une fois ; tous les shaders servent au chargement (pas d'à-coup à la première explosion). Les matériaux passent en priorité de rendu haute : sinon le tri des transparents (émetteurs centrés sur la carte) ferait dessiner certaines façades par-dessus le feu.

## Véhicules

`Vehicle` (classe de base abstraite) porte l'état commun : identifiant, conducteur, blindage, épave, diffusion d'état à 20 Hz par le conducteur (`Net.SendVehicleState`), interpolation chez les autres pairs, textes du HUD et réglages de caméra. `Tank` et `Helicopter` en héritent. L'hôte attribue les places (`VehicleSeatRequest` → `VehicleDriver`). `World.Vehicles` : char 1, hélicoptère 2.

## Interface

- Direction artistique « E » (ADN EDF, `docs/maquettes/`) : `UiE` (palette verte, polices Share Tech Mono / Saira Condensed, panneaux, thème vert, `EStage` = scène 1280×720 aux coordonnées des maquettes mise à l'échelle de la fenêtre, `EFrame` = fond + bandeau de titre + bande basse avec aides clavier), `EPlate` (plaque commune à coins coupés : survol, sélection animée, variante danger, plaque d'affichage), `MenuBackdrop` (fond animé dessiné en 2D : grille, radar, circuits, particules ; figé avec l'option « Réduire les animations »), `ECard` (carte inclinée, focalisable), `SoldierPortrait` (soldat 3D de la classe choisie, fond transparent), `MissionProgress` (déblocage séquentiel des missions, vignettes `assets/missions/mission_N.jpg`), `DeployOverlay` (écran « DÉPLOIEMENT »). La ville 3D `MenuBackground` n'est plus affichée derrière les menus.
- `UiKit` regroupe les jetons historiques et les fabriques de contrôles ; `MenuScreen` est le gabarit commun (cadre E : titre dans le bandeau, onglets Q/E à droite du titre, aides clavier dans la bande basse, action principale en bas à droite).
- `Stepper` : sélecteur « ‹ valeur › » focalisable (gauche/droite changent la valeur, Entrée fait défiler).
- Focus : chaque écran donne le focus à son premier élément actionnable ; après reconstruction d'une liste, `SetFocusKey` / `RestoreFocus` (dans `MenuScreen`) rendent le focus au même élément.

## Armes et progression

- **Modèle** (`WeaponTemplate`, `Weapons.All`) : valeurs de base au niveau 0, sans étoile. La **catégorie** (`Category`) est le *type d'arme* : l'arsenal affiche une colonne par type de la classe (`Weapons.CategoriesFor`, intitulés pluriels `Weapons.CategoryTitle`).
- **Exemplaire possédé** (`WeaponInstance`) : modèle + **niveau** (0 à 100) + **étoiles par caractéristique** `Stars[WeaponStat]` (dégâts, cadence, chargeur, rechargement, précision, portée). Chaque caractéristique va de 0 (minimum : valeur du modèle au niveau de l'arme) à `WeaponInstance.MaxStars` (constante unique, 5). Chaque étoile ajoute un bonus chiffré, toujours favorable (`DamagePerStar`… : +6 % de dégâts, +4 % de cadence, +8 % de chargeur (au moins +1), −7 % de rechargement, −10 % de dispersion, +5 % de portée). `HasStat` dit quelles caractéristiques ont un sens pour un modèle (pas de cadence pour une balise, etc.) ; `Describe(stat)` renvoie la valeur et le bonus affichés (« 46 » et « +10 »).
- **Aucune rareté, aucun recyclage, aucun crédit, aucune expérience ni niveau de classe** : la progression vient de l'armure ramassée (`Profile.Armor` = armure de base de la classe + `ArmorBonus`) et du niveau et des étoiles des armes tirés au butin.
- **Un exemplaire par modèle** : l'arsenal ne contient jamais deux armes du même modèle. `Profile.Grant` ajoute une arme nouvelle (`IsNew`) ; une arme déjà possédée améliore l'exemplaire existant (`WeaponInstance.MergeFrom` : niveau le plus haut, et pour chaque caractéristique le plus grand nombre d'étoiles). Il renvoie un `LootResult` (exemplaire, arme tirée, instantanés `Before` / `After`, `IsNew`, `Improved`, `ImprovedStats`).
- **Butin** (`Loot.RollWeapon`, fin de mission dans `MissionReport.Build`) : niveau selon la difficulté et l'avancement, puis pour chaque caractéristique utile `MaxStars` tirages indépendants de probabilité `Loot.StarChance(difficulté, niveau)` (≈ 0,15 en Facile au niveau 0 → ≈ 0,7 en Inferno au niveau 100). Garantie : au bout de 30 armes sans caractéristique au maximum, la suivante en reçoit une. Le bilan (`ResultsScreen`) affiche une arme nouvelle avec l'étiquette « NEW », une arme possédée améliorée suivie d'une ligne en retrait par amélioration (« Niveau 12 → 18 », « Dégâts ☆2 → ☆3 (+10) », bonus tiré de `Describe`), une arme possédée sans amélioration assombrie (« déjà possédée »), puis une ligne « Armure avant → après (+gain) » si de l'armure a été gagnée.
- **Caisses** : arme, armure, soin. `CrateType` circule en entier sur le réseau et indexe les compteurs (`Crates.Count` = 4) : la valeur 3 (anciennes caisses de crédits) est réservée (`Reserved3`) et n'est plus jamais tirée ; ne pas la réutiliser sans changer le protocole LAN.
- **Sauvegarde** (`user://profile.json`, `ProfileData.Version` = 3) : migration automatique dans `Profile.Migrate`. Version 1 (rareté + 5 étoiles de 1 à 5) → `WeaponInstance.MigrateLegacy` : étoile − 2 + bonus de rareté (Standard/Renforcée 0, Élite/Prototype +1, Légendaire +2), bornée à 0..5 puis ramenée à l'échelle de `MaxStars` ; la portée prend la moyenne des anciennes étoiles. Les tableaux d'étoiles de mauvaise longueur sont complétés et bornés (changer `MaxStars` ne casse pas un profil). Version 3 : `Profile.MergeDuplicates` fusionne les doublons d'un même modèle dans le premier exemplaire (niveau max, étoiles max par caractéristique) et redirige les références de `Equipped` (sans doublon d'emplacement) ; les anciens champs JSON `Credits` et `ClassXp` sont ignorés à la lecture et disparaissent à la sauvegarde suivante.
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
| `--end-after=N` | Termine la mission avec du butin fictif (test du bilan : armes tirées + une arme nouvelle, une arme de départ améliorée et un doublon sans amélioration). |
| `--menu-shots` | Captures de tous les menus (dont la fenêtre « Difficulté »). |
| `--menu-shots=arsenal` | Profil de test garni d'armes de toutes les classes (avec migration et fusion de doublons), puis captures de l'arsenal (navigation, équipement, changement de classe). |
| `--pause-at=N` | Ouvre le menu pause après N s (captures). |
| `--boom=N` | N explosions par seconde (effets seuls, mélange explosif / plasma / acide, 10 à 55 m devant le joueur) à partir de 5 s ; journal `[boom]` toutes les 2 s : images par seconde, moyenne depuis 6 s de mission, pire image, charge et réserves. `--boom=0` donne la référence sans explosion. Avec `--novsync`, la limite d'images est levée. `--boom-skip=billows,sparks,chunks,lights,haze,decals` masque des familles d'effets pour mesurer leur coût. |
| `--boom-at=T1,T2 --boom-kind=K1,K2 --boom-radius=R1,R2 --boom-dist=D` | Une explosion à D m (20) devant la caméra à chaque instant, avec captures à +0,05 / 0,2 / 0,6 / 1,5 / 4 s (`boom_k<kind>_r<rayon>_t<instant>_<délai>_f<image>.png`) ; le bot reste immobile et ne tire pas. Exemple : `--autostart=0 --bot --god --nohud --shots=build/fx --boom-at=15,20 --boom-kind=0,2 --boom-radius=6,16`. |

Les lancements de test utilisent un profil séparé (`profile_test.json`). Le script `tools/shots.sh` enchaîne compilation et captures dans `build/shots`.

## Build et publication

- `tools/build.ps1` : `dotnet build` puis export Godot vers `build/windows/Rempart.exe` (exécutable autonome, .NET embarqué).
- CI GitHub Actions : build à chaque push sur `main`, release avec l'archive Windows à chaque tag `v*`.
