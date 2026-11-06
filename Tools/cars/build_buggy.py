"""Builds the BUGGY, a toy after the Ariel Nomad, in the Kenney Toy Car Kit style on the kit's colormap atlas.

Like the kit: sizes on a 1.25 cm grid, chunky parts with one 45-degree chamfer, the body in one paint cell (the one
the game repaints), every face's UV in the right half of its atlas cell with its height picking the cell's vertical
gradient. Paint: the nose shell, the cockpit sides and the four cycle wings. Grey-blue trim (1,0): the roll cage, the
skid plate, the lamp housings and the engine. Dark slate (2,0): the chassis, the floor, the nose face and the seats.
White (4,0): the lamp lenses. The wheels are the kit's own wheel-medium on all four corners.

Front +Y, up +Z, ground z = 0, written in cm and exported in metres, like the imported Kenney racer.
Run with absolute paths (Blender resolves relative ones against its own folder):
     blender -b --python Tools/cars/build_buggy.py -- <kit wheel folder> <project>/Assets/Art/Textures/tex_toy_car_kit_colormap.png
     <project>/Assets/Art/Models/Cars/mdl_car_buggy.fbx
The kit wheel folder holds wheel-medium.fbx from the Kenney Toy Car Kit pack (not in the repo), then
Tools > Cars > Update Catalog redraws the portraits.
"""
import bpy, bmesh, sys, math
from mathutils import Vector

WHEELS, COLORMAP, OUT = sys.argv[sys.argv.index('--') + 1:]

PAINT = (1, 1)
TRIM = (1, 0)
DARK = (2, 0)
METAL = (4, 0)
CM = 0.01
TOP = 52.0


def kit_wheel(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f'{WHEELS}/{name}.fbx')
    o = bpy.data.objects[name]
    centre = o.matrix_world.translation.copy()
    points = [o.matrix_world @ p.co - centre for p in o.data.vertices]
    faces = [list(p.vertices) for p in o.data.polygons]
    uvl = o.data.uv_layers.active.data
    uvs = {(p.index, o.data.loops[li].vertex_index): uvl[li].uv.copy() for p in o.data.polygons for li in p.loop_indices}
    return points, faces, uvs


WHEEL = kit_wheel('wheel-medium')
bpy.ops.wm.read_factory_settings(use_empty=True)

bm = bmesh.new()
uv_layer = bm.loops.layers.uv.new('UVMap')
ROUND = set()   # tube vertices: their edges shade smooth up to 50 degrees, the rest of the body only up to 40


def paint(faces, cell):
    for f in faces:
        for loop in f.loops:
            t = min(1.0, max(0.0, loop.vert.co.z / (TOP * CM)))
            loop[uv_layer].uv = ((cell[0] + 0.75) / 8.0, (cell[1] + 0.1 + 0.8 * t) / 4.0)


def v(p):
    return bm.verts.new((p[0] * CM, p[1] * CM, p[2] * CM))


def closed(loops, cell, end_cell=None, cap_end=True):
    """Quads between equal loops, the first end capped and the last one too unless cap_end is off."""
    n = len(loops[0])
    faces = [bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])) for lo, hi in zip(loops, loops[1:])
             for i in range(n)]
    paint(faces, cell)
    paint([bm.faces.new(list(reversed(loops[0])))], cell)
    if cap_end:
        paint([bm.faces.new(loops[-1])], end_cell or cell)


def box(x0, x1, y0, y1, z0, z1, cell, ch=1.25, top_cell=None):
    """A box with its four vertical edges and its top edges chamfered."""
    def ring(inset, z):
        a, b = x0 + inset, x1 - inset
        c, d = y0 + inset, y1 - inset
        return [v(p + (z,)) for p in ((a + ch, c), (b - ch, c), (b, c + ch), (b, d - ch),
                                      (b - ch, d), (a + ch, d), (a, d - ch), (a, c + ch))]
    loops = [ring(0.0, z0), ring(0.0, z1 - ch), ring(ch, z1)]
    n = len(loops[0])
    faces = [bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])) for lo, hi in zip(loops, loops[1:])
             for i in range(n)]
    faces.append(bm.faces.new(list(reversed(loops[0]))))
    paint(faces, cell)
    paint([bm.faces.new(loops[-1])], top_cell or cell)


def loft(stations, cell, end_cell=None):
    """A solid lofted along Y through sections: (y, [(x, z), ...]), each section clockwise seen from +Y."""
    closed([[v((x, y, z)) for x, z in section] for y, section in stations], cell, end_cell)


def section(half, bottom, top, ch=2.5):
    return [(-half + ch, bottom), (-half, bottom + ch), (-half, top - ch), (-half + ch, top),
            (half - ch, top), (half, top - ch), (half, bottom + ch), (half - ch, bottom)]


