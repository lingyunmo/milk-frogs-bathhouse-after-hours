from pathlib import Path
import bpy,numpy as np
from bpy_extras.object_utils import world_to_camera_view
p=Path(__file__).resolve().parent;s=bpy.context.scene;s.frame_set(1)
h=next(o for o in s.objects if o.name.startswith('01 | Full hide'));m=h.data
def field(seq,name,k):
 a=np.empty(len(seq)*k,'f');seq.foreach_get(name,a);return a.reshape(-1,k)
v=field(m.vertices,'co',3);norm=field(m.vertices,'normal',3)
world=np.column_stack([v,np.ones(len(v))])@np.array(h.matrix_world).T
views=[];rays=[]
for cam in sorted((o for o in s.objects if o.type=='CAMERA'),key=lambda o:o.name):
 camera=world@np.array(cam.matrix_world.inverted()).T
 scale=cam.data.ortho_scale
 views.append(np.column_stack([.5+camera[:,0]/scale,.5-camera[:,1]/scale,camera[:,2]]))
 rays.append(np.array(cam.matrix_world)[:3,2])
edge=np.empty(len(m.edges)*2,'i');m.edges.foreach_get('vertices',edge)
loops=np.empty(len(m.loops),'i');m.loops.foreach_get('edge_index',loops)
rim=np.unique(edge.reshape(-1,2)[np.bincount(loops,minlength=len(m.edges))==1])
np.savez_compressed(p/'capture.npz',v=v,norm=norm,views=views,rays=rays,rim=rim,ink=field(m.color_attributes['Complexion'].data,'color',4))
