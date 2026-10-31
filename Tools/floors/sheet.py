import sys
from PIL import Image
names=sys.argv[2:]
im=[Image.open('../../Assets/Art/Textures/Arenas/tex_floor_%s.png'%n).convert('RGB') for n in names]
w=len(im)
sheet=Image.new('RGB',(512*w,512))
for i,x in enumerate(im): sheet.paste(x.resize((512,512),Image.LANCZOS),(512*i,0))
sheet.save(sys.argv[1])
