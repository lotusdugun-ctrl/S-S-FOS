"""Store images from game shots: captioned screenshots for Google Play and the App Store,
and the Play feature graphic. Usage: compose.py <spec.json> <outdir>"""
import json, sys, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

FONTS = os.path.join(os.path.dirname(__file__), '..', '..', 'unity', 'Assets', 'Resources', 'Fonts')
def font(name, size): return ImageFont.truetype(os.path.join(FONTS, name), size)
GOLD = (214, 176, 104)
CREAM = (241, 228, 196)

def crop_to(im, ratio, focus=0.5):
    w, h = im.size
    if w / h > ratio:
        nw = int(h * ratio); x = int((w - nw) * focus); return im.crop((x, 0, x + nw, h))
    nh = int(w / ratio); y = int((h - nh) / 2); return im.crop((0, y, w, y + nh))

def meander(d, x0, x1, y, s, col):
    # a Greek key band, s tall
    x = x0
    while x + s * 1.6 <= x1:
        pts = [(x, y + s), (x, y), (x + s, y), (x + s, y + s * 0.66), (x + s * 0.33, y + s * 0.66), (x + s * 0.33, y + s * 0.33), (x + s * 0.66, y + s * 0.33)]
        d.line(pts, fill=col, width=max(1, int(s * 0.11)))
        d.line([(x, y + s), (x + s * 1.6, y + s)], fill=col, width=max(1, int(s * 0.11)))
        x += s * 1.6

def caption(im, title, sub, top=True):
    W, H = im.size
    band = int(H * 0.26)
    shade = Image.new('L', (1, band))
    for i in range(band):
        t = i / band
        shade.putpixel((0, i), int(235 * (1 - t) ** 1.6) if top else int(235 * t ** 1.6))
    shade = shade.resize((W, band))
    dark = Image.new('RGB', (W, band), (10, 8, 12))
    y0 = 0 if top else H - band
    im.paste(dark, (0, y0), shade)
    d = ImageDraw.Draw(im)
    ts = int(H * 0.068)
    f1 = font('Cinzel-Bold.ttf', ts)
    tw = d.textlength(title, font=f1)
    ty = int(H * 0.045) if top else H - int(H * 0.19)
    d.text(((W - tw) / 2 + 2, ty + 2), title, font=f1, fill=(0, 0, 0))
    d.text(((W - tw) / 2, ty), title, font=f1, fill=CREAM)
    if sub:
        f2 = font('CormorantGaramond-MediumItalic.ttf', int(ts * 0.62))
        sw = d.textlength(sub, font=f2)
        sy = ty + int(ts * 1.18)
        d.text(((W - sw) / 2 + 1, sy + 1), sub, font=f2, fill=(0, 0, 0))
        d.text(((W - sw) / 2, sy), sub, font=f2, fill=GOLD)
    return im

def shot(src, size, title, sub, top=True, focus=0.5):
    im = Image.open(src).convert('RGB')
    im = crop_to(im, size[0] / size[1], focus).resize(size, Image.LANCZOS)
    im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=40, threshold=2))
    return caption(im, title, sub, top)

def framed(src, size, title, sub, focus=0.5):
    """a picture set on a dark page, for screens much taller than the game (iPad)"""
    W, H = size
    page = Image.new('RGB', size, (14, 12, 18))
    pw = int(W * 0.94); ph = int(pw * 9 / 16)
    pic = crop_to(Image.open(src).convert('RGB'), 16 / 9, focus).resize((pw, ph), Image.LANCZOS)
    px = (W - pw) // 2; py = int(H * 0.30)
    d = ImageDraw.Draw(page)
    d.rectangle((px - 10, py - 10, px + pw + 10, py + ph + 10), outline=GOLD, width=3)
    page.paste(pic, (px, py))
    meander(d, px, px + pw, py - int(H * 0.05), int(H * 0.022), GOLD)
    meander(d, px, px + pw, py + ph + int(H * 0.025), int(H * 0.022), GOLD)
    ts = int(H * 0.055)
    f1 = font('Cinzel-Bold.ttf', ts)
    tw = d.textlength(title, font=f1)
    d.text(((W - tw) / 2, int(H * 0.09)), title, font=f1, fill=CREAM)
    if sub:
        f2 = font('CormorantGaramond-MediumItalic.ttf', int(ts * 0.7))
        sw = d.textlength(sub, font=f2)
        d.text(((W - sw) / 2, int(H * 0.09) + int(ts * 1.3)), sub, font=f2, fill=GOLD)
    return page

def feature(src, out, focus=0.5, right=False):
    im = crop_to(Image.open(src).convert('RGB'), 1024 / 500, focus).resize((1024, 500), Image.LANCZOS)
    shade = Image.new('L', (1024, 1))
    for x in range(1024):
        t = (1023 - x) / 1024 if right else x / 1024
        shade.putpixel((x, 0), int(200 * max(0, 1 - t * 1.9)))
    shade = shade.resize((1024, 500))
    im.paste(Image.new('RGB', (1024, 500), (10, 8, 12)), (0, 0), shade)
    d = ImageDraw.Draw(im)
    f1 = font('Cinzel-Bold.ttf', 78); f2 = font('Cinzel-Medium.ttf', 34)
    x0 = 1024 - 440 - 50 if right else 54
    d.text((x0, 170), 'SISYPHUS', font=f1, fill=CREAM)
    meander(d, x0 + 4, x0 + 386, 268, 14, GOLD)
    d.text((x0 + 4, 298), 'ENDLESS CLIMB', font=f2, fill=GOLD)
    im.save(out)

if __name__ == '__main__':
    spec = json.load(open(sys.argv[1])); out = sys.argv[2]
    os.makedirs(out, exist_ok=True)
    for i, s in enumerate(spec['shots']):
        n = f"{i + 1:02d}-{s['name']}"
        for target, size in (('play-phone', (2000, 1000)), ('ios-iphone-6.9', (2868, 1320))):
            os.makedirs(os.path.join(out, target), exist_ok=True)
            shot(s['src'], size, s['title'], s.get('sub'), s.get('top', True), s.get('focus', 0.5)).save(os.path.join(out, target, n + '.png'))
        os.makedirs(os.path.join(out, 'ios-ipad-13'), exist_ok=True)
        framed(s['src'], (2752, 2064), s['title'], s.get('sub'), s.get('focus', 0.5)).save(os.path.join(out, 'ios-ipad-13', n + '.png'))
    if spec.get('feature'):
        feature(spec['feature'], os.path.join(out, 'feature-1024x500.png'), spec.get('featureFocus', 0.5), spec.get('featureRight', False))
