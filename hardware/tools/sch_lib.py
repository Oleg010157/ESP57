# Мини-библиотека: чтение/запись S-выражений KiCad и сборка листа схемы.
import re, uuid, copy, os, json, hashlib

# Отпечатки листов, которые записал скрипт. Если лист с тех пор поменяли
# (например, поправили в KiCad), скрипт его не тронет.
HASHES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "generated-hashes.json")

def _digest(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()

def _load():
    try:
        return json.load(open(HASHES, encoding="utf-8"))
    except FileNotFoundError:
        return {}

def guard(path):
    """Остановиться, если лист правили руками после прошлой генерации."""
    if not os.path.exists(path):
        return
    key = os.path.basename(os.path.dirname(path)) + "/" + os.path.basename(path)
    known = _load().get(key)
    if known != _digest(path):
        raise SystemExit("СТОП: лист " + key + " менялся после генерации (или записан не этим скриптом). "
                         "Перезаписывать не буду, чтобы не стереть правки.")

def remember(path):
    key = os.path.basename(os.path.dirname(path)) + "/" + os.path.basename(path)
    h = _load(); h[key] = _digest(path)
    json.dump(h, open(HASHES, "w", encoding="utf-8"), indent=1, ensure_ascii=False, sort_keys=True)

class Q(str):
    """Строка, которую при записи нужно взять в кавычки."""

def U():
    return str(uuid.uuid4())

def tokenize(s):
    i, n = 0, len(s)
    while i < n:
        c = s[i]
        if c in " \t\r\n":
            i += 1
        elif c == "(" or c == ")":
            yield c; i += 1
        elif c == '"':
            j = i + 1; buf = []
            while s[j] != '"':
                if s[j] == "\\":
                    buf.append({"n": "\n", '"': '"', "\\": "\\"}.get(s[j + 1], s[j + 1])); j += 2
                else:
                    buf.append(s[j]); j += 1
            yield Q("".join(buf)); i = j + 1
        else:
            j = i
            while j < n and s[j] not in ' \t\r\n()"':
                j += 1
            yield s[i:j]; i = j

def parse(s):
    stack = [[]]
    for t in tokenize(s):
        if t == "(" and not isinstance(t, Q):
            stack.append([])
        elif t == ")" and not isinstance(t, Q):
            x = stack.pop(); stack[-1].append(x)
        else:
            stack[-1].append(t)
    return stack[0]

def dump(x):
    if isinstance(x, list):
        return "(" + " ".join(dump(e) for e in x) + ")"
    if isinstance(x, Q):
        return '"' + x.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'
    return str(x)

def find(lst, key):
    return [e for e in lst if isinstance(e, list) and e and e[0] == key]

def lib_symbol(libfile, name):
    tree = parse(open(libfile, encoding="utf-8").read())[0]
    for e in find(tree, "symbol"):
        if e[1] == name:
            return copy.deepcopy(e)
    raise KeyError(name)

def resolved_symbol(libfile, name):
    """Символ из библиотеки; если он построен на другом (extends) - развернуть в полный."""
    s = lib_symbol(libfile, name)
    ext = find(s, "extends")
    if not ext:
        return s
    base = resolved_symbol(libfile, str(ext[0][1]))
    rename_symbol(base, name, name, str(base[1]))
    for p in find(s, "property"):          # свойства потомка важнее
        set_prop(base, str(p[1]), str(p[2]))
    return base

def rename_symbol(sym, newfull, newshort, oldshort):
    sym[1] = Q(newfull)
    for e in find(sym, "symbol"):
        e[1] = Q(str(e[1]).replace(oldshort, newshort, 1))
    return sym

def set_prop(sym, key, val):
    for e in find(sym, "property"):
        if e[1] == key:
            e[2] = Q(val); return
    sym.append([ "property", Q(key), Q(val), ["at", 0, 0, 0], ["effects", ["font", ["size", 1.27, 1.27]], ["hide", "yes"]]])

def pins_of(sym, unit=None):
    """{номер: (x, y, angle)}. unit - номер секции многосекционного символа
    (берутся выводы этой секции и общей, _0_); None - все секции."""
    out = {}
    for u in find(sym, "symbol"):
        m = re.search(r"_(\d+)_\d+$", str(u[1]))
        if unit is not None and m and int(m.group(1)) not in (0, unit):
            continue
        for e in find(u, "pin"):
            at = find(e, "at")[0]; num = find(e, "number")[0][1]
            out[str(num)] = (float(at[1]), float(at[2]), float(at[3]) if len(at) > 3 else 0.0)
    return out

def rot(x, y, a):
    a = int(a) % 360
    return {0: (x, y), 90: (-y, x), 180: (-x, -y), 270: (y, -x)}[a]

ALL_REFS = set()   # обозначения по всему проекту: повтор - ошибка

class Sheet:
    def __init__(self, project, path, paper="A3"):
        self.project, self.path, self.paper = project, path, paper
        self.libs = {}        # lib_id -> определение
        self.items = []       # готовые S-выражения
        self.refs = set()

    def add_lib(self, lib_id, sym):
        self.libs[lib_id] = sym

    def _font(self, size=1.27):
        return ["font", ["size", size, size]]

    def label(self, net, x, y, ang):
        just = {0: "left", 180: "right", 90: "left", 270: "right"}[int(ang) % 360]
        self.items.append(["label", Q(net), ["at", round(x, 3), round(y, 3), int(ang)],
                           ["fields_autoplaced", "yes"],
                           ["effects", self._font(), ["justify", just, "bottom"]], ["uuid", Q(U())]])

    def hlabel(self, net, shape, x, y):
        self.items.append(["hierarchical_label", Q(net), ["shape", shape], ["at", x, y, 180],
                           ["effects", self._font(), ["justify", "right"]], ["uuid", Q(U())]])

    def wire(self, x1, y1, x2, y2):
        self.items.append(["wire", ["pts", ["xy", round(x1, 3), round(y1, 3)], ["xy", round(x2, 3), round(y2, 3)]],
                           ["stroke", ["width", 0], ["type", "default"]], ["uuid", Q(U())]])

    def junction(self, x, y):
        self.items.append(["junction", ["at", round(x, 3), round(y, 3)], ["diameter", 0],
                           ["color", 0, 0, 0, 0], ["uuid", Q(U())]])

    def pin_xy(self, lib_id, x, y, ang, num, unit=None):
        """Координаты конца вывода символа, поставленного в (x, y) с поворотом ang."""
        px, py, pa = pins_of(self.libs[lib_id], unit)[str(num)]
        dx, dy = rot(px, py, ang)
        return x + dx, y - dy

    def bus_power(self, name, points, side, offset=5.08, tail=0.0):
        """Соединить выводы points (концы выводов) коротким отводом вбок и общей
        вертикальной шиной, на конце шины - один символ питания.
        side: -1 - шина левее выводов, +1 - правее; tail - доп. отвод шины вбок."""
        xs = points[0][0] + side * offset
        pts = sorted(points, key=lambda p: p[1])
        for (x, y) in pts:
            self.wire(x, y, xs, y)
        for (x1, y1), (x2, y2) in zip(pts, pts[1:]):
            self.wire(xs, y1, xs, y2)
        for (x, y) in pts[1:-1]:
            self.junction(xs, y)
        if name == "GND":
            ex, ey = xs, pts[-1][1]
        else:
            ex, ey = xs, pts[0][1]
        if len(pts) > 1:
            self.junction(ex, ey)
        if tail:
            self.wire(ex, ey, ex + side * tail, ey)
            ex = ex + side * tail
        self.power(name, ex, ey, 270 if name == "GND" else 90)

    def noconnect(self, x, y):
        self.items.append(["no_connect", ["at", round(x, 3), round(y, 3)], ["uuid", Q(U())]])

    def text(self, s, x, y, size=1.27):
        self.items.append(["text", Q(s), ["exclude_from_sim", "no"], ["at", x, y, 0],
                           ["effects", self._font(size), ["justify", "left", "top"]], ["uuid", Q(U())]])

    def place(self, lib_id, ref, value, fp, x, y, ang=0, nets=None, dnp=False, extra=None, fields=None, unit=1):
        """Поставить символ и на каждый вывод - метку цепи (или «не подключено»).
        nets: {номер вывода: имя цепи | "!NC" | "@GND"/"@+12V" (символ питания)}."""
        key = ref if unit == 1 else f"{ref}#{unit}"
        assert key not in self.refs and key not in ALL_REFS, "повтор обозначения " + key
        self.refs.add(key); ALL_REFS.add(key)
        sym = self.libs[lib_id]
        multi = any(re.search(r"_([2-9])_\d+$", str(u[1])) for u in find(sym, "symbol"))
        pins = pins_of(sym, unit if multi else None)
        props = [
            ["property", Q("Reference"), Q(ref), ["at", round((fields or {}).get("ref", (x + 3.81, y - 1.27))[0], 2),
             round((fields or {}).get("ref", (x + 3.81, y - 1.27))[1], 2), 0],
             ["effects", self._font()] + ([] if (fields or {}).get("just") == "center" else [["justify", (fields or {}).get("just", "left")]])],
            ["property", Q("Value"), Q(value), ["at", round((fields or {}).get("val", (x + 3.81, y + 1.27))[0], 2),
             round((fields or {}).get("val", (x + 3.81, y + 1.27))[1], 2), 0],
             ["effects", self._font()] + ([] if (fields or {}).get("just") == "center" else [["justify", (fields or {}).get("just", "left")]])],
            ["property", Q("Footprint"), Q(fp), ["at", x, y, 0], ["hide", "yes"], ["effects", self._font()]],
            ["property", Q("Datasheet"), Q(""), ["at", x, y, 0], ["hide", "yes"], ["effects", self._font()]],
        ]
        for k, v in (extra or {}).items():
            props.append(["property", Q(k), Q(v), ["at", x, y, 0], ["hide", "yes"], ["effects", self._font()]])
        inst = ["symbol", ["lib_id", Q(lib_id)], ["at", x, y, ang], ["unit", unit],
                ["exclude_from_sim", "no"], ["in_bom", "yes"], ["on_board", "yes"],
                ["dnp", "yes" if dnp else "no"], ["uuid", Q(U())]] + props
        inst += [["pin", Q(n), ["uuid", Q(U())]] for n in pins]
        inst.append(["instances", ["project", Q(self.project),
                     ["path", Q(self.path), ["reference", Q(ref)], ["unit", unit]]]])
        self.items.append(inst)
        for n, net in (nets or {}).items():
            px, py, pa = pins[str(n)]
            dx, dy = rot(px, py, ang)
            wx, wy = x + dx, y - dy               # Y в схеме направлен вниз
            out = (pa + ang + 180) % 360          # направление наружу от корпуса
            if net == "!NC":
                self.noconnect(wx, wy)
            elif net.startswith("@"):
                self.power(net[1:], wx, wy, out)
            else:
                lab_ang = {0: 0, 90: 90, 180: 180, 270: 270}[int(out)]
                self.label(net, wx, wy, lab_ang)

    def power(self, name, x, y, out_dir=270):
        if int(out_dir) % 180 == 0:          # вывод смотрит влево/вправо
            dx = 5.08 if int(out_dir) % 360 == 0 else -5.08
            self.wire(x, y, x + dx, y)
            x, out_dir = x + dx, (270 if name == "GND" else 90)
        lib_id = "power:" + name
        sym = self.libs[lib_id]
        n = len([i for i in self.items if i[0] == "symbol" and find(i, "lib_id")[0][1] == lib_id])
        ref = f"#PWR{len([r for r in ALL_REFS if r.startswith('#PWR')]) + 1:03d}"
        # символ земли «висит» вниз, +12V - вверх; поворачиваем по направлению вывода
        base = 270 if name == "GND" else 90
        ang = (out_dir - base) % 360
        self.refs.add(ref); ALL_REFS.add(ref)
        inst = ["symbol", ["lib_id", Q(lib_id)], ["at", round(x, 3), round(y, 3), int(ang)], ["unit", 1],
                ["exclude_from_sim", "no"], ["in_bom", "yes"], ["on_board", "yes"], ["dnp", "no"],
                ["uuid", Q(U())],
                ["property", Q("Reference"), Q(ref), ["at", x, y, 0], ["hide", "yes"], ["effects", self._font()]],
                ["property", Q("Value"), Q(name), ["at", x, round(y + (3.81 if name == "GND" else -3.81), 3), 0], ["effects", self._font()]],
                ["property", Q("Footprint"), Q(""), ["at", x, y, 0], ["hide", "yes"], ["effects", self._font()]],
                ["property", Q("Datasheet"), Q(""), ["at", x, y, 0], ["hide", "yes"], ["effects", self._font()]],
                ["pin", Q("1"), ["uuid", Q(U())]],
                ["instances", ["project", Q(self.project), ["path", Q(self.path), ["reference", Q(ref)], ["unit", 1]]]]]
        self.items.append(inst)

    def write(self, fname, title, comment, sheet_uuid):
        libsyms = []
        for lib_id, sym in self.libs.items():
            s = copy.deepcopy(sym); s[1] = Q(lib_id); libsyms.append(s)
        tb = ["title_block", ["title", Q(title)], ["date", Q("2026-11-24")], ["rev", Q("0.2 схема")],
              ["comment", 1, Q(comment)], ["comment", 2, Q("Техзадание: docs/k57-power-stage-spec-RU.md")]]
        root = ["kicad_sch", ["version", 20250610], ["generator", Q("eeschema")], ["generator_version", Q("10.0")],
                ["uuid", Q(sheet_uuid)], ["paper", Q(self.paper)], tb, ["lib_symbols"] + libsyms] + self.items + [["embedded_fonts", "no"]]
        guard(fname)
        with open(fname, "w", encoding="utf-8") as f:
            f.write(dump(root))
        remember(fname)
