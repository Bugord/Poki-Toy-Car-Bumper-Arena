"""Sandbox looks: rake, molds, bricks. A circle of radius 15; the crumbling rings end at r = 13, 11, 9, 7, 5."""
import numpy as np
from scipy import ndimage
from floorlib import *


def mpp(f):
    return self_mpp(f)


def wood_planks_rim(img, f, seed, count=28, width=0.95):
    """Planks round the rim of a circle (arclength param s): base colour, grain along the rim, seams between planks."""
    d, s, L = f['d'], f['s'], f['L']
    sh = d.shape
    n = count
    frac = (s / L * n) % 1.0
    idx = np.floor(s / L * n)
    tone = hash2(idx, np.zeros_like(idx), seed)
    base = mix(mix(col(214, 156, 94), col(196, 138, 80), tone), col(224, 170, 108), (tone > 0.7) * 0.6)
    grain = np.sin(2 * np.pi * (d * 5.5 + 1.4 * noise(sh, (90, 30), seed + 3, 2)))
    base = base * (1 + 0.05 * grain)[..., None]
    arc = L / n
    seam = np.minimum(frac, 1 - frac) * arc
    seam_a = sm(0.05 - seam, mpp(f))
    base = mix(base, col(120, 74, 46), seam_a * 0.9)
    inner = sm(0.075 - np.abs(d - width), mpp(f))
    base = mix(base, NAVY, inner)
    # nails
    for nail in (0.18,):
        pass
    inside = sm(width - d, mpp(f))
    return mix(img, base, inside)


def plank_side(XM, YM, tones=((214, 156, 94), (196, 138, 80), (224, 170, 108), (204, 146, 86)), count=4):
    p = 6.0 / count
    idx = np.floor(XM / p)
    t = np.asarray(tones, np.float32)[(idx.astype(int)) % len(tones)] / 255.0
    ph = np.sin(2 * np.pi * (YM * 16 + 0.5 * np.sin(2 * np.pi * XM / 6 * 3 + idx)))
    img = t * (1 + 0.05 * ph)[..., None]
    img = img * (1 + 0.1 * (0.5 - YM / 0.5))[..., None] * 0.9 + 0.0
    joint = np.abs(((XM / p) % 1.0) - 0.0)
    joint = np.minimum(joint, 1 - joint) * p
    img = mix(img, NAVY, sm(0.03 - joint, 0.006))
    for nx in (0.15, p - 0.15):
        for ny in (0.12, 0.38):
            q = np.hypot(((XM % p) - nx), YM - ny)
            img = mix(img, NAVY, sm(0.025 - q, 0.006) * 0.8)
    img = mix(img, NAVY, sm(YM - 0.465, 0.01) * 0.0)
    return img


# --- rake --------------------------------------------------------------------------------------------------------
def rake_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    r = np.hypot(X, Z)
    sh = r.shape
    rw = rim_weight(f, 9)
    img = np.broadcast_to(col(238, 214, 152), sh + (3,)).copy()
    img *= (1 + 0.03 * noise(sh, 6, 2) + 0.02 * noise(sh, 30, 5))[..., None]
    rr = r + 0.5 * noise(sh, 300, 3, 2)
    ridge = np.cos(2 * np.pi * rr / 0.34)
    group = np.clip(np.cos(2 * np.pi * rr / 2.3 + 1.0) * 1.8 + 0.1, 0, 1)
    brk = np.clip(noise(sh, (150, 150), 4, 2) * 1.6 + 0.9, 0, 1)
    amp = (0.3 + 0.7 * rw) * 0.15
    img *= (1 + amp * ridge * group * brk)[..., None]
    # a few pebbles and shells near the rim
    ov = Overlay(fp)
    rng = np.random.default_rng(11)
    for i in range(14):
        a = rng.random() * 2 * np.pi
        rad = rng.uniform(9.0, 13.2)
        x, z = rad * np.cos(a), rad * np.sin(a)
        sz = rng.uniform(0.16, 0.3)
        ov.ellipse(x, z, sz * 1.2, sz, (186, 180, 172, 255), (29, 43, 79, 255), 0.035)
        ov.ellipse(x - sz * 0.3, z + sz * 0.25, sz * 0.35, sz * 0.22, (232, 228, 220, 255))
    img = ov.composite(img)
    return wood_planks_rim(img, f, 21)


