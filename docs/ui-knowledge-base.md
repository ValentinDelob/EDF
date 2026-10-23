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

### 1.b Sources ajoutées (Analyse 2, 91 captures)

Les fichiers sont dans `docs/ui-refs/` (toujours git-ignorés). Les sites marqués « guide » sont des articles de presse ; les images y sont plus petites (1024–1200 px) mais ce sont les seules captures de menus trouvées pour Helldivers 2, Space Marine 2 et Darktide.

| Jeu | Écran | Fichier local | URL |
|---|---|---|---|
| **— Sélection d'arme / armurerie / inspection / comparaison (29)** | | | |
| CoD Black Ops 4 | Créer une classe (slots + pick-10) | bo4-create-class.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-black-ops-4/call-of-duty-black-ops-4-create-class.jpg |
| CoD MW3 (2023) | Gunsmith (arme 3D, nœuds d'accessoires, barres avec delta) | mw3-gunsmith.jpg | https://staticg.sportskeeda.com/editor/2023/10/c597d-16962715261675-1920.jpg |
| Space Marine 2 | Armurerie — arbre de perks, stats avec delta ▲▼ | sm2-armouring-perk-tree.jpg | https://shacknews-www.s3.amazonaws.com/assets/editorial/2024/09/melta-rifle-perk-tree-in-space-marine-2.jpg |
| Space Marine 2 | Armurerie — vue d'ensemble | sm2-armouring-hall.jpg | https://shacknews-www.s3.amazonaws.com/assets/editorial/2024/09/the-armouring-hall-screen-in-space-marine-2.jpg |
| Space Marine 2 | Versions / paliers de rareté (Standard → Relic) | sm2-weapon-versions-tiers.jpg | https://www.destructoid.com/wp-content/uploads/2024/09/Space-Marine-2-upgrading-artificer-weapons.jpg |
| Helldivers 2 | Weaponry (liste par catégorie + stats + Equip) | helldivers2-weaponry.jpg | https://www.destructoid.com/wp-content/uploads/2024/02/How-to-change-your-default-loadout-in-Helldivers-2-2.jpg |
| Helldivers 2 | Armory — 3 cartes de slot | helldivers2-armory-categories.jpg | https://www.destructoid.com/wp-content/uploads/2024/02/How-to-change-your-default-loadout-in-Helldivers-2-3.jpg |
| Darktide | Carte de stats d'arme (rating, rareté, barres) | darktide-weapon-stats-card.jpg | https://primagames.com/wp-content/uploads/2022/11/Warhammer-40K-Darktide-Weapon-Stats.jpg |
| Darktide | Loadout (slots arme + rating) | darktide-loadout.jpg | https://gamerjournalist.com/wp-content/uploads/2022/12/loadout-in-darktide.jpg |
| Aliens: Fireteam Elite | Armes — inspection, étoiles, combat rating, jauges | aliensfe-weapons-inspect.jpg | https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1549970/ss_f271d11ec630b3c8fe11f9f196842718e1145f9c.1920x1080.jpg |
| The Division 2 | Inventaire armes — comparaison chiffrée avec delta | division2-weapon-compare.jpg | https://cdn.dtmg.xyz/uploads/2019/03/division-2-builds-and-specializations-guide-10.jpg |
| The Division 2 | Weapon modding | division2-weapon-modding.jpg | https://cdn.dtmg.xyz/uploads/2019/03/division-2-builds-and-specializations-guide-14.jpg |
| Destiny 2 | Inspection d'arme (perks, puissance, barres) | destiny2-weapon-inspect.jpg | https://www.highgroundgaming.com/wp-content/uploads/2021/06/Masterwork-1024x576.jpg |
| Insurgency: Sandstorm | Loadout — liste + accessoires + stats chiffrées | insurgency-loadout-weapon-stats.jpg | https://interfaceingame.com/wp-content/uploads/insurgency-sandstorm/insurgency-sandstorm-loadout.jpg |
| Insurgency: Sandstorm | Loadout de classe (slots, presets) | insurgency-class-loadout.jpg | https://interfaceingame.com/wp-content/uploads/insurgency-sandstorm/insurgency-sandstorm-class-inventory.jpg |
| Insurgency: Sandstorm | Équipement (déblocage) | insurgency-equipment.jpg | https://interfaceingame.com/wp-content/uploads/insurgency-sandstorm/insurgency-sandstorm-equipment.jpg |
| Killing Floor 2 | Marchand entre vagues (achat / amélioration) | kf2-trader-weapon-upgrade.jpg | https://interfaceingame.com/wp-content/uploads/killing-floor-2/killing-floor-2-upgrade-gear.jpg |
| R6 Extraction | Loadout (étapes Opérateur › Loadout › Prêt) | r6x-loadout.png | https://interfaceingame.com/wp-content/uploads/rainbow-six-siege-extraction/rainbow-six-siege-extraction-loadout.png |
| Star Wars Zero Company | Armurerie — liste d'armes + rendu 3D | swzero-armory-weapon-list.png | https://interfaceingame.com/wp-content/uploads/star-wars-zero-company/star-wars-zero-company-armory.png |
| The Ascent | Loadout — tableau de stats | ascent-loadout-stats-table.png | https://interfaceingame.com/wp-content/uploads/the-ascent/the-ascent-loadout.png |
| Warframe | Arsenal (slots + perso sur socle) | warframe-arsenal-loadout.jpg | https://interfaceingame.com/wp-content/uploads/warframe/warframe-inventory.jpg |
| Warframe | Collection d'armes (tuiles + rang) | warframe-weapon-collection.jpg | https://interfaceingame.com/wp-content/uploads/warframe/warframe-equipment-inspection.jpg |
| Warframe | Détail d'objet | warframe-item-details.jpg | https://interfaceingame.com/wp-content/uploads/warframe/warframe-item-details.jpg |
| XDefiant | Édition des loadouts | xdefiant-loadouts.png | https://interfaceingame.com/wp-content/uploads/xdefiant-beta/xdefiant-beta-loadouts.png |
| Ghost Recon Breakpoint | Loadout (armes + gear score) | ghostrecon-bp-loadout.jpg | https://interfaceingame.com/wp-content/uploads/tom-clancys-ghost-recon-breakpoint/tom-clancys-ghost-recon-breakpoint-loadout.jpg |
| Metro Exodus | Établi diégétique | metro-exodus-workbench.png | https://interfaceingame.com/wp-content/uploads/metro-exodus/metro-exodus-weapon-cleaning.png |
| DOOM: The Dark Ages | Arsenal (améliorations) | doomtda-arsenal.png | https://interfaceingame.com/wp-content/uploads/doom-the-dark-ages/doom-the-dark-ages-arsenal.png |
| DOOM: The Dark Ages | Inspection 3D plein écran | doomtda-weapon-inspect-3d.png | https://interfaceingame.com/wp-content/uploads/doom-the-dark-ages/doom-the-dark-ages-3d-model.png |
| Helldivers (1) | Loadout 4 joueurs côte à côte | helldivers1-loadout-4players.png | https://interfaceingame.com/wp-content/uploads/helldivers/helldivers-loadout.png |
| **— Inventaire / grille / rareté (8)** | | | |
| Killing Floor 2 | Inventaire, grille à liseré de rareté + filtres | kf2-inventory-rarity-grid.jpg | https://interfaceingame.com/wp-content/uploads/killing-floor-2/killing-floor-2-inventory.jpg |
| Remnant II | Personnage — slots en arc autour du perso | remnant2-character-equipment.png | https://interfaceingame.com/wp-content/uploads/remnant-ii/remnant-ii-inventory.png |
| Remnant II | Grille d'inventaire par catégories | remnant2-inventory-grid.png | https://interfaceingame.com/wp-content/uploads/remnant-ii/remnant-ii-inventory-2.png |
| Vermintide 2 | Équipement — grille colorée par rareté | vermintide2-equipment-rarity.jpg | https://interfaceingame.com/wp-content/uploads/warhammer-vermintide-2/warhammer-vermintide-2-equipment.jpg |
| S.T.A.L.K.E.R. 2 | Butin / équipement / inventaire | stalker2-inventory.png | https://interfaceingame.com/wp-content/uploads/s-t-a-l-k-e-r-2-heart-of-chornobyl/s-t-a-l-k-e-r-2-heart-of-chornobyl-inventory.png |
| Generation Zero | Inventaire + carte d'arme (état, barres) | genzero-inventory-weapon-card.jpg | https://interfaceingame.com/wp-content/uploads/generation-zero/generation-zero-inventory.jpg |
| Cronos: The New Dawn | Inventaire style terminal | cronos-inventory-terminal.png | https://interfaceingame.com/wp-content/uploads/cronos-the-new-dawn/cronos-the-new-dawn-inventory.png |
| Aliens: Fireteam Elite | Inventaire (cartes colorées par rareté) | aliensfe-inventory-cards.jpg | https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1549970/ss_79561b151ad9f129f9ece9d7a9c7158a516a3e96.1920x1080.jpg |
| **— Butin / révélation de récompense (7)** | | | |
| Vermintide 2 | Révélation de coffre (cartes) | vermintide2-chest-reveal.jpg | https://interfaceingame.com/wp-content/uploads/warhammer-vermintide-2/warhammer-vermintide-2-chest-loot.jpg |
| Vermintide 2 | Butin de mission → niveau de coffre | vermintide2-items-collected.jpg | https://interfaceingame.com/wp-content/uploads/warhammer-vermintide-2/warhammer-vermintide-2-items-collected.jpg |
| CoD Black Ops 4 | Révélation (carte à cadre de rareté) | bo4-reward-reveal.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-black-ops-4/call-of-duty-black-ops-4-rewards.jpg |
| CoD Black Ops 4 | Piste de récompenses de niveau | bo4-level-rewards.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-black-ops-4/call-of-duty-black-ops-4-level-rewards.jpg |
| Apex Legends | Ouverture de pack (3 objets, rareté) | apex-packs-reveal.jpg | https://interfaceingame.com/wp-content/uploads/apex-legends/apex-legends-apex-packs.jpg |
| Apex Legends | Déblocage d'arme (héros centré) | apex-weapon-unlock.jpg | https://interfaceingame.com/wp-content/uploads/apex-legends/apex-legends-weapon-unlock.jpg |
| Helldivers (1) | Récompense de mission par joueur | helldivers1-mission-reward.png | https://interfaceingame.com/wp-content/uploads/helldivers/helldivers-event-mission-reward.png |
| **— HUD en jeu (14)** | | | |
| Remnant II | HUD minimal (vie centrée, armes bas-droite) | remnant2-hud.png | https://interfaceingame.com/wp-content/uploads/remnant-ii/remnant-ii-hud.png |
| Remnant II | HUD (variante) | remnant2-hud-2.png | https://interfaceingame.com/wp-content/uploads/remnant-ii/remnant-ii-hud-2.png |
| Aliens: Fireteam Elite | HUD coop (escouade haut-gauche, radar arc) | aliensfe-hud.jpg | https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1549970/ss_b0cb31b2977312de10720ddf052e0a8a74a85d6a.1920x1080.jpg |
| The Division 2 | HUD + chiffres de dégâts / DPS | division2-hud-damage-numbers.jpg | https://cdn.dtmg.xyz/uploads/2019/03/division-2-builds-and-specializations-guide-12.jpg |
| Halo Infinite | HUD (objectif, bouclier, munitions) | halo-infinite-hud.png | https://interfaceingame.com/wp-content/uploads/halo-infinite/halo-infinite-hud-2.png |
| Halo Infinite | HUD + toast de ramassage | halo-infinite-hud-pickup.png | https://interfaceingame.com/wp-content/uploads/halo-infinite/halo-infinite-hud-3.png |
| Borderlands 4 | HUD (bouclier/vie, bannière mission) | borderlands4-hud.png | https://interfaceingame.com/wp-content/uploads/borderlands-4/borderlands-4-hud.png |
| Gears of War: E-Day | HUD quasi absent + carte de chapitre | gears-eday-hud.png | https://interfaceingame.com/wp-content/uploads/gears-of-war-e-day/gears-of-war-e-day-hud.png |
| The Finals | HUD (variante) | thefinals-hud-2.png | https://interfaceingame.com/wp-content/uploads/the-finals/the-finals-hud-2.png |
| S.T.A.L.K.E.R. 2 | HUD (quête, boussole, munitions) | stalker2-hud.png | https://interfaceingame.com/wp-content/uploads/s-t-a-l-k-e-r-2-heart-of-chornobyl/s-t-a-l-k-e-r-2-heart-of-chornobyl-hud.png |
| Marvel Rivals | HUD (vie centrée segmentée) | marvelrivals-hud.png | https://interfaceingame.com/wp-content/uploads/marvel-rivals/marvel-rivals-hud.png |
| Apex Legends | Ramassage d'arme in-world + comparaison | apex-hud-pickup-compare.jpg | https://interfaceingame.com/wp-content/uploads/apex-legends/apex-legends-replace-weapon.jpg |
| Cronos: The New Dawn | Sélection rapide d'arme (4 slots) | cronos-hud-weapon-quickselect.png | https://interfaceingame.com/wp-content/uploads/cronos-the-new-dawn/cronos-the-new-dawn-weapon-selection.png |
| Hunt: Showdown | Bannière d'annonce (« Get Ready ») | hunt-announcement-banner.jpg | https://interfaceingame.com/wp-content/uploads/hunt-showdown/hunt-showdown-get-ready.jpg |
| **— Menu principal (6)** | | | |
| DOOM: The Dark Ages | Menu principal | doomtda-main-menu.png | https://interfaceingame.com/wp-content/uploads/doom-the-dark-ages/doom-the-dark-ages-main-menu.png |
| R6 Extraction | Menu principal | r6x-main-menu.png | https://interfaceingame.com/wp-content/uploads/rainbow-six-siege-extraction/rainbow-six-siege-extraction-main-menu.png |
| Vermintide 2 | Menu principal (héros 3D) | vermintide2-main-menu.jpg | https://interfaceingame.com/wp-content/uploads/warhammer-vermintide-2/warhammer-vermintide-2-main-menu.jpg |
| Helldivers (1) | Menu principal | helldivers1-main-menu.png | https://interfaceingame.com/wp-content/uploads/helldivers/helldivers-main-menu.png |
| Cronos: The New Dawn | Menu principal (terminal) | cronos-main-menu.png | https://interfaceingame.com/wp-content/uploads/cronos-the-new-dawn/cronos-the-new-dawn-main-menu.png |
| XDefiant | Menu principal | xdefiant-main-menu.png | https://interfaceingame.com/wp-content/uploads/xdefiant-beta/xdefiant-beta-main-menu.png |
| **— Sélection de mission / difficulté (7)** | | | |
| Helldivers (1) | Briefing de mission | helldivers1-mission-briefing.png | https://interfaceingame.com/wp-content/uploads/helldivers/helldivers-mission-briefing.png |
| World War Z | Sélection d'épisode | wwz-select-episode.jpg | https://interfaceingame.com/wp-content/uploads/world-war-z/world-war-z-select-episode.jpg |
| Deep Rock Galactic | Carte holo des régions | drg-select-region.jpg | https://interfaceingame.com/wp-content/uploads/deep-rock-galactic/deep-rock-galactic-select-region.jpg |
| Ghost Recon Breakpoint | Missions (tableau de liège) | ghostrecon-bp-missions.jpg | https://interfaceingame.com/wp-content/uploads/tom-clancys-ghost-recon-breakpoint/tom-clancys-ghost-recon-breakpoint-missions.jpg |
| Warframe | Carte des missions (nœuds) | warframe-star-chart.jpg | https://interfaceingame.com/wp-content/uploads/warframe/warframe-level-selection.jpg |
| Halo Infinite | Difficulté (liste + aperçu) | halo-infinite-difficulty.png | https://interfaceingame.com/wp-content/uploads/halo-infinite/halo-infinite-difficulty-select.png |
| Gears of War: E-Day | Difficulté (4 cartes) + prêt | gears-eday-difficulty.png | https://interfaceingame.com/wp-content/uploads/gears-of-war-e-day/gears-of-war-e-day-difficulty-selection.png |
| **— Lobby escouade / prêt / classe (6)** | | | |
| Helldivers 2 | Loadout de mission + READY UP, 3 slots vides | helldivers2-loadout-readyup.jpg | https://www.destructoid.com/wp-content/uploads/2024/02/How-to-change-your-default-loadout-in-Helldivers-2-8.jpg |
| Helldivers (1) | Lobby diégétique 4 joueurs | helldivers1-lobby.png | https://interfaceingame.com/wp-content/uploads/helldivers/helldivers-lobby.png |
| Gears 5 | Lobby coop 3 colonnes | gears5-lobby.png | https://interfaceingame.com/wp-content/uploads/gears-5/gears-5-lobby.png |
| CoD Black Ops 4 | Lobby (groupe + DEPLOY) | bo4-lobby.jpg | https://interfaceingame.com/wp-content/uploads/call-of-duty-black-ops-4/call-of-duty-black-ops-4-lobby.jpg |
| Ghost Recon Breakpoint | Lobby 4 slots | ghostrecon-bp-lobby.jpg | https://interfaceingame.com/wp-content/uploads/tom-clancys-ghost-recon-breakpoint/tom-clancys-ghost-recon-breakpoint-lobby.jpg |
| Marvel Rivals | Sélection de héros (compte à rebours) | marvelrivals-hero-select.png | https://interfaceingame.com/wp-content/uploads/marvel-rivals/marvel-rivals-hero-selection.png |
| **— Résultats de mission (5)** | | | |
| R6 Extraction | Extraction réussie + XP par joueur | r6x-extraction-results.png | https://interfaceingame.com/wp-content/uploads/rainbow-six-siege-extraction/rainbow-six-siege-extraction-stats.png |
| Hunt: Showdown | Résumé de mission | hunt-mission-summary.jpg | https://interfaceingame.com/wp-content/uploads/hunt-showdown/hunt-showdown-mission-summary.jpg |
| Insurgency: Sandstorm | Résumé (VICTOIRE + stats) | insurgency-match-summary.jpg | https://interfaceingame.com/wp-content/uploads/insurgency-sandstorm/insurgency-sandstorm-match-summary.jpg |
| The Finals | Tableau de scores | thefinals-summary.png | https://interfaceingame.com/wp-content/uploads/the-finals/the-finals-summary-2.png |
| Deep Rock Galactic | Mission accomplie (paie) | drg-mission-complete.jpg | https://interfaceingame.com/wp-content/uploads/deep-rock-galactic/deep-rock-galactic-assignment-mission-complete.jpg |
| **— Pause (5)** | | | |
| Insurgency: Sandstorm | Pause à onglets + infos mission | insurgency-pause.jpg | https://interfaceingame.com/wp-content/uploads/insurgency-sandstorm/insurgency-sandstorm-paused.jpg |
| S.T.A.L.K.E.R. 2 | Pause | stalker2-pause.png | https://interfaceingame.com/wp-content/uploads/s-t-a-l-k-e-r-2-heart-of-chornobyl/s-t-a-l-k-e-r-2-heart-of-chornobyl-pause.png |
| XDefiant | Menu en jeu | xdefiant-ingame-menu.png | https://interfaceingame.com/wp-content/uploads/xdefiant-beta/xdefiant-beta-in-game-menu.png |
| The Finals | Pause | thefinals-pause.png | https://interfaceingame.com/wp-content/uploads/the-finals/the-finals-pause.png |
| Hunt: Showdown | Pause + équipement | hunt-pause-equipment.jpg | https://interfaceingame.com/wp-content/uploads/hunt-showdown/hunt-showdown-menu.jpg |
| **— Options / touches (4)** | | | |
| DOOM: The Dark Ages | Contrôles | doomtda-controls.png | https://interfaceingame.com/wp-content/uploads/doom-the-dark-ages/doom-the-dark-ages-controls.png |
| Star Wars Zero Company | Contrôles (onglets LB/RB, LT/RT) | swzero-controls.png | https://interfaceingame.com/wp-content/uploads/star-wars-zero-company/star-wars-zero-company-controls.png |
| Helldivers (1) | Réassignation des touches | helldivers1-keybindings.png | https://interfaceingame.com/wp-content/uploads/helldivers/helldivers-keybindings.png |
| Hunt: Showdown | Options HUD (masquage auto par élément) | hunt-hud-options.jpg | https://interfaceingame.com/wp-content/uploads/hunt-showdown/hunt-showdown-hud.jpg |

Non trouvés (Analyse 2) : captures de menus Starship Troopers: Extermination, Back 4 Blood, Battlefield 2042, EDF 6 (hors existantes). Leurs captures Steam ne montrent que du gameplay sans UI. Gameuidatabase.com et helldivers.wiki.gg bloquent le téléchargement automatique (Cloudflare / 403). Pour Helldivers 2 et Darktide, les seules captures de menus viennent d'articles-guides, en 1200 px. Pour Space Marine 2, elles viennent aussi d'articles-guides (1200–1920 px).

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

---

## 7. Analyse 2 — tendances modernes

Observations faites en regardant les 91 nouvelles captures (section 1.b). Elles complètent les sections 2 à 6 et, en cas de conflit, priment pour la refonte. La priorité est l'écran de **sélection d'arme**, jugé « horrible » aujourd'hui.

### 7.1 Sélection d'arme / armurerie : ce que font les jeux modernes

**Composition dominante (≈ 80 % des captures) : 3 zones, toujours dans le même ordre.**
1. **Gauche (25–30 % de la largeur) : liste ou grille de choix.** Chaque ligne est une *carte d'arme* : rendu ou silhouette de l'arme (≈ 2:1), nom, et 1 à 3 méta-données (niveau, ★, score, coût). Exemples : Helldivers 2 `helldivers2-weaponry`, Star Wars Zero Company `swzero-armory-weapon-list`, Insurgency `insurgency-loadout-weapon-stats`, XDefiant, Ghost Recon.
2. **Centre / droite (≈ 45 %) : l'arme focus en grand,** en 3D, sur fond flouté ou sombre, légèrement en perspective. Elle n'est jamais dans un cadre. Variantes : établi diégétique (Metro, Insurgency, CoD), fond studio uni (SM2, MW3, Aliens FE).
3. **Bloc de stats ancré sous ou à côté de l'arme :** nom en grand, puis type et rareté sur une ligne secondaire, puis stats.
- Navigation : **onglets de slot en haut** (`PRIMARY · SECONDARY · GRENADE` dans HD2, `RIFLE SLOT · HEAVY SLOT` dans Aliens FE). Raccourcis `Q/E` ou `L1/R1` pour les slots et `L2/R2` pour les sous-catégories. Dans Space Marine 2, les **silhouettes d'armes servent d'onglets**, avec Q/E de part et d'autre.
- **Action primaire unique** dans un bouton plein, sur la couleur d'accent : `EQUIP` (HD2, en bas à droite, contour jaune), `CONFIRM LOADOUT` (R6X, en haut à gauche), `ACCEPT` (Insurgency).
- Barre d'aide en bas : `Retour · Pivoter · Comparer · Équiper` (HD2), `Back · Viewer mode · Rotate` (Aliens FE), `Preview · Reset` (SM2).

**Comment les stats sont présentées.** Il existe 4 patterns. Rempart devrait combiner 1 et 2.
1. **Libellé + valeur + barre fine + delta coloré** (SM2 `sm2-armouring-perk-tree`, MW3 `mw3-gunsmith`, Insurgency).
   - Disposition : libellé à gauche en gris, valeur en blanc tabulaire, barre de 3–4 px sur 180–220 px, puis delta `▲ 2` vert ou `▼ 1` rouge **à droite de la barre**.
   - Barre de MW3 : la portion gagnée s'affiche en **vert au bout de la barre**, la portion perdue en **rouge sur la piste**. C'est le pattern le plus lisible pour comparer.
   - Pour les stats sans échelle (chargeur, réserve), SM2 n'affiche que le nombre et son delta, sans barre.
2. **Gros chiffres « héros » pour 3 stats clés + delta en dessous** (Division 2 `division2-weapon-compare`).
   - Exemple : `DMG 2.6k / -9741`, `RPM 900 / +720`, `MAG 50 / +40`, à environ 56 px.
   - Les barres sont réservées aux stats secondaires (précision, stabilité), avec un segment rouge ou vert sur la différence.
   - La **courbe de chute de dégâts** se compare en superposant 2 lignes (blanc = équipée, orange = focus).
   - La liste elle-même montre des **mini-flèches ▲▼** à côté de chaque valeur (`4.2k ▼ 605 ▲`) : on compare sans ouvrir l'arme.
3. **Mini-barres en tirets + valeur à 3 chiffres** (HD2 : `DAMAGE ▮▮▮▮▮▮ 112`, `CAPACITY 020`).
   - Les zéros devant (`020`) ont un rendu « terminal militaire » ; 4 stats seulement.
   - Arrowhead a retiré une page plus détaillée, jugée trop complexe. Rempart garde donc 4 à 6 stats visibles et met le détail dans une vue « Inspecter ».
4. **Jauges circulaires + tableau 2 colonnes** (Aliens FE : `ACCURACY 77`, `STABILITY 86` en anneaux, `COMBAT RATING 100`, puis un tableau `Damage 135 / Reload 1.90…`).
   - Le « combat rating » unique, comme le rating de Darktide (`143`) ou la puissance de Destiny (`1310`), résume la valeur en un nombre. Pour Rempart, `Lv` + ★ jouent ce rôle.

**Comment la rareté est montrée.**
- **Couleur sur le texte du palier, pas sur le panneau.** SM2 : `Melta Rifle / Artificer` en violet, `Master-Crafted` en vert, `Relic` en orange. Darktide : nom de l'arme en vert pour la rareté « Black Market ».
- **Liseré ou cadre de tuile.**
  - Killing Floor 2 : contour coloré de 2–3 px autour de l'icône (jaune, bleu, vert). Sélection = fond orange plein.
  - Vermintide 2 : fond de tuile teinté de la rareté.
  - Division 2 : fond de ligne violet pour « Superior », orange plein pour « High-End » ; lignes très saturées, à éviter telles quelles.
- **Piste de paliers horizontale** (SM2 `sm2-weapon-versions-tiers`) : `STANDARD · MASTER-CRAFTED · ARTIFICER · RELIC` avec un seuil d'XP au-dessus de chaque segment. Les variantes verrouillées affichent un cadenas, avec une infobulle « il vous faut… ».
  - **Ce pattern s'applique directement à Lv/★ de Rempart :** la piste montre les étoiles de l'arme et la prochaine étoile à débloquer.
- **Étoiles en ligne à droite du nom** (Aliens FE `★★★★` + `MAX RANK`). Chez KF2, une étoile montre le palier d'amélioration.

**Comment le loadout est présenté.**
- **Personnage 3D central + colonnes de slots de chaque côté**, avec le perso qui tient l'arme focus : Insurgency, Ghost Recon, Warframe, R6X, Remnant II (slots en arc autour du perso), The Ascent.
  - Les slots d'armes ont un rendu large ; les slots d'équipement sont carrés.
  - Slot vide : `+` en filigrane ou `[Empty]` (XDefiant). Slot verrouillé : cadenas + condition (« Débloqué au niveau 8 », Darktide).
- **Loadout de mission coop** (Helldivers 1 `helldivers1-loadout-4players`, HD2 `helldivers2-loadout-readyup`) :
  - 1 carte verticale par joueur : nom + grade, slots arme/équipement en icônes, bouton `CONFIRM` ou `READY UP` par carte.
  - En-tête de mission en haut : environnement, ennemi, difficulté en barre colorée.
  - Les slots non occupés affichent `EMPTY SLOT` hachuré.
  - **C'est exactement le besoin d'un lobby coop 1–4 joueurs.**

### 7.2 Inventaire / grille

- **Tuiles carrées de 96–128 px à 1080p**, espacées de 8–12 px, avec compteur `x3` en bas à droite. Badge `!` pour « nouveau » (Remnant II, Division 2).
- **Filtres** en liste à gauche ou en menus déroulants (rareté, perk, type) ; **tri** en menu déroulant à droite (Warframe `SORT BY`).
- La rareté passe par le **liseré de 2–3 px** (KF2) ou un **fond teinté sombre** (Aliens FE cartes, Vermintide). Le focus utilise **une autre couleur que la rareté** : orange plein (KF2) ou contour blanc (Remnant).
- **Infobulle-carte au survol** (Remnant II) :
  - Contenu : nom, type, rendu, 2 gros chiffres (`Armor 9 / Weight 5`), résistances en icônes + valeurs, texte d'ambiance en italique.
  - Pied : actions avec touches `Sélectionner · Retirer · Inspecter`.
- **Remnant II ou STALKER 2 conviennent au ton « gris en ruine »** : charbon `#141414`, grilles à peine visibles (blanc à 4 %), aucune couleur sauf la vie (rouge) et le poids (vert). Le risque est le manque de contraste des cases vides.

### 7.3 Révélation de butin / caisses

- **Butin de mission puis coffre** (Vermintide 2 `vermintide2-items-collected`) :
  - À gauche, liste des contributions : mission accomplie ✓, bonus, `tome x2`. Les contributions absentes sont grisées.
  - Une barre de progression mène au **palier de coffre**. Le coffre est posé sur un socle dans un décor brumeux.
  - **Modèle parfait pour les caisses ramassées pendant la mission.**
- **Révélation en cartes alignées** : 2–3 objets côte à côte, le central plus grand ; puce de rareté au-dessus du nom (`RARE` bleu, `COMMON` gris) (Apex `apex-packs-reveal`, Vermintide `vermintide2-chest-reveal`). Les objets pas encore ouverts gardent la forme de la caisse.
- **Carte héros pleine hauteur** au cadre coloré par la rareté, avec la grille des autres gains à gauche (BO4 `bo4-reward-reveal`).
- Coop : **1 colonne par joueur** avec sa récompense et un compte à rebours `CONTINUER : 19` (Helldivers 1). Les 4 joueurs voient le même écran.

### 7.4 Menus principal / mission / lobby / résultats / pause / options

- **Menu principal**
  - Logo en haut à gauche, liste de 5–7 entrées en texte à gauche, séparées par des filets de 1 px. Entrée focus : **barre pleine** (orange chez DOOM TDA, blanc texte noir chez Cronos, coup de pinceau blanc chez STALKER 2).
  - Grand visuel à droite. Version et état réseau en bas à gauche.
- **Mission**
  - World War Z `wwz-select-episode` : titre géant en haut à gauche, liste à gauche avec barre rouge sur le focus, globe 3D au centre, image + description à droite, difficulté en **rangée de crânes** modifiable avec `R`. C'est la meilleure référence.
  - Gears E-Day : 4 **cartes de difficulté** horizontales (emblème, nom, description) ; sélection = bord supérieur coloré + coche, avec `0/1 PRÊT` au-dessus.
  - Halo Infinite : fil d'Ariane `CAMPAGNE > NOUVELLE PARTIE`, liste à filets, gros bouton `JOUER` cyan.
  - Deep Rock Galactic : carte holographique diégétique.
- **Lobby**
  - Gears 5 : 3 colonnes de joueurs, avec `+ INVITER` sur les vides.
  - BO4 : `GROUPE 1/4` en haut à droite + bouton `DEPLOY` orange en bas à droite.
  - R6X : progression `OPÉRATEUR › LOADOUT › PRÊT` avec compte à rebours `87`. **À reprendre pour Rempart : Classe › Loadout › Prêt.**
- **Résultats**
  - R6X : grande tuile jaune avec coche + `EXTRACTION RÉUSSIE`, puis 3 opérateurs 3D, chacun avec sa **barre d'XP verte et ses flèches**.
  - Insurgency : bandeau sombre centré `VICTOIRE !`, rangée de stats `valeur / moyenne de carrière`.
  - DRG : bande orange `TERMINÉ` + icônes de paie, bouton `CONTINUER` jaune.
- **Pause**
  - Insurgency : onglets `JEU · CLASSE · LOADOUT` avec Q/E, liste condensée à gauche (focus = aplat crème), panneau mission à droite bordé d'un filet rouge.
  - XDefiant : liste de boutons plats, focus blanc + barre jaune à gauche.
  - Pour Rempart, permettre **de changer de loadout depuis la pause** en coop (Insurgency).
- **Options**
  - Toujours : onglets icône + libellé avec Q/E, sous-onglets avec LT/RT (SW Zero Company), en-têtes de section en bande, ligne focus en aplat ou contour, sliders avec valeur numérique, **panneau de description à droite**.
  - Hunt propose un **masquage automatique par élément du HUD**.

### 7.5 HUD

- **Densité : la tendance est au « presque rien ».**
  - Remnant II (le plus proche du ton de Rempart) : une barre de vie rouge fine + endurance **centrées en bas** (≈ 420 px), deux tuiles d'arme empilées en bas à droite (active plus grande, rendu + `chargeur / réserve`), minicarte carrée en haut à droite. Rien d'autre.
  - Gears E-Day : objectif en une ligne en haut à gauche, puis rien.
- **Munitions**
  - Toujours en **bas à droite**, au format `36 | 108` ou `44 / 537` : chargeur en grand (≈ 40 px), réserve en petit et gris.
  - Silhouette blanche de l'arme active ; arme secondaire en plus petit en dessous (Aliens FE, Halo).
  - Variante Division 2 : munitions **près du réticule** (bas-centre-droit) avec barre segmentée de vie/armure au-dessus.
- **Escouade**
  - En haut à gauche chez Aliens FE (icône de classe + nom + barre fine + compteur de kits) ; en bas à gauche chez Apex et The Finals.
  - Toujours : **icône de classe + nom + barre fine** de 160–280 px.
- **Objectif**
  - En haut à gauche ou en haut à droite, sur 1–2 lignes : titre de lieu en petites capitales + objectif. Le sous-objectif a une puce radio (STALKER 2) ou un fond translucide (Halo).
  - Annonces de phase en **bannière centrée** (`MISSION PRINCIPALE TERMINÉE` dans Borderlands 4, `Get Ready` sur coup de pinceau noir dans Hunt, carte de chapitre en lettres espacées dans Gears).
- **Radar** : demi-cercle ou arc en bas à droite (Aliens FE, avec distances `22.4m`) ou cercle en haut à gauche (Division 2). Cible : 180–220 px.
- **Toasts de ramassage**
  - À droite, une ligne `OBJET ACQUIS` + `[Tab]` (Halo).
  - Apex : **carte de comparaison ancrée près du réticule** pour remplacer une arme : `MAINTENIR E REMPLACER`, nom, slots d'accessoires colorés par rareté. **À utiliser pour les caisses d'armes de Rempart** (comparer avant de prendre).
- **Chiffres de dégâts** : blancs, groupés, avec DPS facultatif (stand de tir Division 2). Hitmarker = 4 traits en coin autour du réticule (Division 2 orange, Aliens FE blanc).

### 7.6 Animation et retours (déduits des captures et conventions de ces jeux)

- **Focus**
  - Remplissage de barre de gauche à droite en 120–160 ms (DOOM TDA, XDefiant).
  - Coins en crochets qui se resserrent sur la tuile (BO4, Aliens FE `[01]`).
  - Effet de clic court ; pas de grossissement au-delà de 1,03×.
- **Changement de stat** : le segment delta apparaît en fondu (vert/rouge) et la valeur défile, ou « roule », en 200 ms. La flèche ▲▼ clignote une fois.
- **Révélation**
  - Pause → flash de la couleur de rareté (80 ms) → l'arme tourne une fois → les stats s'animent ligne par ligne (40 ms de décalage).
  - Les raretés hautes ajoutent des particules ou un halo (Apex, BO4).
  - On peut toujours **passer** l'animation (maintenir A/Espace).
- **Prêt coop** : la carte du joueur passe à la couleur d'accent avec une coche ; un compte à rebours démarre quand tous sont prêts (R6X `87`, HD1 `CONTINUE : 19`).

### 7.7 Trois directions de design pour Rempart

Les trois directions respectent les fondamentaux de la section 3 : un accent, action primaire en bas à droite, grille de 8 px, Q/E. Elles diffèrent par la matière, la typo et la place du diégétique.

#### A. « Terminal tactique militaire »
*Esprit : le soldat consulte un ordinateur de commandement EDF ou ONU ; froid, chiffré, discipliné.*
- **Exemples** : Helldivers 2 (Weaponry, chiffres `020`, contours fins), Aliens: Fireteam Elite (onglets en aplat lime, `// MODS`, `// PERKS`), R6 Extraction (progression d'étapes, rayures de danger), Cronos (monospace), Insurgency (stats avec unités `m/s`, `rpm`).
- **Palette**

  | Token | Hex | Usage |
  |---|---|---|
  | `bg` | `#0B0E0D` | Fond |
  | `panel` | `#121816` à 85 % | Panneaux |
  | `line` | `#2C3833` | Filets 1 px, réticules de coin |
  | `text` | `#DDE5DF` | Texte |
  | `text-dim` | `#7E8C85` | Texte secondaire |
  | **`accent`** | **`#D7E04A`** (jaune-lime signal) | Accent |
  | `danger` | `#E5483B` | Danger |
  | `ok` | `#59D17A` | Validation |

- **Typo** : titres en **Rajdhani / Barlow Condensed SemiBold en capitales**, tracking +6 %. Chiffres et étiquettes en **JetBrains Mono** ou **IBM Plex Mono** (`DMG 112`, `MAG 020`), zéros devant.
- **Signes distinctifs**
  - Préfixes `//` sur les sections, coordonnées et horodatage en très petit dans les coins, crochets `[ ]` sur le focus.
  - Barres en **tirets segmentés** (10 segments), hachures diagonales sur les slots vides ou verrouillés.
  - Une rayure de danger noir/jaune réservée au bouton `DÉPLOYER`.
- **Arme** : rendu studio sur fond sombre et quadrillé ; 4 stats en tirets + 3 « traits » textuels ; carte de comparaison en 2 colonnes `ÉQUIPÉE | SÉLECTION`.
- **Risque** : peut sembler « sci-fi propre » plutôt qu'apocalyptique. Il faut le salir avec du grain, de légères aberrations (sans glitch animé) et un fond de ville grise.

#### B. « AAA moderne épuré »
*Esprit : clarté maximale, l'arme et le soldat sont les héros, l'UI se retire.*
- **Exemples** : CoD MW3 Gunsmith, Space Marine 2 Armurerie (hors cadres dorés), Star Wars Zero Company, Halo Infinite, The Division 2 (chiffres héros), Destiny 2, Remnant II.
- **Palette** : celle de la section 3.4 telle quelle, avec un orange signal `#FF8A1F`.
  - Fond `#0E1114` ; panneaux en verre dépoli noir à 60 % avec flou d'arrière-plan.
  - Texte `#F2F4F5` / `#9AA4AD` ; deltas `#5BD16E` / `#E8413C`.
  - Raretés de 4.4 sur le **texte du palier** et un liseré de 3 px.
- **Typo** : **Barlow** (Regular/SemiBold) pour tout. Titres d'écran **très grands, fins, très espacés** comme SM2 (`P R I M A R Y  W E A P O N`) ou CoD ; chiffres tabulaires ; chiffres héros de 56–72 px pour 3 stats clés.
- **Signes distinctifs**
  - Aucun cadre, des filets de 1 px. L'arme en 3D occupe 50 % de l'écran sur un fond studio dégradé (gris bleuté → noir).
  - Barres fines de 3 px avec **delta vert ou rouge collé au bout** (MW3) et `▲ 2 / ▼ 1` à droite (SM2).
  - **Piste de paliers ★1→★5** sous l'arme, comme les versions de SM2 ; liste à gauche en cartes larges (rendu + nom + ★ + Lv).
- **Risque** : générique. Il faut le rattacher à Rempart par des photos et un fond 3D de ruines grises, et par l'iconographie (silhouettes d'insectes, insignes).

#### C. « Équipement de terrain, diégétique et sale »
*Esprit : on est dans un dépôt de fortune ; le matériel est posé sur un établi, les fiches sont des étiquettes de caisse.*
- **Exemples** : Metro Exodus (établi, bandeau orange rouillé), STALKER 2 (inventaire à grille, poids), Hunt: Showdown (bannières au pinceau, typo serif sobre, texture papier), Remnant II (charbon, matières usées), Deep Rock Galactic (terminal dans la base), Vermintide 2 / Darktide (gris sale + une couleur de rareté), Insurgency (pièce réelle derrière le loadout).
- **Palette**

  | Token | Hex | Usage |
  |---|---|---|
  | `bg` | `#151413` | Charbon chaud |
  | `panel` | `#1E1C1A` à 88 % | Panneaux, grain de bruit 3 % |
  | `line` | `#3A3631` | Filets |
  | `text` | `#E8E2D6` | Texte, blanc cassé « papier » |
  | `text-dim` | `#8F887C` | Texte secondaire |
  | **`accent`** | **`#E0782A`** (orange rouille / signalisation) | Accent |
  | `danger` | `#C8352B` | Danger |
  | `ok` | `#8DBF5A` | Validation |

  Raretés désaturées de 15 % : gris `#B5B2AA`, vert `#7FB35A`, bleu `#4C8FD6`, violet `#9466D6`, or `#E2B23A`.
- **Typo** : titres en **Oswald / Bebas Neue** ou un stencil sobre (**Saira Stencil One** pour les seuls titres d'écran). Corps en **Barlow**. Étiquettes en capitales, style « pochoir sur caisse ».
- **Signes distinctifs**
  - Le menu Arsenal **est** une scène 3D : établi ou râtelier dans un hangar en ruine, la caméra glisse d'une arme à l'autre.
  - La fiche de stats est une **étiquette de caisse** : fond blanc cassé, texte noir, barres en blocs. Le bouton principal ressemble à un **coup de pinceau ou ruban adhésif** orange.
  - Les bannières d'annonce du HUD sont au pinceau noir (Hunt) ; les caisses de loot sont de vraies caisses 3D qui s'ouvrent.
- **Risque**
  - Coût de production le plus élevé (scènes 3D, textures) et lisibilité en danger si la texture dépasse 5 % d'opacité.
  - Limiter le diégétique à l'Arsenal, au lobby et à la révélation de caisses. Le HUD et les options restent en style B.

**Recommandation de travail :** prendre une base **B** pour le HUD, les options, la pause et la navigation, et ajouter les touches **A** pour les stats, l'escouade et les briefings (chiffres monospace, préfixes `//`, crochets de focus). Réserver le **C** aux moments forts : écran titre, Arsenal sur établi, révélation de caisses. L'**écran Sélection d'arme** suit alors :
- onglets de slots en haut (Q/E) ;
- liste de cartes à gauche (rendu + nom + Lv + ★ + liseré de rareté) ;
- arme 3D au centre-droite ;
- bloc stats en bas à droite, avec 3 chiffres héros (Dégâts, Cadence, Chargeur) puis 3 barres fines avec delta vs arme équipée (Rechargement, Portée, Précision) ;
- piste ★ sous l'arme ;
- `ÉQUIPER` plein en bas à droite ; `Maintenir C : Comparer` et `I : Inspecter` dans la barre d'aide.
