"""Five of the shop's toppers (the first ones remade) and the wing the halo shares."""
from math import sin, cos, pi, radians
from mathutils import Vector, Matrix
from hatkit import *


def interp(poly, z):
    """y at height z on a polyline of (y, z) points rising in z."""
    for (y0, z0), (y1, z1) in zip(poly, poly[1:]):
        if z <= z1:
            t = 0.0 if z1 == z0 else (z - z0) / (z1 - z0)
            return y0 + (y1 - y0) * clamp01(t)
    return poly[-1][0]


def party_hat():
    """A paper party hat: a spiral of pink, white and sky, a gold rolled rim and a tinsel pom-pom."""
    h = Hat('party-hat', 0.95, 0.15)
    p = h.part()
    m = T(0, 0, 0.03) @ R(5, 'X')
    sides, bands, height, radius = 16, 9, 0.76, 0.30
    profile = [(radius * (1.0 - k / bands), height * k / bands) for k in range(bands + 1)]
    p.revolve(profile, sides, PINK, m=m, twist=2 * pi / sides * 0.85,
              cellfn=lambda k, i: (PINK, PINK, PINK, PINK, SKY, WHITE, WHITE, SKY)[i % 8])
    p.torus(0.295, 0.05, 16, 6, GOLD, m=m @ T(0, 0, 0.035))
    p.ball((0, 0, height + 0.05), 0.075, GOLD, sub=1, m=m, poke=0.06, t=0.85)
    return h


def chef_hat():
    """A chef's toque: a tall straight band with a hem, and a pleated top that billows over it and leans aside."""
    h = Hat('chef-hat', 0.8, 0.5)
    p = h.part()
    p.revolve([(0.262, 0.0), (0.28, 0.012), (0.28, 0.065), (0.258, 0.08), (0.25, 0.34), (0.235, 0.37)], 24, WHITE,
              ts=[0.55, 0.6, 0.6, 0.5, 0.78, 0.6])
    # The top: eight round pleats with creased valleys between them, shaded in the creases, leaning to one side.
    puff = [(0.22, 0.30, 0.0), (0.30, 0.315, 0.06), (0.375, 0.37, 0.13), (0.41, 0.46, 0.17), (0.405, 0.56, 0.19),
            (0.36, 0.65, 0.21), (0.27, 0.725, 0.23), (0.14, 0.77, 0.2), (0.0, 0.78, 0.0)]
    sides, lobes = 48, 8
    lean = T(0, 0, 0.31) @ R(9, 'Y') @ T(0, 0, -0.31)
    rows, shades = [], []
    for r, z, amp in puff:
        s = clamp01((z - 0.32) / 0.46)
        s = s * s * (3 - 2 * s)
        row, shade = [], []
        for i in range(sides):
            a = 2 * pi * i / sides
            fold = 1.0 - abs(cos(lobes * a / 2))
            rr = r * (1.0 - amp * fold ** 1.5)
            row.append(lean @ Vector((rr * cos(a) + 0.05 * s, rr * sin(a) - 0.025 * s, z)))
            shade.append(0.5 + 0.5 * clamp01(z / 0.78) - 1.6 * amp * fold ** 2)
        rows.append(row)
        shades.append(shade)
    vs = p.grid(rows, WHITE, cap_start=True)
    for row, shade in zip(vs, shades):
        for vert, t in zip(row, shade):
            p.vt[vert] = t
    p.paint([f for f in p.bm.faces if any(vert in p.vt for vert in f.verts)], WHITE)
    return h


def horns():
    """A bull's horns on a leather wrap: they leave it sideways, sweep up and turn forward to their points."""
    h = Hat('horns', 0.7, 0.0)
    p = h.part()
    height = 0.155
    p.revolve([(0.105, -0.16), (0.14, -0.14), (0.14, 0.14), (0.105, 0.16)], 12, BROWN, m=T(0, 0, height) @ R(90, 'Y'))
    for side in (-1, 1):
        p.revolve([(0.11, -0.045), (0.16, -0.028), (0.16, 0.028), (0.11, 0.045)], 12, GOLD,
                  m=T(side * 0.165, 0, height) @ R(90, 'Y'))
        path = spline([(side * 0.15, 0, height), (side * 0.30, 0, height + 0.005), (side * 0.43, 0.0, 0.215),
                       (side * 0.525, 0.02, 0.335), (side * 0.55, 0.06, 0.475), (side * 0.515, 0.11, 0.60),
                       (side * 0.45, 0.155, 0.685)], 3)
        n = len(path) - 1
        radii = [0.125 * (1.0 - (i / n) ** 1.7) for i in range(n + 1)]
        p.tube(path, radii, 10, CREAM, ts=[1.0 - 0.85 * (i / n) ** 1.5 for i in range(n + 1)])
    return h


