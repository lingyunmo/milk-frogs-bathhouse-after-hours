"""Rest-anchored detail for the oral surfaces and under-enamel shadows."""
import numpy as np
oral_scan=bpy.data.images.load(str(p/'Oral-mucosa.png'));oral_scan.pack()
for ob in list(s.objects):
 gum=ob.name in ('Upper gums','Lower gums')
 vault=ob.name.startswith('Open mouth |')
 tongue=ob.name.startswith('Tongue |')
 if not (gum or vault or tongue):continue
 mesh=ob.data;co=np.empty(len(mesh.vertices)*3,'f');mesh.vertices.foreach_get('co',co);co=co.reshape(-1,3)
 i=np.arange(len(co))
 if gum or vault:
  nx,ny=(91,9) if gum else (101,30)
  u=(i%nx)/(nx-1);v=(i//nx)/(ny-1)
  off=.37 if ob.name.startswith('Upper') else .07
  tile=np.stack([u*1.85+off,v*(.22 if gum else .78)+off],axis=1)
  if gum:
   teeth_count=14 if ob.name.startswith('Upper') else 13
   collar=.5+.5*np.cos(2*np.pi*((u-.5)*teeth_count/.89))
   val=.75+.21*collar*(1-v)-.12*v
   pigment=np.stack([val*.78+.12,val*.54+.02,val*.40+.03],axis=1)
  else:
   vault_depth=(np.sin(v*np.pi)**.72)*np.sin(u*np.pi)**.52
   val=.15+.41*(1-vault_depth)**1.4+.13*v**3
   pigment=np.stack([val*.75,val*.50,val*.57],axis=1)
 else:
  tile=np.stack([co[:,0]*1.3+.53,co[:,1]*1.6+.93],axis=1)
  val=.66-.31*np.exp(-(co[:,0]/.041)**2)
  pigment=np.stack([val*1.65,val*.75,val*.85],axis=1)
 indices=np.empty(len(mesh.loops),'i');mesh.loops.foreach_get('vertex_index',indices)
 mesh.uv_layers.new(name='Mucosa').data.foreach_set('uv',tile[indices].astype('f').ravel())
 a=mesh.color_attributes.new(name='Tissue depth',type='FLOAT_COLOR',domain='POINT')
 a.data.foreach_set('color',np.column_stack([pigment,np.ones(len(co))]).astype('f').ravel())
 mt=mesh.materials[0].copy();mt.name='Oral 03 | '+ob.name;mesh.materials[0]=mt
 nt=mt.node_tree;B=next(q for q in nt.nodes if q.type=='BSDF_PRINCIPLED')
 st=nt.nodes.new('ShaderNodeUVMap');st.uv_map='Mucosa'
 im=nt.nodes.new('ShaderNodeTexImage');im.image=oral_scan;im.extension='MIRROR';nt.links.new(st.outputs[0],im.inputs[0])
 attr=nt.nodes.new('ShaderNodeVertexColor');attr.layer_name='Tissue depth'
 product=nt.nodes.new('ShaderNodeVectorMath');product.operation='MULTIPLY';nt.links.new(attr.outputs[0],product.inputs[0]);nt.links.new(im.outputs[0],product.inputs[1])
 nt.links.new(product.outputs[0],B.inputs['Base Color'])
 bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.0027;bump.inputs['Strength'].default_value=.16
 nt.links.new(im.outputs[0],bump.inputs['Height']);nt.links.new(bump.outputs[0],B.inputs['Normal'])
 B.inputs['Roughness'].default_value=.40 if vault else .37
 B.inputs['Specular IOR Level'].default_value=.16 if vault else .28
 B.inputs['Subsurface Weight'].default_value=.055
