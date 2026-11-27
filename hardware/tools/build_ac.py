# Плата 230 В (k57-ac): листы «Синус-фильтр», «Сетевой контактор и защита», «Измерения 230 В»,
# «Разъём к плате управления»; на верхнем листе метки GND/+12V заменяются символами питания.
# Запуск: python -I hardware/tools/build_ac.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os, copy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *
import sch_lib
from k57_links import AC_LINK, place_idc

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-ac"
PDIR = os.path.join(HW, PRJ)
COMMENT = "Синус-фильтр, сетевой контактор, измерения 230 В"
ROOTF = os.path.join(PDIR, PRJ + ".kicad_sch")

root = parse(open(ROOTF, encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
for s in find(root, "symbol"):                     # символы питания верхнего листа (после первого запуска)
    for p in find(s, "property"):
        if p[1] == "Reference":
            ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

def hier_of(fname):
    t = parse(open(os.path.join(PDIR, fname), encoding="utf-8").read())[0]
    return [(str(h[1]), str(find(h, "shape")[0][1])) for h in find(t, "hierarchical_label")], str(find(t, "uuid")[0][1])

def sheet_pins(fname):
    """Выводы блока листа на верхнем листе - в том порядке, как задумано структурой."""
    for sh in find(root, "sheet"):
        if [p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0] == fname:
            return [(str(p[1]), str(p[2])) for p in find(sh, "pin")]

# ---------------------------------------------------------------- свои символы
REL = lib("Relay", "Relay_SPST-NO")
rename_symbol(REL, "ALFG2PF12", "ALFG2PF12", "Relay_SPST-NO")
for u in find(REL, "symbol"):
    for p in find(u, "pin"):
        num = find(p, "number")[0]
        if str(num[1]) == "13":
            num[1] = Q("11")                       # у LF-G контакт 11-14 (как в посадочном месте)
set_prop(REL, "Value", "ALFG2PF12")
set_prop(REL, "Footprint", "Relay_THT:Relay_SPST_Panasonic_ALFG_FormA")
set_prop(REL, "Datasheet", "https://www3.panasonic.biz/ac/e_download/control/relay/power/catalog/mech_eng_lfg.pdf")
set_prop(REL, "Description", "Panasonic LF-G, 1 Form A, 22 А 250 В~, зазор контактов 1,8 мм (для солнечных инверторов), катушка 12 В")

def ct_symbol():
    name = "CT_1000"
    st = ["stroke", ["width", 0.254], ["type", "default"]]
    body = ["symbol", Q(name + "_0_1"),
            ["rectangle", ["start", -2.54, 5.08], ["end", 5.08, -5.08], st, ["fill", ["type", "background"]]],
            ["circle", ["center", 1.27, 0], ["radius", 1.905], st, ["fill", ["type", "none"]]]]
    unit = ["symbol", Q(name + "_1_1")]
    for n, y in (("1", 2.54), ("2", -2.54)):
        unit.append(["pin", "passive", "line", ["at", -5.08, y, 0], ["length", 2.54],
                     ["name", Q("S" + n), ["effects", ["font", ["size", 1.27, 1.27]]]],
                     ["number", Q(n), ["effects", ["font", ["size", 1.27, 1.27]]]]])
    def prop(k, v, at, hide=True):
        p = ["property", Q(k), Q(v), ["at"] + at, ["effects", ["font", ["size", 1.27, 1.27]]]]
        if hide: p.insert(4, ["hide", "yes"])
        return p
    return ["symbol", Q(name), ["pin_names", ["offset", 0], ["hide", "yes"]],
            ["exclude_from_sim", "no"], ["in_bom", "yes"], ["on_board", "yes"],
            prop("Reference", "T", [6.35, 3.81, 0], False),
            prop("Value", name, [6.35, -3.81, 0], False),
            prop("Footprint", "Transformer_THT:Transformer_Zeming_ZMCT103C", [0, 0, 0]),
            prop("Datasheet", "", [0, 0, 0]),
            prop("Description", "Трансформатор тока 1:1000 с окном: провод первичной цепи проходит через окно, "
                                "на плату - только выводы вторичной обмотки", [0, 0, 0]),
            body, unit]
CT = ct_symbol()
upsert_lib(os.path.join(HW, "lib", "k57.kicad_sym"), [REL, CT])

BASE = {"Device:R": ("Device", "R"), "Device:C": ("Device", "C"),
        "power:GND": ("power", "GND"), "power:+12V": ("power", "+12V"), "power:PWR_FLAG": ("power", "PWR_FLAG")}
def new_sheet(fname, extra, paper="A3"):
    sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[fname], paper=paper)
    for lid, v in {**BASE, **extra}.items():
        sh.add_lib(lid, v if isinstance(v, list) else lib(*v))
    return sh

R0603, R1206 = "Resistor_SMD:R_0603_1608Metric", "Resistor_SMD:R_1206_3216Metric"
C0603, C1206 = "Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_1206_3216Metric"
TB2 = "TerminalBlock_Phoenix:TerminalBlock_Phoenix_MKDS-3-2-5.08_1x02_P5.08mm_Horizontal"
TERM = "Connector:Screw_Terminal_01x02"
POWER = ("GND", "+12V")

# ================================================================ лист «Синус-фильтр»
F = "sine_filter.kicad_sch"
HIER, FU = hier_of(F)
sh = new_sheet(F, {TERM: ("Connector", "Screw_Terminal_01x02")})
hier_block(sh, sheet_pins(F), POWER)
sh.place(TERM, "J101", "Вторичка трансформатора (L1 - его рассеяние)", TB2, 101.6, 101.6,
         nets={"1": "XS_L", "2": "OUT_N"}, fields={"ref": (101.6, 91.44), "val": (101.6, 93.98), "just": "center"})
for i, ref in enumerate(("C101", "C102")):
    sh.place("Device:C", ref, "1uF X2 310VAC", "Capacitor_THT:C_Rect_L26.5mm_W10.5mm_P22.50mm_MKS4",
             160.02 + i * 25.4, 101.6, nets={"1": "XS_L", "2": "C_DAMP"})
sh.place("Device:R", "R101", "8.2 10W", "Resistor_THT:R_Axial_Power_L48.0mm_W12.5mm_P55.88mm", 172.72, 132.08,
         nets={"1": "C_DAMP", "2": "OUT_N"}, extra={"Part": "проволочный или керамический, 10 Вт"})
sh.place(TERM, "J102", "К дросселю L2 (на шасси)", TB2, 254.0, 101.6,
         nets={"1": "XS_L", "2": "OUT_L"}, fields={"ref": (254.0, 91.44), "val": (254.0, 93.98), "just": "center"})
sh.text("СИНУС-ФИЛЬТР LCL (техзадание 6.8) - и для режима АИН, и для работы на сеть\n"
        "L1 - рассеяние трансформатора, отдельного дросселя нет. [?] Требование к трансформатору: 3-5 мГн к вторичке.\n"
        "C = C101 + C102 = 2 мкФ, плёнка X2 310 В~ (класс WIMA MKP-X2, шаг 22,5 мм). R101 8,2 Ом 10 Вт - демпфирование\n"
        "  резонанса (~1/(3*w*C)); пульсации 10 кГц греют его на ~4 Вт. Конденсатор разряжается через обмотку трансформатора.\n"
        "L2 - сетевой дроссель 1,5 мГн, 13-15 А действующих, на шасси: J102.1 - начало, J102.2 - конец.\n"
        "OUT_L / OUT_N - выход после всего фильтра: к реле сети и к датчику uSec (лист «Измерения 230 В»).\n"
        "[?] Номиналы - проверить моделью, когда будет трансформатор. ЭМС-фильтр (150 кГц - 30 МГц) - не нарисован.\n"
        "Всё на листе - под сетевым напряжением: зазоры и пути утечки - как для 230 В.", 63.5, 165.1)
sh.write(os.path.join(PDIR, F), "K57 плата 230 В: Синус-фильтр", COMMENT, FU)

# ================================================================ лист «Сетевой контактор и защита»
F = "grid_switch.kicad_sch"
HIER, FU = hier_of(F)
sh = new_sheet(F, {"k57:ALFG2PF12": REL, "k57:CT_1000": CT, TERM: ("Connector", "Screw_Terminal_01x02"),
                   "Transistor_FET:AO3400A": ("Transistor_FET", "AO3400A"), "Diode:1N4148W": ("Diode", "1N4148W"),
                   "Device:Varistor": ("Device", "Varistor")})
hier_block(sh, sheet_pins(F), POWER)
YL, YN, KX = 114.3, 165.1, 165.1           # линии L и N, реле - в одну колонку
XS, XG = 88.9, 177.8                        # начало линий (метки OUT_L/OUT_N), начало сетевой стороны
# трансформатор тока: провод OUT_L проходит через окно - линия L нарисована сквозь символ
sh.place("k57:CT_1000", "T201", "ТТ 1:1000 [?]", "Transformer_THT:Transformer_Zeming_ZMCT103C", 127.0, YL,
         nets={"1": "IB_CT_P", "2": "IB_CT_N"}, fields={"ref": (127.0, YL - 10.16), "val": (127.0, YL - 7.62), "just": "center"})
for i, (yl, ref, dref, src, dst) in enumerate(((YL, "K201", "D201", "OUT_L", "GRID_L"), (YN, "K202", "D202", "OUT_N", "GRID_N"))):
    ky = yl - 5.08
    sh.place("k57:ALFG2PF12", ref, "ALFG2PF12", "Relay_THT:Relay_SPST_Panasonic_ALFG_FormA", KX, ky, ang=270,
             nets={"11": None, "14": None, "A1": None, "A2": None},
             fields={"ref": (KX + 15.24, ky - 1.27), "val": (KX + 15.24, ky + 1.27), "just": "left"})
    sh.label(src, XS, yl, 180)
    sh.wire(XS, yl, KX - 7.62, yl)                                    # вход контакта (11)
    sh.wire(KX + 7.62, yl + 2.54, XG, yl + 2.54); sh.wire(XG, yl + 2.54, XG, yl)   # выход контакта (14)
    sh.label(dst, XG + 12.7, yl, 0)
    # катушка: A1 - к +12V, A2 - к ключу Q201; диод - прямо на выводах катушки
    yd = yl - 20.32
    sh.place("Diode:1N4148W", dref, "1N4148W", "Diode_SMD:D_SOD-123", KX, yd, ang=180, nets={"1": None, "2": None},
             fields={"ref": (KX, yd - 6.35), "val": (KX, yd - 3.81), "just": "center"})
    sh.wire(KX - 7.62, ky - 5.08, KX - 7.62, yd); sh.wire(KX - 7.62, yd, KX - 3.81, yd)
    sh.wire(KX + 7.62, ky - 5.08, KX + 7.62, yd); sh.wire(KX + 7.62, yd, KX + 3.81, yd)
    sh.wire(KX + 7.62, yd, KX + 7.62, yd - 5.08); sh.power("+12V", KX + 7.62, yd - 5.08, 90)
    sh.junction(KX + 7.62, yd)
    if i == 0:
        YC = yl - 30.48                                               # COIL_N: от A2 к стоку Q201
        sh.wire(KX - 7.62, yd, KX - 7.62, YC); sh.wire(KX - 7.62, YC, 142.24, YC)
        sh.label("COIL_N", 147.32, YC, 0)
    else:
        sh.wire(KX - 7.62, yd, KX - 7.62, yd - 5.08); sh.label("COIL_N", KX - 7.62, yd - 5.08, 90)
    sh.junction(KX - 7.62, yd)
# ключ катушек
QY = YL - 25.4
sh.place("Transistor_FET:AO3400A", "Q201", "AO3400A", "Package_TO_SOT_SMD:SOT-23", 139.7, QY,
         nets={"1": None, "2": "@GND", "3": None}, fields={"ref": (144.78, QY - 1.27), "val": (144.78, QY + 1.27), "just": "left"})
sh.place("Device:R", "R201", "100", R0603, 127.0, QY, ang=90, nets={"1": "GRID_CONT_EN", "2": None},
         fields={"ref": (127.0, QY - 3.81), "val": (127.0, QY + 3.81), "just": "center"})
sh.wire(130.81, QY, 134.62, QY)
sh.place("Device:R", "R202", "100k", R0603, 132.08, QY + 6.35, nets={"1": None, "2": "@GND"},
         fields={"ref": (128.27, QY + 8.89), "val": (128.27, QY + 11.43), "just": "right"})
sh.wire(132.08, QY, 132.08, QY + 2.54); sh.junction(132.08, QY)
sh.place("Device:C", "C201", "10uF 25V X7R", C1206, 195.58, QY, nets={"1": "@+12V", "2": "@GND"})
# сетевая сторона: варистор между L и N, клеммы J201
XRV, XJL, XJN = 210.82, 238.76, 233.68
sh.place("Device:Varistor", "RV201", "S20K275", "Varistor:RV_Disc_D21.5mm_W5.1mm_P10mm", XRV, 139.7, nets={"1": None, "2": None})
sh.wire(XRV, 135.89, XRV, YL); sh.wire(XRV, 143.51, XRV, YN)
sh.wire(XG, YL, XJL, YL); sh.junction(XRV, YL)
sh.wire(XG, YN, XJN, YN); sh.junction(XRV, YN)
JY = 139.7
sh.place(TERM, "J201", "Сеть / нагрузка (через внешний автомат)", TB2, 248.92, JY, nets={"1": None, "2": None},
         fields={"ref": (246.38, JY - 7.62), "val": (246.38, JY - 5.08), "just": "left"})
sh.wire(XJL, YL, XJL, JY); sh.wire(XJL, JY, 243.84, JY)
sh.wire(XJN, YN, XJN, JY + 2.54); sh.wire(XJN, JY + 2.54, 243.84, JY + 2.54)
sh.text("СЕТЕВОЙ КОНТАКТОР И ЗАЩИТА (техзадание 6.6)\n"
        "Линия L: OUT_L (после синус-фильтра) -> окно ТТ T201 -> контакт K201 -> GRID_L -> J201. Линия N - так же через K202.\n"
        "K201 (L) и K202 (N) - реле Panasonic LF-G ALFG2PF12: 22 А, зазор контактов 1,8 мм, сделаны для солнечных инверторов.\n"
        "  Разрываются оба провода. Катушки 12 В соединены параллельно, включает их Q201 по GRID_CONT_EN (GPIO59).\n"
        "  R202 держит Q201 закрытым при сбросе МК и без шлейфа: реле по умолчанию разомкнуты. D201/D202 - на выбег катушек.\n"
        "  [?] Ток катушки LF-G ~117 мА (1,4 Вт); удержание можно снизить ШИМ - пока не нужно.\n"
        "T201 - трансформатор тока Ib 1:1000: провод OUT_L проходит через окно (до реле - ток инвертора в обоих режимах).\n"
        "  Нагрузка ТТ и защита - на листе «Измерения 230 В». [?] Модель ТТ: шкала ±40 А пик, класс ZMCT103C мал - подобрать.\n"
        "RV201 S20K275 - варистор от сетевых выбросов, у клемм J201.\n"
        "Предохранителя на плате нет: держатель 5x20 мм не держит 14,5 А длительно. Защита от КЗ - внешний\n"
        "  автомат C20 между J201 и сетью. [?] Подтвердить (раздел 8 техзадания).", 63.5, 200.66)
sh.write(os.path.join(PDIR, F), "K57 плата 230 В: Сетевой контактор и защита", COMMENT, FU)

# ================================================================ лист «Измерения 230 В»
F = "meas_230v.kicad_sch"
HIER, FU = hier_of(F)
sh = new_sheet(F, {"Isolator_Analog:AMC3330": ("Isolator_Analog", "AMC3330"),
                   "Device:FerriteBead_Small": ("Device", "FerriteBead_Small"),
                   "Diode:SMAJ5.0CA": ("Diode", "SMAJ5.0CA")}, paper="A2")
hier_block(sh, sheet_pins(F), POWER)
AMC = "Isolator_Analog:AMC3330"

def amc_channel(n, Y, hot, cold, outp, outn, flag_ref):
    """Канал напряжения на AMC3330 проводами: делитель 4 x 1 МОм + 10 кОм, развязка по SBASA34B (рис. 7-5).
    Высокая сторона (слева) - под напряжением своей цепи; cold - её N, к нему HGND, INN, DCDC_HGND."""
    XA = 254.0
    sh.place(AMC, f"U{n + 1}", "AMC3330DWE", "Package_SO:SOIC-16W_7.5x10.3mm_P1.27mm", XA, Y,
             nets={"4": "!NC", "14": "!NC", "9": "@GND", "2": cold,
                   **{k: None for k in ("1", "3", "5", "6", "7", "8", "10", "11", "12", "13", "15", "16")}},
             fields={"ref": (XA - 12.7, Y - 22.86), "val": (XA - 12.7, Y - 20.32), "just": "left"})
    P = lambda k: sh.pin_xy(AMC, XA, Y, 0, k)
    (x1, y1), (x3, y3), (x5, y5), (x6, y6), (x7, y7), (x8, y8) = P(1), P(3), P(5), P(6), P(7), P(8)
    # DCDC_OUT (1) и HLDO_IN (3) - вместе; 1 мкФ + 1 нФ + 100 нФ к HGND (конденсаторы над линией)
    XJ = XA - 30.48
    sh.wire(x3, y3, XJ, y3); sh.wire(XJ, y3, XJ, y1)
    hdc = [XA - 43.18, XA - 55.88, XA - 68.58]
    sh.wire(x1, y1, hdc[-1], y1); sh.junction(XJ, y1)
    for (ref, val, fp), x in zip([(f"C{n + 2}", "1uF", C0603), (f"C{n + 3}", "1nF", C0603), (f"C{n + 4}", "100nF", C0603)], hdc):
        sh.place("Device:C", ref, val, fp, x, y1 - 3.81, ang=180, nets={"1": None, "2": cold})
        if x != hdc[-1]:
            sh.junction(x, y1)
    # HLDO_OUT (5): 100 нФ NP0 + 1 нФ к HGND
    hl = [XA - 76.2, XA - 91.44]
    sh.wire(x5, y5, hl[-1], y5)
    for (ref, val, fp), x in zip([(f"C{n + 5}", "100nF NP0", C1206), (f"C{n + 6}", "1nF", C1206)], hl):
        sh.place("Device:C", ref, val, fp, x, y5 - 3.81, ang=180, nets={"1": None, "2": cold})
    sh.junction(hl[0], y5)
    # INP (6): делитель 4 x 1 МОм от hot, 10 кОм и 1 нФ к INN (7); INN = HGND = cold
    XR5, XC1, XCOLD = XA - 30.48, XA - 38.1, XA - 50.8
    xs = [XA - 106.68 + i * 10.16 for i in range(4)]
    for i, x in enumerate(xs):
        sh.place("Device:R", f"R{n + 1 + i}", "1M 1%", R1206, x, y6, ang=90,
                 nets={"1": hot if i == 0 else None, "2": None},
                 fields={"ref": (x, y6 + 3.81), "val": (x, y6 + 6.35), "just": "center"})
    for xa, xb in zip(xs, xs[1:]):
        sh.wire(xa + 3.81, y6, xb - 3.81, y6)
    sh.wire(xs[-1] + 3.81, y6, x6, y6)
    sh.place("Device:R", f"R{n + 5}", "10k 0.1%", R0603, XR5, y6 + 3.81, nets={"1": None, "2": None})
    sh.place("Device:C", f"C{n + 1}", "1nF C0G", C0603, XC1, y6 + 3.81, nets={"1": None, "2": None},
             fields={"ref": (XC1 - 3.81, y6 + 2.54), "val": (XC1 - 3.81, y6 + 5.08), "just": "right"})
    sh.junction(XR5, y6); sh.junction(XC1, y6)
    sh.wire(x7, y7, XCOLD, y7); sh.junction(XR5, y7); sh.junction(XC1, y7)
    sh.label(cold, XCOLD, y7, 180)
    # HGND (8) - снизу к линии INN
    sh.wire(x8, y8, x8, y8 + 2.54); sh.wire(x8, y8 + 2.54, XA - 20.32, y8 + 2.54); sh.wire(XA - 20.32, y8 + 2.54, XA - 20.32, y7)
    sh.junction(XA - 20.32, y7)
    # низкая сторона: LDO_OUT (13) и DCDC_IN (16) вместе, 100 нФ до DCDC_GND (15)
    (x13, y13), (x16, y16), (x15, y15), (x11, y11), (x10, y10), (x12, y12) = P(13), P(16), P(15), P(11), P(10), P(12)
    XC7 = XA + 25.4
    sh.wire(x13, y13, XC7, y13)
    sh.wire(x16, y16, XC7 - 2.54, y16); sh.wire(XC7 - 2.54, y16, XC7 - 2.54, y13); sh.junction(XC7 - 2.54, y13)
    sh.place("Device:C", f"C{n + 7}", "100nF", C0603, XC7, y13 + 3.81, nets={"1": None, "2": None})
    sh.wire(x15, y15, XC7 + 5.08, y15); sh.junction(XC7, y15)
    sh.power("GND", XC7 + 5.08, y15, 270)
    # выходы через 49,9 Ом на кабель
    XRO = XA + 38.1
    for (xo, yo), ref, net, fy in (((x11, y11), f"R{n + 6}", outp, (-6.35, -3.81)), ((x10, y10), f"R{n + 7}", outn, (3.81, 6.35))):
        sh.wire(xo, yo, XRO - 3.81, yo)
        sh.place("Device:R", ref, "49.9", R0603, XRO, yo, ang=90, nets={"1": None, "2": net},
                 fields={"ref": (XRO, yo + fy[0]), "val": (XRO, yo + fy[1]), "just": "center"})
    # VDD (12): вверх к линии питания, 1 нФ + 1 мкФ к GND, бусина от +3V3A, флажок питания
    YV = Y - 27.94
    sh.wire(x12, y12, x12, YV)
    XB = XA + 53.34
    sh.wire(x12, YV, XB - 2.54, YV)
    sh.place("power:PWR_FLAG", flag_ref, "PWR_FLAG", "", XA + 15.24, YV, nets={"1": None},
             fields={"ref": (XA + 15.24, YV - 7.62), "val": (XA + 15.24, YV - 5.08), "just": "center"})
    sh.junction(XA + 15.24, YV)
    for (ref, val, fp), x in zip([(f"C{n + 8}", "1nF", C1206), (f"C{n + 9}", "1uF", C1206)], (XA + 30.48, XA + 43.18)):
        sh.place("Device:C", ref, val, fp, x, YV + 3.81, nets={"1": None, "2": "@GND"})
        sh.junction(x, YV)
    sh.place("Device:FerriteBead_Small", f"FB{n + 1}", "BLM15HD182SH1", "Inductor_SMD:L_0402_1005Metric", XB, YV, ang=270,
             nets={"1": "+3V3A", "2": None}, fields={"ref": (XB, YV - 7.62), "val": (XB, YV - 5.08), "just": "center"})

amc_channel(310, 101.6, "GRID_L", "GRID_N", "U220_P", "U220_N", "#FLG301")
amc_channel(330, 223.52, "OUT_L", "OUT_N", "USEC_P", "USEC_N", "#FLG302")
# Ib: нагрузка трансформатора тока - всегда на месте, даже без шлейфа (ТТ без нагрузки опасен)
XI = 434.34
sh.place("Device:R", "R301", "24.9 0.1%", R1206, XI, 76.2, nets={"1": "IB_CT_P", "2": "IB_CT_N"})
sh.place("Diode:SMAJ5.0CA", "D301", "SMAJ5.0CA", "Diode_SMD:D_SMA", XI + 17.78, 76.2, ang=90,
         nets={"2": "IB_CT_P", "1": "IB_CT_N"}, fields={"ref": (XI + 20.32, 74.93), "val": (XI + 20.32, 77.47), "just": "left"})
sh.place("Device:R", "R302", "49.9", R0603, XI + 40.64, 76.2, nets={"1": "IB_CT_P", "2": "IB_P"})
sh.place("Device:R", "R303", "49.9", R0603, XI + 55.88, 76.2, nets={"1": "IB_CT_N", "2": "IB_N"})
sh.text("ИЗМЕРЕНИЯ 230 В (техзадание 6.5): U220 - сеть (после реле со стороны сети), uSec - выход после синус-фильтра\n"
        "U311, U331 - AMC3330: усиленная изоляция, свой изолированный DC/DC - питания со стороны 230 В не нужно (SBASA34B).\n"
        "  Делитель 4 x 1 МОм + 10 кОм: 400 В пик -> 1,0 В на входе (линейный диапазон ±1 В, ограничение ±1,25 В = 500 В).\n"
        "  Ток делителя 100 мкА при 400 В - предел TI. Резисторы 1206 по 100 В каждый. [?] Для выбросов - резисторы\n"
        "  на высокое напряжение (класс Vishay CRHV) или MELF.\n"
        "  1 нФ C0G на входе (рис. 7-2): 10 мкс задержки, одинаково в обоих каналах - U220 и uSec сравниваются без сдвига.\n"
        "  Развязка (X7R 25-50 В, 100 нФ на HLDO - NP0) - по рис. 7-5 и табл. 7-3: VDD 1 нФ + 1 мкФ, DC/DC 100 нФ / 1 мкФ + 1 нФ, HLDO 100 нФ и 100 нФ NP0 + 1 нФ.\n"
        "  INN, HGND, DCDC_HGND - на нижний вывод резистора 10 кОм (N своей цепи). DIAG не используется.\n"
        "  Выход: дифференциальный, усиление 2 (±2 В при ±400 В), синфазное 1,44 В; 49,9 Ом на кабель.\n"
        "  Питание 3,3 В (+3V3A со шлейфа) через бусину на каждый AMC3330: до 41 мА на корпус.\n"
        "Ib: ТТ T201 1:1000, нагрузка R301 24,9 Ом: 40 А пик -> 1,0 В. D301 ограничивает выбросы при КЗ.\n"
        "  Перевод в однополярный 1,5 ± 1,0 В - на плате управления (лист «Приёмники сигналов 230 В»).\n"
        "Барьер: всё левее и выше AMC3330 - под сетевым напряжением; путь утечки до низкой стороны - не меньше 8 мм\n"
        "  (корпус SOIC-16W), под корпусом - без меди. [?] Зазоры - по IEC 62109-1.", 101.6, 287.02)
sh.write(os.path.join(PDIR, F), "K57 плата 230 В: Измерения 230 В", COMMENT, FU)

# ================================================================ лист «Разъём к плате управления»
F = "link_ctrl.kicad_sch"
HIER, FU = hier_of(F)
CONN = "Connector_Generic:Conn_02x08_Odd_Even"
sh = new_sheet(F, {CONN: ("Connector_Generic", "Conn_02x08_Odd_Even")})
hier_block(sh, sheet_pins(F), POWER)
CX, CY = 177.8, 101.6
place_idc(sh, CONN, "J401", "К плате управления (IDC 2x8)", "Connector_IDC:IDC-Header_2x08_P2.54mm_Vertical",
          CX, CY, AC_LINK, fields={"ref": (CX, 83.82), "val": (CX, 86.36), "just": "center"})
for i, net in enumerate(["@+12V", "@GND", "+3V3A"]):
    flag(sh, net, 269.24 + i * 17.78, 96.52, f"#FLG{401 + i}")
sh.text("РАЗЪЁМ К ПЛАТЕ УПРАВЛЕНИЯ (k57-control, лист «Разъём к плате 230 В», J801)\n"
        "J401 - IDC 2x8, плоский шлейф, контакт 1 - в контакт 1. Распиновка - общая (hardware/tools/k57_links.py, AC_LINK),\n"
        "  сверяется скриптом check_links.py. Провода по порядку: 1 GND, 2-3 Ib, 4 GND, 5-6 U220, 7 GND, 8-9 uSec, 10 GND,\n"
        "  11 GRID_CONT_EN, 12 GND, 13 +3V3A, 14 GND, 15-16 +12V. Каждая дифференциальная пара - соседние провода.\n"
        "Всё на этом листе - низковольтное, по ту сторону барьера от 230 В.\n"
        "PWR_FLAG - пометка для проверки схемы: питание приходит с платы управления.", 81.28, 165.1)
sh.write(os.path.join(PDIR, F), "K57 плата 230 В: Разъём к плате управления", COMMENT, FU)

# ================================================================ верхний лист: GND и +12V - символами питания
labels = [l for l in find(root, "label") if str(l[1]) in POWER]
if labels:
    rs = Sheet(PRJ, "/" + ROOT)
    for lid in ("power:GND", "power:+12V"):
        rs.add_lib(lid, lib("power", lid.split(":")[1]))
    for l in labels:
        x, y, a = (float(v) for v in find(l, "at")[0][1:4])
        rs.power(str(l[1]), x, y, int(a))           # отвод 5,08 мм наружу и символ
    root[:] = [e for e in root if not (isinstance(e, list) and e and e[0] == "label" and str(e[1]) in POWER)]
    ls = find(root, "lib_symbols")[0]
    for lid, sym in rs.libs.items():
        s = copy.deepcopy(sym); s[1] = Q(lid); ls.append(s)
    i = next(k for k, e in enumerate(root) if isinstance(e, list) and e and e[0] == "sheet")
    root[i:i] = rs.items
    guard(ROOTF)
    open(ROOTF, "w", encoding="utf-8").write(dump(root))
    remember(ROOTF)
print("ok")
