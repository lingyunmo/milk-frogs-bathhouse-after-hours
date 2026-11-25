from pathlib import Path
import numpy as np
from scipy.sparse import coo_matrix,diags
from scipy.sparse.linalg import splu

p=Path(__file__).parent
d=np.load(p/'rest.npz');ref=d['rest'].astype(float);tri=d['face']
t=139.52-76*ref[:,2];x,y=ref[:,:2].T
def ease(x):
 x=np.clip(x,0,1);return x*x*(3-2*x)
# Ring spacing changes at the shoulder. Use physical surface area so both sides
# of that join receive the same polishing distance irrespective of edge density.
def polish(pos):
 a,b,c=pos[tri].transpose(1,0,2)
 area2=np.linalg.norm(np.cross(b-a,c-a),axis=1)
 mass=np.bincount(tri.flatten(),np.repeat(area2/6,3),minlength=len(pos))
 rows=[];cols=[];vals=[]
 for k in range(3):
  i,j,l=tri[:,k],tri[:,(k+1)%3],tri[:,(k+2)%3]
  e=pos[i]-pos[l];f=pos[j]-pos[l]
  cot=np.einsum('ij,ij->i',e,f)/np.maximum(area2,1.e-12)
  rows.extend([i,j]);cols.extend([j,i]);vals.extend([cot*.5,cot*.5])
 w=coo_matrix((np.concatenate(vals),(np.concatenate(rows),np.concatenate(cols))),shape=(len(pos),len(pos))).tocsr()
 # Limit degenerate triangle slivers along the original zipper, after adding opposite angles.
 w.data=np.clip(w.data,0,40);w.eliminate_zeros()
 lap=diags(np.asarray(w.sum(axis=1)).ravel())-w
 side=ease((np.abs(x)-.62)/.55)
 back=ease((y+.55)/.75)
 faceprotect=1-(1-ease((t-115)/24))*(1-back)
 shoulder=np.exp(-((t-148)/65)**2)
 wrist=ease((np.abs(x)-1.4)/.27)*ease((t-210)/24)
 lower=ease((t-275)/27)*(1-ease((t-337)/16))
 radius=(.12+.17*shoulder+.07*side+.08*lower)*(1-.82*wrist)*(1-.88*ease((t-338)/17))
 radius*=faceprotect*(1-.28*side*ease((t-130)/33))*(1-.22*lower)
 fixed=(radius<.0008)|(mass<1.e-13)
 active=~fixed;radius=np.maximum(radius,.0008)
 anchor=diags(mass/radius**2)
 mat=(lap+anchor).tocsc();solve=splu(mat[active,:][:,active])
 boundary=lap[active,:][:,fixed]@pos[fixed]
 def blur(target):
  out=target.copy()
  out[active]=solve.solve((mass[:,None]*target/radius[:,None]**2)[active]-boundary)
  return out
 first=blur(pos);second=blur(first)
 # Return part of the large-scale volume after diffusion; leave short creases softened.
 result=first+.74*(first-second)
 assert np.isfinite(result).all() and np.array_equal(result[fixed],pos[fixed])
 print('moved (median,95%,max)',np.quantile(np.linalg.norm(result-pos,axis=1),[.5,.95,1]))
 return result.astype('f')
np.savez(p/'polished.npz',rest=polish(ref),laugh=polish(d['laugh'].astype(float)))
