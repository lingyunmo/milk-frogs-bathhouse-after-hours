"""Reconstruct the fused lower anatomy in orthographic idle-sprite coordinates."""
from pathlib import Path
import numpy as np
from scipy.interpolate import PchipInterpolator
from skimage.measure import marching_cubes

out = Path(__file__).parent
# Image row, half-width, near and far profile edges (side-view origin at x=140).
trunk = np.array([
 (5,.7,-26,-25),(5.6,8,-34,-18),(7,15,-43,-10),(12,27,-57,0),(25,45,-78,22),(45,59,-89,42),
 (65,71,-95,58),(85,80,-93,75),(102,87,-85,87),(116,89,-79,97),
 (130,87,-87,105),(146,89,-106,113),(169,95,-122,122),
 (193,105,-132,130),(215,113,-136,133),(234,115,-132,130),
 (249,113,-125,123),(263,106,-115,113),(276,97,-101,100),
 (287,83,-85,82),(296,65,-64,61),(302,42,-40,36),(306,.8,-1,1)])
arm = np.array([ # row, lateral center, sagittal center, two radii
 (83,73,2,.6,.6),(99,77,4,16,19),(116,84,5,25,33),(131,92,4,29,38),
 (146,99,2,30,40),(166,108,-4,27,37),(178,118,-13,26,33),
 (194,128,-25,27,32),(210,135,-35,23,28),(226,141,-42,17,21),
 (238,142,-46,18,21),(248,142,-47,19,22),(257,142,-47,16,18),
 (266,141,-46,8,13),(268,141,-46,.8,.8)])
leg=np.array([(260,53,10,13,22),(274,58,16,33,46),(290,62,16,37,48),
 (305,66,15,32,42),(316,69,17,28,34),(327,69,19,21,28),
 (335,66,20,17,24),(343,68,17,18,26),(350,73,13,23,32),(357,75,11,23,32),
 (362,75,10,15,20),(364,75,10,1,1)])

# The oblique paintings show a wider hip stance and forearms with a curved outer head.
leg[:,1]=leg[:,1]+np.interp(leg[:,0],[260,285,315,345,364],[0,3,6,5,3])
arm[:,4]=arm[:,4]*np.interp(arm[:,0],[83,116,146,180,216,250,268],[1,1.08,1.15,1.18,1.14,1,1])
def sampler(table):
 return PchipInterpolator(table[:,0],table[:,1:],axis=0,extrapolate=False)
body_at, arm_at, leg_at = map(sampler,(trunk,arm,leg))
def union(a,b,s):
 return np.minimum(a,b)-np.maximum(s-np.abs(a-b),0)**2/(4*s)
def ellipse(x,y,cx,cy,rx,ry):
 return (np.sqrt(((x-cx)/rx)**2+((y-cy)/ry)**2)-1)*min(rx,ry)
def sweep(x,y,t,table,fn):
 if t<table[0,0] or t>table[-1,0]:return np.full_like(x,1000.)
 cx,cy,rx,ry=fn(t)
 return ellipse(x,y,cx,cy,rx,ry)
def ellipsoid(x,y,t,center,radii,angle=0,toe=False):
 cx,cy,cz=center; rx,ry,rz=radii
 u=(x-cx)*np.cos(angle)+(y-cy)*np.sin(angle)
 v=(y-cy)*np.cos(angle)-(x-cx)*np.sin(angle)
 dz=t-cz
 if toe and dz>0:rz*=.75
 return (np.sqrt((u/rx)**2+(v/ry)**2+(dz/rz)**2)-1)*min(rx,ry,rz)
def digit(x,y,t,path):
 # Tapered phalanges following three points rather than cylinders.
 dist=np.full_like(x,1000.)
 for (a,ra),(b,rb) in zip(path,path[1:]):
  axis=np.subtract(b,a); u=np.clip(((x-a[0])*axis[0]+(y-a[1])*axis[1]+(t-a[2])*axis[2])/np.dot(axis,axis),0,1)
  d=np.sqrt((x-a[0]-axis[0]*u)**2+(y-a[1]-axis[1]*u)**2+(t-a[2]-axis[2]*u)**2)-(ra+(rb-ra)*u)
  dist=np.minimum(dist,d)
 return dist

fingers=[
 [((134,-62,245),8.7),((126,-66,254),8),((120,-65,261),6.4),((119,-59,264),4.7)],
 [((145,-62,250),8),((148,-65,262),7.6),((145,-65,273),6.3),((138,-62,279),4.5)],
 [((153,-51,251),8),((155,-52,264),7.6),((151,-52,275),6.1),((143,-50,280),4.7)],
 [((152,-38,252),7.8),((154,-39,264),7),((149,-39,274),5.7),((142,-38,277),4.3)]]
