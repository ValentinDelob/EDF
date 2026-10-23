# Rempart — Base de connaissances UI / HUD

Objectif : définir des menus et un HUD **simples, modernes et lisibles** pour Rempart (coop TPS 1–4 joueurs, insectes géants, ville côtière réaliste, 4 classes, caisses de loot, armes à rareté/étoiles).

Les captures sont dans `docs/ui-refs/` (ignorées par git : images tierces sous droits). Analyse faite en regardant chaque image.

---

## 1. Sources

| Jeu | Écran | Fichier local | URL |
|---|---|---|---|
| EDF 6 | Menu principal (choix du mode) | edf6-main-menu.jpg | https://www.co-optimus.com/images/upload/image/EDF6-Menu-screenshot--01.jpg |
| EDF 6 | Sélection mission / difficulté | edf6-mission-select.jpg | https://www.co-optimus.com/images/upload/image/EDF6-Menu-screenshot--29.jpg |
| EDF 6 | Sélection classe + équipement | edf6-class-select.jpg | https://www.co-optimus.com/images/upload/image/EDF6-Menu-screenshot--04.jpg |
| EDF 6 | Liste d'armes + stats (étoiles) | edf6-equipment-select.jpg | https://www.co-optimus.com/images/upload/image/EDF6-Menu-screenshot--08.jpg |
| EDF 6 | HUD Ranger (radar, armure, armes) | edf6-hud-boss.jpg | https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/2291060/ss_0d502c3a5fc5ffc71f4a841be01df904c2328f3c.1920x1080.jpg |
| EDF 6 | HUD Wing Diver (jauge énergie) | edf6-hud-wingdiver.jpg | https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/2291060/ss_046e5e209e51f5fae2755b21357efaad743ff973.1920x1080.jpg |
| Helldivers 2 | HUD en mission | helldivers2-hud-overview.jpg | https://helldivers.wiki.gg/images/HUD_Overview.jpg |
| The Division 2 | HUD combat coop | division2-hud-combat.jpg | https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/2221490/be0b56637f8e456fa72c376d4e8688a124d84e70/ss_be0b56637f8e456fa72c376d4e8688a124d84e70.1920x1080.jpg |
| The Division 2 | Matchmaking (tuiles) | division2-matchmaking.jpg | https://interfaceingame.com/wp-content/uploads/tom-clancys-the-division-2/tom-clancys-the-division-2-matchmake.jpg |
| The Division 2 | Inventaire / apparence | division2-inventory-apparel.jpg | https://interfaceingame.com/wp-content/uploads/tom-clancys-the-division-2/tom-clancys-the-division-2-apparel.jpg |
| The Division 2 | Classement (panneau diégétique) | division2-leaderboard-diegetic.jpg | https://interfaceingame.com/wp-content/uploads/tom-clancys-the-division-2/tom-clancys-the-division-2-world-named-enemies-defeated.jpg |
| Destiny 2 | Personnage / équipement | destiny2-character-equipment.jpg | https://interfaceingame.com/wp-content/uploads/destiny-2/destiny-2-character.jpg |
| Destiny 2 | Options — réassignation touches | destiny2-options-keymapping.jpg | https://interfaceingame.com/wp-content/uploads/destiny-2/destiny-2-key-mapping.jpg |
| Apex Legends | Menu principal / lobby | apex-main-menu.jpg | https://interfaceingame.com/wp-content/uploads/apex-legends/apex-legends-main-menu.jpg |
| Apex Legends | Armurerie (catégories) | apex-armory.jpg | https://interfaceingame.com/wp-content/uploads/apex-legends/apex-legends-armory.jpg |
| Apex Legends | Résumé de match | apex-match-summary.jpg | https://interfaceingame.com/wp-content/uploads/apex-legends/apex-legends-match-summary.jpg |
| CoD Modern Warfare (2019) | Sélection opérateur | codmw-operator-select.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-modern-warfare/call-of-duty-modern-warfare-operator-select.jpg |
| CoD Modern Warfare (2019) | Armurerie (arme + stats) | codmw-armory.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-modern-warfare/call-of-duty-modern-warfare-armory.jpg |
| CoD Modern Warfare (2019) | Menu pause in-game | codmw-pause-menu.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-modern-warfare/call-of-duty-modern-warfare-in-game-menu.jpg |
| CoD Modern Warfare (2019) | Lobby escouade | codmw-squad-lobby.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-modern-warfare/call-of-duty-modern-warfare-match-beginning.jpg |
| Deep Rock Galactic | Terminal de mission (diégétique) | drg-mission-terminal.jpg | https://interfaceingame.com/wp-content/uploads/deep-rock-galactic/deep-rock-galactic-open-mission-selection.jpg |
| Deep Rock Galactic | Loadout classe | drg-loadout.jpg | https://interfaceingame.com/wp-content/uploads/deep-rock-galactic/deep-rock-galactic-gunner-inventory.jpg |
| Deep Rock Galactic | Pause coop | drg-pause.jpg | https://interfaceingame.com/wp-content/uploads/deep-rock-galactic/deep-rock-galactic-pause.jpg |
| Deep Rock Galactic | Ouverture de caisse/récompense | drg-claim-reward.jpg | https://interfaceingame.com/wp-content/uploads/deep-rock-galactic/deep-rock-galactic-claim-loot.jpg |
| Deep Rock Galactic | Options contrôles (sliders) | drg-options-controls.jpg | https://interfaceingame.com/wp-content/uploads/deep-rock-galactic/deep-rock-galactic-controls.jpg |
| World War Z | Options HUD | wwz-options-hud.jpg | https://interfaceingame.com/wp-content/uploads/world-war-z/world-war-z-hud.jpg |
| World War Z | Personnalisation arme | wwz-weapon-customization.jpg | https://interfaceingame.com/wp-content/uploads/world-war-z/world-war-z-weapon-customization.jpg |
| World War Z | Post-match | wwz-post-match.jpg | https://interfaceingame.com/wp-content/uploads/world-war-z/world-war-z-post-match.jpg |
| The Finals | HUD | thefinals-hud.png | https://interfaceingame.com/wp-content/uploads/the-finals/the-finals-hud.png |
| Titanfall 2 | HUD pilote | titanfall2-hud.jpg | https://interfaceingame.com/wp-content/uploads/titanfall-2/titanfall-2-pilot-hud.jpg |
| Titanfall 2 | Loadouts | titanfall2-loadouts.jpg | https://interfaceingame.com/wp-content/uploads/titanfall-2/titanfall-2-pilot-loadouts.jpg |
| Titanfall 2 | Menu principal | titanfall2-main-menu.jpg | https://interfaceingame.com/wp-content/uploads/titanfall-2/titanfall-2-main-menu.jpg |
| Daemon X Machina | Résultats (analyse arme) | dxm-results.jpg | https://interfaceingame.com/wp-content/uploads/daemon-x-machina/daemon-x-machina-results.jpg |
| Daemon X Machina | Hangar — liste d'armes | dxm-weapon-list.jpg | https://interfaceingame.com/wp-content/uploads/daemon-x-machina/daemon-x-machina-weapon-list.jpg |

