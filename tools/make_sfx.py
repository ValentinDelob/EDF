# Prépare les sons d'armes à partir des banques CC0 (Free Firearm Sound Library, Kenney Sci-Fi Sounds, explosions OGA).
# Usage : uv run --with numpy --with soundfile --with scipy python tools/make_sfx.py <banques extraites> <user://audio_dump (--dump-audio)> assets/sfx
# Les comptes de variantes et les gains de lecture sont recopiés dans scripts/Fx/Sfx.Recorded.cs.
# Le dossier de dump doit contenir les sons synthétisés d'origine (calage du volume) : sans lui, les volumes changent.
import sys, os, json
import numpy as np, soundfile as sf
from scipy.signal import resample_poly, butter, sosfilt

SND, OLD, OUT = sys.argv[1:4]
FIRE = os.path.join(SND, "fire", "x", "Prepared SFX Library")
KEN = None
for root, _, files in os.walk(os.path.join(SND, "kenney", "x")):
    if "laserLarge_000.ogg" in files: KEN = root
BOOM = os.path.join(SND, "boom")
SR = 44100
os.makedirs(OUT, exist_ok=True)
manifest = {}

def load(path):
    d, sr = sf.read(path, always_2d=True)
    x = d.mean(axis=1)
    if sr != SR:
        from math import gcd
        g = gcd(SR, sr)
        x = resample_poly(x, SR // g, sr // g)
    return x.astype(np.float64)

def pitch(x, f):
    # Changement de vitesse (hauteur + durée), comme un lecteur à vitesse variable.
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
    # Attaque nette : départ au premier échantillon significatif, micro fondu d'entrée.
    fi = int(0.0015 * SR)
    y[:fi] *= np.linspace(0, 1, fi)
    fo = int(len(y) * fade)
    y[-fo:] *= np.linspace(1, 0, fo) ** 2
    return y

def hp(x, hz):
    return sosfilt(butter(2, hz, "hp", fs=SR, output="sos"), x)

def lp(x, hz):
    return sosfilt(butter(2, hz, "lp", fs=SR, output="sos"), x)

def rms(x, win=0.25):
    seg = x[: int(win * SR)]
    return np.sqrt(np.mean(seg ** 2)) + 1e-9

def old_rms(name):
    p = os.path.join(OLD, f"sfx_{name}.wav")
    if not os.path.exists(p): p = os.path.join(OLD, f"sfx_{name}_0.wav")
    return rms(load(p)) if os.path.exists(p) else None

def finish(name, clips, gain_db=0.0):
    """Cale le volume sur l'ancien son (équilibre du mixage conservé), limite les crêtes, écrit name_i.wav."""
    ref = old_rms(name)
    out = []
    for c in clips:
        c = c - np.mean(c[: int(0.01 * SR)]) * 0  # pas de décalage continu
        # Volume calé sur l'ancien son, sans jamais écraser l'attaque (crête plafonnée à 0,95).
        k = (ref / rms(c)) * 10 ** (gain_db / 20) if ref else 0.7 / np.abs(c).max()
        c = c * min(k, 0.95 / np.abs(c).max())
        out.append(c)
    for i, c in enumerate(out):
        sf.write(os.path.join(OUT, f"{name}_{i}.wav"), c, SR, subtype="PCM_16")
    manifest[name] = len(out)
    print(f"{name}: {len(out)} variante(s), {len(out[0]) / SR:.2f}s, crête {np.abs(out[0]).max():.2f}")

def shots(rel, length, n=None, thr=0.25, f=1.0, hpf=None, fade=0.35):
    x = load(os.path.join(FIRE, rel))
    xs = [cut(x, o, length * f, fade) for o in onsets(x, thr)][:n]
    xs = [pitch(c, f) for c in xs]
    if hpf: xs = [hp(c, hpf) for c in xs]
    return xs

def ken(name, f=1.0):
    return pitch(load(os.path.join(KEN, name + ".ogg")), f)

def mix(*parts):
    n = max(len(p) for p, _ in parts)
    y = np.zeros(n)
    for p, g in parts: y[: len(p)] += p * g
    return y

# --- Armes à feu (enregistrements « near distance » pour le joueur) ---
finish("rifle", shots("AR-15/D_32P.wav", 0.95) + shots("AK-47/C_28P.wav", 0.95, 2))
finish("rifle_far", (shots("AR-15/D_24P.wav", 1.1) + shots("AK-47/C_31P.wav", 1.1, 2))[:4])
finish("smg", shots("Carl Gustav M45/G_31P.wav", 0.6))
finish("shotgun", shots("Nova/O_21P.wav", 1.5) + shots("CD/H_21P.wav", 1.5))
finish("sniper", shots("Mosin Nagant/M_21P.wav", 2.4), gain_db=1.5)
finish("minigun", shots("PPSh/P_30P.wav", 0.32, 4, f=1.12, fade=0.6))

# --- Mécanique d'arme : la pompe du Winchester Model 12 (clic-clac du réarmement entre deux tirs) ---
m12 = load(os.path.join(FIRE, "Model 12/K_22P.wav"))
rack = cut(m12, int(4.18 * SR), 0.85, 0.3, 0)
finish("reload", [hp(rack, 250)], gain_db=2)
finish("empty", [hp(cut(m12, int(4.24 * SR), 0.12, 0.6, 0), 600)])

# --- Armes lourdes et explosions ---
nova = shots("Nova/O_21P.wav", 0.9, 1)[0]
thr0 = ken("thrusterFire_000")
def whoosh(n): return cut(lp(ken(n), 6000), 0, 1.5, 0.5, 0)
finish("rocket", [mix((pitch(nova, 0.62)[: int(1.5 * SR)], 0.9), (whoosh("thrusterFire_000"), 0.8)), mix((pitch(nova, 0.55)[: int(1.5 * SR)], 0.9), (whoosh("thrusterFire_002"), 0.8))])
finish("launcher", [lp(c, 2500) for c in shots("Mossberg/N_30P.wav", 0.55, 2, f=0.6)])
chunky = load(os.path.join(BOOM, "chunky.mp3"))
low = [ken(f"lowFrequency_explosion_00{i}") for i in range(2)]
start = onsets(chunky, 0.2)[0]
ch = cut(chunky, start, 3.2, 0.5)
finish("explosion", [mix((ch, 1.0), (low[0], 0.7)), mix((pitch(ch, 0.88), 1.0), (low[1], 0.7))])
finish("explosion_small", [mix((ken(f"explosionCrunch_00{i}"), 1.0), (lp(pitch(ch, 1.25)[: int(1.4 * SR)], 3000), 0.5)) for i in range(4)])
muf = load(os.path.join(BOOM, "muffled.wav"))
far = load(os.path.join(BOOM, "far.mp3"))
finish("collapse", [mix((cut(muf, onsets(muf, 0.2)[0], 3.5, 0.5), 1.0), (cut(far, onsets(far, 0.2)[0], 3.5, 0.5), 0.7))])

# --- Armes à énergie (Kenney Sci-Fi) ---
finish("laser", [ken(f"laserLarge_00{i}") for i in range(5)])
finish("zap", [mix((ken(f"laserSmall_00{i}"), 1.0), (ken(f"forceField_00{i}")[: int(0.35 * SR)], 0.6)) for i in range(4)])
finish("plasma", [ken(f"laserLarge_00{i}", 0.7) for i in range(3)])
finish("hammer", [mix((ken(f"impactMetal_00{i}"), 1.0), (low[i % 2][: int(0.6 * SR)], 0.8)) for i in range(4)])

# Jetpack : voir tools/make_jet.py.
