"""FP arms + AK47 rig.

blender --background --factory-startup --python DCC/Blender/build_fps_rig.py -- <repo root> [preview dir]

Builds DCC/Blender/FPSArms_AK47.blend and exports
Portfolio/Assets/_Project/Art/Characters/FPSArms/SK_FPSArms_AK47.fbx
"""

import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

args = sys.argv[sys.argv.index("--") + 1:]
ROOT = os.path.abspath(args[0])
PREVIEW = args[1] if len(args) > 1 else None

ARMS_SRC = os.path.join(ROOT, "DCC/Sources/Arms/male_base_mesh_arms.glb")
AK_SRC = os.path.join(ROOT, "DCC/Sources/AK47/AK47.fbx")
BLEND_OUT = os.path.join(ROOT, "DCC/Blender/FPSArms_AK47.blend")
FBX_OUT = os.path.join(ROOT, "Portfolio/Assets/_Project/Art/Characters/FPSArms/SK_FPSArms_AK47.fbx")

# Камера в начале координат, смотрит в -Y, вверх Z (при экспорте -Y станет +Z в Unity).
FORWARD = Vector((0, -1, 0))
RIGHT = Vector((-1, 0, 0))
UP = Vector((0, 0, 1))

SHOULDERS = Vector((0, -0.09, -0.21))   # середина плеч: рукоять ниже плеча, как у стрелка
GRIP = Vector((-0.12, -0.36, -0.29))    # пистолетная рукоять в позе удержания
AK_SCALE = 0.88 / 1.986                # длина реального АК
ARMS_SCALE = 1.12
PALM_LENGTH = 0.07      # от запястья до центра ладони
PALM_THICKNESS = 0.02

FINGERS = ("index", "middle", "ring", "pinky")


def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def bone_name(src):
    side = "l" if src.endswith(".L") else "r"
    base = src[:-2]
    if base == "upper_arm":
        return f"b_upperarm_{side}"
    if base in ("forearm", "hand"):
        return f"b_{base}_{side}"
    if base.startswith("thumb."):
        return f"b_thumb_{base.split('.')[1]}_{side}"
    finger, index = base[2:].split(".")
    return f"b_{finger}_{index}_{side}"


def import_arms():
    bpy.ops.import_scene.gltf(filepath=ARMS_SRC)
    rig = bpy.data.objects["metarig"]
    mesh = bpy.data.objects["mesh"]
    rig.name = "FPSArms"
    rig.data.name = "FPSArms"
    mesh.name = "SK_FPSArms"

    # Исходник смотрит в +X. Поворачиваем к камере (-Y) и ставим плечи под камеру.
    shoulder_mid = (rig.matrix_world @ rig.data.bones["upper_arm.L"].head_local +
                    rig.matrix_world @ rig.data.bones["upper_arm.R"].head_local) / 2
    turn = Matrix.Rotation(math.radians(-90), 4, "Z")
    # базовый меш мелковат: до запястья 0.57 м, у взрослого около 0.63
    scale = Matrix.Scale(ARMS_SCALE, 4)
    move = Matrix.Translation(SHOULDERS - scale @ turn @ shoulder_mid)
    for obj in (rig, mesh):
        obj.matrix_world = move @ scale @ turn @ obj.matrix_world
    bpy.context.view_layer.update()
    for obj in (mesh, rig):
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    for b in rig.data.bones:
        new = bone_name(b.name)
        group = mesh.vertex_groups.get(b.name)
        if group:
            group.name = new
        b.name = new
    return rig, mesh


