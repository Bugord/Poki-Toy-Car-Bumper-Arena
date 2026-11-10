"""The rest of the shop's toppers remade, and the leagues' helmets."""
from math import sin, cos, pi, radians, degrees, asin, sqrt
from mathutils import Vector, Matrix
from hatkit import *
from hats import wing


def off_front(i, sides, a0=0.5):
    """How far face i of a revolve is from the front (+Y), in degrees."""
    a = degrees(2 * pi * (i + a0 + 0.5) / sides)
    return abs((a - 90.0 + 180.0) % 360.0 - 180.0)


def brim_grid(p, n, inner, outer, sy, zfn, thick, cell, shares=(0.0, 0.3, 0.6, 0.82, 0.94, 1.0), squeeze=None):
    """A brim in one piece: rings from inner to outer radius, its height zfn(angle, share), thick downwards."""
    def at(i, s, dz):
        a = 2 * pi * (i + 0.5) / n
        r = inner + (outer - inner) * s
        k = squeeze(a, s) if squeeze else 1.0
        return Vector((r * cos(a) * k, r * sy * sin(a), zfn(a, s) + dz))
    rows = [[at(i, s, 0.0) for i in range(n)] for s in shares]
    rows += [[at(i, s, -thick) for i in range(n)] for s in reversed(shares)]
    p.grid(rows, cell, cap_start=True, cap_end=True)


# ---------------------------------------------------------------- shop

def top_hat():
    """A gentleman's top hat: a brim rolled up at the sides, a tall crown flaring to its top, a red band."""
    h = Hat('top-hat', 0.68, 0.0)
    p = h.part()
    brim_grid(p, 32, 0.22, 0.45, 1.1,
              lambda a, s: 0.035 + 0.1 * abs(cos(a)) ** 2 * s ** 2.5, 0.035, DARK,
              squeeze=lambda a, s: 1.0 - 0.1 * abs(cos(a)) ** 2 * s ** 2.5)
    p.revolve([(0.258, 0.02), (0.252, 0.12), (0.245, 0.3), (0.256, 0.48), (0.276, 0.6), (0.28, 0.635),
               (0.262, 0.66), (0.16, 0.672), (0.0, 0.675)], 32, DARK, sy=1.1, ts=[0.3, 0.35, 0.45, 0.6, 0.75, 0.85,
                                                                              0.95, 1.0, 1.0])
    p.revolve([(0.252, 0.07), (0.268, 0.08), (0.268, 0.175), (0.25, 0.185)], 32, RED, sy=1.1)
    return h


def cone():
    """A traffic cone: a rounded square foot with a lip, a cone with two raised white stripes and a round tip."""
    h = Hat('cone', 0.72, 0.15)
    p = h.part()
    sides = 32

    def square(r, z, power=7.0):
        row = []
        for i in range(sides):
            a = 2 * pi * (i + 0.5) / sides
            k = (abs(cos(a)) ** power + abs(sin(a)) ** power) ** (-1.0 / power)
            row.append(Vector((r * k * cos(a), r * k * sin(a), z)))
        return row
    foot = [(0.30, 0.0), (0.315, 0.012), (0.315, 0.055), (0.30, 0.07), (0.22, 0.075)]
    p.grid([square(r, z) for r, z in foot], ORANGE, cap_start=True, cap_end=True, ts=[0.0, 0.15, 0.3, 0.45, 0.5])

    def r(z):
        return 0.255 - 0.2 * (z - 0.07) / 0.6
    pts, cells = [(0.272, 0.072), (r(0.085), 0.085)], [ORANGE]
    for z0, z1, c in ((0.085, 0.30, ORANGE), (0.30, 0.40, WHITE), (0.40, 0.49, ORANGE), (0.49, 0.565, WHITE),
                      (0.565, 0.655, ORANGE)):
        if c == WHITE:
            pts += [(r(z0) + 0.011, z0 + 0.005), (r(z1) + 0.011, z1 - 0.005), (r(z1), z1)]
            cells += [WHITE, WHITE, WHITE]
        else:
            pts.append((r(z1), z1))
            cells.append(ORANGE)
    pts += [(0.045, 0.685), (0.0, 0.695)]
    cells += [ORANGE, ORANGE]
    p.revolve(pts, 24, ORANGE, cellfn=lambda k, i: cells[min(k, len(cells) - 1)])
    return h


