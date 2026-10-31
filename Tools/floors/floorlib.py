"""Shared pieces of the floor painter: the floor's footprint, its signed-distance fields and small drawing helpers.

The layout of a floor texture is fixed by FloorUv.cs: the bounding box of the floor fills u 0..1 and v 0..TOP_RANGE
(image rows counted from the top, so v = 1 - row / height), and a strip for the sides sits at v SIDE_BOTTOM..SIDE_TOP
and repeats SIDE_REPEATS times round the floor. Keep these constants in step with that file.
"""
import os
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

TOP_RANGE = 0.84
SIDE_TOP = 0.99
SIDE_BOTTOM = 0.88
SIDE_REPEATS = 16
NAVY = np.array([29, 43, 79], np.float32) / 255.0

SIZE = 1024   # output texture is SIZE x SIZE
SS = 2        # supersampling

HERE = os.path.dirname(os.path.abspath(__file__))


def sm(x, w=1.0):
    """Smooth 0..1 step across [-w, w], used as an anti-aliased edge."""
    t = np.clip(x / (2 * w) + 0.5, 0, 1)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    t = np.asarray(t, np.float32)
    if t.ndim == 2:
        t = t[..., None]
    return a * (1 - t) + b * t


def col(r, g, b):
    return np.array([r, g, b], np.float32) / 255.0


class Footprint:
    """World <-> texture mapping of one floor, plus its signed-distance fields."""

    def __init__(self, name, min_x, min_z, max_x, max_z, loops=None, circle=None):
        self.name = name
        self.min_x, self.min_z, self.max_x, self.max_z = min_x, min_z, max_x, max_z
        self.sx, self.sz = max_x - min_x, max_z - min_z
        self.loops = loops or []      # list of (points, is_hole)
        self.circle = circle
        self.W = SIZE * SS
        self.H = SIZE * SS
        self.row0 = int(round((1 - TOP_RANGE) * self.H))   # first row of the footprint
        self._fields = None

    # --- mapping -----------------------------------------------------------------------------------------------
    def px(self, x, z):
        u = (x - self.min_x) / self.sx
        v = (z - self.min_z) / self.sz * TOP_RANGE
        return u * self.W, (1 - v) * self.H

    def grid(self):
        rows = np.arange(self.row0, self.H, dtype=np.float32) + 0.5
        cols = np.arange(self.W, dtype=np.float32) + 0.5
        v = 1 - rows / self.H
        z = self.min_z + v / TOP_RANGE * self.sz
        x = self.min_x + cols / self.W * self.sx
        X, Z = np.meshgrid(x, z)
        return X.astype(np.float32), Z.astype(np.float32)

    def metres_per_px(self):
        return self.sx / self.W, self.sz / (self.H * TOP_RANGE)

    # --- fields ------------------------------------------------------------------------------------------------
    def fields(self):
        """Returns dict: X, Z, d (signed distance to the nearest edge or hole, m; positive on the floor),
        s (arclength of the nearest boundary point, m), L (length of that loop, m), loop (index)."""
        if self._fields is not None:
            return self._fields
        cache = os.path.join(HERE, 'cache_%s.npz' % self.name)
        X, Z = self.grid()
        if os.path.exists(cache):
            z = np.load(cache)
            self._fields = dict(X=X, Z=Z, d=z['d'], s=z['s'], L=z['L'], loop=z['loop'])
            return self._fields
        if self.circle:
            r = np.hypot(X, Z)
            d = self.circle - r
            s = np.arctan2(Z, X) * self.circle
            L = np.full(X.shape, 2 * np.pi * self.circle, np.float32)
            loop = np.zeros(X.shape, np.int8)
        else:
            best = np.full(X.shape, 1e9, np.float32)
            s = np.zeros(X.shape, np.float32)
            L = np.zeros(X.shape, np.float32)
            loop = np.zeros(X.shape, np.int8)
            for li, (pts, _) in enumerate(self.loops):
                pts = np.array(pts, np.float32)
                seg = np.roll(pts, -1, axis=0) - pts
                seglen = np.hypot(seg[:, 0], seg[:, 1])
                cum = np.concatenate([[0], np.cumsum(seglen)])
                total = float(cum[-1])
                for i in range(len(pts)):
                    a = pts[i]
                    e = seg[i]
                    l2 = float(e @ e)
                    t = np.clip(((X - a[0]) * e[0] + (Z - a[1]) * e[1]) / l2, 0, 1)
                    dx = X - (a[0] + t * e[0])
                    dz = Z - (a[1] + t * e[1])
                    dist = np.sqrt(dx * dx + dz * dz)
                    better = dist < best
                    best = np.where(better, dist, best)
                    s = np.where(better, cum[i] + t * seglen[i], s)
                    L = np.where(better, total, L)
                    loop = np.where(better, li, loop).astype(np.int8)
            mask = self.mask()
            d = np.where(mask, best, -best)
        np.savez_compressed(cache, d=d.astype(np.float32), s=s.astype(np.float32), L=L.astype(np.float32), loop=loop)
        self._fields = dict(X=X, Z=Z, d=d.astype(np.float32), s=s.astype(np.float32), L=L.astype(np.float32), loop=loop)
        return self._fields

    def mask(self):
        img = Image.new('L', (self.W, self.H), 0)
        dr = ImageDraw.Draw(img)
        for pts, hole in self.loops:
            poly = [self.px(x, z) for x, z in pts]
            dr.polygon(poly, fill=0 if hole else 255)
        return (np.asarray(img)[self.row0:] > 127)


