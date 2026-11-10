"""DOZER, the GOLD league car, in the Kenney Toy Car Kit style (shared parts in carkit.py).

A toy bulldozer: two hollow tracks around the kit's wheels (the game turns them, so they are the car's wheels) with
painted fenders over them, an engine housing with a tall cab whose windows are cut into its faces, an exhaust, and a
big gold blade on arms with chrome rams in front. The cab's roof is the top by more than 6 cm, for the hats.

Run: blender -b --factory-startup --python Tools/cars/build_dozer.py -- <kit wheel folder> <colormap png> <out fbx>
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carkit as k

WHEELS, COLORMAP, OUT = (os.path.abspath(a) for a in sys.argv[sys.argv.index('--') + 1:][:3])
k.begin(WHEELS, COLORMAP, 60.0)

WHEEL_Y, AXLE_Z, TRACK_R = 20.0, 16.25, 16.25
TRACK_IN, TRACK_OUT = 15.0, 28.75
WHEEL_X = (TRACK_IN + TRACK_OUT) / 2

# The tracks: a band around both wheels, bogie rollers on the bottom run and a painted fender on top.
loop = k.stadium(-WHEEL_Y, WHEEL_Y, AXLE_Z, TRACK_R)
for s in (-1, 1):
    x0, x1 = sorted((s * TRACK_IN, s * TRACK_OUT))
    k.band(loop, x0, x1, 2.5, k.DARK)
    for y in (-6.25, 6.25):
        k.disc((x0 + 1.25, y, 7.5), 'x', 5.625, x1 - x0 - 2.5, k.TRIM, face_cell=k.DARK)
    f0, f1 = sorted((s * 14.375, s * 30.0))
    k.box(f0, f1, -38.75, 38.75, 31.875, 35.0, k.PAINT, ch=1.25)

# The chassis between the tracks.
k.box(-15.625, 15.625, -27.5, 27.5, 8.75, 23.75, k.DARK)

# The engine housing: high under the cab, stepping down over the hood to a narrower nose with a grille sunk in it.
k.loft([(-35.0, k.tapered(13.75, 12.5, 22.5, 41.25, 3.125)), (-32.5, k.tapered(15.0, 13.75, 21.25, 42.5)),
        (-5.0, k.tapered(15.0, 13.75, 21.25, 42.5)), (0.0, k.tapered(15.0, 13.75, 21.25, 38.75)),
        (13.75, k.tapered(15.0, 13.75, 21.25, 36.25)), (17.5, k.tapered(13.75, 12.5, 22.5, 33.75, 3.125))],
       k.PAINT)
k.box(-8.75, 8.75, 16.875, 18.125, 25.0, 31.25, k.DARK, ch=0.625)

# The tall cab with windows on all four sides and a roof with a lip.
glass = (k.DARK, k.SKY, 2.5, 1.25, 1.25)
k.shell([k.ring(-13.75, 13.75, -31.25, -6.25, 41.25, 1.875),
         k.ring(-13.75, 13.75, -31.25, -6.25, 43.75, 1.875),
         k.ring(-12.5, 12.5, -30.0, -8.125, 55.0, 1.875),
         k.ring(-13.75, 13.75, -31.25, -6.875, 56.25, 1.875),
         k.ring(-12.5, 12.5, -30.0, -8.125, 58.125, 1.875)],
        [k.PAINT, k.DARK, k.PAINT, k.PAINT],
        panels={(1, 0): glass, (1, 2): glass, (1, 4): glass, (1, 6): glass})

# The exhaust on the hood, below the roof by more than the 6 cm the hats need.
k.pipe([(8.75, 5.0, 36.25), (8.75, 5.0, 50.0)], 1.875, k.METAL, lens=k.DARK)

# The blade: a curved gold plate, concave to the front, built in planar quads (no concave n-gons).
back = [(40.0, 1.25), (37.5, 6.25), (36.25, 15.0), (37.5, 23.75), (40.0, 30.0)]
front = [(y + 3.125, z) for y, z in back]
half = 31.25
L = [[k.v((x, y, z)) for y, z in back] for x in (-half, half)]
R = [[k.v((x, y, z)) for y, z in front] for x in (-half, half)]
quads = []
for i in range(len(back) - 1):
    quads += [(L[0][i], L[0][i + 1], L[1][i + 1], L[1][i]), (R[0][i], R[1][i], R[1][i + 1], R[0][i + 1]),
              (L[0][i], R[0][i], R[0][i + 1], L[0][i + 1]), (L[1][i], L[1][i + 1], R[1][i + 1], R[1][i])]
quads += [(L[0][0], L[1][0], R[1][0], R[0][0]), (L[0][-1], R[0][-1], R[1][-1], L[1][-1])]
k.paint([k.bm.faces.new(q) for q in quads], k.GOLD)

# The arms from the chassis to the blade and the chrome rams from the housing.
for s in (-1, 1):
    x0, x1 = sorted((s * 10.0, s * 13.75))
    k.box(x0, x1, 26.25, 37.5, 9.375, 14.375, k.DARK)
    k.pipe([(s * 11.25, 12.5, 30.0), (s * 11.25, 38.125, 21.875)], 1.5, k.METAL)

# The front wheels are named wheel-ml and wheel-mr, not wheel-fl and wheel-fr: the game turns a front wheel ("-f" in its
# name) with the steering, and a turned wheel would poke through the track around it. All four still roll.
k.finish('vehicle-dozer', OUT, [
    ('wheel-ml', 'wheel-small', -WHEEL_X, WHEEL_Y, AXLE_Z), ('wheel-mr', 'wheel-small', WHEEL_X, WHEEL_Y, AXLE_Z),
    ('wheel-bl', 'wheel-small', -WHEEL_X, -WHEEL_Y, AXLE_Z), ('wheel-br', 'wheel-small', WHEEL_X, -WHEEL_Y, AXLE_Z)])
