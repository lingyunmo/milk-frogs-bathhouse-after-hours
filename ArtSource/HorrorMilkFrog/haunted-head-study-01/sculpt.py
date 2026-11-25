import bpy, math, random, os, sys
from pathlib import Path
from mathutils import Vector
from math import sin,cos,exp,sqrt,pi
out=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene
def col(name):
    c=bpy.data.collections.new(name);sc.collection.children.link(c);return c
skincol=col('01 • Sculpt | cranial surface')
eyecol=col('02 • Eyes | orbital insets')
mouthcol=col('03 • Oral cavity | teeth and gums')
studio=col('04 • Studio | cameras and lamps')
refs=col('05 • Reference | front and inferred profile')
def move(o,c):
    for k in list(o.users_collection): k.objects.unlink(o)
    c.objects.link(o)
def smooth(t): t=max(0,min(1,t));return t*t*(3-2*t)
def ga(a,b):return exp(-min(80,a*a+b*b))
def material(name,palette,rough=.46,sss=0,grain=.12,scale=135,emit=None):
    m=bpy.data.materials.new(name);m.diffuse_color=(*palette[-1],1);m.use_nodes=True
    nt=m.node_tree;n=nt.nodes;l=nt.links;bs=n.get('Principled BSDF')
    bs.inputs['Roughness'].default_value=rough;bs.inputs['Subsurface Weight'].default_value=sss
    bs.inputs['Subsurface Scale'].default_value=.035
    coord=n.new('ShaderNodeTexCoord')
    noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=6.2;noise.inputs['Detail'].default_value=4
    l.new(coord.outputs['Object'],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB');r=ramp.color_ramp;r.elements[0].position=.18;r.elements[0].color=(*palette[0],1)
    r.elements[1].position=.83;r.elements[1].color=(*palette[-1],1)
    if len(palette)==3:r.elements.new(.5).color=(*palette[1],1)
    l.new(noise.outputs['Fac'],ramp.inputs['Fac'])
    if name.startswith('S01'):
        attr=n.new('ShaderNodeVertexColor');attr.layer_name='Complexion'
        mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
        l.new(ramp.outputs['Color'],mix.inputs[1]);l.new(attr.outputs['Color'],mix.inputs[2]);l.new(mix.outputs[0],bs.inputs['Base Color'])
    else:l.new(ramp.outputs[0],bs.inputs['Base Color'])
    micro=n.new('ShaderNodeTexNoise');micro.inputs['Scale'].default_value=scale;micro.inputs['Detail'].default_value=2.5
    l.new(coord.outputs['Object'],micro.inputs['Vector'])
    bump=n.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.004;bump.inputs['Strength'].default_value=grain
    l.new(micro.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],bs.inputs['Normal'])
    if emit:bs.inputs['Emission Color'].default_value=(*emit,1);bs.inputs['Emission Strength'].default_value=.9
    return m
skin=material('S01 | ochre hide · fine pores',[(.49,.199,.014),(.83,.443,.036),(.95,.542,.079)],.53,.064,.21)
gum=material('M01 | umber gingiva',[(.074,.011,.009),(.25,.061,.042)],.32,.11)
inner=material('M02 | oral mucosa',[(.011,.002,.004),(.07,.011,.017)],.38,.08)
tongue=material('M03 | dorsum',[(.103,.022,.029),(.30,.073,.068)],.38,.12,.23)
tooth=material('M04 | old ivory',[(.26,.145,.065),(.61,.48,.30),(.84,.72,.52)],.35,.07,.05)
socket=material('E01 | deep orbital umber',[(.004,.0027,.0018),(.020,.013,.004)],.66)
iris=material('E02 | sulfur iris',[(.34,.36,.013),(.71,.78,.09)],.28,.025,.04,emit=(.16,.18,.008))
pupil=material('E03 | pupil',[(.015,.018,.003),(.065,.065,.008)],.24)
def mesh(name,vs,fs,mat,c,colors=None,other=None):
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.materials.append(mat);me.update()
    o=bpy.data.objects.new(name,me);c.objects.link(o)
    for f in me.polygons:f.use_smooth=True
    if colors:
        attr=me.color_attributes.new(name='Complexion',type='FLOAT_COLOR',domain='POINT')
        attr.data.foreach_set('color',[v for color in colors for v in (*color,1)])
    if other:
        o.shape_key_add(name='Grin');k=o.shape_key_add(name='Belly laugh');k.data.foreach_set('co',[c for v in other for c in v])
        if name.startswith('Head'):
            for frame,value in [(1,0),(32,0),(55,1),(72,.85),(86,1),(110,0)]:
                k.value=value;k.keyframe_insert(data_path='value',frame=frame)
        else:
            fc=k.driver_add('value');v=fc.driver.variables.new();v.name='laugh';v.type='SINGLE_PROP'
            v.targets[0].id_type='KEY';v.targets[0].id=head.data.shape_keys;v.targets[0].data_path='key_blocks["Belly laugh"].value'
            fc.driver.expression='laugh'
    return o