def noise(shape, cell, seed, octaves=1):
    """Smooth value noise in -1..1; `cell` is the lattice step in pixels, or (cell_x, cell_y) for stretched noise."""
    rng = np.random.default_rng(seed)
    h, w = shape
    cx, cy = (cell, cell) if np.isscalar(cell) else cell
    out = np.zeros(shape, np.float32)
    amp = 1.0
    tot = 0.0
    for o in range(octaves):
        sx, sy = max(2.0, cx / (2 ** o)), max(2.0, cy / (2 ** o))
        gh, gw = int(h / sy) + 4, int(w / sx) + 4
        g = rng.random((gh, gw)).astype(np.float32) * 2 - 1
        z = ndimage.zoom(g, (h / (gh - 3), w / (gw - 3)), order=3)[:h, :w]
        if z.shape != shape:
            z = np.pad(z, ((0, h - z.shape[0]), (0, w - z.shape[1])), mode='edge')
        out += z * amp
        tot += amp
        amp *= 0.5
    return np.clip(out / tot, -1, 1)


class Overlay:
    """Hand-drawn shapes (PIL) over the footprint, in world metres, composited onto the image afterwards."""

    def __init__(self, fp):
        self.fp = fp
        self.w = fp.W
        self.h = fp.H - fp.row0
        self.img = Image.new('RGBA', (self.w, self.h), (0, 0, 0, 0))
        self.dr = ImageDraw.Draw(self.img)
        self.ppm = fp.W / fp.sx   # pixels per metre across

    def p(self, x, z):
        px, py = self.fp.px(x, z)
        return (px, py - self.fp.row0)

    def m(self, metres):
        return metres * self.ppm

    def poly(self, pts, fill, outline=None, width=0):
        self.dr.polygon([self.p(x, z) for x, z in pts], fill=fill, outline=outline)

    def line(self, pts, fill, width_m):
        q = [self.p(x, z) for x, z in pts]
        self.dr.line(q, fill=fill, width=max(1, int(round(self.m(width_m)))), joint='curve')
        r = self.m(width_m) / 2
        for (x, y) in (q[0], q[-1]):
            self.dr.ellipse([x - r, y - r, x + r, y + r], fill=fill)

    def ellipse(self, x, z, rx, rz, fill, outline=None, width_m=0):
        (cx, cy) = self.p(x, z)
        self.dr.ellipse([cx - self.m(rx), cy - self.m(rz), cx + self.m(rx), cy + self.m(rz)], fill=fill,
                        outline=outline, width=max(1, int(round(self.m(width_m)))) if width_m else 1)

    def composite(self, img):
        a = np.asarray(self.img).astype(np.float32) / 255.0
        return mix(img, a[..., :3], a[..., 3])


def hash2(ix, iz, seed=0):
    """Deterministic 0..1 hash of integer cell coordinates."""
    n = (ix.astype(np.int64) * 374761393 + iz.astype(np.int64) * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    n = n ^ (n >> 16)
    return (n & 0xFFFF).astype(np.float32) / 65535.0


def navy_edges(img, f, width=0.13, holes=True):
    """Comic outline: navy line along every edge of the floor, filled navy outside it."""
    d = f['d']
    mpp = max(self_mpp(f), 1e-6)
    a = sm(width - d, mpp * 1.2)   # 1 on the line and beyond
    return mix(img, NAVY, a)


def self_mpp(f):
    return float(f['X'][0, 1] - f['X'][0, 0])


def rim_weight(f, start=7.0, end=0.0):
    """0 well inside, 1 at the rim: detail grows toward the edge."""
    return np.clip(1 - (f['d'] - end) / (start - end), 0, 1) ** 1.3


def tile_loop_s(f, period):
    """Arclength along the nearest loop, rescaled so a whole number of `period`s fits: 0..N periods."""
    n = np.maximum(1, np.round(f['L'] / period))
    return f['s'] / f['L'] * n   # in units of periods (wraps because s covers one loop)


def down(img):
    """Box-filter the supersampled image to the output size."""
    h, w = img.shape[:2]
    return img.reshape(h // SS, SS, w // SS, SS, -1).mean(axis=(1, 3))


def save_png(path, img):
    a = np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8)
    Image.fromarray(a, 'RGB').save(path, optimize=True)
