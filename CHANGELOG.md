# Journal des versions

## v0.20.0 — Monster Attack au rendu actuel

- **Lumière et couleurs** inspirées de Monster Attack : grand ciel bleu à cumulus, plein soleil, ombres bleutées, couleurs
  vives et brume claire au loin ; coucher de soleil orangé, nuit bleue lisible ; ciel gris seulement sous la pluie et
  l'orage. Vignettage et grain allégés.
- **Ville** : immeubles clairs et bigarrés (crème, sable, saumon, gris bleuté, brique orangée, tours vitrées bleutées),
  beaucoup moins de suie et de vitres brisées ; pièces visibles derrière les fenêtres (murs, sol, plafond et mobilier en
  perspective, éclairées la nuit) ; pelouses vertes ; flaques seulement sous la pluie (elles passaient pour de la neige).
- **Caméra comme dans EDF** : soldat centré dans le tiers bas de l'image, caméra plus loin et plus haute, épaule seulement
  en visée ; champ de vision de 60° par défaut.
- **Difficulté progressive** : la campagne commence par des fourmis noires seules, puis un nouveau type d'ennemi à la fois ;
  vagues bien moins nombreuses (32 ennemis en mission 1 au lieu de 88) ; ennemis moins résistants et moins dangereux au
  début de la campagne (× 0,55 en mission 1, puissance normale à partir de la mission 11) ; une vague minutée attend que
  le terrain se dégage ; nids plus lents ; reine plus abordable ; pas d'élites dans les deux premières missions.
- **Butin réduit** (environ trois fois moins de caisses) et **nouvelles caisses modélisées** comme dans EDF 5 : conteneur
  d'arme vert, caisse d'armure rouge, mallette de soin blanche à croix rouge ; elles rebondissent et se posent à plat,
  avec un halo au sol et un faisceau visible seulement de loin.
- **Effets de tir** : flamme de bouche en étoile avec flammes le long du canon, traçantes en courtes traînées qui filent
  vers la cible, impacts de balles qui restent sur les murs et le sol.
- **Véhicules** : char vert-gris de l'armée, traces de chenilles au sol, poussière soulevée par le coup de canon, fumée et
  étincelles quand le blindage faiblit.

## v0.19.0 — Animations du soldat

