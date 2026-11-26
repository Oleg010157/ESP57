# Листы «Драйверы» и «Мост» платы k57-power + библиотека hardware/lib/k57.kicad_sym
import sys, os, re, copy
sys.path.insert(0, os.path.dirname(__file__))
from sch_lib import *

HW = sys.argv[1]
KL = sys.argv[2]                       # .../share/kicad/symbols
PRJ = "k57-power"
PDIR = os.path.join(HW, PRJ)
G = 2.54

# ---------------------------------------------------------------- свои символы
def ucc27282():
    pins = [  # номер, имя, тип, сторона, y
        ("7", "HI", "input", "L", 7.62), ("8", "LI", "input", "L", 5.08), ("6", "EN", "input", "L", 2.54),
        ("1", "VDD", "power_in", "L", -2.54), ("9", "VSS", "power_in", "L", -7.62), ("11", "EP", "passive", "B", -15.24),
        ("3", "HB", "passive", "R", 7.62), ("4", "HO", "output", "R", 5.08), ("5", "HS", "passive", "R", 2.54),
        ("10", "LO", "output", "R", -2.54), ("2", "NC", "no_connect", "R", -7.62)]
    body = ["symbol", Q("UCC27282DRC_0_1"),
            ["rectangle", ["start", -10.16, 10.16], ["end", 10.16, -12.7],
             ["stroke", ["width", 0.254], ["type", "default"]], ["fill", ["type", "background"]]]]
    unit = ["symbol", Q("UCC27282DRC_1_1")]
    for num, name, typ, side, y in pins:
        x, a = {"L": (-12.7, 0), "R": (12.7, 180), "B": (0, 90)}[side]
        unit.append(["pin", typ, "line", ["at", x, y, a], ["length", 2.54],
                     ["name", Q(name), ["effects", ["font", ["size", 1.27, 1.27]]]],
                     ["number", Q(num), ["effects", ["font", ["size", 1.27, 1.27]]]]])
    def prop(k, v, at, hide=True):
        p = ["property", Q(k), Q(v), ["at"] + at, ["effects", ["font", ["size", 1.27, 1.27]]]]
        if hide: p.insert(4, ["hide", "yes"])
        return p
    return ["symbol", Q("UCC27282DRC"), ["pin_names", ["offset", 1.016]],
            ["exclude_from_sim", "no"], ["in_bom", "yes"], ["on_board", "yes"],
            prop("Reference", "U", [0, 12.7, 0], False), prop("Value", "UCC27282DRC", [0, -15.24, 0], False),
            prop("Footprint", "Package_SON:Texas_DRC0010J_ThermalVias", [0, -17.78, 0]),
            prop("Datasheet", "https://www.ti.com/lit/ds/symlink/ucc27282.pdf", [0, -20.32, 0]),
            prop("Description", "120 В, 3 А полумостовой драйвер с блокировкой и выводом EN, VSON-10", [0, -22.86, 0]),
            body, unit]

def irf100p218():
    s = lib_symbol(os.path.join(KL, "Transistor_FET.kicad_sym"), "BUZ11")
    rename_symbol(s, "IRF100P218", "IRF100P218", "BUZ11")
    set_prop(s, "Value", "IRF100P218")
    set_prop(s, "Footprint", "Package_TO_SOT_THT:TO-247-3_Vertical")
    set_prop(s, "Datasheet", "https://www.infineon.com/assets/row/public/documents/24/49/infineon-irf100p218-datasheet-en.pdf")
    set_prop(s, "Description", "N-MOSFET 100 В, 1,28 мОм, 209 А (корпус), TO-247-3")
    # убрать фильтр посадочных мест от TO-220
    s[:] = [e for e in s if not (isinstance(e, list) and e and e[0] == "property" and e[1] == "ki_fp_filters")]
    return s

os.makedirs(os.path.join(HW, "lib"), exist_ok=True)
upsert_lib(os.path.join(HW, "lib", "k57.kicad_sym"), [ucc27282(), irf100p218()])
for prj in ["k57-power", "k57-control", "k57-ac"]:
    with open(os.path.join(HW, prj, "sym-lib-table"), "w", encoding="utf-8") as f:
        f.write('(sym_lib_table\n  (version 7)\n  (lib (name "k57")(type "KiCad")(uri "${KIPRJMOD}/../lib/k57.kicad_sym")(options "")(descr "Символы проекта K57"))\n)\n')

