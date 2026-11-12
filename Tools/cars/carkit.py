"""Shared parts for the cars built in the Kenney Toy Car Kit style on the kit's colormap atlas (the league cars).

The same conventions as build_buggy.py: sizes in cm on a 1.25 cm grid, chunky parts with one 45-degree chamfer,
every face's UV in the right half of its atlas cell with its height picking the cell's vertical gradient, front +Y,
up +Z, ground z = 0, exported in metres like the imported Kenney racer. The body is one paint cell (the one the game
repaints), so a car keeps its paint dominant over every other colour cell.

Like the kit, windows are cut into the cab's own faces (shell with panels), never glued on, and every part that sits
on another sinks into it by at least 0.625 cm: no two faces share a plane, so nothing z-fights.

A car script does:
    import carkit as k
    k.begin(wheel_folder, colormap, top_cm)
    k.box(...); k.loft(...); ...
    k.finish(name, out_fbx, [('wheel-fl', 'wheel-small', x, y), ...])
"""
import bpy, bmesh, math
from mathutils import Vector

# Atlas cells (x, y from the bottom-left), as the kit paints its cars.
PAINT = (1, 1)      # the body, repainted by the game
TRIM = (1, 0)       # grey-blue: windows, trim
DARK = (2, 0)       # dark slate: chassis, floor, rubber
METAL = (4, 0)      # white: lamp lenses, chrome highlights
CHROME = (3, 0)     # pale steel
COPPER = (5, 0)
BROWN = (6, 0)
TAN = (7, 0)
CREAM = (0, 1)
GOLD = (2, 1)
ORANGE = (3, 1)
RED = (4, 1)
BLUE = (5, 1)
SKY = (6, 1)        # lamp glass
PURPLE = (7, 1)
PINK = (0, 2)

CM = 0.01
bm = None
uv_layer = None
ROUND = set()
TOP = 50.0
WHEELS = {}
WHEEL_FOLDER = ''
COLORMAP = ''


def begin(wheel_folder, colormap, top):
    """Starts a new car: reads the kit's wheels and clears the scene."""
    global bm, uv_layer, TOP, WHEEL_FOLDER, COLORMAP
    WHEEL_FOLDER, COLORMAP, TOP = wheel_folder, colormap, top
    for name in ('wheel-small', 'wheel-medium', 'wheel-large'):
        WHEELS[name] = kit_wheel(name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ROUND.clear()
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new('UVMap')


def kit_wheel(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f'{WHEEL_FOLDER}/{name}.fbx')
    o = bpy.data.objects[name]
    centre = o.matrix_world.translation.copy()
    points = [o.matrix_world @ p.co - centre for p in o.data.vertices]
    faces = [list(p.vertices) for p in o.data.polygons]
    uvl = o.data.uv_layers.active.data
    uvs = {(p.index, o.data.loops[li].vertex_index): uvl[li].uv.copy() for p in o.data.polygons for li in p.loop_indices}
    radius = max(abs(p.z) for p in points)
    return points, faces, uvs, radius


def wheel_radius(name):
    return WHEELS[name][3] / CM


def paint(faces, cell):
    for f in faces:
        for loop in f.loops:
            t = min(1.0, max(0.0, loop.vert.co.z / (TOP * CM)))
            loop[uv_layer].uv = ((cell[0] + 0.75) / 8.0, (cell[1] + 0.1 + 0.8 * t) / 4.0)


def v(p):
    return bm.verts.new((p[0] * CM, p[1] * CM, p[2] * CM))


def closed(loops, cell, end_cell=None, cap_end=True, cap_start=True):
    """Quads between equal loops, the ends capped."""
    n = len(loops[0])
    faces = [bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])) for lo, hi in zip(loops, loops[1:])
             for i in range(n)]
    paint(faces, cell)
    if cap_start:
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


def ring(x0, x1, y0, y1, z, ch=1.25):
    """A horizontal rectangle with chamfered corners: rear, rear-right corner, right, front-right corner, front, ...

    The faces between two rings run in this order: 0 rear, 2 right, 4 front, 6 left, the odd ones the corners."""
    return [v(p + (z,)) for p in ((x0 + ch, y0), (x1 - ch, y0), (x1, y0 + ch), (x1, y1 - ch),
                                  (x1 - ch, y1), (x0 + ch, y1), (x0, y1 - ch), (x0, y0 + ch))]