- **Vraies foulées** : marche, trot, course et sprint avec des pas posés au sol (le pied ne glisse plus pendant l'appui),
  rebond du corps, rotation du bassin et des épaules, déroulé du pied ; pas lourds et appuyés pour le Fencer.
- **Déplacements en visée** : pas chassés de côté et recul à reculons (les jambes vont dans le sens du déplacement, le buste
  et l'arme restent sur la cible) ; le corps se penche selon la vitesse, au démarrage et au freinage.
- **Saut** (genou levé à la montée, jambes ramenées à la descente, réception amortie selon la hauteur), **roulade
  d'esquive** (Ranger, Air Raider), fente pendant la ruée du Fencer et la poussée de la Wing Diver, corps couché dans le sens
  du vol.
- **L'arme bouge dans les mains** : elle rebondit à chaque pas, roule d'un côté à l'autre, traîne quand le soldat démarre,
  s'arrête ou tourne (plus discrète en visée).

## v0.18.0 — Ranger, Fencer et Air Raider réalistes

- **Nouveaux modèles pour les trois autres classes**, faits dans Blender sur la même base réaliste que la Wing Diver
  (MakeHuman) :
  - **Ranger** (Fusilier, et soldats alliés) : treillis camouflé, gilet porte-plaques à poches (chargeurs, radio, trousse
    de secours), ceinturon et étui, genouillères et coudières, gants, rangers, casque de combat à lunettes et support de
    vision nocturne, sac d'assaut.
  - **Fencer** (Lourd) : exosquelette en acier (cuirasse, épaulières massives, gantelets, cuissards et jambières, vérins
    hydrauliques le long des jambes), casque intégral à visière lumineuse orange, groupe d'alimentation dorsal à
    propulseurs.
  - **Air Raider** (Appui) : tenue de travail gris-vert, gilet à bandes orange, tablette de ciblage, désignateur laser,
    casque à visière relevée avec casque audio et micro, gros sac radio à antenne.
- Animés comme la Wing Diver : squelette qui suit l'animation du soldat, mains sur l'arme, doigts refermés ; expressions du
  visage (clignement, effort, douleur, cri, concentration) pour le Ranger et l'Air Raider.

## v0.17.1 — Coiffure de la Wing Diver

- **Plus de trou à l'arrière du crâne** : la calotte de cheveux (nuque comprise) reste fixée à la tête ; seules les mèches
  qui s'éloignent du crâne suivent la queue de cheval animée. Sous-couche châtain sous les cheveux (aucune peau visible entre
  les mèches).

## v0.17.0 — Wing Diver réaliste

- **Nouvelle Wing Diver** (Commando aéroporté), modélisée dans Blender : femme au corps réaliste (base MakeHuman), visage,
  yeux noisette, queue de cheval châtain ; combinaison blanche à panneaux bleu marine, gants, bottes, armure blanche laquée à
  liserés (plastron, épaulières, avant-bras, hanches, cuisses, genouillères, col) ; unité de vol futuriste (carénage galbé,
  cœur plasma lumineux, ailes-propulseurs en flèche, buses lumineuses). Tête nue, sans inscription « EDF ».
- **Animée** comme les autres soldats (course, visée, vol, réception, à terre) : son squelette suit l'animation du soldat,
  ses mains tiennent l'arme, doigts refermés sur la poignée ; flammes du réacteur sous ses buses.
- **Queue de cheval animée** (ressort : elle suit la course, le vol et les poussées).
- **Expressions du visage** : clignement des yeux, effort au tir et en vol, douleur quand elle est touchée, cri à terre,
  concentration en visée.

## v0.16.2 — Bombes Splendor fidèles à EDF 5

- **Splendor** (Limpet Splendor, Splendor Shot, Buster Bomb, détecteurs) : retour au comportement d'EDF 5, vérifié sur le wiki du jeu — les éclats partent en demi-sphère dans la direction du dessus de la bombe (normale de la surface où elle s'est collée) ; collée sur un ennemi, elle l'épargne presque, posée sous des ennemis elle fait le plus de dégâts. Splendor Shot : 15 bombes de 8 éclats (13,6 × 15 × 8). Annule l'essai de la v0.16.0 (éclats donnés à l'ennemi porteur, une seule bombe).
- Valeurs de référence du banc d'essai recoupées avec le wiki d'EDF 5 (MLRA, FG7 Hand Gatling, Splendor Shot).

## v0.16.1 — Lance-roquettes sur l'épaule et corrections du banc d'essai

- **Lance-roquettes et lance-missiles portés sur l'épaule**, comme dans les jeux de guerre modernes : le tube repose sur le dessus de l'épaule droite, la tête à sa gauche, main droite sur la poignée de tir, main gauche sur la poignée avant ; au repos, il reste sur l'épaule, bouche relevée. Ils ne rentrent plus dans l'épaule.
- **Dégâts des projectiles rapides** (lances de la Wing Diver, fusils de précision, canons) : l'atténuation avec la distance est calculée au point d'impact réel (elle comptait une image de vol en trop : une Power Lance perdait 38 % à 10 m).
- **Split Ball** : la boule se divise en 8 ; **Plasmafall** : les boules retombent en pluie autour du point visé.
- **Changer d'arme** efface les verrouillages de missiles et arrête la rafale, la salve ou la décharge en cours (elles passaient à l'arme suivante).
- **Missiles en piqué** (MLRA, MLRA-TW) : la bascule commence avant le sommet de la montée et le guidage final est plus serré ; ils ne bouclent plus au-dessus de la cible pour s'écraser devant.
- **Quake Haken** : le soldat automatique frappe devant lui (pas la dispersion arrière).
- Banc d'essai : **les 907 armes conformes à EDF 5** (rapport `docs/AUDIT_ARMES.md`).

