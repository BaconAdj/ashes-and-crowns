"""Shared helpers for the headless Blender asset scripts.

Run any generator with:
    blender -b -P Tools/blender/gen_hut.py -- --out UnityProject/Assets/Art/Buildings/Hut.fbx

Conventions (see Tools/blender/README.md): 1 unit = 1 m, origin at the ground centre, +Y forward
in Blender becomes +Z forward in Unity, two materials named Walls and Roof so the Unity side can
map them to the palette materials.
"""
import sys
try:
    import bpy
    import bmesh
    HAVE_BLENDER = True
except ImportError:          # plain Python: the same scripts write Wavefront OBJ instead (Unity imports both)
    bpy = bmesh = None
    HAVE_BLENDER = False


def parse_out(default):
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
        if "--out" in argv:
            return argv[argv.index("--out") + 1]
    return default


def _reset_scene_blender():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0


def _material_blender(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.95
    return mat


class _BlenderMeshBuilder:
    """Collect boxes, gable roofs and pyramids into one flat-shaded mesh with two material slots."""

    def __init__(self, name):
        self.bm = bmesh.new()
        self.name = name
        self.faces_roof = set()

    def box(self, cx, cy, z0, sx, sy, sz, roof=False):
        x0, x1 = cx - sx / 2, cx + sx / 2
        y0, y1 = cy - sy / 2, cy + sy / 2
        z1 = z0 + sz
        v = [self.bm.verts.new(p) for p in [
            (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
            (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]]
        quads = [(0, 1, 2, 3)[::-1], (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        for q in quads:
            f = self.bm.faces.new([v[i] for i in q])
            if roof:
                self.faces_roof.add(f)

    def gable(self, cx, cy, z0, sx, sy, height, overhang):
        """Ridge runs along +Y (Blender), i.e. along the building's length."""
        x0, x1 = cx - sx / 2 - overhang, cx + sx / 2 + overhang
        y0, y1 = cy - sy / 2 - overhang, cy + sy / 2 + overhang
        z1 = z0 + height
        r0 = self.bm.verts.new((cx, y0, z1)); r1 = self.bm.verts.new((cx, y1, z1))
        a = self.bm.verts.new((x0, y0, z0)); b = self.bm.verts.new((x0, y1, z0))
        c = self.bm.verts.new((x1, y1, z0)); d = self.bm.verts.new((x1, y0, z0))
        self.faces_roof.add(self.bm.faces.new([a, b, r1, r0]))
        self.faces_roof.add(self.bm.faces.new([c, d, r0, r1]))
        # gable end walls
        o = overhang
        self.bm.faces.new([self.bm.verts.new((x1 - o, y1 - o, z0)), self.bm.verts.new((x0 + o, y1 - o, z0)), self.bm.verts.new((cx, y1 - o, z1))])
        self.bm.faces.new([self.bm.verts.new((x0 + o, y0 + o, z0)), self.bm.verts.new((x1 - o, y0 + o, z0)), self.bm.verts.new((cx, y0 + o, z1))])

    def pyramid(self, cx, cy, z0, side, height):
        s = side / 2
        apex = self.bm.verts.new((cx, cy, z0 + height))
        corners = [self.bm.verts.new(p) for p in [(cx - s, cy - s, z0), (cx + s, cy - s, z0), (cx + s, cy + s, z0), (cx - s, cy + s, z0)]]
        for i in range(4):
            self.faces_roof.add(self.bm.faces.new([corners[i], corners[(i + 1) % 4], apex]))

    def finish(self, wall_mat, roof_mat):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        me.materials.append(wall_mat); me.materials.append(roof_mat)
        roof_idx = {f.index for f in self.faces_roof}
        self.bm.faces.ensure_lookup_table()
        for i, poly in enumerate(me.polygons):
            poly.material_index = 1 if i in roof_idx else 0
            poly.use_smooth = False
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(obj)
        self.bm.free()
        return obj


def _export_fbx_blender(obj, path):
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', object_types={'MESH'},
                             bake_anim=False, use_mesh_modifiers=True, path_mode='AUTO')
    print("exported", path)


# ---------------------------------------------------------------- pure-Python fallback (no Blender installed)
class _ObjMeshBuilder:
    """Same calls as the Blender builder, kept as plain lists; faces are oriented outward from each primitive's centre."""

    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []        # (indices, is_roof)

    def _add(self, points, quads, roof, centre):
        base = len(self.verts)
        self.verts.extend(points)
        for q in quads:
            idx = [base + i for i in q]
            a, b, c = (points[q[0]], points[q[1]], points[q[2]])
            ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
            vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
            nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
            fc = [sum(points[i][k] for i in q) / len(q) for k in range(3)]
            if nx * (fc[0] - centre[0]) + ny * (fc[1] - centre[1]) + nz * (fc[2] - centre[2]) < 0:
                idx.reverse()
            self.faces.append((idx, roof))

    def box(self, cx, cy, z0, sx, sy, sz, roof=False):
        x0, x1 = cx - sx / 2, cx + sx / 2
        y0, y1 = cy - sy / 2, cy + sy / 2
        z1 = z0 + sz
        pts = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        quads = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        self._add(pts, quads, roof, (cx, cy, z0 + sz / 2))

    def gable(self, cx, cy, z0, sx, sy, height, overhang):
        x0, x1 = cx - sx / 2 - overhang, cx + sx / 2 + overhang
        y0, y1 = cy - sy / 2 - overhang, cy + sy / 2 + overhang
        z1 = z0 + height
        pts = [(cx, y0, z1), (cx, y1, z1), (x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)]
        self._add(pts, [(2, 3, 1, 0), (4, 5, 0, 1)], True, (cx, cy, z0 + height / 2))
        o = overhang
        self._add([(x1 - o, y1 - o, z0), (x0 + o, y1 - o, z0), (cx, y1 - o, z1)], [(0, 1, 2)], False, (cx, cy, z0 + height / 2))
        self._add([(x0 + o, y0 + o, z0), (x1 - o, y0 + o, z0), (cx, y0 + o, z1)], [(0, 1, 2)], False, (cx, cy, z0 + height / 2))

    def pyramid(self, cx, cy, z0, side, height):
        s = side / 2
        pts = [(cx, cy, z0 + height), (cx - s, cy - s, z0), (cx + s, cy - s, z0), (cx + s, cy + s, z0), (cx - s, cy + s, z0)]
        self._add(pts, [(1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 1, 0)], True, (cx, cy, z0 + height / 3))

    def finish(self, wall_mat, roof_mat):
        self.materials = (wall_mat, roof_mat)
        return self


def _export_obj(builder, path):
    """Wavefront OBJ in Unity's frame: Blender (x, y forward, z up) -> (x, z, y); Unity mirrors X on import, which our models tolerate."""
    import os
    if path.lower().endswith(".fbx"):
        path = path[:-4] + ".obj"
        print("Blender not available: writing OBJ instead ->", path)
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    with open(path, "w") as f:
        f.write("# Ashes and Crowns, generated by Tools/blender without Blender. 1 unit = 1 m.\n")
        f.write("o %s\n" % builder.name)
        for x, y, z in builder.verts:
            f.write("v %.4f %.4f %.4f\n" % (x, z, y))
        for roof in (False, True):
            group = [fc for fc, r in builder.faces if r == roof]
            if not group:
                continue
            f.write("usemtl %s\n" % builder.materials[1 if roof else 0])
            for idx in group:
                f.write("f " + " ".join(str(i + 1) for i in idx) + "\n")
    print("exported", path)


def reset_scene():
    if HAVE_BLENDER:
        _reset_scene_blender()


def material(name, rgb):
    return _material_blender(name, rgb) if HAVE_BLENDER else name


MeshBuilder = _BlenderMeshBuilder if HAVE_BLENDER else _ObjMeshBuilder


def export_fbx(obj, path):
    if HAVE_BLENDER:
        _export_fbx_blender(obj, path)
    else:
        _export_obj(obj, path)