def std(lib, name):
    return lib_symbol(os.path.join(KL, lib + ".kicad_sym"), name)

# ---------------------------------------------------------------- пути листов
root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
root_uuid = str(find(root, "uuid")[0][1])
sheet_uuid = {}
for sh in find(root, "sheet"):
    fname = [p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]
    sheet_uuid[str(fname)] = str(find(sh, "uuid")[0][1])
def file_uuid(fname):
    t = parse(open(os.path.join(PDIR, fname), encoding="utf-8").read())[0]
    return str(find(t, "uuid")[0][1])

def new_sheet(fname):
    sh = Sheet(PRJ, "/" + root_uuid + "/" + sheet_uuid[fname])
    sh.add_lib("Device:R", std("Device", "R"))
    sh.add_lib("Device:C", std("Device", "C"))
    sh.add_lib("power:GND", std("power", "GND"))
    sh.add_lib("power:+12V", std("power", "+12V"))
    return sh

import sch_lib
def hier_block(sh, specs):
    """Иерархические метки слева; каждая проводком к локальной метке или символу питания."""
    sch_lib.hier_block(sh, specs, ("GND", "+12V"))

R0603, R1206 = "Resistor_SMD:R_0603_1608Metric", "Resistor_SMD:R_1206_3216Metric"
C0603, C0805, C1206, C1210 = ("Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_0805_2012Metric",
                              "Capacitor_SMD:C_1206_3216Metric", "Capacitor_SMD:C_1210_3225Metric")

# ================================================================ ДРАЙВЕРЫ
sh = new_sheet("drivers.kicad_sch")
sh.add_lib("k57:UCC27282DRC", ucc27282())
hier_block(sh, [("PWM1A", "input"), ("PWM1B", "input"), ("PWM2A", "input"), ("PWM2B", "input"),
                ("DRV_EN", "input"), ("+12V", "passive"), ("GND", "passive"),
                ("GATE_HA", "output"), ("GATE_LA", "output"), ("GATE_HB", "output"), ("GATE_LB", "output"),
                ("PHASE_A", "passive"), ("PHASE_B", "passive")])

def driver(n, leg, pwm_hi, pwm_lo, gh, gl, phase, y):
    """Один UCC27282 с обвязкой. n = 0 - плечо A (U101, R101.., C101..), 1 - плечо B (U102, R111.., C111..)."""
    b = 100 + 10 * n
    X = 200.66
    hi, li, en, hb = f"HI_{leg}", f"LI_{leg}", f"EN_{leg}", f"HB_{leg}"
    sh.place("k57:UCC27282DRC", f"U{101 + n}", "UCC27282DRC", "Package_SON:Texas_DRC0010J_ThermalVias", X, y,
             nets={"7": hi, "8": li, "6": en, "1": "@+12V", "9": "@GND", "11": "@GND",
                   "3": hb, "4": gh, "5": phase, "10": gl, "2": "!NC"},
             fields={"ref": (X, y - 13.97), "val": (X + 7.62, y + 15.24), "just": "center"})
    # входы: последовательный резистор + подтяжка к земле (ключи закрыты, пока МК в сбросе)
    cx = 81.28
    for i, (src, dst, rs, rp) in enumerate([(pwm_hi, hi, "01", "02"), (pwm_lo, li, "03", "04"), ("DRV_EN", en, "05", "06")]):
        x = cx + i * 25.4
        sh.place("Device:R", f"R{b + int(rs)}", "47", R0603, x, y - 12.7, nets={"1": src, "2": dst})
        sh.place("Device:R", f"R{b + int(rp)}", "10k", R0603, x + 10.16, y - 12.7, nets={"1": dst, "2": "@GND"})
    sh.place("Device:C", f"C{b + 1}", "1nF 50V X7R", C0603, cx + 2 * 25.4 + 10.16, y + 12.7, nets={"1": en, "2": "@GND"})
    # бутстреп: 4,7 мкФ + 1 нФ между HB и HS
    bx = 254.0
    sh.place("Device:C", f"C{b + 2}", "4.7uF 25V X7R", C1206, bx, y, nets={"1": hb, "2": phase})
    sh.place("Device:C", f"C{b + 3}", "1nF 50V X7R", C0603, bx + 25.4, y, nets={"1": hb, "2": phase})
    # питание драйвера: 22 мкФ + 1 мкФ + 1 нФ
    vx = 132.08
    sh.place("Device:C", f"C{b + 4}", "22uF 25V X7R", C1210, vx, y + 25.4, nets={"1": "@+12V", "2": "@GND"})
    sh.place("Device:C", f"C{b + 5}", "1uF 25V X7R", C0805, vx + 25.4, y + 25.4, nets={"1": "@+12V", "2": "@GND"})
    sh.place("Device:C", f"C{b + 6}", "1nF 50V X7R", C0603, vx + 50.8, y + 25.4, nets={"1": "@+12V", "2": "@GND"})