## v0.16.0 — Modèles d'armes détaillés et banc d'essai des armes

- **Toutes les armes remodelées dans Blender** (151 modèles d'armes et 25 objets de l'Air Raider) : silhouettes et dimensions propres à chaque famille, arêtes biseautées, découpes (fentes d'aération, évidements, rainures), vis, stries de prise, inscriptions au pochoir. Fini les blocs.
- **Textures de détail** cuites pour chaque modèle (occlusion, usure des arêtes, crasse) : peinture écaillée qui laisse voir le métal, arêtes polies, grain du polymère, salissures dans les creux, marquages usés. Couleurs propres à chaque famille.
- **Banc d'essai des armes** (`--weapon-audit`, plan dans `docs/PLAN_TEST_ARMES.md`, résultats dans `docs/AUDIT_ARMES.md`) : les 907 armes des quatre classes sont mesurées une à une (dégâts par coup, cadence, rechargement, vitesse des projectiles, DPS) et comparées aux valeurs d'EDF 5 : **883 conformes**, 24 écarts restants listés dans le rapport.
- **Corrections trouvées par le banc d'essai** :
  - Cadence exacte des armes très rapides (Mag Blaster et Rapier à 60 coups/s, Raven et Sirius à 30 coups/s) même quand les images ralentissent : les coups dus partent dans la même image.
  - Plus de recul sur les armes à énergie de la Wing Diver (rayons, lasers, arcs, éclairs) ; recul par coup réduit pour les armes très rapides. Le MONSTER et le Closed Laser restent sur la cible.
  - Explosions : dégâts pleins dans tout le rayon, comme dans EDF 5 (bombes collantes, roquettes, grenades, mortiers…).
  - Missiles et lances guidées : guidage terminal, ils ne tournent plus autour d'une cible immobile (MLRA, Mirage, Ghost Chaser).
  - Obus de plasma de la Wing Diver à leur vitesse d'EDF 5 ; Splasher : gerbe tirée devant soi.
  - Lance-grenades et bombes collantes à leur vitesse d'EDF 5 (elles étaient plafonnées).
  - Bound Gun : les balles ne partaient pas (vitesse nulle).
  - Flame Revolver : flammes à 20 coups/s, à la vitesse et la portée d'EDF 5 ; les flammes rapides ne traversent plus la cible.
  - Limpet Splendor, Splendor Shot, Buster Bomb, détecteurs : l'ennemi qui porte la bombe reçoit la moitié des éclats ; Splendor Shot : une bombe de 15 éclats.
  - Leviathan, Haytal, Phoenix sans kit de guidage : tir droit.
  - Changer d'arme pendant la décharge d'une arme à rafale chargée l'arrête (l'arme suivante ne se vidait plus toute seule).
  - Rafales rapides (FGS Diffusion Gatling…) : la rafale tient dans l'intervalle de la cadence.

## v0.15.1 — Corrections

- **Balles explosives** (Minion Buster, mitrailleuses à impulsions de la Wing Diver, canons de la canonnière…) : l'insecte touché de plein fouet prend toujours les dégâts. Une grande fourmi a son centre loin du point d'impact, et l'explosion, de rayon réduit, ne l'atteignait pas. Même correction pour les roquettes et grenades qui explosent sur un grand ennemi.
- **Lance-missiles** : pas de tir sans cible verrouillée, comme dans EDF 5. Les lance-missiles à kit de guidage du Fencer tirent toujours droit sans cible.

## v0.15.0 — Armes de l'Air Raider d'EDF 5 et verrouillage des missiles

