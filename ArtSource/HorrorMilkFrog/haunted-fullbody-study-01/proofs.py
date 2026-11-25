from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np

p=Path(__file__).parent
regular='C:/Windows/Fonts/msyh.ttc'
bold='C:/Windows/Fonts/msyhbd.ttc'
labels=['正面','左前','左侧','左后','背面','右后','右侧','右前']
refs=[Image.open(p/f'ref{i}.png') for i in range(8)]
renders=[Image.open(next((p/'views').glob(f'{i+1:02}-*.png'))) for i in range(8)]
def font(n,b=False):return ImageFont.truetype(bold if b else regular,n)
def show(canvas,im,cx,foot,height):
 box=im.getchannel('A').point(lambda x:255 if x>95 else 0).getbbox()
 im=im.crop(box);im=im.resize((round(im.width*height/im.height),height),Image.Resampling.LANCZOS)
 canvas.paste(im,(int(cx-im.width/2),foot-height),im)
def sheet(compare):
 W=1840; H=1120 if not compare else 1090
 canvas=Image.new('RGB',(W,H),'#242624');d=ImageDraw.Draw(canvas)
 d.text((46,25),'恐怖奶娃',font=font(37,True),fill='#f3eadc')
 d.text((228,41),'原画与模型对照' if compare else '全身 · 八向定妆',font=font(21),fill='#cfbf9f')
 for i in range(8):
  row,col=divmod(i,4);x=36+col*445;y=104+row*(488 if compare else 498)
  d.rounded_rectangle((x,y,x+430,y+(470 if compare else 480)),radius=10,fill='#343633')
  d.text((x+18,y+12),labels[i],font=font(22,True),fill='#ece7db')
  if compare:
   show(canvas,refs[i],x+109,y+360,213);show(canvas,renders[i],x+320,y+360,213)
   for cx,name in [(109,'游戏原画'),(320,'3D 模型')]:
    d.text((x+cx,y+400),name,font=font(17),anchor='mt',fill='#d7d2c5')
  else:show(canvas,renders[i],x+215,y+449,385)
 name='Original-vs-model.png' if compare else 'Full-body-eight-views.png'
 canvas.save(p/name)
sheet(False);sheet(True)
def mask(im):
 a=im.getchannel('A').point(lambda x:255 if x>128 else 0);a=a.crop(a.getbbox())
 a=a.resize((round(400*a.width/a.height),400));out=Image.new('L',(430,400))
 out.paste(a,((430-a.width)//2,0));return np.asarray(out)>127
for name,ref,model in zip(labels,refs,renders):
 a,b=mask(ref),mask(model);print(name,round(np.sum(a&b)/np.sum(a|b),3))
for f in (p/'views').glob('*.png'):
 im=Image.open(f);assert im.size==(1200,1200) and im.mode=='RGBA'
