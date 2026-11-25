import bpy,bmesh,numpy as np
from pathlib import Path
p=Path(bpy.data.filepath).parent;s=bpy.context.scene;h=next(o for o in s.objects if o.name.startswith('01 | Full hide'))
edges=np.empty(len(h.data.edges)*2,np.int32);h.data.edges.foreach_get('vertices',edges)
loops=np.empty(len(h.data.loops),np.int32);h.data.loops.foreach_get('edge_index',loops)
counts=np.bincount(loops,minlength=len(h.data.edges));v=np.empty(len(h.data.vertices)*3,np.float32);h.data.vertices.foreach_get('co',v);pts=v.reshape(-1,3)
bound=edges.reshape(-1,2)[counts==1];assert not (counts>2).any(); assert pts[bound,2].min()>0, 'open seam below mouth'
for fr,value in [(1,0),(55,1),(110,0)]:
 s.frame_set(fr)
 assert all(abs(o.data.shape_keys.key_blocks[1].value-value)<.001 for o in s.objects if o.type=='MESH')
print('CHECK',len(pts),'skin vertices;',len(bound),'lip rim edges; 8 cameras;',sum(im.packed_file is not None for im in bpy.data.images),'packed refs;',flush=True)
s.frame_set(55);s.render.filepath=str(p/'views/09-belly-laugh.png');bpy.ops.render.render(write_still=True)
