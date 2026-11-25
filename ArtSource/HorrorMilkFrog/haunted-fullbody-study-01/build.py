from pathlib import Path
import bpy, numpy as np, math, sys
from math import pi,sin,cos,sqrt,exp
from mathutils import Vector,Quaternion
out=Path(__file__).resolve().parent
prior=out.parent/'haunted-head-study-01'
source=(prior/'sculpt.py').read_text(encoding='utf8')
data=np.load(out/'anatomy.npz')
tab=data['sampled']; env=data['envelope']; floor_px=368.0
def sample(table,v):
 v=max(0,min(len(table)-1.000001,v));j=int(v)
 return table[j]+(table[j+1]-table[j])*(v-j)
def shape(t):return sample(tab,(t-5)*4)
def pt(x,y,t):return (x/76,y/76,(139.52-t)/76)
def sprite_row(p):return 5+(p-22)*.76
def sph(a,t):
 rx,fr,bk=shape(t);mid=(fr+bk)*.5;ry=(bk-fr)*.5;m=1
 if t>=83:
  i=(a/pi*128+128)%256;j=int(i);rr=min(125.99999,(t-83)*2)
  k=int(rr);w=rr-k;v=i-j
  m=(1-w)*(env[k,j]*(1-v)+env[k,(j+1)%256]*v)+w*(env[k+1,j]*(1-v)+env[k+1,(j+1)%256]*v)
 return rx*sin(a)*m,mid-ry*cos(a)*m
def shoulder_weight(a,t):
 if t<=83:return 0
 i=(a/pi*128+128)%256;j=int(i);k=min(125.9999,(t-83)*2);l=int(k)
 table=upper_w
 return ((table[l,j]*(1-(i-j))+table[l,(j+1)%256]*(i-j))*(1-(k-l))+
  (table[l+1,j]*(1-(i-j))+table[l+1,(j+1)%256]*(i-j))*(k-l))
upper_w=data['upper_weights']
def clamp(x):return min(1,max(0,x))
def soft(t):
 t=clamp(t);return t*t*(3-2*t)

# Repaint a single continuous hide. The pale abdomen has a feathered pigment boundary;
# palms and soles darken gradually. The front patch never wraps onto the back.
detail=np.load(out/'grain.npy')
def pigment(x,y,t,a,hand):
 u=(a*4/pi)%8;i=round(u)%8; theta=i*pi/4
 px=int(round(190+x*cos(theta)+y*sin(theta)));py=int(round(t))
 grain=float(detail[i,max(0,min(373,py)),max(0,min(380,px))])
 wave=.013*sin(.048*x+.062*t)*sin(.087*y-.031*t)
 d=((t-210)/75)**2+(abs(a)/1.21)**2
 cream=soft((1.045+wave-d)/.16)*(1-hand)
 toe=soft((t-327)/30)
 umber=max(hand*soft((t-214+2*sin(y*.13))/30),toe)
 basal=np.array([.97,.93,.90])
 ivory=np.array([1.08,1.54,7.7])
 brown=np.array([.205,.193,.63])
 val=((basal*(1-cream)+ivory*cream)*(1-umber)+brown*umber)*(1+grain*.20)
 return tuple(val)
def interp(t, table):
 return [float(np.interp(t,table[:,0],table[:,i])) for i in range(1,table.shape[1])]

pose=np.array([(82,73,2,82,0),(105,82,4,105,1),(130,98,-2,128,5),
 (151,103,-8,151,8),(174,117,-29,170,19),(194,124,-65,181,37),
 (211,112,-87,194,51),(226,94,-104,204,60),(239,82,-115,207,64),
 (250,73,-122,211,70),(261,65,-128,213,74),(282,47,-133,218,77)])
arm_samples=data['arm_table']
def laugh_point(x,y,t,w):
 if w<=0 or t<86:return pt(x,y,t)
 side=-1 if x<0 else 1
 cx,cy=interp(min(t,256),arm_samples)[:2]
 nx,ny,nt,ang=interp(t,pose);ang=math.radians(ang)
 dx=abs(x)-cx;dy=y-cy
 xx=nx+dx*cos(ang)+dy*.32*sin(ang)
 yy=ny+dy*cos(ang*.65)-dx*.55*sin(ang)
 fan=soft((t-218)/33)
 tt=nt+dx*sin(ang)+fan*.76*dy
 xx-=fan*max(0,t-249)*.33
 if t>216:
  rx,fr,bk=shape(tt)
  front=(fr+bk)/2-(bk-fr)/2*sqrt(max(0,1-(xx/rx)**2))
  yy=min(yy,front-2)
 w*=soft((t-87)/38)
 return pt((1-w)*x+w*side*xx,(1-w)*y+w*yy,(1-w)*t+w*tt)