def antenna():
    """A car aerial: a dark foot, a chrome spring, a thin rod (its own part, to sway) and a red ball."""
    h = Hat('antenna', 1.2, 0.2)
    p = h.part()
    p.revolve([(0.085, 0.0), (0.095, 0.01), (0.095, 0.035), (0.075, 0.058), (0.04, 0.072), (0.0, 0.076)], 16, DARK)
    turns, steps = 4, 64
    p.tube([(0.042 * cos(2 * pi * turns * s / steps), 0.042 * sin(2 * pi * turns * s / steps), 0.075 + 0.2 * s / steps)
            for s in range(steps + 1)], [0.015] * (steps + 1), 6, CHROME)
    p.tube([(0, 0, 0.06), (0, 0, 0.3)], [0.022, 0.022], 8, SLATE)
    s = h.part('sway')
    s.revolve([(0.035, 0.27), (0.045, 0.28), (0.045, 0.31), (0.03, 0.32)], 12, DARK)
    s.tube([(0, 0, 0.3), (0, 0, 0.65), (0, 0, 1.02)], [0.022, 0.019, 0.015], 8, CHROME)
    s.ball((0, 0, 1.085), 0.09, RED, sub=2)
    s.ball((0.035, 0.04, 1.125), 0.022, WHITE, sub=1, t=1.0)
    return h


def flag():
    """A checkered racing flag waving on a white pole with a gold ball."""
    h = Hat('flag', 1.15, 0.3)
    p = h.part()
    p.revolve([(0.07, 0.0), (0.08, 0.01), (0.08, 0.03), (0.05, 0.05), (0.0, 0.055)], 12, DARK)
    p.tube([(0, 0, 0.04), (0, 0, 1.1)], [0.028, 0.028], 8, WHITE)
    p.ball((0, 0, 1.13), 0.05, GOLD, sub=2)
    cols, rows_, sub = 6, 4, 2
    nu, nz = cols * sub, rows_ * sub
    length, top, bottom, th = 0.58, 1.07, 0.71, 0.014
    loops = []
    for k in range(nu + 1):
        u = k / nu
        x = 0.07 * sin(2 * pi * u * 1.1 - 0.3) * u
        y = -0.025 - length * u
        sag = 0.035 * u * u
        side = []
        for j in range(nz + 1):
            z = top - (top - bottom) * j / nz - sag
            side.append((x, y, z))
        loop = [Vector((px + th, py, pz)) for px, py, pz in side]
        loop += [Vector((px - th, py, pz)) for px, py, pz in reversed(side)]
        loops.append(loop)

    def checker(k, i):
        col = k // sub
        if i < nz:
            row = i // sub
        elif nz < i < 2 * nz + 1:
            row = (2 * nz - i) // sub
        else:
            return WHITE
        return DARK if (col + row) % 2 else WHITE
    p.grid(loops, WHITE, cap_start=True, cap_end=True, cellfn=checker, ts=[0.85] * (nu + 1))
    return h