# Profile in front-reference coordinates: image height, silhouette radius, sagittal radius, axis offset.
profile=[(22,.4,.5,9),(24,13,14,9),(29,26,27,8),(39,43,41,7),(55,58,52,5),(74,71,61,3),(93,81,67,1),(110,88,71,0),(132,96,74,1),(152,103,73,4),(170,106,69,8),(182,101,65,11),(191,92,60,14),(199,77,52,15)]
def prof(p,k):
    if p<=22:return profile[0][k]
    for i in range(1,len(profile)):
        if p<=profile[i][0]:
            a,b=profile[i-1],profile[i];t=(p-a[0])/(b[0]-a[0])
            m=(b[k]-profile[max(i-2,0)][k])/(b[0]-profile[max(i-2,0)][0])
            n=(profile[min(i+1,len(profile)-1)][k]-a[k])/(profile[min(i+1,len(profile)-1)][0]-a[0])
            return (2*t**3-3*t*t+1)*a[k]+(t**3-2*t*t+t)*(b[0]-a[0])*m+(-2*t**3+3*t*t)*b[k]+(t**3-t*t)*(b[0]-a[0])*n
    return profile[-1][k]
half=1.016
def lips(a,laugh):
    t=min(1,abs(a)/half)
    corner=100+2.3*sin(a)
    top=corner+18.5*(1-t*t)-laugh*(5+2*(1-t*t))
    bot=top+39*(max(0,1-t*t)**.77)+laugh*22*(max(0,1-t*t)**.72)
    return top,bot
def rest(a,p):
    rad=prof(p,1); x=rad*sin(a); return x,prof(p,3)-prof(p,2)*cos(a)
def face(a,p,L):
    x,y=rest(a,p); ax=abs(x);side=1 if x>=0 else -1
    front=smooth((cos(a)-.12)/.55)
    ey=93.0-.37*(ax-37)-2*L
    # Deep slanted sockets; a continuous supraorbital shelf carved from the head.
    recess=14*ga((ax-38)/21,(p-ey)/13)
    brow=15*ga((ax-39)/22,(p-ey+13)/6.8)+6*ga((ax-22)/19,(p-ey+20)/16)
    cheek=12*ga((ax-67)/22,(p-113+L*7)/19)
    up,low=lips(a,L);corner=ga((abs(a)-half)/.115,(p-up)/7.5)
    zone=smooth((half+.10-abs(a))/.17)
    ridge=zone*(6.3*exp(-((p-up+1.7)/4.7)**2)+8.0*exp(-((p-low-3)/6.3)**2))
    groove=zone*3.2*exp(-((p-low-12)/3.9)**2)
    chin=10*ga(x/51,(p-169-7*L)/13)
    # Curved philtral/brow furrows and the subtle crease travelling away from each commissure.
    browcrease=2.0*ga((ax-19-.30*(88-p))/2.1,(p-81)/7)
    foldx=70+.29*(p-106)-.0032*(p-106)**2
    fold=2.6*ga((ax-foldx)/2.6,(p-115)/16)
    sidebulk=3.1*ga((ax-73)/20,(p-137)/22)
    d=front*(browcrease+fold-sidebulk+recess-brow-cheek-ridge+groove-chin+8*corner)
    return (x/100,(y+d)/100,(199-p)/100)
