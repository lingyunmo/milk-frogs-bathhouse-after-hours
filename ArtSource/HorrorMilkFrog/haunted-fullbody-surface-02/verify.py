from pathlib import Path
import bpy,numpy as np
p=Path(bpy.data.filepath).parent
s=bpy.context.scene;h=next(o for o in s.objects if o.name.startswith('01 | Full hide'));m=h.data
new=np.load(p/'polished.npz');old=np.load(p/'rest.npz')
for i,n in enumerate(['rest','laugh']):
 a=np.empty(new[n].size,'f');m.shape_keys.key_blocks[i].data.foreach_get('co',a)
 assert np.array_equal(a.reshape(-1,3),new[n]);assert np.isfinite(a).all()
for fr in (1,32,43,55,72,86,110):
 s.frame_set(fr);v=m.shape_keys.key_blocks[1].value
 assert all(abs(o.data.shape_keys.key_blocks[1].value-v)<.001 for o in s.objects if o.type=='MESH')
 dg=bpy.context.evaluated_depsgraph_get()
 co=np.empty(m.shape_keys.key_blocks[0].data.__len__()*3,'f')
 h.evaluated_get(dg).data.vertices.foreach_get('co',co)
 assert np.allclose(co.reshape(-1,3),new['rest']*(1-v)+new['laugh']*v,atol=2e-5)
assert not s.view_layers[0].material_override and not any(o.hide_render for o in s.objects if o.type=='LIGHT')
print('Checked saved mesh, seven expression frames, original materials and studio lighting.')