def sections(x,y,t):
 xa=np.abs(x)
 if trunk[0,0]<=t<=trunk[-1,0]:
  rx,front,back=body_at(t); mid=(back+front)/2;ry=(back-front)/2
  # Shallow groove of the back and full, heavier lower flanks.
  yy=y+np.maximum(y-mid,0)/max(ry,1)*2.7*np.exp(-(x/14)**2-((t-173)/73)**2)
  a=np.arctan2(x/rx,(mid-yy)/ry)
  girth=1+.105*np.sin(2*a)**2*np.clip((t-85)/45,0,1)
  torso=(np.sqrt((x/rx)**2+((yy-mid)/ry)**2)-girth)*min(rx,ry)
 else:torso=np.full_like(x,1000.)
 hand=sweep(xa,y,t,arm,arm_at)
 if 230<t<291:
  for chain in fingers:hand=union(hand,digit(xa,y,t,chain),3.4)
 hips=sweep(xa,y,t,leg,leg_at)
 if t>319:
  # Metatarsal pad and separate blunt, splayed toe lobes. A broad heel turns into the ankle.
  heel=ellipsoid(xa,y,t,(75,7,352.0),(30,41,17.5),.20,True)
  fore=ellipsoid(xa,y,t,(87,-14,356),(36,27,14.3),.32,True)
  foot=union(heel,fore,6)
  for j,(cx,cy,rx,ry) in enumerate([(60,-39,15,31),(84,-42,14,30),(105,-38,13,28),(119,-29,10,24)]):
   toe=ellipsoid(xa,y,t,(cx,cy,357.5+j*.36),(rx,ry,14-j*.6), .22+j*.14,True)
   foot=union(foot,toe,1.7)
  hips=union(hips,foot,8)
 join=union(torso,hips,13)
 return union(join,hand,13 if t<155 else 6),hand,join

def make():
 step=1.5; t0=146
 xs=np.arange(-177,178,step);ys=np.arange(-150,151,step);ts=np.arange(t0,376,step)
 x,y=np.meshgrid(xs,ys,indexing='ij')
 vol=np.stack([sections(x,y,t)[0] for t in ts],axis=2).astype('f')
 verts,faces,_,_=marching_cubes(vol,0,spacing=(step,step,step),allow_degenerate=False)
 verts+=np.array([xs[0],ys[0],ts[0]])
 # Upper surface radial contour, sampled around a common sagittal center.
 angles=(np.arange(256)-128)*np.pi/128
 rows=np.linspace(83,t0,127); envelope=[]; upper_weights=[]
 for t in rows:
  rx,f,b=body_at(t);cy=(f+b)/2;ry=(b-f)/2
  # ellipse angle parameter is the same as the face sculpt's parameter
  xx=rx*np.sin(angles);yy=-ry*np.cos(angles)
  lo=np.ones(256)*.85;hi=np.ones(256)*2.2
  for _ in range(17):
   m=(hi+lo)/2;outside=sections(xx*m,cy+yy*m,t)[0]>0
   hi=np.where(outside,m,hi);lo=np.where(outside,lo,m)
  m=(hi+lo)/2
  envelope.append(m)
  _,hand,body=sections(xx*m,cy+yy*m,t)
  upper_weights.append(np.clip(.5+(body-hand)/26,0,1))
 weights=np.zeros(len(verts),dtype=np.float32)
 for t in np.unique(np.round(verts[:,2]*2)/2):
  idx=np.abs(verts[:,2]-t)<=.25001
  if t>288:continue
  _,hand,body=sections(verts[idx,0],verts[idx,1],t)
  weights[idx]=np.clip(.5+(body-hand)/(2*(13 if t<155 else 6)),0,1)
 # Smooth the grid isosurface without shifting the open collar ring. This removes
 # narrow shoulder ridges from tracing a concave armpit with a finite grid.
 from scipy.sparse import coo_matrix
 e=np.concatenate((faces[:,[0,1]],faces[:,[1,2]],faces[:,[2,0]]))
 mat=coo_matrix((np.ones(len(e)),(e[:,0],e[:,1])),shape=(len(verts),len(verts))).tocsr()
 mat=mat+mat.T;mat=mat.multiply(1/mat.sum(axis=1)).tocsr()
 fixed=verts[:,2]<147.5
 for _ in range(8):
  dif=mat@verts-verts;dif[fixed]=0;verts+=.49*dif
 np.savez_compressed(out/'anatomy.npz',verts=verts,faces=faces,
  upper_weights=np.array(upper_weights),envelope=np.array(envelope),rows=rows,profile=trunk, sampled=body_at(np.arange(5,306.01,.25)), arm_table=arm, hand_weight=weights)
if __name__=='__main__':make()
