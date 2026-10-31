"""Candy looks: frosting, chocolate, waffle. A hexagonal donut: apothem 15, pointy at z (far/near), a small hex hole in the middle."""
import numpy as np
from scipy import ndimage
from floorlib import *


def mpp(f):
    return self_mpp(f)


# --- frosting ----------------------------------------------------------------------------------------------------
def frosting_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 6)
    pink = col(255, 190, 216)
    img = np.broadcast_to(pink, sh + (3,)).copy()
    # soft swirls: low-frequency tone only, so the cars stay clear
    img = img * (1 + 0.035 * noise(sh, 160, 4, 2) + 0.012 * noise(sh, 7, 5))[..., None]
    sw = np.sin(2 * np.pi * (np.hypot(X, Z) / 1.6 + 0.4 * noise(sh, 200, 6)))
    img = mix(img, col(255, 214, 230), np.clip(sw, 0, 1) * 0.35 * (0.3 + 0.7 * rw))
    # piping: shells along every edge (outer rim and the hole)
    d_, s, L = f['d'], f['s'], f['L']
    n = np.round(L / 1.15)
    frac = (s / L * n) % 1.0
    arc = L / n
    u = (frac - 0.5) * arc                       # metres along the piping, 0 at the middle of a dollop
    h = np.sqrt(np.clip(1 - (u / (arc * 0.5)) ** 2, 0, 1))
    reach = 0.2 + 1.05 * h                       # how far in from the edge this dollop reaches
    inside = sm(reach - d_, mpp(f))
    cream = col(255, 247, 224)
    ridge = 0.5 + 0.5 * np.cos(2 * np.pi * (frac * 1.0 + d_ * 1.1))
    shade = cream * (0.9 + 0.1 * ridge)[..., None]
    shade = mix(shade, col(236, 200, 190), np.clip(1 - h, 0, 1) * 0.5)
    shade = mix(shade, col(255, 255, 250), sm(0.07 - np.abs(d_ - (reach - 0.2)), mpp(f)) * 0.5 * h)
    outline = sm(0.04 - np.abs(d_ - reach), mpp(f))
    out = mix(img, shade, inside)
    out = mix(out, NAVY, outline * 0.85 * (reach > 0.2))
    # sprinkles, scattered more thickly toward the rim
    ov = Overlay(fp)
    rng = np.random.default_rng(8)
    colors = [(255, 255, 255), (255, 214, 60), (90, 170, 255), (255, 130, 70), (120, 210, 110), (190, 120, 240)]
    placed = 0
    tries = 0
    while placed < 340 and tries < 6000:
        tries += 1
        x = rng.uniform(fp.min_x, fp.max_x)
        z = rng.uniform(fp.min_z, fp.max_z)
        px, py = fp.px(x, z)
        pi, pj = int(py - fp.row0), int(px)
        if pi < 0 or pj < 0 or pi >= d_.shape[0] or pj >= d_.shape[1]:
            continue
        dd = d_[pi, pj]
        if dd < 1.3:
            continue
        if rng.random() > 0.3 + 0.7 * min(1.0, (1 - min(1.0, (dd - 1.3) / 7.0))):
            continue
        a = rng.uniform(0, np.pi)
        l = 0.19
        c = colors[int(rng.integers(len(colors)))]
        ov.line([(x - l * np.cos(a), z - l * np.sin(a)), (x + l * np.cos(a), z + l * np.sin(a))], c + (255,), 0.09)
        placed += 1
    return ov.composite(out)