def panel(lo0, lo1, hi1, hi0, frame, glass, side, bottom, top, parts=1, inner=None):
    """Cuts a window into the flat quad lo0-lo1-hi1-hi0: a frame of the given widths around the glass, in one plane.

    Like the kit's windows the glass is part of the surface, not stuck on it, so nothing overlaps and nothing fights.
    side insets along the lo edge, bottom and top along lo0 to hi0. parts splits the glass into that many panes along
    the lo edge with frame pillars of the side width between them; inner, a tuple like this one's arguments from frame
    on, cuts a second panel into the glass instead (a badge on a door).
    """
    def along(a, b, d):
        e = (b.co - a.co)
        t = min(0.45, d * CM / e.length)
        return a.co + e * t, b.co - e * t
    l0, l1 = along(lo0, lo1, side)
    h0, h1 = along(hi0, hi1, side)
    def up(a, b):
        e = b - a
        h = e.length
        return a + e * min(0.45, bottom * CM / h), b - e * min(0.45, top * CM / h)
    i_l0, i_h0 = (bm.verts.new(p) for p in up(l0, h0))
    i_l1, i_h1 = (bm.verts.new(p) for p in up(l1, h1))
    paint([bm.faces.new(f) for f in ((lo0, lo1, i_l1, i_l0), (lo1, hi1, i_h1, i_l1), (hi1, hi0, i_h0, i_h1),
                                    (hi0, lo0, i_l0, i_h0))], frame)
    if inner is not None:
        panel(i_l0, i_l1, i_h1, i_h0, *inner)
        return
    if parts == 1:
        paint([bm.faces.new((i_l0, i_l1, i_h1, i_h0))], glass)
        return
    # Panes and pillars across the glass: cut points along its bottom and top edges.
    eb, et = i_l1.co - i_l0.co, i_h1.co - i_h0.co
    pillar = side * CM / eb.length
    pane = (1.0 - pillar * (parts - 1)) / parts
    cuts = [0.0]
    for k in range(parts - 1):
        cuts += [cuts[-1] + pane, cuts[-1] + pane + pillar]
    bottoms = [i_l0] + [bm.verts.new(i_l0.co + eb * t) for t in cuts[1:]] + [i_l1]
    tops = [i_h0] + [bm.verts.new(i_h0.co + et * t) for t in cuts[1:]] + [i_h1]
    for k in range(len(bottoms) - 1):
        paint([bm.faces.new((bottoms[k], bottoms[k + 1], tops[k + 1], tops[k]))], glass if k % 2 == 0 else frame)


def shell(rings, cells, cap_cell=None, panels=None, cap_bottom=True):
    """A solid through stacked rings of equal size, bottom first: band k between rings k and k+1 is painted cells[k].

    panels maps (band, face) to (frame, glass, side, bottom, top) and cuts a window into that face of that band."""
    panels = panels or {}
    n = len(rings[0])
    for b, (lo, hi) in enumerate(zip(rings, rings[1:])):
        for i in range(n):
            quad = (lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])
            if (b, i) in panels:
                panel(*quad, *panels[(b, i)])
            else:
                paint([bm.faces.new(quad)], cells[b])
    if cap_bottom:
        paint([bm.faces.new(list(reversed(rings[0])))], cells[0])
    paint([bm.faces.new(rings[-1])], cap_cell or cells[-1])


def loft(stations, cell, end_cell=None, faces=None, panels=None):
    """A solid lofted along Y through sections: (y, [(x, z), ...]), each section clockwise seen from +Y.

    faces maps (band, face) to a cell for that face of the band between stations band and band + 1 (a stripe),
    panels maps it to a panel cut into that face (see panel; its lo edge is the section's edge)."""
    loops = [[v((x, y, z)) for x, z in sec] for y, sec in stations]
    faces, panels = faces or {}, panels or {}
    n = len(loops[0])
    for b, (lo, hi) in enumerate(zip(loops, loops[1:])):
        for i in range(n):
            quad = (lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])
            if (b, i) in panels:
                panel(*quad, *panels[(b, i)])
            else:
                paint([bm.faces.new(quad)], faces.get((b, i), cell))
    paint([bm.faces.new(list(reversed(loops[0])))], cell)
    paint([bm.faces.new(loops[-1])], end_cell or cell)


def striped(bottom_half, top_half, bottom, top, s0, s1, ch=2.5):
    """A tapered section whose sides are split at heights s0 and s1, so a stripe can run along the body.

    Faces: 0 lower-left chamfer, 1 left below, 2 left stripe, 3 left above, 4 top-left chamfer, 5 top,
    6 top-right chamfer, 7 right above, 8 right stripe, 9 right below, 10 lower-right chamfer, 11 bottom."""
    def x_at(z):
        t = (z - bottom - ch) / (top - bottom - 2 * ch)
        return bottom_half + (top_half - bottom_half) * t
    return [(-bottom_half + ch, bottom), (-bottom_half, bottom + ch), (-x_at(s0), s0), (-x_at(s1), s1),
            (-top_half, top - ch), (-top_half + ch, top), (top_half - ch, top), (top_half, top - ch),
            (x_at(s1), s1), (x_at(s0), s0), (bottom_half, bottom + ch), (bottom_half - ch, bottom)]


