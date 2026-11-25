from pathlib import Path
import bpy
p=Path(bpy.data.filepath).parent
s=bpy.context.scene
assert s.camera.name.startswith('Turntable')
head=next(o for o in s.objects if o.name.startswith('Head |'))
s.frame_set(55);assert head.data.shape_keys.key_blocks[1].value>.99
for o in s.objects:
 if o.type=='MESH' and o!=head:
  assert o.parent and o.data.shape_keys.key_blocks[1].value>.99,o.name
assert all(im.packed_file for im in bpy.data.images if im.name.endswith('reference.png'))
print('VERIFIED saved file, packed references, linked pose keys:',sum(o.type=='MESH' for o in s.objects),flush=True)
s.cycles.samples=10;s.render.resolution_percentage=60
dest=p/'frames';dest.mkdir(exist_ok=True)
for fr in range(1,111,3):
 s.frame_set(fr);s.render.filepath=str(dest/('%03d.png'%fr));bpy.ops.render.render(write_still=True)