driver(0, "A", "PWM1A", "PWM1B", "GATE_HA", "GATE_LA", "PHASE_A", 81.28)
driver(1, "B", "PWM2A", "PWM2B", "GATE_HB", "GATE_LB", "PHASE_B", 182.88)
sh.text("ДРАЙВЕРЫ ЗАТВОРОВ (техзадание 6.2)\n"
        "U101 - плечо A (EPWM1), U102 - плечо B (EPWM2). Уровень 1 = ключ открыт (IGBT_ACTIVE_HIGH = 1).\n"
        "Входы HI/LI/EN: 47 Ом последовательно + 10 кОм к земле - пока МК в сбросе, ключи закрыты.\n"
        "EN: 1 нФ к земле - рекомендация TI для зашумлённых систем.\n"
        "Бутстреп: 2 затвора x 330 нК + утечки за самый длинный импульс (~195 мкс) = ~0,83 мкКл;\n"
        "  просадка <= 0,5 В -> >= 1,7 мкФ; 4,7 мкФ X7R 25 В (под 12 В ~2,8 мкФ) + 1 нФ рядом.\n"
        "Питание: 22 мкФ + 1 мкФ + 1 нФ у VDD-VSS (>= 10 x бутстреп).\n"
        "Теплоотводящая площадка (EP) - только к VSS. Драйвер - вплотную к ключам своего плеча,\n"
        "HS - к истокам верхних ключей кратчайшим путём.", 81.28, 236.22)
sh.write(os.path.join(PDIR, "drivers.kicad_sch"), "K57 силовая плата: Драйверы",
         "Мост, драйверы, звено, шунты, предзаряд", file_uuid("drivers.kicad_sch"))

# ================================================================ МОСТ (силовой путь - проводами)
sh = new_sheet("bridge.kicad_sch")
sh.paper = "A2"
sh.add_lib("k57:IRF100P218", irf100p218())
sh.add_lib("Connector_Generic:Conn_01x01", std("Connector_Generic", "Conn_01x01"))
sh.add_lib("Device:Transformer_1P_1S", std("Device", "Transformer_1P_1S"))
sh.add_lib("Device:R_Shunt", std("Device", "R_Shunt"))
sh.add_lib("Amplifier_Current:INA240A1PW", std("Amplifier_Current", "INA240A1PW"))
hier_block(sh, [("GATE_HA", "input"), ("GATE_LA", "input"), ("GATE_HB", "input"), ("GATE_LB", "input"),
                ("DC_POS", "passive"), ("GND", "passive"), ("PHASE_A", "passive"), ("PHASE_B", "passive"),
                ("IA_OUT", "output"), ("+3V3A", "passive"), ("VBIAS_1V5", "passive")])

Y_DC, Y_PH, Y_GND = 60.96, 127.0, 203.2      # шина звена, средние точки плеч, земля (минус звена)
Y_HI, Y_LO = 88.9, 165.1                     # ряды верхних и нижних ключей
from collections import Counter
taps = {Y_DC: Counter(), Y_GND: Counter()}   # отводы к шинам звена и земли: x -> сколько проводов подходит
ph_taps = {"A": Counter(), "B": Counter()}   # отводы к средней точке плеча

