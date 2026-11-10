"""Builds the hats and renders each alone (two sides) and on the racer. Set ONLY = [...] before exec to pick hats."""
import bpy, bmesh, sys, os, math
from math import sin, cos, radians
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
COLORMAP = os.path.join(PROJECT, 'Assets', 'Art', 'Textures', 'tex_toy_car_kit_colormap.png')
CAR = os.path.join(PROJECT, 'Assets', 'Art', 'Models', 'Cars', 'mdl_car_racer.fbx')
OUT = os.path.join(PROJECT, 'Logs', 'toppers-preview')
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, HERE)
import hatkit, hats, hats2

only = globals().get('ONLY') or (sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None)
size = globals().get('SIZE', 560)
log = {}

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
scene = bpy.context.scene
for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try:
        scene.render.engine = engine
        break
    except Exception:
        pass
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.resolution_x = size
scene.render.resolution_y = size
scene.render.resolution_percentage = 100

world = bpy.data.worlds.new('preview')
scene.world = world
try:
    world.use_nodes = True
except Exception:
    pass
if world.node_tree and 'Background' in world.node_tree.nodes:
    bg = world.node_tree.nodes['Background']
    bg.inputs[0].default_value = (0.85, 0.9, 1.0, 1.0)
    bg.inputs[1].default_value = 0.75

sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
sun.data.energy = 3.2
sun.data.angle = radians(8)
sun.rotation_euler = (radians(48), 0, radians(145))
scene.collection.objects.link(sun)

cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
cam.data.lens = 90
scene.collection.objects.link(cam)
scene.camera = cam


def aim(target, az, el, dist):
    d = Vector((sin(radians(az)) * cos(radians(el)), cos(radians(az)) * cos(radians(el)), sin(radians(el))))
    cam.location = target + d * dist
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()


def bounds(objects):
    pts = []
    for o in objects:
        if o.type == 'MESH' and not o.name.endswith('.hull'):
            pts += [o.matrix_world @ v.co for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def shot(path, target, az, el, radius):
    aim(target, az, el, radius / math.tan(cam.data.angle / 2) * 1.12)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


material = hatkit.colormap_material(COLORMAP)
outline = hatkit.outline_material()

# The racer, as the game shows it: the atlas material and the outline.
bpy.ops.import_scene.fbx(filepath=CAR)
car_objects = [o for o in scene.objects if o.type == 'MESH']
bpy.context.view_layer.update()
# The game shows the kit's cars three times their size (2.6 m long).
for o in [o for o in scene.objects if o.parent is None and o not in (sun, cam)]:
    o.matrix_world = Matrix.Scale(3.0, 4) @ o.matrix_world
bpy.context.view_layer.update()
hull = bmesh.new()
for o in car_objects:
    o.data.materials.clear()
    o.data.materials.append(material)
    part = bmesh.new()
    part.from_mesh(o.data)
    part.transform(o.matrix_world)
    part.normal_update()
    for v in part.verts:
        v.co += v.normal * 0.02
    bmesh.ops.reverse_faces(part, faces=part.faces)
    tmp = bpy.data.meshes.new('tmp')
    part.to_mesh(tmp)
    part.free()
    hull.from_mesh(tmp)
    bpy.data.meshes.remove(tmp)
hull_mesh = bpy.data.meshes.new('car.hull')
hull.to_mesh(hull_mesh)
hull.free()
hull_mesh.materials.append(outline)
car_hull = bpy.data.objects.new('car.hull', hull_mesh)
car_hull.visible_shadow = False
scene.collection.objects.link(car_hull)
car_all = car_objects + [car_hull]
car_lo, car_hi = bounds(car_objects)
body = [o for o in car_objects if 'wheel' not in o.name.lower()]
top_pts = [o.matrix_world @ v.co for o in body for v in o.data.vertices]
top_z = max(p.z for p in top_pts)
near = [p for p in top_pts if p.z > top_z - 0.02]
roof = Vector((sum(p.x for p in near) / len(near), sum(p.y for p in near) / len(near), top_z))
front_wheels = [o for o in car_objects if o.name.lower().startswith('wheel-f')]
front = 1.0 if not front_wheels or sum((o.matrix_world.translation.y for o in front_wheels)) > 0 else -1.0
log['car'] = {'lo': tuple(car_lo), 'hi': tuple(car_hi), 'roof': tuple(roof), 'front': front,
              'objects': [o.name for o in car_objects]}

for make in hats.SHOP + hats2.SHOP + hats2.LEAGUE:
    name = make.__name__.replace('_', '-')
    if only and name not in only:
        continue
    hat = make()
    col = bpy.data.collections.new(hat.name)
    scene.collection.children.link(col)
    root, counts = hatkit.make_objects(hat, material, col, outline=outline, thickness=0.014)
    bpy.context.view_layer.update()
    lo, hi = bounds(col.objects)
    log[hat.name] = {'verts': counts[0], 'tris': counts[1], 'size': tuple(round(c, 3) for c in hi - lo),
                     'lo': tuple(round(c, 3) for c in lo), 'parts': [p.name for p in hat.parts]}
    centre = (lo + hi) / 2
    radius = (hi - lo).length / 2
    for o in car_all:
        o.hide_render = True
    az_a, az_b = {'fin': (62, 118), 'phoenix': (48, 150)}.get(hat.name, (38, 142))
    shot(os.path.join(OUT, f'{hat.name}_a.png'), centre, az_a, 20, radius)
    shot(os.path.join(OUT, f'{hat.name}_b.png'), centre, az_b, 24, radius)
    # On the roof of the racer at the game's scale, from above like the arena camera.
    for o in car_all:
        o.hide_render = False
    root.location = roof
    root.scale = (1.3, 1.3, 1.3)
    root.rotation_euler = (0, 0, 0 if front > 0 else math.pi)
    bpy.context.view_layer.update()
    lo2, hi2 = bounds(list(col.objects) + car_objects)
    shot(os.path.join(OUT, f'{hat.name}_car.png'), (lo2 + hi2) / 2, 40 if front > 0 else 220, 36,
         (hi2 - lo2).length / 2)
    for o in col.objects:
        o.hide_render = True

result = log
for key, value in log.items():
    print('LOG', key, value)
if globals().get('SAVE') or os.environ.get('HATS_SAVE'):
    for o in scene.objects:
        o.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'hats.blend'))