# --- molds -------------------------------------------------------------------------------------------------------
def star(cx, cz, R, rot):
    pts = []
    for i in range(10):
        rad = R if i % 2 == 0 else R * 0.46
        a = rot + i * np.pi / 5
        pts.append((cx + rad * np.cos(a), cz + rad * np.sin(a)))
    return pts


def heart(cx, cz, R, rot):
    pts = []
    for i in range(40):
        t = i / 40 * 2 * np.pi
        x = 16 * np.sin(t) ** 3
        y = 13 * np.cos(t) - 5 * np.cos(2 * t) - 2 * np.cos(3 * t) - np.cos(4 * t)
        x, y = x / 17.0 * R, y / 17.0 * R
        pts.append((cx + x * np.cos(rot) - y * np.sin(rot), cz + x * np.sin(rot) + y * np.cos(rot)))
    return pts


def shell(cx, cz, R, rot):
    pts = [(cx, cz)]
    for i in range(0, 41):
        a = rot - 1.35 + i / 40 * 2.7
        rad = R * (1 + 0.07 * np.cos(i * 0.9 * 2.0))
        pts.append((cx + rad * np.cos(a), cz + rad * np.sin(a)))
    return pts


def imprints(fp, f, img, items, depth=0.16):
    """Presses the shapes into the sand: darker inside, a highlight and a shadow on the rims."""
    ov = Overlay(fp)
    detail = Overlay(fp)
    for kind, x, z, size, rot in items:
        if kind == 'star':
            ov.poly(star(x, z, size, rot), (255, 255, 255, 255))
        elif kind == 'heart':
            ov.poly(heart(x, z, size, rot), (255, 255, 255, 255))
        elif kind == 'shell':
            ov.poly(shell(x, z, size, rot), (255, 255, 255, 255))
            for k in range(-3, 4):
                a = rot + k * 0.34
                detail.line([(x, z), (x + size * 0.95 * np.cos(a), z + size * 0.95 * np.sin(a))], (255, 255, 255, 255), 0.045)
        elif kind == 'flower':
            for k in range(6):
                a = rot + k * np.pi / 3
                ov.ellipse(x + size * 0.55 * np.cos(a), z + size * 0.55 * np.sin(a), size * 0.4, size * 0.4, (255, 255, 255, 255))
            ov.ellipse(x, z, size * 0.4, size * 0.4, (255, 255, 255, 255))
            detail.ellipse(x, z, size * 0.22, size * 0.22, (255, 255, 255, 255))
        elif kind == 'foot':
            c, sn = np.cos(rot), np.sin(rot)
            def at(u, v):
                return x + u * c - v * sn, z + u * sn + v * c
            ox, oz = at(0, 0)
            # sole: a tilted oval, heel: smaller circle, five toes
            for (u, v, a, b) in ((0.0, 0.0, size * 0.34, size * 0.5), (0.0, -size * 0.62, size * 0.24, size * 0.22)):
                ex, ez = at(u, v)
                ov.ellipse(ex, ez, a, b, (255, 255, 255, 255))
            for k in range(5):
                tx, tz = at(-size * 0.28 + k * size * 0.14, size * (0.62 - 0.05 * abs(k - 1.5)))
                ov.ellipse(tx, tz, size * 0.07, size * 0.075, (255, 255, 255, 255))
    m = np.asarray(ov.img)[..., 3].astype(np.float32) / 255.0
    ln = np.asarray(detail.img)[..., 3].astype(np.float32) / 255.0
    b = ndimage.gaussian_filter(m, 2.0)
    gz, gx = np.gradient(b)
    shade = -(gx + gz) * 5.0
    rw = np.clip(0.4 + 0.6 * rim_weight(f, 9), 0, 1)
    out = img * (1 - depth * m * rw[..., None][..., 0][..., None] * 0 - 0)[..., ] if False else img
    out = img * (1 - depth * b * rw)[..., None]
    out = out * (1 + 0.5 * shade * rw)[..., None]
    out = out * (1 - 0.14 * ndimage.gaussian_filter(ln, 1.5) * m * rw)[..., None]
    edge = np.clip(4 * b * (1 - b), 0, 1)
    out = mix(out, col(150, 108, 66), edge * 0.35 * rw)
    return out


