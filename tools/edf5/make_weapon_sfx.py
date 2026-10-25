# Sons de tir des familles d'armes d'EDF 5 (Ranger), depuis les banques CC0 (The Free Firearm Sound Library « Prepared SFX
# Library », Kenney Sci-Fi Sounds) : pour chaque famille, l'arme réelle la plus proche, découpée, filtrée, transposée et
# éventuellement doublée d'une couche (grave, énergie, souffle), puis calée en volume sur le son actuel du même type
# (assets/sfx/rifle_0.wav, shotgun_0.wav…) pour garder l'équilibre du mixage.
# Usage : uv run --with numpy --with soundfile --with scipy python tools/edf5/make_weapon_sfx.py <banques extraites>
# (<banques>/fire/x/Prepared SFX Library, <banques>/kenney/x/Audio). Sortie : assets/sfx/edf5/<famille>_<i>.wav.
import sys, os
import numpy as np, soundfile as sf
from scipy.signal import resample_poly, butter, sosfilt

SND = sys.argv[1]
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FIRE = os.path.join(SND, "fire", "x", "Prepared SFX Library")
KEN = os.path.join(SND, "kenney", "x", "Audio")
REF = os.path.join(ROOT, "assets", "sfx")
OUT = os.path.join(REF, "edf5")
SR = 44100
os.makedirs(OUT, exist_ok=True)


