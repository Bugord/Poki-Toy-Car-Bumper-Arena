"""Shared parts for the toppers built in the Kenney Toy Car Kit style on the kit's colormap atlas.

Metres, front +Y, up +Z, the hat standing on z = 0. Every face's UV sits in the right (gradient) half of its atlas
cell and its height picks the cell's vertical gradient, like the kit's cars.
"""
import bpy, bmesh, math
from math import sin, cos, pi, radians
from mathutils import Vector, Matrix

I4 = Matrix.Identity(4)

DARK = (0, 0)
TRIM = (1, 0)
SLATE = (2, 0)
CHROME = (3, 0)
WHITE = (4, 0)
COPPER = (5, 0)
BROWN = (6, 0)
TAN = (7, 0)
CREAM = (0, 1)
GREEN = (1, 1)
GOLD = (2, 1)
ORANGE = (3, 1)
RED = (4, 1)
BLUE = (5, 1)
SKY = (6, 1)
PURPLE = (7, 1)
PINK = (0, 2)


def clamp01(x):
    return max(0.0, min(1.0, x))


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def R(deg, axis):
    return Matrix.Rotation(radians(deg), 4, axis)


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


# A teardrop along its height: (share of the height, share of the radius).
DROP = [(0.0, 0.55), (0.07, 0.86), (0.16, 1.0), (0.27, 0.95), (0.4, 0.78), (0.53, 0.58), (0.66, 0.4), (0.78, 0.25),
        (0.9, 0.11), (1.0, 0.0)]
# A feather along its length.
LEAF = [(0.0, 0.45), (0.14, 0.84), (0.38, 1.0), (0.66, 0.94), (0.85, 0.7), (0.96, 0.36), (1.0, 0.0)]


