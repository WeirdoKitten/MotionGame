"""Cut runtime sprites for the three camera motion games from the concept sheets.

Art/MotionGamesSource/{star,line,rock}-sheet.png (1536x1024 concept boards) ->
Resources/Motion/{bg-star,bg-line,bg-rock,star,star-bonus,rock,rock-mossy}.png
Backgrounds are the "main environment" panels; objects are cut from the dark asset panels
(dark blue panel pixels become transparent, largest object kept).
"""
import pathlib

import cv2
import numpy as np
from PIL import Image, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / 'Assets/_Project/Art/MotionGamesSource'
OUT = ROOT / 'Assets/_Project/Resources/Motion'

BACKGROUNDS = [
    ('bg-star', 'star-sheet.png', (272, 34, 972, 318)),
    ('bg-line', 'line-sheet.png', (272, 56, 972, 318)),
    ('bg-rock', 'rock-sheet.png', (266, 60, 706, 352)),
]
OBJECTS = [
    ('star', 'star-sheet.png', (18, 360, 122, 470)),
    ('rock', 'rock-sheet.png', (554, 378, 666, 478)),
]


def glow(img, box):
    # Stars glow on a dark panel: brightness becomes opacity, so the soft glow is kept.
    crop = np.asarray(img.crop(box).convert('RGB')).astype(float)
    value = crop.max(2)
    alpha = np.clip((value - 120) / 80, 0, 1) * 255
    return Image.fromarray(np.dstack([crop, alpha]).astype('uint8'), 'RGBA')


def cut(img, box):
    crop = np.asarray(img.crop(box).convert('RGB')).astype(int)
    r, g, b = crop[..., 0], crop[..., 1], crop[..., 2]
    value = crop.max(2)
    panel = ((b > r + 18) & (value < 170)) | (value < 55)
    obj = (~panel).astype('uint8') * 255
    obj = cv2.morphologyEx(obj, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(obj)
    if n > 1:
        big = 1 + int(np.argmax(st[1:, 4]))
        obj = np.where(lab == big, 255, 0).astype('uint8')
        # Fill holes (dark eyes, shadows) inside the object.
        flood = obj.copy(); h, w = obj.shape
        mask = np.zeros((h + 2, w + 2), 'uint8')
        cv2.floodFill(flood, mask, (0, 0), 128)
        obj[flood == 0] = 255
    alpha = Image.fromarray(obj).filter(ImageFilter.GaussianBlur(1))
    return Image.fromarray(np.dstack([crop.astype('uint8'), np.asarray(alpha)]), 'RGBA')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, sheet, box in BACKGROUNDS:
        Image.open(SRC / sheet).convert('RGB').crop(box).resize((1280, int(1280 * (box[3] - box[1]) / (box[2] - box[0]))), Image.LANCZOS).save(OUT / f'{name}.png')
    for name, sheet, box in OBJECTS:
        (glow if name == 'star' else cut)(Image.open(SRC / sheet), box).save(OUT / f'{name}.png')
    print('Prepared motion game sprites')


if __name__ == '__main__':
    main()
