# Journal des versions

## v0.7.3 — Jetpack à énergie

- **Son du jetpack refait façon « ailes d'énergie »** : plus de flamme. Bourdonnement électrique pulsé, grave et propre, avec un scintillement cristallin et de petites étincelles ; montée en régime à l'allumage, descente à la coupure, éclat d'énergie pour la poussée et le piqué.

## v0.7.2 — Jetpack

- **Nouveau son de jetpack** (Aéroporté) : allumage avec souffle et claquement grave, flamme de propulseur continue sans coupure (fini les rafales hachées) dont la hauteur monte avec la vitesse de montée, extinction en douceur ; rafale dédiée pour la poussée et le piqué. Les coéquipiers entendent aussi le jetpack en LAN.

## v0.7.1 — Vrais sons d'armes

- **Sons d'armes enregistrés** à la place des sons synthétisés : fusils d'assaut (AR-15, AK-47), arme de défense (Carl Gustav M45), fusils à pompe (Benelli Nova, Charles Daly), précision (Mosin Nagant), mitrailleuse rotative, lance-grenades, départ de roquette, réarmement (pompe Winchester) et clic à vide. Plusieurs variantes par arme tirées au hasard ; les tirs des alliés et des coéquipiers utilisent une prise de son à distance.
- **Explosions enregistrées** (grosse explosion, petites explosions, effondrement d'immeuble) et armes à énergie modernisées (laser, lance électrique, plasma, marteau).
- Sons non compressés pour un rendu net. Sources toutes CC0, listées dans `assets/sfx/CREDITS.md`.

## v0.7.0 — Démesure

- **Insectes géants, comme dans EDF** : la fourmi noire fait environ 11 m de long et 4,3 m de haut (le soldat arrive sous sa tête, les pattes passent au-dessus de lui). Rouge ≈ 13 m, charognard ≈ 8 m, araignée ≈ 12 m d'envergure, guêpe ≈ 6 m, drone ≈ 5 m, mille-pattes ≈ 25 m, reine ≈ 34 m, nid ≈ 34 m ; la forteresse ne change pas. Portées de morsure, collisions, points faibles, escalade, sorties de terre, tirs et butin suivent la taille ; foulées plus longues et vitesses à peine relevées ; points de vie inchangés.
- **Saisie par les fourmis rouges** : la fourmi rouge attrape sa victime dans ses mandibules, la soulève et la secoue en la mordant (dégâts continus) ; seule la mort de la fourmi la libère. Aucun message à l'écran, seulement un voile rouge qui pulse. Synchronisé en LAN.
- Un insecte dans lequel passe la caméra devient translucide : le soldat reste visible au milieu de l'essaim.
- **Mobilier et véhicules destructibles** : lampadaires, feux, panneaux et poteaux basculent et s'abattent (ils blessent les insectes dessous) ; arbres qui tombent ; bancs, poubelles, bornes, abribus, cabanes, séparateurs en béton éclatent en morceaux ; voitures, fourgons et camions fument, prennent feu puis explosent (souffle qui blesse les insectes, réactions en chaîne, épave calcinée) ; épaves aplaties par le char, les gros insectes et la forteresse ; conteneurs poussés par les explosions. Le couvert détruit disparaît.
- **Immeubles** : dégâts progressifs (fissures, trous d'obus, éclats de façade, incendies aux étages touchés, fumée du toit), puis effondrement en plusieurs temps : façades qui se vident, enfoncement incliné, blocs projetés, énorme nuage de poussière qui roule dans les rues, tas de gravats praticable.
- **Plus de crédits** : caisses jaunes supprimées (remplacées dans le tirage par des caisses d'arme et d'armure), compteur retiré du profil, du menu principal, du bilan, du tableau des scores et du guide.
- **Plus d'expérience ni de niveau de classe** : ni bonus de dégâts ni PV par niveau, plus de « Niv. » dans les menus ; l'armure grandit uniquement avec les caisses d'armure.
- **Un exemplaire par arme** : retrouver une arme déjà possédée l'améliore (niveau le plus haut, meilleur nombre d'étoiles par caractéristique). Au bilan, une arme nouvelle porte l'étiquette « NEW » ; une arme améliorée liste ses améliorations en dessous (« Niveau 12 → 18 », « Dégâts ☆2 → ☆3 (+10) ») ; une arme possédée sans amélioration est assombrie.
- **Bilan** : plus de jauge d'armure, de crédits ni d'XP ; une simple ligne « Armure 250 → 264 (+14) ».
- **Sauvegardes** : les doublons d'armes existants sont fusionnés automatiquement (armes équipées conservées).
- **Hélicoptère** : les balles partent de la tourelle de menton (qui s'oriente vers la cible, jusqu'à la verticale) et les roquettes des paniers ; viser sous l'appareil tire vers le sol (les roquettes n'explosent plus sur l'hélicoptère). Les chocs contre les murs, toits et le sol (rotor compris) abîment l'appareil selon la vitesse ; un choc violent le détruit. Sons réalistes : battement des pales, turbine, montée et descente en régime, minigun, départ de roquettes, alarmes du cockpit, Doppler pour l'appareil d'un coéquipier.
- **Munitions limitées pour les véhicules** : minigun, roquettes, canon et mitrailleuse du char ont un chargeur et une réserve par sortie, affichés dans la pile d'armes ; réarmement à l'arrêt au point de départ. Le char s'abîme légèrement en percutant un mur à pleine vitesse.
- **Radar** : char et hélicoptère affichés en jaune (losange à rotor, carré), triangle jaune sur le bord quand ils sont loin.
- Petits bonus d'étoile affichés avec une décimale (« +9,6 » au lieu de « +10 », plus de « +0 »).

## v0.6.0 — Nouvelles interfaces

- **Interfaces entièrement refaites** (maquettes validées, ADN Earth Defense Force) : palette verte, fond animé (grille, radar, circuits, balayage), boutons communs en plaques, polices Share Tech Mono et Saira Condensed.
- **Sélection de mission** : seules les missions débloquées sont affichées (déblocage en chaîne) ; un clic sur une mission demande la difficulté, un clic sur la difficulté lance la mission (en LAN, dès que l'escouade est prête). « Très difficile » devient « Chaos ».
- **HUD épuré** : armure graduée, radar, pile d'armes, petit réticule, barre de vie fine au-dessus de chaque allié, marqueur des alliés à terre.
- **Armes** : plus de rareté, plus d'amélioration ni de recyclage ; chaque caractéristique (dégâts, cadence, chargeur, rechargement, précision, portée) a son propre niveau d'étoiles (0 à 5) qui donne un bonus chiffré. Anciennes sauvegardes converties.
- **Arsenal** : une colonne par type d'arme, armes équipées assombries, noms longs défilants, bonus affichés par caractéristique.
- **Pause, bilan, options, guide, rejoindre** au même style ; option « Réduire les animations ».

## v0.5.0 — Lisière

### Direction artistique « invasion »
- **Ciel nuageux** procédural (deux couches qui dérivent, éclairées côté soleil), lumière voilée, palette grise et désaturée, vignettage et grain.
- **Villes en ruines** : épaves calcinées, barrages militaires abandonnés, gravats, façades noircies, vitres soufflées, incendies et colonnes de fumée, toiles et résine d'insectes, réservoirs en feu au port, maisons brûlées et arbres morts en banlieue.
- **Matières patinées** : coulures de pluie, salissures, flaques, asphalte fissuré, trottoirs cassés, angles chanfreinés, véhicules ternis et rouillés.

### Gameplay
- **Les insectes grimpent** : fourmis, araignées et mille-pattes escaladent les façades, traversent les toits et attaquent les joueurs perchés (solo et LAN).
- **Collisions précises** : murets, balcons, parapets, clôtures, toitures, mobilier et véhicules arrêtent les tirs et les joueurs là où ils sont visibles.

- **Hélicoptère d'attaque** pilotable (E pour monter) : roquettes et canon, vol stationnaire, synchronisé en LAN. Char et hélicoptère partagent une base commune de véhicule.
- **Nouvelle carte « Banlieue boisée »** : village avec mairie, église et cimetière, pavillons avec jardins, supermarché, stade, rivière guéable et ponts, forêt dense en lisière.
- **Mission 8 « Lisière »** : vagues venues de la forêt, traversée de la rivière, assaut final de fourmis rouges et d'araignées.
- **Nouvel ennemi : le mille-pattes géant** : corps de 12 segments qui serpente, morsure et crachat acide, chaque segment encaisse les tirs et les explosions.
- **Équilibrage de la forteresse** : 70 000 PV au lieu de 150 000, missiles moins puissants.
- **Salon** : liste des missions compacte (le détail n'apparaît que pour la mission choisie).
- **Corrections** : le menu ne reste plus affiché au lancement d'une mission, la place d'un véhicule est libérée quand son conducteur quitte la partie, message d'aide déplacé pour ne plus masquer la vie.

## v0.4.0 — La forteresse

- **Boss « Forteresse marcheuse »** : robot quadrupède de 35 m qui écrase les immeubles, salves laser des tourelles, barrages de missiles, canon principal à charge visible (zones rouges au sol), point faible sur le noyau, seconde phase avec lâcher de drones, séquence de destruction.
- **Mission 7 « La forteresse »** : crépuscule sous l'orage, escortes de fourmis rouges, drones et araignées.
- **Interface peaufinée** : navigation complète au clavier et à la manette (focus visible, listes qui bouclent, onglets Q/E), sélecteurs « ‹ valeur › », transitions douces, mise en page 1280×720, arsenal avec une seule action principale et atelier dans la fiche, révélation des armes avec flash de rareté, pause avec confirmation, radar avec nord et cône de vue.
- **Qualité** : tests de non-régression automatisés en CI (7 missions, char, bilan, partie LAN hôte + client).

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