def bus(y, taps_x):
    """Горизонтальная шина через все отводы: отрезки между соседними точками. Точка соединения -
    внутри шины и на конце, если к концу подходят два провода (Т-образное соединение)."""
    xs = sorted(taps_x)
    for a, b in zip(xs, xs[1:]):
        sh.wire(a, y, b, y)
    for x in xs:
        if x not in (xs[0], xs[-1]) or taps_x[x] >= 2:
            sh.junction(x, y)

def mosfet(qn, x, y, gate_label, gate_r, pull_r, top_y, bot_y):
    """Ключ с затворным резистором и резистором затвор-исток. Сток - проводом вверх до top_y, исток - вниз до bot_y."""
    sh.place("k57:IRF100P218", qn, "IRF100P218", "Package_TO_SOT_THT:TO-247-3_Vertical", x, y,
             nets={"1": None, "2": None, "3": None},
             fields={"ref": (x + 5.08, y - 6.35), "val": (x + 5.08, y - 3.81), "just": "left"})
    xd = x + 2.54
    sh.wire(xd, y - 5.08, xd, top_y)                         # сток
    sh.wire(xd, y + 5.08, xd, bot_y)                         # исток
    sh.place("Device:R", gate_r, "2.7", R1206, x - 15.24, y, ang=90, nets={"1": gate_label, "2": None},
             fields={"ref": (x - 15.24, y - 3.81), "val": (x - 15.24, y + 3.81), "just": "center"})
    sh.wire(x - 11.43, y, x - 5.08, y)
    sh.place("Device:R", pull_r, "10k", R0603, x - 7.62, y + 6.35, nets={"1": None, "2": None},
             fields={"ref": (x - 10.16, y + 8.89), "val": (x - 10.16, y + 11.43), "just": "right"})
    sh.junction(x - 7.62, y)
    sh.wire(x - 7.62, y, x - 7.62, y + 2.54)
    sh.wire(x - 7.62, y + 10.16, xd, y + 10.16)
    sh.junction(xd, y + 10.16)
    return xd

def snubber(n, xs, top_y, bot_y, y0):
    """RC-снаббер сток-исток места ключа (не запаивать): R сверху, C снизу, концы - к шинам."""
    sh.place("Device:R", f"R{220 + n}", "4.7", R1206, xs, y0, dnp=True, nets={"1": None, "2": None})
    sh.place("Device:C", f"C{220 + n}", "2.2nF 100V C0G", C1206, xs, y0 + 25.4, dnp=True, nets={"1": None, "2": None})
    sh.wire(xs, y0 - 3.81, xs, top_y)
    sh.wire(xs, y0 + 3.81, xs, y0 + 25.4 - 3.81)
    sh.wire(xs, y0 + 25.4 + 3.81, xs, bot_y)

# плечи по краям, нагрузка (трансформатор и шунт) - между их средними точками, как в учебной схеме H-моста
LEGS = [("A", 106.68, (201, 202), (203, 204), "GATE_HA", "GATE_LA"),
        ("B", 330.2, (205, 206), (207, 208), "GATE_HB", "GATE_LB")]
for li, (leg, x0, hi, lo, g_hi, g_lo) in enumerate(LEGS):
    for k in range(2):
        x = x0 + k * 38.1
        xd = mosfet(f"Q{hi[k]}", x, Y_HI, g_hi, f"R{hi[k]}", f"R{hi[k] + 10}", Y_DC, Y_PH)
        taps[Y_DC][xd] += 1; ph_taps[leg][xd] += 1
        xd = mosfet(f"Q{lo[k]}", x, Y_LO, g_lo, f"R{lo[k]}", f"R{lo[k] + 10}", Y_PH, Y_GND)
        taps[Y_GND][xd] += 1; ph_taps[leg][xd] += 1
    xs = x0 + 38.1 + 20.32
    snubber(2 * li + 1, xs, Y_DC, Y_PH, 76.2)              # снаббер верхнего места
    snubber(2 * li + 2, xs, Y_PH, Y_GND, 152.4)            # снаббер нижнего места
    taps[Y_DC][xs] += 1; taps[Y_GND][xs] += 1; ph_taps[leg][xs] += 2

