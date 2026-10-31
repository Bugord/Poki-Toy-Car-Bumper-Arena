"""Tabletop looks: board, cloth, notebook. A rounded square, 21 m across (x and z from -10.5 to 10.5)."""
import numpy as np
from scipy import ndimage
from floorlib import *


def mpp(f):
    return self_mpp(f)


# --- board: a cutting board of glued planks -----------------------------------------------------------------------
def board_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 5)
    plank_w = 1.05
    idx = np.floor((X + 10.5) / plank_w)
    local = ((X + 10.5) / plank_w) % 1.0
    tones = np.array([(236, 196, 138), (222, 174, 112), (230, 186, 124), (240, 204, 148)], np.float32) / 255.0
    tone = (hash2(idx, np.zeros_like(idx), 4) * 4).astype(int) % 4
    base = tones[tone]
    # grain: long streaks along z, each plank its own
    g = noise(sh, (14, 260), 7, 3)
    g2 = np.sin(2 * np.pi * (X * 5.0 + 2.2 * g + idx * 0.37))
    base = base * (1 + 0.035 * g2 + 0.03 * g)[..., None]
    # a few darker streaks
    streak = np.clip(noise(sh, (8, 400), 9, 2) - 0.45, 0, 1) * 1.6
    base = base * (1 - 0.18 * streak * (0.35 + 0.65 * rw))[..., None]
    seam = np.minimum(local, 1 - local) * plank_w
    base = mix(base, col(150, 100, 60), sm(0.03 - seam, mpp(f)) * (0.35 + 0.4 * rw))
    # end-grain bevel at the rim
    bevel = sm(0.42 - d, mpp(f))
    base = mix(base, col(244, 214, 164), bevel * 0.55)
    base = mix(base, col(160, 108, 64), sm(0.04 - np.abs(d - 0.42), mpp(f)) * 0.7)
    # juice groove: a rounded square line inset from the rim
    gd = np.abs(d - 1.55)
    base = mix(base, col(176, 120, 72), sm(0.17 - gd, mpp(f)) * 0.85)
    base = mix(base, col(250, 224, 176), sm(0.05 - np.abs(d - 1.40), mpp(f)) * 0.6)
    # cup rings and knife scratches
    ov = Overlay(fp)
    rng = np.random.default_rng(3)
    for (x, z, r, a0) in ((-6.2, 5.8, 0.62, 0.4), (6.9, -2.2, 0.55, 2.0), (3.1, 7.4, 0.5, 4.0), (-4.6, -7.2, 0.6, 5.2),
                          (7.6, 6.2, 0.45, 1.0)):
        for k in range(2):
            rad = r + k * 0.1
            pts = [(x + rad * np.cos(a0 + t), z + rad * np.sin(a0 + t)) for t in np.linspace(0, 5.2 - 0.6 * k, 30)]
            ov.line(pts, (150, 98, 58, 110 - 30 * k), 0.05)
    for i in range(16):
        x, z = rng.uniform(-8.5, 8.5, 2)
        a = rng.uniform(0, np.pi)
        l = rng.uniform(0.5, 1.4)
        ov.line([(x, z), (x + l * np.cos(a), z + l * np.sin(a))], (255, 240, 210, 140), 0.025)
    # a hanging hole (a stadium slot) near the far edge: a darker pill
    for sx in (-1,):
        pass
    img = ov.composite(base)
    return img


def board_side(XM, YM):
    idx = np.floor(XM / 0.5)
    tones = np.array([(236, 196, 138), (222, 174, 112), (230, 186, 124), (240, 204, 148)], np.float32) / 255.0
    base = tones[(hash2(idx, np.zeros_like(idx), 5) * 4).astype(int) % 4]
    base = base * (1 + 0.04 * np.sin(2 * np.pi * (YM * 24 + 0.3 * np.sin(XM * 2 * np.pi / 6 * 2))))[..., None]
    seam = np.minimum((XM / 0.5) % 1.0, 1 - (XM / 0.5) % 1.0) * 0.5
    base = mix(base, col(150, 100, 60), sm(0.012 - seam, 0.004) * 0.7)
    return mix(base, NAVY, sm(YM - 0.482, 0.006) * 0.0)


# --- cloth: red and white checkered tablecloth with a lace edge ---------------------------------------------------
def lace_band(img, f, base_white, fill_red=None, width=1.35):
    d, s, L = f['d'], f['s'], f['L']
    n = np.round(L / 0.9)
    frac = (s / L * n) % 1.0
    arc = L / n
    # scalloped inner edge
    scallop = width - 0.38 * (1 - np.abs(np.sin(np.pi * frac))) ** 0.5 * 0 - 0.32 * (0.5 - 0.5 * np.cos(2 * np.pi * frac)) * -1
    scallop = width + 0.18 * np.cos(2 * np.pi * frac) * -1 - 0.0
    scallop = 1.05 + 0.3 * np.abs(np.sin(np.pi * frac))
    lace = sm(scallop - d, mpp(f))
    # eyelets: two rows of little holes
    u = (frac - 0.5) * arc
    holes = np.zeros_like(d)
    for (dd, off) in ((0.38, 0.0), (0.82, 0.45)):
        fr2 = ((s / L * n * 2 + off) % 1.0 - 0.5) * (arc / 2)
        q = np.hypot(fr2, d - dd)
        holes = np.maximum(holes, sm(0.095 - q, mpp(f)))
    lace_col = np.broadcast_to(base_white, d.shape + (3,)).copy()
    # woven shading
    lace_col = lace_col * (1 - 0.04 * np.sin(2 * np.pi * d / 0.09))[..., None]
    outline = sm(0.045 - np.abs(d - scallop), mpp(f))
    out = mix(img, lace_col, lace)
    out = mix(out, col(214, 128, 132), holes * lace * 0.75)
    out = mix(out, NAVY, outline * 0.8)
    return out


