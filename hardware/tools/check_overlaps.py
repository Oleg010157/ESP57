# Поиск налезающих надписей на листах схемы.
# Схема выгружается в PDF (kicad-cli), из PDF берутся рамки всех слов, пересекающиеся рамки - перекрытие.
# Запуск: python -I hardware/tools/check_overlaps.py <путь к pymupdf> hardware/k57-power/k57-power.kicad_sch [подробно]
import sys, os, subprocess, tempfile
sys.path.insert(0, sys.argv[1])
import pymupdf

CLI = r"C:\Program Files\KiCad\10.0\bin\kicad-cli.exe"
sch = sys.argv[2]
verbose = len(sys.argv) > 3
pdf = os.path.join(tempfile.gettempdir(), os.path.basename(sch) + ".pdf")
subprocess.run([CLI, "sch", "export", "pdf", "-o", pdf, sch], check=True, capture_output=True)
doc = pymupdf.open(pdf)
MM = 25.4 / 72                                   # пункты PDF -> мм
total = 0
for pno, page in enumerate(doc):
    text = page.get_text()
    title = next((l for l in text.splitlines() if l.startswith("Title:")), f"стр. {pno + 1}")
    words = [w for w in page.get_text("words") if w[4].strip()]
    # рамка листа и штамп не интересны: отбрасываем слова в штампе (правый нижний угол)
    W, H = page.rect.width, page.rect.height
    words = [w for w in words if not (w[0] > W * 0.72 and w[1] > H * 0.86)]
    hits = []
    for i in range(len(words)):
        a = words[i]
        for j in range(i + 1, len(words)):
            b = words[j]
            ix = min(a[2], b[2]) - max(a[0], b[0])
            iy = min(a[3], b[3]) - max(a[1], b[1])
            # перекрытие - если слова заметно накрывают друг друга: больше трети высоты строки
            # (рамки соседних строк одного текста касаются на ~1 пункт - это не перекрытие)
            hmin = min(a[3] - a[1], b[3] - b[1])
            if ix > 0.6 and iy > 0.35 * hmin:
                # соседние слова одной строки одного текста (тот же блок, та же строка) - не перекрытие
                if a[5] == b[5] and a[6] == b[6]:
                    continue
                # KiCad кладёт в PDF каждую надпись дважды (видимую и для поиска) - это не перекрытие
                if a[4] == b[4] and abs(a[0] - b[0]) < 1.5 and abs(a[1] - b[1]) < 1.5:
                    continue
                hits.append((a, b, ix * iy))
    total += len(hits)
    print(f"{title}: перекрытий {len(hits)}")
    if verbose:
        for a, b, area in sorted(hits, key=lambda h: -h[2])[:40]:
            print(f"   '{a[4]}' x '{b[4]}'  ({a[0] * MM:.0f}, {a[1] * MM:.0f}) мм")
print("всего:", total)
