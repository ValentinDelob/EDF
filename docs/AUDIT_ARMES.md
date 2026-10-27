# Audit des armes — résultats

Généré par `tools/weapon_audit.py` à partir de `--weapon-audit` (plan : [PLAN_TEST_ARMES.md](PLAN_TEST_ARMES.md)).

**907 armes, 883 sans échec, 24 avec échec.**

## Écarts restants

- Rifleman **grantm33** (Rocket) : cadence ÉCHEC (0.86 / 1.00 par s)
- Rifleman **volcano6** (Rocket) : cadence ÉCHEC (0.56 / 1.00 par s)
- Rifleman **mlra** (Missile) : dégâts ÉCHEC (aucun dégât)
- Rifleman **mlratw** (Missile) : dégâts ÉCHEC (1.9 / 226.4, -99 %)
- Rifleman **firecracker1** (Firecracker) : dégâts ÉCHEC (187.0 / 510.0, -63 %)
- Rifleman **firecracker4** (Firecracker) : dégâts ÉCHEC (3924.0 / 9171.0, -57 %)
- Airborne **powerlance** (Lance) : dégâts ÉCHEC (201.6 / 307.6, -34 %)
- Airborne **doublelance** (Lance) : dégâts ÉCHEC (266.6 / 384.7, -31 %) ; DPS ÉCHEC (758 / 1094)
- Airborne **reflectronlaser** (Beam) : dégâts ÉCHEC (5.9 / 8.1, -27 %) ; DPS ÉCHEC (311 / 424)
- Airborne **lightningbowzamd** (Spark) : DPS ÉCHEC (4233 / 7102)
- Airborne **boltshooterra** (Spark) : DPS ÉCHEC (272 / 372)
- Airborne **boltshooterrf** (Spark) : DPS ÉCHEC (764 / 1205)
- Airborne **splitball** (HandGrenade) : dégâts ÉCHEC (170.0 / 1359.2, -87 %)
- Airborne **plasmaheavycannon** (PlasmaShot) : dégâts ÉCHEC (5610.5 / 7700.7, -27 %)
- Airborne **plasmafallf10** (PlasmaShot) : dégâts ÉCHEC (815.0 / 4077.0, -80 %)
- Airborne **plasmagreatcannon** (PlasmaShot) : dégâts ÉCHEC (16457.2 / 22649.1, -27 %)
- Airborne **splitballat** (HandGrenade) : dégâts ÉCHEC (849.5 / 6794.4, -87 %)
- Airborne **plasmafallf20** (PlasmaShot) : dégâts ÉCHEC (1631.0 / 16308.0, -90 %)
- Support **b1mortar** (Artillery) : dégâts ÉCHEC (aucun dégât)
- Support **105mmrapidfirecannony** (Gunship) : dégâts ÉCHEC (aucun dégât)
- Support **suppressgunmh** (Bullet) : dégâts ÉCHEC (12481.5 / 10839.6, +15 %)
- Support **suppressgunmh2** (Bullet) : dégâts ÉCHEC (13091.5 / 10503.0, +25 %) ; DPS ÉCHEC (6766 / 5429)
- Heavy **fg20handgatling** (Bullet) : dégâts ÉCHEC (55.5 / 158.0, -65 %) ; DPS ÉCHEC (871 / 2394)
- Heavy **galleonrapidfirecannonm2** (Bullet) : dégâts ÉCHEC (aucun dégât)

## Rifleman (254/260)

