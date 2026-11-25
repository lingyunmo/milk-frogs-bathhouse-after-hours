from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np
p=Path(__file__).parent;prev=p.parent/'haunted-fullbody-study-01'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
def place(sheet,path,cx,y,height):
 im=Image.open(path);box=im.getchannel('A').point(lambda x:255 if x>30 else 0).getbbox()
 im=im.crop(box);im=im.resize((round(im.width*height/im.height),height),Image.Resampling.LANCZOS)
 sheet.paste(im,(round(cx-im.width/2),y),im)
out=Image.new('RGB',(1420,790),'#302f2e');draw=ImageDraw.Draw(out)
for i,(prefix,title) in enumerate([('before','第一版'),('after','曲面修整后')]):
 cx=355+i*710;draw.text((cx,25),title,font=font,fill='#ede8dd',anchor='mt')
 place(out,p/(prefix+'-flank.png'),cx,83,670)
out.save(p/'Shoulder-comparison.png')
if len(list((p/'views').glob('*.png')))==9:
 sheets=Image.new('RGB',(1520,840),'#333532');d=ImageDraw.Draw(sheets)
 for i,(path,label) in enumerate(zip(sorted((p/'views').glob('*.png'))[:8],['正面','左前','左侧','左后','背面','右后','右侧','右前'])):
  col,row=i%4,i//4;cx=190+col*380
  d.text((cx,14+row*420),label,font=font,fill='#ede8dd',anchor='mt')
  place(sheets,path,cx,55+row*420,345)
  im=Image.open(path);assert im.size==(1200,1200) and im.mode=='RGBA'
  new=np.asarray(im.getchannel('A'))>127;old=np.asarray(Image.open(prev/'views'/path.name).getchannel('A'))>127
  print(path.name,round((old&new).sum()/(old|new).sum(),3))
 sheets.save(p/'Eight-views.png')