def frosting_side(XM, YM):
    sponge = col(255, 228, 160)
    base = np.broadcast_to(sponge, XM.shape + (3,)).copy()
    crumb = hash2(np.floor(XM * 40), np.floor(YM * 40), 4)
    base = base * (1 - 0.06 * (crumb > 0.85))[..., None]
    # frosting with drips: a wavy lower edge, periodic over the repeat
    drip = 0.12 + 0.07 * np.sin(2 * np.pi * XM / 6 * 3 + 1) + 0.05 * np.sin(2 * np.pi * XM / 6 * 5)
    for c, w, depth in ((0.9, 0.17, 0.30), (2.6, 0.14, 0.2), (4.1, 0.2, 0.36), (5.3, 0.13, 0.17)):
        dx = np.minimum(np.abs(XM - c), 6 - np.abs(XM - c))
        drip = np.maximum(drip, depth * np.sqrt(np.clip(1 - (dx / w) ** 2, 0, 1)) + 0.05 * 0)
    pink = col(255, 160, 200)
    base = mix(base, pink, sm(drip - YM, 0.01))
    base = mix(base, col(255, 214, 232), sm(0.02 - np.abs(YM - 0.035), 0.01) * sm(drip - YM, 0.01) * 0.8)
    base = mix(base, NAVY, sm(0.016 - np.abs(YM - drip), 0.006) * 0.6)
    return base


# --- chocolate ---------------------------------------------------------------------------------------------------
def foil(img, f, width=0.95):
    d, s, L = f['d'], f['s'], f['L']
    sh = d.shape
    edge = width + 0.25 * noise(sh, 90, 21, 2)
    base = mix(col(232, 220, 188), col(246, 238, 214), 0.5 + 0.5 * noise(sh, 12, 22, 2))
    creases = np.abs(noise(sh, 28, 23, 2))
    base = base * (1 - 0.1 * np.clip(1 - creases * 5, 0, 1))[..., None]
    shadow = sm(0.05 - np.abs(d - edge), mpp(f))
    out = mix(img, base, sm(edge - d, mpp(f)))
    out = mix(out, col(120, 74, 44), shadow * 0.6)
    return out


def chocolate_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 7)
    cell = 2.2
    fx = ((X + 15) / cell) % 1.0
    fz = ((Z + 17.4) / cell) % 1.0
    milk = col(176, 118, 80)
    img = np.broadcast_to(milk, sh + (3,)).copy()
    img = img * (1 + 0.025 * noise(sh, 9, 3) + 0.02 * noise(sh, 70, 4))[..., None]
    gx = np.minimum(fx, 1 - fx) * cell
    gz = np.minimum(fz, 1 - fz) * cell
    edge = np.minimum(gx, gz)
    k = 0.4 + 0.6 * rw
    groove = sm(0.12 - edge, mpp(f))
    img = mix(img, col(116, 70, 46), groove * (0.5 + 0.5 * k))
    # a raised inner panel on each square: lit on its top/left, shaded on its bottom/right
    inner = (edge > 0.34) & (edge < 0.0 + 99)
    lx = (fx - 0.5) * cell
    lz = (fz - 0.5) * cell
    panel = np.minimum(0.82 - np.abs(lx), 0.82 - np.abs(lz))   # distance in from the panel's edge
    ring = sm(0.1 - np.abs(panel), mpp(f))
    lit = ((lz > lx * 0.0 - 0.0) ^ (lx > 0)).astype(np.float32)   # which side is lit
    top_lit = np.clip((lz - np.abs(lx)) * 0.9 + 0.5, 0, 1)         # lz up (away) is the lit side in the picture
    left_lit = np.clip((-lx - np.abs(lz)) * 0.9 + 0.5, 0, 1)
    hl = np.maximum(top_lit, left_lit)
    img = mix(img, col(214, 156, 112), ring * hl * 0.55 * k)
    img = mix(img, col(120, 74, 48), ring * (1 - hl) * 0.45 * k)
    return foil(img, f)


def chocolate_side(XM, YM):
    base = np.broadcast_to(col(150, 96, 62), XM.shape + (3,)).copy()
    base = base * (1 + 0.04 * np.sin(2 * np.pi * XM / 6 * 4))[..., None]
    base = mix(base, col(232, 176, 90), sm(0.045 - np.abs(YM - 0.27), 0.01))
    base = mix(base, col(238, 228, 200), sm(0.07 - YM, 0.01))
    base = mix(base, NAVY, sm(0.012 - np.abs(YM - 0.07), 0.005) * 0.6)
    return base