def load(path):
    d, sr = sf.read(path, always_2d=True)
    x = d.mean(axis=1)
    if sr != SR:
        from math import gcd
        g = gcd(SR, sr)
        x = resample_poly(x, SR // g, sr // g)
    return x.astype(np.float64)


def pitch(x, f):
    if abs(f - 1) < 1e-3: return x
    n = int(len(x) / f)
    return np.interp(np.arange(n) * f, np.arange(len(x)), x)


def onsets(x, thr=0.25):
    hop = int(SR * 0.01)
    env = np.array([np.abs(x[i:i + hop]).max() for i in range(0, len(x), hop)])
    pk = env.max()
    return [i * hop for i in range(1, len(env)) if env[i] > thr * pk and env[i - 1] < 0.5 * env[i]]


def cut(x, start, length, fade=0.35, pre=0.004):
    s = max(0, int(start - pre * SR))
    y = x[s:s + int(length * SR)].copy()
    fi = int(0.0015 * SR)
    y[:fi] *= np.linspace(0, 1, fi)
    fo = int(len(y) * fade)
    y[-fo:] *= np.linspace(1, 0, fo) ** 2
    return y


def hp(x, hz): return sosfilt(butter(2, hz, "hp", fs=SR, output="sos"), x)
def lp(x, hz): return sosfilt(butter(2, hz, "lp", fs=SR, output="sos"), x)
def rms(x, win=0.25): return np.sqrt(np.mean(x[: int(win * SR)] ** 2)) + 1e-9


def mix(*parts):
    n = max(len(p) for p, _ in parts)
    y = np.zeros(n)
    for p, g in parts: y[: len(p)] += p * g
    return y


def shots(rel, length, n=2, thr=0.25, f=1.0):
    x = load(os.path.join(FIRE, rel))
    xs = [cut(x, o, length * f) for o in onsets(x, thr)][:n]
    return [pitch(c, f) for c in xs]


def ken(name, f=1.0, length=None):
    x = pitch(load(os.path.join(KEN, name + ".ogg")), f)
    return cut(x, 0, length, 0.4, 0) if length else x


def thump(freq, length=0.5, decay=0.12):
    t = np.arange(int(length * SR)) / SR
    return np.sin(2 * np.pi * freq * t * (1 - 0.35 * t / length)) * np.exp(-t / decay)


def finish(fam, clips, ref, gain_db=0.0):
    r = rms(load(os.path.join(REF, f"{ref}_0.wav")))
    for i, c in enumerate(clips):
        k = r / rms(c) * 10 ** (gain_db / 20)
        c = c * min(k, 0.95 / np.abs(c).max())
        sf.write(os.path.join(OUT, f"{fam}_{i}.wav"), c, SR, subtype="PCM_16")
    print(f"{fam}: {len(clips)} variante(s), {len(clips[0]) / SR:.2f}s")


AR, AK, SMG, SKS = "AR-15/D_32P.wav", "AK-47/C_28P.wav", "Carl Gustav M45/G_31P.wav", "SKS/U_14P.wav"
NOVA, M12, CD, MOSS = "Nova/O_21P.wav", "Model 12/K_22P.wav", "CD/H_21P.wav", "Mossberg/N_30P.wav"
TIKKA, SAV, MOSIN, SPR, ARI = "Tikka/W_29P.wav", "Savage 10 .300 Blackout/T_27P.wav", "Mosin Nagant/M_21P.wav", "1917/B_24P.wav", "Arisaka/E_25P.wav"
PPQ, PPSH = "Walther PPQ/X_39P.wav", "PPSh/P_30P.wav"

# ── Fusils d'assaut ──
finish("pa11", shots(AR, 0.95), "rifle")
finish("raven", [hp(c, 180) for c in shots(SMG, 0.6, f=1.06)], "rifle", -1)
finish("minion", [mix((c, 1.0), (thump(70, 0.5, 0.1), 0.5), (ken("explosionCrunch_000", 1.6, 0.4), 0.35)) for c in shots(AK, 0.9, f=0.9)], "rifle", 1)
finish("slugger", shots(AK, 0.95), "rifle")
finish("orchid", [mix((c, 1.0), (ken("laserSmall_001", 0.8, 0.3), 0.4)) for c in shots(SKS, 0.9)], "rifle")
finish("gm", shots(SKS, 1.0), "rifle", 1)
finish("gm64", shots(ARI, 1.2, f=1.05), "rifle", 1.5)
finish("stork", [hp(c, 120) for c in shots(AR, 0.9, f=0.94)], "rifle")
finish("stork4", [lp(c, 7000) for c in shots(AR, 0.9, f=1.04)], "rifle")
finish("slade", [mix((c, 1.0), (ken("laserRetro_002", 0.7, 0.35), 0.45)) for c in shots(AK, 0.9, f=0.95)], "rifle")
finish("blazer", [mix((ken(f"laserLarge_00{i}", 0.65), 1.0), (thump(55, 0.6, 0.18), 0.5)) for i in (1, 3)], "laser", 2)
# ── Fusils à pompe ──
finish("slaughter", shots(NOVA, 1.5), "shotgun")
finish("sgn", shots(M12, 1.3, f=1.05), "shotgun")
finish("herring", shots(CD, 1.6), "shotgun")
finish("breecher", [mix((c, 1.0), (thump(48, 0.6, 0.15), 0.45)) for c in shots(MOSS, 1.5, f=0.82)], "shotgun", 1)
finish("monsoon", [hp(c, 150) for c in shots(NOVA, 1.0, f=1.15)], "shotgun", -1)
finish("slugshot", shots(MOSS, 1.6, f=0.95), "shotgun", 1)
finish("bustershot", [mix((c, 1.0), (ken("lowFrequency_explosion_000", 1.0, 1.2), 0.6)) for c in shots(CD, 1.6, f=0.8)], "shotgun", 2)
finish("fracture", [mix((c, 0.8), (ken(f"forceField_00{i}", 1.1, 0.6), 0.6)) for i, c in enumerate(shots(NOVA, 1.2))], "shotgun")
# ── Fusils de précision ──
finish("kff", shots(TIKKA, 2.4), "sniper")
finish("np", shots(SAV, 2.0), "sniper")
finish("lysander", [mix((c, 1.0), (thump(42, 0.9, 0.25), 0.6)) for c in shots(MOSIN, 2.6, f=0.8)], "sniper", 2)
finish("fang", shots(MOSIN, 2.4), "sniper")
finish("dunkel", shots(SPR, 2.4, f=1.05), "sniper")
finish("eagle", shots(ARI, 2.2), "sniper")
finish("flounders", [hp(c, 150) for c in shots(SKS, 1.4, f=0.95)], "sniper", -1)
finish("ptarmigan", [mix((c, 1.0), (thump(36, 1.1, 0.3), 0.7)) for c in shots(SPR, 2.8, f=0.72)], "sniper", 2.5)
finish("pylon", [mix((c, 0.9), (ken("laserLarge_002", 0.8, 0.8), 0.6)) for c in shots(TIKKA, 2.2)], "sniper", 1)
finish("penetrator", [mix((ken(f"laserLarge_00{i}", 0.55), 1.0), (thump(40, 0.8, 0.2), 0.6), (ken("forceField_002", 0.7, 0.8), 0.4)) for i in (0, 4)], "sniper", 2)
# ── Lance-roquettes et lance-missiles ──
nova = shots(NOVA, 0.9, 1)[0]
def rocket(f_body, f_whoosh, k=0):
    return mix((pitch(nova, f_body)[: int(1.5 * SR)], 0.9), (cut(lp(ken(f"thrusterFire_00{k}", f_whoosh), 6000), 0, 1.5, 0.5, 0), 0.8))
finish("grant", [rocket(0.62, 1.0, 0), rocket(0.6, 1.0, 1)], "rocket")
finish("leopard", [rocket(0.66, 1.1, 2), rocket(0.64, 1.1, 3)], "rocket")
finish("goliath", [mix((rocket(0.5, 0.85, 0), 1.0), (thump(38, 0.9, 0.25), 0.6)), mix((rocket(0.48, 0.85, 1), 1.0), (thump(36, 0.9, 0.25), 0.6))], "rocket", 1.5)
finish("cascade", [rocket(0.8, 1.3, 2), rocket(0.82, 1.3, 4)], "rocket", -1.5)
finish("hornet", [rocket(0.72, 1.2, 1), rocket(0.7, 1.2, 3)], "rocket", -0.5)
finish("volcano", [rocket(0.76, 1.25, 0), rocket(0.78, 1.25, 4)], "rocket", -1)
finish("ibis", [rocket(0.68, 1.15, 4), rocket(0.66, 1.15, 2)], "rocket")
def missile(k, f=1.0):
    return mix((cut(ken(f"thrusterFire_00{k}", f), 0, 1.2, 0.5, 0), 1.0), (pitch(nova, 0.9)[: int(0.4 * SR)], 0.35))
finish("emerald", [missile(1, 1.2), missile(3, 1.2)], "rocket", -2)
finish("mlra", [missile(2, 1.6), missile(4, 1.6)], "rocket", -4)
finish("fork", [missile(0, 1.0), missile(2, 1.0)], "rocket", -1)
finish("falcon", [mix((missile(1, 1.3), 1.0), (ken("laserSmall_003", 0.9, 0.3), 0.4)), mix((missile(3, 1.3), 1.0), (ken("laserSmall_004", 0.9, 0.3), 0.4))], "rocket", -2)
finish("tortoise", [mix((ken("spaceEngineLarge_000", 0.9, 1.6), 1.0), (thump(45, 0.8, 0.2), 0.6)), mix((ken("spaceEngineLarge_002", 0.9, 1.6), 1.0), (thump(42, 0.8, 0.2), 0.6))], "rocket")
finish("prominence", [mix((ken("spaceEngineLarge_001", 0.75, 2.0), 1.0), (thump(34, 1.2, 0.3), 0.7)), mix((ken("spaceEngineLarge_003", 0.75, 2.0), 1.0), (thump(32, 1.2, 0.3), 0.7))], "rocket", 1)
# ── Lance-grenades et lancers ──
def launch(f, l=0.55): return [lp(c, 2500) for c in shots(MOSS, l, 2, f=f)]
finish("um", launch(0.6), "launcher")
finish("brute", [mix((c, 1.0), (pitch(c, 1.06), 0.7), (pitch(c, 0.94), 0.6)) for c in launch(0.58, 0.6)], "launcher", 1)
finish("crumble", [mix((c, 1.0), (pitch(c, 1.05), 0.7)) for c in launch(0.55, 0.6)], "launcher", 1)
finish("stampede", launch(0.7, 0.45), "launcher", -1)
finish("splash", [mix((c, 1.0), (ken("forceField_001", 1.4, 0.4), 0.3)) for c in launch(0.5, 0.7)], "launcher")
finish("sticky", launch(0.52, 0.65), "launcher")
finish("napalm", [mix((c, 1.0), (cut(ken("thrusterFire_001", 0.7), 0, 0.9, 0.5, 0), 0.5)) for c in launch(0.55, 0.6)], "launcher")
finish("geyser", [mix((c, 1.0), (cut(ken("thrusterFire_003", 0.6), 0, 1.2, 0.5, 0), 0.6)) for c in launch(0.5, 0.7)], "launcher", 1)
finish("firecracker", [mix((c, 1.0), (ken("explosionCrunch_002", 2.0, 0.3), 0.3)) for c in launch(0.9, 0.4)], "launcher")
whoosh = [hp(cut(ken(f"forceField_00{i}", 1.8), 0, 0.35, 0.6, 0), 900) for i in (0, 3)]
for fam in ("mg", "dng", "boundgren", "cbomb", "impulse"):
    finish(fam, whoosh, "launcher", -10)
# ── Armes spéciales ──
finish("acid", [mix((ken(f"slime_00{i}", 1.1), 1.0), (cut(ken("thrusterFire_002", 1.6), 0, 0.4, 0.5, 0), 0.3)) for i in (0, 1)], "launcher", -3)
finish("flamer", [cut(lp(ken(f"thrusterFire_00{i}", 0.75), 4000), 0, 0.9, 0.5, 0) for i in (1, 3)], "launcher", -4)
finish("flamecannon", [mix((cut(lp(ken(f"thrusterFire_00{i}", 0.6), 3000), 0, 1.0, 0.5, 0), 1.0), (thump(50, 0.5, 0.15), 0.4)) for i in (0, 2)], "launcher", -2)
finish("boundgun", [mix((c, 1.0), (ken("impactMetal_001", 2.2, 0.15), 0.3)) for c in shots(PPQ, 0.5, f=1.0)], "rifle", -2)
finish("reverser", [ken(f"forceField_00{i}", 1.3, 0.6) for i in (1, 4)], "laser", -6)
finish("revshooter", [mix((ken(f"forceField_00{i}", 0.9, 0.7), 1.0), (pitch(nova, 0.8)[: int(0.4 * SR)], 0.3)) for i in (2, 3)], "laser", -3)

# ═════════════ Wing Diver : armes à énergie (Kenney Sci-Fi, couches graves et souffle) ═════════════
def zap(i, f, l=0.4, g=1.0): return cut(ken(f"laserSmall_00{i}", f), 0, l, 0.5, 0) * g
def big(i, f, l=0.9): return cut(ken(f"laserLarge_00{i}", f), 0, l, 0.5, 0)
def field(i, f, l=0.6): return cut(ken(f"forceField_00{i}", f), 0, l, 0.5, 0)
finish("rapier", [mix((zap(0, 1.6, 0.25), 1.0), (field(1, 2.0, 0.25), 0.4)), mix((zap(2, 1.7, 0.25), 1.0), (field(3, 2.0, 0.25), 0.4))], "laser", -4)
finish("phalanx", [mix((field(0, 1.4, 0.4), 1.0), (zap(1, 1.3, 0.3), 0.6)), mix((field(2, 1.4, 0.4), 1.0), (zap(3, 1.3, 0.3), 0.6))], "laser", -3)
finish("powerlance", [mix((big(0, 0.9, 0.8), 1.0), (thump(70, 0.4, 0.1), 0.5)), mix((big(2, 0.9, 0.8), 1.0), (thump(65, 0.4, 0.1), 0.5))], "laser", 1)
finish("dragoon", [mix((big(1, 0.65, 1.1), 1.0), (thump(45, 0.8, 0.2), 0.7)), mix((big(3, 0.65, 1.1), 1.0), (thump(42, 0.8, 0.2), 0.7))], "laser", 2)
finish("monsterlance", [mix((big(4, 0.55, 1.2), 1.0), (thump(40, 0.9, 0.25), 0.7), (field(4, 0.8, 0.8), 0.4)) for _ in (0, 1)], "laser", 2)
finish("sparkvine", [mix((zap(i, 0.8, 0.45), 1.0), (ken("impactMetal_002", 2.5, 0.15), 0.25)) for i in (0, 4)], "laser", -2)
finish("sparkwhip", [mix((zap(i, 0.7, 0.5), 1.0), (zap(i + 1, 0.9, 0.5), 0.6)) for i in (1, 2)], "laser", -2)
finish("magblaster", [mix((big(i, 1.3, 0.35), 1.0), (field(i, 1.5, 0.3), 0.3)) for i in (0, 2)], "laser", -6)
finish("destroyer", [mix((big(i, 1.1, 0.4), 1.0), (zap(i, 0.9, 0.3), 0.6)) for i in (1, 3)], "laser", -5)
finish("spine", [mix((big(i, 1.5, 0.3), 1.0), (zap(i, 1.8, 0.2), 0.4)) for i in (2, 4)], "laser", -6)
finish("reflectron", [mix((big(i, 1.2, 0.4), 1.0), (field(i, 2.2, 0.3), 0.5)) for i in (0, 3)], "laser", -5)
finish("lightningbow", [mix((zap(i, 0.6, 0.6), 1.0), (field(i, 1.2, 0.5), 0.4)) for i in (2, 3)], "laser", -3)
finish("boltgun", [mix((zap(i, 0.55, 0.5), 1.0), (thump(90, 0.3, 0.08), 0.4)) for i in (0, 1)], "laser", -1)
finish("crossbow", [mix((zap(i, 0.5, 0.6), 1.0), (zap(i + 2, 0.6, 0.6), 0.7), (thump(80, 0.4, 0.1), 0.4)) for i in (0, 1)], "laser")
finish("pulsemg", [mix((cut(ken(f"laserRetro_00{i}", 1.3), 0, 0.3, 0.5, 0), 1.0), (thump(110, 0.2, 0.05), 0.4)) for i in (0, 2)], "laser", -4)
finish("stardust", [mix((cut(ken(f"laserRetro_00{i}", 0.9), 0, 0.6, 0.5, 0), 1.0), (field(i, 1.0, 0.5), 0.5), (thump(70, 0.4, 0.1), 0.4)) for i in (1, 3)], "laser")
finish("pulsebuster", [mix((cut(ken(f"laserRetro_00{i}", 1.1), 0, 0.4, 0.5, 0), 1.0), (thump(95, 0.25, 0.06), 0.4)) for i in (3, 4)], "laser", -3)
finish("closedlaser", [mix((big(i, 1.6, 0.4), 1.0), (field(i, 2.5, 0.3), 0.3)) for i in (1, 4)], "laser", -5)
finish("boltshooter", [mix((zap(i, 0.45, 0.8), 1.0), (thump(60, 0.5, 0.15), 0.5)) for i in (3, 4)], "laser", 1)
finish("monster", [mix((big(i, 0.75, 1.0), 1.0), (thump(50, 0.7, 0.2), 0.6)) for i in (0, 2)], "laser", 1)
finish("raijin", [mix((zap(i, 0.35, 1.2), 1.0), (thump(38, 1.0, 0.3), 0.8), (field(i, 0.6, 1.0), 0.5)) for i in (1, 2)], "laser", 3)
def plasma_shot(i, f, l=0.9, low=60):
    return mix((cut(ken(f"laserLarge_00{i}", f), 0, l, 0.5, 0), 1.0), (thump(low, l, 0.18), 0.6), (cut(ken(f"thrusterFire_00{i}", 1.4), 0, l, 0.5, 0), 0.3))
finish("plasmacannon", [plasma_shot(0, 0.6), plasma_shot(2, 0.6)], "plasma", 1)
finish("plasmafall", [plasma_shot(1, 0.5, 1.0, 50), plasma_shot(3, 0.5, 1.0, 50)], "plasma", 1)
finish("bigcannon", [plasma_shot(4, 0.45, 1.2, 40), plasma_shot(1, 0.45, 1.2, 40)], "plasma", 2)
finish("heavycannon", [plasma_shot(2, 0.4, 1.4, 34), plasma_shot(4, 0.4, 1.4, 34)], "plasma", 3)
finish("burstcannon", [plasma_shot(3, 0.7, 0.7), plasma_shot(0, 0.7, 0.7)], "plasma")
finish("grit", [plasma_shot(1, 1.0, 0.35, 90), plasma_shot(3, 1.0, 0.35, 90)], "plasma", -4)
finish("splasher", [plasma_shot(2, 0.8, 0.6, 80), plasma_shot(4, 0.8, 0.6, 80)], "plasma", -2)
finish("blinkball", [hp(field(i, 1.8, 0.3), 900) for i in (0, 2)], "launcher", -10)
for fam, f in (("mirage", 1.8), ("geist", 0.9), ("ghostchaser", 1.3), ("fenrir", 2.0), ("psiblade", 1.0)):
    finish(fam, [mix((zap(i, f, 0.3), 1.0), (field(i, f * 1.2, 0.25), 0.4)) for i in (1, 3)], "laser", -6)
for fam, f in (("diffuser", 1.0), ("gleipnir", 1.2), ("thundercluster", 0.8), ("heavensgate", 0.7), ("psicluster", 0.9), ("finaldays", 0.5)):
    finish(fam, [mix((field(i, f, 0.8), 1.0), (thump(70, 0.5, 0.15), 0.4)) for i in (0, 4)], "laser", -3)