Non trouvés en image exploitable : menus EDF 5/4.1 (même structure qu'EDF 6), loadout Helldivers 2, menus Space Marine 2 et Battlefield 2042 (les captures Steam ne montrent pas d'UI). Galeries utiles à consulter à la main : https://interfaceingame.com/games/ (Division 2, Destiny 2, Apex, DRG, WWZ, The Finals, Halo Infinite, Remnant II), https://www.gameuidatabase.com/ (recherche par jeu/écran), https://helldivers.wiki.gg/wiki/Heads-Up_Display.

---

## 2. Ce que chaque référence fait bien (observé)

### EDF 6 — la référence de genre… et le contre-exemple de forme
- **Bon** : tout est à 1 écran de distance : liste à gauche (mode / classe / slots), aperçu au centre, **personnage 3D à droite** avec récapitulatif (classe, Armor, armes + Lv).
- **Bon** : écran d'arme = colonnes par catégorie, chaque arme avec `Lv` et `★` ; panneau stats valeurs chiffrées en cyan sur fond noir (ex. munitions, cadence, dégâts, rechargement, portée, précision). Ce modèle de données est exactement celui de Rempart.
- **Bon (HUD)** : armure en haut-gauche (barre + valeur actuelle/max), radar circulaire en haut-droite (ennemis = points rouges, alliés = bleus), armes empilées en bas-droite avec munitions `107 / 120`, arme active plus grande. Couleur unique (vert) = identité forte.
- **Mauvais** : cadres biseautés partout, textures rayées vert fluo, 4–5 niveaux de cadres imbriqués, pastilles, barre de titre + barre de pied décoratives. Densité élevée, hiérarchie faible : c'est ce qui fait « daté ». Le HUD vert sature sur un fond rouge/violet (wingdiver).