# --- waffle ------------------------------------------------------------------------------------------------------
def waffle_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 7)
    cell = 1.15
    fx = ((X + 15) / cell) % 1.0
    fz = ((Z + 17.4) / cell) % 1.0
    ridge_w = 0.1
    ex = np.minimum(fx, 1 - fx) * cell
    ez = np.minimum(fz, 1 - fz) * cell
    e = np.minimum(ex, ez)
    gold = col(250, 206, 120)
    pocket = col(238, 170, 84)
    k = 0.45 + 0.55 * rw
    t = sm(e - ridge_w, 0.05)         # 0 on the ridge, 1 inside a pocket
    base = mix(gold, mix(gold, pocket, 0.7 * k + 0.1), t)
    # pocket shading: darker toward the top-left inside edge (a pressed hollow)
    lx = (fx - 0.5)
    lz = (fz - 0.5)
    base = base * (1 - 0.10 * k * t * np.clip(1.0 - (0.5 + 0.9 * (lx - lz) * -1) * 0.0 - 0.0, 0, 1) * (0.5 + 0.5 * (lz - lx)))[..., None]
    base = base * (1 + 0.03 * noise(sh, 8, 3))[..., None]
    # syrup: pools that grow thick toward the rim, a thin trickle in the middle
    n = noise(sh, 190, 6, 3)
    thick = n * 0.55 + 0.95 * rw - 0.62
    pool = sm(thick, 0.05)
    amber = col(212, 112, 30)
    sy = amber * (1 + 0.07 * noise(sh, 40, 7))[..., None]
    gloss = sm(0.06 - np.abs(thick - 0.07), 0.015) * 0.0
    sy = mix(sy, col(250, 190, 90), np.clip(sm(0.14 - thick, 0.06) * (1 - sm(0.05 - thick, 0.03)), 0, 1) * 0.0)
    # bright rim of each pool on its lit side, and a white glint
    gx, gz_ = np.gradient(ndimage.gaussian_filter(pool, 3))
    glint = np.clip((-gx - gz_) * 40, 0, 1) * pool
    sy = mix(sy, col(255, 214, 140), glint * 0.7)
    out = mix(base, sy, pool * (0.55 + 0.45 * rw))
    out = mix(out, col(150, 70, 24), sm(0.04 - np.abs(thick), 0.012) * 0.7)
    # a thick syrup band right along the rim
    rimband = sm(0.4 + 0.2 * noise(sh, 70, 9, 2) - d, mpp(f))
    out = mix(out, sy, rimband)
    out = mix(out, col(255, 218, 150), rimband * sm(0.04 - np.abs(d - 0.22), mpp(f)) * 0.7)
    return out


def waffle_side(XM, YM):
    cell = 6.0 / 8
    fx = (XM / cell) % 1.0
    e = np.minimum(fx, 1 - fx) * cell
    gold = col(250, 206, 120)
    base = mix(gold, col(232, 160, 76), sm(e - 0.06, 0.03) * 0.5)
    drip = 0.1 + 0.05 * np.sin(2 * np.pi * XM / 6 * 4)
    for c, w, depth in ((1.2, 0.2, 0.34), (3.0, 0.16, 0.24), (4.4, 0.22, 0.38), (5.6, 0.14, 0.2)):
        dx = np.minimum(np.abs(XM - c), 6 - np.abs(XM - c))
        drip = np.maximum(drip, depth * np.sqrt(np.clip(1 - (dx / w) ** 2, 0, 1)))
    base = mix(base, col(212, 112, 30), sm(drip - YM, 0.01))
    base = mix(base, col(255, 214, 140), sm(0.015 - np.abs(YM - 0.03), 0.006) * 0.8 * sm(drip - YM, 0.01))
    base = mix(base, NAVY, sm(0.014 - np.abs(YM - drip), 0.006) * 0.6)
    return base


def register(d):
    d['candy_frosting'] = ('candy', frosting_top, frosting_side)
    d['candy_chocolate'] = ('candy', chocolate_top, chocolate_side)
    d['candy_waffle'] = ('candy', waffle_top, waffle_side)
