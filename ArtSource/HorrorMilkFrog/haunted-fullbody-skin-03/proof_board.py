from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
p=Path(__file__).parent
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)
def crop_into(board,f,box,pos,size):
 im=Image.open(f).convert('RGBA').crop(box).resize(size,Image.Resampling.LANCZOS)
 board.paste(im,pos,im)
board=Image.new('RGB',(1416,1022),'#343630');d=ImageDraw.Draw(board)
for j,(ver,label) in enumerate([('before','上一版'),('after','新版贴图')]):
 x=24+699*j
 d.text((x+334,20),label,font=font(26),fill='#f3e9d3',anchor='mt')
 crop_into(board,p/(ver+'-face.png'),(195,355,957,830),(x,76),(672,419))
 crop_into(board,p/(ver+'-hand.png'),(245,362,1074,982),(x,513),(672,502))
board.save(p/'Texture-comparison.png')
views=sorted((p/'views').glob('[0-9][0-9]-*.png'))
assert len(views)==9
board=Image.new('RGB',(1520,840),'#333532');d=ImageDraw.Draw(board)
for i,(f,label) in enumerate(zip(views,['正面','左前','左侧','左后','背面','右后','右侧','右前'])):
 im=Image.open(f).convert('RGBA');assert im.size==(1200,1200)
 im=im.crop(im.getchannel('A').point(lambda a:255 if a>65 else 0).getbbox())
 im=im.resize((round(im.width*345/im.height),345),Image.Resampling.LANCZOS)
 cx=190+380*(i%4);top=420*(i//4)
 board.paste(im,(cx-im.width//2,55+top),im);d.text((cx,14+top),label,font=font(24),fill='#ece6d6',anchor='mt')
board.save(p/'Eight-views.png')