### Helldivers 2 — HUD minimal en coin
- Bas-gauche : une ligne grenades `x3/4`, chargeurs `x3/5`, icône d'arme ; dessous l'identifiant joueur (pastille orange) + **barre de vie fine blanche** + stims `x2/4`.
- Haut-centre : boussole fine avec distance au waypoint. Haut-droite : objectif courant dans un bandeau semi-transparent avec icône hexagonale orange.
- Haut-gauche : « STRATAGEMS » + compteur renforts. **Aucun cadre lourd**, texte blanc, un seul accent orange ; 80 % de l'écran reste libre.

### The Division 2 — HUD coop et menus « à onglets »
- HUD : minimap circulaire haut-gauche ; **munitions/compétences flottantes près du réticule** (bas-centre-droit) ; barre de santé segmentée au-dessus ; coéquipiers bas-droite (nom + barre segmentée + icônes de compétence) ; barres de vie ennemies au-dessus des têtes ; nombres de dégâts orange.
- Menus : **barre d'onglets en haut avec icône + libellé**, raccourcis `A`/`E` aux extrémités, onglet actif en aplat orange. Inventaire : **personnage 3D plein pied à gauche**, grille de tuiles à droite, pastilles « ! » pour le nouveau. Barre d'aide de touches en bas (`R Inspecter`, `H Loadouts`, `ECHAP Retour`).
- Matchmaking : grandes tuiles carrées (icône + titre), la sélection est **remplie en orange**, le reste en verre dépoli sur une carte 3D de la ville. Très bon modèle pour une sélection de mission urbaine.

### Destiny 2 — calme et hiérarchie
- Bandeau supérieur coloré avec nom + niveau + barre d'XP, onglets texte en capitales espacées, onglet actif blanc, inactifs gris.
- Personnage au centre, **armes en colonne à gauche, armure en colonne à droite** (tuiles carrées), chiffre de puissance très grand en cyan. Fond flou/neutre.
- Options : catégories à gauche (boutons larges), lignes à droite : libellé à gauche, touche au centre, bouton « Set Binding » à droite ; ligne survolée = contour blanc. Pied de page : `Reset to Defaults`, `Dismiss`.