def molds_items():
    rng = np.random.default_rng(5)
    items = []
    kinds = ['star', 'shell', 'heart', 'flower', 'shell', 'star']
    tries = 0
    while len(items) < 22 and tries < 4000:
        tries += 1
        a = rng.random() * 2 * np.pi
        rad = 13.4 * np.sqrt(rng.random())
        if rng.random() > 0.2 + 0.8 * (rad / 13.4) ** 1.3:
            continue
        x, z = rad * np.cos(a), rad * np.sin(a)
        if any(np.hypot(x - ix, z - iz) < 2.6 for _, ix, iz, _, _ in items):
            continue
        items.append((kinds[len(items) % len(kinds)], x, z, rng.uniform(0.75, 1.1), rng.uniform(0, 6.28)))
    # a trail of footprints walking in from the rim
    a0 = 2.4
    for i in range(9):
        t = i / 8
        rad = 13.0 - 9.5 * t
        a = a0 + 0.55 * np.sin(t * 3.0)
        x, z = rad * np.cos(a), rad * np.sin(a)
        heading = np.arctan2(-np.sin(a), -np.cos(a)) + np.pi / 2 - 0.0
        side = 0.34 if i % 2 else -0.34
        px, pz = -np.sin(a) * side, np.cos(a) * side
        items.append(('foot', x + px, z + pz, 0.8, np.arctan2(-np.sin(a), -np.cos(a)) - np.pi / 2))
    return items


def rope_rim(img, f, width=0.8):
    d, s, L = f['d'], f['s'], f['L']
    n = np.round(L / 0.5)
    t = (s / L * n + d / width * 0.9) % 1.0
    strand = 0.5 + 0.5 * np.sin(2 * np.pi * t)
    base = mix(col(158, 112, 66), col(236, 204, 140), strand ** 0.8)
    # rounded across: darker at both edges of the rope
    across = 1 - np.abs(d / width * 2 - 1) ** 2
    base = base * (0.78 + 0.22 * across)[..., None]
    base = mix(base, NAVY, sm(0.07 - np.abs(d - width), mpp(f)))
    return mix(img, base, sm(width - d, mpp(f)))


def molds_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    r = np.hypot(X, Z)
    sh = r.shape
    img = np.broadcast_to(col(244, 220, 160), sh + (3,)).copy()
    img *= (1 + 0.03 * noise(sh, 6, 12) + 0.02 * noise(sh, 40, 13))[..., None]
    img = imprints(fp, f, img, molds_items())
    return rope_rim(img, f)


def rope_side(XM, YM):
    t = (XM / 0.42 + YM / 0.5 * 0.9) % 1.0
    strand = 0.5 + 0.5 * np.sin(2 * np.pi * t)
    base = mix(col(158, 112, 66), col(236, 204, 140), strand ** 0.8)
    across = 1 - np.abs(YM / 0.5 * 2 - 1) ** 2
    base = base * (0.8 + 0.2 * across)[..., None]
    return mix(base, NAVY, sm(YM - 0.475, 0.008) + sm(0.025 - YM, 0.008))