- **Les 233 armes et équipements de l'Air Raider d'EDF 5** remplacent ceux de l'Opérateur d'appui (les véhicules viendront ensuite) : tirs d'artillerie (mortiers, obusiers, Cannon Gun, Gigantic Grenade), canonnière (canons de 105 à 190 mm, roquettes, Autocannon, Minigun, Vulcan), bombardiers (KM6, Kamuy, Vesta, Phobos), missiles de croisière (AH, N5/N6, Lionic, Tempest), satellites (Bulge Laser, Spritefall), bombes collantes (Limpet Gun, Sniper, Splendor, Chain Gun, Detector, Buster Bomb), armes posées (robots bombes, Assault / Stag Beetle, Patroller, Speed Star, tourelles ZE / FZ), soutien (kits de guidage laser, balises, Life Vendor, Life Spout, Power / Guard Post, Zone Protector), abris et leurres (bunkers électromagnétiques, abri antiaérien, cage, murs, ballons leurres, Suppress Gun).
- **Frappes comme dans EDF 5** : fumigène pour l'artillerie et les bombardiers (le bombardier passe dans l'axe soldat → fumigène), désignation au laser pour la canonnière, les missiles (ils suivent le laser) et les satellites ; rechargement en points gagnés en infligeant des dégâts. Aucune frappe sous terre.
- **Verrouillage des lance-missiles** : rectangle de verrouillage au HUD, bouton de visée tenu, bip régulier puis bip de confirmation, cible gardée tant qu'elle reste dans le rectangle, marquée pendant le verrouillage puis confirmée ; trajectoires d'EDF 5 (poussée de départ, montée et piqué de la Prominence, de l'Air Tortoise, des MLRA et du FG High Altitude Impact, accélération, guidage selon le rang).
- **Les balles partent du bout du canon** : le soldat épaule instantanément au tir et la traînée ne dépasse plus en arrière de l'arme.
- **Leurres** : les ennemis proches attaquent les ballons leurres et le Speed Star. **Abris** : leurs murs arrêtent les tirs ennemis.
- Les lance-missiles à kit de guidage du Fencer visent les balises et le laser de guidage de l'Air Raider.
- 30 modèles 3D (objets tenus, tourelles, robots, postes, ballon, canonnière et bombardiers) et les sons de l'Air Raider.

## v0.14.0 — Armes du Fencer d'EDF 5 et équipements de soutien

- **Les 175 armes du Fencer d'EDF 5** remplacent celles du Grenadier blindé, avec leurs noms et valeurs officielles : frappe (Vibro Hammer, Vulcan Hammer, Force Blade, Dynamo Blade, Force Ax, Vibro Roller, Vibro / Quake Haken), perforation (Blasthole Spear, Blast Twin Spears, Flashing Spear, Jackhammer, Spine Driver), boucliers (Deflection, Ion Mirror, Tower, Reflector, Master, Great Shield), canons automatiques (Hand Gatling, FGS, Galleon, Dexter, Flame Revolver, Disruptor), artillerie (canons à main NC, Gallic, Powerdyne, Cannon Shot, mortiers FH / FHD / YH, Super-Heavy Crusher) et lance-missiles (Arm Hound, Blood Storm, Leviathan, Haytal, Arcane, FG High Altitude Impact, Phoenix).
- **Frappes chargées** : tenir pour charger jusqu'à trois niveaux, relâcher pour frapper (onde de choc, lames d'énergie qui volent, dispersions, explosion, charge vers l'avant) ; parade pendant la frappe. **Lances** à deux attaques enchaînées.
- **Boucliers** : dans le jeu actif, ils parent les coups venus de face en s'usant ; en main, la garde renvoie les projectiles ennemis vers leur tireur.
- **Canons rotatifs** à mise en rotation, mortiers en cloche qui retombent sur le point visé, Disruptor à rafale continue.
- **50 équipements de soutien** (deux emplacements) : propulseurs (poussées et sauts propulsés consécutifs), renforts de bouclier, stabilisateurs de recul, exosquelettes (vitesse de marche, effet du poids). Les poussées et sauts propulsés remplacent la jauge de chaleur.
- **36 modèles 3D** montés sur l'avant-bras de l'exosquelette, bouclier tenu au bras gauche, et un son par famille.
- Les anciennes armes du Grenadier sont converties en l'arme d'EDF 5 du même genre.

## v0.13.0 — Armes de la Wing Diver d'EDF 5 et cœurs plasma

- **Les 239 armes de la Wing Diver d'EDF 5** remplacent celles du Commando aéroporté, avec leurs noms et valeurs officielles : courte portée (Rapier, Phalanx, Power Lance, Dragoon Lance, Spark Vine / Whip, Monster Lance), blasters (Mag Blaster, Destroyer, Spine, Reflectron), électriques (Lightning Bow, Bolt Gun, Thunder Crossbow), impulsions (Pulse Machine Gun et ses versions N-Way, Stardust Cannon, Pulse Buster), longue portée (Closed Laser, MONSTER, Bolt Shooter, Raijin), artillerie plasma (Plasma Cannon, Plasmafall, Big / Heavy / Burst Cannon, Grit, Splasher, Blink Ball), armes guidées (Mirage, Geist, Ghost Chaser, Fenrir, Psiblade) et spéciales (Diffuser, Gleipnir, Thunder Cluster, Heaven's Gate, Psi Cluster, Final Days).
- **Énergie comme dans EDF 5** : armes à recharge payée en énergie (même rangées), armes à charge (tenir pour charger, relâcher pour tirer : la puissance suit la charge), rafales chargées et armes à alimentation directe ; changer d'arme perd la charge.
- **38 cœurs plasma** (nouvel emplacement du Commando) : énergie maximale, recharge, recharge d'urgence, coût du vol et de la poussée, poussées renforcées (Turbo, Jet, Return), charge des armes accélérée (Rush), vol plus rapide (Bird)… Quand l'énergie est vide, recharge d'urgence jusqu'au plein.
- **40 modèles 3D** dans le style de la Wing Diver (coque claire, bandes lumineuses) et un son par famille.
- Les anciennes armes du Commando sont converties en l'arme d'EDF 5 du même genre.
- **Arsenal** : pages de colonnes quand une classe a plus de sept types d'armes (PgPréc / PgSuiv).
- Les balles explosives (impulsions, Minion Buster) calculent leurs dégâts de zone chez le tireur : beaucoup moins de messages réseau.