class Part:
    """One mesh of a topper: the body, or a piece that moves on its own (a flame, a wing)."""

    def __init__(self, name, top, t0=0.0):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.top = top
        self.t0 = t0
        self.vt = {}

    def paint(self, faces, cell):
        for f in faces:
            for loop in f.loops:
                t = self.vt.get(loop.vert)
                if t is None:
                    t = self.t0 + (1.0 - self.t0) * clamp01(loop.vert.co.z / self.top)
                loop[self.uv].uv = ((cell[0] + 0.75) / 8.0, (cell[1] + 0.1 + 0.8 * clamp01(t)) / 4.0)

    def grid(self, rows, cell, close_u=True, close_v=False, cap_start=False, cap_end=False, cellfn=None, ts=None):
        """Quads between consecutive rows of equal length; cellfn(band, column) picks a face's cell."""
        vs = [[self.bm.verts.new(p) for p in row] for row in rows]
        if ts is not None:
            for row, t in zip(vs, ts):
                for vert in row:
                    self.vt[vert] = t
        n, m = len(vs[0]), len(vs)
        for k in range(m if close_v else m - 1):
            for i in range(n if close_u else n - 1):
                quad = (vs[k][i], vs[k][(i + 1) % n], vs[(k + 1) % m][(i + 1) % n], vs[(k + 1) % m][i])
                self.paint([self.bm.faces.new(quad)], cellfn(k, i) if cellfn else cell)
        if cap_start:
            self.paint([self.bm.faces.new(list(reversed(vs[0])))], cellfn(0, 0) if cellfn else cell)
        if cap_end:
            self.paint([self.bm.faces.new(vs[-1])], cellfn(m - 2, 0) if cellfn else cell)
        return vs

    def revolve(self, profile, sides, cell, m=I4, sx=1.0, sy=1.0, lobes=0, amps=None, phase=0.0, twist=0.0,
                offs=None, cellfn=None, a0=0.5, ts=None):
        """A solid of revolution around Z through (radius, z) points; lobes and amps pleat it around."""
        rows = []
        for k, (r, z) in enumerate(profile):
            amp = amps[k] if amps else 0.0
            off = Vector(offs[k]) if offs else Vector((0, 0, 0))
            row = []
            for i in range(sides):
                a = 2 * pi * (i + a0) / sides + twist * k
                rr = r * (1.0 + amp * cos(lobes * (a - twist * k) + phase))
                row.append(m @ (Vector((rr * cos(a) * sx, rr * sin(a) * sy, z)) + off))
            rows.append(row)
        return self.grid(rows, cell, cap_start=profile[0][0] > 1e-6, cap_end=profile[-1][0] > 1e-6, cellfn=cellfn,
                         ts=ts)

    def tube(self, pts, radii, sides, cell, m=I4, flat=1.0, normal=None, a0=0.5, ts=None, cellfn=None):
        """A tube through the points with a radius at each; flat squeezes it along normal (a blade)."""
        pts = [Vector(p) for p in pts]
        tang = [(pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(len(pts))]
        nrm = Vector(normal) if normal is not None else tang[0].cross(Vector((0, 0, 1)))
        if nrm.length < 1e-6:
            nrm = Vector((1, 0, 0))
        rows = []
        for p, t, r in zip(pts, tang, radii):
            nrm = (nrm - t * nrm.dot(t)).normalized()
            bi = t.cross(nrm)
            rows.append([m @ (p + (nrm * cos(a) * flat + bi * sin(a)) * r)
                         for a in (2 * pi * (k + a0) / sides for k in range(sides))])
        return self.grid(rows, cell, cap_start=radii[0] > 1e-6, cap_end=radii[-1] > 1e-6, ts=ts, cellfn=cellfn)

    def teardrop(self, base, height, radius, cell, lean=(0, 0, 0), sides=10, m=I4, shade=(0.1, 1.0),
                 sway=(0, 0, 0)):
        """A tongue of flame standing on base: its tip bent by lean, its belly pushed out by sway."""
        base, lean, sway = Vector(base), Vector(lean), Vector(sway)
        pts = [base + Vector((0, 0, height * s)) + lean * (s ** 2.4) + sway * sin(pi * s) for s, _ in DROP]
        self.tube(pts, [radius * w for _, w in DROP], sides, cell, m=m,
                  ts=[shade[0] + (shade[1] - shade[0]) * s for s, _ in DROP])

    def blade(self, root, direction, length, width, thick, cell, normal, bend=(0, 0, 0), sides=6, m=I4,
              shade=(0.25, 1.0), profile=None):
        """A feather: a flat leaf from root along direction, bent by bend towards its tip."""
        root, direction, bend = Vector(root), Vector(direction).normalized(), Vector(bend)
        profile = profile or LEAF
        pts = [root + direction * (length * s) + bend * (s * s) for s, _ in profile]
        self.tube(pts, [width * w for _, w in profile], sides, cell, m=m, flat=thick / width, normal=normal,
                  ts=[shade[0] + (shade[1] - shade[0]) * s for s, _ in profile])

    def torus(self, radius, tube, sides, tsides, cell, m=I4, sx=1.0, sy=1.0, lobes=0, amp=0.0):
        """A ring; lobes and amp make its tube lumpy around (fur)."""
        def at(a, b):
            t = tube * (1.0 + amp * cos(lobes * a))
            return m @ Vector(((radius + t * cos(b)) * cos(a) * sx, (radius + t * cos(b)) * sin(a) * sy, t * sin(b)))
        rows = [[at(a, b) for a in (2 * pi * (i + 0.5) / sides for i in range(sides))]
                for b in (2 * pi * (j + 0.5) / tsides for j in range(tsides))]
        self.grid(rows, cell, close_v=True)

    def plate(self, outline, thick, cell, m=I4, t=None):
        """A flat piece from an outline of (x, z) points in its plane, thick along Y."""
        front = [self.bm.verts.new(m @ Vector((x, thick / 2, z))) for x, z in outline]
        back = [self.bm.verts.new(m @ Vector((x, -thick / 2, z))) for x, z in outline]
        n = len(outline)
        faces = [self.bm.faces.new(front), self.bm.faces.new(list(reversed(back)))]
        faces += [self.bm.faces.new((front[k], back[k], back[(k + 1) % n], front[(k + 1) % n])) for k in range(n)]
        if t is not None:
            for vert in front + back:
                self.vt[vert] = t
        self.paint(faces, cell)

    def star(self, centre, radius, cell, inner=0.45, depth=0.05, points=5, m=I4, puff=0.7, t=None):
        """A chunky star facing +Y: its points on a ring, its middle puffed out front and back."""
        mat = m @ Matrix.Translation(centre)
        ring_f, ring_b = [], []
        for k in range(points * 2):
            a = pi / 2 + pi * k / points
            r = radius if k % 2 == 0 else radius * inner
            ring_f.append(self.bm.verts.new(mat @ Vector((r * cos(a), depth / 2, r * sin(a)))))
            ring_b.append(self.bm.verts.new(mat @ Vector((r * cos(a), -depth / 2, r * sin(a)))))
        cf = self.bm.verts.new(mat @ Vector((0, depth / 2 + depth * puff, 0)))
        cb = self.bm.verts.new(mat @ Vector((0, -depth / 2 - depth * puff, 0)))
        n = len(ring_f)
        faces = []
        for k in range(n):
            j = (k + 1) % n
            faces.append(self.bm.faces.new((cf, ring_f[k], ring_f[j])))
            faces.append(self.bm.faces.new((cb, ring_b[j], ring_b[k])))
            faces.append(self.bm.faces.new((ring_f[k], ring_b[k], ring_b[j], ring_f[j])))
        if t is not None:
            for vert in ring_f + ring_b + [cf, cb]:
                self.vt[vert] = t
        self.paint(faces, cell)

    def _new_faces(self, verts):
        return list({f for vert in verts for f in vert.link_faces})

    def ball(self, centre, radius, cell, sub=2, scale=(1, 1, 1), m=I4, poke=0.0, t=None, cellfn=None):
        mat = m @ Matrix.Translation(centre) @ Matrix.Diagonal((radius * scale[0], radius * scale[1],
                                                                 radius * scale[2], 1.0))
        geo = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=1.0, matrix=mat)
        faces = self._new_faces(geo['verts'])
        if poke:
            self.bm.normal_update()
            faces = bmesh.ops.poke(self.bm, faces=faces, offset=poke)['faces']
        if t is not None:
            for vert in {vert for f in faces for vert in f.verts}:
                self.vt[vert] = t
        if cellfn is not None:
            for f in faces:
                self.paint([f], cellfn(f.calc_center_median()))
            return
        self.paint(faces, cell)

    def box(self, centre, size, cell, m=I4, t=None):
        mat = m @ Matrix.Translation(centre) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        geo = bmesh.ops.create_cube(self.bm, size=1.0, matrix=mat)
        faces = self._new_faces(geo['verts'])
        if t is not None:
            for vert in geo['verts']:
                self.vt[vert] = t
        self.paint(faces, cell)


