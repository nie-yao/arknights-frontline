import bpy, os, json, sys
source = sys.argv[sys.argv.index('--') + 1]
target = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Assets/Game/Characters/NiuLai/Resources')
os.makedirs(target, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
original = len(obj.data.polygons)
modifier = obj.modifiers.new('GameReduction', 'DECIMATE')
modifier.ratio = 40000 / original
bpy.ops.object.modifier_apply(modifier=modifier.name)
for image in bpy.data.images:
    if image.type == 'IMAGE':
        image.filepath_raw = os.path.join(target, image.name + '.png')
        image.file_format = 'PNG'
        image.save()
bpy.ops.export_scene.fbx(filepath=os.path.join(target, 'NiuLai.fbx'), use_selection=True,
    object_types={'MESH'}, bake_anim=False, path_mode='COPY', embed_textures=False,
    axis_forward='-Z', axis_up='Y')
print(json.dumps({'original_faces': original, 'game_faces': len(obj.data.polygons)}))