## v0.12.0 — Armes du Ranger d'EDF 5, morsure des fourmis, arsenal

- **Les 260 armes du Ranger d'EDF 5** remplacent celles du Fusilier, avec leurs noms et leurs valeurs officielles (dégâts, cadence, munitions, rechargement, portée, vitesse des balles, précision S++ à F, explosion, verrouillage) : fusils d'assaut (PA-11, M1 Raven, Minion Buster, Slugger, G&M, Stork, X-Orchid, Slade, Blazer…), fusils à pompe (Slaughter, SGN, Breecher, Monsoon…), fusils de précision (KFF, Lysander, Fang, Dunkel, Ptarmigan, Penetrator…), lance-roquettes (Grant, Leopard guidé, Goliath, Volcano…), lance-missiles à verrouillage (Emerald, MLRA, Air Tortoise, Prominence…), grenades (MG, DNG, lance-grenades UM, BRUTE, Stampede, grenades collantes et rebondissantes, napalm, Flame Geyser), armes spéciales (acide, lance-flammes, pétards, Bound Gun, bombes C, mines Impulse, Reverser).
- **Étoiles comme dans EDF 5** : chaque caractéristique a son propre maximum (jusqu'à ★10) et suit la formule d'EDF 5 ; le niveau est celui de l'arme ; ★ quand une caractéristique est au maximum. Les armes du Fusilier déjà gagnées sont converties en l'arme d'EDF 5 du même type au niveau le plus proche.
- **Balles réelles** : vitesse, portée, perte de puissance avec la distance, perforation, balles explosives, rafales ; missiles qui verrouillent plusieurs cibles, roquettes guidées sur le réticule, grenades à retardement qui rebondissent, collantes, napalm qui brûle au sol, lance-flammes, bombes à déclencher, mines automatiques, soin des coéquipiers.
- **62 modèles 3D** (un par famille, faits dans Blender) avec leurs variantes (laser, lunette) et des matières d'arme (acier phosphaté, polymère grenu, peinture) ; **un son enregistré par famille**.
- **Morsure des fourmis noires** refaite : la fourmi se cabre, lève les pattes avant, bondit, referme les mandibules sur sa cible puis la secoue ; les dégâts tombent quand les mandibules se ferment (on peut esquiver) ; portée d'attaque raccourcie pour que la tête atteigne vraiment le soldat.
- **Arsenal** : cliquer sur une arme équipée (ou sur sa ligne dans la carte d'équipement) la marque « À REMPLACER » et la prochaine arme cliquée prend sa place, y compris pour le second emplacement ; colonnes compactes pour les sept types d'armes du Ranger.
- Tests : `--edf5-fire` (stand de tir de toutes les armes), `--weapons=…`, `--weapon-cam=…`, `--grab=bite`.

## v0.11.2 — Nouvel hélicoptère

- **Hélicoptère d'attaque entièrement remodelé** (dans Blender) : fuselage effilé, verrière en tandem à facettes et montants, flancs à caissons, nacelles moteur avec diffuseurs d'échappement infrarouge, ailerons à pylônes (rails de missiles et paniers de roquettes), train à roues avec bras tirés et roulette de queue, dérive en flèche, stabilisateur, radôme de mât, boule de capteurs sous le nez, tubes de Pitot, marchepieds, lance-leurres, feux de position.
- **Pièces animées** : rotor principal à quatre pales, rotor de queue en ciseaux, tourelle de menton qui suit la visée (lacet et site séparés) avec un canon rotatif à trois tubes ; les roquettes partent des paniers sous les ailerons.
- Formes de collision ajustées au nouveau fuselage (corps, poutre de queue, ailerons, disque du rotor).
- Correctif : plus d'erreur quand le véhicule est détruit par un choc pendant la conduite (le joueur est éjecté proprement).
- Script de présentation des modèles dans Blender (`tools/blender/studio.py`).

## v0.11.1 — Nouveau char, suspension et jauges de classe

- **Char de combat entièrement remodelé** (dans Blender) : char moderne détaillé, avec caisse à long glacis et plaques additionnelles, jupes blindées à panneaux boulonnés, tourelle à blindage en coin, caissons latéraux, panier arrière, viseurs du chef et du tireur, tourelleau téléopéré, lance-pots fumigènes, antennes, phares, grilles moteur. Le canon a un manchon thermique, un évacuateur de fumées et un frein de bouche, et son fût recule au tir.
- **Vraies chenilles** : 7 galets par côté, barbotin denté, roue tendeuse et rouleaux porteurs qui tournent selon la vitesse ; bandes dont les maillons, crampons et connecteurs défilent. En virage, la chenille extérieure va plus vite ; en rotation sur place, les deux tournent en sens contraire. Synchronisé en LAN.
- Le char repose désormais sur ses chenilles (il flottait de 30 cm à l'arrêt).
- **Suspension** : la caisse et la tourelle reposent sur des ressorts amortis : le char cabre en accélérant, plonge au freinage, roule en virage, encaisse les bosses et bascule au départ du coup de canon ; il épouse le sol (trottoirs, gravats, pentes) sur toutes les cartes.
- **Franchissement** : trottoirs, bordures, murets de jardin, sacs de sable, épaves et blocs de moins de 1,1 m ne l'arrêtent plus — il les gravit (garde au sol de 55 cm). Il écrase voitures, camions et blocs de béton et pousse les conteneurs. Seuls les immeubles, les murs hauts et les poutres basses l'arrêtent.
- **Échappement** : voile léger au ralenti, bouffées noires qui s'épaississent à l'accélération (moteur en marche seulement avec un conducteur).
- **Son des chocs** : le choc métallique « en cloche » (véhicules contre les murs, mobilier qui tombe) est remplacé par un impact sourd et mat, nettement plus discret.
- **Jauge de classe** (HUD, sous l'armure) : énergie du Commando aéroporté et chaleur de l'exosquelette du Grenadier blindé (elles n'étaient plus affichées) : dix segments, pourcentage, seuil d'alerte, état « Surchauffe » qui pulse.
- Test : `--tank-drive` (conduite scriptée, journal des blocages et de la caisse ; `=cam` pour une caméra de suivi).

## v0.11.0 — Vallée forestière, réseau souterrain, capitale et Vaisseau-mère

- **Nouvelle carte « Vallée forestière »** (spécification §9.1 : relief, rivière, visibilité réduite) : une vallée encaissée sous un ciel bas, noyée de pluie et de brume. La rivière descend d'un petit barrage-poids (déversoir qui gronde dans son bassin, retenue au-delà, maison des vannes et conduites forcées) jusqu'à la centrale hydroélectrique en brique, puis serpente en rapides écumeux entre les blocs, passe sous un vieux pont routier en béton et se franchit à deux gués. Route de la vallée et pistes : maison forestière (aire de départ, bivouac de l'armée), camp de bûcherons sur un replat du versant est (baraque, hangars ouverts, piles de grumes, grumiers, coupe rase hérissée de souches), mirador de l'affût sur la rive ouest, ligne à haute tension qui monte vers la crête. Futaie dense d'épicéas et de hêtres, sous-bois de fougères et de buissons, semis, chablis couchés en travers des pentes, falaises et chaos rocheux, toiles d'araignée entre les fûts. Au loin, crêtes boisées à perte de vue.
- **Vrai relief** : collines, ravines, falaises et lit de rivière dans l'aire de jeu (jusqu'à ~50 m de dénivelé). Soldats, véhicules, insectes, alliés, caisses, camions du convoi et structures suivent le sol ; les insectes escaladent rochers, falaises, bâtiments, pont et barrage ; ils contournent la rivière (gués et pont seulement) et les pentes trop raides, que les grimpeurs escaladent si c'est plus court. Char posé sur un terrain plat, qui épouse la pente en roulant.
- **Visibilité réduite** : brouillard épais, brume dans les fonds de la rivière et dans la gorge du barrage, forêt dense ; les sous-bois sont sombres.
- **Arbres destructibles** le long des pistes et autour des lieux (ils basculent sous les tirs et les explosions) ; maison forestière, garage, baraque, hangars et centrale s'effondrent.
- **Deux missions** : « La vallée » (pluie : escorter trois camions de la maison forestière à la centrale, par le pont, la rive ouest et le gué nord) et « Chasse de nuit » (nuit, pluie et brume : araignées et mille-pattes dans les sous-bois).
- Tests : `--forestprobe` (hauteur du sol comparée à la collision, capsules sur la piste du camp, le pont et les deux gués, insectes sous le relief), `--citycam-ground` (hauteurs de caméra relatives au sol), `--nofog` (vues aériennes de contrôle).
- **Nouvelle carte « Réseau souterrain »** : la colonie a gagné le sous-sol de la ville. Grande station de correspondance sous une voûte carrelée de 17 m (quai central, piliers, escalators jusqu'à une passerelle qui domine les voies, dernier poste de combat de l'armée, une rame abandonnée portes ouvertes et une rame qui brûle), tunnels à double voie en voussoirs de béton (rails, chemins de câbles, lampes de secours, signaux, voitures déraillées couchées en travers), atelier de maintenance, galeries techniques et salle des pompes, couloir de correspondance carrelé, puits de ventilation d'où tombe un jour gris, collecteur d'égout et sa salle de décantation, tronçon effondré qu'on franchit en escaladant l'éboulis (ou par la fissure creusée par les insectes), chambre de ponte et grande caverne évidée par la colonie : parois de ruche alvéolées, colonnes organiques, concrétions, grappes d'œufs, résine, toiles, excroissances luminescentes.
- **Obscurité** : ni ciel ni soleil. On y voit à la lampe de casque (chaque soldat en porte une), aux lampes de secours dont une sur trois est morte, aux fusées éclairantes, aux incendies et à la lueur verte de la ruche, dans une brume froide.
- **Combat en tunnel** : les insectes courent sur les parois et sous les voûtes, sortent du sol des galeries et contournent la roche ; les araignées bondissent sous la voûte. La caméra ne traverse plus les parois étroites.
- **Pas d'appui aérien sous terre** (spécification §9.1) : ni char ni hélicoptère, et l'Opérateur n'a plus de liaison — balises, barrage d'artillerie et appels de véhicule sont refusés (son « vide », plaque « Appui » éteinte, « hors liaison »).
- **Deux missions** : « Les entrailles » (détruire quatre nids au fond de l'atelier, dans la salle des pompes, au-delà de l'éboulement et au bord de la caverne) et « La mère » (abattre la reine dans la grande caverne ; elle n'en sort pas, mais tient l'entrée).
- Les caisses de butin se posent sur le quai ou l'éboulis où elles tombent.
- **Nouvelle carte « Capitale en ruines »** pour la fin de la campagne : un fleuve aux quais de pierre franchi par trois ponts à arches (l'un rompu), une grande place à obélisque et fontaines transformée en bastion (gabions, sacs de sable, canons antiaériens, poste de secours), un grand boulevard sous un métro aérien (station, escalier, travée écroulée qui sert de rampe, rames déraillées), un arc de triomphe au quadrige renversé, un parlement à portique et dôme, deux ministères à colonnade, une cathédrale à la nef brûlée, un opéra, un parc aux arbres morts. Îlots haussmanniens (boutiques, balcons filants, combles mansardés, façades éventrées), barricades aux têtes de pont, convois détruits, cratères, incendies, panaches à l'horizon. Tout se casse, tout a sa collision exacte, on grimpe partout (escaliers, travée, berges, emmarchement).
- **Boss final : le vaisseau-mère.** Une soucoupe de plus de 400 m qui couvre le ciel du centre historique. Sa coque ne craint rien : il faut attendre que son canon principal s'ouvre (le cœur s'embrase, un anneau marque le sol visé) pour frapper le cœur, puis survivre au rayon qui balaie la ville. Dix tourelles destructibles, soutes qui lâchent drones, volants et vaisseaux de largage, trois phases de plus en plus rapides (double tir à la fin). À sa mort, une cascade d'explosions le traverse, il bascule et va s'écraser au loin. Pas de barre de vie ; repère propre au radar (avec la zone visée par le canon), fiche dans le bestiaire, sons synthétisés, gros butin (14 caisses dont 7 armes), synchronisé en LAN.
- **Deux missions** : « Dernier rempart » (crépuscule : tenir la capitale sous le vaisseau-mère pendant que ses vagues déferlent de tous côtés) et « Reconquête » (jour de pluie : reprendre la ville rue par rue après sa chute).
- Tests : `--showcase=mothership`, `--mothership-act`, `--mothership-cam`, `--mothership-shots`, `--mothership-hp`, `--mothership-log`, `--mothership-test`, `--capprobe` (voir ARCHITECTURE.md › Tests automatisés).

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
