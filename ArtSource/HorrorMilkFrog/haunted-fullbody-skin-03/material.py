import bpy,numpy as np,sys
from pathlib import Path
p=Path(__file__).resolve().parent;s=bpy.context.scene;s.frame_set(1)
h=next(o for o in s.objects if o.name.startswith('01 | Full hide'));m=h.data
data=np.load(p/'paint.npz');cap=np.load(p/'capture.npz')

def rgba(name,c):
 a=m.color_attributes.new(name=name,type='FLOAT_COLOR',domain='POINT')
 a.data.foreach_set('color',np.column_stack([c,np.ones(len(c))]).astype('f').ravel())
rgba('Paint | reference pigments',data['albedo'])
normal=np.abs(cap['norm'])**5;normal/=np.maximum(.00001,normal.sum(axis=1,keepdims=True))
rgba('Paint | anatomical projection',normal)
rgba('Paint | tissue response',np.column_stack([data['rough'],data['belly'],data['pads']]))
rgba('Paint | flexion',np.repeat(data['crease'][:,None],3,axis=1))

loops=np.empty(len(m.loops),'i');m.loops.foreach_get('vertex_index',loops)
v=cap['v'];uv=m.uv_layers.new(name='Skin atlas');uv.data.foreach_set('uv',np.load(p/'paint-uv.npy'))
names=['Hide sagittal','Hide frontal','Hide horizontal']
for i,axes in enumerate([(1,2),(0,2),(0,1)]):
 layer=m.uv_layers.new(name=names[i],do_init=False)
 a=[.31,-.17,.81][i];rot=np.array([[np.cos(a),-np.sin(a)],[np.sin(a),np.cos(a)]])
 coords=v[:,axes]@rot/1.68+np.array([[.33,-.64],[-.71,.49],[.15,.87]])[i]
 layer.data.foreach_set('uv',coords[loops].astype('f').ravel())
m.uv_layers.active_index=0;m.uv_layers['Skin atlas'].active_render=True

mat=bpy.data.materials.new('Hide 03 | reference-painted golden amphibian');mat.use_nodes=True
h.data.materials[0]=mat
n=mat.node_tree.nodes;l=mat.node_tree.links;n.clear()
def node(type,name,x,y):
 q=n.new(type);q.name=name;q.label=name;q.location=(x,y);return q
def plug(sock,value):
 if isinstance(value,bpy.types.NodeSocket):l.new(value,sock)
 else:sock.default_value=value
def op(name,a,b,x,y,label):
 q=node('ShaderNodeMath',label,x,y);q.operation=name;plug(q.inputs[0],a);plug(q.inputs[1],b);return q.outputs[0]
def vector(name,a,b,x,y,label):
 q=node('ShaderNodeVectorMath',label,x,y);q.operation=name;plug(q.inputs[0],a)
 plug(q.inputs['Scale'] if name=='SCALE' else q.inputs[1],b)
 return q.outputs[0]
def col(name,x,y):
 q=node('ShaderNodeVertexColor',name,x,y);q.layer_name=name;return q.outputs[0]
def split(out,label,x,y):
 q=node('ShaderNodeSeparateXYZ',label,x,y);plug(q.inputs[0],out);return q.outputs
tex=bpy.data.images.load(str(p/'Hide-material-channels.png'));tex.colorspace_settings.name='Non-Color';tex.pack()
weights=split(col('Paint | anatomical projection',-1550,180),'Bind-pose weights',-1330,180)
layers=[]
for i in range(3):
 y=700-i*245
 u=node('ShaderNodeUVMap',names[i],-1550,y);u.uv_map=names[i]
 im=node('ShaderNodeTexImage','Hide detail / '+str(i+1),-1290,y);im.image=tex;im.extension='MIRROR';im.interpolation='Linear'
 l.new(u.outputs['UV'],im.inputs[0])
 layers.append(vector('SCALE',im.outputs[0],weights[i],-990,y,'Anatomical blend '+str(i+1)))
