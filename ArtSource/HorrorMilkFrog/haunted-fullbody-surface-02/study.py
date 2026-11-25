from pathlib import Path
import bpy,numpy as np,sys

p=Path(__file__).resolve().parent
s=bpy.context.scene
h=next(o for o in s.objects if o.name.startswith('01 | Full hide'))
m=h.data;m.calc_loop_triangles()
keys=m.shape_keys.key_blocks
def points(key):
 a=np.empty(len(key.data)*3,'f');key.data.foreach_get('co',a)
 return a.reshape(-1,3)
updated='apply' in sys.argv
if not updated:
 tris=np.empty(len(m.loop_triangles)*3,'i');m.loop_triangles.foreach_get('vertices',tris)
 print('smooth polygons',sum(f.use_smooth for f in m.polygons),'of',len(m.polygons),flush=True)
 np.savez(p/'rest.npz',face=tris.reshape(-1,3),rest=points(keys[0]),laugh=points(keys[1]))
else:
 coords=np.load(p/'polished.npz')
 for key,name in zip(keys,['rest','laugh']):
  key.data.foreach_set('co',coords[name].astype('f').ravel())
 m.vertices.foreach_set('co',coords['rest'].astype('f').ravel())
 m.update()
 s.frame_set(1)
 bpy.context.preferences.filepaths.save_version=0
 bpy.ops.wm.save_as_mainfile(filepath=str(p/'Horror-Milk-Frog-02.blend'),compress=True)

# Same camera, single broad raking key and weak ambient lift on both versions.
clay=bpy.data.materials.new('Grey clay inspection');clay.use_nodes=True
bs=next(n for n in clay.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(.28,.28,.28,1)
bs.inputs['Roughness'].default_value=.6
s.view_layers[0].material_override=clay
for ob in s.objects:
 if ob.type=='LIGHT':
  ob.hide_render=not ob.name.startswith(('01','03'))
  ob.data.color=(1,1,1)
s.render.resolution_percentage=55;s.cycles.samples=14
for cam,frame,label in [('07-right',1,'flank'),('08-front-right',55,'laugh')]:
 s.camera=bpy.data.objects[cam];s.frame_set(frame)
 s.render.filepath=str(p/('%s-%s.png'%('after' if updated else 'before',label)))
 bpy.ops.render.render(write_still=True)
