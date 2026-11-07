"""Shared helpers for the headless Blender asset scripts.

Run any generator with:
    blender -b -P Tools/blender/gen_hut.py -- --out UnityProject/Assets/Art/Buildings/Hut.fbx

Conventions (see Tools/blender/README.md): 1 unit = 1 m, origin at the ground centre, +Y forward
in Blender becomes +Z forward in Unity, two materials named Walls and Roof so the Unity side can
map them to the palette materials.
"""
import sys
import bpy
import bmesh


def parse_out(default):
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
        if "--out" in argv:
            return argv[argv.index("--out") + 1]
    return default


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0


def material(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.95
    return mat


class MeshBuilder:
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


def export_fbx(obj, path):
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', object_types={'MESH'},
                             bake_anim=False, use_mesh_modifiers=True, path_mode='AUTO')
    print("exported", path)
