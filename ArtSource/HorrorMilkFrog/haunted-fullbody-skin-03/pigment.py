from pathlib import Path
import numpy as np,cv2
from PIL import Image
from scipy.ndimage import gaussian_filter,maximum_filter,distance_transform_edt

p=Path(__file__).parent; old=p.parent/'haunted-fullbody-surface-02'; ref=p.parent/'haunted-fullbody-study-01'
d=np.load(p/'capture.npz'); v,n=d['v'],d['norm'];x,y,z=v.T;t=139.52-z*76
ink=d['ink'][:,:3]
def ease(x):
 x=np.clip(x,0,1);return x*x*(3-2*x)
def rgba(f):return np.asarray(Image.open(f).convert('RGBA'),dtype='f')/255
def rows(mask):
 yy=np.where(mask.any(1))[0];lo=np.argmax(mask,axis=1);hi=mask.shape[1]-1-np.argmax(mask[:,::-1],axis=1)
 return yy,lo,hi
def field(im,x,y):
 return cv2.remap(im.astype('f'),np.clip(x,0,im.shape[1]-1).astype('f'),np.clip(y,0,im.shape[0]-1).astype('f'),cv2.INTER_LINEAR)
def sample(im,uv):
 # Vectorized bilinear samples without treating the vertices as a very tall OpenCV image.
 sx=np.clip(uv[:,0],0,im.shape[1]-1.001);sy=np.clip(uv[:,1],0,im.shape[0]-1.001)
 ix=sx.astype(int);iy=sy.astype(int); ax=(sx-ix)[:,None];ay=(sy-iy)[:,None]
 return (im[iy,ix]*(1-ax)+im[iy,ix+1]*ax)*(1-ay)+(im[iy+1,ix]*(1-ax)+im[iy+1,ix+1]*ax)*ay

acc=np.zeros_like(v);tot=np.zeros((len(v),1));N=480
for i,(uv,ray) in enumerate(zip(d['views'],d['rays'])):
 refimg=rgba(ref/f'ref{i}.png')
 base=cv2.resize(rgba(next((old/'views').glob(f'{i+1:02}-*.png'))),(N,N),interpolation=cv2.INTER_AREA)
 ma=base[:,:,3]>.60;mr=refimg[:,:,3]>.60
 yy,lo,hi=rows(ma);yr,l,h=rows(mr);ys,xs=np.mgrid[:N,:N]
 # Register the original silhouette at each height. Smooth widths so a separating
 # hand or the end of a far toe does not produce a hard pigment band.
 sy=(ys-yy[0])*(yr[-1]-yr[0])/(yy[-1]-yy[0])+yr[0]
 r=np.clip(sy[:,0].round().astype(int),yr[0],yr[-1])
 def blur(a):return gaussian_filter(a.astype('f'),4)
 u=(xs-blur(lo)[:,None])/np.maximum((blur(hi)-blur(lo))[:,None],5)
 sx=blur(l)[r,None]+u*(blur(h)-blur(l))[r,None]
 target=field(refimg,sx,sy);confidence=ma*(target[:,:,3]>.85)
 # Estimate broad chromatic differences; remove speculars, occluded edges and teeth.
 residual=np.clip(np.log((target[:,:,:3]+.045)/(base[:,:,:3]+.045)),-.38,.24)
 confidence*=np.min(base[:,:,:3],axis=2)>.035
 weights=gaussian_filter(confidence.astype('f'),5)+1.e-6
 residual=gaussian_filter(residual*confidence[:,:,None],(5,5,0))/weights[:,:,None]
 pix=uv[:,:2]*N-.5;ij=np.clip(np.round(pix).astype(int),0,N-1)
 depth=np.full((N,N),-100.,'f');np.maximum.at(depth,(ij[:,1],ij[:,0]),uv[:,2])
 seen=uv[:,2]>maximum_filter(depth,3)[ij[:,1],ij[:,0]]-.075
 wt=(np.clip(n@ray,0,1)**4*seen*weights[ij[:,1],ij[:,0]])[:,None]
 acc+=wt*sample(residual,pix);tot+=wt
