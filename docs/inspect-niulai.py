import bpy, json
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='Assets/Game/Characters/NiuLai/Resources/NiuLai.fbx')
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
coords=[obj.matrix_world@v.co for v in obj.data.vertices]
lo=[min(v[i] for v in coords) for i in range(3)]
hi=[max(v[i] for v in coords) for i in range(3)]
print('BOUNDS',json.dumps([lo,hi]))
for index in range(10):
    part=[v for v in coords if lo[2]+(hi[2]-lo[2])*index/10 <= v.z < lo[2]+(hi[2]-lo[2])*(index+1)/10]
    if part: print('SLICE',index,[[round(min(v[i] for v in part),3),round(max(v[i] for v in part),3)] for i in range(3)])
