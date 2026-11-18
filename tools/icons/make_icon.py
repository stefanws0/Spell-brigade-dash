"""Turn a generated card icon into a square, round icon PNG with a transparent outside, at <size> px.

usage: python -I make_icon.py <source image> <output png> [size]
       python -I make_icon.py --sheet <source image> <out1.png> <out2.png> ... [--size N]
If the source already has transparency (a round badge on a transparent background), it is cropped to the visible
circle. Otherwise (a badge on an opaque square), the black disc is measured, a white ring is added like the game's
icons, and everything outside the ring is made transparent.
--sheet: the source holds several badges side by side (left to right); each is cut out and saved to the next output.
"""
import math
import sys

from PIL import Image


def crop_square(img, box):
    left, top, right, bottom = box
    side = max(right - left, bottom - top)
    cx, cy = (left + right) / 2, (top + bottom) / 2
    half = side / 2 + 2
    return img.crop((int(cx - half), int(cy - half), int(cx + half), int(cy + half)))


def masked(img):
    w, h = img.size
    px = img.load()
    dark = lambda p: (p[0] + p[1] + p[2]) / 3 < 70
    cols = [sum(1 for y in range(h) if dark(px[x, y])) for x in range(w)]
    rows = [sum(1 for x in range(w) if dark(px[x, y])) for y in range(h)]
    left = min(i for i, c in enumerate(cols) if c >= 40)
    right = max(i for i, c in enumerate(cols) if c >= 40)
    top = min(i for i, c in enumerate(rows) if c >= 40)
    r = (right - left) / 2
    cx, cy = (left + right) / 2, top + r
    ring = r * 1.075
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    o = out.load()
    for y in range(h):
        for x in range(w):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if d > ring + 1:
                continue
            rgb = px[x, y][:3] if d <= r else (255, 255, 255)
            o[x, y] = rgb + (int(max(0.0, min(1.0, ring + 0.5 - d)) * 255),)
    return out, (int(cx - ring), int(cy - ring), int(cx + ring), int(cy + ring))


def is_transparent(img):
    box = img.getchannel("A").point(lambda a: 255 if a > 32 else 0).getbbox()
    return box is not None and box != (0, 0, img.width, img.height)


def one_icon(img, label):
    if is_transparent(img):
        box = img.getchannel("A").point(lambda a: 255 if a > 32 else 0).getbbox()
        print(f"{label} {img.size}: already transparent, visible circle {box}")
        return crop_square(img, box)
    result, box = masked(img)
    print(f"{label} {img.size}: opaque, masked to circle {box}")
    return crop_square(result, box)


def split_sheet(img):
    """Column ranges holding a badge: runs of columns with visible content (opaque, or dark on an opaque sheet)."""
    w, h = img.size
    px = img.load()
    transparent = is_transparent(img)
    content = (lambda p: p[3] > 32) if transparent else (lambda p: p[3] > 32 and (p[0] + p[1] + p[2]) / 3 < 70)
    cols = [sum(1 for y in range(0, h, 2) if content(px[x, y])) for x in range(w)]
    runs, start = [], None
    for x, c in enumerate(cols + [0]):
        if c >= 10 and start is None:
            start = x
        elif c < 10 and start is not None:
            if x - start >= w // 20:
                runs.append((start, x))
            start = None
    return runs


def main():
    args = sys.argv[1:]
    size = 256
    if "--size" in args:
        i = args.index("--size")
        size = int(args[i + 1])
        del args[i:i + 2]

    if args and args[0] == "--sheet":
        src, outs = args[1], args[2:]
        img = Image.open(src).convert("RGBA")
        runs = split_sheet(img)
        print(f"sheet {img.size}: found {len(runs)} badge(s) at columns {runs}")
        if len(runs) != len(outs):
            sys.exit(f"expected {len(outs)} badge(s) for the given outputs, found {len(runs)}")
        for i, ((x0, x1), dst) in enumerate(zip(runs, outs)):
            # Cut halfway to the neighbouring badges: wide enough to keep this badge's white ring (outside the dark
            # disc on an opaque sheet), never into a neighbour (whose edge would otherwise be cropped in as well).
            left = 0 if i == 0 else (runs[i - 1][1] + x0) // 2
            right = img.width if i == len(runs) - 1 else (x1 + runs[i + 1][0]) // 2
            part = img.crop((left, 0, right, img.height))
            one_icon(part, dst).resize((size, size), Image.LANCZOS).save(dst)
            print(f"saved {dst} ({size}x{size})")
        return

    src, dst = args[0], args[1]
    if len(args) > 2:
        size = int(args[2])
    img = Image.open(src).convert("RGBA")
    one_icon(img, "source").resize((size, size), Image.LANCZOS).save(dst)
    print(f"saved {dst} ({size}x{size})")


if __name__ == "__main__":
    main()