### Apex Legends — lobby « un bouton »
- Onglets top (PLAY, LEGENDS, ARMORY…) inclinés, actif en rouge ; monnaies en haut-droite.
- Personnage au centre, **slots d'escouade « + » de chaque côté** (parfait pour coop 4).
- Bas-gauche : mode + **gros bouton READY** = l'action principale unique. Icônes utilitaires carrées en bas-droite.
- Armurerie : 6 grosses tuiles de catégorie (rendu d'arme + nom), sélection = aplat rouge. Résumé : titre centré énorme, tableau XP à gauche, gros total chiffré, barre de niveau + récompenses suivantes.

### CoD Modern Warfare (2019) — sobriété « militaire réaliste »
- Titre d'écran en haut-gauche en **capitales fines, très grand, couleur atténuée** (OPERATOR SELECT, ARMORY, SETTINGS), sous-onglets petits dessous.
- Armurerie : l'arme posée sur un établi en 3D occupe l'écran ; à gauche nom en gras, rareté colorée (violet = Épique), **barres de stats fines** avec delta vert/rouge. Très bon pour l'écran « révélation d'arme ».
- Pause : liste verticale de boutons translucides à gauche, fond flouté, défis à droite. Lobby : liste d'escouade en haut-gauche, 3 personnages au centre, carte du mode en bas-gauche. Barre de pied fine (Back, Options).

### Deep Rock Galactic — coop, lisibilité, personnalité
- Loadout : **classe 3D au centre**, armes primaires à gauche, outils/équipement à droite, icônes en trait orange monochrome ; sélecteur de classe en bas `<< GUNNER >>` avec LT/RT ; bouton CLOSE en aplat orange.
- Pause coop : liste des joueurs à gauche (avatar, classe, ping, MUTE), infos mission en dessous, menu centré en boutons pleins. Options : onglets top, lignes libellé à droite-aligné + slider + valeur %, ligne focus en aplat orange + chevron.
- Sélection de mission **diégétique** (terminal dans le vaisseau). Récompense = caisse au centre + un seul bouton CLAIM.

### World War Z — le plus proche de « simple et moderne »
- Titre d'écran énorme en gras blanc en haut-gauche, groupe + monnaies en haut-droite.
- Options : onglets avec raccourcis `Q`/`E`, lignes avec sélecteur `< ON >`, **barre d'accent rouge à gauche** de la ligne focus, panneau de description à droite. Option par élément du HUD (vie/munitions, équipe, radar, journal de ramassage, marqueurs).
- Arme : liste à gauche, rendu au centre, stats chiffre + barre à droite (delta vert/rouge), grille d'améliorations numérotée 1–5. Post-match : un seul message, barre d'XP orange, bonus empilés au centre.

### The Finals / Titanfall 2 — HUD FPS épurés
- The Finals : vie en blocs blancs bas-gauche (`150/150`), équipe au-dessus ; arme + slots numérotés bas-droite ; objectif **jaune géant** au centre avec distance ; boussole + chrono haut-centre.
- Titanfall 2 : seulement capacités + arme + `3 | 120` en bas-gauche, petites étiquettes de touche. Menu principal : logo + 3 entrées texte géantes, la sélection en surbrillance, actualités à droite.

### Daemon X Machina — mecha japonais propre (alternative moderne à EDF)
- Fil d'Ariane `HANGAR ▸ Install equipment` en haut-gauche, aide de touches en haut-droite.
- Slots à gauche (Right Weapon, Left Weapon…), liste au centre, **chiffres clés en très gros** (portée 220, dégâts 150/30), robot à droite sur grand motif graphique rouge.
- Résultats : « MISSION COMPLETE » centré cyan, pages paginées `< 02 >`, temps en filigrane géant.

---

## 3. Synthèse — principes pour les menus de Rempart

### 3.1 Navigation
- **Barre d'onglets horizontale en haut** (pattern Division 2 / Apex / Destiny / WWZ) pour le hub : `OPÉRATION · ARSENAL · ESCOUADE · OPTIONS`. Raccourcis `Q`/`E` (LB/RB) affichés aux extrémités.
- Dans un onglet : **liste verticale à gauche** (catégories/slots) + contenu à droite. Jamais plus de 2 niveaux de navigation visibles.
- **Une seule action primaire par écran**, toujours au même endroit : **bas-droite** (ex. `DÉPLOYER`, `PRÊT`, `REJOINDRE`), en aplat couleur accent. Retour toujours bas-gauche (`Échap  Retour`).
- Barre d'aide de touches en bas (glyphes clavier ou manette selon le dernier périphérique utilisé).

### 3.2 Composition
- **Fond 3D plein écran** : le soldat de la classe sélectionnée (ou l'escouade de 1–4) dans un décor de ville côtière/QG, légèrement flouté ou vignetté côté UI. Pas de panneau plein écran opaque.
- Panneaux = **aplats sombres translucides** (noir à 60–75 %), **sans bordure ou 1 px**, coins droits (0–2 px). Pas de cadres biseautés, pas de textures rayées, pas de cadres imbriqués.
- Grille : marges d'écran **96 px gauche/droite, 64 px haut/bas** à 1080p (zone sûre ~5 %). Espacement en multiples de **8 px** (8 / 16 / 24 / 32 / 48 / 64). Colonnes : 12 colonnes, gouttière 24 px.

### 3.3 Typographie (tailles à 1920×1080 ; ×0,833 pour 1600×900)
- Titres/affichage : **Barlow Condensed** SemiBold/Bold (OFL, libre) — ou Rajdhani. Texte et chiffres : **Inter** ou **Barlow** Regular/Medium, chiffres tabulaires pour munitions et stats.
- Échelle :
  | Rôle | Taille | Graisse | Casse |
  |---|---|---|---|
  | Titre d'écran (haut-gauche) | 56 px | Bold | MAJUSCULES, tracking +2 % |
  | Onglets | 20 px | SemiBold | MAJUSCULES, tracking +8 % |
  | Titre de section / nom d'arme | 32 px | Bold | Capitales |
  | Élément de liste / bouton | 22 px | Medium | Phrase |
  | Corps / description | 18 px | Regular | Phrase |
  | Libellé secondaire / aide touches | 15 px | Medium | MAJUSCULES |
  | Chiffre héros (puissance, total XP) | 72–96 px | Bold | — |
- Maximum **2 familles, 3 graisses**. Texte de corps jamais < 15 px à 1080p.

### 3.4 Palette (thème « Rempart » : militaire sobre + un accent signal)
| Token | Hex | Usage |
|---|---|---|
| `bg-900` | `#0E1114` | Fonds pleins, barre de pied |
| `panel` | `#151A1F` à 72 % d'opacité | Panneaux sur fond 3D |
| `line` | `#2A323A` | Séparateurs 1 px, pistes de barres |
| `text-100` | `#F2F4F5` | Texte principal |
| `text-400` | `#9AA4AD` | Texte secondaire, onglets inactifs |
| `text-600` | `#5C6670` | Désactivé |
| **`accent`** | **`#FF8A1F`** (orange signal) | Sélection, action primaire, objectif — **seule couleur saturée des menus** |
| `accent-press` | `#E06F00` | Pression du bouton |
| `info` | `#3FB6E8` | Alliés, bouclier, info neutre |
| `danger` | `#E8413C` | Vie basse, ennemis, alertes |
| `success` | `#5BD16E` | Bonus de stats (+), validation |
- Règles : l'accent ne sert qu'à **ce qui est actif ou actionnable**. Le rouge est réservé au danger. Couleurs de rareté réservées au loot (voir 4.4) et jamais utilisées pour la navigation.
- Contraste : texte ≥ 4,5:1 sur le panneau (blanc sur `#151A1F` ≈ 15:1) ; sur fond 3D, toujours un panneau ou une ombre portée 0 2px 6px noir à 60 %.

### 3.5 États (focus / survol / pression)
- Normal : texte `text-400`, pas de fond. **Focus/hover** : texte `text-100` + **barre d'accent verticale 4 px à gauche** + fond blanc à 6 %. Sélectionné/actif : aplat `accent`, texte `#0E1114`. Désactivé : `text-600`, cadenas.
- Animation : 120 ms ease-out sur le fond et la barre, léger glissement de 4 px. Son de clic court. Aucun effet glitch/scanline.
- **Manette** : le focus est toujours visible et unique ; navigation D-pad sans piège (wrap dans les listes) ; LB/RB = onglets, LT/RT = classe ou page ; A = valider, B = retour, Y/X = action secondaire affichée en bas.

### 3.6 Iconographie
- Icônes en **trait monochrome 2 px** (style DRG/Division), sur grille 24 px, coloriées par l'état. Armes : silhouettes de profil blanches. Classes : 4 pictogrammes simples (fusil, parachute, radio/drone, bouclier). Pas d'icône sans libellé dans les menus (le libellé peut disparaître seulement dans le HUD).

---

## 4. Principes HUD

### 4.1 Zones (position du coin d'ancrage ; marges 48 px à 1080p / 40 px à 900p)
| Zone | 1920×1080 | 1600×900 | Contenu |
|---|---|---|---|
| Haut-gauche | x48 y48, ~360×120 | x40 y40, ~300×100 | Objectif courant + compteurs de mission (vagues, ennemis restants) |
| Haut-centre | centré, y32, 560×40 | centré, y28, 460×34 | Boussole + marqueurs (objectif, caisses pingées, alliés) |
| Haut-centre (sous boussole) | centré, y96, 960×24 | centré, y80, 800×20 | **Barre de boss** (nom + barre), uniquement si boss actif |
| Haut-droite | x1872-r y48, Ø200 | x1560-r y40, Ø170 | Radar circulaire (ennemis rouges, alliés bleus, caisses en couleur de rareté) |
| Droite-milieu | x1872-r y360, 320 de large | x1560-r y300 | Kill feed / journal de ramassage (5 lignes max, fondu 4 s) |
| Bas-gauche | x48 y1032-b, 420×110 | x40 y860-b, 350×92 | **Vie/armure** (barre 360×10 + valeur), capacité de classe, consommables |
| Au-dessus du bas-gauche | x48 y880-b | x40 y730-b | Escouade : 3 lignes (nom 15 px + barre 160×4, icône état « à terre ») |
| Bas-droite | x1872-r y1032-b, 380×110 | x1560-r y860-b, 320×92 | **Arme active** (nom, ★, munitions 40 px `107` / réserve 20 px `120`) + arme secondaire réduite |
| Centre | réticule | réticule | Réticule, hitmarker, indicateurs de dégâts directionnels (arcs rouges sur un cercle Ø300) |
| Bas-centre | centré, y920 | centré, y766 | Invite contextuelle (« [E] Ouvrir la caisse ») + barre de progression d'interaction |

### 4.2 Toujours visible vs contextuel
- **Toujours** : vie/armure, arme + munitions, réticule, radar, objectif (réduit à une ligne).
- **Contextuel** : barre de boss (boss actif), escouade (mode >1 joueur ; masquée si tout le monde est à 100 % après 5 s — option), kill feed/ramassage (apparaît puis fondu), invites d'interaction, compteurs de loot (s'affichent 3 s au ramassage, sinon visibles sur pause/tab), indicateurs de dégâts (1,5 s).
- Vie basse < 25 % : barre `danger` + léger vignettage rouge pulsé (désactivable).

### 4.3 Opacité, échelle, lisibilité
- Éléments du HUD : texte blanc 100 %, pistes de barre noir 40 %, **pas de cadres** ; chaque groupe posé sur un léger dégradé noir (0 → 35 %) depuis le bord d'écran, plutôt qu'un panneau.
- Ombre portée systématique sur texte et icônes (1 px, noir 70 %) — indispensable sur ciel clair, explosions et nuées d'insectes.
- Option « Échelle HUD » 80–120 % et « Opacité HUD » 50–100 %. Option par élément (comme WWZ).
- Les chiffres changeants utilisent des **chiffres tabulaires** pour éviter le tremblement.
- Ne jamais placer d'info critique au centre hors réticule : la nuée doit rester lisible.

### 4.4 Couleurs de rareté et types de caisses
| Rareté | Hex | Étoiles indicatives |
|---|---|---|
| Commun | `#C9CED3` (gris clair) | ★1 |
| Peu commun | `#5BD16E` (vert) | ★2 |
| Rare | `#3FA2FF` (bleu) | ★3 |
| Épique | `#B067FF` (violet) | ★4 |
| Légendaire | `#FFC233` (or) | ★5 |
- La rareté apparaît **en liseré 3 px** (bord gauche de la tuile / bas de l'icône) et sur les étoiles, jamais en fond plein (sauf moment de révélation).
- Caisses (forme + couleur, pour le daltonisme) : **Arme** = pictogramme fusil, couleur de la rareté ; **Armure/vie** = croix, `#3FB6E8` ; **Munitions/soutien** = chargeur, `#FF8A1F`. Le marqueur 3D de caisse reprend la même icône dans un losange + distance (style The Finals).
- Compteurs de loot de mission (ex. « Armes 3 · Armure 5 ») en haut-gauche sous l'objectif, icônes 20 px.

### 4.5 Barre de boss, kill feed, dégâts
- Boss : nom 20 px MAJ au-dessus d'une barre 960×10 `danger`, segments de phase (traits 2 px), dégâts récents en blanc qui se résorbe en 400 ms.
- Kill feed : « Joueur [icône arme] Fourmi géante », 15 px, icône classe colorée selon le joueur (4 couleurs joueur : `#3FB6E8`, `#5BD16E`, `#FFC233`, `#E86FD0`).
- Nombres de dégâts (option) : blanc normal, jaune `#FFC233` critique/point faible, petits (18 px), regroupés.

---

## 5. Spécification concrète des écrans de Rempart

Règle commune : fond 3D, titre d'écran haut-gauche (56 px), onglets en haut, retour bas-gauche, **action primaire bas-droite**, barre d'aide de touches en bas. Pas plus de 3 zones de contenu par écran.

### 5.1 Menu principal (écran titre)
- **Fond** : vue cinématique lente de la ville côtière avec silhouettes d'insectes au loin, soldat Rempart de dos sur un toit.
- **Gauche (x96, centré verticalement)** : logo REMPART (hauteur ~120 px), puis 4 entrées texte 40 px : `JOUER` (solo/héberger), `REJOINDRE (LAN)`, `ARSENAL`, `OPTIONS` ; `Quitter` en petit en dessous. Entrée focus en blanc + barre d'accent ; les autres `text-400`.
- **Bas-droite** : version + statut réseau en 15 px.
- Primaire : `JOUER`. Pas de panneau, pas de cadre.

### 5.2 Lobby de mission / loadout
- **Haut** : onglets `OPÉRATION · LOADOUT · ESCOUADE`. Haut-droite : code/IP de la partie hébergée.
- **Centre** : les 1–4 soldats en 3D côte à côte (style Apex/CoD) ; slot vide = « + » en filigrane ; sous chaque soldat : pseudo 18 px, classe + icône, coche verte « PRÊT ».
- **Gauche (onglet OPÉRATION)** : liste de missions (numéro + nom), en dessous difficulté en sélecteur `< DIFFICILE >`, une ligne de description et les récompenses possibles (icônes de caisse).
- **Onglet LOADOUT** : 4 cartes de classe en haut-gauche (LB/RB) ; slots `ARME 1`, `ARME 2`, `ÉQUIPEMENT`, `CAPACITÉ` en liste à gauche (icône + nom + ★ + liseré de rareté) ; panneau stats à droite seulement quand un slot est focus.
- Primaire bas-droite : `PRÊT` (client) / `DÉPLOYER` (hôte, actif quand tous prêts).

### 5.3 Rejoindre (LAN)
- **Centre-gauche** : liste des parties détectées (nom d'hôte, mission, joueurs `2/4`, ping) — lignes de 56 px, focus = barre d'accent.
- **Dessous** : champ `Adresse IP` + bouton `CONNEXION` secondaire (connexion directe).
- **Droite** : rien, ou le personnage du joueur. État vide : « Recherche de parties sur le réseau local… » + spinner fin.
- Primaire bas-droite : `REJOINDRE`. Secondaire : `Actualiser` (X/Y).

### 5.4 Options
- Onglets : `JEU · CONTRÔLES · SOURIS & MANETTE · VIDÉO · AUDIO · HUD`.
- Contenu : **colonne unique de 960 px** à gauche, sections en titres 15 px MAJ `text-400` ; lignes 52 px : libellé à gauche, contrôle à droite (sélecteur `< >`, slider + valeur numérique éditable, case).
- **Panneau de description** à droite (comme WWZ) : explication de l'option focus, aperçu éventuel.
- **Contrôles** : lignes `Action | Touche principale | Touche secondaire` ; clic → état « Appuyez sur une touche… (Échap pour annuler) » ; conflit = ligne surlignée `danger` avec proposition d'échange. Bouton `Réinitialiser` par section.
- **Souris** : sensibilité (slider 0,1–10, pas 0,05, valeur saisie possible), sensibilité en visée séparée, inverser Y, accélération off par défaut. Manette : sensibilité X/Y, zone morte, assistance de visée.
- Bas : `Échap Retour` à gauche, `R Réinitialiser` ; les changements s'appliquent immédiatement (pas de bouton « Appliquer » sauf résolution vidéo, avec confirmation 10 s).

### 5.5 Pause (coop : le jeu continue en LAN)
- Fond : jeu flouté + assombri 50 %.
- **Gauche** : liste verticale `REPRENDRE`, `OPTIONS`, `ABANDONNER LA MISSION`, `QUITTER` (boutons texte 28 px, focus = barre d'accent).
- **Droite** : carte « Mission » (objectif, temps, difficulté) et escouade (pseudo, classe, ping, `Muet`) comme DRG.
- Primaire : `REPRENDRE` focus par défaut. Confirmation modale pour abandonner/quitter.

### 5.6 Résultats + révélation d'arme
- **Étape 1 (2–3 s)** : « MISSION ACCOMPLIE » / « ÉCHEC » centré 72 px, temps de mission en filigrane.
- **Étape 2 — Caisses** : les caisses récupérées alignées au centre, chacune s'ouvre l'une après l'autre (A pour accélérer). À l'ouverture : flash de la couleur de rareté, l'arme apparaît en grand en 3D (style armurerie CoD), à gauche nom 32 px + rareté + ★, 4 barres de stats avec delta vs arme équipée (vert/rouge). Tag `NOUVEAU`.
- **Étape 3 — Récapitulatif** : à gauche tableau par joueur (éliminations, dégâts, réanimations) ; à droite XP de classe avec barre de niveau (style WWZ/Apex).
- Primaire bas-droite : `CONTINUER` (retour lobby). Secondaire : `Équiper` sur une arme révélée.

### 5.7 Arsenal
- **Haut** : sélecteur de classe (4 pictogrammes, LB/RB) + filtres de catégorie en onglets secondaires.
- **Gauche (480 px)** : liste/grille d'armes de la catégorie : icône silhouette, nom, ★, liseré de rareté, pastille `NOUVEAU`. Tri (rareté / étoiles / récent) en Y.
- **Centre-droite** : arme focus en 3D sur fond neutre ; dessous ou à droite : nom 32 px, rareté, ★, **6 stats max** (dégâts, cadence, chargeur, rechargement, portée, précision) en chiffre 22 px + barre fine, delta vs équipée.
- Primaire bas-droite : `ÉQUIPER (Arme 1 / Arme 2)`. Secondaire : `Comparer` (maintenir), `Inspecter`.

---

## 6. À faire / À éviter

**À faire**
- Un écran = un objectif = un bouton primaire, toujours bas-droite.
- Laisser le personnage/décor 3D respirer : ≥ 40 % de l'écran sans UI dans les menus.
- Aplats translucides sans bordure, coins droits, grille de 8 px, marges généreuses.
- Une seule couleur d'accent (orange) pour l'état actif ; couleurs de rareté réservées au loot.
- Chiffres grands et tabulaires pour ce qui compte (munitions, puissance, total XP).
- Focus visible et unique, glyphes clavier/manette dynamiques, onglets LB/RB.
- HUD dans les coins, info critique près du réticule seulement si elle est minimale.
- Ombre portée sur tout texte du HUD ; options d'échelle/opacité/éléments.
- Tester les écrans en 1600×900 et 1920×1080, et sur un fond d'explosion + nuée.

**À éviter**
- Cadres biseautés, textures rayées, cadres imbriqués, décors « sci-fi » autour des panneaux (le travers d'EDF 6).
- Plusieurs couleurs vives qui se battent ; HUD entièrement vert ou cyan saturé.
- Texte < 15 px, MAJUSCULES pour les paragraphes, plus de 2 polices.
- Fenêtres modales empilées ; plus de 2 niveaux de navigation visibles.
- Tout afficher en permanence (kill feed, escouade, compteurs) : contextualiser.
- Rareté indiquée par la couleur seule : toujours doubler avec étoiles/forme.
- Effets glitch, scanlines, animations > 250 ms sur la navigation.
