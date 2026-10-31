"""Playroom looks: puzzle, road, blocks. A stadium, x within +-14.5 and z within +-21.5, with a pit on each side."""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from floorlib import *

FONT = os.path.abspath(os.path.join(HERE, '..', '..', 'Assets', 'UI', 'Fonts', 'font_lilita_one.ttf'))


def mpp(f):
    return self_mpp(f)


PASTELS = np.array([(255, 150, 150), (150, 200, 255), (255, 225, 120), (150, 225, 150), (255, 190, 120), (200, 160, 245),
                    (255, 170, 210), (130, 225, 215)], np.float32) / 255.0


# --- puzzle: a foam mat of interlocking pieces -------------------------------------------------------------------
def puzzle_ids(fp, f, cell):
    X, Z = f['X'], f['Z']
    ci = np.floor((X + 14.5) / cell).astype(np.int32)
    cj = np.floor((Z + 21.5) / cell).astype(np.int32)
    pid = ci * 1000 + cj
    h, w = X.shape
    mx = fp.sx / fp.W
    mz = fp.sz / (fp.H * TOP_RANGE)
    ppx, ppz = 1 / mx, 1 / mz
    x0, z0 = fp.min_x, fp.max_z   # row 0 of the footprint is z at the top of the footprint rows
    rng = np.random.default_rng(2)

    def bulb(xc, zc, ro, rr, horizontal, direction, owner):
        """paints the head and the neck of a tab at (xc, zc); the tab protrudes in `direction` along x or z."""
        nonlocal pid
        r = 0.2 * cell
        head = (xc + direction * 0.26 * cell, zc) if horizontal else (xc, zc + direction * 0.26 * cell)
        reach = 0.4 * cell
        x_lo, x_hi = xc - reach, xc + reach
        z_lo, z_hi = zc - reach, zc + reach
        # window in pixels
        col0 = int((x_lo - fp.min_x) / mx)
        col1 = int((x_hi - fp.min_x) / mx) + 1
        # row index: z -> v -> row
        def rowof(z):
            v = (z - fp.min_z) / fp.sz * TOP_RANGE
            return (1 - v) * fp.H - fp.row0
        row0 = int(rowof(z_hi))
        row1 = int(rowof(z_lo)) + 1
        col0, col1 = max(col0, 0), min(col1, w)
        row0, row1 = max(row0, 0), min(row1, h)
        if col1 <= col0 or row1 <= row0:
            return
        wx = X[row0:row1, col0:col1]
        wz = Z[row0:row1, col0:col1]
        inside = np.hypot(wx - head[0], wz - head[1]) < r
        if horizontal:
            neck = (np.abs(wz - zc) < 0.09 * cell) & ((wx - xc) * direction > -0.03 * cell) & ((wx - xc) * direction < 0.2 * cell)
        else:
            neck = (np.abs(wx - xc) < 0.09 * cell) & ((wz - zc) * direction > -0.03 * cell) & ((wz - zc) * direction < 0.2 * cell)
        m = inside | neck
        sub = pid[row0:row1, col0:col1]
        sub[m] = owner
    for i in range(-1, int(29 / cell) + 2):
        for j in range(-1, int(43 / cell) + 2):
            xe = (i + 1) * cell - 14.5
            zc = (j + 0.5) * cell - 21.5
            dr = 1 if hash2(np.array([i]), np.array([j]), 1)[0] > 0.5 else -1
            owner = (i if dr > 0 else i + 1) * 1000 + j
            bulb(xe, zc, 0, 0, True, dr, owner)
            ze = (j + 1) * cell - 21.5
            xc = (i + 0.5) * cell - 14.5
            dr2 = 1 if hash2(np.array([i]), np.array([j]), 2)[0] > 0.5 else -1
            owner2 = i * 1000 + (j if dr2 > 0 else j + 1)
            bulb(xc, ze, 0, 0, False, dr2, owner2)
    return pid


def puzzle_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 7)
    cell = 2.4
    pid = puzzle_ids(fp, f, cell)
    ci = pid // 1000
    cj = pid % 1000
    ci = np.where(pid < 0, 0, ci)
    colour_idx = (ci + 2 * cj) % 8
    base = PASTELS[colour_idx]
    calm = 0.5 * (1 - rw) + 0.12
    base = mix(base, col(250, 244, 236), calm)
    edge = np.zeros(sh, bool)
    edge[:, 1:] |= pid[:, 1:] != pid[:, :-1]
    edge[1:, :] |= pid[1:, :] != pid[:-1, :]
    dist = ndimage.distance_transform_edt(~edge).astype(np.float32) * mpp(f)
    puff = 1 - np.clip(dist / 0.28, 0, 1)
    base = base * (1 + 0.075 * puff)[..., None]
    base = base * (1 + 0.025 * noise(sh, 6, 5))[..., None]
    seam = sm(0.04 - dist, mpp(f))
    k = 0.55 + 0.35 * rw
    base = mix(base, base * 0.6, seam * k * 0.8)
    return base


