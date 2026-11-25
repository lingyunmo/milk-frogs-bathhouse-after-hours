from pathlib import Path
import bpy,sys,numpy as np
from mathutils import Vector
p=Path(__file__).resolve().parent;s=bpy.context.scene
old='old' in sys.argv;tag='before' if old else 'after'
s.frame_set(1);cams=sorted((o for o in s.objects if o.type=='CAMERA'),key=lambda o:o.name)
h=next(o for o in s.objects if o.name.startswith('01 | Full hide'))
if not old:
 mat=h.data.materials[0];bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 color=bs.inputs['Base Color'].links[0].from_node;assert color.image.packed_file and list(color.image.size)==[4096,4096]
 assert all(q.image.packed_file for q in mat.node_tree.nodes if q.type=='TEX_IMAGE')
 assert bpy.data.images['Sulfur-iris.png'].packed_file
 u=h.data.uv_layers['Skin atlas'];uv=np.empty(len(u.data)*2,'f');u.data.foreach_get('uv',uv)
 for fr in [1,43,55,86,110]:
  s.frame_set(fr);dg=bpy.context.evaluated_depsgraph_get()
  uv_fr=np.empty_like(uv);h.evaluated_get(dg).data.uv_layers['Skin atlas'].data.foreach_get('uv',uv_fr)
  assert np.array_equal(uv,uv_fr)
 print('Verified packed image nodes and bind-pose UVs across five expression frames',flush=True)
s.frame_set(1)
def render(path):
 s.render.filepath=str(path);bpy.ops.render.render(write_still=True)
s.cycles.samples=52;s.render.resolution_x=s.render.resolution_y=1100
for label,cam,at,extent in [('face',(2.6,-11.7,4.65),(0,-.47,3.98),2.25),('hand',(6,-11,3.10),(1.35,-.85,1.74),1.81)]:
 obj=cams[0].copy();obj.data=cams[0].data.copy();s.collection.objects.link(obj)
 obj.location=cam;obj.rotation_euler=(Vector(at)-obj.location).to_track_quat('-Z','Y').to_euler();obj.data.ortho_scale=extent
 s.camera=obj;render(p/(tag+'-'+label+'.png'))
if not old:
 dest=p/'views';dest.mkdir(exist_ok=True)
 s.render.resolution_x=s.render.resolution_y=1200;s.cycles.samples=40
 for c,fr,label in [(c,1,c.name) for c in cams]+[(cams[0],55,'09-belly-laugh')]:
  s.camera=c;s.frame_set(fr);render(dest/(label+'.png'))
