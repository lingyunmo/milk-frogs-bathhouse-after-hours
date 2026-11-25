from pathlib import Path
from PIL import Image
import subprocess
p=Path(__file__).resolve().parent
files=sorted((p/'frames').glob('*.png'))
assert len(files)==110,len(files)
proc=subprocess.Popen(['ffmpeg','-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-video_size','614x614','-framerate','24','-i','pipe:0','-c:v','libx264','-pix_fmt','yuv420p','-crf','17','-movflags','+faststart',str(p/'Head-turntable.mp4')],stdin=subprocess.PIPE)
for f in files:
 im=Image.open(f).convert('RGBA');assert im.size==(614,614),im.size;bg=Image.new('RGB',im.size,(34,36,35));bg.paste(im,mask=im.getchannel('A'));proc.stdin.write(bg.tobytes())
proc.stdin.close();assert proc.wait()==0