def propeller():
    """A propeller beanie: a cap of four coloured panels, a red visor, a gold stem; the blades are their own part."""
    h = Hat('propeller', 0.5, 0.2)
    p = h.part()
    panels = [RED, GOLD, BLUE, GREEN]
    p.revolve([(0.31, 0.0), (0.325, 0.012), (0.325, 0.05), (0.31, 0.062), (0.30, 0.072), (0.29, 0.13), (0.255, 0.2),
               (0.19, 0.26), (0.1, 0.295), (0.0, 0.305)], 24, WHITE,
              cellfn=lambda k, i: WHITE if k < 3 else panels[(i // 3) % 4])
    p.ball((0, 0, 0), 1.0, RED, sub=2, scale=(0.25, 0.18, 0.026), m=T(0, 0.30, 0.04) @ R(-8, 'X'))
    p.ball((0, 0, 0.3), 0.05, GOLD, sub=2)
    p.tube([(0, 0, 0.3), (0, 0, 0.43)], [0.022, 0.022], 8, GOLD)
    b = h.part('blades')
    hub = 0.44
    b.ball((0, 0, hub), 0.045, GOLD, sub=2)
    for side, cell in ((1, RED), (-1, BLUE)):
        pitch = radians(22)
        b.tube([(side * 0.03, 0, hub), (side * 0.18, 0, hub), (side * 0.36, 0, hub), (side * 0.5, 0, hub),
                (side * 0.54, 0, hub)], [0.03, 0.068, 0.078, 0.06, 0.0], 8, cell, flat=0.2,
               normal=(0, side * sin(pitch), cos(pitch)), ts=[0.6, 0.7, 0.8, 0.9, 1.0])
    return h


def viking_helmet():
    """A viking's helmet: a fur rim, a steel dome on a riveted gold band crossed by gold straps, two curved horns."""
    h = Hat('viking-helmet', 0.7, 0.1)
    p = h.part()
    p.torus(0.305, 0.058, 32, 8, BROWN, m=T(0, 0, 0.058), sy=1.05, lobes=11, amp=0.18)
    p.revolve([(0.30, 0.07), (0.318, 0.08), (0.318, 0.135), (0.30, 0.145), (0.296, 0.155), (0.287, 0.24),
               (0.252, 0.33), (0.19, 0.40), (0.11, 0.445), (0.0, 0.46)], 32, CHROME, sy=1.05,
              cellfn=lambda k, i: GOLD if k < 3 else CHROME)
    dome = [(0.296, 0.155), (0.287, 0.24), (0.252, 0.33), (0.19, 0.40), (0.11, 0.445), (0.0, 0.46)]
    arc = [(r + 0.006, z) for r, z in dome]
    front_back = [(0, r * 1.05, z) for r, z in arc] + [(0, -r * 1.05, z) for r, z in reversed(arc[:-1])]
    p.tube(spline(front_back, 3), [0.042] * len(spline(front_back, 3)), 8, GOLD, flat=0.3, normal=(0, 1, 0))
    for k in range(14):
        a = 2 * pi * (k + 0.5) / 14
        p.ball((0.322 * cos(a), 0.322 * 1.05 * sin(a), 0.108), 0.017, CHROME, sub=1, t=0.9)
    for side in (-1, 1):
        p.revolve([(0.085, -0.035), (0.115, -0.02), (0.115, 0.02), (0.085, 0.035)], 14, GOLD,
                  m=T(side * 0.285, 0, 0.25) @ R(90, 'Y'))
        path = spline([(side * 0.24, 0, 0.25), (side * 0.36, 0, 0.27), (side * 0.46, 0.02, 0.36),
                       (side * 0.5, 0.05, 0.49), (side * 0.48, 0.09, 0.60), (side * 0.42, 0.12, 0.67)], 3)
        n = len(path) - 1
        p.tube(path, [0.1 * (1.0 - (i / n) ** 1.7) for i in range(n + 1)], 10, CREAM,
               ts=[1.0 - 0.85 * (i / n) ** 1.5 for i in range(n + 1)])
    return h


def duck():
    """A rubber duck riding on the roof: a plump body with a raised tail and wings, a big head, an orange beak."""
    h = Hat('duck', 0.8, 0.3)
    p = h.part()
    p.ball((0, -0.04, 0.2), 1.0, GOLD, sub=3, scale=(0.3, 0.4, 0.2))
    p.tube([(0, -0.3, 0.2), (0, -0.4, 0.27), (0, -0.46, 0.36), (0, -0.47, 0.43)], [0.15, 0.11, 0.06, 0.0], 12, GOLD)
    for side in (-1, 1):
        p.ball((0, 0, 0), 1.0, GOLD, sub=2, scale=(0.06, 0.2, 0.11),
               m=T(side * 0.27, -0.1, 0.25) @ R(-18, 'X') @ R(side * -8, 'Z'), t=0.45)
    p.tube([(0, 0.11, 0.28), (0, 0.15, 0.42)], [0.15, 0.13], 12, GOLD)
    head = Vector((0, 0.17, 0.53))
    p.ball(head, 0.19, GOLD, sub=3)
    p.ball((0, 0.375, 0.5), 1.0, ORANGE, sub=2, scale=(0.12, 0.11, 0.045))
    p.ball((0, 0.345, 0.46), 1.0, ORANGE, sub=2, scale=(0.095, 0.085, 0.03), t=0.35)
    for side in (-1, 1):
        d = Vector((side * 0.458, 0.815, 0.356))
        p.ball(head + d * 0.17, 0.045, WHITE, sub=2, t=1.0)
        p.ball(head + d * 0.2, 0.028, DARK, sub=2)
        p.ball(head + d * 0.222 + Vector((0, 0, 0.012)), 0.01, WHITE, sub=1, t=1.0)
        p.ball(head + Vector((side * 0.15, 0.08, -0.05)), 1.0, PINK, sub=1, scale=(0.02, 0.045, 0.03), t=0.3)
    p.tube(spline([(0, 0.16, 0.69), (0, 0.12, 0.76), (0, 0.17, 0.8), (0, 0.21, 0.76)], 3),
           [0.035, 0.03, 0.022, 0.014, 0.008, 0.004, 0.002, 0.001, 0.0, 0.0][:10], 6, GOLD)
    return h


def halo():
    """An angel's halo floating over a little cloud with two swept white wings."""
    h = Hat('halo', 0.68, 0.4)
    p = h.part()
    for c, r in (((0, 0, 0.12), 0.12), ((0.12, 0.02, 0.08), 0.085), ((-0.12, 0.02, 0.08), 0.085),
                 ((0.0, 0.11, 0.075), 0.08), ((0.0, -0.11, 0.085), 0.085)):
        p.ball(c, r, WHITE, sub=2, t=0.7)
    feathers = [(50, 0.32, 0.068, WHITE), (28, 0.31, 0.068, WHITE), (6, 0.23, 0.06, WHITE)]
    for side in (-1, 1):
        wing(p, side, (side * 0.1, -0.03, 0.17), feathers, sweep=25, thick=0.03)
    p.torus(0.25, 0.048, 28, 8, GOLD, m=T(0, 0, 0.6) @ R(8, 'X'))
    p.star((0.27, 0.05, 0.72), 0.045, GOLD, depth=0.02, m=R(0, 'Z'), t=1.0)
    p.star((-0.24, -0.06, 0.5), 0.032, GOLD, depth=0.016, t=1.0)
    return h


def rocket():
    """A toy rocket on three swept fins: a white body, a red stripe and nose, a gold porthole, a steel nozzle."""
    h = Hat('rocket', 0.95, 0.2)
    p = h.part()
    body = [(0.12, 0.12), (0.17, 0.15), (0.19, 0.22), (0.19, 0.25), (0.198, 0.255), (0.198, 0.295), (0.19, 0.30),
            (0.19, 0.56), (0.185, 0.62), (0.17, 0.68), (0.13, 0.79), (0.07, 0.88), (0.0, 0.93)]
    p.revolve(body, 24, WHITE, cellfn=lambda k, i: RED if k in (3, 4, 5) or k >= 8 else WHITE)
    p.revolve([(0.085, 0.13), (0.075, 0.09), (0.11, 0.03), (0.118, 0.02), (0.0, 0.025)], 16, SLATE)
    fin = [(0.16, 0.40), (0.16, 0.12), (0.32, 0.0), (0.36, 0.05), (0.31, 0.18)]
    for deg in (30, 150, 270):
        p.plate(fin, 0.045, RED, m=R(deg, 'Z'))
    p.torus(0.065, 0.02, 16, 6, GOLD, m=T(0, 0.19, 0.47) @ R(90, 'X'))
    p.ball((0, 0.185, 0.47), 1.0, SKY, sub=2, scale=(0.058, 0.035, 0.058), t=0.85)
    p.ball((0.02, 0.215, 0.49), 0.012, WHITE, sub=1, t=1.0)
    return h


def crown():
    """A king's crown: an ermine rim, a jewelled gold band rising to five points with pearls, a red cushion and an
    orb with a cross on top."""
    h = Hat('crown', 0.62, 0.15)
    p = h.part()
    points, per = 5, 4
    n = points * per
    lift = 0.04

    def at(i, r, z):
        a = pi / 2 + 2 * pi * i / n
        return Vector((r * cos(a), r * sin(a), z + lift))

    def top(i, inset):
        r, z = ((0.385, 0.45), (0.345, 0.28), (0.33, 0.225), (0.345, 0.28))[i % per]
        return at(i, r - inset, z - (0.012 if inset else 0.0))
    outer = [(0.30, 0.0), (0.33, 0.012), (0.33, 0.058), (0.31, 0.072), (0.315, 0.16), (0.335, 0.172), (0.335, 0.205)]
    rows = [[at(i, r, z) for i in range(n)] for r, z in outer]
    rows.append([top(i, 0.0) for i in range(n)])
    rows.append([top(i, 0.04) for i in range(n)])
    rows += [[at(i, r, z) for i in range(n)] for r, z in ((0.285, 0.19), (0.275, 0.02))]
    p.grid(rows, GOLD, cap_start=True, cap_end=True)
    p.revolve([(0.29, 0.12 + lift), (0.275, 0.22 + lift), (0.21, 0.30 + lift), (0.11, 0.345 + lift),
               (0.0, 0.355 + lift)], 20, RED, a0=0.0)
    p.torus(0.33, 0.05, 32, 8, WHITE, m=T(0, 0, 0.05), lobes=13, amp=0.12)
    for k in range(10):
        a = pi / 2 + 2 * pi * (k + 0.25) / 10
        p.ball((0.378 * cos(a), 0.378 * sin(a), 0.07), 1.0, DARK, sub=1, scale=(0.012, 0.012, 0.022))
    gems = [RED, BLUE, GREEN, BLUE, RED]
    for k in range(points):
        a = pi / 2 + 2 * pi * k / points
        out = Vector((cos(a), sin(a), 0.0))
        p.ball((0, 0, 0), 1.0, gems[k], sub=1, scale=(0.06, 0.04, 0.07),
               m=T(*(out * 0.318 + Vector((0, 0, 0.152)))) @ R(degrees(a) - 90, 'Z'), t=0.9)
        p.ball(out * 0.385 + Vector((0, 0, 0.45 + lift + 0.02)), 0.042, WHITE, sub=2, t=1.0)
        b = pi / 2 + 2 * pi * (k + 0.5) / points
        p.ball((0.325 * cos(b), 0.325 * sin(b), 0.152), 0.025, WHITE, sub=1, t=1.0)
    p.ball((0, 0, 0.43), 0.06, GOLD, sub=2)
    p.box((0, 0, 0.53), (0.03, 0.03, 0.12), GOLD, t=1.0)
    p.box((0, 0, 0.545), (0.09, 0.03, 0.03), GOLD, t=1.0)
    return h


def wizard_hat():
    """A wizard's hat: a wide drooping brim, a tall cone bending back to a star, a gold band and buckle, gold stars
    and a moon."""
    h = Hat('wizard-hat', 0.9, 0.2)
    p = h.part()
    brim_grid(p, 32, 0.22, 0.48, 1.0, lambda a, s: 0.05 - 0.035 * s * s + 0.022 * s * s * sin(3 * a + 0.5), 0.035,
              PURPLE)
    path = spline([(0, 0, 0.04), (0, -0.01, 0.3), (0, -0.05, 0.55), (0, -0.13, 0.75), (0, -0.27, 0.86),
                   (0, -0.4, 0.83)], 4)
    n = len(path) - 1
    radii = [0.255 * (1.0 - i / n) ** 1.05 for i in range(n + 1)]
    p.tube(path, radii, 18, PURPLE, ts=[0.3 + 0.7 * i / n for i in range(n + 1)])
    p.revolve([(0.247, 0.07), (0.266, 0.08), (0.266, 0.15), (0.245, 0.16)], 24, GOLD)
    p.box((0, 0.265, 0.115), (0.11, 0.03, 0.09), GOLD, t=0.9)
    p.box((0, 0.27, 0.115), (0.055, 0.03, 0.045), PURPLE, t=0.4)

    def on_cone(z, az):
        k = min(range(n + 1), key=lambda i: abs(path[i].z - z))
        c, r = path[k], radii[k]
        return c + Vector((cos(radians(az)), sin(radians(az)), 0.0)) * r
    for z, az, size in ((0.35, 90, 0.075), (0.53, 40, 0.05), (0.6, 140, 0.045), (0.27, 20, 0.04)):
        p.star((0, 0, 0), size, GOLD, depth=0.03, m=T(*on_cone(z, az)) @ R(az - 90, 'Z') @ R(-10, 'X'), t=1.0)
    # A crescent moon: the outer arc one way, the inner arc back.
    moon = [(0.07 * cos(a), 0.07 * sin(a)) for a in [radians(d) for d in range(60, 301, 20)]]
    moon += [(0.032 + 0.05 * cos(a), 0.05 * sin(a)) for a in [radians(d) for d in range(250, 109, -20)]]
    p.plate(moon, 0.03, GOLD, m=T(*on_cone(0.43, 150)) @ R(150 - 90, 'Z') @ R(-10, 'X'), t=1.0)
    p.star((0, 0, 0), 0.065, GOLD, depth=0.035, m=T(*(path[-1] + Vector((0, -0.02, -0.04)))) @ R(-25, 'X'), t=1.0)
    return h


# ---------------------------------------------------------------- leagues: helmets

def spartan_helmet():
    """BRONZE: a Spartan's bronze helmet with a T-shaped face opening and a tall red crest from front to back."""
    h = Hat('spartan-helmet', 0.82, 0.15)
    p = h.part()
    sides, sy = 40, 1.1
    prof = [(0.30, 0.0), (0.315, 0.012), (0.315, 0.05), (0.30, 0.062), (0.30, 0.12), (0.305, 0.19), (0.305, 0.27),
            (0.295, 0.34), (0.26, 0.42), (0.19, 0.48), (0.10, 0.515), (0.0, 0.525)]

    def face(k, i):
        d = off_front(i, sides)
        if k == 5 and d < 62:
            return DARK
        if k <= 4 and d < 10:
            return DARK
        return GOLD if k <= 2 else COPPER
    p.revolve(prof, sides, COPPER, sy=sy, cellfn=face)
    # The brow over the eyes: a raised ridge across the front.
    brow = [(0.307 * cos(radians(a)), 0.307 * sy * sin(radians(a)), 0.285) for a in range(25, 156, 10)]
    p.tube(brow, [0.022] * len(brow), 8, COPPER, flat=0.6, normal=(0, 0, 1))
    # The crest: a mane of horsehair over the top, from the brow to the nape; its own part, to sway.
    dome = [(0.295, 0.34), (0.26, 0.42), (0.19, 0.48), (0.10, 0.515), (0.0, 0.525)]
    base = [Vector((0, r * sy, z)) for r, z in dome] + [Vector((0, -r * sy, z)) for r, z in reversed(dome[:-1])]
    base += [Vector((0, -0.305 * sy, 0.27))]
    base = spline(base, 3)
    p.tube(base, [0.038] * len(base), 8, GOLD, flat=0.45, normal=(1, 0, 0))
    c = h.part('crest')
    m = len(base) - 1
    loops, ts = [], []
    for k, b in enumerate(base):
        u = k / m
        n = Vector((0.0, b.y, b.z - 0.15)).normalized()
        height = 0.07 + 0.2 * sin(pi * min(1.0, u * 1.1)) ** 0.6
        back = Vector((0, -1, 0)) * 0.05 * sin(pi * u)
        root, tip = b - n * 0.02, b + n * height + back
        th = 0.05
        loops.append([root + Vector((-th, 0, 0)), root.lerp(tip, 0.6) + Vector((-th * 1.05, 0, 0)),
                      tip + Vector((-th * 0.55, 0, 0)), tip + n * 0.012, tip + Vector((th * 0.55, 0, 0)),
                      root.lerp(tip, 0.6) + Vector((th * 1.05, 0, 0)), root + Vector((th, 0, 0))])
    vs = c.grid(loops, RED, cap_start=True, cap_end=True)
    for loop in vs:
        for j, vert in enumerate(loop):
            c.vt[vert] = (0.2, 0.6, 0.95, 1.0, 0.95, 0.6, 0.2)[j]
    c.paint(list(c.bm.faces), RED)
    return h


def knight_helmet():
    """SILVER: a knight's great helm: a steel bucket with an eye slit under a gold cross, breathing holes, and a plume
    of blue and white feathers."""
    h = Hat('knight-helmet', 0.95, 0.2)
    p = h.part()
    sides, sy = 36, 1.08
    prof = [(0.27, 0.0), (0.285, 0.012), (0.285, 0.055), (0.27, 0.067), (0.272, 0.18), (0.278, 0.29), (0.28, 0.325),
            (0.278, 0.36), (0.268, 0.44), (0.235, 0.51), (0.16, 0.565), (0.08, 0.59), (0.0, 0.598)]

    def face(k, i):
        d = off_front(i, sides)
        if k <= 2:
            return GOLD
        if k == 5 and 6 < d < 72:
            return DARK
        if d < 6 and 3 <= k <= 9:
            return GOLD
        if k == 6 and d < 36:
            return GOLD
        return CHROME
    p.revolve(prof, sides, CHROME, sy=sy, cellfn=face)
    for a in (55, 65, 75):
        for z in (0.11, 0.155, 0.2):
            rr = 0.272 + 0.006 * (z - 0.067) / 0.113
            p.ball((rr * cos(radians(a)), rr * sy * sin(radians(a)), z), 1.0, DARK, sub=1,
                   scale=(0.016, 0.016, 0.016), t=0.0)
    for k in range(12):
        a = 2 * pi * (k + 0.5) / 12
        p.ball((0.29 * cos(a), 0.29 * sy * sin(a), 0.034), 0.014, WHITE, sub=1, t=1.0)
    p.tube([(0, -0.06, 0.585), (0, -0.1, 0.64)], [0.04, 0.035], 10, GOLD)
    plume = h.part('plume')
    root = Vector((0, -0.1, 0.635))
    feathers = [((0.0, -0.25, 1.0), 0.42, BLUE), ((0.3, -0.45, 0.8), 0.4, WHITE), ((-0.3, -0.45, 0.8), 0.4, WHITE),
                ((0.15, -0.8, 0.5), 0.44, BLUE), ((-0.15, -0.8, 0.5), 0.44, BLUE), ((0.0, -0.9, 0.25), 0.4, SKY)]
    for direction, length, cell in feathers:
        d = Vector(direction).normalized()
        normal = d.cross(Vector((0, 0, 1))) if abs(d.z) < 0.95 else Vector((1, 0, 0))
        plume.blade(root, d, length, 0.072, 0.03, cell, normal.cross(d), bend=(0, -0.08, -0.12))
    return h


def samurai_helmet():
    """GOLD: a samurai's kabuto: a ridged gold bowl, a red neck guard of three tiers trimmed in gold, side flaps,
    and tall gold horns with a red sun between them."""
    h = Hat('samurai-helmet', 0.8, 0.15)
    p = h.part()
    p.revolve([(0.24, 0.0), (0.24, 0.17)], 24, DARK)
    bowl = [(0.25, 0.165), (0.27, 0.2), (0.272, 0.26), (0.255, 0.33), (0.215, 0.4), (0.15, 0.45), (0.07, 0.475),
            (0.0, 0.48)]
    p.revolve(bowl, 32, GOLD, lobes=16, amps=[0.0, 0.02, 0.035, 0.035, 0.035, 0.03, 0.015, 0.0])
    p.ball((0, 0, 0.485), 0.04, GOLD, sub=2, t=1.0)
    p.torus(0.255, 0.022, 32, 6, RED, m=T(0, 0, 0.18))
    sy = 1.05
    open_half = 55
    tiers = [(0.205, 0.135, 0.27, 0.33), (0.145, 0.075, 0.32, 0.385), (0.085, 0.0, 0.375, 0.44)]
    steps = 26
    for zt, zb, rt, rb in tiers:
        rows = []
        for s in range(steps + 1):
            a = radians(90 + open_half + (360 - 2 * open_half) * s / steps)
            loop = [(rt, zt), (rt + (rb - rt) * 0.72, zt + (zb - zt) * 0.72), (rb, zb), (rb - 0.03, zb + 0.008),
                    (rt - 0.03, zt)]
            rows.append([Vector((r * cos(a), r * sy * sin(a), z)) for r, z in loop])
        p.grid(rows, RED, cap_start=True, cap_end=True,
               cellfn=lambda k, i: (RED, GOLD, GOLD, DARK, RED)[i])
    flap = [(0.0, 0.0), (0.13, -0.01), (0.16, 0.05), (0.14, 0.12), (0.0, 0.12)]
    for side in (-1, 1):
        a = radians(90 - side * open_half)
        pos = Vector((0.3 * cos(a), 0.3 * sy * sin(a), 0.11))
        p.plate(flap, 0.025, GOLD, m=T(*pos) @ R(degrees(a) - side * 70, 'Z'))
        p.plate([(x * 0.85 + 0.01, z * 0.7 + 0.018) for x, z in flap], 0.03, RED,
                m=T(*pos) @ R(degrees(a) - side * 70, 'Z'))
    p.ball((0, 0, 0), 1.0, RED, sub=2, scale=(0.24, 0.15, 0.022), m=T(0, 0.2, 0.175) @ R(-10, 'X'))
    for side in (-1, 1):
        path = spline([(side * 0.03, 0.27, 0.25), (side * 0.1, 0.31, 0.38), (side * 0.17, 0.33, 0.52),
                       (side * 0.2, 0.32, 0.65), (side * 0.18, 0.3, 0.73)], 3)
        n = len(path) - 1
        p.tube(path, [0.032 + 0.016 * sin(pi * min(1.0, i / n * 1.3)) if i < n else 0.0 for i in range(n + 1)], 8,
               GOLD, flat=0.3, normal=(0, 1, 0), ts=[0.5 + 0.5 * i / n for i in range(n + 1)])
    p.revolve([(0.085, 0.0), (0.085, 0.02), (0.07, 0.035), (0.0, 0.04)], 20, GOLD, m=T(0, 0.27, 0.28) @ R(-80, 'X'))
    p.revolve([(0.062, 0.0), (0.062, 0.03), (0.05, 0.045), (0.0, 0.05)], 20, RED, m=T(0, 0.275, 0.28) @ R(-80, 'X'))
    return h


def space_helmet():
    """LEGEND: an astronaut's helmet with a rainbow visor in a gold frame, gold ear pieces, an aerial with a star,
    and a ring orbiting it (its own part, to spin)."""
    h = Hat('space-helmet', 0.75, 0.25)
    p = h.part()
    p.revolve([(0.255, 0.0), (0.272, 0.012), (0.272, 0.06), (0.255, 0.072), (0.17, 0.08)], 32, SLATE,
              cellfn=lambda k, i: GOLD if k == 1 else SLATE)
    centre, radius = 0.3, 0.3
    prof = []
    for e in range(-53, 91, 9):
        el = radians(min(e, 90))
        prof.append((radius * cos(el), centre + radius * sin(el)))
    p.revolve(prof, 32, WHITE)
    p.ball((0, 0.215, 0.33), 1.0, GOLD, sub=3, scale=(0.25, 0.12, 0.17))
    bands = [(0.43, PINK), (0.375, PURPLE), (0.32, BLUE), (0.265, SKY), (-1.0, GOLD)]
    p.ball((0, 0.24, 0.33), 1.0, BLUE, sub=3, scale=(0.235, 0.13, 0.155),
           cellfn=lambda c: next(cell for z, cell in bands if c.z > z))
    p.ball((0, 0, 0), 1.0, WHITE, sub=2, scale=(0.05, 0.015, 0.022), m=T(-0.09, 0.348, 0.42) @ R(-25, 'Y'), t=1.0)
    for side in (-1, 1):
        p.revolve([(0.075, 0.0), (0.075, 0.03), (0.06, 0.05), (0.0, 0.055)], 20, GOLD,
                  m=T(side * 0.285, 0, 0.31) @ R(side * 90, 'Y'))
    p.star((0, 0, 0), 0.05, GOLD, depth=0.025, m=T(-0.2, 0.08, 0.5) @ R(120, 'Z') @ R(-35, 'X'), t=1.0)
    p.tube([(0.33, 0, 0.33), (0.35, -0.01, 0.48), (0.36, -0.02, 0.62)], [0.016, 0.014, 0.012], 8, CHROME)
    blink = h.part('blink')
    blink.star((0, 0, 0), 0.065, GOLD, depth=0.03, m=T(0.362, -0.02, 0.67), t=1.0)
    orbit = h.part('orbit')
    # Tipped forward so the near side passes under the visor, not across it.
    tilt = T(0, 0, 0.32) @ R(-26, 'X') @ R(-8, 'Y')
    orbit.torus(0.47, 0.02, 48, 6, GOLD, m=tilt)
    orbit.ball(tilt @ Vector((0.47 * cos(radians(30)), 0.47 * sin(radians(30)), 0)), 0.06, PURPLE, sub=2)
    orbit.torus(0.085, 0.01, 20, 4, PINK, m=tilt @ T(0.47 * cos(radians(30)), 0.47 * sin(radians(30)), 0) @ R(25, 'X'))
    orbit.star((0, 0, 0), 0.045, GOLD, depth=0.02,
               m=tilt @ T(0.47 * cos(radians(215)), 0.47 * sin(radians(215)), 0) @ R(90, 'X'), t=1.0)
    return h


SHOP = [top_hat, cone, antenna, flag, propeller, viking_helmet, duck, halo, rocket, crown, wizard_hat]
LEAGUE = [spartan_helmet, knight_helmet, samurai_helmet, space_helmet]