prefix=source[:source.index('# Profile in front-reference')]
prefix=prefix.replace('out=Path(__file__).resolve().parent','')
exec(prefix)
ramp=next(n for n in skin.node_tree.nodes if n.type=='VALTORGB').color_ramp
for stop,tint in zip(ramp.elements,[(.68,.357,.049,1),(.715,.385,.056,1),(.76,.409,.063,1)]):
 stop.color=tint
skin.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.62
def prof(p,k):
 t=sprite_row(p);rx,fr,bk=shape(t)
 return (0,rx/.76,(bk-fr)/1.52,(bk+fr)/1.52)[k]
original=source[source.index('half=1.016'):source.index('head=mesh(')]
original=original.replace('rad=prof(p,1); x=rad*sin(a); return x,prof(p,3)-prof(p,2)*cos(a)','x,y=sph(a,sprite_row(p));return x/.76,y/.76')
original=original.replace("out/'complexion.rgb'","prior/'complexion.rgb'")
original=original.replace('N=256;U=78;B=49','N=256;U=78;B=90')
original=original.replace('b+(199-b)*f','b+((22+(144-5)/.76)-b)*f')
original=original.replace('if L==0:cc=color(a,p)','if L==0:\n                    cc=color(a,p) if sprite_row(p)<125 else pigment(pts[0][0]*76,pts[0][1]*76,sprite_row(p),a,0)')
original=original.replace('lower=rows[0];fs.append(tuple(reversed(rows[-1])))','lower=rows[0];seam=rows[-1]')
original=original.replace('return (x/100,(y+d)/100,(199-p)/100)', '''# Carry the commissures around the cheeks so the smile reads in profile.
    outer=smooth((abs(a)-.45)/.62)*smooth((1.67-abs(a))/.44)
    smilezone=ga((p-114)/40,0)
    y+=outer*smilezone*22
    x*=1+.07*outer*smilezone
    return (x/100,(y+d)/100,(199-p)/100)''')
original=original.replace('pts.append(face(a,p,L))',"""v=face(a,p,L)
                if L and sprite_row(p)>86:
                    x,y,z=v; v=laugh_point(x*76,y*76,sprite_row(p),shoulder_weight(a,sprite_row(p)))
                pts.append(v)""")
original=original.replace('corner=100+2.3*sin(a)','corner=96+2.3*sin(a)').replace('top=corner+18.5','top=corner+22.5')
exec(original)

# Join the full-body scan to the continuous face surface. Angles are sorted in world
# space because the arms distort the nominal ellipse at the shoulder cut.
vbody=data['verts']; offset=len(vs);weights=data['hand_weight']
def theta(x,y,t):
 rx,fr,bk=shape(min(305,t))
 return math.atan2(x/max(1,rx),((fr+bk)/2-y)/max(1,(bk-fr)/2))
for (x,y,t),w in zip(vbody,weights):
 x=float(x);y=float(y);t=float(t);w=float(w)
 vs.append(pt(x,y,t));other.append(laugh_point(x,y,t,w))
 cs.append(pigment(x,y,t,theta(x,y,t),w))
fs.extend([tuple(f) for f in data['faces'][:,::-1]+offset])
border=np.where(abs(vbody[:,2]-146)<.0002)[0]
def loopangle(v):
 rx,fr,bk=shape(146);x,y,_=vs[v]
 return math.atan2(x*76,(fr+bk)/2-y*76)%(2*pi)
# Offset the body boundary a fraction vertically so transition faces have finite area.
for j in border:
 n=offset+j
 for store in (vs,other):
  x,y,z=store[n];store[n]=(x,y,z-.0015)
