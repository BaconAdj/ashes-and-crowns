"""Shared helpers for the asset generator scripts.

Run any generator with plain Python (no Blender needed):
    python3 Tools/blender/gen_hut.py -- --out UnityProject/Assets/Art/Buildings/Hut.obj
or with Blender, which additionally writes an FBX next to it:
    blender -b -P Tools/blender/gen_hut.py -- --out UnityProject/Assets/Art/Buildings/Hut.fbx

Geometry is always built by the pure-Python MeshBuilder below (boxes, tapered boxes, tubes, ellipsoids,
gable roofs, pyramids, cones, noise displacement) and written as Wavefront OBJ with normals. Conventions:
1 unit = 1 m, origin at the ground centre, modelling space is Blender's (x right, +y forward, z up); the
writer swaps to Unity's y-up. Two material slots: "Walls" (slot 1) and "Roof" (slot 2); the Unity scene
builders map them onto the palette materials.
"""
import math
import os
import sys

try:
    import bpy
    HAVE_BLENDER = True
except ImportError:
    bpy = None
    HAVE_BLENDER = False


def parse_out(default):
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
        if "--out" in argv:
            return argv[argv.index("--out") + 1]
    return default


def reset_scene():
    if HAVE_BLENDER:
        bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, rgb):
    """Materials are only names here; colours live in the Unity palette."""
    return name


# ---------------------------------------------------------------- tiny vector helpers
def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _add3(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def _mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def _cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _len(a): return math.sqrt(_dot(a, a))
def _norm(a):
    l = _len(a)
    return (a[0] / l, a[1] / l, a[2] / l) if l > 1e-9 else (0.0, 0.0, 1.0)


def _hash_noise(x, y, z, seed):
    """Deterministic value noise in [-1, 1] from a lattice hash, trilinearly blended."""
    def h(i, j, k):
        n = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791) ^ (seed * 2654435761)
        n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
        return ((n ^ (n >> 16)) & 0xFFFF) / 32767.5 - 1.0
    i, j, k = math.floor(x), math.floor(y), math.floor(z)
    fx, fy, fz = x - i, y - j, z - k
    fx, fy, fz = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy), fz * fz * (3 - 2 * fz)
    def lerp(a, b, t): return a + (b - a) * t
    c00 = lerp(h(i, j, k), h(i + 1, j, k), fx); c10 = lerp(h(i, j + 1, k), h(i + 1, j + 1, k), fx)
    c01 = lerp(h(i, j, k + 1), h(i + 1, j, k + 1), fx); c11 = lerp(h(i, j + 1, k + 1), h(i + 1, j + 1, k + 1), fx)
    return lerp(lerp(c00, c10, fy), lerp(c01, c11, fy), fz)


