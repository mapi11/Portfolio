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

SHOULDERS = Vector((0, 0.03, -0.29))    # середина плеч относительно камеры
GRIP = Vector((-0.12, -0.27, -0.2))    # пистолетная рукоять в позе удержания
AK_SCALE = 0.88 / 1.986                # длина реального АК
ARMS_SCALE = 1.12

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
    points = {
        "muzzle": muzzle,
        "shell": Vector((bolt_lo.x, (bolt_lo.y + bolt_hi.y) / 2, (bolt_lo.z + bolt_hi.z) / 2)),
        "handguard": Vector((GRIP.x, mag_lo.y - 0.05, mag_hi.z + 0.015)),
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
        pole = SHOULDERS + (Vector((0.1, -0.35, -0.35)) if side == "l" else Vector((-0.35, -0.05, -0.25)))
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


def aim_matrix(direction, side_axis, location):
    """Матрица кости: Y вдоль direction, Z перпендикулярно, X по side_axis."""
    y = direction.normalized()
    x = (side_axis - y * side_axis.dot(y)).normalized()
    z = x.cross(y)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = location
    return m


def hold_pose(rig):
    """Пальцы обхватывают рукоять и цевьё; цели IK — в осях оружия."""
    pb = rig.pose.bones
    for side, curl in (("r", 70), ("l", 55)):
        for finger in FINGERS:
            for i, k in (("01", 1.0), ("02", 1.1), ("03", 0.8)):
                b = pb[f"b_{finger}_{i}_{side}"]
                b.rotation_mode = "XYZ"
                b.rotation_euler = (math.radians(curl * k), 0, 0)
        for i, ang in (("01", 20), ("02", 25), ("03", 20)):
            b = pb[f"b_thumb_{i}_{side}"]
            b.rotation_mode = "XYZ"
            b.rotation_euler = (math.radians(ang), 0, 0)


def place_ik_targets(rig, points):
    pb = rig.pose.bones
    # правая кисть: запястье за рукоятью, пальцы вперёд-вниз вокруг неё
    wrist_r = GRIP + Vector((-0.02, 0.07, 0.06))
    dir_r = (FORWARD * 0.55 - UP * 0.75 + RIGHT * -0.15)
    m_r = aim_matrix(dir_r, RIGHT * -1 + UP * 0.2, wrist_r)
    # левая кисть: под цевьём, ладонь вверх
    wrist_l = points["handguard"] + Vector((0.07, 0.05, -0.05))
    dir_l = (FORWARD * 0.6 + RIGHT * 0.75 + UP * 0.1)
    m_l = aim_matrix(dir_l, UP * -1, wrist_l)
    for name, m in (("ik_hand_r", m_r), ("ik_hand_l", m_l)):
        bone = pb[name]
        bone.matrix = m
    bpy.context.view_layer.update()


CONTROLS = ("ik_hand_r", "ik_hand_l", "b_weapon_attach", "b_magazine", "b_bolt", "b_trigger")


def finger_bones(rig):
    return [b.name for b in rig.pose.bones if b.name.startswith(("b_index", "b_middle", "b_ring", "b_pinky", "b_thumb"))]


def key(rig, name, frame, matrix=None):
    """matrix — желаемое положение кости в пространстве арматуры при оружии в покое.
    Для детей b_weapon_root поза родителя сокращается, поэтому basis от кадра не зависит."""
    b = rig.pose.bones[name]
    b.rotation_mode = "QUATERNION" if b.name in CONTROLS else b.rotation_mode
    if matrix is not None:
        b.matrix_basis = b.bone.matrix_local.inverted() @ matrix
    b.keyframe_insert("location", frame=frame)
    b.keyframe_insert("rotation_quaternion" if b.rotation_mode == "QUATERNION" else "rotation_euler", frame=frame)


def new_action(rig, name):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    return action


def make_hold(rig):
    new_action(rig, "Hold")
    for name in CONTROLS + tuple(finger_bones(rig)):
        key(rig, name, 0)
        key(rig, name, 1)


def make_reload(rig, points):
    pb = rig.pose.bones
    rest = {n: pb[n].bone.matrix_local.copy() for n in CONTROLS}
    hold_l = pb["ik_hand_l"].matrix.copy()
    hold_r = pb["ik_hand_r"].matrix.copy()
    new_action(rig, "Reload")

    for name in finger_bones(rig):
        key(rig, name, 0)
    key(rig, "ik_hand_r", 0, hold_r)
    key(rig, "b_trigger", 0, rest["b_trigger"])

    # наклон оружия вокруг рукояти: магазин поворачивается к левой руке
    def tilt(frame, roll, pitch, lift):
        r = Matrix.Rotation(math.radians(roll), 4, FORWARD) @ Matrix.Rotation(math.radians(pitch), 4, RIGHT)
        m = Matrix.Translation(GRIP + lift) @ r @ Matrix.Translation(-GRIP) @ rest["b_weapon_attach"]
        key(rig, "b_weapon_attach", frame, m)

    up = Vector((0.03, 0, 0.03))
    for frame, roll, pitch, lift in ((0, 0, 0, Vector()), (8, 22, 6, up), (44, 22, 6, up), (46, 24, 2, up),
                                     (49, 22, 6, up), (56, 4, 1, up * 0.3),
                                     (59, -2, -1, Vector()), (62, 0, 0, Vector())):
        tilt(frame, roll, pitch, lift)

    grab = points["magazine"] + Vector((0.07, 0.04, -0.06))
    hand = lambda pos: Matrix.Translation(pos - hold_l.translation) @ hold_l
    out_1 = Vector((0.08, 0.06, -0.22))
    out_2 = Vector((0.18, 0.12, -0.45))
    seat = Vector((0, 0, 0.012))
    left = ((0, hold_l), (6, hold_l), (12, hand(grab)), (16, hand(grab)), (24, hand(grab + out_1)),
            (32, hand(grab + out_2)), (38, hand(grab + out_2)), (44, hand(grab + out_1 * 0.35)),
            (47, hand(grab + seat)), (49, hand(grab)), (57, hold_l), (62, hold_l))
    for frame, m in left:
        key(rig, "ik_hand_l", frame, m)

    # магазин едет вместе с рукой, на 32-38 кадре он за кадром — «новый» возвращается
    mag = rest["b_magazine"]
    for frame, offset in ((0, None), (16, None), (24, out_1), (32, out_2), (38, out_2), (44, out_1 * 0.35),
                          (47, seat), (49, None), (62, None)):
        key(rig, "b_magazine", frame, Matrix.Translation(offset) @ mag if offset else mag)

    key(rig, "b_bolt", 0, rest["b_bolt"])


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
    hold_pose(rig)
    place_ik_targets(rig, points)
    make_reload(rig, points)
    make_hold(rig)

    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = 0, 62
    reload = bpy.data.actions["Reload"]
    if PREVIEW:
        os.makedirs(PREVIEW, exist_ok=True)
        rig.animation_data.action = reload
        for frame in (0, 12, 30, 47, 54):
            scene.frame_set(frame)
            render(os.path.join(PREVIEW, f"reload_{frame:02d}.png"), Vector((0, 0, 0)), Vector((0, -1, 0)), lens=None)
        scene.frame_set(0)
        render(os.path.join(PREVIEW, "side.png"), Vector((-1.4, -0.25, -0.1)), Vector((0, -0.25, -0.2)), lens=35)

    export_fbx(rig, mesh, bpy.data.objects["SK_AK47"])
    rig.animation_data.action = bpy.data.actions["Hold"]
    scene.frame_set(0)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)


main()
