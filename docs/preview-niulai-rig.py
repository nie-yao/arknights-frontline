import bpy, os, math, json
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
path=os.path.join(root,'docs/niulai-source/NiuLai_Rigged.blend')
bpy.ops.wm.open_mainfile(filepath=path)
output=os.path.join(root,'docs/niulai-source/preview')
os.makedirs(output,exist_ok=True)
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
material=bpy.data.materials.new('NiuLaiPreviewMaterial'); material.use_nodes=True
nodes=material.node_tree.nodes; links=material.node_tree.links
shader=nodes.get('Principled BSDF'); shader.inputs['Roughness'].default_value=.55
for name,kind in [('texture_pbr_20250901.png','base'),('texture_pbr_20250901_normal.png','normal')]:
    texture=nodes.new('ShaderNodeTexImage')
    texture.image=bpy.data.images.load(os.path.join(root,'Assets/Game/Characters/NiuLai/Resources',name),check_existing=True)
    texture.image.pack()
    if kind=='base': links.new(texture.outputs['Color'],shader.inputs['Base Color'])
    else:
        texture.image.colorspace_settings.name='Non-Color'
        normal=nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value=.6
        links.new(texture.outputs['Color'],normal.inputs['Color']); links.new(normal.outputs['Normal'],shader.inputs['Normal'])
body.data.materials.clear(); body.data.materials.append(material)
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=512; scene.render.resolution_y=512; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world=bpy.data.worlds.new('PreviewWorld'); scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.09,.12,.17,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
bpy.ops.object.camera_add(location=(1.5,-2.6,1.25))
camera=bpy.context.object; camera.name='PreviewCamera'
camera.rotation_euler=(Vector((0,.04,.49))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'; camera.data.ortho_scale=1.45; scene.camera=camera
for position,power,size in [((1,-2,3),250,3),((-2,-1,1),100,2),((0,2,2),180,2)]:
    bpy.ops.object.light_add(type='AREA',location=position)
    light=bpy.context.object; light.data.energy=power; light.data.shape='DISK'; light.data.size=size
    light.rotation_euler=(Vector((0,0,.5))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025))
floor=bpy.context.object; floor.name='PreviewFloor'
floor_material=bpy.data.materials.new('Floor'); floor_material.diffuse_color=(.055,.07,.09,1); floor.data.materials.append(floor_material)
report=[]
for name,frame in [('Idle',1),('Run',7),('Attack',10),('Flight',11)]:
    rig.animation_data.action=bpy.data.actions[name]; scene.frame_set(frame)
    deps=bpy.context.evaluated_depsgraph_get(); evaluated=body.evaluated_get(deps)
    evaluated_mesh=evaluated.to_mesh()
    coords=[body.matrix_world@v.co for v in evaluated_mesh.vertices]
    report.append({'action':name,'min':[min(v[i] for v in coords) for i in range(3)],'max':[max(v[i] for v in coords) for i in range(3)]})
    evaluated.to_mesh_clear()
    scene.render.filepath=os.path.join(output,name+'.png'); bpy.ops.render.render(write_still=True)
for action_name,end in [('Idle',60),('Run',24),('Attack',18)]:
    rig.animation_data.action=bpy.data.actions[action_name]
    for index in range(16):
        position=1+index*(end-1)/16
        scene.frame_set(int(position),subframe=position-int(position))
        scene.render.filepath=os.path.join(output,f'{action_name.lower()}-{index:02}.png')
        bpy.ops.render.render(write_still=True)
rig.animation_data.action=bpy.data.actions['Idle']; scene.frame_set(1)
scene.frame_end=60
rig.select_set(True); body.select_set(False); floor.select_set(False)
bpy.context.view_layer.objects.active=rig
# Default viewport opens on the rig, with packed textures and ready-to-play Idle action.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=2
            area.spaces.active.region_3d.view_location=Vector((0,0,.5))
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=path)
with open(os.path.join(output,'deformation-bounds.json'),'w') as f: json.dump(report,f,indent=2)