def pipe(points, radius, cell, sides=8, lens=None):
    """A round tube through the points, sections carried along without twisting; lens paints a chamfered end."""
    pts = [Vector(p) for p in points]
    tangents = [(pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(len(pts))]
    normal = tangents[0].cross(Vector((0, 0, 1)))
    normal = (normal if normal.length > 1e-6 else Vector((1, 0, 0))).normalized()
    loops = []
    for p, t in zip(pts, tangents):
        normal = (normal - t * normal.dot(t)).normalized()
        bi = t.cross(normal)
        loops.append([v(p + (normal * math.cos(a) + bi * math.sin(a)) * radius)
                      for a in (2 * math.pi * (k + 0.5) / sides for k in range(sides))])
        ROUND.update(loops[-1])
    if lens is None:
        closed(loops, cell)
        return
    p, t = pts[-1], tangents[-1]
    bi = t.cross(normal)
    rim = [v(p + t * 0.625 + (normal * math.cos(a) + bi * math.sin(a)) * (radius - 0.625))
           for a in (2 * math.pi * (k + 0.5) / sides for k in range(sides))]
    closed(loops, cell, cap_end=False)
    n = sides
    lo = loops[-1]
    faces = [bm.faces.new((lo[i], lo[(i + 1) % n], rim[(i + 1) % n], rim[i])) for i in range(n)]
    faces.append(bm.faces.new(rim))
    paint(faces, lens)


def spline(points, steps):
    """Centripetal Catmull-Rom through the points, steps samples per span."""
    pts = [Vector(p) for p in points]
    pts = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        t1 = (p1 - p0).length ** 0.5
        t2 = t1 + (p2 - p1).length ** 0.5
        t3 = t2 + (p3 - p2).length ** 0.5
        for k in range(steps if i < len(pts) - 3 else steps + 1):
            t = t1 + (t2 - t1) * k / steps
            a1 = (t1 - t) / t1 * p0 + t / t1 * p1
            a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
            a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
            b1 = (t2 - t) / t2 * a1 + t / t2 * a2
            b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3
            out.append((t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2)
    return out


def at_y(curve, y):
    return min(curve, key=lambda p: abs(p.y - y))


def wing(wy, x0, x1, a0, a1, cell, seg=6, th=3.75, ch=1.25):
    """A cycle wing arched over a wheel from a0 to a1 degrees (0 at the front, 90 on top)."""
    radius = WHEEL_R + 2.5
    shape = [(0.0, x0), (th - ch, x0), (th, x0 + ch), (th, x1 - ch), (th - ch, x1), (0.0, x1)]
    loops = []
    for i in range(seg + 1):
        a = math.radians(a0 + (a1 - a0) * i / seg)
        loops.append([v((x, wy + (radius + dr) * math.cos(a), WHEEL_R + (radius + dr) * math.sin(a)))
                      for dr, x in shape])
    closed(loops, cell)


# Proportions after the Nomad: a long wheelbase on big wheels, the front wheels well back from a long low nose,
# the rear wheels right at the tail.
WHEEL_R, WHEEL_W = 15.625, 15.625
WHEEL_X, FRONT_Y, REAR_Y = 22.5, 17.5, -28.75

# Chassis: a narrow dark spine high off the ground.
box(-8.75, 8.75, -41.25, 38.75, 7.5, 11.25, DARK)

# Nose: a long shell sloping up from the low face to the cowl in front of the cockpit; a dark face with the lamps.
loft([(42.5, section(10.0, 11.25, 25.0)),
      (32.5, section(12.5, 11.25, 28.75)),
      (12.5, section(13.75, 11.25, 32.5)),
      (8.75, section(13.75, 11.25, 32.5))], PAINT)
box(-8.75, 8.75, 42.5, 43.75, 12.5, 23.75, DARK, ch=0.625)
box(-10.0, 10.0, 40.0, 45.0, 7.5, 12.5, TRIM)                       # skid plate
for s in (-1, 1):
    pipe([(s * 5.0, 42.5, 18.125), (s * 5.0, 45.625, 18.125)], 3.125, TRIM, lens=METAL)

# Cockpit: a tub with painted sides and a dark floor, two dark bucket seats.
box(-13.75, 13.75, -23.75, 8.75, 11.25, 21.25, PAINT, ch=1.25, top_cell=DARK)
for s in (-1, 1):
    x0, x1 = sorted((s * 10.0, s * 13.75))
    box(x0, x1, -23.75, 8.75, 21.25, 25.0, PAINT)                    # sill
    x0, x1 = sorted((s * 1.25, s * 10.0))
    box(x0, x1, -10.0, -1.25, 21.25, 23.75, DARK)                    # seat cushion
    box(x0, x1, -13.75, -10.0, 21.25, 38.75, DARK)                   # seat back

# Rear: a dark deck between the rear wheels with the engine on it.
box(-12.5, 12.5, -41.25, -23.75, 11.25, 18.75, DARK)
box(-8.75, 8.75, -38.75, -25.0, 18.75, 27.5, TRIM, ch=2.5)

# Cycle wings over every wheel, in the paint.
for s in (-1, 1):
    x0, x1 = sorted((s * (WHEEL_X - WHEEL_W / 2 - 1.25), s * (WHEEL_X + WHEEL_W / 2 + 1.25)))
    wing(FRONT_Y, x0, x1, 30.0, 125.0, PAINT)
    wing(REAR_Y, x0, x1, 55.0, 150.0, PAINT)

# Roll cage, the Nomad's signature, in grey round tubes. Each side: one long line from the cowl up the windscreen
# pillar, along a long roof and steeply down to the tail; a sill rail along the bottom; a post behind the seats;
# a brace in the rear bay. Cross tubes at the windscreen top, over the seats and at the tail. A tube meeting another
# ends on its centre line, the cap hidden inside. The roof is the car's top, where a hat sits.
TUBE, CX, SILL = 1.875, 12.5, 26.25
side = {}
for s in (-1, 1):
    line = spline([(s * CX, 10.0, 30.0), (s * CX, -2.5, 45.0), (s * CX, -11.25, 50.0), (s * CX, -21.25, 50.625),
                   (s * CX, -31.25, 50.0), (s * CX, -38.75, 45.0), (s * CX, -41.25, 32.5), (s * CX, -41.25, 17.5)], 3)
    side[s] = line
    pipe(line, TUBE, TRIM)
    tail = min((p for p in line if p.y < -35.0), key=lambda p: abs(p.z - SILL))
    pipe([(s * CX, 8.75, SILL), tail], TUBE, TRIM)                                   # sill rail
    pipe([(s * CX, -15.0, SILL), at_y(line, -15.0)], TUBE, TRIM)                     # post behind the seats
    pipe([(s * CX, -15.0, SILL), at_y(line, -31.25)], TUBE, TRIM)                   # rear brace
for y in (-6.25, -15.0, -40.0):
    a, b = at_y(side[-1], y), at_y(side[1], y)
    pipe([a, (a + b) / 2, b], TUBE, TRIM)

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
sharp = [len(e.link_faces) == 2 and e.calc_face_angle() > math.radians(
    50.0 if e.verts[0] in ROUND and e.verts[1] in ROUND else 40.0) for e in bm.edges]
mesh = bpy.data.meshes.new('vehicle-buggy')
bm.to_mesh(mesh)
bm.free()
for p in mesh.polygons:
    p.use_smooth = True
mesh.attributes.new('sharp_edge', 'BOOLEAN', 'EDGE').data.foreach_set('value', sharp)

mat = bpy.data.materials.new('colormap')
mat.use_nodes = True
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(COLORMAP)
tex.interpolation = 'Closest'
mat.node_tree.links.new(tex.outputs['Color'], mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
mesh.materials.append(mat)
root = bpy.data.objects.new('vehicle-buggy', mesh)
bpy.context.scene.collection.objects.link(root)

for name, x, y in (('wheel-fl', -WHEEL_X, FRONT_Y), ('wheel-fr', WHEEL_X, FRONT_Y),
                   ('wheel-bl', -WHEEL_X, REAR_Y), ('wheel-br', WHEEL_X, REAR_Y)):
    points, faces, uvs = WHEEL
    if x > 0:
        # The kit's wheel has its hub on its -X side; the right-hand wheels are its mirror image, as on the racer.
        points = [Vector((-p.x, p.y, p.z)) for p in points]
        faces = [list(reversed(f)) for f in faces]
    wm = bpy.data.meshes.new(name)
    wm.from_pydata(points, [], faces)
    wuv = wm.uv_layers.new(name='UVMap')
    for p in wm.polygons:
        p.use_smooth = True
        for li in p.loop_indices:
            wuv.data[li].uv = uvs[(p.index, wm.loops[li].vertex_index)]
    wm.update()
    wm.set_sharp_from_angle(angle=math.radians(40.0))
    wm.materials.append(mat)
    w = bpy.data.objects.new(name, wm)
    bpy.context.scene.collection.objects.link(w)
    w.parent = root
    w.location = (x * CM, y * CM, WHEEL_R * CM)

print('COUNTS', len(mesh.vertices), len(mesh.polygons))
bpy.ops.export_scene.fbx(filepath=OUT, object_types={'MESH'}, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                         bake_anim=False, path_mode='STRIP')
print('DONE')