a=sorted(seam,key=loopangle);b=sorted((offset+int(j) for j in border),key=loopangle)
ia=ib=0
while ia<len(a) or ib<len(b):
 ax=loopangle(a[(ia+1)%len(a)])+(2*pi if ia+1>=len(a) else 0) if ia<len(a) else float('inf')
 bx=loopangle(b[(ib+1)%len(b)])+(2*pi if ib+1>=len(b) else 0) if ib<len(b) else float('inf')
 if ax<bx:
  fs.append((a[ia%len(a)],b[ib%len(b)],a[(ia+1)%len(a)]));ia+=1
 else:
  fs.append((a[ia%len(a)],b[ib%len(b)],b[(ib+1)%len(b)]));ib+=1
exec(source[source.index("head=mesh("):source.index("cams=[camera")].replace('width=(.066-.020*abs(q))','width=(.077-.023*abs(q))'))
# Diffuse the arm displacement over the actual connected surface to smooth the
# transition between the lifted elbow and the flank while keeping the lips and toes fixed.
edges=np.empty(len(head.data.edges)*2,dtype=np.int32);head.data.edges.foreach_get('vertices',edges)
edge=edges.reshape(-1,2)
j=np.concatenate([edge[:,0],edge[:,1]]);k=np.concatenate([edge[:,1],edge[:,0]])
degree=np.maximum(1,np.bincount(j,minlength=len(vs)))
neutral=np.asarray(vs);raised=np.empty(len(vs)*3,dtype=np.float32)
laugh_key=head.data.shape_keys.key_blocks[1]
laugh_key.data.foreach_get('co',raised);delta=raised.reshape(-1,3).astype(np.float64)-neutral
height=139.52-neutral[:,2]*76
mask=(np.clip((height-99)/24,0,1)*np.clip((282-height)/25,0,1)*np.clip((abs(neutral[:,0])*76-55)/24,0,1))[:,None]
for step in range(36):
 mean=np.stack([np.bincount(j,weights=delta[k,d],minlength=len(vs))/degree for d in range(3)],axis=1)
 delta+=mask*.63*(mean-delta)
laugh_key.data.foreach_set('co',(neutral+delta).astype(np.float32).ravel())
head.name='01 | Full hide, sculpted shoulders, palms, toes and oral rim'
skincol.name='01 • Character | complete sculpt'
def light(name,pos,power,size,color):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
 o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=pos;point(o,(0,0,2.15))
light('01 • Large softbox',(-3.8,-5.3,8.4),750,5,(1,.96,.88))
light('02 • Face fill',(4,-4,5.1),380,5,(.87,.92,1))
light('03 • Shoulder rim',(1.7,4,7.5),650,4.6,(1,.92,.79))
light('05 • Ground return',(0,-5.4,1.25),195,5.8,(1,.89,.77))
light('06 • Flank bounce',(5.7,2.3,1.5),175,6,(1,.88,.73))
light('04 • Back bounce',(-4,4.6,3.3),330,5.4,(.86,.9,1))
exec(source[source.index('world=bpy.data.worlds'):source.index("turn=camera(")])
pivot.name='Milk Frog | full character';pivot.location.z=(floor_px-139.52)/76
names=['01-front','02-front-left','03-left','04-back-left','05-back','06-back-right','07-right','08-front-right']
cams=[]
for i,name in enumerate(names):
 a=i*pi/4
 cams.append(camera(name,(12*sin(a),-12*cos(a),3.18),(0,0,2.36),5.28))
sc.camera=cams[0];sc.frame_set(1)
for imname in range(8):
 im=bpy.data.images.load(str(out/f'ref{imname}.png'));im.name='Reference | '+names[imname];im.pack();im.use_fake_user=True
for nm in ['grin-reference.png','laugh-reference.png']:
 im=bpy.data.images.load(str(prior/nm));im.pack();im.use_fake_user=True
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   sp=area.spaces.active;sp.region_3d.view_perspective='CAMERA';sp.region_3d.view_camera_zoom=12
   sp.overlay.show_overlays=False;sp.shading.type='MATERIAL';sp.shading.use_scene_world=True;sp.shading.use_scene_lights=True
bpy.context.view_layer.objects.active=head
head.select_set(True)
sc.render.resolution_x=sc.render.resolution_y=1200
sc.render.resolution_percentage=100;sc.cycles.samples=32
sc.render.filepath='//views/01-front.png'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Horror-Milk-Frog-Full-Body.blend'),compress=True)
