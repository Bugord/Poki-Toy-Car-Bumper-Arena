"""Builds the web template's loading screen assets into Assets/WebGLTemplates/poki-template/loading.

- truck.webp, car.webp: the cars LoadingScreenArt draws into Logs/loading-art (run that PlayMode test first),
  trimmed to their pixels and shrunk to the same scale.
- font.woff2: Lilita One cut down to the few letters the screen shows. Under the OFL a subset is a modified
  version, and "Lilita" is a reserved name, so the subset gets a name of its own.

Usage: python Tools/loading/art.py   (Pillow and fonttools with brotli)
"""
import os
import shutil

from fontTools import subset
from fontTools.ttLib import TTFont
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Logs", "loading-art")
OUT = os.path.join(ROOT, "Assets", "WebGLTemplates", "poki-template", "loading")
FONTS = os.path.join(ROOT, "Assets", "UI", "Fonts")

# Shown at most about 190 CSS px wide; twice that keeps it sharp on phones (both cars are drawn at one scale).
SCALE = 0.6
TEXT = "BONKAR0123456789%!. "
FAMILY = "Bonk Loading"


def cars():
    pictures = {name: Image.open(os.path.join(ART, f"{name}.png")).convert("RGBA") for name in ("truck", "car")}
    for name, picture in pictures.items():
        picture = picture.crop(picture.getchannel("A").getbbox())
        size = (round(picture.width * SCALE), round(picture.height * SCALE))
        picture = picture.resize(size, Image.LANCZOS)
        path = os.path.join(OUT, f"{name}.webp")
        picture.save(path, "WEBP", quality=88, alpha_quality=90, method=6)
        print(f"{path}: {size[0]}x{size[1]}, {os.path.getsize(path)} bytes")


def font():
    options = subset.Options()
    options.flavor = "woff2"
    options.hinting = False
    options.name_IDs = []
    font = TTFont(os.path.join(FONTS, "font_lilita_one.ttf"))
    subsetter = subset.Subsetter(options)
    subsetter.populate(text=TEXT)
    subsetter.subset(font)
    names = font["name"]
    names.names = []
    names.setName(FAMILY, 1, 3, 1, 0x409)
    names.setName("Regular", 2, 3, 1, 0x409)
    names.setName(FAMILY, 4, 3, 1, 0x409)
    names.setName(FAMILY.replace(" ", "") + "-Regular", 6, 3, 1, 0x409)
    names.setName("Subset of Lilita One, Copyright (c) 2011 Juan Montoreano, SIL Open Font License 1.1", 0, 3, 1, 0x409)
    path = os.path.join(OUT, "font.woff2")
    font.flavor = "woff2"
    font.save(path)
    shutil.copy(os.path.join(FONTS, "font_lilita_one_OFL.txt"), os.path.join(OUT, "font_OFL.txt"))
    print(f"{path}: {os.path.getsize(path)} bytes")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    font()
    cars()
