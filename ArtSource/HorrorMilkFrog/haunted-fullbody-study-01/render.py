from pathlib import Path
import sys,bpy
p=Path(bpy.data.filepath).parent;s=bpy.context.scene
draft='draft' in sys.argv
dest=p/('draft' if draft else 'views');dest.mkdir(exist_ok=True)
s.render.resolution_percentage=38 if draft else 100;s.cycles.samples=6 if draft else 32
cams=sorted((o for o in s.objects if o.type=='CAMERA'),key=lambda o:o.name)
assert len(cams)==8
assert len([i for i in bpy.data.images if i.packed_file])==10
h=next(o for o in s.objects if o.name.startswith('01 | Full hide'))
assert len(h.data.vertices)>100000
for c,frame,name in [(c,1,c.name) for c in cams]+[(cams[0],55,'09-belly-laugh')]:
 s.frame_set(frame);s.camera=c
 assert abs(h.data.shape_keys.key_blocks[1].value-(frame==55))<.001
 s.render.filepath=str(dest/(name+'.png'));bpy.ops.render.render(write_still=True)
