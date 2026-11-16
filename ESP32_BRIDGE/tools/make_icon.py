# Значки страницы моста (main/page/icon-*.png, apple-touch-icon.png): синий
# квадрат, белое «K57», жёлтая синусоида. Рисуется в координатах 512x512 и
# масштабируется; края сглажены по расстоянию до штриха. Без PIL - PNG вручную.
#
#   python tools/make_icon.py main/page
import math
import struct
import sys
import zlib

BG_TOP = (0x24, 0x7a, 0xe0)
BG_BOT = (0x15, 0x4f, 0xa8)
TEXT = (0xff, 0xff, 0xff)
WAVE = (0xff, 0xc2, 0x3a)


def strokes():
    """Штрихи: (список отрезков, радиус, цвет) в координатах 512."""
    top, bot = 118.0, 288.0
    segs = []
    x = 80.0
    # K
    segs += [((x, top), (x, bot)), ((x + 100, top), (x + 10, 212)), ((x + 42, 182), (x + 100, bot))]
    x += 128
    # 5
    # верх, стойка и чаша - дуга окружности, низ чаши на линии bot
    cx, cy, r5 = x + 50, bot - 50, 50.0
    arc = [(cx + r5 * math.cos(math.radians(a)), cy + r5 * math.sin(math.radians(a)))
           for a in range(-150, 141, 5)]
    p5 = [(x + 98, top), (x + 10, top), (x + 6, 212)] + arc
    segs += list(zip(p5, p5[1:]))
    x += 128
    # 7
    segs += [((x, top), (x + 100, top)), ((x + 100, top), (x + 38, bot))]
    text = (segs, 21.0, TEXT)

    wave = []
    n = 48
    pts = [(116 + 280 * i / n, 386 - 44 * math.sin(2 * math.pi * i / n)) for i in range(n + 1)]
    wave = list(zip(pts, pts[1:]))
    return [text, (wave, 13.0, WAVE)]


def render(size):
    k = size / 512.0
    cov = [[[0.0] * size for _ in range(size)] for _ in range(2)]
    for li, (segs, r, _) in enumerate(strokes()):
        r *= k
        c = cov[li]
        for (ax, ay), (bx, by) in segs:
            ax, ay, bx, by = ax * k, ay * k, bx * k, by * k
            x0 = max(0, int(min(ax, bx) - r - 2)); x1 = min(size - 1, int(max(ax, bx) + r + 2))
            y0 = max(0, int(min(ay, by) - r - 2)); y1 = min(size - 1, int(max(ay, by) + r + 2))
            dx, dy = bx - ax, by - ay
            ll = dx * dx + dy * dy or 1e-9
            for py in range(y0, y1 + 1):
                cy = py + 0.5
                row = c[py]
                for px in range(x0, x1 + 1):
                    cx = px + 0.5
                    t = ((cx - ax) * dx + (cy - ay) * dy) / ll
                    t = 0.0 if t < 0 else 1.0 if t > 1 else t
                    ex, ey = cx - (ax + t * dx), cy - (ay + t * dy)
                    a = r + 0.5 - math.sqrt(ex * ex + ey * ey)
                    if a > 0:
                        a = 1.0 if a > 1 else a
                        if a > row[px]:
                            row[px] = a
    raw = bytearray()
    for py in range(size):
        raw.append(0)
        f = py / (size - 1)
        bg = [BG_TOP[i] + (BG_BOT[i] - BG_TOP[i]) * f for i in range(3)]
        for px in range(size):
            col = bg
            for li, (_, _, fg) in enumerate(strokes_cache):
                a = cov[li][py][px]
                if a:
                    col = [col[i] + (fg[i] - col[i]) * a for i in range(3)]
            raw += bytes(int(v + 0.5) for v in col)
    return png(size, size, bytes(raw))


def png(w, h, raw):
    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) +
            chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


strokes_cache = strokes()
out = sys.argv[1]
for size, name in ((512, 'icon-512.png'), (192, 'icon-192.png'), (180, 'apple-touch-icon.png')):
    data = render(size)
    open(out + '/' + name, 'wb').write(data)
    print(name, len(data), 'байт')