# --- bricks ------------------------------------------------------------------------------------------------------
def bricks_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    r = np.hypot(X, Z)
    sh = r.shape
    th = np.arctan2(Z, X)
    rw = rim_weight(f, 8)
    row = np.floor(15.0 - r)                      # rows 1 m wide: the crumble rings (2 m) are two rows each
    row_frac = (15.0 - r) % 1.0
    rmid = 15.0 - row - 0.5
    count = np.maximum(5, np.round(2 * np.pi * np.maximum(rmid, 0.3) / 1.7))
    u = th / (2 * np.pi) * count + 0.5 * (row % 2)
    brick = np.floor(u)
    ufrac = u - brick
    tone = hash2(brick, row, 7)
    palettes = np.array([(224, 168, 110), (214, 152, 98), (232, 182, 124), (206, 146, 96)], np.float32) / 255.0
    pick = (tone * 4).astype(int) % 4
    base = palettes[pick]
    wet = np.clip((7.5 - r) / 4.0, 0, 1) ** 1.2
    base = mix(base, base * np.array([0.78, 0.8, 0.88], np.float32), wet)
    # bevel: lighter along the top (outer) edge of a brick, darker along the bottom
    base = base * (1 + 0.07 * (0.5 - row_frac) * (0.3 + 0.7 * rw)[..., None][..., 0])[..., None]
    base = base * (1 + 0.04 * noise(sh, 7, 8))[..., None]
    arc = 2 * np.pi * np.maximum(rmid, 0.3) / count
    mortar_w = 0.075
    mu = np.minimum(ufrac, 1 - ufrac) * arc
    mv = np.minimum(row_frac, 1 - row_frac)
    mort = np.maximum(sm(mortar_w - mu, mpp(f)), sm(mortar_w - mv, mpp(f)))
    mort_col = mix(col(247, 232, 200), col(190, 160, 130), wet)
    contrast = 0.35 + 0.65 * rw
    base = mix(base, mix(base, mort_col, 0.9), mort)
    # darker pencil line inside the mortar toward the rim
    line = np.maximum(sm(0.022 - np.abs(mu - mortar_w), mpp(f)), sm(0.022 - np.abs(mv - mortar_w), mpp(f)))
    base = mix(base, NAVY, line * 0.18 * rw * (1 - mort))
    base = mix(base, base * 0.0 + 0.5 * (base + 0.5), 0.0)
    img = mix(np.broadcast_to(col(236, 214, 160), sh + (3,)), base, 1.0)
    # reduce brick/mortar contrast in the middle (keep cars readable)
    flat = np.broadcast_to(np.array([226, 190, 140], np.float32) / 255, sh + (3,)) * (1 - 0.14 * wet)[..., None]
    img = mix(flat, img, 0.45 + 0.55 * rw)
    # top course: a pale cap stone along the rim
    cap = sm(0.5 - d, mpp(f))
    capc = mix(col(244, 232, 206), col(226, 210, 180), hash2(np.floor(th * 40), np.zeros_like(th), 3))
    img = mix(img, capc, cap)
    img = mix(img, NAVY, sm(0.06 - np.abs(d - 0.5), mpp(f)))
    return img


def bricks_side(XM, YM):
    row = (YM >= 0.25).astype(int)
    u = (XM / 0.75 + 0.5 * row) % 1.0
    brick = np.floor(XM / 0.75 + 0.5 * row)
    tone = hash2(brick, row, 9)
    pal = np.array([(224, 168, 110), (214, 152, 98), (232, 182, 124), (206, 146, 96)], np.float32) / 255.0
    base = pal[(tone * 4).astype(int) % 4]
    mu = np.minimum(u, 1 - u) * 0.75
    mv = np.minimum(YM % 0.25, 0.25 - YM % 0.25)
    mort = np.maximum(sm(0.03 - mu, 0.006), sm(0.03 - mv, 0.006))
    base = mix(base, col(247, 232, 200), mort * 0.9)
    return mix(base, NAVY, sm(YM - 0.48, 0.006) * 0.8)


def register(d):
    d['sandbox_rake'] = ('sandbox', rake_top, plank_side)
    d['sandbox_molds'] = ('sandbox', molds_top, rope_side)
    d['sandbox_bricks'] = ('sandbox', bricks_top, bricks_side)
