# Journal des versions

## v0.3.0 — Réalisme et nouvelle carte

- **Soldats réalistes** : maillages lissés, camouflage généré par classe (woodland, urbain, désert, sombre), gilet porte-plaques, casque moderne avec lunettes et casque audio, exosquelette du Grenadier ; mains guidées par IK sur l'arme, postures de visée, course, sprint, réception, rechargement.
- **Insectes réalistes** : fourmis anatomiques (tête, antennes coudées, mandibules dentées, thorax, nœuds, gastre segmenté, pattes articulées avec griffes), guêpe à ailes nervurées, araignée velue, drone détaillé, nid organique ; démarche avec pieds posés au sol, pose de mort au sol. Meilleures performances (62-69 i/s avec 150 fourmis au lieu de 55).
- **Nouvelle carte « Zone portuaire »** : entrepôts en tôle, parc à conteneurs destructibles, grues portiques, cuves, mâts d'éclairage, quai. Accueille « La toile ».
- **Mission 6 « Les docks »** : destruction de nids de nuit, sous la pluie, sur la zone portuaire.
- **Marquage de position** (clic molette) partagé avec l'escouade et affiché sur le radar.

## v0.2.0 — Contenu et sensations

### Nouveautés
- **Ville réaliste** : voirie marquée (passages piétons, lignes, bordures), 10 styles de façades (verre, brique, enduit), commerces en rez-de-chaussée éclairés la nuit, balcons, toits équipés, feux tricolores, arrêts de bus, mobilier urbain, chantiers, parkings, place à fontaine, front de mer avec promenade, parasols, poste de secours et jetée.
- **Char de combat** pilotable (E pour monter) : canon, mitrailleuse, écrasement des insectes, synchronisé en LAN.
- **Escouades alliées** : soldats PNJ qui suivent le joueur et tirent sur les insectes.
- **Nouveaux ennemis** : araignée géante (bonds, toiles) et drone d'attaque (lasers).
- **2 missions bonus** : « La toile » et « Ciel noir », débloquées après « La reine » ; missions débloquées progressivement.
- **Progression de classe** : expérience, niveaux 1 à 60 (+1 % de dégâts, +3 PV par niveau).
- **Atelier** : amélioration d'une étoile d'arme contre des crédits.
- **13 armes de haut niveau** (32 au total), dont des salves multi-roquettes.
- **Musique procédurale** (menu, calme, combat, boss) avec fondus enchaînés, bruitages enrichis.
- **Météo** : pluie (Nids sous la ville) et orage avec éclairs (La reine).
- **Manette** : déplacement, caméra au stick droit, gâchettes et boutons.
- **Tableau des scores** (Tab) et escouade dans le bilan ; écran **Guide**.

### Sensations
- Flash discret à l'impact, chiffres de dégâts regroupés, aimantation des caisses, bond des fourmis à la morsure.
- Arme tenue selon l'arme équipée, animation de rechargement, rappel des commandes en début de mission.
- Options : échelle du HUD, affichage des chiffres de dégâts ; touches affichées selon la disposition du clavier (ZQSD en AZERTY).

### Technique
- CI plus rapide (2 min 30 au lieu de 13 min), profil de test séparé, sauvegarde atomique du profil.

## v0.1.0 — Premier POC jouable

### Jeu
- 3 missions sur la ville côtière : « Premier contact » (jour, extermination), « Nids sous la ville » (crépuscule, destruction de nids), « La reine » (nuit, boss).
- 4 classes : Fusilier (sprint, roulade, fusée de ralliement), Commando aéroporté (vol au réacteur, énergie, piqué), Opérateur d'appui (frappes d'artillerie, tourelle, champ de soin, barrage), Grenadier blindé (poussées, chaleur, ancrage).
- 19 armes avec statistiques (dégâts, cadence, chargeur, rechargement, précision, portée), niveaux 0 à 100, 5 raretés et étoiles de qualité ; doublons qui améliorent l'arme.
- Ennemis : fourmis noires, rouges et acides, guêpes, charognards (avalent les caisses), nids, reine.
- Caisses de butin lâchées par les ennemis : armes, armure permanente, soin, crédits ; aimantation des caisses proches.
- Escouades de soldats alliés qui suivent le joueur et tirent sur les insectes.
- Immeubles destructibles, explosions, débris, poussière ; chiffres de dégâts et flash à l'impact.
- 5 difficultés (Facile à Inferno), bilan de mission avec révélation des armes.

### Multijoueur
- Coopération 1 à 4 joueurs en LAN (hôte faisant autorité), découverte automatique des parties, connexion par IP.
- Relève des coéquipiers à terre ; seconde chance en solo.

### Interface
- Menus refaits selon la base de connaissances UI (docs/ui-knowledge-base.md) : écran titre, salon avec escouade en 3D, arsenal, options, pause, bilan.
- Options : remappage complet des touches (affichage selon la disposition du clavier), sensibilité souris et en visée, inversion de l'axe, qualité graphique, échelle de rendu FSR 2, champ de vision, volumes, échelle du HUD.
- HUD sans cadres : objectif, compteurs de butin, radar, barre de boss, vie, ressource de classe, arme et munitions, indicateurs de dégâts.

### Technique
- Godot 4.7.2 .NET, C#. Rendu Forward+ (SDFGI, SSAO, brouillard volumétrique selon la qualité).
- Sons synthétisés procéduralement, sans fichier audio externe.
- Build local `tools/build.ps1`, CI GitHub Actions (build à chaque push, release à chaque tag `v*`).
- Options de test : `--autostart`, `--bot`, `--shots`, `--autohost` / `--autojoin`, `--stress`, `--showcase`, `--fps`.