def import_ak():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=AK_SRC)
    parts = {o.name.replace("AK47 ", ""): o for o in set(bpy.data.objects) - before if o.type == "MESH"}
    for o in parts.values():
        world = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = world
    bpy.context.view_layer.update()
    for o in parts.values():
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    def bounds(o):
        vs = [v.co for v in o.data.vertices]
        return (Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))),
                Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs))))

    body_lo, body_hi = bounds(parts["Body"])
    trig_lo, trig_hi = bounds(parts["Trigger"])
    trigger = (trig_lo + trig_hi) / 2
    center_x = (body_lo.x + body_hi.x) / 2
    # рукоять: чуть позади и ниже спуска
    grip_src = Vector((center_x, trigger.y + 0.14, trigger.z - 0.13))

    # Масштаб до реального и перенос рукояти в точку удержания.
    xf = Matrix.Translation(GRIP) @ Matrix.Scale(AK_SCALE, 4) @ Matrix.Translation(-grip_src)
    for o in parts.values():
        o.data.transform(xf)
        o.data.update()

    body = parts["Body"]
    verts = [v.co for v in body.data.vertices]
    muzzle_y = min(v.y for v in verts)
    front = [v for v in verts if v.y < muzzle_y + 0.02]
    muzzle = Vector((sum(v.x for v in front) / len(front), muzzle_y, sum(v.z for v in front) / len(front)))
    mag_lo, mag_hi = bounds(parts["Magazine"])
    bolt_lo, bolt_hi = bounds(parts["Bolt"])
    top = max(v.z for v in verts if abs(v.y - GRIP.y) < 0.15)
    trig = xf @ trigger

    # рукоять: всё, что ниже спуска и позади него
    grip = [v for v in verts if trig.y + 0.01 < v.y < trig.y + 0.13 and v.z < trig.z - 0.02]
    grip_top = max(v.z for v in grip)
    grip_bottom = min(v.z for v in grip)
    upper = [v for v in grip if v.z > grip_top - 0.02]
    lower = [v for v in grip if v.z < grip_bottom + 0.02]
    grip_a = sum(upper, Vector()) / len(upper)
    grip_b = sum(lower, Vector()) / len(lower)
    grip_width = max(v.x for v in grip) - min(v.x for v in grip)

    # цевьё: впереди магазина, под стволом
    hg = [v for v in verts if mag_lo.y - 0.12 < v.y < mag_lo.y - 0.02 and v.z < top - 0.02]
    hg_bottom = min(v.z for v in hg)
    hg_y = mag_lo.y - 0.06
    handguard = Vector(((min(v.x for v in hg) + max(v.x for v in hg)) / 2, hg_y, hg_bottom))
    points = {
        "grip_top": grip_a,
        "grip_bottom": grip_b,
        "grip_width": grip_width,
        "handguard": handguard,
        "handguard_bottom": handguard,
        "mag_front_top": Vector(((mag_lo.x + mag_hi.x) / 2, mag_lo.y, mag_hi.z)),
        "mag_width": mag_hi.x - mag_lo.x,
        "muzzle": muzzle,
        "shell": Vector((bolt_lo.x, (bolt_lo.y + bolt_hi.y) / 2, (bolt_lo.z + bolt_hi.z) / 2)),
        "magazine": (mag_lo + mag_hi) / 2,
        "mag_top": Vector(((mag_lo.x + mag_hi.x) / 2, (mag_lo.y + mag_hi.y) / 2, mag_hi.z)),
        "bolt": (bolt_lo + bolt_hi) / 2,
        "trigger": xf @ trigger,
        "sight": Vector((GRIP.x, GRIP.y - 0.05, top)),
    }

    # Один меш с жёсткими весами: одна SkinnedMesh вместо четырёх объектов.
    groups = {"Body": "b_weapon_root", "Bolt": "b_bolt", "Magazine": "b_magazine", "Trigger": "b_trigger"}
    for name, o in parts.items():
        g = o.vertex_groups.new(name=groups[name])
        g.add(range(len(o.data.vertices)), 1.0, "REPLACE")
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts.values():
        o.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = "SK_AK47"
    body.data.name = "SK_AK47"
    return body, points


def edit_bone(eb_map, name, head, tail, parent=None, deform=True, roll=0.0):
    eb = eb_map.new(name)
    eb.head = head
    eb.tail = tail
    eb.roll = roll
    eb.parent = eb_map[parent] if parent else None
    eb.use_deform = deform
    return eb