raw=vector('ADD',vector('ADD',layers[0],layers[1],-780,710,'Combine planes'),layers[2],-575,710,'Continuous hide')
grain,relief,oil=split(raw,'Pigment · pores · oil',-360,700)
rough,belly,pads=split(col('Paint | tissue response',-750,110),'Regional response',-540,110)
strength=op('SUBTRACT',2.42,op('MULTIPLY',belly,.97,-335,210,'Softer belly'),-110,270,'Colour modulation')
gain=op('ADD',1.,op('MULTIPLY',op('SUBTRACT',grain,.5,-110,600,'Center pigment'),strength,110,545,'Fine pigment'),330,540,'Reflectance gain')
paint=vector('SCALE',col('Paint | reference pigments',85,725),gain,550,710,'Unique skin colour')
oilvar=op('MULTIPLY',op('SUBTRACT',oil,.5,-80,-65,'Center oil'),.68,125,-65,'Local moisture')
rough_out=op('ADD',rough,oilvar,345,-65,'Surface roughness')
inc=op('MULTIPLY',col('Paint | flexion',-355,-270),.32,-75,-250,'Knuckle grooves')
height=op('SUBTRACT',relief,inc,135,-260,'Creases and pores')
bump=node('ShaderNodeBump','Fine epidermal relief',550,-220);plug(bump.inputs['Height'],height)
bump.inputs['Distance'].default_value=.0065
plug(bump.inputs['Strength'],op('SUBTRACT',.44,op('MULTIPLY',belly,.19,120,-415,'Belly relief'),344,-406,'Pore strength'))
bs=node('ShaderNodeBsdfPrincipled','Ochre hide',815,550);plug(bs.inputs['Base Color'],paint);plug(bs.inputs['Roughness'],rough_out);plug(bs.inputs['Normal'],bump.outputs[0])
bs.inputs['Subsurface Weight'].default_value=.055;bs.inputs['Subsurface Scale'].default_value=.03
bs.inputs['Coat Weight'].default_value=.035;bs.inputs['Coat Roughness'].default_value=.40
out=node('ShaderNodeOutputMaterial','Skin finish',1100,550);plug(out.inputs['Surface'],bs.outputs[0])

# A thin amber collar at the gumline and a smoother enamel edge on each tooth.
for key in ('Upper tooth','Lower tooth'):
 teeth=[o for o in s.objects if o.name.startswith(key)]
 tooth=teeth[0].data.materials[0].copy();tooth.name=key+' | layered enamel'
 nt=tooth.node_tree
 B=next(q for q in nt.nodes if q.type=='BSDF_PRINCIPLED')
 uvw=nt.nodes.new('ShaderNodeTexCoord');sep=nt.nodes.new('ShaderNodeSeparateXYZ')
 nt.links.new(uvw.outputs['Generated'],sep.inputs[0]);r=nt.nodes.new('ShaderNodeValToRGB')
 pal=[(.34,.275,.173,1),(.73,.645,.485,1),(.47,.296,.134,1),(.19,.081,.029,1)]
 if key.startswith('Lower'):pal=pal[::-1]
 stops=r.color_ramp.elements;stops[0].color=pal[0];stops[1].color=pal[-1]
 stops.new(.34).color=pal[1];stops.new(.78).color=pal[2]
 nt.links.new(sep.outputs[2],r.inputs[0])
 streak=nt.nodes.new('ShaderNodeVectorMath');streak.operation='MULTIPLY';streak.inputs[1].default_value=(13,10,1.6)
 nt.links.new(uvw.outputs['Generated'],streak.inputs[0])
 noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2.8;nt.links.new(streak.outputs[0],noise.inputs[0])
 mix=nt.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.33
 nt.links.new(r.outputs[0],mix.inputs[1]);nt.links.new(noise.outputs[0],mix.inputs[2]);nt.links.new(mix.outputs[0],B.inputs['Base Color'])
 B.inputs['Roughness'].default_value=.33;B.inputs['Coat Weight'].default_value=.10
 for o in teeth:o.data.materials[0]=tooth

