import sys, os, numpy as np
from PIL import Image
only = sys.argv[1] if len(sys.argv)>1 else None
for f in sorted(os.listdir('/tmp/claude-0/mz/ref')):
    if not f.endswith('.png'): continue
    n=f[:-4]
    if only and only not in n: continue
    if not os.path.exists(f'/tmp/claude-0/mz/cs/{f}'): print(n,'missing'); continue
    a=np.asarray(Image.open(f'/tmp/claude-0/mz/ref/{f}').convert('RGBA')).astype(float)
    b=np.asarray(Image.open(f'/tmp/claude-0/mz/cs/{f}').convert('RGBA')).astype(float)
    if a.shape!=b.shape: print(n,'shape',a.shape,b.shape); continue
    d=np.abs(a-b)[...,:3]
    bg=a[0,0,:3]
    m=(np.abs(a[...,:3]-bg).sum(-1)>0)|(np.abs(b[...,:3]-bg).sum(-1)>0)
    ys,xs=np.nonzero(m)
    if len(ys)==0: print(n,'empty'); continue
    y0,y1,x0,x1=max(0,ys.min()-4),ys.max()+5,max(0,xs.min()-4),xs.max()+5
    crop=d[y0:y1,x0:x1]
    print(f"{n:28s} crop {x1-x0}x{y1-y0} mean {crop.mean():.3f} max {crop.max():.0f} >16: {(crop.max(-1)>16).mean()*100:.2f}%")