# конденсаторы звена (справа от плеча B): 4 x 10 мкФ плёнка + 4 x 100 нФ керамика, между шинами
FILM = "Capacitor_THT:C_Rect_L26.5mm_W10.5mm_P22.50mm_MKS4"
for i in range(8):
    x = 408.94 + i * 17.78
    ref, val, fp = (f"C{201 + i // 2}", "10uF MKP", FILM) if i % 2 == 0 else (f"C{211 + i // 2}", "100nF X7R", C1206)
    sh.place("Device:C", ref, val, fp, x, Y_PH, nets={"1": None, "2": None})
    sh.wire(x, Y_PH - 3.81, x, Y_DC); sh.wire(x, Y_PH + 3.81, x, Y_GND)
    taps[Y_DC][x] += 1; taps[Y_GND][x] += 1

# шины звена: метка DC_POS и символ земли на левом конце
XL = 76.2
taps[Y_DC][XL] += 0; taps[Y_GND][XL] += 0
bus(Y_DC, taps[Y_DC]); bus(Y_GND, taps[Y_GND])
sh.label("DC_POS", XL, Y_DC, 180)
sh.power("GND", XL, Y_GND, 270)

# ---------------- нагрузка моста: PHASE_A -> J201 -> обмотка трансформатора -> J501 -> шунт R501 -> PHASE_B
JA, JB = 182.88, 210.82                      # выводы клемм J201 и J501 (болты M6 под наконечник)
M6 = "MountingHole:MountingHole_6.4mm_M6_DIN965_Pad"
ph_taps["A"][JA] += 1
bus(Y_PH, ph_taps["A"])
sh.label("PHASE_A", min(ph_taps["A"]) + 2.54, Y_PH, 0)
sh.place("Connector_Generic:Conn_01x01", "J201", "XFMR_A, болт M6", M6, JA + 5.08, Y_PH, nets={"1": None},
         fields={"ref": (JA + 2.54, Y_PH - 7.62), "val": (JA + 2.54, Y_PH - 5.08), "just": "left"})
sh.place("Connector_Generic:Conn_01x01", "J501", "XFMR_B, болт M6", M6, JB - 5.08, Y_PH, ang=180, nets={"1": None},
         fields={"ref": (JB - 5.08, Y_PH + 5.08), "val": (JB - 5.08, Y_PH + 7.62), "just": "center"})
# трансформатор - на шасси: на схеме показан, чтобы был виден путь тока; на плату и в закупку не идёт
TX, TY = 226.06, 101.6
sh.place("Device:Transformer_1P_1S", "T201", "Трансформатор 50 Гц, 1:n (на шасси)", "", TX, TY, offboard=True,
         nets={"1": None, "2": None, "3": "!NC", "4": "!NC"},
         fields={"ref": (TX, TY - 12.7), "val": (TX, TY - 10.16), "just": "center"})
sh.wire(JA, Y_PH, JA, TY - 5.08); sh.wire(JA, TY - 5.08, TX - 10.16, TY - 5.08)
sh.junction(JA, Y_PH)
sh.wire(JB, Y_PH, JB, TY + 5.08); sh.wire(JB, TY + 5.08, TX - 10.16, TY + 5.08)
# шунт R501 горизонтально: вывод 4 - к J501, вывод 1 - к PHASE_B; измерительные 2 и 3 - снизу
SX = 238.76
sh.place("Device:R_Shunt", "R501", "0.2mR 15W 5930", "", SX, Y_PH, ang=270, nets={"1": None, "2": None, "3": None, "4": None},
         extra={"Part": "Bourns CSS2H-5930R-L200F или Milliohm HoLRS5930-15W-0.2mR"},
         fields={"ref": (SX, Y_PH - 6.35), "val": (SX, Y_PH - 3.81), "just": "center"})
