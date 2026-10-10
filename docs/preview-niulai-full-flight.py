import bpy,os,math,json
from mathutils import Vector
root=os.getcwd();out=os.path.join(root,'docs/niulai-source/folded-arm-full-flight');os.makedirs(out,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'docs/niulai-source/NiuLai_Rigged.blend'))
rig=bpy.data.objects['NiuLaiRig'];body=bpy.data.objects['NiuLaiBody'];rig.animation_data.action=None
a=bpy.data.actions.new('Takeoff_Flight_Landing');rig.animation_data.action=a
foot_ids=[v.index for v in body.data.vertices if v.co.z<.065 and abs(v.co.x)>.06 and v.co.y<.14]
def ease(t):t=max(0,min(1,t));return t*t*(3-2*t)
report=[]
for frame in range(1,91):
 if frame<=10:
  t=ease((frame-1)/9);tilt=.16*t;lift=0;crouch=.7*t
 elif frame<=26:
  t=ease((frame-10)/16);tilt=.16+1.24*t;lift=.8*t;crouch=.7*(1-t)
 elif frame<=42:tilt=1.4;lift=.8;crouch=0
 elif frame<=66:
  t=ease((frame-42)/24);tilt=1.4*(1-t);lift=.8*(1-t);crouch=.18*t
 elif frame<=74:
  t=ease((frame-66)/8);tilt=.12*t;lift=0;crouch=.18+.82*t
 else:
  t=ease((frame-74)/16);tilt=.12*(1-t);lift=0;crouch=1-t
 for b in rig.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
 rig.pose.bones['Root'].rotation_euler.x=tilt
 rig.pose.bones['Pelvis'].location.y=-.045*crouch
 rig.pose.bones['Chest'].rotation_euler.x=.13*crouch
 rig.pose.bones['Head'].rotation_euler.x=-.12*crouch-.15*lift/.8
 for side in ['L','R']:
  thigh=.48*crouch-.12*lift/.8;shin=-.85*crouch
  rig.pose.bones['Thigh.'+side].rotation_euler.x=thigh
  rig.pose.bones['Shin.'+side].rotation_euler.x=shin
  rig.pose.bones['Foot.'+side].rotation_euler.x=-thigh-shin
 bpy.context.view_layer.update()
 ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh()
 foot_min=min((ev.matrix_world@m.vertices[i].co).z for i in foot_ids);ev.to_mesh_clear()
 # Root local Y is world vertical: keep the feet at the flight height or on the floor.
 rig.pose.bones['Root'].location.y=lift-foot_min
 for b in rig.pose.bones:b.keyframe_insert('rotation_euler',frame=frame,group=b.name);b.keyframe_insert('location',frame=frame,group=b.name)
 report.append({'frame':frame,'height':lift,'crouch':crouch})
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=90;scene.render.fps=30
scene.camera.location=(2.6,-1.8,1.4);scene.camera.rotation_euler=(Vector((0,-.3,.65))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=2.1
scene.render.resolution_x=512;scene.render.resolution_y=512;scene.eevee.taa_render_samples=16
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'NiuLai_Takeoff_Flight_Landing.blend'))
for i in range(45):
 scene.frame_set(1+2*i);scene.render.filepath=os.path.join(out,f'frame-{i:02}.png');bpy.ops.render.render(write_still=True)
with open(os.path.join(out,'timing.json'),'w') as f:json.dump(report,f,indent=2)
