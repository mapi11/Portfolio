"""Модели для LOD Demo: LOD0-2 через Decimate, реальный размер, пивот в основании.

blender --background --factory-startup --python DCC/Blender/prepare_lod_demo.py -- <repo root>
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
SOURCES = os.path.join(ROOT, "DCC/Sources/Kenney")
OUT = os.path.join(ROOT, "Portfolio/Assets/_Project/Art/LODDemo")

# имя, исходник, высота в метрах, доли треугольников для LOD1 и LOD2
MODELS = (
    ("House", "House/building-type-c.glb", 7.5, (0.45, 0.15)),
    ("Car", "Car/sedan.glb", 1.5, (0.45, 0.15)),
    ("Tree", "Tree/tree_detailed.glb", 7.0, (0.5, 0.2)),
)


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def prepare(name, source, height, ratios):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SOURCES, source))

    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in meshes:
        world = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = world
    for o in list(bpy.data.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    lod0 = bpy.context.view_layer.objects.active

    # пивот в центре основания, высота — реальная
    vs = [v.co for v in lod0.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    base = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    lod0.data.transform(Matrix.Scale(height / (hi.z - lo.z), 4) @ Matrix.Translation(-base))
    lod0.name = lod0.data.name = f"SM_{name}_LOD0"

    for index, ratio in enumerate(ratios, start=1):
        lod = lod0.copy()
        lod.data = lod0.data.copy()
        lod.name = lod.data.name = f"SM_{name}_LOD{index}"
        bpy.context.scene.collection.objects.link(lod)
        decimate = lod.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = ratio
        bpy.ops.object.select_all(action="DESELECT")
        lod.select_set(True)
        bpy.context.view_layer.objects.active = lod
        bpy.ops.object.modifier_apply(modifier=decimate.name)

    for o in sorted(bpy.data.objects, key=lambda o: o.name):
        print("LOD", o.name, tris(o))

    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, f"SM_{name}.fbx"), object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=True, bake_anim=False)


for model in MODELS:
    prepare(*model)
