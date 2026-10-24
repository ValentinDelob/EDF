# Journal des versions

## Non publié

- **Nouvelle carte « Vallée forestière »** (spécification §9.1 : relief, rivière, visibilité réduite) : une vallée encaissée sous un ciel bas, noyée de pluie et de brume. La rivière descend d'un petit barrage-poids (déversoir qui gronde dans son bassin, retenue au-delà, maison des vannes et conduites forcées) jusqu'à la centrale hydroélectrique en brique, puis serpente en rapides écumeux entre les blocs, passe sous un vieux pont routier en béton et se franchit à deux gués. Route de la vallée et pistes : maison forestière (aire de départ, bivouac de l'armée), camp de bûcherons sur un replat du versant est (baraque, hangars ouverts, piles de grumes, grumiers, coupe rase hérissée de souches), mirador de l'affût sur la rive ouest, ligne à haute tension qui monte vers la crête. Futaie dense d'épicéas et de hêtres, sous-bois de fougères et de buissons, semis, chablis couchés en travers des pentes, falaises et chaos rocheux, toiles d'araignée entre les fûts. Au loin, crêtes boisées à perte de vue.
- **Vrai relief** : collines, ravines, falaises et lit de rivière dans l'aire de jeu (jusqu'à ~50 m de dénivelé). Soldats, véhicules, insectes, alliés, caisses, camions du convoi et structures suivent le sol ; les insectes escaladent rochers, falaises, bâtiments, pont et barrage ; ils contournent la rivière (gués et pont seulement) et les pentes trop raides, que les grimpeurs escaladent si c'est plus court. Char posé sur un terrain plat, qui épouse la pente en roulant.
- **Visibilité réduite** : brouillard épais, brume dans les fonds de la rivière et dans la gorge du barrage, forêt dense ; les sous-bois sont sombres.
- **Arbres destructibles** le long des pistes et autour des lieux (ils basculent sous les tirs et les explosions) ; maison forestière, garage, baraque, hangars et centrale s'effondrent.
- **Deux missions** : « La vallée » (pluie : escorter trois camions de la maison forestière à la centrale, par le pont, la rive ouest et le gué nord) et « Chasse de nuit » (nuit, pluie et brume : araignées et mille-pattes dans les sous-bois).
- Tests : `--forestprobe` (hauteur du sol comparée à la collision, capsules sur la piste du camp, le pont et les deux gués, insectes sous le relief), `--citycam-ground` (hauteurs de caméra relatives au sol), `--nofog` (vues aériennes de contrôle).

## v0.10.0 — Aliens, Titan reptilien, base du désert

