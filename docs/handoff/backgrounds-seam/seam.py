import sys
from PIL import Image
im=Image.open(sys.argv[1]).convert('RGB'); W,H=im.size
n=0
for y in range(0,2200):
    x0=round(0.2865*y)
    for x in range(max(2,x0-3),min(W-2,x0+4)):
        p=im.getpixel((x,y)); l=im.getpixel((x-2,y)); r=im.getpixel((x+2,y))
        if sum(abs(a-b) for a,b in zip(p,l))>40 and sum(abs(a-b) for a,b in zip(p,r))>40 and sum(abs(a-b) for a,b in zip(l,r))<12: n+=1
print(sys.argv[1],n)
