from pathlib import Path
import bpy
p=Path(__file__).parent
s=bpy.context.scene
h=next(o for o in s.objects if o.name.startswith('01 | Full hide'));m=h.data
print('uv',[(v.name,v.active_render) for v in m.uv_layers],flush=True)
nt=m.materials[0].node_tree
B=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
nt.links.new(nt.nodes['Unique skin colour'].outputs[0],B.inputs['Base Color'])
src=(p/'inspect.py').read_text('utf8');start=src.index('s.frame_set(1)\ndef render')
src=src[start:src.index('if not old:\n dest=')].replace('s.cycles.samples=52','s.cycles.samples=12').replace('resolution_y=1100','resolution_y=720')
cams=sorted((o for o in s.objects if o.type=='CAMERA'),key=lambda o:o.name);tag='unbaked';from mathutils import Vector
exec(src)