paint=(out/'complexion.rgb').read_bytes()
def color(a,p,L=0):
    x,_=rest(a,p);ax=abs(x);f=smooth((cos(a)-.15)/.48);ey=93-.37*(ax-37)
    shadow=f*exp(-((ax-38)/21)**4-((p-ey)/12.7)**4)*.99
    up,lo=lips(a,0);rim=f*smooth((half+.08-abs(a))/.13)*max(exp(-((p-up)/3.6)**2),exp(-((p-lo)/4.5)**2))*.33
    base=(max(.026,1-.963*shadow-rim*.7),max(.018,1-.986*shadow-rim),max(.017,1-.982*shadow-rim*.72))
    # Broad pigment and socket patina taken from the original; mask the eyeballs and the mouth aperture.
    px=round(x+268)-158; py=round(p)-15
    if not (0<=px<222 and 0<=py<184):return base
    wt=f*smooth((78-ax)/15)*smooth((179-p)/16)*smooth((p-30)/12)
    k=(py*222+px)*3; rgb=paint[k:k+3]
    if abs(a)<half and up-3<p<lo+2:wt=0
    if ((ax-38)/11)**2+((p-ey)/9)**2<1: wt=0
    pigments=[max(.05,min(1.12,(v/ref)**1.7)) for v,ref in zip(rgb,[240,182,74])]
    return tuple(base[i]*(1-wt*.68+wt*.68*pigments[i]) for i in range(3))
