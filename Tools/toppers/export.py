"""Exports every hat to Assets/Art/Models/Toppers/mdl_topper_<name>.fbx for Unity.

Each part is its own object: 'body', and the parts the game moves ('blades', 'sway', 'crest', 'plume', 'blink',
'orbit'), each with its origin on its pivot (and the orbit turned to its ring), so Unity turns them in place.
The hats are built facing +Y and turned round here: with the axes the car kit exports with, Unity sees the front
along +Z, as CarTopper expects.

    blender -b --factory-startup --python Tools/toppers/export.py -- [hat ids]

A new hat also needs its .meta (copy another topper's with a new guid: Read/Write, scale 1, no materials) and its
entry in Assets/Data/Cars/data_topper_models. Look at it first: render.py writes Logs/toppers-preview, sheet.py lays
those out in rows.
"""
import bpy, sys, os
from math import radians, pi
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
COLORMAP = os.path.join(PROJECT, 'Assets', 'Art', 'Textures', 'tex_toy_car_kit_colormap.png')
OUT = os.path.join(PROJECT, 'Assets', 'Art', 'Models', 'Toppers')
sys.path.insert(0, HERE)
import hatkit, hats, hats2

T, R = hatkit.T, hatkit.R

# Pivot of each moving part (Blender space, before the hat is turned round), and its turn.
PIVOTS = {
    ('antenna', 'sway'): (T(0, 0, 0.3), Matrix.Identity(4)),
    ('propeller', 'blades'): (T(0, 0, 0.44), Matrix.Identity(4)),
    ('spartan-helmet', 'crest'): (T(0, 0, 0.3), Matrix.Identity(4)),
    ('knight-helmet', 'plume'): (T(0, -0.1, 0.635), Matrix.Identity(4)),
    ('space-helmet', 'blink'): (T(0.362, -0.02, 0.67), Matrix.Identity(4)),
    ('space-helmet', 'orbit'): (T(0, 0, 0.32), R(-26, 'X') @ R(-8, 'Y')),
}

only = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None
os.makedirs(OUT, exist_ok=True)
material = hatkit.colormap_material(COLORMAP)
turn = R(180, 'Z')
makers = hats.SHOP + hats2.SHOP + hats2.LEAGUE
for make in makers:
    name = make.__name__.replace('_', '-')
    if only and name not in only:
        continue
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    hat = make()
    col = bpy.context.scene.collection
    root, counts = hatkit.make_objects(hat, material, col)
    objects = [o for o in root.children]
    for o in objects:
        part = o.name.split('.', 1)[1]
        pivot, rotation = PIVOTS.get((name, part), (Matrix.Identity(4), Matrix.Identity(4)))
        place = pivot @ rotation
        o.data.transform(place.inverted())
        o.parent = None
        o.matrix_world = turn @ place
        o.name = part
        o.data.name = f'{name}.{part}'
    bpy.data.objects.remove(root)
    bpy.context.view_layer.update()
    for o in bpy.data.objects:
        o.select_set(True)
    out = os.path.join(OUT, 'mdl_topper_' + name.replace('-', '_') + '.fbx')
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP')
    print('EXPORT', name, [o.name for o in objects], counts, out)