def racing(bottom_half, top_half, bottom, top, stripe, ch=2.5):
    """A tapered section whose top is split around a racing stripe of half width stripe down the middle.

    Faces: 0 lower-left chamfer, 1 left, 2 top-left chamfer, 3 top left, 4 stripe, 5 top right, 6 top-right chamfer,
    7 right, 8 lower-right chamfer, 9 bottom."""
    return [(-bottom_half + ch, bottom), (-bottom_half, bottom + ch), (-top_half, top - ch), (-top_half + ch, top),
            (-stripe, top), (stripe, top), (top_half - ch, top), (top_half, top - ch), (bottom_half, bottom + ch),
            (bottom_half - ch, bottom)]


def cone(centre, axis, r0, r1, length, cell, sides=8, tip_cell=None):
    """A truncated cone from radius r0 at centre to r1 at length along axis 'x', 'y' (or '-y') or 'z' (a flame)."""
    c = Vector(centre)
    d = {'x': Vector((1, 0, 0)), 'y': Vector((0, 1, 0)), '-y': Vector((0, -1, 0)), 'z': Vector((0, 0, 1))}[axis]
    a = Vector((0, 0, 1)) if axis != 'z' else Vector((1, 0, 0))
    b = d.cross(a)
    loops = [[v(c + d * s + (a * math.cos(t) + b * math.sin(t)) * r)
              for t in (2 * math.pi * (k + 0.5) / sides for k in range(sides))] for s, r in ((0.0, r0), (length, r1))]
    for lo in loops:
        ROUND.update(lo)
    closed(loops, cell, end_cell=tip_cell)


def band(path, x0, x1, thickness, cell):
    """A closed band around a loop of (y, z) points, inward normal by thickness, from x0 to x1: a track."""
    n = len(path)
    pts = [Vector((0.0, y, z)) for y, z in path]
    rings = []
    for i, p in enumerate(pts):
        t = (pts[(i + 1) % n] - pts[i - 1]).normalized()
        inward = Vector((0.0, -t.z, t.y))
        if inward.dot(sum(pts, Vector()) / n - p) < 0:
            inward = -inward
        q = p + inward * thickness
        rings.append([v((x0, p.y, p.z)), v((x1, p.y, p.z)), v((x1, q.y, q.z)), v((x0, q.y, q.z))])
    faces = [bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k])) for a, b in zip(rings, rings[1:] + rings[:1])
             for k in range(4)]
    paint(faces, cell)


def stadium(y0, y1, z, radius, steps=6):
    """The loop of a track around two wheels at y0 and y1 (centre height z): rear arc, bottom, front arc, top."""
    out = []
    for k in range(steps + 1):
        a = math.pi / 2 + math.pi * k / steps
        out.append((y0 + radius * math.cos(a), z + radius * math.sin(a)))
    for k in range(steps + 1):
        a = -math.pi / 2 + math.pi * k / steps
        out.append((y1 + radius * math.cos(a), z + radius * math.sin(a)))
    return out


def loft_x(stations, cell):
    """A solid lofted along X through sections: (x, [(y, z), ...]), each section clockwise seen from +X."""
    closed([[v((x, y, z)) for y, z in sec] for x, sec in stations], cell)


def section(half, bottom, top, ch=2.5):
    return [(-half + ch, bottom), (-half, bottom + ch), (-half, top - ch), (-half + ch, top),
            (half - ch, top), (half, top - ch), (half, bottom + ch), (half - ch, bottom)]


def tapered(bottom_half, top_half, bottom, top, ch=2.5):
    """A section narrower at the top, like a cab with sloping windows."""
    return [(-bottom_half + ch, bottom), (-bottom_half, bottom + ch), (-top_half, top - ch), (-top_half + ch, top),
            (top_half - ch, top), (top_half, top - ch), (bottom_half, bottom + ch), (bottom_half - ch, bottom)]


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


def disc(centre, axis, radius, depth, cell, sides=12, face_cell=None):
    """A short cylinder (a lamp, a hub cap, a beacon) along axis 'x', 'y' or 'z'."""
    c = Vector(centre)
    d = {'x': Vector((1, 0, 0)), 'y': Vector((0, 1, 0)), 'z': Vector((0, 0, 1))}[axis]
    a = Vector((0, 0, 1)) if axis != 'z' else Vector((1, 0, 0))
    b = d.cross(a)
    loops = [[v(c + d * s + (a * math.cos(t) + b * math.sin(t)) * radius)
              for t in (2 * math.pi * (k + 0.5) / sides for k in range(sides))] for s in (0.0, depth)]
    for lo in loops:
        ROUND.update(lo)
    closed(loops, cell, end_cell=face_cell)


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