def cloth_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 7)
    cell = 1.3
    bx = np.abs(((X + 10.5) / cell) % 1.0 - 0.5) < 0.25
    bz = np.abs(((Z + 10.5) / cell) % 1.0 - 0.5) < 0.25
    # soft band edges
    ax = sm(0.25 - np.abs(((X + 10.5) / cell) % 1.0 - 0.5), 0.02)
    az = sm(0.25 - np.abs(((Z + 10.5) / cell) % 1.0 - 0.5), 0.02)
    white = col(254, 250, 244)
    strong = 0.35 + 0.65 * rw
    light_red = mix(white, col(238, 120, 128), 0.45 * strong)
    dark_red = mix(white, col(222, 70, 84), 0.9 * strong * 0.85 + 0.1)
    img = np.broadcast_to(white, sh + (3,)).copy()
    img = mix(img, light_red, np.maximum(ax, az))
    img = mix(img, dark_red, ax * az)
    # woven threads
    weave = np.sin(2 * np.pi * X / 0.07) * np.sin(2 * np.pi * Z / 0.07)
    img = img * (1 + 0.025 * weave)[..., None] * (1 + 0.02 * noise(sh, 20, 3))[..., None]
    # a few stains and a crease
    return lace_band(img, f, col(255, 252, 246))


def cloth_side(XM, YM):
    cell = 6.0 / 5
    ax = sm(0.25 - np.abs((XM / cell) % 1.0 - 0.5), 0.03)
    white = col(254, 250, 244)
    base = mix(np.broadcast_to(white, XM.shape + (3,)), col(232, 90, 100), ax * 0.85)
    # lace hem: white scallops along the bottom
    sc = 0.36 + 0.1 * np.abs(np.sin(np.pi * ((XM / 0.375) % 1.0)))
    base = mix(base, white, sm(YM - sc, 0.008))
    base = mix(base, NAVY, sm(0.012 - np.abs(YM - sc), 0.005) * 0.6)
    base = base * (1 - 0.05 * np.sin(2 * np.pi * XM / 0.05))[..., None] * 1.0
    return base


# --- notebook: ruled paper with a margin and doodles --------------------------------------------------------------
def spiral(cx, cz, r, turns=2.2, rot=0.0):
    pts = []
    for i in range(60):
        t = i / 59
        a = rot + t * turns * 2 * np.pi
        rad = r * (0.12 + 0.88 * t)
        pts.append((cx + rad * np.cos(a), cz + rad * np.sin(a)))
    return pts


def starp(cx, cz, R, rot=0.0):
    pts = []
    for i in range(11):
        rad = R if i % 2 == 0 else R * 0.42
        a = rot + i * np.pi / 5 * 1.0
        pts.append((cx + rad * np.cos(a), cz + rad * np.sin(a)))
    # pencil star: one stroke, point to point
    order = [0, 4, 8, 2, 6, 0]
    return [(cx + R * np.cos(rot + k * 2 * np.pi / 5 * 2 + np.pi / 2), cz + R * np.sin(rot + k * 2 * np.pi / 5 * 2 + np.pi / 2)) for k in range(6)]


def heartp(cx, cz, R, rot=0.0):
    pts = []
    for i in range(41):
        t = i / 40 * 2 * np.pi
        x = 16 * np.sin(t) ** 3 / 17.0 * R
        y = (13 * np.cos(t) - 5 * np.cos(2 * t) - 2 * np.cos(3 * t) - np.cos(4 * t)) / 17.0 * R
        pts.append((cx + x * np.cos(rot) - y * np.sin(rot), cz + x * np.sin(rot) + y * np.cos(rot)))
    return pts


