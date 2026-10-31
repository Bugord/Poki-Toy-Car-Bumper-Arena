"""Paints the floor textures of the arena variants: python Tools/floors/floors.py [name ...] [--debug].

Writes Assets/Art/Textures/Arenas/tex_floor_<map>_<variant>.png. Needs numpy, scipy and Pillow. The footprints
(outline and holes of each shape) come from shapes.json, dumped from the game's ArenaShape (see the pipeline notes in
CLAUDE.md); the texture layout is described in Assets/Scripts/Gameplay/Arena/FloorUv.cs.
"""
import json
import os
import sys
import numpy as np
from floorlib import *

ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Textures', 'Arenas')
EXPOSURE = 1.0


def load_footprints():
    data = json.load(open(os.path.join(HERE, 'shapes.json')))
    fps = {}
    for name, m in data.items():
        if m.get('circle'):
            r = m['circle']
            fps[name] = Footprint(name, -r, -r, r, r, circle=r)
        else:
            out = m['outline']
            xs = [p[0] for p in out]
            zs = [p[1] for p in out]
            loops = [(out, False)] + [(v, True) for v in m['voids']]
            fps[name] = Footprint(name, min(xs), min(zs), max(xs), max(zs), loops=loops)
    return fps


def strip_grid(fp):
    """Coordinates of the side strip: xm metres along (one repeat is ~6 m), ym metres down from the top edge."""
    rt = int(round((1 - SIDE_TOP) * fp.H))
    rb = int(round((1 - SIDE_BOTTOM) * fp.H))
    h = rb - rt
    xm = (np.arange(fp.W, dtype=np.float32) + 0.5) / fp.W * 6.0
    ym = (np.arange(h, dtype=np.float32) + 0.5) / h * 0.5
    XM, YM = np.meshgrid(xm, ym)
    return rt, rb, XM, YM


def compose(fp, top, side, name):
    f = fp.fields()
    img = top(fp, f)
    img = navy_edges(img, f)
    canvas = np.zeros((fp.H, fp.W, 3), np.float32)
    canvas[fp.row0:] = img
    rt, rb, XM, YM = strip_grid(fp)
    st = side(XM, YM)
    canvas[rt:rb] = st
    canvas[:rt] = st[0]
    canvas[rb:fp.row0] = st[-1]
    out = down(canvas) * EXPOSURE
    path = os.path.join(OUT, 'tex_floor_%s.png' % name)
    os.makedirs(OUT, exist_ok=True)
    save_png(path, out)
    print('wrote', path, os.path.getsize(path) // 1024, 'KB')


# --- debug -------------------------------------------------------------------------------------------------------
def debug_top(fp, f):
    X, Z = f['X'], f['Z']
    chk = ((np.floor(X) + np.floor(Z)) % 2).astype(np.float32)
    img = np.stack([0.55 + 0.25 * chk] * 3, -1)
    # quadrant tint: +x red, +z green so mirroring and flips are obvious
    img[..., 0] += 0.3 * np.clip(X / fp.max_x, 0, 1)
    img[..., 1] += 0.3 * np.clip(Z / fp.max_z, 0, 1)
    grid5 = (np.abs(X - np.round(X / 5) * 5) < 0.08) | (np.abs(Z - np.round(Z / 5) * 5) < 0.08)
    img[grid5] = NAVY
    return np.clip(img, 0, 1)


def debug_side(XM, YM):
    img = np.zeros(XM.shape + (3,), np.float32) + 0.9
    img[..., 2] = 0.3 + 0.7 * (XM / 6.0)
    tick = np.abs(XM - np.round(XM)) < 0.05
    img[tick] = NAVY
    return img


DESIGNS = {}   # name -> (footprint key, top fn, side fn); filled by designs.py

if __name__ == '__main__':
    from designs import register
    register(DESIGNS)
    DESIGNS['debug_sandbox'] = ('sandbox', debug_top, debug_side)
    DESIGNS['debug_playroom'] = ('playroom', debug_top, debug_side)
    fps = load_footprints()
    names = [a for a in sys.argv[1:] if not a.startswith('--')] or [n for n in DESIGNS if not n.startswith('debug')]
    for n in names:
        key, top, side = DESIGNS[n]
        compose(fps[key], top, side, n)