class MeshBuilder:
    """Collects primitives as polygons. Each primitive is one smoothing group; boxes and roofs stay flat."""

    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []        # (indices, is_roof, group_id, smooth)
        self._group = 0

    # ---- core
    def _prim(self, points, polys, roof, centre, smooth=False):
        base = len(self.verts)
        self.verts.extend(points)
        self._group += 1
        for p in polys:
            idx = [base + i for i in p]
            a, b, c = points[p[0]], points[p[1]], points[p[2]]
            n = _cross(_sub(b, a), _sub(c, a))
            fc = _mul([sum(points[i][k] for i in p) for k in range(3)], 1.0 / len(p))
            if _dot(n, _sub(fc, centre)) < 0:
                idx.reverse()
            self.faces.append((idx, roof, self._group, smooth))

    # ---- rooms
    def room(self, cx, cy, z0, sx, sy, h, t, doors=(), windows=(), batter=0.0, roof=False):
        """Four wall slabs around an open interior. `doors`/`windows` are (side, offset, width, z_bottom, height) with side in
        "N" (+Y), "S" (-Y), "E" (+X), "W" (-X) and offset along that wall from its centre. Openings split the slab into pieces.
        `batter` leans the outside faces inward a little (daub walls)."""
        def wall(side):
            if side in ("N", "S"):
                length, wy = sx, cy + (sy / 2 - t / 2) * (1 if side == "N" else -1)
            else:
                length, wx = sy, cx + (sx / 2 - t / 2) * (1 if side == "E" else -1)
            cuts = sorted([(o, w, zb, zh) for (sd, o, w, zb, zh) in list(doors) + list(windows) if sd == side])
            # vertical slices: full-height pieces between openings, plus the bits above/below each opening
            pieces = []   # (offset_centre, width, z_bottom, z_top)
            cursor = -length / 2
            for (o, w, zb, zh) in cuts:
                a, b = o - w / 2, o + w / 2
                if a > cursor: pieces.append(((cursor + a) / 2, a - cursor, 0.0, h))
                if zb > 0: pieces.append((o, w, 0.0, zb))
                if zb + zh < h: pieces.append((o, w, zb + zh, h - zb - zh))
                cursor = b
            if cursor < length / 2: pieces.append(((cursor + length / 2) / 2, length / 2 - cursor, 0.0, h))
            for (oc, w, zb, zh) in pieces:
                if w <= 0.005 or zh <= 0.005: continue
                if side in ("N", "S"):
                    self.frustum(cx + oc, wy, z0 + zb, w, t, w, t - batter * zh, zh, roof)
                else:
                    self.frustum(wx, cy + oc, z0 + zb, t, w, t - batter * zh, w, zh, roof)
        for side in ("N", "S", "E", "W"):
            wall(side)

    # ---- flat primitives
    def box(self, cx, cy, z0, sx, sy, sz, roof=False):
        self.frustum(cx, cy, z0, sx, sy, sx, sy, sz, roof)

    def frustum(self, cx, cy, z0, sx0, sy0, sx1, sy1, sz, roof=False):
        """A box whose top is a different size from its bottom (battered walls, tunics, horse heads)."""
        z1 = z0 + sz
        pts = [(cx - sx0 / 2, cy - sy0 / 2, z0), (cx + sx0 / 2, cy - sy0 / 2, z0), (cx + sx0 / 2, cy + sy0 / 2, z0), (cx - sx0 / 2, cy + sy0 / 2, z0),
               (cx - sx1 / 2, cy - sy1 / 2, z1), (cx + sx1 / 2, cy - sy1 / 2, z1), (cx + sx1 / 2, cy + sy1 / 2, z1), (cx - sx1 / 2, cy + sy1 / 2, z1)]
        quads = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        self._prim(pts, quads, roof, (cx, cy, z0 + sz / 2))

    def gable(self, cx, cy, z0, sx, sy, height, overhang, drop=0.0):
        """Gable roof, ridge along +Y. `drop` lowers the eaves below z0 so the thatch hangs past the wall top."""
        x0, x1 = cx - sx / 2 - overhang, cx + sx / 2 + overhang
        y0, y1 = cy - sy / 2 - overhang, cy + sy / 2 + overhang
        ze, z1 = z0 - drop, z0 + height
        t = 0.18  # roof slab thickness so the eaves read as thatch, not paper
        pts = [(cx, y0, z1), (cx, y1, z1), (x0, y0, ze), (x0, y1, ze), (x1, y1, ze), (x1, y0, ze),
               (cx, y0, z1 - t), (cx, y1, z1 - t), (x0 + t, y0, ze - t * 0.5), (x0 + t, y1, ze - t * 0.5), (x1 - t, y1, ze - t * 0.5), (x1 - t, y0, ze - t * 0.5)]
        c = (cx, cy, z0 + height / 2)
        polys = [(2, 3, 1, 0), (4, 5, 0, 1),            # outer slopes
                 (8, 9, 7, 6), (10, 11, 6, 7),           # underside
                 (2, 8, 9, 3), (4, 10, 11, 5),           # eave faces
                 (0, 6, 8, 2), (3, 9, 7, 1), (1, 7, 10, 4), (5, 11, 6, 0)]   # gable edges
        self._prim(pts, polys, True, c)
        o = overhang
        self._prim([(x1 - o, y1 - o, z0), (x0 + o, y1 - o, z0), (cx, y1 - o, z1)], [(0, 1, 2)], False, (cx, cy, z0))
        self._prim([(x0 + o, y0 + o, z0), (x1 - o, y0 + o, z0), (cx, y0 + o, z1)], [(0, 1, 2)], False, (cx, cy, z0))

    def pyramid(self, cx, cy, z0, side, height, roof=True):
        s = side / 2
        pts = [(cx, cy, z0 + height), (cx - s, cy - s, z0), (cx + s, cy - s, z0), (cx + s, cy + s, z0), (cx - s, cy + s, z0)]
        self._prim(pts, [(1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 1, 0), (1, 4, 3, 2)], roof, (cx, cy, z0 + height / 3))

    def wedge(self, cx, cy, z0, sx, sy, sz, roof=False):
        """Triangular prism, ridge along +Y at the top centre: lean-to roofs, hooves, noses."""
        x0, x1, y0, y1, z1 = cx - sx / 2, cx + sx / 2, cy - sy / 2, cy + sy / 2, z0 + sz
        pts = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (cx, y0, z1), (cx, y1, z1)]
        self._prim(pts, [(0, 1, 2, 3), (0, 1, 4), (2, 3, 5), (1, 2, 5, 4), (3, 0, 4, 5)], roof, (cx, cy, z0 + sz / 3))

    # ---- round primitives (smooth)
    def tube(self, p0, p1, r0, r1, n=8, roof=False, cap=True, smooth=True, twist=0.0):
        """Tapered cylinder from p0 to p1 (any direction)."""
        axis = _norm(_sub(p1, p0))
        side = (1.0, 0.0, 0.0) if abs(axis[0]) < 0.9 else (0.0, 1.0, 0.0)
        u = _norm(_cross(axis, side)); v = _cross(axis, u)
        pts, polys = [], []
        for ring, (p, r) in enumerate(((p0, r0), (p1, r1))):
            for i in range(n):
                a = (i / n) * math.tau + twist * ring
                pts.append(_add3(p, _add3(_mul(u, math.cos(a) * r), _mul(v, math.sin(a) * r))))
        for i in range(n):
            j = (i + 1) % n
            polys.append((i, j, n + j, n + i))
        c = _mul(_add3(p0, p1), 0.5)
        self._prim(pts, polys, roof, c, smooth)
        if cap:   # caps are their own flat group so the rim normals stay radial
            self._prim(list(pts), [tuple(range(n)), tuple(range(n, 2 * n))], roof, c, False)

    def cone(self, cx, cy, z0, r, height, n=10, roof=False, smooth=True):
        pts = [(cx + math.cos(i / n * math.tau) * r, cy + math.sin(i / n * math.tau) * r, z0) for i in range(n)] + [(cx, cy, z0 + height)]
        c = (cx, cy, z0 + height / 3)
        self._prim(pts, [(i, (i + 1) % n, n) for i in range(n)], roof, c, smooth)
        self._prim(list(pts[:n]), [tuple(range(n))], roof, c, False)

    def ellipsoid(self, cx, cy, cz, rx, ry, rz, seg=10, rings=6, roof=False, bump=0.0, seed=1, smooth=True, freq=1.0):
        """UV sphere scaled per axis; `bump` displaces vertices with value noise (canopies, rumps, rocks)."""
        pts = [(cx, cy, cz + rz), (cx, cy, cz - rz)]
        for r in range(1, rings):
            phi = math.pi * r / rings
            for s in range(seg):
                th = math.tau * s / seg
                x, y, z = math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi)
                k = 1.0 + bump * _hash_noise(x * freq * 2.0 + seed, y * freq * 2.0, z * freq * 2.0, seed)
                pts.append((cx + x * rx * k, cy + y * ry * k, cz + z * rz * k))
        polys = []
        def idx(r, s): return 2 + (r - 1) * seg + (s % seg)
        for s in range(seg):
            polys.append((0, idx(1, s), idx(1, s + 1)))
            polys.append((1, idx(rings - 1, s + 1), idx(rings - 1, s)))
        for r in range(1, rings - 1):
            for s in range(seg):
                polys.append((idx(r, s), idx(r + 1, s), idx(r + 1, s + 1), idx(r, s + 1)))
        self._prim(pts, polys, roof, (cx, cy, cz), smooth)

    def finish(self, wall_mat, roof_mat):
        self.materials = (wall_mat, roof_mat)
        return self