def polys(n,m):return [(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(m-1) for i in range(n)]
N=256;U=78;B=49;angs=[(i-N//2)*2*pi/N for i in range(N)]
# Corner sectors are explicit vertices. The split ring inside the grin is an actual aperture.
half=angs[N//2+41]
vs=[];other=[];cs=[];fs=[];upper=[];lower=[]
for upperpart,count in [(True,U),(False,B)]:
    rows=[]
    for j in range(count):
        row=[];f=j/(count-1)
        for i,a in enumerate(angs):
            if not upperpart and j==0 and abs(a)>=half-1.e-10:
                row.append(upper[i]);continue
            pts=[]
            for L in [0,1]:
                u,b=lips(a,L)
                # converge to same mid-head seam away from animated face
                if abs(a)>half:
                    fade=smooth((abs(a)-half)/.45); u=u*(1-fade)+104*fade;b=u
                p=22+(u-22)*f if upperpart else b+(199-b)*f
                pts.append(face(a,p,L))
                if L==0:cc=color(a,p)
            row.append(len(vs));vs.append(pts[0]);other.append(pts[1]);cs.append(cc)
        rows.append(row)
    for ra,rb in zip(rows,rows[1:]):
        fs.extend([(ra[i],rb[i],rb[(i+1)%N],ra[(i+1)%N]) for i in range(N)])
    if upperpart:upper=rows[-1];fs.append(tuple(rows[0]))
    else:lower=rows[0];fs.append(tuple(reversed(rows[-1])))
head=mesh('Head | sculpted hide and integral lips',vs,fs,skin,skincol,cs,other)
def oral(a,t,L,depth=0):
    u,b=lips(a,L); A=face(a,u,L);Bv=face(a,b,L)
    return tuple(A[k]*(1-t)+Bv[k]*t+(depth if k==1 else 0) for k in range(3))
def patch(name,fn,n,m,mat,c):
    uv=[(i/(n-1),j/(m-1)) for j in range(m) for i in range(n)]
    fs=[(j*n+i,j*n+i+1,(j+1)*n+i+1,(j+1)*n+i) for j in range(m-1) for i in range(n-1)]
    return mesh(name,[fn(u,v,0) for u,v in uv],fs,mat,c,other=[fn(u,v,1) for u,v in uv])
def cavity(u,t,L):
    a=(u*2-1)*half
    return oral(a,t,L,(sin(pi*t)**.64)*.27*(max(0,1-(a/half)**2)**.7)+.002)
patch('Open mouth | vaulted palate and throat',cavity,101,30,inner,mouthcol)
for bottom in (False,True):
    def gums(u,v,L):
        a=(u*2-1)*half;t=.135*v if not bottom else 1-.14*v
        return oral(a,t,L,.008+.024*v-.012*sin(pi*v))
    patch('Lower gums' if bottom else 'Upper gums',gums,91,9,gum,mouthcol)
def oval(name,fn,mat,c,n=32,m=20):
    pts=[(j/(m-1),2*pi*i/n) for j in range(m) for i in range(n)]
    return mesh(name,[fn(t,a,0) for t,a in pts],polys(n,m),mat,c,other=[fn(t,a,1) for t,a in pts])
rng=random.Random(230)
for bot,num in [(False,14),(True,13)]:
    for i in range(num):
        q=(i-(num-1)/2)/((num-1)/2);a=.89*q+rng.uniform(-.008,.008)
        width=(.066-.020*abs(q))*rng.uniform(.93,1.08);length=(.157-.059*abs(q))*rng.uniform(.82,1.14)
        if i in (2,num-3):length*=1.20
        cant=rng.uniform(-.22,.22);depth=width*.71
        def crown(t,th,L):
            cen=oral(a,.978 if bot else .018,L,.066)
            radial=sin(pi*t)**.57*(1.08-.18*t+.055*cos(2*th));s=radial*width*cos(th)
            return (cen[0]+s*cos(a)+cant*length*t,cen[1]+s*sin(a)+radial*depth*sin(th)-.015*sin(pi*t),cen[2]+(1 if bot else -1)*(t-.18)*length)
        oval(('Lower' if bot else 'Upper')+' tooth %02d'%(i+1),crown,tooth,mouthcol,22,17)
def ling(t,ang,L):
    # Visible low in the laugh, behind the teeth. Shallow medial furrow.
    r=sin(pi*t);x=.445*r*cos(ang);y=.13*r*sin(ang)
    center=oral(0,.82,L,.19)
    return (x,center[1]+y+.10*(x/.445)**2,center[2]+.054*cos(pi*t)-.013*exp(-(x/.052)**2))
oval('Tongue | median sulcus',ling,tongue,mouthcol,48,22)
for side in [-1,1]:
    suffix=' L' if side<0 else ' R'
    def eyePosition(x,p,L):
        a=math.asin(max(-.99,min(.99,x/prof(p,1))));return face(a,p,L)
    def almond(u,v,L):
        h=1-.91*L;s=2*u-1;x=side*(38+17.8*s);mid=93-.37*(abs(x)-37)-2*L
        bulge=max(0,1-s*s);p=mid+(2*v-1)*7.9*h*bulge**.63
        xx,y,z=eyePosition(x,p,L)
        return xx,y-.001-.025*bulge*sin(pi*v)*h,z
    patch('Scleral slit'+suffix,almond,57,22,socket,eyecol)
    def eyeDisc(name,radius,offset,mat):
        def fn(t,th,L):
            h=1-.91*L; dx=radius*t*cos(th);dy=radius*t*sin(th)*h
            x=side*38+dx;p=92.5-2*L+dy-.37*side*dx
            xx,y,z=eyePosition(x,p,L);return xx,y-offset-.030*h-.008*(1-t*t),z
        oval(name+suffix,fn,mat,eyecol,48,15)
    def bulb(t,ang,L):
        h=1-.94*L;phi=pi*t
        x=side*38.3+4.7*sin(phi)*cos(ang)
        p=92.3-2*L-4.9*sin(phi)*sin(ang)*h
        xx,y,z=eyePosition(x,p,L)
        return xx,y-.034*h-.005-.048*cos(phi)*h,z
    eye=oval('Bulb | sulfur glass'+suffix,bulb,iris,eyecol,44,33)
    core=material('E04 | warm luminous core'+suffix,[(.91,.81,.19),(1,.97,.5)],.18,.02,.03,emit=(1,.89,.31))
    eye.data.materials.append(core)
    for poly in eye.data.polygons:
        if poly.index//44<7:poly.material_index=1
    for upperlid in [True,False]:
        def lids(u,v,L):
            s=2*u-1;x=side*(38+18.6*s);b=max(0,1-s*s);sgn=-1 if upperlid else 1
            p=93-.37*(abs(x)-37)-2*L+sgn*(7.9*(1-.91*L)*b**.63+v*(3 if upperlid else 2)*b)
            xx,y,z=eyePosition(x,p,L);return xx,y-.003-.012*b*sin(v*pi),z
        # Integral dark rim around the eyeball.
        lid=patch(('Upper lid margin' if upperlid else 'Lower lid margin')+suffix,lids,48,6,skin,eyecol)

        tint=lid.data.color_attributes.new(name='Complexion',type='FLOAT_COLOR',domain='POINT')
        tint.data.foreach_set('color',[z for i in range(len(tint.data)) for z in (.23,.17,.12,1)])
# The cheek and cranium rise together through the laugh; the neck stays seated.
for coll in (skincol,eyecol,mouthcol):
    for obj in coll.objects:
        key=obj.data.shape_keys.key_blocks.get('Belly laugh')
        for v in key.data:
            x,y,z=v.co;influence=smooth((z-.0)/1.12);a=-.175*influence
            v.co.y= .08+(y-.08)*cos(a)-(z-.10)*sin(a)
            v.co.z= .10+(y-.08)*sin(a)+(z-.10)*cos(a)
# Normalise outward normals of closed components; skin winds outward by construction.
def point(o,at):o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
def camera(name,pos,at,scale):
    dat=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,dat);studio.objects.link(o);o.location=pos;point(o,at);dat.type='ORTHO';dat.ortho_scale=scale;return o
cams=[camera('Front • grin',(0,-5.4,1.04),(0,0,.9),2.52),camera('Three quarter',(3.55,-3.55,1.25),(0,0,.90),2.55),camera('Profile',(5.5,-.02,1.10),(0,0,.90),2.54)]
def light(name,pos,pow,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=pow;d.shape='DISK';d.size=size;d.color=color;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=pos;point(o,(0,0,.9))
light('Softbox • upper left',(-3,-4,5),390,3.6,(1,.94,.84))
light('Fill • cool right',(3,-2,2),145,3.3,(.8,.89,1))
light('Edge • rear',(1,2.8,3.7),290,2.6,(1,.84,.65))
world=bpy.data.worlds.new('Graphite studio');sc.world=world;world.use_nodes=True
bg=world.node_tree.nodes.get('Background');bg.inputs[0].default_value=(.17,.17,.17,1);bg.inputs[1].default_value=.35
sc.render.engine='CYCLES';sc.cycles.samples=20;sc.cycles.use_denoising=True
sc.render.resolution_x=sc.render.resolution_y=1024;sc.render.resolution_percentage=100
sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA'
sc.view_settings.view_transform='Khronos PBR Neutral';sc.frame_end=110;sc.render.fps=24
for fr,lab in [(1,'Resting grin'),(32,'Inhale'),(55,'Belly laugh'),(86,'Second beat'),(110,'Settle')]:sc.timeline_markers.new(lab,frame=fr)
pivot=bpy.data.objects.new('Head master | rotate or move the entire sculpt',None);skincol.objects.link(pivot)
pivot.empty_display_type='PLAIN_AXES';pivot.empty_display_size=.18
for collection in [skincol,eyecol,mouthcol]:
    for obj in collection.objects:
        if obj.type=='MESH':obj.parent=pivot
turn=camera('Turntable | playback', (0,-5.4,1.04),(0,0,.90),2.55)
for frame,deg in [(1,0),(32,45),(55,55),(72,22),(86,0),(110,0)]:
    ang=math.radians(deg);turn.location=(5.4*sin(ang),-5.4*cos(ang),1.06+.16*sin(ang));point(turn,(0,0,.9))
    turn.keyframe_insert(data_path='location',frame=frame);turn.keyframe_insert(data_path='rotation_euler',frame=frame)
sc.camera=turn;sc.frame_set(1)
for o in bpy.context.selected_objects:o.select_set(False)
head.select_set(True);bpy.context.view_layer.objects.active=head
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.region_3d.view_perspective='CAMERA';sp.overlay.show_overlays=False;sp.shading.color_type='MATERIAL';sp.shading.type='MATERIAL';sp.shading.use_scene_world=True;sp.shading.use_scene_lights=True
for file in ['grin-reference.png','laugh-reference.png']:
    im=bpy.data.images.load(str(out/file));im.pack();im.use_fake_user=True
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Horror-Milk-Frog-Head.blend'))
for name,cam,fr in [('front',0,1),('three-quarter',1,1),('profile',2,1),('laugh',0,55)]:
    sc.camera=cams[cam];sc.frame_set(fr);sc.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