def wing(wy, wz, wheel_r, x0, x1, a0, a1, cell, seg=6, th=3.75, ch=1.25, gap=2.5):
    """A fender arched over a wheel centred at (wy, wz) from a0 to a1 degrees (0 at the front, 90 on top)."""
    radius = wheel_r + gap
    shape = [(0.0, x0), (th - ch, x0), (th, x0 + ch), (th, x1 - ch), (th - ch, x1), (0.0, x1)]
    loops = []
    for i in range(seg + 1):
        a = math.radians(a0 + (a1 - a0) * i / seg)
        loops.append([v((x, wy + (radius + dr) * math.cos(a), wz + (radius + dr) * math.sin(a)))
                      for dr, x in shape])
    closed(loops, cell)


def check_coplanar():
    """Prints a COPLANAR line for faces of different parts lying in one plane and overlapping there: they z-fight."""
    bm.normal_update()
    part = {}
    for f in bm.faces:
        if f in part:
            continue
        stack, part[f] = [f], f.index
        while stack:
            g = stack.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h not in part:
                        part[h] = f.index
                        stack.append(h)
    planes = {}
    for f in bm.faces:
        n = f.normal.normalized()
        if n.length < 0.5:
            continue
        # The same plane whichever way the face looks: flip the normal so its first clear component is positive.
        lead = next(c for c in n if abs(c) > 1e-3)
        n = n if lead > 0 else -n
        key = (round(n.x, 3), round(n.y, 3), round(n.z, 3), round(n.dot(f.verts[0].co) * 1000))
        planes.setdefault(key, []).append(f)
    found = 0
    for faces in planes.values():
        for i, a in enumerate(faces):
            for b in faces[i + 1:]:
                if part[a] == part[b]:
                    continue
                # Convex faces overlap unless an edge normal of one of them separates them (in the plane).
                n = a.normal.normalized()
                def span(f, axis):
                    d = [p.co.dot(axis) for p in f.verts]
                    return min(d), max(d)
                axes = [n.cross(e.verts[1].co - e.verts[0].co).normalized() for f in (a, b) for e in f.edges]
                if all(min(span(a, ax)[1], span(b, ax)[1]) - max(span(a, ax)[0], span(b, ax)[0]) > 0.001
                       for ax in axes if ax.length > 0.5):
                    found += 1
                    if found <= 10:
                        print('COPLANAR', [tuple(round(c / CM, 2) for c in p.co) for p in a.verts][:3])
    print('COPLANAR TOTAL', found)


def finish(name, out, wheels):
    """Writes the body mesh and the kit's wheels (name, kit wheel, x, y[, centre height]) to the FBX at out."""
    global bm
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # The game takes the body's paint cell as the colour cell with the most UVs, so the paint must win by a margin.
    cells = {}
    for f in bm.faces:
        for loop in f.loops:
            uv = loop[uv_layer].uv
            cell = (int(uv.x * 8), int(uv.y * 4))
            cells[cell] = cells.get(cell, 0) + 1
    print('CELLS', sorted(cells.items(), key=lambda c: -c[1]))
    check_coplanar()
    sharp = [len(e.link_faces) == 2 and e.calc_face_angle() > math.radians(
        50.0 if e.verts[0] in ROUND and e.verts[1] in ROUND else 40.0) for e in bm.edges]
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    bm = None
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
    root = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(root)

    for wheel in wheels:
        wname, kit, x, y = wheel[:4]
        points, faces, uvs, radius = WHEELS[kit]
        height = wheel[4] * CM if len(wheel) > 4 else radius
        if x > 0:
            # The kit's wheel has its hub on its -X side; the right-hand wheels are its mirror image, as on the racer.
            points = [Vector((-p.x, p.y, p.z)) for p in points]
            faces = [list(reversed(f)) for f in faces]
        wm = bpy.data.meshes.new(wname)
        wm.from_pydata(points, [], faces)
        wuv = wm.uv_layers.new(name='UVMap')
        for p in wm.polygons:
            p.use_smooth = True
            for li in p.loop_indices:
                wuv.data[li].uv = uvs[(p.index, wm.loops[li].vertex_index)]
        wm.update()
        wm.set_sharp_from_angle(angle=math.radians(40.0))
        wm.materials.append(mat)
        w = bpy.data.objects.new(wname, wm)
        bpy.context.scene.collection.objects.link(w)
        w.parent = root
        w.location = (x * CM, y * CM, height)

    print('COUNTS', name, len(mesh.vertices), len(mesh.polygons))
    bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP')
    print('DONE', out)
