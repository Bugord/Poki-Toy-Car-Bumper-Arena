"""COMET, the LEGEND league car, in the Kenney Toy Car Kit style (shared parts in carkit.py).

A rocket car: a pointed body loft with a rainbow racing stripe down its top (cut into the loft's faces), a canopy with
its windows cut into its faces, stub wings, a low tail fin, a chrome nozzle and a rainbow flame of nested cones.
The canopy is the top by more than 6 cm, for the hats.

Run: blender -b --factory-startup --python Tools/cars/build_comet.py -- <kit wheel folder> <colormap png> <out fbx>
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carkit as k

WHEELS, COLORMAP, OUT = (os.path.abspath(a) for a in sys.argv[sys.argv.index('--') + 1:][:3])
k.begin(WHEELS, COLORMAP, 45.0)

# The body's key sections: y, bottom half width, top half width, bottom, top, chamfer; stations between are blends.
KEYS = [(-40.0, 13.75, 12.5, 12.5, 27.5, 3.75), (-37.5, 16.25, 15.0, 10.0, 28.75, 3.125),
        (-10.0, 16.25, 15.0, 10.0, 28.75, 3.125), (10.0, 15.0, 13.75, 10.0, 26.25, 3.125),
        (30.0, 11.25, 10.0, 11.25, 21.25, 2.5), (39.375, 6.25, 5.0, 13.125, 18.125, 1.875),
        (42.5, 2.5, 1.875, 14.375, 16.875, 0.625)]


def key_at(y):
    for a, b in zip(KEYS, KEYS[1:]):
        if a[0] <= y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return tuple(p + (q - p) * t for p, q in zip(a, b))


def station(y):
    _, bh, th, bottom, top, ch = key_at(y)
    return y, k.racing(bh, th, bottom, top, min(4.375, (th - ch) * 0.6), ch)


ys = [-40.0, -37.5, -26.25, -15.0, -3.75, 7.5, 18.75, 30.0, 39.375, 42.5]
# A rainbow racing stripe down the top, the side the arena camera sees: pink at the tail to red at the nose (never
# green: green is the paint the game recolours). The canopy sits over its middle.
rainbow = [k.PINK, k.PINK, k.PURPLE, k.BLUE, k.SKY, k.GOLD, k.ORANGE, k.RED, k.RED]
stripe = {(b, 4): cell for b, cell in enumerate(rainbow)}
k.loft([station(y) for y in ys], k.PAINT, faces=stripe)

# The canopy: a painted base sunk into the body, a glass band with windows on all sides and a small painted top.
glass = (k.DARK, k.SKY, 2.5, 1.25, 1.25)
k.shell([k.ring(-10.0, 10.0, -22.5, 2.5, 26.25, 2.5),
         k.ring(-10.0, 10.0, -22.5, 2.5, 28.75, 2.5),
         k.ring(-8.125, 8.125, -18.75, -3.75, 36.25, 2.5),
         k.ring(-6.25, 6.25, -16.25, -6.25, 38.75, 1.875)],
        [k.PAINT, k.DARK, k.PAINT],
        panels={(1, 0): glass, (1, 2): glass, (1, 4): glass, (1, 6): glass})


def plate(points_lo, points_hi, cell):
    """A solid between two equal flat quads of points, its sides as quads."""
    lo = [k.v(p) for p in points_lo]
    hi = [k.v(p) for p in points_hi]
    n = len(lo)
    faces = [k.bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])) for i in range(n)]
    faces += [k.bm.faces.new(list(reversed(lo))), k.bm.faces.new(hi)]
    k.paint(faces, cell)


# A low swept tail fin and stub wings between the wheels.
fin = [(-38.75, 26.25), (-38.75, 32.5), (-34.375, 32.5), (-25.0, 26.25)]
plate([(-1.25, y, z) for y, z in fin], [(1.25, y, z) for y, z in fin], k.PAINT)
for s in (-1, 1):
    root = [(s * 13.75, -6.25, 15.0), (s * 13.75, 10.0, 15.0), (s * 13.75, 10.0, 17.5), (s * 13.75, -6.25, 17.5)]
    tip = [(s * 26.25, -5.0, 15.625), (s * 26.25, 1.25, 15.625), (s * 26.25, 1.25, 16.875),
           (s * 26.25, -5.0, 16.875)]
    plate(root, tip, k.PAINT)

# The chrome nozzle and the flame: nested cones sunk into each other, orange, gold and pink to the tip.
k.pipe([(0.0, -38.75, 20.0), (0.0, -44.375, 20.0)], 7.5, k.CHROME, sides=10, lens=k.DARK)
k.cone((0.0, -44.375, 20.0), '-y', 5.625, 5.0, 3.75, k.ORANGE, sides=10)
k.cone((0.0, -47.5, 20.0), '-y', 5.0, 3.75, 4.375, k.GOLD, sides=10)
k.cone((0.0, -51.25, 20.0), '-y', 3.75, 0.625, 5.0, k.PINK, sides=10)

k.finish('vehicle-comet', OUT, [
    ('wheel-fl', 'wheel-small', -17.5, 25.0), ('wheel-fr', 'wheel-small', 17.5, 25.0),
    ('wheel-bl', 'wheel-medium', -20.0, -25.0), ('wheel-br', 'wheel-medium', 20.0, -25.0)])
