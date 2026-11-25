from pathlib import Path
import bpy,numpy as np
p=Path(__file__).resolve().parent
h=next(o for o in bpy.context.scene.objects if o.name.startswith('01 | Full hide'))
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);bpy.context.view_layer.objects.active=h
m=h.data;m.uv_layers.new(name='Hide paint')
before=np.empty(len(m.loops),'i');m.loops.foreach_get('vertex_index',before)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=.70,island_margin=.0035,area_weight=.35,correct_aspect=True,scale_to_bounds=True)
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.seams_from_islands(mark_seams=True,mark_sharp=False)
bpy.ops.uv.unwrap(method='CONFORMAL',margin=.0015)
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.pack_islands(margin_method='FRACTION',margin=.0025,rotate=True,shape_method='AABB')
bpy.ops.object.mode_set(mode='OBJECT')
a=np.empty(len(m.loops)*2,'f');m.uv_layers.active.data.foreach_get('uv',a)
after=np.empty(len(m.loops),'i');m.loops.foreach_get('vertex_index',after)
print('loop changes',np.mean(after!=before),flush=True)
np.save(p/'paint-uv.npy',a)
print('Packed UV bounds',a.min(),a.max(),flush=True)