def notebook_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 5)
    paper = col(255, 251, 236)
    img = np.broadcast_to(paper, sh + (3,)).copy()
    img = img * (1 + 0.015 * noise(sh, 8, 2) + 0.01 * noise(sh, 50, 3))[..., None]
    # blue rules every 0.95 m, a double red margin on the left
    rule = np.abs(((Z + 10.5) / 0.95) % 1.0 - 0.0)
    rule = np.minimum(rule, 1 - rule) * 0.95
    img = mix(img, col(138, 184, 236), sm(0.034 - rule, mpp(f)) * (0.55 + 0.35 * rw))
    for mx in (-6.5, -6.3):
        img = mix(img, col(238, 98, 104), sm(0.028 - np.abs(X - mx), mpp(f)) * 0.75)
    # punched holes on the left edge, with a soft shadow ring
    ov = Overlay(fp)
    for z in (-6.0, 0.0, 6.0):
        ov.ellipse(-9.3, z, 0.38, 0.38, (232, 224, 205, 255), (29, 43, 79, 200), 0.035)
        ov.ellipse(-9.27, z - 0.03, 0.31, 0.31, (196, 184, 160, 255))
    # tear-off perforation all round the rim
    d_, s, L = f['d'], f['s'], f['L']
    n = np.round(L / 0.36)
    fr = ((s / L * n) % 1.0 - 0.5) * (L / n)
    perf = sm(0.055 - np.hypot(fr, d_ - 0.5), mpp(f))
    img = mix(img, col(214, 206, 188), perf * 0.9)
    pencil = (46, 60, 100, 235)
    red = (230, 80, 90, 235)
    orange = (246, 150, 60, 235)
    blue = (70, 130, 220, 235)
    green = (80, 170, 100, 235)
    w = 0.07
    # doodles, kept near the rim so the middle stays quiet
    ov.line(starp(-3.2, 8.2, 0.7, 0.1), orange, w)
    ov.line(heartp(2.6, 8.4, 0.62, 0.1), red, w)
    ov.line(spiral(7.6, 7.6, 0.8), blue, w)
    ov.line(spiral(-7.8, -7.9, 0.7, 2.6, 1.0), green, w)
    ov.line(starp(5.2, -8.4, 0.62, 0.5), red, w)
    ov.line(heartp(-3.8, -8.5, 0.55, 0.3), blue, w)
    # little car
    cx, cz = 8.3, -3.0
    ov.line([(cx - 0.9, cz), (cx - 0.9, cz + 0.35), (cx - 0.45, cz + 0.4), (cx - 0.25, cz + 0.8), (cx + 0.35, cz + 0.8),
             (cx + 0.6, cz + 0.4), (cx + 0.95, cz + 0.35), (cx + 0.95, cz), (cx - 0.9, cz)], pencil, w)
    ov.ellipse(cx - 0.5, cz - 0.02, 0.2, 0.2, (255, 251, 236, 255), pencil, 0.06)
    ov.ellipse(cx + 0.5, cz - 0.02, 0.2, 0.2, (255, 251, 236, 255), pencil, 0.06)
    # sun
    sx, sz = -8.0, 4.2
    ov.ellipse(sx, sz, 0.42, 0.42, (255, 251, 236, 0), orange, 0.07)
    for k in range(8):
        a = k * np.pi / 4
        ov.line([(sx + 0.62 * np.cos(a), sz + 0.62 * np.sin(a)), (sx + 0.92 * np.cos(a), sz + 0.92 * np.sin(a))], orange, 0.06)
    # zigzag and arrows
    ov.line([(-1.0 + i * 0.28, -9.1 + (0.22 if i % 2 else 0)) for i in range(9)], pencil, 0.06)
    ov.line([(4.0, 8.9), (5.4, 8.9)], blue, 0.07)
    ov.line([(5.1, 9.1), (5.4, 8.9), (5.1, 8.7)], blue, 0.07)
    # coffee-cup stains
    for (x, z, r) in ((-5.2, 2.4, 0.7), (6.0, 1.2, 0.55)):
        pts = [(x + r * np.cos(t), z + r * np.sin(t)) for t in np.linspace(0.2, 6.0, 30)]
        ov.line(pts, (196, 150, 96, 90), 0.09)
    # x o x o row
    for i in range(6):
        px, pz = -9.0 + i * 0.5, -9.3
        if i % 2 == 0:
            ov.line([(px - 0.14, pz - 0.14), (px + 0.14, pz + 0.14)], red, 0.05)
            ov.line([(px - 0.14, pz + 0.14), (px + 0.14, pz - 0.14)], red, 0.05)
        else:
            ov.ellipse(px, pz, 0.14, 0.14, (0, 0, 0, 0), blue, 0.05)
    img = ov.composite(img)
    return img


def notebook_side(XM, YM):
    # a stack of pages with a blue cover below
    page = (YM / 0.032) % 1.0
    base = np.broadcast_to(col(255, 251, 236), XM.shape + (3,)).copy()
    base = base * (1 - 0.06 * sm(0.12 - np.minimum(page, 1 - page), 0.1) * 1.0)[..., None]
    base = mix(base, col(214, 206, 188), sm(0.06 - page, 0.05) * 0.7)
    cover = sm(YM - 0.4, 0.008)
    base = mix(base, col(82, 138, 226), cover)
    base = mix(base, NAVY, sm(0.014 - np.abs(YM - 0.4), 0.005))
    return base


def register(d):
    d['tabletop_board'] = ('tabletop', board_top, board_side)
    d['tabletop_cloth'] = ('tabletop', cloth_top, cloth_side)
    d['tabletop_notebook'] = ('tabletop', notebook_top, notebook_side)
