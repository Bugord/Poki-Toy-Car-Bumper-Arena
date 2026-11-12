"""HOT ROD, the BRONZE league car, in the Kenney Toy Car Kit style (shared parts in carkit.py).

A highboy: one flowing body from the round tail over the cowl down to the nose, a chopped cab with its windows cut
into its faces between huge bare rear wheels, small front wheels on an open axle, a chrome engine with a blower and
upright stacks on the hood and copper headers along the sills. The roof stays the highest part by more than 6 cm,
because the game sits hats on the model's highest vertices.

Run: blender -b --factory-startup --python Tools/cars/build_hot_rod.py -- <kit wheel folder> <colormap png> <out fbx>
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carkit as k

WHEELS, COLORMAP, OUT = (os.path.abspath(a) for a in sys.argv[sys.argv.index('--') + 1:][:3])
k.begin(WHEELS, COLORMAP, 50.0)

REAR_Y, FRONT_Y = -24.0, 28.75
REAR_X, FRONT_X = 23.75, 20.0
rear_r = k.wheel_radius('wheel-large')
front_r = k.wheel_radius('wheel-small')

# The frame under the body and the axles the wheels hang on.
k.box(-10.0, 10.0, -38.75, 36.875, 7.5, 13.75, k.DARK)
k.box(-FRONT_X + 3.75, FRONT_X - 3.75, FRONT_Y - 2.5, FRONT_Y + 2.5, front_r - 2.5, front_r + 2.5, k.DARK)
k.box(-REAR_X + 6.25, REAR_X - 6.25, REAR_Y - 3.75, REAR_Y + 3.75, rear_r - 3.75, rear_r + 3.75, k.DARK)

# One body: the round tail, the tub under the cab, the cowl sloping down to the hood and the narrowing nose. Its
# sides lean in a little, like the kit's cars.
k.loft([(-43.125, k.tapered(11.25, 10.0, 15.0, 26.25, 3.75)),
        (-40.625, k.tapered(13.75, 12.5, 11.875, 29.375, 3.125)),
        (-37.5, k.tapered(14.375, 13.125, 11.25, 30.625, 2.5)),
        (-10.0, k.tapered(14.375, 13.125, 11.25, 30.625, 2.5)),
        (-5.0, k.tapered(13.125, 11.875, 11.25, 28.125, 2.5)),
        (0.0, k.tapered(11.25, 10.0, 11.25, 25.0, 2.5)),
        (30.0, k.tapered(11.25, 10.0, 11.25, 23.75, 2.5)),
        (35.0, k.tapered(10.0, 8.75, 12.5, 21.25, 3.125)),
        (36.875, k.tapered(8.125, 6.875, 14.375, 19.375, 2.5))], k.PAINT)

# The chopped cab rises out of the tub: a painted base, the glass band leaning in with the windows cut into its
# faces and a low roof with a chamfered top.
k.shell([k.ring(-11.25, 11.25, -31.25, -11.25, 28.125, 1.875),
         k.ring(-11.25, 11.25, -31.25, -11.25, 31.875, 1.875),
         k.ring(-10.0, 10.0, -30.0, -15.0, 40.625, 1.875),
         k.ring(-10.625, 10.625, -30.625, -14.375, 42.5, 1.875),
         k.ring(-9.375, 9.375, -29.375, -15.625, 44.375, 1.875)],
        [k.PAINT, k.DARK, k.PAINT, k.PAINT],
        panels={(1, 0): (k.DARK, k.SKY, 2.5, 1.25, 1.25), (1, 4): (k.DARK, k.SKY, 2.5, 1.25, 1.25),
                (1, 2): (k.DARK, k.SKY, 2.5, 1.25, 1.25), (1, 6): (k.DARK, k.SKY, 2.5, 1.25, 1.25)})

# The grille shell sunk into the nose with the chrome grille sunk into it, and round headlamps on stalks.
k.box(-8.75, 8.75, 34.375, 38.125, 10.625, 23.75, k.PAINT, ch=1.875)
k.box(-6.25, 6.25, 37.5, 38.75, 13.125, 21.25, k.METAL, ch=0.625)
for x in (-11.875, 11.875):
    k.box(x - 1.25, x + 1.25, 30.0, 32.5, 18.125, 25.625, k.DARK, ch=0.625)
    k.disc((x, 28.75, 27.5), 'y', 3.75, 5.625, k.PAINT, face_cell=k.SKY)

# The engine sunk into the hood: block, blower, its intake and two rows of stacks leaning out.
k.box(-8.125, 8.125, 1.25, 22.5, 21.875, 30.625, k.CHROME, ch=1.875)
k.box(-5.625, 5.625, 4.375, 18.75, 29.375, 34.375, k.METAL, ch=1.25)
k.box(-3.75, 3.75, 6.875, 16.25, 33.125, 36.25, k.DARK, ch=1.25)
for x in (-6.875, 6.875):
    for y in (4.375, 11.875, 19.375):
        k.pipe([(x, y, 28.75), (x * 1.2, y, 33.125), (x * 1.35, y, 36.875)], 1.5, k.METAL, lens=k.DARK)

# The copper headers drop out of the block and run back along the sills, ending ahead of the rear wheels.
for s in (-1, 1):
    k.pipe([(s * 5.625, 13.75, 26.25), (s * 13.75, 12.5, 19.375), (s * 16.875, 8.75, 15.625),
            (s * 16.875, -2.5, 15.625)], 2.5, k.COPPER, sides=6, lens=k.DARK)

# The tail lights sunk into the round tail.
for x in (-8.75, 8.75):
    k.box(x - 2.5, x + 2.5, -43.75, -40.0, 21.875, 25.625, k.RED, ch=0.625)

k.finish('vehicle-hot-rod', OUT, [
    ('wheel-fl', 'wheel-small', -FRONT_X, FRONT_Y), ('wheel-fr', 'wheel-small', FRONT_X, FRONT_Y),
    ('wheel-bl', 'wheel-large', -REAR_X, REAR_Y), ('wheel-br', 'wheel-large', REAR_X, REAR_Y)])