# ---------------------------------------------------------------- walk-through check
def _tri_box(tri, c, h):
    """Triangle against an axis-aligned box (centre c, half sizes h): the separating axis test."""
    v = [_sub(p, c) for p in tri]
    e = [_sub(v[1], v[0]), _sub(v[2], v[1]), _sub(v[0], v[2])]
    for i in range(3):
        if min(p[i] for p in v) > h[i] or max(p[i] for p in v) < -h[i]: return False
    n = _cross(e[0], e[1])
    if abs(_dot(n, v[0])) > h[0] * abs(n[0]) + h[1] * abs(n[1]) + h[2] * abs(n[2]): return False
    for ed in e:
        for a in ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)):
            ax = _cross(a, ed)
            if _dot(ax, ax) < 1e-12: continue
            pr = [_dot(ax, p) for p in v]
            r = h[0] * abs(ax[0]) + h[1] * abs(ax[1]) + h[2] * abs(ax[2])
            if min(pr) > r or max(pr) < -r: return False
    return True


def check_walk(builder, path, width=0.86, height=1.98, step=0.4, label=""):
    """Slide a person-sized box along `path` (modelling-space points on the floor line: x, y, floor z) and fail loudly if any
    part of the model is in the way. width covers the capsule and its skin; the bottom `step` of the box is ignored
    because a walker steps over that much. Every generator with a door calls this, so a blocked doorway cannot ship."""
    tris = []
    for idx, roof, group, smooth in builder.faces:
        pts = [builder.verts[i] for i in idx]
        for k in range(1, len(pts) - 1): tris.append((pts[0], pts[k], pts[k + 1]))
    hits = 0
    for (a, bb) in zip(path, path[1:]):
        n = max(2, int(_len(_sub(bb, a)) / 0.1))
        for i in range(n + 1):
            t = i / n
            p = (a[0] + (bb[0] - a[0]) * t, a[1] + (bb[1] - a[1]) * t, a[2] + (bb[2] - a[2]) * t)
            c = (p[0], p[1], p[2] + step + (height - step) / 2)
            h = (width / 2, width / 2, (height - step) / 2)
            for tri in tris:
                if _tri_box(tri, c, h):
                    hits += 1
                    if hits <= 3: print("  BLOCKED %s at (%.2f, %.2f, %.2f) by a face near (%.2f, %.2f, %.2f)" % (label, p[0], p[1], p[2], tri[0][0], tri[0][1], tri[0][2]))
                    break
    if hits:
        raise SystemExit("%s: the way through %s is blocked in %d places" % (builder.name, label or "the door", hits))
    print("  walk-through clear: %s %s" % (builder.name, label))


