"""Regenerate the optional app icon (requires Pillow). The app itself needs no Python."""
from PIL import Image, ImageDraw
from pathlib import Path

im = Image.new("RGBA", (256, 256))
d = ImageDraw.Draw(im)
d.rounded_rectangle((8, 8, 248, 248), 56, fill="#2763DC")
d.rounded_rectangle((47, 72, 114, 107), 10, fill="#DCE9FF")
d.rounded_rectangle((42, 90, 199, 187), 13, fill="#FFFFFF")
d.rounded_rectangle((42, 107, 199, 187), 13, fill="#DCE9FF")
d.ellipse((96, 89, 177, 170), fill="#FFFFFF", outline="#173E8C", width=12)
d.line((164, 157, 204, 198), fill="#173E8C", width=17)
d.ellipse((195, 188, 212, 205), fill="#173E8C")
im.save(Path(__file__).with_name("LockLens.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
