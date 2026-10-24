# Interfaces retenues — direction E (ADN Earth Defense Force)

Maquettes validées par le joueur-commanditaire (canevas Claude Design « Rempart — Interfaces V3 »), exportées dans `project/E-*.dc.html` (ouvrir dans un navigateur : HTML + CSS, les tailles sont en px pour un écran 1280×720 à mettre à l'échelle).

## Style commun
- Palette verte EDF : fond radial `#158050 → #0C4A2C → #052012`, cadres `#46E86B`, liserés `#2AB84F`, texte clair `#DBF6E6` / `#9CFFB0`, panneaux `rgba(6,40,18,0.88)` bordés de vert avec halo. **Aucun marron / beige.**
- Polices : « Share Tech Mono » (chiffres, textes de briefing/stats), « Saira Condensed » (titres, boutons), contour sombre (text-shadow).
- **Fond animé identique** pour le menu principal, la sélection de mission et le choix des armes : grille qui défile en diagonale, anneaux radar qui tournent (sens opposés) avec balayage, impulsions lumineuses le long des circuits, nœuds qui pulsent, bande de balayage horizontale, particules qui montent, voyant qui clignote. Animations coupées si « réduire les animations ».
- Bandeau de titre en haut (biseaux verts), bande basse (biseaux) avec l'aide clavier à droite (« Entrée:OK  Échap:Retour… ») qui ne doit jamais chevaucher un bouton.
- **Boutons uniques partout** (menu, missions, difficulté, pause, bilan) : plaque en verre vert sombre à coins coupés (clip 14 px en haut à droite et en bas à gauche), bloc icône/numéro à gauche (50 px), titre en capitales Saira 22 px, **aucun sous-titre sous le titre**. Survol : s'éclaircit et glisse de 6 px. Sélectionné : vert vif dégradé, texte foncé, décalé de 16 px, halo, reflet lumineux qui le parcourt, flèche animée à gauche. Variante « danger » (Abandonner) : accent rouge discret.

## Menu principal
Jouer en solo, Héberger (LAN), Rejoindre (LAN), Arsenal, Options, Guide, Quitter. Panneau central d'info (texte seulement, **pas de barre de progression**), carte de profil, soldat à droite.

## Sélection de mission
- **N'afficher que les missions débloquées.** Les missions se débloquent les unes après les autres (pas de badge « Nouveau »). Coche sur les missions terminées.
- **Cliquer une mission ouvre une fenêtre « Difficulté »** (rappel de la mission + 5 boutons : Facile, Normal, Difficile, Chaos, Inferno). **Cliquer une difficulté lance la mission** (écran « DÉPLOIEMENT »). Échap ferme la fenêtre. Plus de bouton « Déployer » ni de sélecteur de difficulté dans le briefing.
- La difficulté « Très difficile » s'appelle désormais **« Chaos »**.
- Briefing (texte) + vignette de mission (image seule, sans libellé ni cases), carte de classe, escouade (multijoueur).

## Choix des armes (Arsenal)
- Même fond animé et palette verte que les menus.
- Tableau du haut : **une colonne par type d'arme** (Fusils d'assaut, Lance-roquettes, Fusils à pompe, Mitrailleuses, Précision… selon la classe), chaque arme rangée sous son type, colonnes **Lv** et **☆**.
- **Pas de rareté d'arme.** **Pas de recyclage ni d'amélioration.** Pas de pastilles/numéros rouges.
- Arme déjà équipée : ligne **foncée** (non équipable une seconde fois) + étiquette « ÉQUIPÉE 1 » / « ÉQUIPÉE 2 ».
- Noms trop longs : **défilement en boucle** (bandeau) dans la cellule.
- Panneau du bas : description à gauche ; à droite, une ligne par caractéristique : `(☆n) Dégâts : 46 (+10)`. **Chaque caractéristique a son propre nombre d'étoiles (0 = minimum, X = maximum ; X à confirmer, 5 par défaut)** et chaque étoile apporte **un bonus chiffré**, toujours un avantage (jamais de comparaison « moins bon »).
- Soldat + carte d'équipement à droite.

## HUD en mission
Garder uniquement :
- Armure en haut à gauche (jauge EDF graduée, PV actuels / max).
- Radar EDF en haut à droite (anneau segmenté, ennemis rouges, alliés bleus, cône de vision).
- Pile d'armes en bas à droite (arme active grande : nom, silhouette, munitions « 27 / 40 Balles » + barre de rechargement ; arme 2 petite).
- **Vie des alliés : petite barre fine (5 px, sans graduations) au-dessus de leur tête**, avec leur nom ; allié à terre : marqueur « Nom · distance / Relever [E] ».
- Réticule **petit et discret** (croix fine ~16 px + point) avec marqueur de touche, chiffres de dégâts.

Supprimés : objectif, panneau escouade, notification de butin, message radio, barre/pourcentage de boss, bloc « Fusée de ralliement », aide des contrôles.

## Pause
Reprendre / Options / Abandonner la mission (boutons communs), panneau mission (objectif, phase, difficulté, temps, éliminations, ennemis restants), panneau escouade. Pas de phrase « la partie continue », pas de butin.

## Bilan
VICTOIRE / ÉCHEC dans le bandeau, statistiques en plaques communes (nom à gauche, valeur à droite), butin (armes avec Lv et ☆, **sans rareté**), une arme déjà possédée dont le niveau ou des caractéristiques progressent liste ses améliorations en dessous (« Dégâts ☆2 → ☆3 (+10) », « Niveau 12 → 18 »), armure gagnée (ligne simple, **sans barre**), boutons Rejouer / Continuer au style commun.

Décision ultérieure du commanditaire : **plus de crédits ni d'XP / niveaux de classe** (retirés du profil, des menus, du bilan et du guide).