def build_skeleton(rig, points):
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones

    edit_bone(eb, "b_root", Vector((0, 0, 0)), Vector((0, 0, 0.1)))
    edit_bone(eb, "b_camera", Vector((0, 0, 0)), FORWARD * 0.1, "b_root")
    edit_bone(eb, "b_spine_fp", SHOULDERS, SHOULDERS + UP * 0.08, "b_camera")
    for side, sign in (("l", 1), ("r", -1)):
        upper = eb[f"b_upperarm_{side}"]
        clav = edit_bone(eb, f"b_clavicle_{side}", SHOULDERS + Vector((0.04 * sign, 0, 0.02)), upper.head.copy(), "b_spine_fp", deform=False)
        upper.parent = clav
        upper.use_connect = False
        fore = eb[f"b_forearm_{side}"]
        twist = edit_bone(eb, f"b_forearm_twist_{side}", fore.head.lerp(fore.tail, 0.5), fore.tail.copy(), f"b_forearm_{side}")
        twist.align_roll(fore.z_axis)

    attach = edit_bone(eb, "b_weapon_attach", GRIP, GRIP + FORWARD * 0.1, "b_spine_fp")
    edit_bone(eb, "b_weapon_root", GRIP, GRIP + FORWARD * 0.1, "b_weapon_attach")
    edit_bone(eb, "b_bolt", points["bolt"], points["bolt"] + FORWARD * 0.04, "b_weapon_root")
    edit_bone(eb, "b_magazine", points["mag_top"], points["mag_top"] - UP * 0.08, "b_weapon_root")
    edit_bone(eb, "b_trigger", points["trigger"], points["trigger"] - UP * 0.03, "b_weapon_root")
    for name, key in (("socket_muzzle", "muzzle"), ("socket_shell_eject", "shell"), ("socket_lhand_ik", "handguard"), ("socket_sight", "sight")):
        edit_bone(eb, name, points[key], points[key] + FORWARD * 0.03, "b_weapon_root")

    # Контролы для аниматора: в FBX не попадают (не deform).
    edit_bone(eb, "ik_hand_r", eb["b_hand_r"].head.copy(), eb["b_hand_r"].tail.copy(), "b_weapon_root", deform=False)
    edit_bone(eb, "ik_hand_l", eb["b_hand_l"].head.copy(), eb["b_hand_l"].tail.copy(), "b_weapon_root", deform=False)
    for side, sign in (("l", 1), ("r", -1)):
        elbow = eb[f"b_forearm_{side}"].head.copy()
        pole = SHOULDERS + (Vector((0.15, -0.3, -0.4)) if side == "l" else Vector((-0.4, -0.08, -0.25)))
        edit_bone(eb, f"ik_pole_elbow_{side}", pole, pole + UP * 0.05, "b_spine_fp", deform=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def setup_constraints(rig):
    pb = rig.pose.bones
    for side in ("l", "r"):
        ik = pb[f"b_forearm_{side}"].constraints.new("IK")
        ik.target = rig
        ik.subtarget = f"ik_hand_{side}"
        ik.pole_target = rig
        ik.pole_subtarget = f"ik_pole_elbow_{side}"
        ik.pole_angle = math.radians(-90)
        ik.chain_count = 2
        rot = pb[f"b_hand_{side}"].constraints.new("COPY_ROTATION")
        rot.target = rig
        rot.subtarget = f"ik_hand_{side}"
        # половина скручивания кисти уходит в twist-кость предплечья
        tw = pb[f"b_forearm_twist_{side}"].constraints.new("COPY_ROTATION")
        tw.target = rig
        tw.subtarget = f"b_hand_{side}"
        tw.use_x = tw.use_z = False
        tw.target_space = tw.owner_space = "LOCAL"
        tw.influence = 0.5


def split_twist_weights(rig, mesh):
    for side in ("l", "r"):
        fore = rig.data.bones[f"b_forearm_{side}"]
        a, b = fore.head_local, fore.tail_local
        src = mesh.vertex_groups[f"b_forearm_{side}"]
        dst = mesh.vertex_groups.new(name=f"b_forearm_twist_{side}")
        axis = b - a
        for v in mesh.data.vertices:
            w = next((g.weight for g in v.groups if g.group == src.index), 0.0)
            if w <= 0:
                continue
            t = max(0.0, min(1.0, (v.co - a).dot(axis) / axis.length_squared))
            k = max(0.0, min(1.0, (t - 0.3) / 0.7))
            k = k * k * (3 - 2 * k)
            if k > 0:
                src.add([v.index], w * (1 - k), "REPLACE")
                dst.add([v.index], w * k, "REPLACE")
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def hand_matrix(side, contact, palm, fingers):
    """Матрица кисти по точке контакта ладони, направлению ладони и пальцев.
    Оси кости кисти: Y — пальцы, Z — большой палец, ладонь смотрит в +X у левой и в -X у правой."""
    f = fingers.normalized()
    n = (palm - f * palm.dot(f)).normalized()
    x = n if side == "l" else -n
    m = Matrix((x, f, x.cross(f))).transposed().to_4x4()
    m.translation = contact - f * PALM_LENGTH - n * PALM_THICKNESS
    return m


# Сгиб к ладони — положительный поворот по X (и у пальцев, и у большого).
# В покое пальцы модели уже согнуты на 25-35 градусов, это учтено в первом суставе.
GRIPS = {
    "open":       {"index": (-15, 5, 5), "middle": (-15, 5, 5), "ring": (-15, 5, 5), "pinky": (-15, 5, 5), "thumb": (0, 0, 0)},
    "pistol":     {"index": (25, 35, 20), "middle": (55, 85, 55), "ring": (55, 85, 55), "pinky": (55, 85, 55), "thumb": (35, 30, 20)},
    "pistol_off": {"index": (0, 5, 5), "middle": (55, 85, 55), "ring": (55, 85, 55), "pinky": (55, 85, 55), "thumb": (35, 30, 20)},
    "handguard":  {"index": (35, 60, 40), "middle": (40, 65, 45), "ring": (45, 65, 45), "pinky": (45, 70, 45), "thumb": (20, 10, 5)},
    "magazine":   {"index": (-5, 10, 45), "middle": (-5, 12, 50), "ring": (0, 15, 50), "pinky": (5, 20, 50), "thumb": (20, 10, 0)},
}


def set_grip(rig, side, grip, frame=None):
    for finger, angles in GRIPS[grip].items():
        for i, angle in zip(("01", "02", "03"), angles):
            b = rig.pose.bones[f"b_{finger}_{i}_{side}"]
            b.rotation_mode = "XYZ"
            b.rotation_euler = (math.radians(angle), 0, 0)
            if frame is not None:
                b.keyframe_insert("rotation_euler", frame=frame)


HOLD = {}


def aligned_hand(rig, side, contact, palm, wanted, follow=0.65):
    # пальцы продолжают предплечье, поэтому запястье почти прямое; wanted чуть доворачивает их к хвату.
    # Цель IK — запястье, поворот кисти на локоть не влияет, хватает пары итераций.
    target = rig.pose.bones[f"ik_hand_{side}"]
    forearm = rig.pose.bones[f"b_forearm_{side}"]
    m = hand_matrix(side, contact, palm, wanted)
    for _ in range(3):
        target.matrix = m
        bpy.context.view_layer.update()
        along = (forearm.tail - forearm.head).normalized()
        m = hand_matrix(side, contact, palm, along.lerp(wanted.normalized(), 1 - follow))
    target.matrix = m
    bpy.context.view_layer.update()
    return m


def hold_targets(points):
    return HOLD["r"], HOLD["l"]


def place_ik_targets(rig, points):
    # правая: ладонь к рукояти с правой стороны, пальцы обхватывают её спереди
    mid = points["grip_top"].lerp(points["grip_bottom"], 0.45)
    palm_r = (-RIGHT + FORWARD * 0.25).normalized()
    contact_r = mid - palm_r * (points["grip_width"] * 0.5)
    HOLD["r"] = aligned_hand(rig, "r", contact_r, palm_r, FORWARD - UP * 0.3)

    # левая: ладонь вверх-вправо под цевьём (полуповорот предплечья, без перекрута), пальцы справа
    palm_l = (UP + RIGHT * 0.9).normalized()
    center = points["handguard_bottom"] + UP * 0.022
    HOLD["l"] = aligned_hand(rig, "l", center - palm_l * 0.022, palm_l, RIGHT + FORWARD * 0.4)

    set_grip(rig, "r", "pistol")
    set_grip(rig, "l", "handguard")
    bpy.context.view_layer.update()


CONTROLS = ("ik_hand_r", "ik_hand_l", "b_weapon_attach", "b_magazine", "b_bolt", "b_trigger")


def finger_bones(rig):
    return [b.name for b in rig.pose.bones if b.name.startswith(("b_index", "b_middle", "b_ring", "b_pinky", "b_thumb"))]


def key(rig, name, frame, matrix=None):
    # matrix — положение кости в пространстве арматуры при оружии в покое.
    # Для детей b_weapon_root поза родителя сокращается, поэтому basis от кадра не зависит.
    b = rig.pose.bones[name]
    b.rotation_mode = "QUATERNION"
    if matrix is not None:
        b.matrix_basis = b.bone.matrix_local.inverted() @ matrix
    b.keyframe_insert("location", frame=frame)
    b.keyframe_insert("rotation_quaternion", frame=frame)


def new_action(rig, name):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    return action


def make_hold(rig, points):
    new_action(rig, "Hold")
    hold_r, hold_l = hold_targets(points)
    for frame in (0, 1):
        key(rig, "ik_hand_r", frame, hold_r)
        key(rig, "ik_hand_l", frame, hold_l)
        for name in ("b_weapon_attach", "b_magazine", "b_bolt", "b_trigger"):
            key(rig, name, frame, rig.pose.bones[name].bone.matrix_local)
        set_grip(rig, "r", "pistol", frame)
        set_grip(rig, "l", "handguard", frame)


def make_reload(rig, points):
    rest = {n: rig.pose.bones[n].bone.matrix_local.copy() for n in CONTROLS}
    hold_r, hold_l = hold_targets(points)

    # ладонь на левой стороне магазина, пальцы вдоль неё вперёд, кончики за передний край
    mag_side = points["magazine"] + RIGHT * -(points["mag_width"] * 0.5)
    grab = aligned_hand(rig, "l", mag_side + UP * -0.02 + FORWARD * -0.015, RIGHT, FORWARD - UP * 0.35, follow=0.5)
    rig.pose.bones["ik_hand_l"].matrix = hold_l
    new_action(rig, "Reload")

    key(rig, "ik_hand_r", 0, hold_r)
    key(rig, "b_trigger", 0, rest["b_trigger"])
    key(rig, "b_bolt", 0, rest["b_bolt"])

    # оружие: замах вниз, наклон магазином к левой руке, толчок при досылании, возврат с перелётом
    def tilt(frame, roll, pitch, lift):
        r = Matrix.Rotation(math.radians(roll), 4, FORWARD) @ Matrix.Rotation(math.radians(pitch), 4, RIGHT)
        m = Matrix.Translation(GRIP + lift) @ r @ Matrix.Translation(-GRIP) @ rest["b_weapon_attach"]
        key(rig, "b_weapon_attach", frame, m)

    lift = Vector((0.025, 0.0, 0.02))
    for frame, roll, pitch, offset in (
            (0, 0, 0, Vector()), (4, -2, -2, Vector((0, 0, -0.008))), (11, 24, 8, lift), (18, 26, 6, lift),
            (22, 23, 9, lift), (40, 24, 8, lift), (46, 25, 7, lift), (48, 22, 4, lift + Vector((0, 0, 0.012))),
            (51, 24, 8, lift), (56, 8, 3, lift * 0.4), (61, -3, -1.5, Vector()), (66, 0, 0, Vector())):
        tilt(frame, roll, pitch, offset)

    # магазин: качнуть вперёд на переднем зацепе, вытащить вниз; новый — зацепом вперёд и досылом назад
    pivot = points["mag_front_top"]

    def mag(rock, offset=Vector()):
        return (Matrix.Translation(offset + pivot) @ Matrix.Rotation(math.radians(rock), 4, "X")
                @ Matrix.Translation(-pivot))


    out_1 = Vector((0.06, 0.05, -0.16))
    out_2 = Vector((0.2, 0.14, -0.42))
    path = (
        (14, mag(0)), (16, mag(0)), (19, mag(-22)), (24, mag(-26, out_1)), (31, mag(-35, out_2)),
        (38, mag(-35, out_2)), (43, mag(-28, out_1 * 0.4)), (46, mag(-24, Vector((0, 0, 0.003)))),
        (48, mag(1, Vector((0, 0, 0.006)))), (50, mag(0)),
    )
    for frame, m in path:
        key(rig, "b_magazine", frame, m @ rest["b_magazine"])
        key(rig, "ik_hand_l", frame, m @ grab)
    key(rig, "b_magazine", 0, rest["b_magazine"])
    key(rig, "b_magazine", 66, rest["b_magazine"])

    # левая рука: с цевья к магазину по дуге вниз, после досыла обратно
    down = Matrix.Translation(Vector((0.02, 0.02, -0.05)))
    key(rig, "ik_hand_l", 0, hold_l)
    key(rig, "ik_hand_l", 5, hold_l)
    key(rig, "ik_hand_l", 10, down @ hold_l.lerp(grab, 0.6))
    key(rig, "ik_hand_l", 53, grab)
    key(rig, "ik_hand_l", 58, down @ grab.lerp(hold_l, 0.6))
    key(rig, "ik_hand_l", 62, hold_l)
    key(rig, "ik_hand_l", 66, hold_l)

    for frame, grip in ((0, "handguard"), (5, "handguard"), (9, "open"), (13, "open"), (15, "magazine"),
                        (50, "magazine"), (52, "open"), (59, "open"), (62, "handguard"), (66, "handguard")):
        set_grip(rig, "l", grip, frame)
    # указательный правой снимается со спуска на время перезарядки
    for frame, grip in ((0, "pistol"), (4, "pistol"), (8, "pistol_off"), (58, "pistol_off"), (63, "pistol"), (66, "pistol")):
        set_grip(rig, "r", grip, frame)


def export_fbx(rig, mesh, weapon):
    os.makedirs(os.path.dirname(FBX_OUT), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (rig, mesh, weapon):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=FBX_OUT, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=False,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        use_armature_deform_only=True, armature_nodetype="NULL",
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)


def bind(rig, mesh, weapon):
    for obj in (mesh, weapon):
        obj.parent = rig
        mod = obj.modifiers.get("Armature") or obj.modifiers.new("Armature", "ARMATURE")
        mod.object = rig


def render(path, location, target, lens=24):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x, scene.render.resolution_y = 960, 540
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        scene.collection.objects.link(cam)
    if lens:
        cam.data.lens = lens
    else:
        cam.data.sensor_fit = "VERTICAL"
        cam.data.angle = math.radians(70)
    cam.data.clip_start = 0.01
    cam.location = location
    cam.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    clean()
    rig, mesh = import_arms()
    weapon, points = import_ak()
    build_skeleton(rig, points)
    split_twist_weights(rig, mesh)
    bind(rig, mesh, weapon)
    setup_constraints(rig)
    place_ik_targets(rig, points)
    make_reload(rig, points)
    make_hold(rig, points)

    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = 0, 66
    reload = bpy.data.actions["Reload"]
    if PREVIEW:
        os.makedirs(PREVIEW, exist_ok=True)
        rig.animation_data.action = reload
        for frame in (0, 10, 19, 31, 47, 58):
            scene.frame_set(frame)
            render(os.path.join(PREVIEW, f"reload_{frame:02d}.png"), Vector((0, 0, 0)), Vector((0, -1, 0)), lens=None)
        scene.frame_set(0)
        render(os.path.join(PREVIEW, "side.png"), Vector((-1.4, -0.25, -0.1)), Vector((0, -0.25, -0.2)), lens=35)

    export_fbx(rig, mesh, bpy.data.objects["SK_AK47"])
    rig.animation_data.action = bpy.data.actions["Hold"]
    scene.frame_set(0)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)


main()
