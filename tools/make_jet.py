# Son du jetpack « énergie futuriste » (ailes d'énergie, pas de flamme) : bourdonnement électrique pulsé, propre et moderne.
# Usage : uv run --with numpy --with soundfile --with scipy python tools/make_jet.py <dossier des sons Kenney Sci-Fi> assets/sfx
import sys, os
import numpy as np, soundfile as sf
from scipy.signal import butter, sosfilt

KEN, OUT = sys.argv[1:3]
SR = 44100
rng = np.random.default_rng(7)


def bp(x, lo, hi):
    return sosfilt(butter(2, [lo, hi], "bp", fs=SR, output="sos"), x)


def lp(x, hz):
    return sosfilt(butter(2, hz, "lp", fs=SR, output="sos"), x)


def ken(name, f=1.0):
    d, sr = sf.read(os.path.join(KEN, name + ".ogg"), always_2d=True)
    x = d.mean(axis=1)
    n = int(len(x) / f)
    return np.interp(np.arange(n) * f, np.arange(len(x)), x)


def osc(phase, harmonics=10, tilt=1.4):
    """Onde riche mais douce (harmoniques en 1/n^tilt) : timbre électrique sans la dureté d'une scie."""
    y = np.zeros_like(phase)
    for n in range(1, harmonics + 1):
        y += np.sin(n * phase) / n ** tilt
    return y


def hum(t, f0):
    """Moteur d'énergie. Fréquences multiples de 0,25 Hz : un nombre entier de cycles en 4 s, la boucle se referme seule.
    f0 peut être un tableau (balayage de l'allumage / de la coupure)."""
    f0 = np.broadcast_to(f0, t.shape)
    ph = 2 * np.pi * np.cumsum(f0) / SR
    k = f0 / 92.0
    base = osc(ph) * 0.55 + osc(ph * 92.5 / 92.0) * 0.45            # deux voix désaccordées : battement lent
    sub = np.sin(ph * 0.5) * 0.5                                     # assise grave
    mid = np.sin(ph * 3.0) * 0.18 + np.sin(ph * 4.0 + 0.7) * 0.12    # corps
    # Scintillement aigu (cristallin), modulé à 6 Hz.
    shimmer = (np.sin(ph * 2048.0 / 92.0) + 0.6 * np.sin(ph * 3072.25 / 92.0)) * (0.5 + 0.5 * np.sin(2 * np.pi * 6.0 * t))
    # Pulsation de la poussée (16 Hz) : le cœur « électrique » du son.
    pulse = 1.0 - 0.32 * (0.5 + 0.5 * np.sin(2 * np.pi * 16.0 * t)) ** 2
    y = (lp(base, 2400) * 0.7 + sub * 0.55 + mid) * pulse + shimmer * 0.05 * k
    return y


def periodic(x, f):
    """Applique le filtre f à un signal périodique de période len(x) : deux périodes filtrées, on garde la seconde."""
    n = len(x)
    return f(np.tile(x, 2))[n:]


def crackle(n):
    """Grésillement électrique : bruit aigu découpé en petites étincelles irrégulières (périodique sur n)."""
    z = periodic(rng.standard_normal(n), lambda v: bp(v, 3500, 9000))
    gate = np.zeros(n)
    for _ in range(int(n / SR * 22)):
        p = rng.integers(0, n)
        L = rng.integers(200, 1600)
        idx = (p + np.arange(L)) % n
        gate[idx] += np.exp(-np.linspace(0, 5, L))
    gate = periodic(gate, lambda v: np.convolve(v, np.ones(64) / 64, "same"))
    return z * gate


def air(n):
    return periodic(rng.standard_normal(n), lambda v: bp(v, 500, 2600))


def norm(x, peak):
    return x * peak / np.abs(x).max()


def write(name, x):
    sf.write(os.path.join(OUT, f"{name}_0.wav"), x, SR, subtype="PCM_16")
    print(f"{name}: {len(x) / SR:.2f}s")


# --- Boucle de vol (4 s, sans couture) ---
L = 4.0
n = int(L * SR)
t = np.arange(n) / SR
# Le filtre passe-bas a un régime transitoire au début : on calcule 8 s et on garde les 4 dernières (même phase).
t2 = np.arange(2 * n) / SR
loop = (hum(t2, 92.0)[n:]) + crackle(n) * 0.10 + air(n) * 0.10
write("jet_loop", norm(loop, 0.55))

# --- Allumage : montée en régime (balayage 35 → 92 Hz en 0,4 s), éclat d'énergie Kenney, puis tenue ---
d = 1.0
m = int(d * SR)
t = np.arange(m) / SR
f = 92.0 * (0.38 + 0.62 * (1 - np.exp(-t / 0.13)))
start = hum(t, f) * np.minimum(1, t / 0.06)
ff = ken("forceField_001", 0.85)[:m]
start[: len(ff)] += norm(ff, 1.0) * 0.55
chirp = np.sin(2 * np.pi * np.cumsum(600 + 2600 * np.clip(t / 0.3, 0, 1)) / SR) * np.exp(-t / 0.12) * 0.25
start += chirp
start[-int(0.35 * SR):] *= np.linspace(1, 0, int(0.35 * SR)) ** 2
write("jet_start", norm(start, 0.7))

# --- Coupure : descente en régime (92 → 30 Hz) et extinction en 0,55 s ---
d = 0.6
m = int(d * SR)
t = np.arange(m) / SR
f = 92.0 * (1 - 0.68 * (t / d) ** 0.8)
stop = hum(t, f) * (1 - t / d) ** 1.8
write("jet_stop", norm(stop, 0.5))

# --- Poussée (dash, piqué) : éclat d'énergie, balayage montant et souffle ---
d = 0.6
m = int(d * SR)
t = np.arange(m) / SR
f = 92.0 * (1.0 + 0.9 * np.clip(t / 0.12, 0, 1))
boost = hum(t, f) * np.exp(-t / 0.22)
ff2 = ken("forceField_002", 1.1)[:m]
boost[: len(ff2)] += norm(ff2, 1.0) * 0.7
boost += bp(rng.standard_normal(m), 900, 5000) * np.exp(-t / 0.1) * 0.35
boost *= np.minimum(1, t / 0.008)
write("jet_boost", norm(boost, 0.8))
