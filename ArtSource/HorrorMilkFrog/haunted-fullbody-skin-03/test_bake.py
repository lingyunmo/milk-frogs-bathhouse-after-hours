from pathlib import Path
import bpy
p=Path(__file__).parent;dest=p/'qa';dest.mkdir(exist_ok=True)
s=bpy.context.scene
h=next(o for o in s.objects if o.name.startswith('01 | Full hide'))
for ob in s.objects:
 if ob.type=='MESH' and ob!=h:ob.hide_render=True
nt=h.data.materials[0].node_tree
em=nt.nodes.new('ShaderNodeEmission')
target=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL')
nt.links.new(em.outputs[0],target.inputs[0])
s.render.resolution_x=s.render.resolution_y=400;s.cycles.samples=1;s.cycles.use_denoising=False
for cam in sorted((o for o in s.objects if o.type=='CAMERA'),key=lambda o:o.name):
 s.camera=cam
 for tag,source in [('r','Unique skin colour'),('b','Editable 4K colour painting')]:
  nt.links.new(nt.nodes[source].outputs[0],em.inputs[0])
  s.render.filepath=str(dest/(cam.name+tag+'.png'));bpy.ops.render.render(write_still=True)
