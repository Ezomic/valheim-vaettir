import sys
from PIL import Image
piece=sys.argv[1]
keys=("current","a","b","c")
ims=[Image.open(f"E:/Repositories/valheim/vaettir/renders/lhm-69/{piece}_tile_{k}.png").crop((0,0,1000,440)) for k in keys]
w,h=ims[0].size
s=Image.new("RGB",(w,h*4))
for i,im in enumerate(ims): s.paste(im,(0,h*i))
s.save("E:/Repositories/valheim/vaettir/renders/lhm-69/_scratch.png")