sh.wire(JB, Y_PH, SX - 5.08, Y_PH)
sh.junction(JB, Y_PH)
ph_taps["B"][SX + 5.08] += 1
bus(Y_PH, ph_taps["B"])
sh.label("PHASE_B", SX + 7.62, Y_PH, 0)
# измерительные проводники: 2 (сторона PHASE_B) -> R502 -> IN+, 3 (сторона трансформатора) -> R503 -> IN-
XP, XN = SX + 2.54, SX - 5.08
IX, IY = 254.0, 165.1
sh.wire(XP, Y_PH + 2.54, XP, 137.16)
sh.wire(SX - 2.54, Y_PH + 2.54, SX - 2.54, 132.08); sh.wire(SX - 2.54, 132.08, XN, 132.08); sh.wire(XN, 132.08, XN, 137.16)
sh.place("Device:R", "R502", "0", R0603, XP, 140.97, nets={"1": None, "2": None},
         fields={"ref": (XP + 3.81, 139.7), "val": (XP + 3.81, 142.24), "just": "left"})
sh.place("Device:R", "R503", "0", R0603, XN, 140.97, nets={"1": None, "2": None},
         fields={"ref": (XN - 3.81, 139.7), "val": (XN - 3.81, 142.24), "just": "right"})
sh.wire(XP, 144.78, XP, IY - 2.54); sh.wire(XP, IY - 2.54, IX - 7.62, IY - 2.54)
sh.wire(XN, 144.78, XN, IY + 2.54); sh.wire(XN, IY + 2.54, IX - 7.62, IY + 2.54)
sh.place("Device:C", "C501", "100nF", C0603, (XP + XN) / 2, 152.4, ang=270, dnp=True, nets={"1": None, "2": None},
         fields={"ref": ((XP + XN) / 2, 148.59), "val": ((XP + XN) / 2, 156.21), "just": "center"})
sh.junction(XP, 152.4); sh.junction(XN, 152.4)
sh.place("Amplifier_Current:INA240A1PW", "U501", "INA240A1PW", "Package_SO:TSSOP-8_4.4x3mm_P0.65mm", IX, IY,
         nets={"2": None, "3": None, "1": None, "4": None, "5": "+3V3A", "6": None, "7": None, "8": None},
         fields={"ref": (IX + 5.08, IY - 8.89), "val": (IX + 5.08, IY + 12.7), "just": "left"})
PI = lambda k: sh.pin_xy("Amplifier_Current:INA240A1PW", IX, IY, 0, k)
gx, gy = PI(4)                                               # GND (1 и 4 - в одной точке)
sh.wire(gx, gy, gx, gy + 2.54); sh.wire(gx, gy + 2.54, gx - 5.08, gy + 2.54)
sh.power("GND", gx - 5.08, gy + 2.54, 270)
sh.hbus([PI(6), PI(7)], "VBIAS_1V5", up=False, label_at="right")
ox, oy = PI(8)
sh.place("Device:R", "R504", "47", R0603, ox + 10.16, oy, ang=90, nets={"1": None, "2": "IA_OUT"},
         fields={"ref": (ox + 10.16, oy - 3.81), "val": (ox + 10.16, oy + 3.81), "just": "center"})
sh.wire(ox, oy, ox + 6.35, oy)
sh.place("Device:C", "C502", "100nF", C0603, IX + 22.86, 147.32, nets={"1": "+3V3A", "2": "@GND"})
sh.place("Device:C", "C503", "100nF", C0603, IX + 35.56, 147.32, nets={"1": "VBIAS_1V5", "2": "@GND"})