def puzzle_side(XM, YM):
    idx = np.floor(XM / 0.75).astype(int) % 8
    base = PASTELS[idx]
    base = base * (1 + 0.05 * (0.5 - YM / 0.5))[..., None]
    seam = np.minimum((XM / 0.75) % 1.0, 1 - (XM / 0.75) % 1.0) * 0.75
    base = mix(base, NAVY, sm(0.02 - seam, 0.006) * 0.7)
    return base


# --- road: a green rug with a road round the rim ------------------------------------------------------------------
def stadium_dist(X, Z):
    """Distance in from the stadium's rim (positive inside): 14.5 minus the distance to the segment on the axis."""
    return 14.5 - np.hypot(X, np.maximum(np.abs(Z) - 7.0, 0.0))


def stadium_s(X, Z):
    """Arclength along the rim, counter-clockwise from the right side's middle."""
    L_side = 14.0
    cap = 14.5 * np.pi
    right_up = Z <= 7.0
    s = np.where(np.abs(Z) <= 7.0, np.where(X >= 0, Z + 7.0, L_side + cap + (7.0 - Z)), 0.0)
    ang_top = np.arctan2(Z - 7.0, X)
    s = np.where(Z > 7.0, L_side + 14.5 * (np.pi / 2 - (np.pi / 2 - ang_top)) * 1.0 + 0.0, s) if False else s
    top = Z > 7.0
    phi = np.arctan2(Z - 7.0, X)                                 # 0 at +x, pi at -x
    s = np.where(top, L_side + 14.5 * phi, s)
    bot = Z < -7.0
    phi2 = np.arctan2(-(Z + 7.0), -X)                            # 0 at -x, pi at +x going through -z
    s = np.where(bot, 2 * L_side + cap + 14.5 * phi2, s)
    return s, 2 * L_side + 2 * cap


def road_top(fp, f):
    X, Z = f['X'], f['Z']
    sh = X.shape
    rw = rim_weight(f, 7)
    do = stadium_dist(X, Z)
    s, L = stadium_s(X, Z)
    grass = col(150, 214, 112)
    img = np.broadcast_to(grass, sh + (3,)).copy()
    # rug weave: fine rows, plus a soft mottling
    img = img * (1 + 0.025 * np.sin(2 * np.pi * Z / 0.11) + 0.03 * noise(sh, 40, 3) + 0.015 * noise(sh, 5, 4))[..., None]
    asphalt = col(150, 158, 180)
    in_road = sm(np.minimum(do - 0.6, 3.0 - do), mpp(f))
    road = np.broadcast_to(asphalt, sh + (3,)).copy() * (1 + 0.03 * noise(sh, 6, 7))[..., None]
    img = mix(img, road, in_road)
    # kerbs: navy outline each side of the road, white edge lines inside
    img = mix(img, NAVY, sm(0.045 - np.abs(do - 0.6), mpp(f)) * 0.8)
    img = mix(img, NAVY, sm(0.045 - np.abs(do - 3.0), mpp(f)) * 0.8)
    white = col(255, 252, 244)
    img = mix(img, white, sm(0.05 - np.abs(do - 0.85), mpp(f)) * in_road * 0.9)
    img = mix(img, white, sm(0.05 - np.abs(do - 2.75), mpp(f)) * in_road * 0.9)
    n = np.round(L / 1.7)
    frac = (s / L * n) % 1.0
    dash = sm(0.55 - frac, 0.02)
    img = mix(img, col(255, 214, 70), sm(0.07 - np.abs(do - 1.8), mpp(f)) * dash * in_road)
    # a zebra crossing on each long side
    for xs in (1, -1):
        along = np.abs(Z - 0.0) < 1.0
        side = (X * xs > 0)
        bars = sm(0.2 - np.abs(((do - 0.6) / 0.5) % 1.0 - 0.5) * 0.5, 0.03)
        img = mix(img, white, along * side * bars * in_road * 0.9 * (do > 0.95) * (do < 2.65))
    # trees and a pond on the grass: round tops, a navy outline, kept off the pits and the middle
    ov = Overlay(fp)
    rng = np.random.default_rng(14)
    spots = [(-9.5, 12.5), (10.0, 13.5), (-5.0, 17.0), (6.0, 17.5), (-9.0, -13.0), (9.5, -12.0), (-4.0, -17.0), (5.5, -17.2),
             (-11.0, 9.0), (11.0, 8.5), (-11.0, -9.0), (11.0, -9.5)]
    for (x, z) in spots:
        r = rng.uniform(0.7, 1.0)
        if stadium_dist(np.array([x]), np.array([z]))[0] < 3.2 + r:
            continue
        ov.ellipse(x + 0.12, z - 0.12, r, r, (84, 160, 84, 255), (29, 43, 79, 255), 0.07)
        ov.ellipse(x - 0.12, z + 0.12, r * 0.6, r * 0.6, (120, 200, 110, 255))
    for i in range(60):
        x = rng.uniform(-13, 13)
        z = rng.uniform(-20, 20)
        if stadium_dist(np.array([x]), np.array([z]))[0] < 3.4 or np.hypot(x, z) < 8.0:
            continue
        ov.ellipse(x, z, 0.1, 0.1, (255, 255, 255, 255))
        ov.ellipse(x, z, 0.05, 0.05, (255, 214, 70, 255))
    return ov.composite(img)