# Average the multi-view estimate over neighbouring surface points. Visibility
# can switch between a near arm and the belly; don't paint those switches as flecks.
start=v.min(axis=0)-.15; cell=.037
ix=np.round((v-start)/cell).astype(int); shape=tuple(ix.max(axis=0)+5)
idx=tuple(ix.T);sums=np.zeros(shape,'f');np.add.at(sums,idx,tot[:,0])
den=gaussian_filter(sums,1.7)[idx]
channels=[]
for j in range(3):
 grid=np.zeros(shape,'f');np.add.at(grid,idx,acc[:,j])
 channels.append(gaussian_filter(grid,1.7)[idx]/np.maximum(den,.08))
correct=np.exp(1.48*np.stack(channels,axis=1))

ax=np.abs(x);front=ease((-y-.13)/.5)
width=np.array([np.interp(t,[130,170,210,234,265,289,305],[87,95,112,115,107,83,37]),
                np.interp(t,[130,170,210,234,265,289,305],[97,124,134,131,114,82,36])])
angle=np.arctan2(x*76/width[0],-y*76/width[1])
edge=((t-209)/77)**2+(angle/1.25)**2
soft_belly=ease((1.18-edge)/.57)*ease((-y+.32)/.49)
gold=np.array([.674,.357,.037]);ivory=np.array([.76,.566,.342]);umber=np.array([.116,.067,.037])
dark=ease((.96-ink[:,0])/.72)*ease((t-187)/31)
dark=np.maximum(dark,ease((t-333)/22))
skin=gold+(ivory-gold)*soft_belly[:,None];skin=skin*(1-dark[:,None])+umber*dark[:,None]
original=np.array([.715,.385,.056])*ink
head=ease((134-t)/20)
skin=skin*(1-head[:,None])+original*head[:,None]
face=ease((116-t)/25)*front
socket=np.exp(-((ax-.393)/.28)**2-((t-55+18*(ax-.39)) /9)**2)*face
skin*=1-socket[:,None]*np.array([.21,.29,.24])
skin*=correct**(1-.49*face[:,None])
skin*=np.array([.985,.965,.94])
from scipy.spatial import cKDTree
rim_d=cKDTree(v[d['rim']]).query(v)[0]
rim=np.exp(-(rim_d/.082)**2)
skin=skin*(1-.72*rim[:,None])+np.array([.23,.087,.039])*.72*rim[:,None]
# Compressed skin around knuckles: short interrupted transverse arcs on the fingers.
# Tangential color and relief both follow the bind pose through the belly laugh.
wrinkles=np.zeros(len(v))
for cx,cy,row,length in [(146,-65,261,9),(155,-52,261,9),(153,-39,262,8),(126,-65,253,7),
                         (63,-46,352,12),(86,-50,351,12),(105,-46,353,11),(119,-39,354,9)]:
 dx=ax*76-cx;dy=y*76-cy
 dist=t-row-.021*dx**2+.056*dy
 footprint=np.exp(-(dx/length)**2-(dy/(12 if row<300 else 17))**2)
 wrinkles+=footprint*(np.exp(-(dist/1.25)**2)+.53*np.exp(-((dist-3.1)/.85)**2))
crease=np.clip(wrinkles,0,1)
skin*=1-.24*crease[:,None]
rough=.53+.07*soft_belly+.03*dark+.025*ease((t-68)/30)-rim*.18
np.savez(p/'paint.npz',albedo=skin.astype('f'),rough=rough.astype('f'),belly=soft_belly.astype('f'),pads=dark.astype('f'),crease=crease.astype('f'))

# Derive material channels at the source image's native pixel dimensions. The R channel
# carries pigment variation; G contains pores/folds only, B modulates the local gloss.
img=rgba(p/'Ochre-hide.png')[:,:,:3];lum=img@np.array([.48,.44,.08])
fine=lum-gaussian_filter(lum,3);med=gaussian_filter(lum,3)-gaussian_filter(lum,33)
wide=gaussian_filter(lum,33)-np.mean(lum)
tone=np.clip(.50+fine*1.8+med*1.65+wide*1.32,.08,.90)
height=np.clip(.50+fine*2.75+med*.53,.04,.96)
oil=np.clip(.5-.84*fine-.72*med-.42*wide,.20,.82)
cv2.imwrite(str(p/'Hide-material-channels.png'),(np.stack([oil,height,tone],axis=2)*65535).round().astype(np.uint16))