# ---------------------------------------------------------------- OBJ writer
def _to_unity(p):
    """Blender (x, +y forward, z up) -> written (x, y=up, z=forward). Unity's OBJ importer then mirrors X, which
    together with this swap is a proper rotation, so models are not mirrored in the scene."""
    return (p[0], p[2], p[1])


def _write_obj(b, path):
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    pts = [_to_unity(p) for p in b.verts]
    # The builder orders each loop CCW-from-outside in modelling space. The axis swap has determinant -1, so every loop
    # is now CW in the written right-handed space; reverse them so OBJ's CCW-is-front convention holds.
    fixed = []
    for idx, roof, group, smooth in b.faces:
        idx = list(reversed(idx))
        a, bb, c = pts[idx[0]], pts[idx[1]], pts[idx[2]]
        fixed.append((idx, roof, group, smooth, _norm(_cross(_sub(bb, a), _sub(c, a)))))
    # smooth vertex normals per group
    vnormals = {}
    for idx, roof, group, smooth, n in fixed:
        if not smooth: continue
        for i in idx:
            key = (group, i)
            vnormals[key] = _add3(vnormals.get(key, (0.0, 0.0, 0.0)), n)
    normals_out, normal_index = [], {}
    def nidx(n):
        key = tuple(round(c, 4) for c in n)
        if key not in normal_index:
            normal_index[key] = len(normals_out) + 1
            normals_out.append(key)
        return normal_index[key]
    tris = 0
    # Texture coordinates, one unit per metre, so tiling textures (daub, thatch, stone courses, planks) sit at true scale
    # on every model.
    uv_out, uv_index = [], {}
    def uvidx(p, n):
        # written space: y is up. Flats map east and north. Everything else maps u along the surface on the level and v up
        # the surface, so courses of stone lie level and thatch hangs down the slope whatever the pitch.
        if abs(n[1]) > 0.98: uv = (p[0], p[2])
        else:
            l = math.sqrt(n[0] * n[0] + n[2] * n[2])
            r = (-n[2] / l, 0.0, n[0] / l)
            sv = _cross(n, r)
            if sv[1] < 0: sv = (-sv[0], -sv[1], -sv[2])
            uv = (_dot(p, r), _dot(p, sv))
        key = (round(uv[0], 3), round(uv[1], 3))
        if key not in uv_index:
            uv_index[key] = len(uv_out) + 1
            uv_out.append(key)
        return uv_index[key]
    with open(path, "w") as f:
        f.write("# Ashes and Crowns, generated by Tools/blender (pure Python). 1 unit = 1 m, y up.\n")
        f.write("o %s\n" % b.name)
        for p in pts:
            f.write("v %.4f %.4f %.4f\n" % p)
        face_lines = {False: [], True: []}
        for idx, roof, group, smooth, n in fixed:
            parts = []
            for i in idx:
                nn = _norm(vnormals[(group, i)]) if smooth else n
                parts.append("%d/%d/%d" % (i + 1, uvidx(pts[i], n), nidx(nn)))
            face_lines[roof].append("f " + " ".join(parts) + "\n")
            tris += len(idx) - 2
        for uv in uv_out:
            f.write("vt %.3f %.3f\n" % uv)
        for n in normals_out:
            f.write("vn %.4f %.4f %.4f\n" % n)
        for roof in (False, True):
            if not face_lines[roof]: continue
            f.write("usemtl %s\n" % b.materials[1 if roof else 0])
            f.writelines(face_lines[roof])
    print("exported %s (%d verts, %d tris)" % (path, len(pts), tris))
    return path


def _export_fbx_blender(obj_path, fbx_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=obj_path, forward_axis='NEGATIVE_Z', up_axis='Y')
    for o in bpy.context.scene.objects:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', object_types={'MESH'}, bake_anim=False, path_mode='AUTO')
    print("exported", fbx_path)


def export_fbx(builder, path):
    """Always writes the OBJ twin; with Blender available also writes the FBX at `path`."""
    obj_path = path[:-4] + ".obj" if path.lower().endswith(".fbx") else path
    _write_obj(builder, obj_path)
    if HAVE_BLENDER and path.lower().endswith(".fbx"):
        _export_fbx_blender(os.path.abspath(obj_path), os.path.abspath(path))