- **Nouvelle carte « Base militaire du désert »** : base aérienne fortifiée submergée par l'invasion. Piste en béton qui file jusqu'à l'horizon (un avion de transport écrasé y brûle encore), voie de circulation et tarmac ; grand hangar cintré ouvert (épave d'avion de transport à l'intérieur, voûte crevée), hangar à charpente, deux abris bétonnés (chasseur calciné), tour de contrôle et sa vigie vitrée ; casernements préfabriqués et réfectoire, trois soutes à munitions enterrées, parc automobile, dépôt de carburant dans sa cuvette de merlons, radôme éventré, pylône de transmissions, hélisurfaces. Rempart de gabions percé de brèches, douze miradors, murs en T, postes de combat en sacs de sable, poste de contrôle à la porte sud et route du désert au-delà. Dunes qui envahissent la base, affleurements de grès, cratères, épaves de chars, de blindés et de camions calcinés. Au loin : champ de dunes, mesas, station radar.
- **Véhicules abondants** : trois chars et deux hélicoptères pilotables en plus de ceux du point de départ (devant le parc automobile et sur les hélisurfaces). Synchronisés en LAN comme les autres.
- **Tout se casse** : hangars, abris, tour de contrôle, casernements, radôme et atelier s'effondrent ; gabions, murs en T, miradors (ils basculent d'un bloc), véhicules et caisses sont destructibles ; les soutes à munitions et les cuves du dépôt brûlent puis explosent (souffle énorme, réactions en chaîne).
- **Combat vertical** : escaliers jusqu'au balcon de la vigie (21 m), au toit du hangar à charpente, à la terrasse du radôme, au toit de l'auvent du parc automobile et au plancher des miradors ; on grimpe sur les buttes des soutes et les merlons ; les insectes escaladent hangars, abris, tour, rempart et dunes.
- **Tempête de sable** (nouvelle météo) : brouillard brun qui s'épaissit par rafales, filets de sable chassés à l'horizontale, nappes de poussière au ras du sol, soleil réduit à une tache.
- **Deux missions** : « Fort de sable » (jour : défendre le poste de commandement installé sur le tarmac, un marcheur de combat en dernière vague) et « Vent de sable » (tempête de sable : détruire quatre nids dans une visibilité d'une centaine de mètres).
- Lumière du désert : soleil plus haut et plus franc, brume beige ; le gris de l'invasion reste.
- Tests : `--desprobe` (parcours des escaliers et des buttes avec la vraie physique, explosion d'une soute et d'une cuve).
- **Nouveau boss : le titan reptilien.** Un kaiju de soixante mètres, cuirassé d'écailles sombres, hérissé d'épines dorsales dentelées, qui avance d'un pas lourd (pieds réellement posés au sol, bassin qui se balance, queue qui suit avec retard et fouette aux virages) et traverse la ville en droite ligne : les immeubles s'effondrent à son contact, voitures et mobilier sont écrasés, chaque pas fait trembler le sol et blesse qui se trouve dessous.
  - **Souffle de feu** : sa gorge s'embrase entre les écailles et sa tête se cabre (le tracé du balayage s'allume au sol), puis un long jet de flammes balaie la cible en travers. Il s'arrête au premier obstacle, brûle soldats (véhicules compris), alliés, immeubles et mobilier, et laisse le sol en feu derrière lui.
  - **Coup de queue** contre qui le prend à revers, **rugissement** qui fait trembler l'image à l'arrivée et quand il enrage.
  - **Plaques d'armure à briser** : huit plaques osseuses (épaules, dos, flancs, hanches) encaissent les tirs (le titan ne prend que le quart des dégâts dessous). Brisées, elles tombent avec fracas, le titan titube et la chair à vif incandescente apparaît : dégâts ×3. La tête compte aussi comme point faible.
  - **Phases** : à 50 % puis 20 % de sa vie, il enrage (plus rapide, souffles plus longs et plus fréquents, double balayage en fin de combat).
  - Pas de barre de vie : l'état du combat se lit sur la bête (plaques arrachées, plaies, rugissements). Repère propre au radar (grand disque cerclé, trait vers où il regarde), fiche dans le bestiaire, sons synthétisés (rugissement, cri de douleur, râle, inspiration, grondement du jet, pas colossaux). Butin : 8 à 10 caisses dont 5 armes. Synchronisé en LAN (plaques brisées décidées par l'hôte).
- **Nouvelle mission « Le titan »** (ville, crépuscule) : le titan descend des collines et marche sur le centre-ville, escorté de fourmis, de guêpes, de drones et de mille-pattes.
- Tests : `--showcase=titan`, `--titan-act` (actions forcées), `--titan-plates` (test des plaques, vu du client en LAN), `--titan-hp`, `--titan-log`, `--titan-cam` (voir ARCHITECTURE.md › Tests automatisés).
- **Nouvelle famille d'ennemis : les aliens** (spécification §10.1). Humanoïdes de 4 à 6 m en carapace d'armure sombre sur une chair grise et luisante, casque allongé à fente de capteurs violets, mandibules ; animation complète (marche à pieds posés, course, pas de côté et marche arrière, visée épaulée mains sur l'arme, mise à genou derrière un abri, sursaut à l'impact, recul du tir, chute de mort qui s'étale au sol en lâchant l'arme). Traits d'énergie violets bien visibles, sang d'ichor violet, sons synthétisés propres (tirs, téléportation, rugissement, cris de mort).
  - **Fantassin alien** (400 PV) : se met à couvert derrière les immeubles, murs, conteneurs et camions, se relève ou se décale pour tirer en rafales de quatre traits, puis replonge ; change d'abri quand on le contourne. La tête encaisse double.
  - **Garde alien** (1 200 PV) : bouclier de tour qui arrête les balles de face (étincelles et tintement, sans dégâts) et atténue les explosions de face ; blaster lourd, charge qui écrase le mobilier et coup de bouclier ; il abaisse son bouclier un instant après une charge. Sa cellule d'énergie dorsale est son point faible (×3).
  - **Volant alien** (300 PV) : réacteurs dorsaux, tourne au-dessus des toits (il évite les immeubles), passes de mitraillage en rase-mottes.
  - **Portail** (5 000 PV) : anneau de 18 m à membrane en vortex qui fait passer des escouades d'aliens, de drones et parfois un marcheur tant qu'il tient ; les balles l'égratignent, les explosifs le détruisent. Implosion et effondrement à sa destruction.
  - Variantes élites (armure laquée rouge sang), butin des ennemis moyens (25 %, quatre caisses pour un portail), marqueurs radar (triangles), notes du bestiaire, synchronisation LAN (posture transmise dans l'instantané existant, effets par l'attaque existante, aucune RPC nouvelle). Les vaisseaux de largage déposent des fantassins dans les missions d'aliens.