| Arme | Nature | Tirs | Dégâts / coup | Cadence | Rechargement | Vitesse | DPS |
|---|---|---|---|---|---|---|---|
| pa11 | Bullet | 73 | PASS (15.5 / 15.8, -2 %) | PASS (12.07 / 12.00 par s) | PASS (1.10 / 1.10 s) | PASS (300 / 272 m/s) | PASS (170 / 172) |
| pa11ls | Bullet | 73 | PASS (16.5 / 16.9, -2 %) | PASS (12.07 / 12.00 par s) | PASS (1.10 / 1.10 s) | PASS (307 / 272 m/s) | PASS (181 / 184) |
| minionbuster | Bullet | 73 | PASS (29.5 / 29.4, +0 %) | PASS (12.07 / 12.00 par s) | PASS (1.50 / 1.50 s) | PASS (150 / 136 m/s) | PASS (309 / 307) |
| sluggernn1 | Bullet | 91 | PASS (20.5 / 20.9, -2 %) | PASS (15.08 / 15.00 par s) | PASS (1.30 / 1.30 s) | — | PASS (272 / 275) |
| gm21s | Bullet | 33 | PASS (55.0 / 55.0, -0 %) | PASS (12.15 / 12.00 par s) | PASS (1.60 / 1.60 s) | — | PASS (421 / 417) |
| m1raven | Bullet | 121 | PASS (15.5 / 15.4, +1 %) | PASS (20.17 / 20.00 par s) | PASS (1.70 / 1.70 s) | — | PASS (272 / 268) |
| pa11sls | Bullet | 73 | PASS (26.5 / 26.7, -1 %) | PASS (12.07 / 12.00 par s) | PASS (1.10 / 1.10 s) | PASS (300 / 272 m/s) | PASS (290 / 291) |
| t1stork | Bullet | 72 | PASS (31.5 / 31.6, -0 %) | PASS (11.93 / 12.00 par s) | PASS (1.60 / 1.60 s) | PASS (341 / 306 m/s) | PASS (331 / 334) |
| gm22s | Bullet | 33 | PASS (77.0 / 77.1, -0 %) | PASS (12.15 / 12.00 par s) | PASS (1.10 / 1.10 s) | — | PASS (666 / 661) |
| minionbusterburst | Bullet | 72 | PASS (96.5 / 96.3, +0 %) | PASS (3.97 / 4.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (1000 / 1005) |
| m1ravenls | Bullet | 121 | PASS (28.5 / 28.7, -1 %) | PASS (20.17 / 20.00 par s) | PASS (1.70 / 1.70 s) | — | PASS (500 / 500) |
| t1storks | Bullet | 73 | PASS (43.5 / 43.8, -1 %) | PASS (12.07 / 12.00 par s) | PASS (1.60 / 1.60 s) | PASS (341 / 306 m/s) | PASS (462 / 463) |
| gma23 | Bullet | 38 | PASS (100.0 / 100.2, -0 %) | PASS (12.13 / 12.00 par s) | PASS (1.20 / 1.20 s) | — | PASS (877 / 872) |
| t1storkls | Bullet | 73 | PASS (31.5 / 31.6, -0 %) | PASS (12.07 / 12.00 par s) | PASS (1.60 / 1.60 s) | PASS (341 / 306 m/s) | PASS (335 / 334) |
| minionbustermk2 | Bullet | 73 | PASS (113.0 / 113.2, -0 %) | PASS (12.07 / 12.00 par s) | PASS (1.50 / 1.50 s) | PASS (150 / 136 m/s) | PASS (1185 / 1181) |
| m2raven | Bullet | 181 | PASS (29.4 / 29.7, -1 %) | PASS (30.17 / 30.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (761 / 766) |
| t2stork | Bullet | 73 | PASS (60.5 / 61.1, -1 %) | PASS (12.07 / 12.00 par s) | PASS (1.10 / 1.10 s) | PASS (317 / 285 m/s) | PASS (661 / 664) |
| sluggernn2 | Bullet | 52 | PASS (154.0 / 159.4, -3 %) | PASS (8.67 / 8.60 par s) | PASS (2.20 / 2.20 s) | — | PASS (1196 / 1229) |
| t2storkkf | Bullet | 73 | PASS (67.0 / 67.3, -0 %) | PASS (12.07 / 12.00 par s) | PASS (1.10 / 1.10 s) | PASS (382 / 340 m/s) | PASS (734 / 733) |
| m2rraven | Bullet | 181 | PASS (34.4 / 34.6, -1 %) | PASS (30.17 / 30.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (890 / 892) |
| gma23ls | Bullet | 38 | PASS (158.0 / 158.6, -0 %) | PASS (12.13 / 12.00 par s) | PASS (1.20 / 1.20 s) | — | PASS (1386 / 1380) |
| m2rerraven | Bullet | 181 | PASS (39.5 / 39.6, -0 %) | PASS (30.17 / 30.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (1055 / 1052) |
| t3stork | Bullet | 73 | PASS (122.0 / 123.6, -1 %) | PASS (12.07 / 12.00 par s) | PASS (2.10 / 2.10 s) | PASS (382 / 340 m/s) | PASS (1216 / 1226) |
| sluggernn3 | Bullet | 52 | PASS (194.5 / 201.3, -3 %) | PASS (8.67 / 8.60 par s) | PASS (2.20 / 2.20 s) | — | PASS (1511 / 1552) |
| t3storkb | Bullet | 69 | PASS (150.5 / 151.6, -1 %) | PASS (3.77 / 4.00 par s) | PASS (2.10 / 2.10 s) | PASS (382 / 340 m/s) | PASS (1421 / 1503) |
| minionbustermk3 | Bullet | 73 | PASS (145.0 / 145.0, +0 %) | PASS (12.07 / 12.00 par s) | PASS (1.50 / 1.50 s) | PASS (234 / 204 m/s) | PASS (1521 / 1513) |
| mxrsirius | Bullet | 177 | PASS (32.4 / 32.7, -1 %) | PASS (30.09 / 30.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (936 / 942) |
| sluggernn3sls | Bullet | 52 | PASS (269.5 / 271.4, -1 %) | PASS (8.67 / 8.60 par s) | PASS (2.20 / 2.20 s) | — | PASS (2093 / 2093) |
| minionblast | Bullet | 33 | PASS (489.0 / 489.2, -0 %) | PASS (12.15 / 12.00 par s) | PASS (1.80 / 1.80 s) | — | PASS (3573 / 3548) |
| t4stork | Bullet | 73 | PASS (137.0 / 137.9, -1 %) | PASS (12.07 / 12.00 par s) | PASS (1.10 / 1.10 s) | PASS (465 / 408 m/s) | PASS (1512 / 1515) |
| m3raven | Bullet | 181 | PASS (77.5 / 78.6, -1 %) | PASS (30.17 / 30.00 par s) | PASS (1.80 / 1.80 s) | — | PASS (2047 / 2066) |
| x400orchid | Bullet | 52 | PASS (185.0 / 187.7, -1 %) | PASS (8.67 / 8.60 par s) | PASS (2.20 / 2.20 s) | — | PASS (1437 / 1447) |
| t4storkae | Bullet | 73 | PASS (139.0 / 141.0, -1 %) | PASS (12.07 / 12.00 par s) | PASS (1.20 / 1.20 s) | — | PASS (1522 / 1536) |
| sluggernn4 | Bullet | 52 | PASS (462.0 / 478.3, -3 %) | PASS (8.67 / 8.60 par s) | PASS (2.20 / 2.20 s) | — | PASS (3588 / 3688) |
| m4raven | Bullet | 362 | PASS (80.5 / 81.6, -1 %) | PASS (60.33 / 60.00 par s) | PASS (2.90 / 2.90 s) | — | PASS (3952 / 3990) |
| gma25 | Bullet | 33 | PASS (403.5 / 403.8, -0 %) | PASS (12.15 / 12.00 par s) | PASS (2.20 / 2.20 s) | — | PASS (2709 / 2692) |
| t4storkmk2 | Bullet | 73 | PASS (226.0 / 227.1, -0 %) | PASS (12.07 / 12.00 par s) | PASS (0.85 / 0.86 s) | PASS (465 / 408 m/s) | PASS (2556 / 2553) |
| x500orchid | Bullet | 52 | PASS (346.5 / 351.3, -1 %) | PASS (8.67 / 8.60 par s) | PASS (2.20 / 2.20 s) | — | PASS (2691 / 2708) |
| m3ravensls | Bullet | 181 | PASS (125.5 / 127.4, -2 %) | PASS (30.17 / 30.00 par s) | PASS (1.20 / 1.20 s) | — | PASS (3459 / 3494) |
| t5stork | Bullet | 91 | PASS (230.5 / 231.3, -0 %) | PASS (15.08 / 15.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (3078 / 3074) |
| minionbustermkx | Bullet | 73 | PASS (398.5 / 398.6, -0 %) | PASS (12.07 / 12.00 par s) | PASS (1.50 / 1.50 s) | PASS (318 / 272 m/s) | PASS (4179 / 4159) |
| gma29 | Bullet | 33 | PASS (572.0 / 574.3, -0 %) | PASS (12.15 / 12.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (4477 / 4459) |
| m5raven | Bullet | 362 | PASS (94.5 / 95.8, -1 %) | PASS (60.33 / 60.00 par s) | PASS (2.90 / 2.90 s) | — | PASS (4639 / 4684) |
| tzstork | Bullet | 91 | PASS (282.0 / 285.3, -1 %) | PASS (15.08 / 15.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (3766 / 3791) |
| ma9slade | Bullet | 91 | PASS (403.5 / 407.6, -1 %) | PASS (15.08 / 15.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (5388 / 5418) |
| blazer | Bullet | 361 | PASS (283.0 / 283.1, -0 %) | PASS (60.33 / 60.00 par s) | PASS (7.38 / 7.40 s) | — | PASS (11810 / 11760) |
| ma10slade | Bullet | 91 | PASS (446.0 / 450.5, -1 %) | PASS (15.08 / 15.00 par s) | PASS (1.10 / 1.10 s) | — | PASS (6175 / 6207) |
| slaughtere20 | Bullet | 8 | PASS (432.5 / 445.9, -3 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | PASS (364 / 373) |
| sgn7 | Bullet | 7 | PASS (381.0 / 389.9, -2 %) | PASS (7.06 / 6.70 par s) | PASS (5.48 / 5.50 s) | — | PASS (412 / 417) |
| slaughtere20ls | Bullet | 8 | PASS (627.5 / 646.8, -3 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | PASS (528 / 541) |
| g1herring | Bullet | 7 | PASS (493.0 / 504.5, -2 %) | PASS (3.60 / 3.50 par s) | PASS (2.90 / 2.90 s) | — | PASS (712 / 721) |
| slaughtere21ls | Bullet | 8 | PASS (989.0 / 1038.0, -5 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | PASS (832 / 868) |
| d35breecher | Bullet | 7 | PASS (2918.3 / 3182.6, -8 %) | PASS (1.01 / 1.00 par s) | PASS (3.70 / 3.70 s) | — | PASS (2009 / 2176) |
| slaughtere22 | Bullet | 8 | PASS (1132.5 / 1199.9, -6 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | PASS (1014 / 1068) |
| sgn8 | Bullet | 12 | PASS (751.0 / 768.5, -2 %) | PASS (6.88 / 6.70 par s) | PASS (4.38 / 4.40 s) | — | PASS (1472 / 1490) |
| slaughtere22ga | Bullet | 8 | PASS (1453.5 / 1539.8, -6 %) | PASS (1.21 / 1.20 par s) | PASS (2.20 / 2.20 s) | — | PASS (1389 / 1462) |
| monsoon | Bullet | 7 | PASS (1453.5 / 1502.8, -3 %) | PASS (1.21 / 1.20 par s) | PASS (1.30 / 1.30 s) | — | PASS (1436 / 1475) |
| d55breecher | Bullet | 7 | PASS (5599.1 / 5824.6, -4 %) | PASS (1.01 / 1.00 par s) | PASS (3.70 / 3.70 s) | — | PASS (3855 / 3983) |
| slaughtere22ts | Bullet | 8 | PASS (2046.5 / 2168.5, -6 %) | PASS (1.21 / 1.20 par s) | PASS (2.20 / 2.20 s) | — | PASS (1956 / 2059) |
| sluggershot1 | Bullet | 7 | PASS (1872.5 / 1992.4, -6 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | PASS (1509 / 1597) |
| slaughtere23 | Bullet | 9 | PASS (2468.4 / 2519.5, -2 %) | PASS (1.41 / 1.40 par s) | PASS (2.90 / 2.90 s) | — | PASS (2537 / 2576) |
| sgnk9 | Bullet | 20 | PASS (1568.8 / 1604.9, -2 %) | PASS (6.79 / 6.70 par s) | PASS (6.08 / 6.10 s) | — | PASS (3476 / 3533) |
| monsoonm2 | Bullet | 7 | PASS (2756.2 / 2850.4, -3 %) | PASS (1.21 / 1.20 par s) | PASS (1.10 / 1.10 s) | — | PASS (2802 / 2878) |
| slaughtere23da | Bullet | 9 | PASS (2982.5 / 3044.3, -2 %) | PASS (1.41 / 1.40 par s) | PASS (2.90 / 2.90 s) | — | PASS (3137 / 3185) |
| sgn10a | Bullet | 20 | PASS (1859.0 / 1927.6, -4 %) | PASS (3.53 / 3.50 par s) | PASS (6.08 / 6.10 s) | — | PASS (3165 / 3263) |
| sluggershot2 | Bullet | 7 | PASS (2769.5 / 3281.5, -16 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | PASS (2232 / 2630) |
| monsoonm3 | Bullet | 11 | PASS (2674.5 / 2814.7, -5 %) | PASS (1.71 / 1.70 par s) | PASS (0.97 / 0.98 s) | — | PASS (3974 / 4156) |
| sgn11a | Bullet | 22 | PASS (2190.2 / 2270.9, -4 %) | PASS (3.52 / 3.50 par s) | PASS (6.08 / 6.10 s) | — | PASS (4500 / 4644) |
| slaughtere23sas | Bullet | 9 | PASS (5145.2 / 5252.0, -2 %) | PASS (1.41 / 1.40 par s) | PASS (2.90 / 2.90 s) | — | PASS (5411 / 5494) |
| d79breecher | Bullet | 7 | PASS (22611.0 / 23522.0, -4 %) | PASS (1.01 / 1.00 par s) | PASS (3.70 / 3.70 s) | — | PASS (15566 / 16083) |
| sluggershot3 | Bullet | 7 | PASS (4615.8 / 5469.2, -16 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | PASS (3720 / 4384) |
| monsoonmz | Bullet | 11 | PASS (4358.2 / 4646.7, -6 %) | PASS (1.71 / 1.70 par s) | PASS (0.97 / 0.98 s) | — | PASS (6476 / 6860) |
| d110breecher | Bullet | 7 | PASS (35280.3 / 36698.8, -4 %) | PASS (1.01 / 1.00 par s) | PASS (3.70 / 3.70 s) | — | PASS (24288 / 25093) |
| sgnk12 | Bullet | 30 | PASS (3799.5 / 3818.4, -0 %) | PASS (6.77 / 6.70 par s) | PASS (6.08 / 6.10 s) | — | PASS (10844 / 10830) |
| slaughterez | Bullet | 8 | PASS (9416.8 / 10121.6, -7 %) | PASS (1.21 / 1.20 par s) | PASS (2.60 / 2.60 s) | — | PASS (9419 / 10055) |
| bustershot | Bullet | 8 | PASS (34649.2 / 34649.4, -0 %) | PASS (1.31 / 1.30 par s) | PASS (3.70 / 3.70 s) | — | PASS (28265 / 28131) |
| fracture | Bullet | 9 | PASS (12462.8 / 13392.3, -7 %) | PASS (1.41 / 1.40 par s) | PASS (3.30 / 3.30 s) | — | PASS (13412 / 14334) |
| kff50 | Bullet | 5 | PASS (124.5 / 124.6, -0 %) | PASS (0.74 / 0.73 par s) | PASS (2.00 / 2.00 s) | — | PASS (71 / 70) |
| np3a | Bullet | 10 | PASS (111.0 / 111.0, +0 %) | PASS (0.55 / 0.55 par s) | PASS (3.00 / 3.00 s) | — | PASS (123 / 123) |
| prototypelysander | Bullet | 2 | PASS (453.0 / 453.0, +0 %) | — | PASS (4.00 / 4.00 s) | — | PASS (99 / 99) |
| kff50ls | Bullet | 5 | PASS (274.0 / 274.1, -0 %) | PASS (0.74 / 0.73 par s) | PASS (2.00 / 2.00 s) | — | PASS (156 / 155) |
| mr98fang | Bullet | 1 | PASS (792.8 / 792.7, +0 %) | — | PASS (4.98 / 5.00 s) | — | — |
| dunkeln202 | Bullet | 14 | PASS (170.0 / 169.9, +0 %) | PASS (2.22 / 2.20 par s) | PASS (4.00 / 4.00 s) | — | PASS (243 / 241) |
| eagleg1 | Bullet | 5 | PASS (423.5 / 423.5, +0 %) | PASS (0.74 / 0.73 par s) | PASS (2.00 / 2.00 s) | — | PASS (242 / 239) |
| np3b | Bullet | 13 | PASS (249.0 / 249.1, -0 %) | PASS (0.74 / 0.73 par s) | PASS (2.00 / 2.00 s) | — | PASS (412 / 408) |
| lysander | Bullet | 2 | PASS (792.8 / 792.7, +0 %) | — | PASS (4.00 / 4.00 s) | — | PASS (195 / 195) |
| kff51ls | Bullet | 5 | PASS (509.5 / 509.6, -0 %) | PASS (0.74 / 0.73 par s) | PASS (2.00 / 2.00 s) | — | PASS (291 / 288) |
| flounders | Bullet | 5 | PASS (543.5 / 543.6, -0 %) | PASS (0.74 / 0.73 par s) | PASS (2.00 / 2.00 s) | — | PASS (310 / 307) |
| dunkeln213 | Bullet | 20 | PASS (226.5 / 226.5, +0 %) | PASS (3.32 / 3.30 par s) | PASS (3.20 / 3.20 s) | — | PASS (499 / 497) |
| eagleg2 | Bullet | 6 | PASS (611.5 / 611.5, +0 %) | PASS (0.89 / 0.88 par s) | PASS (2.30 / 2.30 s) | — | PASS (433 / 429) |
| kff60ls | Bullet | 5 | PASS (736.0 / 736.1, -0 %) | PASS (0.74 / 0.73 par s) | PASS (3.00 / 3.00 s) | — | PASS (437 / 432) |
| mr99fang | Bullet | 1 | PASS (2265.0 / 2264.9, +0 %) | — | PASS (4.98 / 5.00 s) | — | — |
| ptarmiganha1 | Bullet | 11 | PASS (475.5 / 475.6, -0 %) | PASS (2.22 / 2.20 par s) | PASS (2.50 / 2.50 s) | — | PASS (702 / 698) |
| kff60re | Bullet | 5 | PASS (934.2 / 934.3, -0 %) | PASS (0.74 / 0.73 par s) | PASS (2.00 / 2.00 s) | — | PASS (594 / 587) |
| pylona1 | Bullet | 7 | PASS (815.5 / 815.4, +0 %) | PASS (1.11 / 1.10 par s) | PASS (2.00 / 2.00 s) | — | PASS (741 / 735) |
| dunkeln224 | Bullet | 20 | PASS (453.0 / 453.0, +0 %) | PASS (3.32 / 3.30 par s) | PASS (4.00 / 4.00 s) | — | PASS (921 / 918) |
| np3c | Bullet | 13 | PASS (764.5 / 764.4, +0 %) | PASS (0.74 / 0.73 par s) | PASS (3.00 / 3.00 s) | — | PASS (1122 / 1112) |
| penetrator | Bullet | 10 | PASS (815.2 / 815.2, +0 %) | PASS (1.71 / 1.70 par s) | PASS (3.00 / 3.00 s) | — | PASS (921 / 918) |
| dunkeln235 | Bullet | 20 | PASS (656.8 / 656.8, -0 %) | PASS (3.28 / 3.30 par s) | PASS (4.98 / 5.00 s) | — | PASS (1212 / 1214) |
| lysanderf | Bullet | 2 | PASS (3397.5 / 3397.4, +0 %) | — | PASS (4.00 / 4.00 s) | — | PASS (834 / 834) |
| ptarmiganha2 | Bullet | 12 | PASS (1019.2 / 1019.2, +0 %) | PASS (2.22 / 2.20 par s) | PASS (2.20 / 2.20 s) | — | PASS (1608 / 1598) |
| floundersfs | Bullet | 5 | PASS (2151.5 / 2151.5, +0 %) | PASS (0.74 / 0.73 par s) | PASS (1.80 / 1.80 s) | — | PASS (1365 / 1349) |
| np4sa | Bullet | 17 | PASS (1359.0 / 1358.9, +0 %) | PASS (0.74 / 0.73 par s) | PASS (7.48 / 7.50 s) | — | PASS (1747 / 1734) |
| penetratorf | Bullet | 10 | PASS (1630.8 / 1630.8, -0 %) | PASS (1.71 / 1.70 par s) | PASS (3.00 / 3.00 s) | — | PASS (1843 / 1836) |
| kff70 | Bullet | 5 | PASS (2718.0 / 2717.9, +0 %) | PASS (0.83 / 0.82 par s) | PASS (2.00 / 2.00 s) | — | PASS (1905 / 1885) |
| lysanderz | Bullet | 2 | PASS (6794.8 / 6794.7, +0 %) | — | PASS (3.50 / 3.50 s) | — | PASS (1747 / 1747) |
| ptarmiganhaz | Bullet | 12 | PASS (2038.5 / 2038.4, +0 %) | PASS (2.22 / 2.20 par s) | PASS (2.00 / 2.00 s) | — | PASS (3303 / 3281) |
| mr100fang | Bullet | 1 | PASS (13589.5 / 13589.5, +0 %) | — | PASS (4.28 / 4.30 s) | — | — |
| dunkeln236r | Bullet | 41 | PASS (792.8 / 792.7, +0 %) | PASS (6.74 / 6.70 par s) | PASS (4.98 / 5.00 s) | — | PASS (3791 / 3771) |
| kff71s | Bullet | 5 | PASS (3963.5 / 3963.6, -0 %) | PASS (0.83 / 0.82 par s) | PASS (2.00 / 2.00 s) | — | PASS (2777 / 2749) |
| lysanderzf | Bullet | 3 | PASS (11324.5 / 11324.6, -0 %) | PASS (0.37 / 0.37 par s) | PASS (3.50 / 3.50 s) | — | PASS (3749 / 3749) |
| grantm31 | Rocket | 3 | PASS (113.2 / 113.2, +0 %) | PASS (1.02 / 1.00 par s) | PASS (1.80 / 1.80 s) | — | — |
| leopardguidedrocketlauncher | GuidedRocket | 3 | PASS (158.5 / 158.5, +0 %) | PASS (1.02 / 1.00 par s) | PASS (1.80 / 1.80 s) | — | — |
| goliathd1 | Rocket | 1 | PASS (328.5 / 328.4, +0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| grantm32 | Rocket | 3 | PASS (249.0 / 249.1, -0 %) | PASS (1.02 / 1.00 par s) | PASS (2.00 / 2.00 s) | — | — |
| cascade1 | Rocket | 30 | PASS (73.5 / 73.6, -0 %) | PASS (6.04 / 6.00 par s) | PASS (4.98 / 5.00 s) | — | — |
| e9hornet | Rocket | 6 | PASS (215.2 / 215.2, +0 %) | PASS (1.52 / 1.50 par s) | PASS (2.00 / 2.00 s) | — | — |
| leopardguidedrocketlauncherm2 | GuidedRocket | 3 | PASS (430.2 / 430.3, -0 %) | PASS (1.02 / 1.00 par s) | PASS (1.80 / 1.80 s) | — | — |
| grantm40 | Rocket | 6 | PASS (475.5 / 475.6, -0 %) | PASS (0.91 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | — |
| goliathd2 | Rocket | 1 | PASS (833.5 / 833.5, +0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| e10hornet | Rocket | 6 | PASS (339.8 / 339.7, +0 %) | PASS (1.52 / 1.50 par s) | PASS (2.00 / 2.00 s) | — | — |
| volcanov12 | Rocket | 6 | PASS (158.5 / 158.5, +0 %) | — | PASS (2.00 / 2.00 s) | — | — |
| leopardguidedrocketlauncherm3 | GuidedRocket | 6 | PASS (436.0 / 436.0, +0 %) | PASS (1.01 / 1.00 par s) | PASS (1.80 / 1.80 s) | — | — |
| cascade2 | Rocket | 30 | PASS (149.5 / 149.5, +0 %) | PASS (6.04 / 6.00 par s) | PASS (4.00 / 4.00 s) | — | — |
| grantm41 | Rocket | 5 | PASS (471.0 / 471.1, -0 %) | PASS (1.32 / 1.30 par s) | PASS (1.50 / 1.50 s) | — | — |
| goliathd3 | Rocket | 1 | PASS (1268.5 / 1268.4, +0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| ibisguidedrocket | GuidedRocket | 30 | PASS (203.8 / 203.8, -0 %) | PASS (6.04 / 6.00 par s) | PASS (4.98 / 5.00 s) | — | — |
| e11hornet | Rocket | 9 | PASS (589.0 / 588.9, +0 %) | PASS (1.51 / 1.50 par s) | PASS (2.00 / 2.00 s) | — | — |
| grantma50 | Rocket | 5 | PASS (736.0 / 736.1, -0 %) | PASS (1.32 / 1.30 par s) | PASS (1.50 / 1.50 s) | — | — |
| leopardguidedrocketlauncherm4 | GuidedRocket | 6 | PASS (679.5 / 679.5, +0 %) | PASS (1.01 / 1.00 par s) | PASS (1.80 / 1.80 s) | — | — |
| cascade3 | Rocket | 38 | PASS (215.0 / 215.2, -0 %) | PASS (7.55 / 7.50 par s) | PASS (5.98 / 6.00 s) | — | — |
| grantm42s | Rocket | 5 | PASS (951.5 / 951.3, +0 %) | PASS (1.32 / 1.30 par s) | PASS (1.50 / 1.50 s) | — | — |
| volcanow20 | Rocket | 6 | PASS (279.5 / 279.7, -0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| e12hornet | Rocket | 9 | PASS (1087.0 / 1087.2, -0 %) | PASS (1.51 / 1.50 par s) | PASS (2.50 / 2.50 s) | — | — |
| leopardguidedrocketlauncherm9 | GuidedRocket | 7 | PASS (1268.5 / 1268.4, +0 %) | PASS (1.01 / 1.00 par s) | PASS (1.80 / 1.80 s) | — | — |
| goliathdmx | Rocket | 1 | PASS (3261.5 / 3261.5, +0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| grantmt51 | Rocket | 6 | PASS (1291.0 / 1291.0, +0 %) | PASS (1.32 / 1.30 par s) | PASS (1.50 / 1.50 s) | — | — |
| cascadefa | Rocket | 45 | PASS (339.5 / 339.7, -0 %) | PASS (7.54 / 7.50 par s) | PASS (5.28 / 5.30 s) | — | — |
| leopardguidedrocketlaunchermodel0 | GuidedRocket | 8 | PASS (1472.0 / 1472.2, -0 %) | PASS (1.21 / 1.20 par s) | PASS (2.00 / 2.00 s) | — | — |
| e20hornet | Rocket | 10 | PASS (1415.5 / 1415.6, -0 %) | PASS (1.51 / 1.50 par s) | PASS (2.00 / 2.00 s) | — | — |
| volcanow30 | Rocket | 6 | PASS (453.0 / 453.0, +0 %) | — | PASS (4.00 / 4.00 s) | — | — |
| goliathzd | Rocket | 1 | PASS (6794.5 / 6794.7, -0 %) | — | PASS (4.98 / 5.00 s) | — | — |
| grantmtx | Rocket | 8 | PASS (1698.5 / 1698.7, -0 %) | PASS (1.31 / 1.30 par s) | PASS (1.20 / 1.20 s) | — | — |
| goliathzdxe | Rocket | 1 | PASS (9059.5 / 9059.6, -0 %) | — | PASS (4.68 / 4.70 s) | — | — |
| e21hornet | Rocket | 13 | PASS (2718.0 / 2717.9, +0 %) | PASS (2.02 / 2.00 par s) | PASS (2.00 / 2.00 s) | — | — |
| me1emerald | Missile | 9 | PASS (136.0 / 135.9, +0 %) | — | PASS (1.50 / 1.50 s) | — | — |
| airtortoise01 | Missile | 2 | PASS (765.5 / 765.7, -0 %) | — | PASS (2.00 / 2.00 s) | — | — |
| me2emerald | Missile | 9 | PASS (158.5 / 158.5, +0 %) | — | PASS (1.50 / 1.50 s) | — | — |
| forka15 | Missile | 2 | PASS (782.0 / 782.0, +0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| airtortoise02 | Missile | 3 | PASS (1531.5 / 1531.4, +0 %) | — | PASS (1.00 / 1.00 s) | — | — |
| mlra2 | Missile | 55 | PASS (70.0 / 70.2, -0 %) | — | PASS (1.00 / 1.00 s) | — | — |
| me3emerald | Missile | 13 | PASS (294.5 / 294.4, +0 %) | — | PASS (1.50 / 1.50 s) | — | — |
| mlra30 | Missile | 69 | PASS (56.5 / 56.6, -0 %) | — | PASS (0.82 / 0.83 s) | — | — |
| prominencem1 | Missile | 2 | PASS (2268.0 / 2267.9, +0 %) | — | PASS (1.50 / 1.50 s) | — | — |
| forka20 | Missile | 2 | PASS (2715.0 / 2718.0, -0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| me4emerald | Missile | 22 | PASS (339.5 / 339.7, -0 %) | — | PASS (0.75 / 0.75 s) | — | — |
| airtortoiseme | Missile | 1 | PASS (7657.0 / 7656.9, +0 %) | — | PASS (9.98 / 10.00 s) | — | — |
| me4remeralds | Missile | 24 | PASS (566.0 / 566.2, -0 %) | — | PASS (0.75 / 0.75 s) | — | — |
| prominencem2 | Missile | 2 | PASS (6047.5 / 6047.7, -0 %) | — | PASS (1.00 / 1.00 s) | — | — |
| forkx20 | Missile | 2 | PASS (7815.0 / 7815.0, +0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| mlratf | Missile | 56 | PASS (204.0 / 407.6, -50 %) | — | PASS (1.70 / 1.70 s) | — | — |
| mex5emerald | Missile | 32 | PASS (906.0 / 906.0, +0 %) | — | PASS (0.75 / 0.75 s) | — | — |
| prominencema | Missile | 1 | PASS (22679.0 / 22678.9, +0 %) | — | PASS (3.00 / 3.00 s) | — | — |
| msfalcon | Missile | 28 | PASS (1132.5 / 1132.5, +0 %) | — | PASS (0.82 / 0.83 s) | — | — |
| mg11 | HandGrenade | 2 | PASS (241.5 / 241.4, +0 %) | — | — | — | — |
| mg11j | HandGrenade | 2 | n.m. (grenade à retardement : elle rebondit sur la cible et peut exploser hors de portée) | — | — | — | — |
| dng1 | HandGrenade | 2 | PASS (724.5 / 724.3, +0 %) | — | — | — | — |
| mg12 | HandGrenade | 2 | PASS (603.5 / 603.6, -0 %) | — | — | — | — |
| mg13 | HandGrenade | 2 | PASS (2173.0 / 2172.8, +0 %) | — | — | — | — |
| mg13j | HandGrenade | 2 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | — | — | — |
| mg14 | HandGrenade | 2 | PASS (1690.0 / 1689.9, +0 %) | — | — | — | — |
| mg14j | HandGrenade | 2 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | — | — | — |
| dng2 | HandGrenade | 2 | PASS (2535.0 / 2534.9, +0 %) | — | — | — | — |
| mg20 | HandGrenade | 2 | PASS (3018.0 / 3017.8, +0 %) | — | — | — | — |
| mg21j | HandGrenade | 2 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | — | — | — |
| dng3 | HandGrenade | 2 | PASS (5432.0 / 5432.0, +0 %) | — | — | — | — |
| boundgrenade | HandGrenade | 2 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | — | — | — |
| dng9 | HandGrenade | 2 | PASS (7242.5 / 7242.6, -0 %) | — | — | — | — |
| grenadelauncherum1 | GrenadeLauncher | 4 | PASS (124.5 / 124.6, -0 %) | PASS (2.07 / 2.00 par s) | PASS (2.00 / 2.00 s) | PASS (164 / 154 m/s) | — |
| grenadelauncherum1j | GrenadeLauncher | 4 | n.m. (grenade à retardement : elle rebondit sur la cible et peut exploser hors de portée) | PASS (2.07 / 2.00 par s) | PASS (2.00 / 2.00 s) | — | — |
| volatilenapalm | Napalm | 4 | PASS (dégâts infligés) | PASS (1.22 / 1.20 par s) | PASS (2.30 / 2.30 s) | — | — |
| stampedem1 | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (4.98 / 5.00 s) | — | — |
| grenadelauncherum2 | GrenadeLauncher | 4 | PASS (226.5 / 226.5, +0 %) | PASS (2.07 / 2.00 par s) | PASS (2.00 / 2.00 s) | PASS (164 / 154 m/s) | — |
| grenadelauncherum2j | GrenadeLauncher | 4 | n.m. (grenade à retardement : elle rebondit sur la cible et peut exploser hors de portée) | PASS (2.07 / 2.00 par s) | PASS (2.00 / 2.00 s) | — | — |
| grenadelauncherumra | GrenadeLauncher | 12 | PASS (113.0 / 113.2, -0 %) | PASS (7.67 / 7.50 par s) | PASS (3.00 / 3.00 s) | PASS (221 / 205 m/s) | — |
| grenadelauncherum3j | GrenadeLauncher | 4 | n.m. (grenade à retardement : elle rebondit sur la cible et peut exploser hors de portée) | PASS (2.07 / 2.00 par s) | PASS (2.70 / 2.70 s) | — | — |
| splashgrenade | GrenadeLauncher | 1 | n.m. (grenade à retardement : elle rebondit sur la cible et peut exploser hors de portée) | — | PASS (5.98 / 6.00 s) | — | — |
| stickygrenadeprototype | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| grenadelauncherum4 | GrenadeLauncher | 5 | PASS (509.5 / 509.6, -0 %) | PASS (2.05 / 2.00 par s) | PASS (3.00 / 3.00 s) | PASS (164 / 154 m/s) | — |
| brutejtriplegrenadelauncher | GrenadeLauncher | 2 | n.m. (grenade à retardement : elle rebondit sur la cible et peut exploser hors de portée) | — | PASS (2.70 / 2.70 s) | — | — |
| flamegeyser | Napalm | 2 | PASS (dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| splashgrenadea | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| volatilenapalmd | Napalm | 4 | PASS (dégâts infligés) | PASS (1.22 / 1.20 par s) | PASS (3.70 / 3.70 s) | — | — |
| triplegrenadelauncherbrute | GrenadeLauncher | 2 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (4.00 / 4.00 s) | — | — |
| stickygrenade | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| grenadelauncherum5 | GrenadeLauncher | 4 | PASS (1132.5 / 1132.5, +0 %) | PASS (1.01 / 1.00 par s) | PASS (4.28 / 4.30 s) | PASS (299 / 257 m/s) | — |
| stampedem2 | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| doubleglaunchercrumble | GrenadeLauncher | 2 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (3.00 / 3.00 s) | — | — |
| volcanicnapalm | Napalm | 1 | PASS (dégâts infligés) | — | PASS (23.30 / 23.30 s) | — | — |
| stickygrenadea | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (3.00 / 3.00 s) | — | — |
| grenadelauncherumax | GrenadeLauncher | 4 | PASS (2265.0 / 2264.9, +0 %) | PASS (1.01 / 1.00 par s) | PASS (4.98 / 5.00 s) | PASS (337 / 308 m/s) | — |
| flamegeyserda | Napalm | 2 | PASS (dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| stampedexm | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| volatilenapalmzd | Napalm | 4 | PASS (dégâts infligés) | PASS (1.22 / 1.20 par s) | PASS (3.70 / 3.70 s) | — | — |
| grenadelauncherumaz | GrenadeLauncher | 5 | PASS (2831.0 / 2831.1, -0 %) | PASS (1.53 / 1.50 par s) | PASS (4.00 / 4.00 s) | — | — |
| flamegeyserf | Napalm | 3 | PASS (dégâts infligés) | PASS (1.22 / 1.20 par s) | PASS (5.98 / 6.00 s) | — | — |
| stampedexam | GrenadeLauncher | 1 | PASS (grenades à rebond / gerbe : dégâts infligés) | — | PASS (5.98 / 6.00 s) | — | — |
| acidshowerprototype | Acid | 13 | PASS (68.0 / 67.8, +0 %) | PASS (2.02 / 2.00 par s) | PASS (3.00 / 3.00 s) | PASS (52 / 50 m/s) | PASS (105 / 104) |
| acidgun | Acid | 362 | PASS (17.0 / 17.0, +0 %) | PASS (60.33 / 60.00 par s) | PASS (3.00 / 3.00 s) | PASS (102 / 99 m/s) | PASS (788 / 785) |
| acidshower | Acid | 7 | PASS (1586.0 / 1586.0, +0 %) | PASS (1.01 / 1.00 par s) | PASS (4.98 / 5.00 s) | — | PASS (1280 / 1269) |
| wideacidgun | Acid | 13 | PASS (1064.0 / 1064.0, +0 %) | PASS (2.02 / 2.00 par s) | PASS (3.00 / 3.00 s) | — | PASS (1649 / 1637) |
| burner | Flame | 359 | PASS (dégâts infligés) | PASS (60.00 / 60.00 par s) | — | — | — |
| flamethrower | Flame | 361 | PASS (dégâts infligés) | PASS (60.16 / 60.00 par s) | — | — | — |
| flamethrowera | Flame | 360 | PASS (dégâts infligés) | PASS (59.99 / 60.00 par s) | — | — | — |
| flamecannon | Flame | 360 | PASS (dégâts infligés) | PASS (59.99 / 60.00 par s) | — | — | — |
| magmacannon | Flame | 360 | PASS (dégâts infligés) | PASS (59.99 / 60.00 par s) | — | — | — |
| firecracker2 | Firecracker | 2 | PASS (1158.2 / 2542.5, -54 %) | — | — | — | — |
| firecracker3 | Firecracker | 2 | PASS (2546.0 / 5094.0, -50 %) | — | — | — | — |
| firecracker0 | Firecracker | 2 | PASS (7973.8 / 16308.0, -51 %) | — | — | — | — |
| h88boundgun | BoundGun | 73 | PASS (13.5 / 13.6, -1 %) | PASS (12.07 / 12.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (142 / 142) |
| hr89boundgun | BoundGun | 181 | PASS (8.4 / 9.1, -8 %) | PASS (30.17 / 30.00 par s) | PASS (2.50 / 2.50 s) | — | PASS (184 / 199) |
| hl90boundgun | BoundGun | 91 | PASS (31.5 / 31.7, -1 %) | PASS (15.08 / 15.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (400 / 400) |
| hs99boundgun | BoundGun | 30 | PASS (226.5 / 226.5, +0 %) | PASS (12.16 / 12.00 par s) | PASS (3.00 / 3.00 s) | — | PASS (1243 / 1235) |
| hx50boundshotgun | BoundGun | 7 | PASS (2265.0 / 2265.0, +0 %) | PASS (1.01 / 1.00 par s) | PASS (5.98 / 6.00 s) | — | PASS (1304 / 1294) |
| c20bomb | Bomb | 6 | PASS (dégâts infligés) | — | PASS (3.70 / 3.70 s) | — | — |
| c21bomb | Bomb | 6 | PASS (dégâts infligés) | — | PASS (3.70 / 3.70 s) | — | — |
| cxspecialbomb | Bomb | 7 | PASS (dégâts infligés) | — | PASS (5.48 / 5.50 s) | — | — |
| c30bomb | Bomb | 6 | PASS (dégâts infligés) | — | PASS (3.70 / 3.70 s) | — | — |
| c70bomb | Bomb | 7 | PASS (dégâts infligés) | — | PASS (5.48 / 5.50 s) | — | — |
| impulsey8 | Impulse | 6 | PASS (dégâts infligés) | — | PASS (5.48 / 5.50 s) | — | — |
| y9antiaircraftimpulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (4.38 / 4.40 s) | — | — |
| y10impulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (4.88 / 4.90 s) | — | — |
| y10antiaircraftimpulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (3.70 / 3.70 s) | — | — |
| y10wideimpulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (3.40 / 3.40 s) | — | — |
| y11impulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (5.48 / 5.50 s) | — | — |
| y11antiaircraftimpulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (5.48 / 5.50 s) | — | — |
| y20impulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (5.48 / 5.50 s) | — | — |
| ydxantiaircraftimpulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (4.00 / 4.00 s) | — | — |
| ydxstrikeimpulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (3.40 / 3.40 s) | — | — |
| y21impulse | Impulse | 7 | PASS (dégâts infligés) | — | PASS (4.88 / 4.90 s) | — | — |
| protoreverser | Reverser | 241 | n.m. | — | — | — | — |
| reverser | Reverser | 360 | n.m. | — | — | — | — |
| reversegun | Reverser | 7 | n.m. | — | — | — | — |
| highcyclereverser | Reverser | 360 | n.m. | PASS (59.99 / 60.00 par s) | — | — | — |
| reverserm2 | Reverser | 360 | n.m. | — | — | — | — |
| reverseshootert1 | ReverseShooter | 7 | n.m. | — | — | — | — |
| reverserm3 | Reverser | 360 | n.m. | — | — | — | — |
| reversebomber | ReverseShooter | 7 | n.m. | — | — | — | — |
| highcyclereverserd | Reverser | 360 | n.m. | PASS (59.99 / 60.00 par s) | — | — | — |
| reverseshootert2 | ReverseShooter | 7 | n.m. | — | — | — | — |
| reverserx | Reverser | 360 | n.m. | — | — | — | — |
| reverseshooterx | ReverseShooter | 7 | n.m. | — | — | — | — |
| highcyclereverserzd | Reverser | 360 | n.m. | PASS (59.99 / 60.00 par s) | — | — | — |
| grantm33 | Rocket | 4 | PASS (317.0 / 317.1, -0 %) | ÉCHEC (0.86 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | — |
| volcano6 | Rocket | 18 | PASS (231.0 / 231.0, +0 %) | ÉCHEC (0.56 / 1.00 par s) | PASS (2.00 / 2.00 s) | — | — |
| mlra | Missile | 40 | ÉCHEC (aucun dégât) | — | PASS (1.00 / 1.00 s) | — | — |
| mlratw | Missile | 60 | ÉCHEC (1.9 / 226.4, -99 %) | — | PASS (1.70 / 1.70 s) | — | — |
| superacidgun | Acid | 362 | PASS (85.0 / 84.9, +0 %) | PASS (60.33 / 60.00 par s) | PASS (3.00 / 3.00 s) | PASS (159 / 148 m/s) | PASS (4182 / 4158) |
| firecracker1 | Firecracker | 2 | ÉCHEC (187.0 / 510.0, -63 %) | — | — | — | — |
| firecracker4 | Firecracker | 2 | ÉCHEC (3924.0 / 9171.0, -57 %) | — | — | — | — |

## Airborne (227/239)

| Arme | Nature | Tirs | Dégâts / coup | Cadence | Rechargement | Vitesse | DPS |
|---|---|---|---|---|---|---|---|
| rapier | Arc | 362 | PASS (3.6 / 6.6, -45 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (207 / 376) |
| sparkviney3 | Spark | 7 | PASS (170.0 / 169.8, +0 %) | PASS (1.01 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (156 / 154) |
| phalanx | Arc | 332 | PASS (15.3 / 17.0, -10 %) | — | PASS (2.00 / 2.00 s) | — | PASS (613 / 683) |
| dragoonlance | Lance | 4 | PASS (683.2 / 907.1, -25 %) | — | PASS (1.50 / 1.50 s) | — | — |
| sparkviney6 | Spark | 7 | PASS (272.0 / 271.8, +0 %) | PASS (1.01 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (255 / 253) |
| phalanxw1 | Arc | 331 | PASS (22.2 / 24.7, -10 %) | — | PASS (2.00 / 2.00 s) | — | PASS (889 / 991) |
| powerlanceld | Lance | 7 | PASS (343.7 / 452.9, -24 %) | — | PASS (0.82 / 0.83 s) | — | — |
| dragoonlanceda | Lance | 3 | PASS (1251.2 / 1503.2, -17 %) | — | PASS (2.00 / 2.00 s) | — | — |
| sparkwhip | Spark | 7 | PASS (449.0 / 448.8, +0 %) | PASS (1.01 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (422 / 417) |
| phalanxm2 | Arc | 332 | PASS (44.8 / 50.0, -10 %) | — | PASS (2.00 / 2.00 s) | — | PASS (1794 / 2004) |
| rapierg | Arc | 362 | PASS (13.0 / 24.0, -46 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (758 / 1391) |
| dragoonlancele | Lance | 4 | PASS (3123.0 / 3597.1, -13 %) | — | PASS (1.50 / 1.50 s) | — | — |
| sparkfoily6 | Spark | 7 | PASS (124.5 / 124.6, -0 %) | PASS (1.01 / 1.00 par s) | PASS (1.00 / 1.00 s) | — | PASS (125 / 123) |
| m2rapier | Arc | 362 | PASS (16.7 / 30.6, -46 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (966 / 1758) |
| doublelanceld | Lance | 14 | PASS (616.5 / 798.5, -23 %) | — | PASS (0.67 / 0.67 s) | — | PASS (1753 / 2271) |
| sparkvineya6 | Spark | 7 | PASS (747.5 / 747.6, -0 %) | PASS (1.01 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (708 / 702) |
| phalanxu1 | Arc | 332 | PASS (84.8 / 94.0, -10 %) | — | PASS (2.00 / 2.00 s) | — | PASS (3397 / 3765) |
| dragoonlancem2 | Lance | 4 | PASS (5894.6 / 6744.6, -13 %) | — | PASS (1.50 / 1.50 s) | — | — |
| powerlancefd | Lance | 9 | PASS (1738.8 / 2311.9, -25 %) | — | PASS (0.67 / 0.67 s) | — | — |
| phalanxm3 | Arc | 342 | PASS (105.7 / 117.2, -10 %) | — | PASS (1.80 / 1.80 s) | — | PASS (4379 / 4857) |
| m3rapier | Arc | 362 | PASS (32.5 / 57.6, -44 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (1881 / 3310) |
| sparkwhipw3 | Spark | 7 | PASS (1087.0 / 1087.2, -0 %) | PASS (1.01 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (1040 / 1030) |
| dragoonlancew | Lance | 6 | PASS (4957.5 / 5440.7, -9 %) | — | PASS (1.80 / 1.80 s) | — | PASS (5408 / 5935) |
| dragoonlancem3 | Lance | 4 | PASS (10040.4 / 12021.0, -16 %) | — | PASS (1.50 / 1.50 s) | — | — |
| m4rapier | Arc | 360 | PASS (65.4 / 79.1, -17 %) | PASS (60.34 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (3785 / 4545) |
| triplelance | Lance | 18 | PASS (1494.0 / 1803.9, -17 %) | — | PASS (0.67 / 0.67 s) | — | PASS (6225 / 7516) |
| phalanxm4 | Arc | 346 | PASS (175.6 / 194.1, -10 %) | — | PASS (1.70 / 1.70 s) | — | PASS (7403 / 8182) |
| sparkfoily8 | Spark | 7 | PASS (260.5 / 260.5, +0 %) | PASS (1.01 / 1.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (262 / 259) |
| dragoonlancem4 | Lance | 4 | PASS (15923.5 / 17082.9, -7 %) | — | PASS (1.50 / 1.50 s) | — | — |
| phalanxw2 | Arc | 348 | PASS (220.4 / 244.4, -10 %) | — | PASS (1.70 / 1.70 s) | — | PASS (9291 / 10303) |
| sparkwhipw5 | Spark | 13 | PASS (1245.5 / 1245.5, +0 %) | PASS (2.02 / 2.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (2153 / 2135) |
| m5rapier | Arc | 362 | PASS (115.1 / 137.7, -16 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (6729 / 7997) |
| twinlance | Lance | 14 | PASS (3387.0 / 4003.8, -15 %) | — | PASS (0.67 / 0.67 s) | — | PASS (9631 / 11385) |
| starrapier | Arc | 362 | PASS (205.3 / 250.9, -18 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (12002 / 14572) |
| dragoonlancetri | Lance | 9 | PASS (11487.0 / 11920.2, -4 %) | — | PASS (1.50 / 1.50 s) | — | PASS (22233 / 23071) |
| sparkviney12 | Spark | 10 | PASS (1902.0 / 1902.0, +0 %) | PASS (1.51 / 1.50 par s) | PASS (1.50 / 1.50 s) | — | PASS (2615 / 2599) |
| powerlanceza | Lance | 9 | PASS (8381.8 / 9988.4, -16 %) | — | PASS (0.67 / 0.67 s) | — | — |
| phalanxm5 | Arc | 361 | PASS (381.4 / 422.4, -10 %) | — | PASS (1.50 / 1.50 s) | — | PASS (16662 / 18455) |
| triplelancezd | Lance | 21 | PASS (4154.5 / 4968.2, -16 %) | — | PASS (0.67 / 0.67 s) | — | PASS (17310 / 20701) |
| rapiernova | Arc | 362 | PASS (335.6 / 404.6, -17 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (19657 / 23545) |
| sparkwhipzy | Spark | 10 | PASS (2672.7 / 2718.0, -2 %) | PASS (1.51 / 1.50 par s) | PASS (1.50 / 1.50 s) | — | PASS (3674 / 3714) |
| phalanxzt | Arc | 372 | PASS (457.9 / 507.0, -10 %) | — | PASS (1.30 / 1.30 s) | — | PASS (20756 / 22984) |
| rapiersupernova | Arc | 362 | PASS (728.8 / 885.7, -18 %) | PASS (60.33 / 60.00 par s) | PASS (0.48 / 0.50 s) | — | PASS (42689 / 51541) |
| sparkwhipzam | Spark | 13 | PASS (2931.7 / 2989.2, -2 %) | PASS (2.02 / 2.00 par s) | PASS (1.50 / 1.50 s) | — | PASS (5233 / 5289) |
| phalanxzat | Arc | 412 | PASS (1733.3 / 1895.9, -9 %) | — | PASS (2.00 / 2.00 s) | — | PASS (75699 / 82800) |
| monsterlance | Laser | 240 | PASS (1019.2 / 1019.2, +0 %) | — | PASS (0.67 / 0.67 s) | — | PASS (36618 / 36618) |
| magblaster | Beam | 362 | PASS (2.6 / 3.4, -23 %) | PASS (60.33 / 60.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (142 / 184) |
| destroyerblaster | Beam | 25 | PASS (127.5 / 127.5, +0 %) | PASS (4.02 / 4.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (479 / 476) |
| magblastera1 | Beam | 362 | PASS (5.3 / 6.8, -23 %) | PASS (60.33 / 60.00 par s) | PASS (1.80 / 1.80 s) | — | PASS (292 / 372) |
| magblastera2 | Beam | 362 | PASS (6.7 / 8.5, -21 %) | PASS (60.33 / 60.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (369 / 465) |
| destroyerblaster2 | Beam | 25 | PASS (234.5 / 234.6, -0 %) | PASS (4.02 / 4.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (880 / 876) |
| magblasterv1 | Beam | 362 | PASS (10.4 / 13.6, -24 %) | PASS (60.33 / 60.00 par s) | PASS (2.30 / 2.30 s) | — | PASS (556 / 723) |
| magblasterb1 | Beam | 362 | PASS (8.6 / 10.2, -16 %) | PASS (60.33 / 60.00 par s) | PASS (4.48 / 4.50 s) | — | PASS (448 / 528) |
| magblasterb2 | Beam | 362 | PASS (13.7 / 17.0, -19 %) | PASS (60.33 / 60.00 par s) | PASS (3.00 / 3.00 s) | — | PASS (729 / 901) |
| magblasterda | Beam | 362 | PASS (29.2 / 36.2, -19 %) | PASS (60.33 / 60.00 par s) | PASS (3.80 / 3.80 s) | — | PASS (1507 / 1860) |
| reflectronvlaser | Beam | 362 | PASS (13.0 / 17.6, -26 %) | PASS (60.33 / 60.00 par s) | PASS (2.70 / 2.70 s) | — | PASS (686 / 924) |
| magblasterg1 | Beam | 362 | PASS (18.4 / 23.8, -23 %) | PASS (60.33 / 60.00 par s) | PASS (3.80 / 3.80 s) | — | PASS (923 / 1189) |
| magblasterg2 | Beam | 362 | PASS (21.9 / 27.2, -19 %) | PASS (60.33 / 60.00 par s) | PASS (3.80 / 3.80 s) | — | PASS (1131 / 1398) |
| destroyerblasterf | Beam | 25 | PASS (1042.0 / 1041.9, +0 %) | PASS (4.02 / 4.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (3911 / 3892) |
| magblasterdn | Beam | 362 | PASS (49.3 / 61.2, -19 %) | PASS (60.33 / 60.00 par s) | PASS (2.30 / 2.30 s) | — | PASS (2699 / 3333) |
| reflectronlaserd | Beam | 362 | PASS (11.4 / 15.2, -25 %) | PASS (60.33 / 60.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (622 / 824) |
| magblasterg3 | Beam | 362 | PASS (26.9 / 34.0, -21 %) | PASS (60.33 / 60.00 par s) | PASS (4.48 / 4.50 s) | — | PASS (1334 / 1677) |
| magblasterbx | Beam | 361 | PASS (35.9 / 40.8, -12 %) | PASS (60.33 / 60.00 par s) | PASS (7.48 / 7.50 s) | — | PASS (1806 / 2042) |
| magblasterw | Beam | 362 | PASS (76.5 / 102.0, -25 %) | PASS (60.33 / 60.00 par s) | PASS (4.48 / 4.50 s) | — | PASS (3648 / 4838) |
| magblastersp1 | Beam | 362 | PASS (42.4 / 54.4, -22 %) | PASS (60.33 / 60.00 par s) | PASS (4.48 / 4.50 s) | — | PASS (2081 / 2655) |
| destroyerblasterw | Beam | 25 | PASS (1377.0 / 1377.0, +0 %) | PASS (4.02 / 4.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (5168 / 5144) |
| reflectron5waylaser | Beam | 362 | PASS (77.8 / 106.9, -27 %) | PASS (60.33 / 60.00 par s) | PASS (4.98 / 5.00 s) | — | PASS (3782 / 5171) |
| magblastersp2 | Beam | 362 | PASS (47.7 / 61.2, -22 %) | PASS (60.33 / 60.00 par s) | PASS (5.28 / 5.30 s) | — | PASS (2265 / 2891) |
| reflectron5waylaserzd | Beam | 362 | PASS (84.0 / 109.4, -23 %) | PASS (60.33 / 60.00 par s) | PASS (3.30 / 3.30 s) | — | PASS (4420 / 5727) |
| destroyerblasterfe | Beam | 25 | PASS (2771.0 / 2771.0, +0 %) | PASS (4.02 / 4.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (10399 / 10351) |
| spineblasterzam4 | Beam | 362 | PASS (117.6 / 117.6, +0 %) | PASS (60.33 / 60.00 par s) | PASS (2.50 / 2.50 s) | — | PASS (6261 / 6230) |
| lightningbow | Spark | 201 | PASS (5.1 / 5.1, -0 %) | — | PASS (0.82 / 0.82 s) | — | PASS (131 / 131) |
| boltgun | Spark | 14 | PASS (159.0 / 159.0, +0 %) | PASS (2.31 / 2.30 par s) | PASS (3.00 / 3.00 s) | — | PASS (305 / 304) |
| lightningbowm2 | Spark | 201 | PASS (10.2 / 10.2, -0 %) | — | PASS (0.82 / 0.82 s) | — | PASS (262 / 262) |
| thundercrossbow | Spark | 8 | PASS (275.6 / 294.0, -6 %) | — | PASS (0.77 / 0.77 s) | — | — |
| boltguna1 | Spark | 14 | PASS (289.9 / 298.8, -3 %) | PASS (2.31 / 2.30 par s) | PASS (3.20 / 3.20 s) | — | PASS (550 / 565) |
| lightningbowm3 | Spark | 206 | PASS (22.4 / 22.6, -1 %) | — | PASS (0.82 / 0.82 s) | — | PASS (582 / 588) |
| thundercrossbow2 | Spark | 8 | PASS (636.6 / 679.0, -6 %) | — | PASS (0.77 / 0.77 s) | — | — |
| boltgunra | Spark | 28 | PASS (275.9 / 283.0, -3 %) | PASS (4.63 / 4.60 par s) | PASS (2.30 / 2.30 s) | — | PASS (1104 / 1127) |
| lightningrapidbow | Spark | 401 | PASS (15.8 / 15.9, -1 %) | — | PASS (0.82 / 0.82 s) | — | PASS (817 / 822) |
| thundercrossbowra | Spark | 12 | PASS (959.6 / 1019.0, -6 %) | — | PASS (0.50 / 0.51 s) | — | — |
| lightningbowm4 | Spark | 218 | PASS (44.8 / 45.3, -1 %) | — | PASS (0.82 / 0.82 s) | — | PASS (1176 / 1189) |
| thundercrossbow3 | Spark | 8 | PASS (1168.1 / 1246.0, -6 %) | — | PASS (0.77 / 0.77 s) | — | — |
| boltguna2 | Spark | 14 | PASS (1052.9 / 1076.0, -2 %) | PASS (2.31 / 2.30 par s) | PASS (3.00 / 3.00 s) | — | PASS (2020 / 2057) |
| thundercrossboww | Spark | 8 | PASS (1380.0 / 1472.0, -6 %) | — | PASS (0.77 / 0.77 s) | — | — |
| lightningbowm5 | Spark | 197 | PASS (55.8 / 56.6, -1 %) | — | — | — | PASS (1488 / 1510) |
| boltgunrb | Spark | 41 | PASS (497.6 / 510.0, -2 %) | PASS (6.74 / 6.70 par s) | PASS (3.00 / 3.00 s) | — | PASS (2402 / 2451) |
| lightningrapidbowt2 | Spark | 435 | PASS (38.1 / 38.5, -1 %) | — | PASS (0.82 / 0.82 s) | — | PASS (2000 / 2021) |
| boltpowergun | Spark | 8 | PASS (3777.8 / 3850.0, -2 %) | PASS (1.21 / 1.20 par s) | PASS (3.00 / 3.00 s) | — | PASS (3767 / 3813) |
| thundercrossbowdz | Spark | 4 | PASS (5512.1 / 5605.5, -2 %) | — | PASS (1.50 / 1.50 s) | — | — |
| boltgunaz | Spark | 14 | PASS (2420.8 / 2492.0, -3 %) | PASS (2.31 / 2.30 par s) | PASS (3.00 / 3.00 s) | — | PASS (4645 / 4765) |
| thundercrossbowwz | Spark | 9 | PASS (3200.2 / 3534.0, -9 %) | — | PASS (0.70 / 0.71 s) | — | — |
| boltmaximumgun | Spark | 8 | PASS (5796.0 / 5888.0, -2 %) | PASS (1.21 / 1.20 par s) | PASS (3.00 / 3.00 s) | — | PASS (5836 / 5888) |
| thundercrossbowrz | Spark | 12 | PASS (3078.8 / 3284.0, -6 %) | — | PASS (0.50 / 0.51 s) | — | — |
| boltbreakgun | Spark | 10 | PASS (8655.3 / 8832.0, -2 %) | PASS (1.51 / 1.50 par s) | PASS (3.00 / 3.00 s) | — | PASS (10656 / 10815) |
| thundercrossbowrzam | Spark | 8 | PASS (7380.6 / 7474.0, -1 %) | — | PASS (0.50 / 0.51 s) | — | — |
| pulsemachinegun | Pulse | 113 | PASS (8.8 / 9.1, -3 %) | — | — | PASS (131 / 120 m/s) | PASS (122 / 126) |
| pulsemachinegunr | Pulse | 113 | PASS (12.4 / 12.8, -3 %) | — | — | PASS (157 / 150 m/s) | PASS (171 / 177) |
| 3waypulsemachinegun | Pulse | 90 | PASS (23.5 / 23.7, -1 %) | — | — | — | PASS (261 / 263) |
| pulserapidgun | Pulse | 226 | PASS (12.2 / 13.6, -10 %) | — | — | PASS (131 / 120 m/s) | PASS (325 / 362) |
| 4waypulsemachinegun | Pulse | 113 | PASS (31.2 / 31.6, -1 %) | — | — | — | PASS (444 / 449) |
| stardustcannon | Pulse | 9 | PASS (1978.8 / 2260.0, -12 %) | — | PASS (0.67 / 0.67 s) | — | — |
| 5waypulsemachinegun | Pulse | 113 | PASS (39.1 / 39.5, -1 %) | — | — | — | PASS (551 / 557) |
| pulsemachinegunm2 | Pulse | 113 | PASS (43.6 / 45.3, -4 %) | — | — | PASS (126 / 120 m/s) | PASS (610 / 634) |
| pulsebuster | Pulse | 30 | PASS (198.0 / 198.0, +0 %) | — | — | — | PASS (754 / 754) |
| 6waypulsemachinegun | Pulse | 113 | PASS (64.9 / 65.4, -1 %) | — | — | — | PASS (915 / 922) |
| stardustcannontypea | Pulse | 9 | PASS (3970.3 / 4300.0, -8 %) | — | PASS (0.67 / 0.67 s) | — | — |
| pulsemachinegunm3 | Pulse | 113 | PASS (76.7 / 79.3, -3 %) | — | — | PASS (130 / 120 m/s) | PASS (1066 / 1102) |
| pulsebusteralpha | Pulse | 30 | PASS (297.0 / 297.0, +0 %) | — | — | — | PASS (1130 / 1130) |
| pulserapidgunde | Pulse | 226 | PASS (38.5 / 45.3, -15 %) | — | — | PASS (191 / 180 m/s) | PASS (1005 / 1183) |
| 8waypulsemachinegun | Pulse | 113 | PASS (75.3 / 87.2, -14 %) | — | — | — | PASS (1062 / 1230) |
| stardustcannontyped | Pulse | 8 | PASS (7166.8 / 7930.0, -10 %) | — | PASS (0.80 / 0.80 s) | — | — |
| pulsemachinegunm4 | Pulse | 113 | PASS (123.6 / 126.8, -3 %) | — | — | PASS (191 / 180 m/s) | PASS (1718 / 1762) |
| 9waypulsemachinegun | Pulse | 113 | PASS (114.5 / 148.5, -23 %) | — | — | — | PASS (1624 / 2106) |
| pulsemachinegunm5 | Pulse | 113 | PASS (120.2 / 123.4, -3 %) | — | — | PASS (259 / 240 m/s) | PASS (1661 / 1706) |
| pulsebusterbeta | Pulse | 30 | PASS (561.0 / 561.0, +0 %) | — | — | — | PASS (2135 / 2135) |
| 10waypulsemachinegun | Pulse | 113 | PASS (124.9 / 180.0, -31 %) | — | — | — | PASS (1771 / 2552) |
| stardustcannontypef | Pulse | 9 | PASS (12200.9 / 12680.0, -4 %) | — | PASS (0.67 / 0.67 s) | — | — |
| pulsemachinegunzdm | Pulse | 113 | PASS (165.5 / 169.9, -3 %) | — | — | PASS (259 / 240 m/s) | PASS (2288 / 2348) |
| stardustfall | Pulse | 7 | PASS (15510.4 / 15850.0, -2 %) | — | PASS (0.80 / 0.80 s) | — | — |
| superpulsemachinegun | Pulse | 113 | PASS (262.4 / 271.8, -3 %) | — | — | — | PASS (3627 / 3757) |
| closedlaser | Laser | 451 | PASS (2.0 / 1.8, +11 %) | — | — | — | PASS (98 / 88) |
| boltshooter | Spark | 7 | PASS (139.0 / 139.2, -0 %) | PASS (1.11 / 1.10 par s) | PASS (4.88 / 4.90 s) | — | PASS (96 / 96) |
| closedlaserm2 | Laser | 450 | PASS (3.5 / 3.4, +3 %) | — | — | — | PASS (174 / 169) |
| monsterzero | Laser | 272 | PASS (56.5 / 56.6, -0 %) | — | PASS (0.82 / 0.82 s) | — | PASS (1967 / 1970) |
| boltshooter2 | Spark | 7 | PASS (339.5 / 339.6, -0 %) | PASS (1.11 / 1.10 par s) | PASS (4.88 / 4.90 s) | — | PASS (235 / 234) |
| monster | Laser | 272 | PASS (56.5 / 56.6, -0 %) | — | PASS (0.82 / 0.82 s) | — | PASS (1967 / 1970) |
| closedlaserm3 | Laser | 451 | PASS (6.0 / 6.1, -1 %) | — | — | — | PASS (295 / 300) |
| closedlaserma | Laser | 451 | PASS (7.0 / 6.8, +3 %) | — | — | — | PASS (353 / 343) |
| raijin | Spark | 3 | PASS (12060.5 / 15288.0, -21 %) | — | PASS (2.50 / 2.50 s) | — | — |
| closedlasermsa | Laser | 451 | PASS (11.0 / 11.0, +0 %) | — | — | — | PASS (555 / 555) |
| boltshooter3 | Spark | 7 | PASS (869.5 / 869.6, -0 %) | PASS (1.11 / 1.10 par s) | PASS (4.88 / 4.90 s) | — | PASS (681 / 676) |
| boltshooterda | Spark | 5 | PASS (1304.5 / 1304.4, +0 %) | PASS (0.80 / 0.79 par s) | PASS (4.88 / 4.90 s) | — | PASS (728 / 721) |
| monsterga | Laser | 272 | PASS (241.5 / 241.4, +0 %) | — | PASS (0.82 / 0.82 s) | — | PASS (8407 / 8404) |
| closedlasermx | Laser | 451 | PASS (20.0 / 19.8, +1 %) | — | — | — | PASS (1020 / 1010) |
| closedlasermz | Laser | 451 | PASS (26.0 / 26.0, +0 %) | — | — | — | PASS (1296 / 1296) |
| closedlonglaser | Laser | 451 | PASS (30.5 / 30.6, -0 %) | — | — | — | PASS (1595 / 1600) |
| boltshooterzf | Spark | 7 | PASS (2038.0 / 2038.2, -0 %) | PASS (1.11 / 1.10 par s) | PASS (4.88 / 4.90 s) | — | PASS (1739 / 1725) |
| monstergz | Laser | 212 | PASS (362.0 / 362.1, -0 %) | — | PASS (0.82 / 0.82 s) | — | PASS (12602 / 12606) |
| closedlaserf | Laser | 451 | PASS (36.0 / 36.2, -1 %) | — | — | — | PASS (1883 / 1893) |
| raijinalpha | Spark | 1 | PASS (64776.5 / 99656.0, -35 %) | — | — | — | — |
| raijinlimitcustom | Spark | 10 | PASS (10191.0 / 10191.0, +0 %) | PASS (1.61 / 1.60 par s) | — | — | PASS (9172 / 9140) |
| plasmacannon | PlasmaShot | 5 | PASS (100.2 / 101.9, -2 %) | — | PASS (1.20 / 1.20 s) | PASS (516 / 437 m/s) | — |
| blinkball | HandGrenade | 1 | PASS (283.0 / 283.1, -0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| gritshot | PlasmaShot | 8 | PASS (90.0 / 91.0, -1 %) | — | — | — | — |
| plasmabigcannon | PlasmaShot | 4 | PASS (189.4 / 203.8, -7 %) | — | PASS (1.60 / 1.60 s) | PASS (231 / 218 m/s) | — |
| plasmafall | PlasmaShot | 11 | PASS (207.9 / 237.8, -13 %) | — | PASS (0.53 / 0.54 s) | PASS (143 / 164 m/s) | — |
| plasmaburstcannon2 | PlasmaShot | 6 | PASS (183.5 / 183.5, +0 %) | — | PASS (1.60 / 1.60 s) | PASS (516 / 437 m/s) | — |
| plasmacannontypeb | PlasmaShot | 5 | PASS (201.5 / 203.8, -1 %) | — | PASS (1.20 / 1.20 s) | — | — |
| splasher | PlasmaShot | 8 | PASS (87.8 / 90.5, -3 %) | — | — | — | — |
| plasmabigcannonm2 | PlasmaShot | 4 | PASS (564.4 / 611.5, -8 %) | — | PASS (1.60 / 1.60 s) | PASS (394 / 371 m/s) | — |
| gritgun | PlasmaShot | 8 | PASS (27.2 / 27.2, +0 %) | — | — | PASS (513 / 437 m/s) | — |
| plasmafallf6 | PlasmaShot | 2 | PASS (917.2 / 1834.8, -50 %) | — | PASS (3.30 / 3.30 s) | — | — |
| blinkballb | HandGrenade | 1 | PASS (566.2 / 566.2, +0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| plasmaburstcannon3 | PlasmaShot | 6 | PASS (334.0 / 334.1, -0 %) | — | PASS (1.80 / 1.80 s) | PASS (516 / 437 m/s) | — |
| plasmacannontypec | PlasmaShot | 5 | PASS (504.6 / 509.6, -1 %) | — | PASS (1.20 / 1.20 s) | — | — |
| gritshotw10 | PlasmaShot | 8 | PASS (317.5 / 317.0, +0 %) | — | — | — | — |
| blinkballc | HandGrenade | 1 | PASS (1132.5 / 1132.5, +0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| plasmafallda | PlasmaShot | 11 | PASS (460.3 / 475.6, -3 %) | — | PASS (0.53 / 0.54 s) | PASS (143 / 164 m/s) | — |
| gritgunma | PlasmaShot | 8 | PASS (68.0 / 67.9, +0 %) | — | — | — | — |
| tripleplasmacannon | PlasmaShot | 5 | PASS (713.2 / 713.4, -0 %) | — | PASS (1.20 / 1.20 s) | — | — |
| plasmacannoncr | PlasmaShot | 9 | PASS (404.7 / 424.7, -5 %) | — | PASS (0.67 / 0.68 s) | — | — |
| blinkfork | HandGrenade | 1 | PASS (226.5 / 226.5, +0 %) | — | PASS (3.70 / 3.70 s) | — | — |
| plasmaburstcannon2d | PlasmaShot | 6 | PASS (826.8 / 826.7, +0 %) | — | PASS (1.20 / 1.20 s) | — | — |
| splasheraz | PlasmaShot | 8 | PASS (141.2 / 141.5, -0 %) | — | — | — | — |
| plasmabigcannonm3 | PlasmaShot | 9 | PASS (1426.4 / 1494.8, -5 %) | — | PASS (0.67 / 0.68 s) | PASS (231 / 218 m/s) | — |
| blinkballd | HandGrenade | 1 | PASS (2265.0 / 2264.9, +0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| plasmacannontyped | PlasmaShot | 7 | PASS (901.4 / 1030.5, -13 %) | — | PASS (0.95 / 0.95 s) | — | — |
| doubleplasmacannon | PlasmaShot | 8 | PASS (891.6 / 951.2, -6 %) | — | PASS (0.82 / 0.82 s) | — | — |
| gritshotw15 | PlasmaShot | 8 | PASS (1477.5 / 1477.5, +0 %) | — | — | — | — |
| plasmacannontypee | PlasmaShot | 7 | PASS (1178.6 / 1347.6, -13 %) | — | PASS (0.95 / 0.95 s) | — | — |
| plasmaburstcannon4d | PlasmaShot | 7 | PASS (1630.8 / 1630.7, +0 %) | — | PASS (1.60 / 1.60 s) | — | — |
| plasmacannontypef | PlasmaShot | 8 | PASS (1499.3 / 1698.7, -12 %) | — | PASS (0.82 / 0.82 s) | PASS (356 / 327 m/s) | — |
| blinkballfz | HandGrenade | 1 | PASS (4529.8 / 4529.8, -0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| plasmaburstcannon4z | PlasmaShot | 7 | PASS (1494.8 / 1494.8, -0 %) | — | PASS (1.80 / 1.80 s) | PASS (516 / 437 m/s) | — |
| plasmacannontypeg | PlasmaShot | 3 | PASS (1607.6 / 2038.4, -21 %) | — | PASS (0.82 / 0.82 s) | PASS (356 / 327 m/s) | — |
| miragezero | Homing | 5 | PASS (34.0 / 34.0, +0 %) | — | PASS (0.27 / 0.27 s) | — | — |
| geist | Homing | 7 | PASS (79.2 / 79.3, -0 %) | — | PASS (0.87 / 0.88 s) | — | — |
| rapidmirage | Homing | 7 | PASS (17.0 / 17.0, +0 %) | — | PASS (0.07 / 0.07 s) | — | — |
| mirage5way | Homing | 7 | PASS (135.0 / 135.6, -0 %) | — | PASS (0.87 / 0.88 s) | — | — |
| geist2 | Homing | 7 | PASS (226.5 / 226.5, +0 %) | — | PASS (0.87 / 0.88 s) | — | — |
| mirage5wayb | Homing | 7 | PASS (271.5 / 271.8, -0 %) | — | PASS (0.68 / 0.70 s) | — | — |
| ghostchaser | Homing | 7 | PASS (57.9 / 100.1, -42 %) | — | PASS (0.07 / 0.07 s) | — | — |
| miragerumble | Homing | 7 | PASS (113.2 / 113.2, +0 %) | — | PASS (0.27 / 0.27 s) | — | — |
| mirage15 | Homing | 7 | PASS (221.0 / 289.0, -24 %) | — | PASS (0.27 / 0.27 s) | — | — |
| psiblade | Homing | 4 | PASS (679.5 / 679.5, +0 %) | — | PASS (1.60 / 1.60 s) | — | — |
| geist3 | Homing | 7 | PASS (679.5 / 679.5, +0 %) | — | PASS (0.38 / 0.39 s) | — | — |
| mirage15s | Homing | 7 | PASS (367.2 / 481.1, -24 %) | — | PASS (0.27 / 0.27 s) | — | — |
| fenrir | Homing | 7 | PASS (136.0 / 135.9, +0 %) | — | PASS (0.07 / 0.07 s) | — | — |
| mirage5ways | Homing | 5 | PASS (918.0 / 917.4, +0 %) | — | PASS (1.20 / 1.20 s) | — | — |
| psibladealpha | Homing | 4 | PASS (2265.0 / 2264.9, +0 %) | — | PASS (1.60 / 1.60 s) | — | — |
| geistd | Homing | 7 | PASS (1359.0 / 1359.0, +0 %) | — | PASS (0.38 / 0.39 s) | — | — |
| mirage15wr | Homing | 7 | PASS (508.5 / 962.2, -47 %) | — | PASS (0.27 / 0.27 s) | — | — |
| fenrir3way | Homing | 7 | PASS (272.0 / 407.7, -33 %) | — | PASS (0.07 / 0.07 s) | — | — |
| geistg | Homing | 7 | PASS (3494.6 / 4530.0, -23 %) | — | PASS (0.38 / 0.39 s) | — | — |
| diffuser | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| gleipnir | Orb | 6 | PASS (dégâts infligés) | — | — | — | — |
| thundercluster | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| diffuserantiaircustom | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| gleipnirb | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| alphadiffuser | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| heavensgate | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| psicluster | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| diffuserairtosurfacecustom | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| psiclustera | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| exgleipnir | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| heavensgate10 | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| thunderclustera | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| diffuserantiaircustomd | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| heavensgated10 | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| gleipnira | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| diffuserairtosurfacecustomd | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| thunderorb | Orb | 7 | PASS (dégâts infligés) | — | — | — | — |
| finaldays | Orb | 6 | PASS (dégâts infligés) | — | — | — | — |
| psiblastwall | Orb | 6 | PASS (dégâts infligés) | — | PASS (60.00 / 60.00 s) | — | — |
| powerlance | Lance | 9 | ÉCHEC (201.6 / 307.6, -34 %) | — | PASS (0.67 / 0.67 s) | — | — |
| doublelance | Lance | 16 | ÉCHEC (266.6 / 384.7, -31 %) | — | PASS (0.67 / 0.67 s) | — | ÉCHEC (758 / 1094) |
| dragoonlancezm | Lance | 4 | PASS (35217.6 / 35671.3, -1 %) | — | PASS (1.50 / 1.50 s) | — | — |
| reflectronlaser | Beam | 362 | ÉCHEC (5.9 / 8.1, -27 %) | PASS (60.33 / 60.00 par s) | PASS (2.70 / 2.70 s) | — | ÉCHEC (311 / 424) |
| spineblaster | Beam | 362 | PASS (3.0 / 2.9, +2 %) | PASS (60.33 / 60.00 par s) | PASS (1.80 / 1.80 s) | — | PASS (165 / 160) |
| spineblastere2 | Beam | 362 | PASS (7.0 / 7.3, -4 %) | PASS (60.33 / 60.00 par s) | PASS (2.00 / 2.00 s) | — | PASS (382 / 396) |
| spineblastere3 | Beam | 362 | PASS (13.5 / 14.6, -8 %) | PASS (60.33 / 60.00 par s) | PASS (1.70 / 1.70 s) | — | PASS (747 / 804) |
| maglaserza | Beam | 362 | PASS (55.7 / 67.9, -18 %) | PASS (60.33 / 60.00 par s) | PASS (6.78 / 6.80 s) | — | PASS (2630 / 3190) |
| spineblasterzm | Beam | 362 | PASS (29.0 / 29.9, -3 %) | PASS (60.33 / 60.00 par s) | PASS (2.50 / 2.50 s) | — | PASS (1544 / 1585) |
| magblasterdx | Beam | 362 | PASS (139.3 / 169.9, -18 %) | PASS (60.33 / 60.00 par s) | PASS (3.00 / 3.00 s) | — | PASS (7484 / 9083) |
| magblasterzm | Beam | 362 | PASS (83.5 / 101.9, -18 %) | PASS (60.33 / 60.00 par s) | PASS (4.48 / 4.50 s) | — | PASS (4256 / 5166) |
| magblasterfdx | Beam | 362 | PASS (195.0 / 237.8, -18 %) | PASS (60.33 / 60.00 par s) | PASS (3.00 / 3.00 s) | — | PASS (10476 / 12713) |
| lightningbowgd | Spark | 221 | PASS (88.2 / 101.9, -13 %) | — | — | — | PASS (2381 / 2751) |
| lightningbowza | Spark | 226 | PASS (107.2 / 142.7, -25 %) | — | — | — | PASS (2936 / 3908) |
| lightningbowzamd | Spark | 423 | PASS (81.0 / 135.9, -40 %) | — | PASS (0.82 / 0.82 s) | — | ÉCHEC (4233 / 7102) |
| boltshooterra | Spark | 73 | PASS (39.6 / 54.4, -27 %) | PASS (12.07 / 12.00 par s) | PASS (4.88 / 4.90 s) | PASS (454 / 379 m/s) | ÉCHEC (272 / 372) |
| boltshooterrf | Spark | 121 | PASS (50.6 / 80.4, -37 %) | PASS (20.17 / 20.00 par s) | PASS (4.88 / 4.90 s) | PASS (454 / 379 m/s) | ÉCHEC (764 / 1205) |
| splitball | HandGrenade | 1 | ÉCHEC (170.0 / 1359.2, -87 %) | — | PASS (6.48 / 6.50 s) | — | — |
| plasmaheavycannon | PlasmaShot | 2 | ÉCHEC (5610.5 / 7700.7, -27 %) | — | — | PASS (106 / 109 m/s) | — |
| plasmafallf10 | PlasmaShot | 3 | ÉCHEC (815.0 / 4077.0, -80 %) | — | PASS (2.50 / 2.50 s) | — | — |
| plasmagreatcannon | PlasmaShot | 2 | ÉCHEC (16457.2 / 22649.1, -27 %) | — | — | — | — |
| splitballat | HandGrenade | 1 | ÉCHEC (849.5 / 6794.4, -87 %) | — | PASS (6.48 / 6.50 s) | — | — |
| plasmafallf20 | PlasmaShot | 3 | ÉCHEC (1631.0 / 16308.0, -90 %) | — | PASS (2.50 / 2.50 s) | — | — |
| ghostchaserda | Homing | 7 | PASS (1028.6 / 1230.8, -16 %) | — | PASS (0.07 / 0.07 s) | — | — |

## Support (229/233)

| Arme | Nature | Tirs | Dégâts / coup | Cadence | Rechargement | Vitesse | DPS |
|---|---|---|---|---|---|---|---|
| lightmortar | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| mortar | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| howitzer | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| cannonguna | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| mortarconcentratedfirestrategy | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| cannongunb | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| largehowitzer | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| b2mortar | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| cannongunc | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| b3mortarconcentratedfirestrategy | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| cannongund | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| enhancedhowitzer | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| b9mortar | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| b10mortar | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| 150mmsinglebarreledcannona | Gunship | 6 | PASS (dégâts infligés) | PASS (1.72 / 1.70 par s) | PASS (6.18 / 6.20 s) | — | — |
| autocannona | Gunship | 2 | PASS (dégâts infligés) | — | PASS (1.40 / 1.40 s) | — | — |
| 105mmrapidfirecannona | Gunship | 1 | PASS (dégâts infligés) | — | PASS (7.68 / 7.70 s) | — | — |
| rocketcannona | Gunship | 2 | PASS (dégâts infligés) | — | PASS (2.30 / 2.30 s) | — | — |
| 150mmsinglebarreledcannonb | Gunship | 7 | PASS (dégâts infligés) | PASS (1.72 / 1.70 par s) | PASS (6.18 / 6.20 s) | — | — |
| 120neutralizecannona | Gunship | 7 | PASS (dégâts infligés) | PASS (1.72 / 1.70 par s) | PASS (4.58 / 4.60 s) | — | — |
| 180mmcannon | Gunship | 4 | PASS (dégâts infligés) | PASS (0.58 / 0.58 par s) | PASS (12.30 / 12.30 s) | — | — |
| 105mmrapidfirecannonb | Gunship | 1 | PASS (dégâts infligés) | — | PASS (7.68 / 7.70 s) | — | — |
| rocketcannonb | Gunship | 4 | PASS (dégâts infligés) | PASS (1.43 / 1.40 par s) | PASS (2.30 / 2.30 s) | — | — |
| autocannonb | Gunship | 1 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| 120neutralizecannonb | Gunship | 9 | PASS (dégâts infligés) | PASS (1.71 / 1.70 par s) | PASS (4.58 / 4.60 s) | — | — |
| 150mmcannonlapis1 | Gunship | 14 | PASS (dégâts infligés) | PASS (2.31 / 2.30 par s) | PASS (7.68 / 7.70 s) | — | — |
| rocketcannonc | Gunship | 4 | PASS (dégâts infligés) | PASS (1.42 / 1.40 par s) | PASS (2.30 / 2.30 s) | — | — |
| autocannonc | Gunship | 2 | PASS (dégâts infligés) | — | PASS (1.30 / 1.30 s) | — | — |
| 105mmrapidfirecannonc | Gunship | 1 | PASS (dégâts infligés) | — | PASS (7.68 / 7.70 s) | — | — |
| 150mmsinglebarreledcannonc | Gunship | 7 | PASS (dégâts infligés) | PASS (1.72 / 1.70 par s) | PASS (6.18 / 6.20 s) | — | — |
| a1minigun | Gunship | 1 | PASS (dégâts infligés) | — | PASS (1.90 / 1.90 s) | — | — |
| 120mmneutralizecannonc | Gunship | 10 | PASS (dégâts infligés) | PASS (1.71 / 1.70 par s) | PASS (4.58 / 4.60 s) | — | — |
| 105mmconsecutiverapidfirecannon | Gunship | 1 | PASS (dégâts infligés) | — | PASS (9.58 / 9.60 s) | — | — |
| 150mmtriplebarreledcannon | Gunship | 5 | PASS (dégâts infligés) | PASS (0.78 / 0.77 par s) | PASS (8.48 / 8.50 s) | — | — |
| a2minigun | Gunship | 1 | PASS (dégâts infligés) | — | PASS (1.80 / 1.80 s) | — | — |
| rocketcannond4 | Gunship | 5 | PASS (dégâts infligés) | PASS (1.42 / 1.40 par s) | PASS (3.10 / 3.10 s) | — | — |
| 105mmrapidfirecannond | Gunship | 1 | PASS (dégâts infligés) | — | PASS (6.88 / 6.90 s) | — | — |
| 150mmsinglebarreledcannonlapis2 | Gunship | 17 | PASS (dégâts infligés) | PASS (3.48 / 3.50 par s) | PASS (5.38 / 5.40 s) | — | — |
| 105mmconsecutivedestructionrapidfirecannon | Gunship | 1 | PASS (dégâts infligés) | — | PASS (7.68 / 7.70 s) | — | — |
| 190mmcannon | Gunship | 5 | PASS (dégâts infligés) | PASS (0.70 / 0.69 par s) | PASS (12.30 / 12.30 s) | — | — |
| 120mmneutralizecannond | Gunship | 12 | PASS (dégâts infligés) | PASS (2.02 / 2.00 par s) | PASS (4.10 / 4.10 s) | — | — |
| vulcancannonm1 | Gunship | 2 | PASS (dégâts infligés) | — | PASS (1.40 / 1.40 s) | — | — |
| t1rocketcannon | Gunship | 4 | PASS (dégâts infligés) | PASS (1.43 / 1.40 par s) | PASS (3.80 / 3.80 s) | — | — |
| 150mmquadruplebarreledcannon | Gunship | 4 | PASS (dégâts infligés) | PASS (0.58 / 0.58 par s) | PASS (6.88 / 6.90 s) | — | — |
| 120mmneutralizedestructioncannon | Gunship | 14 | PASS (dégâts infligés) | PASS (2.30 / 2.30 par s) | PASS (4.58 / 4.60 s) | — | — |
| vulcancannonm2 | Gunship | 1 | PASS (dégâts infligés) | — | PASS (1.90 / 1.90 s) | — | — |
| 150mmcannond | Gunship | 10 | PASS (dégâts infligés) | PASS (1.71 / 1.70 par s) | PASS (4.58 / 4.60 s) | — | — |
| t2rocketcannon | Gunship | 5 | PASS (dégâts infligés) | PASS (1.42 / 1.40 par s) | PASS (3.80 / 3.80 s) | — | — |
| 150mmcannonlapis | Gunship | 20 | PASS (dégâts infligés) | PASS (3.53 / 3.50 par s) | PASS (4.58 / 4.60 s) | — | — |
| t2hsrocketcannon | Gunship | 5 | PASS (dégâts infligés) | PASS (1.42 / 1.40 par s) | PASS (3.80 / 3.80 s) | — | — |
| combatbomberkm6 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| scoutbomber | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkamuy | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybombervesta | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6plan3 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobos | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planx2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosplan2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybombervestab | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planw2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkamuyb | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosplan3 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planx4 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkamuyb2m | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| highspeedbomberkm6f1 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosplan4 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybombervestabplanx2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planx9 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| highspeedbomberkm6f2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkamuyc1 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosplan10 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosbx | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planw3 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosclusterbombs | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| highspeedbomberkm6f2planw2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybombervestacplanw2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosz | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planx5 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkamuycx | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| highspeedbomberkm6fx | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybombervestada | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planz4 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphoboszplan4 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybomberphobosjclusterbombs | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| combatbomberkm6planx18 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| heavybombervestaeplanx2 | Bomber | 1 | PASS (dégâts infligés) | — | — | — | — |
| ahcruisemissile | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (38.40 / 38.40 s) | — | — |
| ahhighspeedmissilegroup | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (92.29 / 92.30 s) | — | — |
| ahmcruisemissile | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (57.70 / 57.70 s) | — | — |
| lionicu20 | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (92.29 / 92.30 s) | — | — |
| tempesta1 | AirMissile | 1 | PASS (dégâts infligés) | — | — | — | — |
| lionicu30 | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (92.29 / 92.30 s) | — | — |
| tempesta2 | AirMissile | 1 | PASS (dégâts infligés) | — | — | — | — |
| n5cruisemissile | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (38.40 / 38.40 s) | — | — |
| lionicua35 | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (92.29 / 92.30 s) | — | — |
| n6cruisemissile | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (47.20 / 47.20 s) | — | — |
| tempesta3 | AirMissile | 1 | PASS (dégâts infligés) | — | — | — | — |
| tempestat | AirMissile | 1 | PASS (dégâts infligés) | — | — | — | — |
| lionicua45 | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (92.29 / 92.30 s) | — | — |
| n6atcruisemissile | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (38.40 / 38.40 s) | — | — |
| tempestats | AirMissile | 1 | PASS (dégâts infligés) | — | — | — | — |
| lionicua45xe | AirMissile | 1 | PASS (dégâts infligés) | — | PASS (92.29 / 92.30 s) | — | — |
| bulgelasera | Satellite | 1 | PASS (dégâts infligés) | — | PASS (34.60 / 34.60 s) | — | — |
| bulgelaserb | Satellite | 1 | PASS (dégâts infligés) | — | PASS (34.60 / 34.60 s) | — | — |
| spritefalla | Satellite | 1 | PASS (dégâts infligés) | — | — | — | — |
| bulgelaserc | Satellite | 1 | PASS (dégâts infligés) | — | PASS (35.90 / 35.90 s) | — | — |
| bulgelaserd | Satellite | 1 | PASS (dégâts infligés) | — | PASS (38.40 / 38.40 s) | — | — |
| spritefallb | Satellite | 1 | PASS (dégâts infligés) | — | — | — | — |
| bulgelaserr1 | Satellite | 1 | PASS (dégâts infligés) | — | PASS (42.30 / 42.30 s) | — | — |
| spritefallshootingmode | Satellite | 2 | PASS (dégâts infligés) | — | — | — | — |
| bulgelaserr2 | Satellite | 1 | PASS (dégâts infligés) | — | PASS (46.10 / 46.10 s) | — | — |
| spritefallc | Satellite | 1 | PASS (dégâts infligés) | — | — | — | — |
| bulgelaserbeammodes | Satellite | 1 | PASS (dégâts infligés) | — | PASS (92.29 / 92.30 s) | — | — |
| spritefallr | Satellite | 1 | PASS (dégâts infligés) | — | — | — | — |
| bulgelaserbeammoded | Satellite | 1 | PASS (dégâts infligés) | — | PASS (57.70 / 57.70 s) | — | — |
| spritefallpowermode | Satellite | 1 | PASS (dégâts infligés) | — | — | — | — |
| spritefallshootingmode2 | Satellite | 2 | PASS (dégâts infligés) | — | — | — | — |
| bulgelaserbeammodef | Satellite | 1 | PASS (dégâts infligés) | — | PASS (32.00 / 32.00 s) | — | — |
| spritefallmaximum | Satellite | 1 | PASS (dégâts infligés) | — | — | — | — |
| spritefallshootingmode3 | Satellite | 2 | PASS (dégâts infligés) | — | — | — | — |
| bulgelaserbeammodez | Satellite | 1 | PASS (dégâts infligés) | — | PASS (30.80 / 30.80 s) | — | — |
| limpetgun | Limpet | 8 | PASS (34.0 / 34.0, +0 %) | PASS (3.56 / 3.50 par s) | PASS (1.50 / 1.50 s) | — | — |
| limpetsniper | Limpet | 3 | PASS (226.5 / 226.5, +0 %) | PASS (1.22 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetgund | Limpet | 8 | PASS (68.0 / 67.9, +0 %) | PASS (3.56 / 3.50 par s) | PASS (1.50 / 1.50 s) | — | — |
| limpetsnipera2 | Limpet | 3 | PASS (362.5 / 362.4, +0 %) | PASS (1.22 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetgunm2 | Limpet | 8 | PASS (102.0 / 101.9, +0 %) | PASS (3.56 / 3.50 par s) | PASS (1.50 / 1.50 s) | — | — |
| limpetsniperd | Limpet | 3 | PASS (611.5 / 611.5, +0 %) | PASS (1.22 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetgunm3 | Limpet | 9 | PASS (187.0 / 186.9, +0 %) | PASS (3.56 / 3.50 par s) | PASS (1.50 / 1.50 s) | — | — |
| limpetchaingun | Limpet | 36 | PASS (51.0 / 51.0, +0 %) | PASS (20.59 / 20.00 par s) | PASS (1.50 / 1.50 s) | — | — |
| limpetsnipera3 | Limpet | 5 | PASS (792.5 / 792.7, -0 %) | PASS (1.21 / 1.20 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetshot | Limpet | 6 | PASS (1078.0 / 1324.8, -19 %) | PASS (2.34 / 2.30 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetsniperf3 | Limpet | 5 | PASS (1585.5 / 1585.4, +0 %) | PASS (1.73 / 1.70 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetgunma | Limpet | 8 | PASS (509.5 / 509.6, -0 %) | PASS (3.56 / 3.50 par s) | PASS (1.50 / 1.50 s) | — | — |
| limpetsniperme | Limpet | 3 | PASS (3397.5 / 3397.4, +0 %) | PASS (1.76 / 1.70 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetgunmt | Limpet | 12 | PASS (849.5 / 849.3, +0 %) | PASS (6.88 / 6.70 par s) | PASS (1.80 / 1.80 s) | — | — |
| limpetsniperzd | Limpet | 3 | PASS (5096.0 / 5096.0, +0 %) | PASS (1.76 / 1.70 par s) | PASS (2.90 / 2.90 s) | — | — |
| limpetchaingunzd | Limpet | 36 | PASS (424.5 / 424.7, -0 %) | PASS (20.59 / 20.00 par s) | PASS (1.10 / 1.10 s) | — | — |
| robotbomb | Robot | 6 | PASS (dégâts infligés) | — | PASS (2.70 / 2.70 s) | — | — |
| assaultbeetle | Robot | 7 | PASS (dégâts infligés) | — | PASS (3.70 / 3.70 s) | — | — |
| typebrobotbomb | Robot | 7 | PASS (dégâts infligés) | — | PASS (2.70 / 2.70 s) | — | — |
| typecrobotbomb | Robot | 7 | PASS (dégâts infligés) | — | PASS (2.90 / 2.90 s) | — | — |
| patroller | Robot | 7 | PASS (dégâts infligés) | — | PASS (2.90 / 2.90 s) | — | — |
| assaultbeetleda | Robot | 7 | PASS (dégâts infligés) | — | PASS (3.70 / 3.70 s) | — | — |
| robotbombtyped | Robot | 7 | PASS (dégâts infligés) | — | PASS (2.20 / 2.20 s) | — | — |
| stagbeetle | Robot | 7 | PASS (dégâts infligés) | — | PASS (3.70 / 3.70 s) | — | — |
| speedstar | Robot | 7 | n.m. | — | PASS (2.90 / 2.90 s) | — | — |
| robotbombdz | Robot | 7 | PASS (dégâts infligés) | — | PASS (2.20 / 2.20 s) | — | — |
| patrollerda | Robot | 7 | PASS (dégâts infligés) | — | PASS (2.90 / 2.90 s) | — | — |
| zegun | Sentry | 2 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zegun2a | Sentry | 4 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zergun | Sentry | 2 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zesniper | Sentry | 5 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zelauncher | Sentry | 4 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| fzgunm2 | Sentry | 4 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zeragun | Sentry | 2 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| fzgunmd | Sentry | 2 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zegun10 | Sentry | 7 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zexlauncher | Sentry | 4 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zexrgun | Sentry | 4 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| fzgunmx | Sentry | 5 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zexrapidlauncher | Sentry | 4 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| zeblaster | Sentry | 4 | PASS (dégâts infligés) | — | PASS (29.40 / 29.40 s) | — | — |
| laserguidekit | Guide | 0 | n.m. | — | — | — | — |
| highspeedlaserguidekit | Guide | 0 | n.m. | — | — | — | — |
| laserguideddevicet2 | Guide | 0 | n.m. | — | — | — | — |
| laserguideddevicet3 | Guide | 0 | n.m. | — | — | — | — |
| laserguideddevicet4 | Guide | 0 | n.m. | — | — | — | — |
| guidebeacongun | Guide | 5 | n.m. | — | — | — | — |
| guidebeacongunm2 | Guide | 6 | n.m. | — | — | — | — |
| highoutputbeacongun | Guide | 2 | n.m. | — | — | — | — |
| guidebeacongunm3 | Guide | 7 | n.m. | — | — | — | — |
| powerbeacongun | Guide | 1 | n.m. | — | — | — | — |
| guidebeacongunma | Guide | 7 | n.m. | — | — | — | — |
| lifevendor | Post | 3 | n.m. | — | — | — | — |
| lifevendorm2 | Post | 3 | n.m. | — | — | — | — |
| lifevendorm3 | Post | 3 | n.m. | — | — | — | — |
| lifespoutgun | Post | 5 | n.m. | — | — | — | — |
| lifespoutgunm2 | Post | 5 | n.m. | — | — | — | — |
| lifevendorma | Post | 4 | n.m. | — | — | — | — |
| lifevendormh | Post | 5 | n.m. | — | — | — | — |
| lifespoutgunm3 | Post | 7 | n.m. | — | — | — | — |
| lifevendorzd | Post | 5 | n.m. | — | — | — | — |
| lifespoutgunzm | Post | 7 | n.m. | — | — | — | — |
| powerassistgun | Post | 3 | n.m. | — | PASS (5.48 / 5.50 s) | — | — |
| powerpost | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| powerpostm2 | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| powerassistgunm2 | Post | 3 | n.m. | — | PASS (4.28 / 4.30 s) | — | — |
| offensiveterritory | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| powerassistgunmg | Post | 3 | n.m. | — | PASS (2.80 / 2.80 s) | — | — |
| powerpostzm | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| guardpost | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| guardassistgun | Post | 3 | n.m. | — | PASS (5.48 / 5.50 s) | — | — |
| guardpostm2 | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| zoneprotector | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| guardassistgung | Post | 3 | n.m. | — | PASS (2.80 / 2.80 s) | — | — |
| guardpostm3 | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| zoneprotectorzm | Post | 3 | n.m. | — | PASS (22.10 / 22.10 s) | — | — |
| electromagneticbunker | Bunker | 4 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| decoygreen | Decoy | 3 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| antiaircraftbunker | Bunker | 3 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| heavybunker | Bunker | 1 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| decoyblue | Decoy | 3 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| electromagneticbunkerm2 | Bunker | 5 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| personalshelter | Bunker | 1 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| electromagneticprison | Bunker | 3 | n.m. | — | PASS (14.70 / 14.70 s) | — | — |
| decoyyellow | Decoy | 4 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| antiaircraftbunkerme | Bunker | 4 | n.m. | — | PASS (29.40 / 29.40 s) | — | — |
| electromagneticbunkergh | Bunker | 5 | n.m. | — | PASS (58.80 / 58.80 s) | — | — |
| borderline | Bunker | 2 | n.m. | — | PASS (58.80 / 58.80 s) | — | — |
| decoyblack | Decoy | 3 | n.m. | — | PASS (44.10 / 44.10 s) | — | — |
| electromagneticwall | Bunker | 1 | n.m. | — | PASS (58.80 / 58.80 s) | — | — |
| absolutebunker | Bunker | 5 | n.m. | — | PASS (44.10 / 44.10 s) | — | — |
| suppressgunga | Bullet | 1 | PASS (7459.5 / 6680.1, +12 %) | — | PASS (4.00 / 4.00 s) | — | — |
| absolutedzbunker | Bunker | 5 | n.m. | — | PASS (44.10 / 44.10 s) | — | — |
| b1mortar | Artillery | 1 | ÉCHEC (aucun dégât) | — | — | — | — |
| b4mortar | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| giganticgrenade | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| cannongundz | Artillery | 1 | PASS (dégâts infligés) | — | — | — | — |
| 105mmrapidfirecannony | Gunship | 1 | ÉCHEC (aucun dégât) | — | PASS (6.48 / 6.50 s) | — | — |
| 105mmrapidfirecannonz | Gunship | 1 | PASS (dégâts infligés) | — | PASS (4.58 / 4.60 s) | — | — |
| spritefalldestroy | Satellite | 1 | PASS (dégâts infligés) | — | — | — | — |
| limpetsplendor | Limpet | 15 | PASS (74.2 / 100.0, -26 %) | PASS (5.60 / 5.50 par s) | PASS (1.80 / 1.80 s) | — | — |
| splendorshot | Limpet | 3 | PASS (122.2 / 204.0, -40 %) | PASS (1.22 / 1.20 par s) | PASS (3.70 / 3.70 s) | — | — |
| limpetdetector | Limpet | 5 | PASS (570.5 / 1018.5, -44 %) | PASS (1.73 / 1.70 par s) | PASS (5.88 / 5.90 s) | — | — |
| busterbomb | Limpet | 3 | PASS (1602.7 / 1904.0, -16 %) | PASS (2.40 / 2.30 par s) | PASS (2.20 / 2.20 s) | — | — |
| limpetsplendorme | Limpet | 18 | PASS (300.3 / 408.0, -26 %) | PASS (8.79 / 8.60 par s) | PASS (1.50 / 1.50 s) | — | — |
| limpetdetectorme | Limpet | 5 | PASS (1834.1 / 3057.0, -40 %) | PASS (1.21 / 1.20 par s) | PASS (5.08 / 5.10 s) | — | — |
| limpetsplendorzd | Limpet | 23 | PASS (625.9 / 814.8, -23 %) | PASS (10.23 / 10.00 par s) | PASS (1.20 / 1.20 s) | — | — |
| limpetsplendorzam | Limpet | 23 | PASS (835.0 / 1087.2, -23 %) | PASS (10.23 / 10.00 par s) | PASS (1.20 / 1.20 s) | — | — |
| suppressgun | Bullet | 1 | PASS (1112.5 / 1022.8, +9 %) | — | PASS (3.00 / 3.00 s) | — | — |
| suppressgunb2 | Bullet | 2 | PASS (1472.5 / 1352.7, +9 %) | — | PASS (3.00 / 3.00 s) | — | PASS (761 / 699) |
| suppressgunmh | Bullet | 1 | ÉCHEC (12481.5 / 10839.6, +15 %) | — | PASS (3.00 / 3.00 s) | — | — |
| suppressgunmh2 | Bullet | 2 | ÉCHEC (13091.5 / 10503.0, +25 %) | — | PASS (3.00 / 3.00 s) | — | ÉCHEC (6766 / 5429) |

## Heavy (173/175)

| Arme | Nature | Tirs | Dégâts / coup | Cadence | Rechargement | Vitesse | DPS |
|---|---|---|---|---|---|---|---|
| vibrohammer | Strike | 8 | PASS (dégâts infligés) | — | — | — | — |
| vibrohammerf3 | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| forceblade | Strike | 8 | PASS (dégâts infligés) | — | — | — | — |
| vibroroller | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| vibrohammerfa | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| forcebladed | Strike | 8 | PASS (dégâts infligés) | — | — | — | — |
| vibrohaken | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| vulcanhammer | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| dynamoblade | Strike | 3 | PASS (dégâts infligés) | — | — | — | — |
| vibrorollerd2 | Strike | 2 | PASS (dégâts infligés) | — | — | — | — |
| vibrohammerfv | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| forceax | Strike | 8 | PASS (dégâts infligés) | — | — | — | — |
| quakehaken | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| vulcanhammerd | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| vibrorollerd3 | Strike | 3 | PASS (dégâts infligés) | — | — | — | — |
| dynamobladem2 | Strike | 2 | PASS (dégâts infligés) | — | — | — | — |
| quakehakenzd | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| vulcanhammerzd | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| forcebladema | Strike | 8 | PASS (dégâts infligés) | — | — | — | — |
| forceaxg2 | Strike | 6 | PASS (dégâts infligés) | — | — | — | — |
| finesthammer | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| forceaxg3 | Strike | 4 | PASS (dégâts infligés) | — | — | — | — |
| vibrorollerd9 | Strike | 3 | PASS (dégâts infligés) | — | — | — | — |
| dynaforceblade | Strike | 2 | PASS (dégâts infligés) | — | — | — | — |
| forceblademaxe | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| quakehakenzdxe | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| ultimatehammerxe | Strike | 5 | PASS (dégâts infligés) | — | — | — | — |
| blastholespear | Pierce | 10 | PASS (dégâts infligés) | — | PASS (2.00 / 2.00 s) | — | — |
| flashingspear | Pierce | 15 | PASS (dégâts infligés) | PASS (4.59 / 4.60 par s) | PASS (4.00 / 4.00 s) | — | — |
| jackhammer | Pierce | 28 | PASS (dégâts infligés) | PASS (4.62 / 4.60 par s) | PASS (2.30 / 2.30 s) | — | — |
| spinedriver | Pierce | 6 | PASS (dégâts infligés) | PASS (0.96 / 0.95 par s) | PASS (3.00 / 3.00 s) | — | — |
| blastholespearm2 | Pierce | 10 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| blasttwinspears | Pierce | 16 | PASS (dégâts infligés) | — | PASS (2.50 / 2.50 s) | — | — |
| flashingspearm2 | Pierce | 24 | PASS (dégâts infligés) | PASS (5.56 / 5.50 par s) | PASS (4.00 / 4.00 s) | — | — |
| jackhammerm2 | Pierce | 41 | PASS (dégâts infligés) | PASS (6.74 / 6.70 par s) | PASS (2.30 / 2.30 s) | — | — |
| spinedriverm2 | Pierce | 6 | PASS (dégâts infligés) | PASS (0.96 / 0.95 par s) | PASS (3.00 / 3.00 s) | — | — |
| flashingspearm3 | Pierce | 27 | PASS (dégâts infligés) | PASS (6.78 / 6.70 par s) | PASS (4.00 / 4.00 s) | — | — |
| blastholespearm3 | Pierce | 10 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| blasttwinspearsm2 | Pierce | 16 | PASS (dégâts infligés) | — | PASS (2.00 / 2.00 s) | — | — |
| blastholespearm4 | Pierce | 10 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| jackhammerm3 | Pierce | 41 | PASS (dégâts infligés) | PASS (6.74 / 6.70 par s) | PASS (2.50 / 2.50 s) | — | — |
| spinedriverm3 | Pierce | 7 | PASS (dégâts infligés) | PASS (1.11 / 1.10 par s) | PASS (3.00 / 3.00 s) | — | — |
| flashingspearm4 | Pierce | 30 | PASS (dégâts infligés) | PASS (10.18 / 10.00 par s) | PASS (4.00 / 4.00 s) | — | — |
| blastholespearm5 | Pierce | 10 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| jackhammerm4 | Pierce | 41 | PASS (dégâts infligés) | PASS (6.74 / 6.70 par s) | PASS (2.00 / 2.00 s) | — | — |
| blasttwinspearsm3 | Pierce | 16 | PASS (dégâts infligés) | — | PASS (2.00 / 2.00 s) | — | — |
| blastholespearm6 | Pierce | 11 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| spinedriverm4 | Pierce | 8 | PASS (dégâts infligés) | PASS (1.31 / 1.30 par s) | PASS (2.00 / 2.00 s) | — | — |
| jackhammerma | Pierce | 41 | PASS (dégâts infligés) | PASS (6.74 / 6.70 par s) | PASS (2.00 / 2.00 s) | — | — |
| blastholespearms | Pierce | 12 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| blasttwinspearsm4 | Pierce | 16 | PASS (dégâts infligés) | — | PASS (2.50 / 2.50 s) | — | — |
| flashingspearm5 | Pierce | 36 | PASS (dégâts infligés) | PASS (10.14 / 10.00 par s) | PASS (4.00 / 4.00 s) | — | — |
| spinedriverma | Pierce | 8 | PASS (dégâts infligés) | PASS (1.31 / 1.30 par s) | PASS (3.00 / 3.00 s) | — | — |
| jackhammermbf | Pierce | 41 | PASS (dégâts infligés) | PASS (6.74 / 6.70 par s) | PASS (2.00 / 2.00 s) | — | — |
| blasttwinspearsm5 | Pierce | 16 | PASS (dégâts infligés) | — | PASS (2.50 / 2.50 s) | — | — |
| blastholespearmsxe | Pierce | 12 | PASS (dégâts infligés) | — | PASS (1.50 / 1.50 s) | — | — |
| deflectionshield | Shield | 0 | n.m. | — | — | — | — |
| ionmirrorshield | Shield | 0 | n.m. | — | — | — | — |
| towershield | Shield | 0 | n.m. | — | — | — | — |
| deflectionshield2 | Shield | 0 | n.m. | — | — | — | — |
| ionmirrorshield2e | Shield | 0 | n.m. | — | — | — | — |
| deflectionshield3 | Shield | 0 | n.m. | — | — | — | — |
| towershield2e | Shield | 0 | n.m. | — | — | — | — |
| deflectionshield4 | Shield | 0 | n.m. | — | — | — | — |
| ionmirrorshield3e | Shield | 0 | n.m. | — | — | — | — |
| reflector | Shield | 0 | n.m. | — | — | — | — |
| deflectionshield5 | Shield | 0 | n.m. | — | — | — | — |
| towershield3e | Shield | 0 | n.m. | — | — | — | — |
| deflectionshield6 | Shield | 0 | n.m. | — | — | — | — |
| ionmirrorshield4e | Shield | 0 | n.m. | — | — | — | — |
| deflectionshield7 | Shield | 0 | n.m. | — | — | — | — |
| towershield4e | Shield | 0 | n.m. | — | — | — | — |
| ionmirrorshield5e | Shield | 0 | n.m. | — | — | — | — |
| deflectionshieldma | Shield | 0 | n.m. | — | — | — | — |
| poweredreflector | Shield | 0 | n.m. | — | — | — | — |
| deflectionshieldmgx | Shield | 0 | n.m. | — | — | — | — |
| ionmirrorshieldmg | Shield | 0 | n.m. | — | — | — | — |
| mastershield | Shield | 0 | n.m. | — | — | — | — |
| greatshield | Shield | 0 | n.m. | — | — | — | — |
| fg7handgatling | Bullet | 103 | PASS (15.8 / 15.9, -1 %) | PASS (21.03 / 20.00 par s) | PASS (7.98 / 8.00 s) | — | PASS (218 / 213) |
| galleonlightweightcannon | Bullet | 10 | PASS (78.8 / 79.3, -1 %) | PASS (1.51 / 1.50 par s) | PASS (4.00 / 4.00 s) | PASS (422 / 375 m/s) | PASS (91 / 91) |
| dexterautomaticshotgun | Bullet | 14 | PASS (186.5 / 199.4, -6 %) | PASS (2.22 / 2.20 par s) | PASS (4.00 / 4.00 s) | — | PASS (287 / 305) |
| flamerevolver | Flame | 113 | PASS (dégâts infligés) | PASS (20.06 / 20.00 par s) | — | — | — |
| fg8handgatling | Bullet | 166 | PASS (15.8 / 15.9, -1 %) | PASS (31.43 / 30.00 par s) | PASS (8.98 / 9.00 s) | — | PASS (327 / 320) |
| galleonrapidfirecannon | Bullet | 19 | PASS (62.0 / 62.6, -1 %) | PASS (3.03 / 3.00 par s) | PASS (4.98 / 5.00 s) | PASS (499 / 450 m/s) | PASS (136 / 137) |
| dexterautomaticshotgund1 | Bullet | 14 | PASS (543.5 / 581.0, -6 %) | PASS (2.31 / 2.30 par s) | PASS (5.28 / 5.30 s) | — | PASS (748 / 797) |
| flamerevolverd | Flame | 113 | PASS (dégâts infligés) | PASS (20.00 / 20.00 par s) | — | — | — |
| ut1handgatling | Bullet | 115 | PASS (47.5 / 47.8, -1 %) | PASS (20.92 / 20.00 par s) | PASS (9.98 / 10.00 s) | — | PASS (819 / 795) |
| galleonlightweightcannonm2 | Bullet | 10 | PASS (237.0 / 237.4, -0 %) | PASS (1.51 / 1.50 par s) | PASS (4.48 / 4.50 s) | — | PASS (267 / 266) |
| disruptor | Bullet | 448 | PASS (45.5 / 45.3, +0 %) | — | — | — | — |
| galleontripleburstcannon | Bullet | 15 | PASS (624.5 / 632.9, -1 %) | PASS (0.75 / 0.75 par s) | PASS (4.00 / 4.00 s) | — | PASS (969 / 982) |
| flamerevolverdc1 | Flame | 111 | PASS (dégâts infligés) | PASS (19.94 / 20.00 par s) | — | — | — |
| ut2handgatling | Bullet | 87 | PASS (105.0 / 105.7, -1 %) | PASS (15.64 / 15.00 par s) | PASS (9.98 / 10.00 s) | — | PASS (1417 / 1376) |
| galleonlightweightcannonm3 | Bullet | 13 | PASS (533.0 / 542.7, -2 %) | PASS (2.02 / 2.00 par s) | PASS (4.00 / 4.00 s) | — | PASS (767 / 775) |
| dexterautomaticshotgung | Bullet | 12 | PASS (1901.0 / 2079.8, -9 %) | PASS (1.91 / 1.90 par s) | PASS (6.88 / 6.90 s) | — | PASS (2412 / 2627) |
| fg10handgatling | Bullet | 105 | PASS (125.5 / 126.7, -1 %) | PASS (21.01 / 20.00 par s) | PASS (7.98 / 8.00 s) | — | PASS (1802 / 1758) |
| galleon3rburstcannon | Bullet | 28 | PASS (629.5 / 635.5, -1 %) | PASS (1.51 / 1.50 par s) | PASS (6.68 / 6.70 s) | — | PASS (1623 / 1631) |
| flamerevolverdc2 | Flame | 112 | PASS (dégâts infligés) | PASS (20.00 / 20.00 par s) | — | — | — |
| powerdisruptor | Bullet | 450 | PASS (113.0 / 113.2, -0 %) | — | — | — | — |
| dexterautomaticshotgunza | Bullet | 14 | PASS (4030.0 / 4076.2, -1 %) | PASS (2.22 / 2.20 par s) | PASS (5.68 / 5.70 s) | — | PASS (5865 / 5890) |
| ut3handgatling | Bullet | 116 | PASS (187.7 / 190.0, -1 %) | PASS (20.91 / 20.00 par s) | PASS (9.98 / 10.00 s) | — | PASS (3258 / 3177) |
| galleonarmorpiercingcannon | Bullet | 10 | PASS (1592.5 / 1620.8, -2 %) | PASS (1.51 / 1.50 par s) | PASS (4.00 / 4.00 s) | — | PASS (1921 / 1945) |
| maximumdisruptor | Bullet | 450 | PASS (181.2 / 181.2, +0 %) | — | — | — | — |
| fgzhandgatling | Bullet | 95 | PASS (316.0 / 316.4, -0 %) | PASS (21.12 / 20.00 par s) | PASS (10.98 / 11.00 s) | — | PASS (5152 / 4942) |
| hellflamerevolver | Flame | 110 | PASS (dégâts infligés) | PASS (20.00 / 20.00 par s) | — | — | — |
| dexterautomaticshotgunzgxe | Bullet | 16 | PASS (7615.2 / 8036.5, -5 %) | PASS (2.52 / 2.50 par s) | PASS (4.48 / 4.50 s) | — | PASS (13943 / 14612) |
| fgzfhandgatling | Bullet | 95 | PASS (471.5 / 474.3, -1 %) | PASS (21.12 / 20.00 par s) | PASS (12.00 / 12.00 s) | — | PASS (8074 / 7769) |
| nc101handcannon | Bullet | 12 | PASS (120.8 / 120.7, +0 %) | PASS (1.91 / 1.90 par s) | PASS (3.50 / 3.50 s) | — | PASS (156 / 155) |
| 30mmgallicheavycannon | Bullet | 2 | PASS (656.5 / 656.6, -0 %) | — | PASS (3.50 / 3.50 s) | — | PASS (194 / 194) |
| powerdyne | Bullet | 5 | PASS (845.0 / 845.0, +0 %) | PASS (0.75 / 0.75 par s) | — | — | — |
| ncs1cannonshot | Bullet | 3 | PASS (1301.5 / 1301.4, +0 %) | PASS (0.50 / 0.50 par s) | PASS (7.98 / 8.00 s) | — | PASS (279 / 279) |
| 30mmgallicheavycannonm2 | Bullet | 2 | PASS (1181.0 / 1181.0, +0 %) | — | PASS (3.50 / 3.50 s) | — | PASS (349 / 349) |
| 25mmlightcanistercannon | Bullet | 5 | PASS (771.2 / 771.2, +0 %) | PASS (0.75 / 0.75 par s) | PASS (9.98 / 10.00 s) | — | PASS (316 / 315) |
| nc102handcannon | Bullet | 12 | PASS (603.5 / 603.6, -0 %) | PASS (1.91 / 1.90 par s) | PASS (4.48 / 4.50 s) | — | PASS (734 / 730) |
| nc103handcannon | Bullet | 12 | PASS (724.2 / 724.3, -0 %) | PASS (1.91 / 1.90 par s) | PASS (3.50 / 3.50 s) | — | PASS (957 / 953) |
| 30mmgallicheavycannonm3 | Bullet | 2 | PASS (3000.0 / 3000.0, +0 %) | — | PASS (4.00 / 4.00 s) | — | PASS (875 / 875) |
| nc104handcannon | Bullet | 12 | PASS (965.8 / 965.7, +0 %) | PASS (1.91 / 1.90 par s) | PASS (3.20 / 3.20 s) | — | PASS (1222 / 1218) |
| ncs2cannonshot | Bullet | 3 | PASS (4344.0 / 4344.0, +0 %) | PASS (0.50 / 0.50 par s) | PASS (8.98 / 9.00 s) | — | PASS (870 / 869) |
| nc201handcannon | Bullet | 12 | PASS (1207.0 / 1207.1, -0 %) | PASS (1.91 / 1.90 par s) | PASS (4.68 / 4.70 s) | — | PASS (1511 / 1504) |
| 30mmlightcanistercannon | Bullet | 5 | PASS (2532.0 / 2532.0, +0 %) | PASS (0.75 / 0.75 par s) | PASS (12.00 / 12.00 s) | — | PASS (894 / 894) |
| nc202handcannon | Bullet | 10 | PASS (1810.8 / 1810.7, +0 %) | PASS (1.51 / 1.50 par s) | PASS (3.50 / 3.50 s) | — | PASS (2022 / 2012) |
| 35mmgallicheavycannon | Bullet | 2 | PASS (9000.0 / 9000.0, +0 %) | — | PASS (4.00 / 4.00 s) | — | PASS (2624 / 2624) |
| ncsscannonshot | Bullet | 4 | PASS (13517.0 / 13517.0, +0 %) | PASS (0.50 / 0.50 par s) | PASS (9.98 / 10.00 s) | — | PASS (3383 / 3379) |
| nc290handcannon | Bullet | 12 | PASS (2414.2 / 2414.2, +0 %) | PASS (1.91 / 1.90 par s) | PASS (3.50 / 3.50 s) | — | PASS (3252 / 3240) |
| 35mmbattlecannon | Bullet | 2 | PASS (15000.0 / 15000.0, +0 %) | — | PASS (5.98 / 6.00 s) | — | PASS (3971 / 3968) |
| fh10lightmortar | Mortar | 7 | PASS (90.5 / 90.6, -0 %) | PASS (1.83 / 1.80 par s) | PASS (4.98 / 5.00 s) | — | — |
| yh1dispersalmortar | Mortar | 2 | PASS (360.0 / 540.0, -33 %) | — | PASS (8.98 / 9.00 s) | — | — |
| fhd1heavymortar | Mortar | 1 | PASS (482.8 / 482.8, -0 %) | — | PASS (4.98 / 5.00 s) | — | — |
| yh2dispersalmortar | Mortar | 2 | PASS (630.0 / 1080.0, -42 %) | — | PASS (8.98 / 9.00 s) | — | — |
| fh11lightmortar | Mortar | 6 | PASS (311.5 / 311.4, +0 %) | PASS (1.83 / 1.80 par s) | PASS (5.28 / 5.30 s) | — | — |
| fhd2heavymortar | Mortar | 2 | PASS (1448.5 / 1448.5, +0 %) | — | PASS (4.98 / 5.00 s) | — | — |
| fh12lightmortar | Mortar | 8 | PASS (543.5 / 543.6, -0 %) | PASS (1.82 / 1.80 par s) | PASS (4.78 / 4.80 s) | — | — |
| fhd3heavymortar | Mortar | 2 | PASS (2414.2 / 2414.2, +0 %) | — | PASS (4.98 / 5.00 s) | — | — |
| fh21lightmortar | Mortar | 7 | PASS (792.8 / 792.7, +0 %) | PASS (1.83 / 1.80 par s) | PASS (5.98 / 6.00 s) | — | — |
| yh3dispersalmortar | Mortar | 3 | PASS (4560.0 / 8280.0, -45 %) | PASS (0.50 / 0.50 par s) | PASS (8.98 / 9.00 s) | — | — |
| fh22mortar | Mortar | 8 | PASS (1132.5 / 1132.5, +0 %) | PASS (1.81 / 1.80 par s) | PASS (5.18 / 5.20 s) | — | — |
| fherexplosionmortar | Mortar | 2 | PASS (3621.2 / 3621.3, -0 %) | — | PASS (4.68 / 4.70 s) | — | — |
| fh29mortar | Mortar | 10 | PASS (1698.8 / 1698.7, +0 %) | PASS (1.82 / 1.80 par s) | PASS (5.18 / 5.20 s) | — | — |
| yh7dispersalmortar | Mortar | 2 | PASS (26400.0 / 28800.0, -8 %) | — | PASS (9.98 / 10.00 s) | — | — |
| fhezsuperheavymortar | Mortar | 2 | PASS (5432.0 / 5432.0, +0 %) | — | PASS (4.68 / 4.70 s) | — | — |
| 38mmragecannon | Bullet | 2 | PASS (18000.0 / 18000.0, +0 %) | — | PASS (5.98 / 6.00 s) | — | PASS (4765 / 4762) |
| fh29remortar | Mortar | 12 | PASS (1698.8 / 1698.7, +0 %) | PASS (3.86 / 3.80 par s) | PASS (5.18 / 5.20 s) | — | — |
| superheavycrushermortar | Mortar | 1 | PASS (21728.0 / 21727.9, +0 %) | — | PASS (60.00 / 60.00 s) | — | — |
| armhound | Missile | 8 | PASS (96.5 / 96.6, -0 %) | — | PASS (2.50 / 2.50 s) | — | — |
| bloodstorm | Missile | 3 | PASS (1210.0 / 1208.0, +0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| leviathan | Missile | 1 | PASS (4828.5 / 4828.4, +0 %) | — | PASS (9.78 / 9.80 s) | — | — |
| haytalmultimissilelauncher | Missile | 2 | PASS (tir droit sans kit de guidage, salve dispersée) | — | PASS (4.88 / 4.90 s) | — | — |
| arcane6barrellauncher | Missile | 6 | PASS (301.8 / 301.8, -0 %) | — | PASS (6.78 / 6.80 s) | — | — |
| fg01highaltitudeimpactlauncher | Missile | 8 | PASS (328.2 / 328.3, -0 %) | — | PASS (8.18 / 8.20 s) | — | — |
| phoenix | Missile | 2 | PASS (2172.8 / 2172.8, -0 %) | — | PASS (3.30 / 3.30 s) | — | — |
| g2armhound | Missile | 22 | PASS (169.0 / 169.0, +0 %) | — | PASS (1.60 / 1.60 s) | — | — |
| haytalmultimissilelauncherm2 | Missile | 2 | PASS (tir droit sans kit de guidage, salve dispersée) | — | PASS (4.88 / 4.90 s) | — | — |
| m2bloodstorm | Missile | 3 | PASS (2112.5 / 2112.0, +0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| leviathanma | Missile | 1 | PASS (60355.2 / 60355.3, -0 %) | — | PASS (9.78 / 9.80 s) | — | — |
| fg02highaltitudeimpactlauncher | Missile | 14 | PASS (470.8 / 470.8, -0 %) | — | PASS (8.18 / 8.20 s) | — | — |
| phoenix2 | Missile | 2 | PASS (4828.5 / 4828.4, +0 %) | — | PASS (3.30 / 3.30 s) | — | — |
| arcane6barrellauncherg2 | Missile | 6 | PASS (1448.5 / 1448.5, +0 %) | — | PASS (6.78 / 6.80 s) | — | — |
| haytalmultimissilelauncherma | Missile | 2 | PASS (tir droit sans kit de guidage, salve dispersée) | — | PASS (4.88 / 4.90 s) | — | — |
| g3armhound | Missile | 24 | PASS (482.8 / 482.8, -0 %) | — | PASS (2.50 / 2.50 s) | — | — |
| wildleviathan | Missile | 1 | PASS (120711.0 / 120711.0, +0 %) | — | PASS (9.78 / 9.80 s) | — | — |
| phoenixx | Missile | 2 | PASS (9657.0 / 9656.9, +0 %) | — | PASS (3.30 / 3.30 s) | — | — |
| arcane6barrellauncherga | Missile | 12 | PASS (1086.5 / 1086.4, +0 %) | — | PASS (2.70 / 2.70 s) | — | — |
| fgxhighaltitudeimpactlauncher | Missile | 14 | PASS (1641.8 / 1641.7, +0 %) | — | PASS (8.18 / 8.20 s) | — | — |
| gxarmhound | Missile | 24 | PASS (965.8 / 965.7, +0 %) | — | PASS (2.50 / 2.50 s) | — | — |
| haytalmultimissilelauncherwx | Missile | 2 | PASS (tir droit sans kit de guidage, salve dispersée) | — | PASS (4.88 / 4.90 s) | — | — |
| phoenixwx | Missile | 2 | PASS (12071.0 / 12071.1, -0 %) | — | PASS (3.30 / 3.30 s) | — | — |
| arcane6barrellaunchergx | Missile | 12 | PASS (2172.8 / 2172.8, -0 %) | — | PASS (2.70 / 2.70 s) | — | — |
| m3bloodstorm | Missile | 3 | PASS (12070.0 / 12071.0, -0 %) | — | PASS (4.88 / 4.90 s) | — | — |
| rapidhound | Missile | 24 | PASS (724.2 / 724.3, -0 %) | — | PASS (2.50 / 2.50 s) | — | — |
| fg9handgatling | Bullet | 105 | PASS (71.0 / 71.8, -1 %) | PASS (21.01 / 20.00 par s) | PASS (7.98 / 8.00 s) | — | PASS (994 / 971) |
| fgsdiffusiongatling | Bullet | 165 | PASS (51.1 / 54.9, -7 %) | PASS (31.44 / 30.00 par s) | PASS (8.98 / 9.00 s) | — | PASS (1044 / 1086) |
| dexterautomaticshotgund2 | Bullet | 14 | PASS (888.5 / 945.8, -6 %) | PASS (2.31 / 2.30 par s) | PASS (5.28 / 5.30 s) | — | PASS (1223 / 1297) |
| fg20handgatling | Bullet | 111 | ÉCHEC (55.5 / 158.0, -65 %) | PASS (20.95 / 20.00 par s) | PASS (8.98 / 9.00 s) | — | ÉCHEC (871 / 2394) |
| galleonrapidfirecannonm2 | Bullet | 37 | ÉCHEC (aucun dégât) | PASS (6.03 / 6.00 par s) | PASS (7.68 / 7.70 s) | — | — |
| dexterautomaticshotgunzg | Bullet | 16 | PASS (4899.0 / 5036.0, -3 %) | PASS (2.52 / 2.50 par s) | PASS (4.48 / 4.50 s) | — | PASS (8970 / 9156) |