def road_side(XM, YM):
    base = np.broadcast_to(col(120, 192, 96), XM.shape + (3,)).copy()
    base = base * (1 + 0.04 * np.sin(2 * np.pi * YM / 0.07))[..., None]
    stitch = sm(0.012 - np.abs(YM - 0.12), 0.004) * sm(0.5 - ((XM / 0.18) % 1.0), 0.04)
    base = mix(base, col(255, 246, 214), stitch)
    return base


# --- blocks: letter blocks -----------------------------------------------------------------------------------------
def blocks_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    rw = rim_weight(f, 7)
    cell = 2.2
    fx = ((X + 14.3) / cell) % 1.0
    fz = ((Z + 21.5) / cell) % 1.0
    ci = np.floor((X + 14.3) / cell)
    cj = np.floor((Z + 21.5) / cell)
    idx = (hash2(ci, cj, 3) * 8).astype(int) % 8
    base = PASTELS[idx]
    base = mix(base, col(250, 244, 236), 0.3 * (1 - rw))
    ex = np.minimum(fx, 1 - fx) * cell
    ez = np.minimum(fz, 1 - fz) * cell
    e = np.minimum(ex, ez)
    # a rounded, pillowy top: light on the upper-left, shaded on the lower-right
    bevel = np.clip(1 - e / 0.22, 0, 1)
    tl = np.clip(((0.5 - fx) + (fz - 0.5)) * 1.2, -1, 1)
    base = base * (1 + 0.12 * bevel * tl)[..., None]
    gap = sm(0.05 - e, mpp(f))
    base = mix(base, NAVY, gap * (0.35 + 0.35 * rw))
    # letters
    font = ImageFont.truetype(FONT, int(fp.W / fp.sx * 1.5))
    fill = Image.new('L', (fp.W, fp.H - fp.row0), 0)
    outl = Image.new('L', (fp.W, fp.H - fp.row0), 0)
    glyphs = 'ABCDEFGHIJKLMNOPRSTUVWXYZ123456789'
    rng = np.random.default_rng(31)
    ppm = fp.W / fp.sx
    for j in range(int(43 / cell) + 2):
        for i in range(int(29 / cell) + 2):
            cx = (i + 0.5) * cell - 14.3
            cz = (j + 0.5) * cell - 21.5
            px, py = fp.px(cx, cz)
            py -= fp.row0
            if px < 0 or py < 0 or px >= fill.width or py >= fill.height:
                continue
            ch = glyphs[int(hash2(np.array([i]), np.array([j]), 9)[0] * len(glyphs)) % len(glyphs)]
            size = int(ppm * 1.5)
            tile = Image.new('L', (size * 2, size * 2), 0)
            tout = Image.new('L', (size * 2, size * 2), 0)
            dr = ImageDraw.Draw(tile)
            do_ = ImageDraw.Draw(tout)
            sw = max(2, int(ppm * 0.06))
            do_.text((size, size), ch, font=font, anchor='mm', fill=255, stroke_width=sw, stroke_fill=255)
            dr.text((size, size), ch, font=font, anchor='mm', fill=255)
            ang = rng.uniform(-14, 14)
            tile = tile.rotate(ang, resample=Image.BICUBIC)
            tout = tout.rotate(ang, resample=Image.BICUBIC)
            fill.paste(255, (int(px - size), int(py - size)), tile)
            outl.paste(255, (int(px - size), int(py - size)), tout)
    A = np.asarray(fill).astype(np.float32) / 255.0
    B = np.clip(np.asarray(outl).astype(np.float32) / 255.0 - A, 0, 1)
    base = mix(base, col(255, 255, 255), A * (0.38 + 0.57 * rw))
    base = mix(base, NAVY, B * (0.08 + 0.8 * rw))
    return base


def blocks_side(XM, YM):
    cell = 6.0 / 8
    idx = np.floor(XM / cell).astype(int) % 8
    base = PASTELS[idx]
    seam = np.minimum((XM / cell) % 1.0, 1 - (XM / cell) % 1.0) * cell
    base = mix(base, NAVY, sm(0.025 - seam, 0.006) * 0.8)
    # a little star on every face
    q = np.hypot((XM / cell) % 1.0 - 0.5, (YM - 0.25) / cell) * cell
    base = mix(base, col(255, 255, 255), sm(0.09 - q, 0.015) * 0.9)
    return base


def register(d):
    d['playroom_puzzle'] = ('playroom', puzzle_top, puzzle_side)
    d['playroom_road'] = ('playroom', road_top, road_side)
    d['playroom_blocks'] = ('playroom', blocks_top, blocks_side)