- **Deux missions** : « Débarquement » (crépuscule en ville : des soldats aliens prennent pied au centre, gardes à bouclier, vaisseau de largage et volants) et « Les portes » (quartier d'affaires : détruire quatre portails).
- Tests : `--aliens=Type[*N],…` (combat réel, journal d'IA, `--aliens-only`, `--aliens-cam`, `--aliens-shots`, `--aliens-kill`, `--aliens-weak`), `--showcase=aliens` (vitrine figée, `--aliens-pose=aim|crouch|walk|run`), `--aliens-net` (synchronisation vue de l'hôte et du client).

## v0.9.1 — Performances (300 ennemis)

- **Performances avec beaucoup d'insectes** (objectif : 300 ennemis à 60 images par seconde) : sur la ville avec 300 fourmis, de ~30 à ~55 images par seconde (150 fourmis : de ~50 à ~80), « Tours de verre » de ~30 à ~58 (150 : de ~53 à ~80), « Acier » (marcheurs et drones) de ~28 à ~50 (150 : de ~49 à ~74). Rendu, comportement et réseau inchangés.
  - Les insectes sont fusionnés en deux maillages animés par os au lieu d'une vingtaine de pièces chacun : moins de mises à jour et d'appels de dessin, animation calculée sans passer par le moteur.
  - Les ennemis ne coûtent plus de calcul physique (corps statiques déplacés par position).
  - Les insectes lointains ou hors champ s'animent moins souvent (jamais ceux dont le corps sert aux collisions : mille-pattes, fourmi qui saisit).
  - IA : positions des soldats, des alliés et des voisins relevées une fois par image.
  - Radar : points ennemis dessinés sans créer de polygone à chaque image.
- Tests : `--perf` (journal détaillé du temps de chaque image, voir ARCHITECTURE.md › Performances) ; `--novsync` lève aussi la limite d'images du profil.

## v0.9.0 — Zone industrielle, Grenadier et Opérateur complets

- **Plus de fourmis sous le sol** : une fourmi qui descendait un mur ou une bordure pouvait continuer sous la dalle et s'y accrocher à une face cachée. Le suivi de surface sonde maintenant dès qu'un pas franchit le niveau du sol, refuse les points situés sous un sol existant (les vrais creux à ciel ouvert, comme l'escalier du métro, restent praticables) et un garde-fou remet en surface tout insecte resté sous le sol.
- **Pas de morsure ni de saisie à travers une paroi** : la fourmi rouge (et toute morsure) exige qu'aucun mur, façade, muret ou véhicule ne la sépare de sa victime ; un arbre, un poteau ou du petit mobilier ne bloquent pas.
- **Grenadier blindé : deux jeux d'armes.** Quatre emplacements (jeu 1 = armes 1-2, jeu 2 = armes 3-4). Nouvelle touche « Jeu d'armes suivant » ([X], croix droite à la manette) : bascule en 0,5 s, arme abaissée et tir bloqué pendant la manœuvre ; les rechargements de l'autre jeu continuent. [Q], la molette et [1] / [2] changent d'arme dans le jeu actif. Nouvelles armes de départ : MH-2 Enclume et MS-6 Essaim.
- Arsenal du Grenadier : quatre emplacements (filet entre les jeux, repères « J1 » / « J2 »), étiquettes « ÉQUIPÉE 1 » à « ÉQUIPÉE 4 », aide « Emplacement n (jeu n) ».
- HUD du Grenadier : le jeu actif reste présenté comme avant (arme tenue en grand, l'autre en petit) ; l'autre jeu apparaît en bande compacte au-dessus (nom, munitions ou « Prête »), avec la touche de bascule et une barre pendant le changement. En LAN, les coéquipiers voient l'arme abaissée pendant la bascule puis la nouvelle arme.
- **Opérateur d'appui : appel de véhicule.** Nouvelle touche « Appeler un véhicule » ([V], croix bas) : un appui bref choisit le char (500 crédits) ou l'hélicoptère (700), un appui maintenu le commande au point visé (80 m au plus). Livraison d'environ 8 s : fumigène rouge, avion cargo, largage sous trois voilures kaki, rétrofusées au ras du sol, poussière à l'impact. Le véhicule se pilote ensuite comme ceux du point de départ (tout le monde peut monter à bord, réarmement à son point de pose). Un seul véhicule appelé par Opérateur : un nouvel appel rapatrie l'ancien s'il est libre. Décidé par l'hôte et synchronisé en LAN.
- Crédits d'appel : gagnés aussi par les tourelles de l'Opérateur, plafond relevé à 1 000, toujours remis à zéro à chaque mission. Affichés sous l'armure dans une petite plaque « Appui » (véhicule choisi et son coût, jauge avec repères du barrage et du véhicule, appui long en surimpression).
- Sauvegarde version 4 : l'équipement du Grenadier est complété à quatre armes sans toucher aux deux premiers emplacements. Une touche par défaut nouvelle ne remplace jamais une touche déjà attribuée par le joueur.
- Tests : `--heavy-sets` et `--call-vehicle=tank|heli` (vérifiés par `tools/smoke.ps1`).
- **Nouvelle carte « Zone industrielle »** : raffinerie et parc de six cuves de stockage dans leur cuvette de rétention, sphères de gaz sur pieds, réservoirs de GPL, colonnes de distillation, four et torchère encore allumée ; ateliers en brique à toiture en sheds et verrières d'acier brisées, fonderie à lanterneau, cheminées de brique et de béton, silos à ciment ; entrepôts en bardage à quais de chargement, parc à conteneurs ; gare de triage (six voies, rame de wagons-citernes, wagons couverts et trémies, wagon déraillé, portique à conteneurs) ; station d'épuration (décanteurs à pont racleur, lagunes de boues), quai de vrac avec tas de charbon, convoyeur et grue, canal aux eaux huileuses franchi par deux ponts. Au-delà des murs : autres usines, tours de refroidissement, gazomètre, ligne à haute tension, panaches de fumée.
- **Citernes explosives** : cuves, sphères, réservoirs et wagons-citernes se percent sous les balles (fuite de vapeur), prennent feu (jet de flammes, puis explosion quelques secondes plus tard) et explosent en boule de feu qui monte en champignon, avec tôles projetées et épave déchirée qui brûle longtemps. Le souffle tue les insectes, abîme immeubles et mobilier et fait sauter les citernes voisines (réactions en chaîne : les réservoirs de GPL serrés partent en cascade, un wagon-citerne enflamme le suivant). Il blesse aussi les soldats proches (moins fort si un joueur l'a provoqué). Synchronisé en LAN.
- **Combat vertical** : escalier hélicoïdal jusqu'au toit des cuves, passerelles au sommet des racks de tuyauteries (tours d'escalier), toit d'entrepôt, galerie des silos (25 m), poutre du portique (18 m), convoyeur jusqu'à la tour de transfert ; les insectes escaladent cuves, sphères, ateliers et cheminées.
- Clôtures grillagées qui cèdent sous les balles, les explosions et les gros insectes ; mâts d'éclairage qui basculent ; wagons, semi-remorques et piles de conteneurs poussés ou abattus ; ateliers, entrepôts et cheminées destructibles. Le canal ne se traverse qu'aux ponts (ou en ressortant par les cales).
- **Deux missions** : « La raffinerie » (jour : la colonie grouille dans le parc de cuves, les citernes sont un outil autant qu'un danger) et « Nuit de soufre » (crépuscule d'orage : détruire les nids au cœur du site, un marcheur de combat dont les obus font sauter les cuves).
- Tests : `--indprobe` (parcours des accès en hauteur avec la vraie physique, réaction en chaîne des citernes) et `--tankboom=x,z,t` (fait sauter une citerne à un instant donné, pour les captures).

## v0.8.0 — Machines, objectifs et quartier d'affaires

- **Escouades à rallier** : les soldats PNJ tiennent leur poste et combattent sur place jusqu'à ce qu'un joueur les rallie (touche d'interaction à moins de 10 m, repère « Rallier [E] » à l'approche) ; ils suivent ensuite ce joueur. Le Fusilier commande jusqu'à 2 escouades, les autres classes une seule. Si le chef quitte la partie, l'escouade reprend un poste sur place. Synchronisé en LAN. L'étiquette « Soldat » géante au-dessus des PNJ disparaît (barre fine du HUD seulement).
- **Visée assistée à la manette** (Options › Contrôles, 50 % par défaut) : le réticule freine sur les ennemis et s'en rapproche doucement quand on bouge ou qu'on vise ; sans effet au clavier et à la souris.
- **Accessibilité** (Options › Jeu) : caisses pour daltoniens (orange / bleu ciel / magenta), intensité des secousses de caméra réglable, éclairs d'explosion atténuables. La caisse de soin devient cylindrique : chaque type de caisse se reconnaît aussi à sa forme.
- **Encyclopédie** dans le Guide : bestiaire (espèces abattues au moins une fois, points de vie, comportement, nombre d'éliminations ; les autres restent « ? ? ? »), armes obtenues par classe et par catégorie, opérations réussies par difficulté.
- **Nouvelle carte « Quartier d'affaires »** : grille dense de tours de bureaux (6 à 50 étages) en mur-rideau, béton et bandeaux techniques, grand parvis central avec bassin asséché, sculpture et deux tours d'angle, viaduc autoroutier au sud avec bretelle d'accès et une travée effondrée jusqu'à la chaussée, deux parkings à étages, entrée de métro, passerelles vitrées entre tours, hélisurfaces sur les toits. Au-delà des murs, la ligne d'horizon de tours prolonge le quartier sur environ 1,2 km².
- **Combat vertical** : toits plats accessibles par les escaliers de secours (pavillons du parvis, bureaux bas), les rampes des parkings, la bretelle et la travée effondrée du viaduc, ou en vol (Aéroporté) ; les insectes grimpent aux façades et marchent sur les toits.
- **Tours debout** : une tour de 20 étages et plus ne s'effondre jamais ; chaque impact arrache des panneaux de façade (vitres qui pleuvent, plaques qui tombent, cavité noircie, parfois un incendie d'étage). Les immeubles plus bas s'effondrent comme en ville.
- Quartier envahi : tours éventrées et en feu, panaches noirs, terrains dévastés où la colonie a creusé ses nids, épaves dans les voies, carambolage, bus encastré, barrages militaires abandonnés avec mâts d'éclairage, cratères, verre et débris, toiles entre les tours. Mobilier et véhicules destructibles comme sur les autres cartes.
- **Deux missions** : « Tours de verre » (jour : guêpes entre les tours, fourmis qui escaladent les façades) et « Panne générale » (nuit sous la pluie, réseau électrique coupé : détruire les nids au pied des tours).
- **Trois nouveaux types de mission** (déblocage à la suite des huit premières) :
  - **09. Le relais** (port, défense) : protégez une antenne relais de campagne (dalle, sacs de sable, abri technique, pylône de 24 m) pendant quatre vagues ; si elle tombe, son mât s'abat et la mission échoue.
  - **10. Convoi** (ville, escorte) : trois camions militaires traversent le centre-ville sur 670 m. Ils s'arrêtent quand un insecte barre la route ou quand aucun soldat n'est à moins de 40 m ; un camion détruit est projeté sur le bas-côté, la mission échoue si les trois tombent. Les vagues surgissent devant et derrière le convoi ; les camions écrasent les voitures garées sur leur passage.
  - **11. Dernier carré** (banlieue, nuit d'orage, survie) : tenez dix minutes face à des vagues sans fin, de plus en plus nombreuses et rapprochées.
- Les insectes s'en prennent aussi à la structure défendue et aux camions (s'ils ne sont guère plus loin qu'un soldat) ; tout est synchronisé en LAN.
- **HUD minimal** : barre de vie fine et nom au-dessus de l'antenne et des camions (comme les alliés), petit losange jaune, rappel sur le bord de l'écran quand ils sont hors champ, « À L'ARRÊT » sous le camion de tête sans escorte, losange du point d'arrivée. En haut au centre, en petit : compte à rebours de survie, puis d'extraction.
- **Fin de mission** : une fois l'objectif rempli, 15 s d'extraction affichées pour ramasser les caisses ; la mission ne peut plus échouer pendant ce temps (même si toute l'escouade est à terre). En Facile et Normal, les caisses restantes sont collectées automatiquement au bilan.
- **Nombre d'insectes selon l'escouade** : × 1 en solo, × 1,4 à deux, × 1,7 à trois, × 2 à quatre (auparavant × 1,8 et × 2,2 à trois et quatre).
- Tests : `--survive-time`, `--obj-hp`, `--convoy-speed`, `--objlog`, `--timescale` ; `tools/smoke.ps1` vérifie l'issue de chaque nouvel objectif.
- **Machines extraterrestres** : trois nouveaux ennemis mécaniques, gris acier, sales et rouillés, à l'échelle EDF.
  - **Marcheur** (≈ 14 m) : robot bipède aux jambes inversées et aux deux bras-canons lourds. Il garde ses distances et pilonne : chaque tir se charge 1,3 s (trait de visée rouge, cercle au sol) avant un obus explosif qui abîme immeubles et mobilier. Point faible : les genoux (dégâts ×2). Le sol tremble à chaque pas.
  - **Tripode** (≈ 23 m) : coque blindée sur trois immenses pattes, tentacules mécaniques. Son rayon de destruction se charge 1,8 s (trajectoire marquée au sol) puis balaie la zone en diagonale : il brûle les soldats, fait exploser le mobilier et peut abattre un immeuble. Point faible : le noyau incandescent sous la coque (×3).
  - **Vaisseau de largage** (≈ 44 m) : descend au-dessus des toits, ouvre sa soute et lâche des fourmis (ou des drones), puis recommence ailleurs avant de repartir. Il ne craint rien tant que sa soute est fermée : visez l'intérieur quand elle s'ouvre (×3, les explosions dans la soute comptent aussi). Abattu, il pique en flammes et s'écrase.
  - Destructions spectaculaires (chapelets d'explosions, effondrement, crash), sons dédiés (pas lourds, charges, canon, rayon, soute, moteurs), marqueurs propres sur le radar, variantes d'élite au blindage bronze, butin. Entièrement synchronisé en LAN.
- **Deux nouvelles missions** : « Acier » (marcheurs et drones dans le centre-ville, sous la pluie) et « Les tripodes » (tripodes et vaisseaux de largage sur le bourg, dans la tempête).
- Le bot de test esquive aussi les tirs lourds des machines.
- Tests : `--showcase=machines`, `--machines=...` (combat, caméra libre, captures aux instants de mission, destruction) et `--hitch` (journal des à-coups).

## v0.7.4 — Jetpack discret

- **Son du jetpack adouci** : bourdonnement grave et constant (plus de pulsation, d'étincelles ni d'éclats), allumage et coupure en fondu, poussée en simple souffle feutré ; volume nettement plus bas et portée réduite pour les coéquipiers.

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
