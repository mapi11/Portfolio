"""AK47 для Weapon Range: реальный масштаб, пивот на рукояти, LOD0-2, сокеты.

blender --background --factory-startup --python DCC/Blender/prepare_ak47.py -- <repo root>
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
SOURCE = os.path.join(ROOT, "DCC/Sources/AK47/AK47.fbx")
BLEND_OUT = os.path.join(ROOT, "DCC/Blender/AK47.blend")
FBX_OUT = os.path.join(ROOT, "Portfolio/Assets/_Project/Art/Weapons/AK47/SM_AK47.fbx")

LENGTH = 0.88                         # реальная длина АК
LODS = ((0, 1.0), (1, 0.55), (2, 0.3))  # доля треугольников


def bounds(vertices):
    return (Vector((min(v.x for v in vertices), min(v.y for v in vertices), min(v.z for v in vertices))),
            Vector((max(v.x for v in vertices), max(v.y for v in vertices), max(v.z for v in vertices))))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SOURCE)
    parts = {o.name.replace("AK47 ", ""): o for o in bpy.data.objects if o.type == "MESH"}
    for o in parts.values():
        world = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = world
    for o in list(bpy.data.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    for o in parts.values():
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    body = [v.co.copy() for v in parts["Body"].data.vertices]
    body_lo, body_hi = bounds(body)
    trig_lo, trig_hi = bounds([v.co for v in parts["Trigger"].data.vertices])
    trigger = (trig_lo + trig_hi) / 2
    scale = LENGTH / (body_hi.y - body_lo.y)
    # пивот — пистолетная рукоять: за ней удобно держать оружие в кадре и считать отдачу
    grip = Vector(((body_lo.x + body_hi.x) / 2, trigger.y + 0.14, trigger.z - 0.13))
    xf = Matrix.Scale(scale, 4) @ Matrix.Translation(-grip)

    front = [v for v in body if v.y < body_lo.y + 0.04]
    muzzle = xf @ (sum(front, Vector()) / len(front))
    muzzle.y = (xf @ body_lo).y
    bolt_lo, bolt_hi = bounds([v.co for v in parts["Bolt"].data.vertices])
    shell = xf @ Vector((bolt_lo.x, (bolt_lo.y + bolt_hi.y) / 2, (bolt_lo.z + bolt_hi.z) / 2))

    for o in parts.values():
        o.data.transform(xf)
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts.values():
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts["Body"]
    bpy.ops.object.join()
    lod0 = parts["Body"]
    lod0.name = lod0.data.name = "SM_AK47_LOD0"

    for index, ratio in LODS[1:]:
        lod = lod0.copy()
        lod.data = lod0.data.copy()
        lod.name = lod.data.name = f"SM_AK47_LOD{index}"
        bpy.context.scene.collection.objects.link(lod)
        decimate = lod.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = ratio
        bpy.ops.object.select_all(action="DESELECT")
        lod.select_set(True)
        bpy.context.view_layer.objects.active = lod
        bpy.ops.object.modifier_apply(modifier=decimate.name)

    # сокеты — пустышки, Unity импортирует их как Transform
    for name, location in (("socket_muzzle", muzzle), ("socket_shell_eject", shell)):
        empty = bpy.data.objects.new(name, None)
        empty.location = location
        bpy.context.scene.collection.objects.link(empty)

    for o in bpy.data.objects:
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons) if o.type == "MESH" else 0
        print("AK47", o.name, "tris", tris, "loc", tuple(round(x, 3) for x in o.location))

    os.makedirs(os.path.dirname(FBX_OUT), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_OUT, use_selection=False, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)


main()