class Hat:
    def __init__(self, name, top, t0=0.0):
        self.name = name
        self.top = top
        self.t0 = t0
        self.parts = []

    def part(self, name='body'):
        p = Part(name, self.top, self.t0)
        self.parts.append(p)
        return p


def colormap_material(path):
    mat = bpy.data.materials.new('colormap')
    mat.use_nodes = True
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(path)
    tex.interpolation = 'Closest'
    bsdf = mat.node_tree.nodes['Principled BSDF']
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.65
    return mat


def outline_material():
    mat = bpy.data.materials.new('outline')
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    em = nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = (0.0123, 0.0242, 0.0782, 1.0)  # #1d2b4f, linear
    em.inputs['Strength'].default_value = 1.0
    mat.node_tree.links.new(em.outputs[0], out.inputs['Surface'])
    mat.use_backface_culling = True
    return mat


def hull_of(bm, thickness):
    """The game's outline in the preview: the mesh pushed out along its normals and turned inside out."""
    hb = bm.copy()
    hb.normal_update()
    for vert in hb.verts:
        vert.co += vert.normal * thickness
    bmesh.ops.reverse_faces(hb, faces=hb.faces)
    return hb


def make_objects(hat, material, collection, outline=None, thickness=0.016):
    """Turns the hat's parts into objects under one root; outline adds the preview's inverted hull to each."""
    root = bpy.data.objects.new(hat.name, None)
    collection.objects.link(root)
    counts = [0, 0]
    for p in hat.parts:
        bm = p.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(f'{hat.name}.{p.name}')
        bm.to_mesh(mesh)
        for poly in mesh.polygons:
            poly.use_smooth = True
        mesh.set_sharp_from_angle(angle=radians(40.0))
        mesh.materials.append(material)
        obj = bpy.data.objects.new(mesh.name, mesh)
        obj.parent = root
        collection.objects.link(obj)
        counts[0] += len(mesh.vertices)
        counts[1] += sum(len(poly.vertices) - 2 for poly in mesh.polygons)
        if outline is not None:
            hb = hull_of(bm, thickness)
            hm = bpy.data.meshes.new(mesh.name + '.hull')
            hb.to_mesh(hm)
            hb.free()
            hm.materials.append(outline)
            ho = bpy.data.objects.new(hm.name, hm)
            ho.parent = root
            ho.visible_shadow = False
            collection.objects.link(ho)
    return root, counts
