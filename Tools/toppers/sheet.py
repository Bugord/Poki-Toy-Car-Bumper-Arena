"""Puts the renders of each hat (two sides and on the car) in one row on the garage's sky: sheet.py out.png hat..."""
import sys, os
from PIL import Image, ImageDraw, ImageFont

renders = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'Logs', 'toppers-preview')
out, names = sys.argv[1], sys.argv[2:]
labels = dict(a.split('=') for a in names if '=' in a)
names = [a.split('=')[0] for a in names]
cell = int(os.environ.get('CELL', 560))
sky = (104, 178, 232)
sheet = Image.new('RGB', (cell * 3, cell * len(names)), sky)
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype('arialbd.ttf', cell // 14)
except Exception:
    font = ImageFont.load_default()
for row, name in enumerate(names):
    for col, view in enumerate(('a', 'b', 'car')):
        img = Image.open(os.path.join(renders, f'{name}_{view}.png')).convert('RGBA').resize((cell, cell))
        sheet.paste(img, (col * cell, row * cell), img)
    draw.text((cell // 28, row * cell + cell // 28), labels.get(name, name).upper(), fill=(29, 43, 79), font=font)
    if row:
        draw.line((0, row * cell, cell * 3, row * cell), fill=(80, 150, 205), width=2)
sheet.save(out)
print(out)
