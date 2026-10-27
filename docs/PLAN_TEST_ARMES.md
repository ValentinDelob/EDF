# Plan de test des armes

Objectif : vérifier, arme par arme, que chaque arme du jeu se comporte comme dans EDF 5 (valeurs des tables officielles,
maximum d'étoiles) : dégâts, cadence, chargeur, rechargement, vitesse des projectiles, portée, DPS, et mécanismes propres
(verrouillage, charge, mise en rotation, frappes aériennes…). Chaque arme des quatre classes (Fusilier = Ranger, Commando
aéroporté = Wing Diver, Grenadier blindé = Fencer, Opérateur d'appui = Air Raider) a son propre cas de test, généré à partir de
ses données (`scripts/Data/Edf5/Edf5Weapons.*.cs`).

## Scénario (par arme)

Banc d'essai automatique `--weapon-audit` (`scripts/AutoTest.Audit.cs`), sans affichage :

1. L'arme est donnée au soldat au **maximum d'étoiles** (ses valeurs sont alors celles des tables d'EDF 5).
2. Une fourmi noire **immobile et quasi indestructible** (5 000 000 PV, points faibles désactivés) est placée à une **distance
   adaptée** à l'arme (8 à 60 m pour les armes à balles selon la portée, 6 m pour un fusil à pompe court, 45 m pour un
   lance-missiles, 60 m pour un mortier…), sur une **ligne de tir dégagée** (aucun décor entre le soldat et la cible).
3. Les escouades alliées ne tirent pas (dégâts mesurés = ceux de l'arme seule) ; le soldat revient à son point de départ et
   reste immobile avant chaque arme (les charges et ruées du Fencer le déplacent).
4. **Phase de tir** : le bot vise le centre de la cible et tire un chargeur (6 s au plus), en gardant la détente comme un
   joueur (armes automatiques), en chargeant (armes à charge), en verrouillant (lance-missiles).
5. **Phase de rechargement** : chargeur vidé, rechargement complet chronométré.
6. **Phase d'attente** : projectiles, frappes aériennes, tourelles et robots finissent leur action.

Le journal note chaque tir (`SHOT`), chaque perte de PV de la cible à chaque image (`HIT`), le début et la fin du
rechargement (`RELOAD`), la distance réelle bouche du canon → surface visée (`AIM`), et les valeurs attendues (`BEGIN`).

## Mesures et tolérances

| Mesure | Calcul | Attendu (EDF 5) | Tolérance |
|---|---|---|---|
| Dégâts par coup | dégâts totaux reçus (après le premier coup) / coups tirés | dégâts × projectiles × atténuation à la distance parcourue bouche → cible (balles : `far + (1 − far) × (1 − d/portée)^x`, x = 2,5 pour les fusils à pompe) ; explosifs : dégâts pleins dans tout le rayon | −25 % / +12 % (−55 % pour les gerbes de plombs, les arcs et les éclairs, dont une partie manque la cible ; +60 % pour les éclairs, qui rebondissent et retouchent) |
| Cadence | (n − 1) / durée entre le 1er et le n-ième tir (rafales : un départ tous les « burst » coups) | « Rate of Fire » | ±12 % |
| Rechargement | durée début → fin | « Reload » (s) | ±10 % (ou ±0,12 s) |
| Vitesse des projectiles | distance bouche → surface / délai premier tir → premier dégât | « Shot Speed » | ±25 % (mesurée seulement si le vol dure ≥ 0,12 s ; pas pour les mortiers, en cloche, ni les grenades à retardement et les gerbes à vitesse dispersée) |
| DPS soutenu | dégâts mesurés × chargeur / (chargeur / cadence + rechargement), avec les mesures ci-dessus | même formule avec les valeurs d'EDF 5 | −25 % / +15 % |
| Fin du scénario | le banc d'essai termine l'arme | — | doit terminer |

Bombes à éclats (Splendor, Buster Bomb, détecteurs) : « n.m. » — dans EDF 5 leurs éclats partent vers l'extérieur et
épargnent l'ennemi sur lequel la bombe est collée ; le scénario (bombe tirée sur la cible) ne mesure pas leurs dégâts.
Natures sans dégâts directs (postes de soutien, kits et balises de guidage, abris, leurres, Speed Star, boucliers) : « n.m. »
(non mesuré par ce scénario) ; vérifications dédiées ci-dessous. Grenades à retardement : elles rebondissent sur la cible ;
dégâts constatés, « n.m. » si elles explosent hors de portée. Lance-missiles à kit de guidage (Haytal, Leviathan, Phoenix) sans
guidage : tir droit ; salve dispersée dont une partie touche. Les coups sont comptés au départ (munitions consommées ou coups
tirés par le soldat : les armes à charge remettent la munition puis tirent).

## Vérifications propres à certaines armes

| Nature | Ce qui est vérifié |
|---|---|
| Lance-missiles (Ranger, Fencer) | pas de tir sans cible verrouillée ; verrouillage dans le rectangle du HUD en « Lock-On Time » ; un missile par cible ; trajectoire d'EDF 5 (poussée, montée / piqué, accélération, guidage) ; dégâts à l'impact |
| Armes à charge (Wing Diver) | charge payée en énergie, tir au relâchement ; puissance selon la charge |
| Rafales chargées (Wing Diver) | le chargeur se remplit en tenant, se vide au relâchement à la cadence |
| Canons rotatifs (Fencer) | délai de mise en rotation avant le 1er coup |
| Frappes (Fencer) | niveaux de charge, dégâts du niveau, parade pendant la frappe |
| Lances (Fencer) | attaques 1 et 2 enchaînées |
| Disruptor | rafale continue, pas de rechargement |
| Frappes aériennes (Air Raider) | délai d'arrivée, nombre de coups (« Shots »), zone (« Attack Range »), rechargement en points |
| Bombes collantes | collées à la cible, mise à feu au rechargement / tir à vide, détecteurs automatiques |
| Tourelles, robots | dégâts infligés seuls, munitions (« Loaded Ammo ») |

## Lancer le plan

```sh
# Un journal par classe (0 Fusilier, 1 Commando aéroporté, 2 Opérateur d'appui, 3 Grenadier blindé), sans fenêtre :
powershell -File tools/hidden_run.ps1 -Exe <godot console> -Args "--headless --max-fps 10 --path . -- --autostart=0 --bot --god --class=0 --weapon-audit=all --quit-after=7000" -Log build/audit_0.log
# Rapport (PASS / ÉCHEC par mesure et par arme) :
uv run python tools/weapon_audit.py build/audit_0.log build/audit_1.log build/audit_2.log build/audit_3.log --out docs/AUDIT_ARMES.md --csv build/audit.csv
```

`--weapon-audit=<id,id,…>`, `=<famille>`, `=<type>` ou `=<nature>` restreint le plan ; `--aim` fait tirer le bot en visée.
Résultats : [AUDIT_ARMES.md](AUDIT_ARMES.md).

## Valeurs de référence

Les valeurs attendues viennent des tables d'armes d'EDF 5 (`scripts/Data/Edf5/Edf5Weapons.*.cs`, générées depuis l'atwiki
EDF 5). Recoupement avec le wiki anglais d'EDF 5 (Weapons EDF5, Automatic Artillery, Limpet Guns) :

| Arme | Wiki EDF 5 | Tables du jeu |
|---|---|---|
| MLRA | 16 missiles, 12 coups/s, 34 dégâts, rechargement 1 s, souffle 2 m, verrouillage 329 m / 16 cibles en 0,068 s | identiques |
| FG7 Hand Gatling | 320 coups, 20 coups/s, 17 → 8,5 dégâts, rechargement 8 s, portée 208 m, 240 m/s, mise en rotation 1 s | identiques |
| Splendor Shot | 13,6 × 15 × 8 (15 bombes de 8 éclats), éclats en demi-sphère vers l'extérieur | identiques (corrigé en v0.16.2) |
| Goliath D1 | 1 roquette, 328,4 dégâts (★8), rechargement 3 s (★5), souffle 15,9 m (★8), zoom ×3 | identiques |
| Mag Blaster | 1132 coups (★8), 60 coups/s, 3,4 dégâts (★8), recharge 2 s (★5), portée 180 m (★5), énergie 39 (★8) ; puissance qui baisse quand le chargeur se vide | identiques |
| Limpet Gun | 8 bombes, 3,5 coups/s, 34 dégâts, rechargement 1,5 s, souffle 4,4 m, portée 1091,5 m | identiques |
| Lysander Z | 11 coups, 0,28 coup/s, ≈ 6795 dégâts, rechargement 3,5 s, portée 1462,5 m | identiques (6794,7) |
| Rapier | 566 coups (★8), 60 coups/s, 1,1 × 6 dégâts, recharge 0,5 s, portée 30 m (★5) | identiques |
| Blasthole Spear | 8 coups, rechargement 2 s ; attaque 1 : 543,2 / 50 m, attaque 2 : 679 / 60 m | identiques (679 × 0,8 = 543,2) |
| ZE-GUN | 2 tourelles, 11,3 dégâts, rechargement 29,4 s | identiques |

Dix armes des quatre classes recoupées, toutes identiques (étoiles comprises) ; seule la mécanique du Splendor Shot différait, corrigée en v0.16.2.