sh.text("H-МОСТ И ШУНТ Ia (техзадание 6.1.1, 6.5)\n"
        "Сверху - шина звена DC_POS, снизу - земля (минус звена). Слева плечо A: Q201,Q202 - верхний ключ, Q203,Q204 - нижний;\n"
        "  справа плечо B: Q205,Q206 - верхний, Q207,Q208 - нижний. Infineon IRF100P218, TO-247, на общем радиаторе через прокладки.\n"
        "  У каждого ключа свой затворный резистор 2,7 Ом (R201-R208) и 10 кОм затвор-исток (R211-R218): ключ закрыт,\n"
        "  пока драйвер без питания. R221-R224, C221-C224 - RC-снабберы, НЕ ЗАПАИВАТЬ: место на случай звона.\n"
        "Между плечами - нагрузка: PHASE_A -> J201 -> первичная обмотка T201 -> J501 -> шунт R501 -> PHASE_B.\n"
        "  J201, J501 - болты M6 под кабельный наконечник. T201 - трансформатор на шасси: на плату и в закупку не входит,\n"
        "  нарисован для наглядности; его вторичная обмотка идёт на плату 230 В (J101). Медь 2-3 oz, до 195 А пик.\n"
        "C201-C204 - плёнка 10 мкФ 100 В, C211-C214 - керамика 100 нФ 100 В: на плате - у ключей, петля\n"
        "  \"конденсатор - верхний ключ - нижний ключ\" минимальная. [?] Плёнку - по выбранной модели.\n"
        "Ia: шунт R501 0,2 мОм, четырёхпроводное подключение: ток идёт через выводы 1-4, напряжение снимается с 2 и 3\n"
        "  отдельными дорожками от внутренних краёв площадок. R502/R503 (0 Ом) и C501 (не запаивать) - место под фильтр.\n"
        "  U501 INA240A1 (x20), REF1/REF2 = VBIAS_1V5: +-250 А -> 1,5 +- 1,0 В. Выход - через R504 47 Ом к разъёму.\n"
        "  [?] Посадочное место шунта 5930 с четырёхпроводным подключением - создать.", 76.2, 238.76)
sh.write(os.path.join(PDIR, "bridge.kicad_sch"), "K57 силовая плата: Мост",
         "Мост, драйверы, звено, шунты, предзаряд", file_uuid("bridge.kicad_sch"))

# ---------------------------------------------------------------- верхний лист: выводы блоков «Мост» и «Измерения 48 В»
ROOTF = os.path.join(PDIR, PRJ + ".kicad_sch")
changed = False
def blk(fname):
    return next(s for s in find(root, "sheet") if [p[2] for p in find(s, "property") if p[1] == "Sheetfile"][0] == fname)
br = blk("bridge.kicad_sch")
have = {str(p[1]) for p in find(br, "pin")}
tmpl = next(p for p in find(br, "pin") if str(p[1]) == "PHASE_B")
rs = Sheet(PRJ, "/" + root_uuid)
for i, (nm, typ) in enumerate([("IA_OUT", "output"), ("+3V3A", "passive"), ("VBIAS_1V5", "passive")]):
    if nm in have:
        continue
    p = copy.deepcopy(tmpl)
    p[1], p[2] = Q(nm), typ
    at = find(p, "at")[0]; at[2] = round(float(at[2]) + 5.08 * (i + 1), 2)
    find(p, "uuid")[0][1] = Q(U())
    br.append(p)
    px, py = float(at[1]), float(at[2])
    rs.wire(px, py, px + 5.08, py)
    rs.label(nm, px + 5.08, py, 0)
    changed = True
me = blk("meas_48v.kicad_sch")
gone = [p for p in find(me, "pin") if str(p[1]) in ("PHASE_B", "IA_OUT")]
for p in gone:
    px, py = (float(v) for v in find(p, "at")[0][1:3])
    me.remove(p)
    def touches(e):
        if not (isinstance(e, list) and e and e[0] in ("wire", "label")):
            return False
        pts = ([tuple(float(v) for v in xy[1:3]) for xy in find(find(e, "pts")[0], "xy")] if e[0] == "wire"
               else [tuple(float(v) for v in find(e, "at")[0][1:3])])
        near = lambda q: abs(q[0] - px) < 5.2 and abs(q[1] - py) < 0.01
        return all(near(q) for q in pts)
    root[:] = [e for e in root if not touches(e)]
    changed = True
if changed:
    i = next(k for k, e in enumerate(root) if isinstance(e, list) and e and e[0] == "sheet")
    root[i:i] = rs.items
    guard(ROOTF)
    open(ROOTF, "w", encoding="utf-8").write(dump(root))
    remember(ROOTF)
print("ok")
