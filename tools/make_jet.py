# Son du jetpack « énergie futuriste » (ailes d'énergie, pas de flamme) : bourdonnement grave, linéaire et discret.
# Usage : uv run --with numpy --with soundfile --with scipy python tools/make_jet.py assets/sfx
import sys, os
import numpy as np, soundfile as sf
from scipy.signal import butter, sosfilt

OUT = sys.argv[1]
SR = 44100
rng = np.random.default_rng(7)


def bp(x, lo, hi):
    return sosfilt(butter(2, [lo, hi], "bp", fs=SR, output="sos"), x)


def lp(x, hz):
    return sosfilt(butter(2, hz, "lp", fs=SR, output="sos"), x)


def osc(phase, harmonics=10, tilt=1.4):
    """Onde riche mais douce (harmoniques en 1/n^tilt) : timbre électrique sans la dureté d'une scie."""
    y = np.zeros_like(phase)
    for n in range(1, harmonics + 1):
        y += np.sin(n * phase) / n ** tilt
    return y


def hum(t, f0):
    """Moteur d'énergie discret : tenue linéaire, sans pulsation. Fréquences multiples de 0,25 Hz : un nombre entier de
    cycles en 4 s, la boucle se referme seule. f0 peut être un tableau (balayage de l'allumage / de la coupure)."""
    f0 = np.broadcast_to(f0, t.shape)
    ph = 2 * np.pi * np.cumsum(f0) / SR
    # Voix principale au timbre doux et une seconde à peine désaccordée (léger mouvement, volume quasi constant).
    base = osc(ph, 6, 2.2) * 0.85 + osc(ph * 92.25 / 92.0, 6, 2.2) * 0.15
    sub = np.sin(ph * 0.5) * 0.35
    y = lp(base, 900) + sub + np.sin(ph * 3.0) * 0.06
    return y


def periodic(x, f):
    """Applique le filtre f à un signal périodique de période len(x) : deux périodes filtrées, on garde la seconde."""
    n = len(x)
    return f(np.tile(x, 2))[n:]


def air(n):
    return periodic(rng.standard_normal(n), lambda v: lp(bp(v, 180, 1400), 900))


def norm(x, peak):
    return x * peak / np.abs(x).max()


def write(name, x):
    sf.write(os.path.join(OUT, f"{name}_0.wav"), x, SR, subtype="PCM_16")
    print(f"{name}: {len(x) / SR:.2f}s")


# --- Boucle de vol (4 s, sans couture) : bourdonnement grave et souffle feutré, constants ---
L = 4.0
n = int(L * SR)
# Le filtre passe-bas a un régime transitoire au début : on calcule 8 s et on garde les 4 dernières (même phase).
t2 = np.arange(2 * n) / SR
loop = hum(t2, 92.0)[n:] + air(n) * 0.35
write("jet_loop", norm(loop, 0.45))

# --- Allumage : montée douce en 0,3 s (légère montée en régime), puis tenue qui s'efface sous la boucle ---
d = 0.8
m = int(d * SR)
t = np.arange(m) / SR
f = 92.0 * (0.8 + 0.2 * (1 - np.exp(-t / 0.15)))
start = (hum(t, f) + air(m) * 0.35) * (1 - np.exp(-t / 0.1))
start[-int(0.4 * SR):] *= np.linspace(1, 0, int(0.4 * SR)) ** 2
write("jet_start", norm(start, 0.4))

# --- Coupure : légère descente en régime et extinction en douceur (0,5 s) ---
d = 0.5
m = int(d * SR)
t = np.arange(m) / SR
f = 92.0 * (1 - 0.25 * (t / d))
stop = (hum(t, f) + air(m) * 0.35) * (1 - t / d) ** 2
write("jet_stop", norm(stop, 0.35))

# --- Poussée (dash, piqué) : souffle feutré qui gonfle puis retombe ---
d = 0.6
m = int(d * SR)
t = np.arange(m) / SR
env = (1 - np.exp(-t / 0.05)) * np.exp(-t / 0.2)
boost = (hum(t, 92.0 * (1 + 0.15 * env)) * 0.6 + lp(bp(rng.standard_normal(m), 250, 2500), 1800) * 0.8) * env
write("jet_boost", norm(boost, 0.45))