if (p/'Sulfur-iris.png').is_file():
 eye=bpy.data.materials.new('Eye 03 | sulfur nucleus and amber stroma');eye.use_nodes=True
 nt=eye.node_tree;B=next(q for q in nt.nodes if q.type=='BSDF_PRINCIPLED')
 iris=bpy.data.images.load(str(p/'Sulfur-iris.png'));iris.pack()
 im=nt.nodes.new('ShaderNodeTexImage');im.image=iris;im.extension='EXTEND'
 uv_eye=nt.nodes.new('ShaderNodeUVMap');uv_eye.uv_map='Iris paint';nt.links.new(uv_eye.outputs[0],im.inputs[0])
 nt.links.new(im.outputs[0],B.inputs['Base Color']);nt.links.new(im.outputs[0],B.inputs['Emission Color'])
 B.inputs['Emission Strength'].default_value=.33;B.inputs['Roughness'].default_value=.26
 B.inputs['Coat Weight'].default_value=.08;B.inputs['Coat Roughness'].default_value=.21
 B.inputs['Specular IOR Level'].default_value=.28
 for ob in s.objects:
  if not ob.name.startswith(('Bulb | sulfur glass','Scleral slit')):continue
  side=-1 if ob.name.endswith(' L') else 1
  mesh=ob.data;xyz=np.empty(len(mesh.vertices)*3,'f');mesh.vertices.foreach_get('co',xyz)
  pos=xyz.reshape(-1,3);dx=pos[:,0]-side*.383;dy=pos[:,2]-1.067-side*.37*dx
  texco=np.stack([.5+dx/.180,.5+dy/.180],axis=1)
  li=np.empty(len(mesh.loops),'i');mesh.loops.foreach_get('vertex_index',li)
  mesh.uv_layers.new(name='Iris paint').data.foreach_set('uv',texco[li].astype('f').ravel())
  for i in range(len(mesh.materials)):mesh.materials[i]=eye

if (p/'Oral-mucosa.png').is_file():exec((p/'oral.py').read_text(encoding='utf8'))

if 'bake' in sys.argv:
 bpy.ops.object.select_all(action='DESELECT');h.select_set(True);bpy.context.view_layer.objects.active=h
 m.uv_layers.active_index=m.uv_layers.find('Skin atlas')
 baked=bpy.data.images.new('Skin colour | 4096',width=4096,height=4096,alpha=False)
 baked.colorspace_settings.name='sRGB'
 dest=node('ShaderNodeTexImage','Editable 4K colour painting',550,980);dest.image=baked
 dest.select=True;n.active=dest
 em=node('ShaderNodeEmission','Albedo baking',815,900);plug(em.inputs['Color'],paint);plug(out.inputs['Surface'],em.outputs[0])
 s.render.engine='CYCLES';old_samples=s.cycles.samples;s.cycles.samples=1
 bpy.ops.object.bake(type='EMIT',margin=12,margin_type='EXTEND',uv_layer='Skin atlas',use_clear=True)
 s.cycles.samples=old_samples
 baked.filepath_raw=str(p/'Skin-albedo-4096.png');baked.file_format='PNG';baked.save();baked.pack()
 n.remove(em)
 atlas=node('ShaderNodeUVMap','Unique UV islands',310,980);atlas.uv_map='Skin atlas';plug(dest.inputs[0],atlas.outputs[0])
 plug(bs.inputs['Base Color'],dest.outputs[0]);plug(out.inputs['Surface'],bs.outputs[0])
 dest.extension='EXTEND'
 bpy.context.preferences.filepaths.save_version=0
 bpy.ops.wm.save_as_mainfile(filepath=str(p/'Horror-Milk-Frog-03.blend'),compress=True)
else:
 s.render.resolution_percentage=75;s.cycles.samples=18
 for name in ['01-front','08-front-right']:
  s.camera=bpy.data.objects[name];s.render.filepath=str(p/(name+'-look.png'));bpy.ops.render.render(write_still=True)
