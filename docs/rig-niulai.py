"""Build a reproducible folded-arm Generic rig without modifying the static FBX."""
import bpy, math, os, json
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
directory=os.path.join(root,'Assets/Game/Characters/NiuLai/Rigged')
os.makedirs(directory,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(root,'Assets/Game/Characters/NiuLai/Resources/NiuLai.fbx'))
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=mesh
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
mesh.name='NiuLaiBody'
data=bpy.data.armatures.new('NiuLaiSkeleton')
rig=bpy.data.objects.new('NiuLaiRig',data)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
mesh.select_set(False); rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
definitions=[
 ('Root',(0,0,0),(0,0,.08),None),
 ('Pelvis',(0,0,.28),(0,0,.38),'Root'),
 ('Spine',(0,0,.38),(0,0,.49),'Pelvis'),
 ('Chest',(0,0,.49),(0,0,.62),'Spine'),
 ('Neck',(0,0,.62),(0,0,.70),'Chest'),
 ('Head',(0,0,.70),(0,0,.90),'Neck'),
 ('Thigh.L',(.10,0,.29),(.12,0,.15),'Pelvis'),
 ('Shin.L',(.12,0,.15),(.13,0,.055),'Thigh.L'),
 ('Foot.L',(.13,0,.055),(.13,-.065,.025),'Shin.L'),
 ('Thigh.R',(-.10,0,.29),(-.12,0,.15),'Pelvis'),
 ('Shin.R',(-.12,0,.15),(-.13,0,.055),'Thigh.R'),
 ('Foot.R',(-.13,0,.055),(-.13,-.065,.025),'Shin.R'),
 ('TailBase',(0,.13,.30),(0,.27,.24),'Pelvis'),
 ('TailMid',(0,.27,.24),(0,.43,.20),'TailBase'),
 ('TailTip',(0,.43,.20),(0,.66,.16),'TailMid')]
for name,head,tail,parent in definitions:
    bone=data.edit_bones.new(name); bone.head=head; bone.tail=tail
    if parent: bone.parent=data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
groups={name:mesh.vertex_groups.new(name=name) for name,_,_,_ in definitions}
def blend(value,knots):
    if value<=knots[0][0]: return {knots[0][1]:1.0}
    for (a,first),(b,second) in zip(knots,knots[1:]):
        if value<=b:
            t=(value-a)/(b-a); return {first:1-t,second:t}
    return {knots[-1][1]:1.0}
for vertex in mesh.data.vertices:
    x,y,z=vertex.co
    if y>.21 and z<.33:
        weights=blend(y,[(.21,'TailBase'),(.35,'TailMid'),(.53,'TailTip')])
    elif z<.31:
        side='L' if x>=0 else 'R'
        weights=blend(z,[(.04,'Foot.'+side),(.11,'Shin.'+side),(.22,'Thigh.'+side),(.31,'Pelvis')])
    else:
        weights=blend(z,[(.31,'Pelvis'),(.40,'Spine'),(.53,'Chest'),(.64,'Neck'),(.72,'Head')])
    for name,weight in weights.items():
        if weight>0: groups[name].add([vertex.index],weight,'REPLACE')
mesh.parent=rig
modifier=mesh.modifiers.new('NiuLaiSkin','ARMATURE'); modifier.object=rig
modifier.use_deform_preserve_volume=True
rig.animation_data_create()
for name,frames in [('Idle',60),('Run',24),('Attack',18),('Flight',21)]:
    action=bpy.data.actions.new(name); rig.animation_data.action=action
    for frame in range(1,frames+1):
        t=(frame-1)/(frames-1); phase=t*math.tau
        for bone in rig.pose.bones:
            bone.rotation_mode='XYZ'; bone.rotation_euler=(0,0,0); bone.location=(0,0,0)
        if name=='Idle':
            rig.pose.bones['Chest'].rotation_euler.x=.018*math.sin(phase)
            rig.pose.bones['Head'].rotation_euler.z=.025*math.sin(phase)
            rig.pose.bones['TailBase'].rotation_euler.z=.12*math.sin(phase)
        elif name=='Run':
            wave=math.sin(phase)
            for side,sign in [('L',1),('R',-1)]:
                rig.pose.bones['Thigh.'+side].rotation_euler.x=sign*.40*wave
                rig.pose.bones['Shin.'+side].rotation_euler.x=-.30*max(0,-sign*wave)
                rig.pose.bones['Foot.'+side].rotation_euler.x=-sign*.10*wave
            rig.pose.bones['Pelvis'].location.z=.006*(1-math.cos(phase*2))
            rig.pose.bones['Chest'].rotation_euler.x=.055
            rig.pose.bones['Chest'].rotation_euler.z=.025*wave
            rig.pose.bones['TailBase'].rotation_euler.z=.16*wave
        elif name=='Flight':
            lift=math.sin(math.pi*t)
            rig.pose.bones['Chest'].rotation_euler.x=-.20*lift
            rig.pose.bones['Head'].rotation_euler.x=-.15*lift
            for side in ['L','R']:
                rig.pose.bones['Thigh.'+side].rotation_euler.x=.60*lift
                rig.pose.bones['Shin.'+side].rotation_euler.x=-.85*lift
                rig.pose.bones['Foot.'+side].rotation_euler.x=.20*lift
            rig.pose.bones['TailBase'].rotation_euler.x=.3*lift
        else:
            # Windup, quick nod/lunge, then recover; all movement is local to the skin.
            amount=math.sin(math.pi*t)**3
            rig.pose.bones['Chest'].rotation_euler.x=.16*amount
            rig.pose.bones['Neck'].rotation_euler.x=.12*amount
            rig.pose.bones['Head'].rotation_euler.x=.32*amount
        for bone in rig.pose.bones:
            bone.keyframe_insert('rotation_euler',frame=frame,group=bone.name)
            bone.keyframe_insert('location',frame=frame,group=bone.name)
    action.use_fake_user=True
    # FBX exports each action as one take using its own frame range.
rig.animation_data.action=bpy.data.actions['Idle']
bpy.context.scene.render.fps=30
bpy.context.scene.frame_start=1; bpy.context.scene.frame_end=60
bpy.context.scene.frame_set(1)
source_directory=os.path.join(root,'docs/niulai-source')
os.makedirs(source_directory,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_directory,'NiuLai_Rigged.blend'))
mesh.select_set(True); rig.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(directory,'NiuLai_Rigged.fbx'),
    use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
    bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
report={'bones':len(data.bones),'vertices':len(mesh.data.vertices),
        'unweighted_vertices':sum(not v.groups for v in mesh.data.vertices),
        'actions':{a.name:list(a.frame_range) for a in bpy.data.actions}}
with open(os.path.join(directory,'rig-report.json'),'w') as f: json.dump(report,f,indent=2)
print(json.dumps(report))