def cowboy_hat():
    """A cowboy hat in one piece: a brim rolled up at the sides and dipped front and back, a creased crown."""
    h = Hat('cowboy-hat', 0.5, 0.3)
    p = h.part()
    n = 28

    def angle(i):
        return 2 * pi * (i + 0.5) / n

    def brim(i, s, dz):
        a = angle(i)
        side = abs(cos(a)) ** 1.6
        x = (0.20 + (0.55 - 0.20) * s) * cos(a)
        y = (0.23 + (0.57 - 0.23) * s) * sin(a)
        curl = side * s ** 2.2
        z = 0.055 + 0.25 * curl - 0.035 * sin(a) ** 2 * s + dz
        return Vector((x * (1.0 - 0.17 * curl), y, z))

    shares = [0.0, 0.25, 0.5, 0.7, 0.86, 1.0]
    rows = [[brim(i, s, 0.0) for i in range(n)] for s in shares]
    rows += [[brim(i, s, -0.04) for i in range(n)] for s in reversed(shares)]
    p.grid(rows, TAN, cap_start=True, cap_end=True)

    def crown(i, a_, b_, z):
        a = angle(i)
        f = z / 0.445
        pinch = 1.0 - 0.24 * max(0.0, sin(a)) ** 2 * f
        return Vector((a_ * cos(a) * pinch, b_ * sin(a), z - 0.035 * sin(a) * f))

    rings = [(0.275, 0.305, 0.02), (0.272, 0.30, 0.12), (0.257, 0.288, 0.25), (0.236, 0.272, 0.35),
             (0.21, 0.25, 0.415), (0.17, 0.215, 0.445), (0.10, 0.165, 0.415), (0.045, 0.125, 0.375)]
    p.grid([[crown(i, a_, b_, z) for i in range(n)] for a_, b_, z in rings], TAN, cap_start=True, cap_end=True)
    band = [(0.268, 0.298, 0.06), (0.292, 0.322, 0.068), (0.288, 0.318, 0.135), (0.262, 0.292, 0.143)]
    p.grid([[crown(i, a_, b_, z) for i in range(n)] for a_, b_, z in band], BROWN, cap_start=True, cap_end=True)
    p.box((0, 0.322, 0.10), (0.10, 0.03, 0.07), GOLD, t=0.8)
    return h


def fin():
    """A shark's fin cutting through a splash of water: a curved leading edge, a hollow trailing edge, a wake."""
    h = Hat('fin', 0.66, 0.35)
    p = h.part()
    lead = [(v.y, v.z) for v in spline([(0, 0.33, 0.0), (0, 0.29, 0.10), (0, 0.21, 0.24), (0, 0.10, 0.38),
                                        (0, -0.04, 0.50), (0, -0.18, 0.585), (0, -0.30, 0.625)], 6)]
    trail = [(v.y, v.z) for v in spline([(0, -0.37, 0.0), (0, -0.335, 0.04), (0, -0.295, 0.12), (0, -0.27, 0.24),
                                         (0, -0.262, 0.36), (0, -0.265, 0.47), (0, -0.28, 0.56),
                                         (0, -0.30, 0.625)], 6)]
    levels = [0.0, 0.05, 0.12, 0.20, 0.29, 0.38, 0.46, 0.52, 0.57, 0.60, 0.617, 0.625]
    rows = []
    for z in levels:
        front, back = interp(lead, z), interp(trail, z)
        chord = max(0.0, front - back)
        th = 0.105 * (chord / 0.70) ** 0.6 if chord > 1e-4 else 0.0
        half = [(th * 0.8, front - 0.2 * chord), (th, front - 0.45 * chord), (th * 0.55, front - 0.78 * chord)]
        ring = [(0.0, front)] + half + [(0.0, back)] + [(-x, y) for x, y in reversed(half)]
        rows.append([Vector((x, y, z)) for x, y in ring])
    p.grid(rows, TRIM, cap_start=True)
    # The water it cuts: a puddle with a wavy edge, and the foam of its wake from the leading edge back.
    p.revolve([(1.0, 0.0), (1.0, 0.022), (0.9, 0.045), (0.0, 0.05)], 28, SKY, sx=0.33, sy=0.52, lobes=7,
              amps=[0.06, 0.06, 0.06, 0.0], m=T(0, -0.03, 0), ts=[0.2, 0.3, 0.75, 0.9])
    for side in (-1, 1):
        wake = spline([(0.0, 0.39, 0.045), (side * 0.11, 0.27, 0.06), (side * 0.19, 0.08, 0.06),
                       (side * 0.25, -0.14, 0.055), (side * 0.29, -0.36, 0.045)], 3)
        m = len(wake) - 1
        p.tube(wake, [0.062 * sin(pi * (0.12 + 0.88 * i / m)) ** 0.7 for i in range(m)] + [0.0], 8, WHITE,
               ts=[1.0] * (m + 1))
    return h


def wing(p, side, root, feathers, sweep=0.0, step=-0.025, thick=0.034):
    """A wing fanned from its root: (angle from level, length, width, cell) per feather, the top one first.

    sweep turns the wing back by that many degrees, its tips behind its root."""
    root = Vector(root)
    back = radians(sweep)
    normal = Vector((side * sin(back), cos(back), 0.0))
    for k, (deg, length, width, cell) in enumerate(feathers):
        a = radians(deg)
        direction = Vector((side * cos(a) * cos(back), -cos(a) * sin(back), sin(a)))
        p.blade(root + Vector((0, step * k, 0)), direction, length, width, thick, cell, normal,
                bend=(0, 0, 0.07 * length / 0.4))


SHOP = [party_hat, chef_hat, horns, cowboy_hat, fin]
