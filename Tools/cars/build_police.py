"""POLICE, the SILVER league car, in the Kenney Toy Car Kit style (shared parts in carkit.py).

A four-door cruiser: one body loft with white doors cut into its sides and a silver badge with a gold star in the
front doors, a cab with split side windows, a push bar on the front bumper and a red and blue light bar on the roof.
The light bar is the top; the game sits hats on it, in the middle of the roof.

Run: blender -b --factory-startup --python Tools/cars/build_police.py -- <kit wheel folder> <colormap png> <out fbx>
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carkit as k

WHEELS, COLORMAP, OUT = (os.path.abspath(a) for a in sys.argv[sys.argv.index('--') + 1:][:3])
k.begin(WHEELS, COLORMAP, 50.0)

WHEEL_X, WHEEL_Y = 18.75, 26.25

side = k.tapered(20.625, 19.375, 9.375, 28.125, 2.5)
# The doors: white panels cut into both sides of the two middle bands; the front ones carry the badge.
door = (k.PAINT, k.METAL, 1.25, 0.625, 0.625)
badge = (k.PAINT, None, 1.25, 0.625, 0.625, 1,
         (k.METAL, None, 2.5, 3.125, 3.125, 1, (k.CHROME, k.GOLD, 1.875, 1.875, 1.875)))
k.loft([(-42.5, k.tapered(18.75, 17.5, 11.25, 26.25, 3.125)), (-40.0, side), (-15.0, side), (0.0, side),
        (15.0, side), (30.0, k.tapered(20.625, 19.375, 9.375, 26.875, 2.5)),
        (38.75, k.tapered(20.625, 19.375, 9.375, 24.375, 2.5)),
        (41.25, k.tapered(19.375, 18.125, 10.625, 23.125, 3.125))],
       k.PAINT, panels={(2, 1): door, (2, 5): door, (3, 1): badge, (3, 5): badge})

# The cab rises out of the body: a painted base, the glass band leaning in (split side windows) and the roof.
glass = (k.DARK, k.SKY, 3.125, 1.25, 1.25)
k.shell([k.ring(-17.5, 17.5, -28.75, 13.75, 26.25, 2.5),
         k.ring(-17.5, 17.5, -28.75, 13.75, 29.375, 2.5),
         k.ring(-15.625, 15.625, -25.0, 6.25, 39.375, 2.5),
         k.ring(-16.25, 16.25, -25.625, 6.875, 40.625, 2.5),
         k.ring(-15.0, 15.0, -24.375, 5.625, 42.5, 2.5)],
        [k.PAINT, k.DARK, k.PAINT, k.PAINT],
        panels={(1, 0): glass, (1, 4): glass, (1, 2): glass + (2,), (1, 6): glass + (2,)})

# The light bar in the middle of the roof: a dark base and a red and a blue lamp sunk into it.
roof_y = (-24.375 + 5.625) / 2
k.box(-11.25, 11.25, roof_y - 3.125, roof_y + 3.125, 41.875, 44.375, k.DARK)
k.box(-10.625, -0.625, roof_y - 2.5, roof_y + 2.5, 43.75, 46.875, k.RED)
k.box(0.625, 10.625, roof_y - 2.5, roof_y + 2.5, 43.75, 46.875, k.BLUE)

# Bumpers sunk into the ends, a push bar sunk into the front bumper, head and tail lamps sunk into the body.
k.box(-21.25, 21.25, 38.125, 44.375, 7.5, 16.25, k.TRIM, ch=1.875)
k.box(-21.25, 21.25, -44.375, -39.375, 7.5, 16.25, k.TRIM, ch=1.875)
for x in (-8.75, 8.75):
    k.box(x - 1.25, x + 1.25, 43.75, 46.25, 8.75, 23.75, k.DARK)
k.box(-11.25, 11.25, 44.375, 46.875, 18.125, 20.625, k.DARK)
for s in (-1, 1):
    x0, x1 = sorted((s * 11.25, s * 16.875))
    k.box(x0, x1, 40.0, 41.875, 18.75, 21.875, k.SKY, ch=0.625)
    k.box(x0, x1, -43.125, -41.875, 20.0, 23.125, k.RED, ch=0.625)

k.finish('vehicle-police', OUT, [
    ('wheel-fl', 'wheel-small', -WHEEL_X, WHEEL_Y), ('wheel-fr', 'wheel-small', WHEEL_X, WHEEL_Y),
    ('wheel-bl', 'wheel-small', -WHEEL_X, -WHEEL_Y), ('wheel-br', 'wheel-small', WHEEL_X, -WHEEL_Y)])
