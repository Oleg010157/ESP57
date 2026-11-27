# Лист «Питание» платы k57-control: 40-60 В -> 12 В -> 5 В -> 3,3 В, +3V3A, опора REF2030.
# Запуск: python -I hardware/tools/build_ctrl_power.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-control"
PDIR = os.path.join(HW, PRJ)
F = "power.kicad_sch"

root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
old = parse(open(os.path.join(PDIR, F), encoding="utf-8").read())[0]
HIER = [(str(h[1]), str(find(h, "shape")[0][1])) for h in find(old, "hierarchical_label")]
FILE_UUID = str(find(old, "uuid")[0][1])
t = parse(open(os.path.join(PDIR, "mcu.kicad_sch"), encoding="utf-8").read())[0]
for s in find(t, "symbol"):
    for p in find(s, "property"):
        if p[1] == "Reference":
            ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[F], paper="A2")
LM5013 = lib("Regulator_Switching", "LM5164DDA")      # тот же корпус и выводы, что LM5013 (SNVSCB3, Table 6-1)
rename_symbol(LM5013, "LM5013DDA", "LM5013DDA", "LM5164DDA")
set_prop(LM5013, "Value", "LM5013DDA")
set_prop(LM5013, "Datasheet", "https://www.ti.com/lit/ds/symlink/lm5013.pdf")
set_prop(LM5013, "Description", "100 В, 3,5 А, понижающий с постоянным временем включения, внешний диод")
for lid, sym in {"Device:R": lib("Device", "R"), "Device:C": lib("Device", "C"),
                 "Device:C_Polarized": lib("Device", "C_Polarized"), "Device:L": lib("Device", "L"),
                 "Device:D_Schottky": lib("Device", "D_Schottky"), "Device:FerriteBead_Small": lib("Device", "FerriteBead_Small"),
                 "Diode:SMAJ60A": lib("Diode", "SMAJ60A"),
                 "k57:LM5013DDA": LM5013,
                 "Regulator_Switching:LMR33630ADDA": lib("Regulator_Switching", "LMR33630ADDA"),
                 "Regulator_Linear:TLV75733PDRV": lib("Regulator_Linear", "TLV75733PDRV"),
                 "Reference_Voltage:REF2030": lib("Reference_Voltage", "REF2030"),
                 "Connector:TestPoint": lib("Connector", "TestPoint"),
                 "power:GND": lib("power", "GND"), "power:+12V": lib("power", "+12V"),
                 "power:+5V": lib("power", "+5V"), "power:+3V3": lib("power", "+3V3"),
                 "power:PWR_FLAG": lib("power", "PWR_FLAG")}.items():
    sh.add_lib(lid, sym)
upsert_lib(os.path.join(HW, "lib", "k57.kicad_sym"), [LM5013])

POWER = {"GND", "+12V", "+5V", "+3V3"}
hier_block(sh, HIER, POWER)

R0603, R0805 = "Resistor_SMD:R_0603_1608Metric", "Resistor_SMD:R_0805_2012Metric"
C0603, C0805, C1206, C1210 = ("Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_0805_2012Metric",
                              "Capacitor_SMD:C_1206_3216Metric", "Capacitor_SMD:C_1210_3225Metric")
TP = "TestPoint:TestPoint_Loop_D2.50mm_Drill1.0mm"
CEN = {"just": "center"}
def fc(x, y):
    # обе подписи - над корпусом: под корпусом символы земли выводов GND/PAD
    return {"ref": (x, y - 16.51), "val": (x, y - 13.97), "just": "center"}

from collections import Counter

def rail(y, taps):
    """Горизонтальная шина через отводы; точки соединения внутри и на концах с двумя проводами."""
    xs = sorted(taps)
    for a, b in zip(xs, xs[1:]):
        sh.wire(a, y, b, y)
    for x in xs:
        if x not in (xs[0], xs[-1]) or taps[x] >= 2:
            sh.junction(x, y)

def vcap(lib_id, ref, val, fp, x, y_top, y_bot, yc=None, fields=None, dnp=False):
    """Конденсатор (или другой двухвыводной) вертикально между двумя шинами: провода вверх и вниз."""
    yc = yc if yc is not None else (y_top + y_bot) / 2
    sh.place(lib_id, ref, val, fp, x, yc, nets={"1": None, "2": None}, fields=fields, dnp=dnp)
    sh.wire(x, yc - 3.81, x, y_top); sh.wire(x, yc + 3.81, x, y_bot)

# ============ 1. VBAT_RAW (40-60 В) -> 12 В: LM5013, типовая схема TI 48 -> 12 В (SNVSCB3, рис. 9-1)
YI1, YG1, YO1 = 50.8, 139.7, 86.36            # вход VBAT_RAW, земля, выход +12V
X1, Y1 = 187.96, 88.9
ti, tg, to = Counter(), Counter(), Counter()
ti[83.82] += 0; tg[83.82] += 0
sh.label("VBAT_RAW", 83.82, YI1, 180)
sh.power("GND", 83.82, YG1, 270)
sh.place("Diode:SMAJ60A", "D201", "SMBJ60A", "Diode_SMD:D_SMB", 88.9, 63.5, ang=270, nets={"1": None, "2": None},
         fields={"ref": (92.71, 62.23), "val": (92.71, 64.77), "just": "left"})
sh.wire(88.9, 59.69, 88.9, YI1); sh.wire(88.9, 67.31, 88.9, YG1); ti[88.9] += 1; tg[88.9] += 1
for ref, val, fp, x in [("C201", "47uF 100V", "Capacitor_SMD:CP_Elec_10x10.5", 105.41),
                        ("C202", "2.2uF 100V", C1210, 121.92), ("C203", "2.2uF 100V", C1210, 138.43)]:
    vcap("Device:C_Polarized" if ref == "C201" else "Device:C", ref, val, fp, x, YI1, YG1, yc=63.5)
    ti[x] += 1; tg[x] += 1
sh.place("k57:LM5013DDA", "U201", "LM5013DDA", "Package_SO:HSOP-8-1EP_3.9x4.9mm_P1.27mm_EP2.41x3.1mm_ThermalVias",
         X1, Y1, nets={k: None for k in "123459 78".replace(" ", "")} | {"6": "!NC"},
         fields={"ref": (X1, Y1 - 16.51), "val": (X1, Y1 - 13.97), "just": "center"})
P1 = lambda n: sh.pin_xy("k57:LM5013DDA", X1, Y1, 0, n)
(xv, yv), (xe, ye), (xr, yr) = P1(2), P1(3), P1(4)
sh.wire(xv, yv, xv - 5.08, yv); sh.wire(xv - 5.08, yv, xv - 5.08, YI1); ti[xv - 5.08] += 1      # VIN
# EN/UVLO: R201 от VBAT_RAW, R202 на землю
XE = 157.48
sh.place("Device:R", "R201", "576k", R0805, XE, 66.04, nets={"1": None, "2": None})
sh.wire(XE, 62.23, XE, YI1); ti[XE] += 1
sh.wire(XE, 69.85, XE, ye); sh.wire(XE, ye, xe, ye); sh.junction(XE, ye)
sh.place("Device:R", "R202", "30.1k", R0603, XE, 116.84, nets={"1": None, "2": None},
         fields={"ref": (XE - 3.81, 115.57), "val": (XE - 3.81, 118.11), "just": "right"})
sh.wire(XE, ye, XE, 113.03); sh.wire(XE, 120.65, XE, YG1); tg[XE] += 1
# RON: R203 на землю
XR = 165.1
sh.wire(xr, yr, XR, yr); sh.wire(XR, yr, XR, 113.03)
sh.place("Device:R", "R203", "100k", R0603, XR, 116.84, nets={"1": None, "2": None})
sh.wire(XR, 120.65, XR, YG1); tg[XR] += 1
for n in ("1", "9"):                                          # GND и теплоотводящая площадка - к земле
    gx, gy = P1(n); sh.wire(gx, gy, gx, YG1); tg[gx] += 1
# BST - C204 - SW; SW -> L201 -> +12V; D202 - на землю
(xb, yb), (xs, ys), (xf, yf) = P1(7), P1(8), P1(5)
sh.wire(xb, yb, xb + 5.08, yb); sh.wire(xb + 5.08, yb, xb + 5.08, 68.58); sh.wire(xb + 5.08, 68.58, xb + 10.16, 68.58)
sh.place("Device:C", "C204", "2.2nF 50V X7R", C0603, xb + 10.16, 72.39, nets={"1": None, "2": None})
sw = Counter(); sw[xs] += 0
sh.wire(xb + 10.16, 76.2, xb + 10.16, ys); sw[xb + 10.16] += 1
XD = xs + 17.78
sh.place("Device:D_Schottky", "D202", "SS510", "Diode_SMD:D_SMC", XD, 106.68, ang=270,
         nets={"1": None, "2": "@GND"}, fields={"ref": (XD - 2.54, 105.41), "val": (XD - 2.54, 107.95), "just": "right"})
sh.wire(XD, 102.87, XD, ys); sw[XD] += 1
XA = xs + 22.86                                                # цепь пульсаций: RA от SW
sh.place("Device:R", "R206", "453k", R0603, XA, 99.06, nets={"1": None, "2": None},
         fields={"ref": (XA + 3.81, 97.79), "val": (XA + 3.81, 100.33), "just": "left"})
sh.wire(XA, 95.25, XA, ys); sw[XA] += 1
XL = xs + 30.48
sh.place("Device:L", "L201", "33uH Isat>=4.5A", "Inductor_SMD:L_Bourns_SRP1038C_10.0x10.0mm", XL, ys, ang=90,
         nets={"1": None, "2": None}, fields={"ref": (XL, ys - 3.81), "val": (XL, ys - 6.35), "just": "center"})
sw[XL - 3.81] += 1
rail(ys, sw)
to[XL + 3.81] += 0
# RA - узел: C205 к выходу, C206 к FB
YRA = 116.84
XCA, XCB = xs + 40.64, xs + 48.26
sh.wire(XA, 102.87, XA, YRA); sh.wire(XA, YRA, XCB, YRA); sh.junction(XCA, YRA)
sh.place("Device:C", "C205", "3.3nF", C0603, XCA, 101.6, nets={"1": None, "2": None},
         fields={"ref": (XCA + 3.81, 100.33), "val": (XCA + 3.81, 102.87), "just": "left"})
sh.wire(XCA, 97.79, XCA, YO1); to[XCA] += 1
sh.wire(XCA, 105.41, XCA, YRA)
YFB = 127.0
XDIV = xs + 58.42
sh.place("Device:C", "C206", "56pF C0G", C0603, XCB, 121.92, nets={"1": None, "2": None},
         fields={"ref": (XCB - 3.81, 120.65), "val": (XCB - 3.81, 123.19), "just": "right"})
sh.wire(XCB, YRA, XCB, 118.11); sh.wire(XCB, 125.73, XCB, YFB); sh.junction(XCB, YFB)
# FB: от вывода вниз и вправо к делителю R204/R205
sh.wire(xf, yf, xf + 2.54, yf); sh.wire(xf + 2.54, yf, xf + 2.54, YFB); sh.wire(xf + 2.54, YFB, XDIV, YFB)
sh.place("Device:R", "R204", "453k", R0603, XDIV, 101.6, nets={"1": None, "2": None})
sh.wire(XDIV, 97.79, XDIV, YO1); to[XDIV] += 1
sh.wire(XDIV, 105.41, XDIV, YFB)
sh.place("Device:R", "R205", "49.9k", R0603, XDIV, 133.35, nets={"1": None, "2": None})
sh.wire(XDIV, YFB, XDIV, 129.54); sh.wire(XDIV, 137.16, XDIV, YG1); tg[XDIV] += 1
sh.junction(XDIV, YFB)
for i, (ref, val, fp) in enumerate([("C207", "22uF 25V", C1210), ("C208", "22uF 25V", C1210),
                                    ("C209", "220uF 25V", "Capacitor_SMD:CP_Elec_8x10.5")]):
    x = XDIV + 15.24 + i * 15.24
    vcap("Device:C_Polarized" if ref == "C209" else "Device:C", ref, val, fp, x, YO1, YG1, yc=113.03)
    to[x] += 1; tg[x] += 1
XO1 = XDIV + 15.24 * 4
to[XO1] += 0
rail(YI1, ti); rail(YG1, tg); rail(YO1, to)
sh.power("+12V", XO1, YO1, 90)

# ============ 2. 12 В -> 5 В: LMR33630A (400 кГц), типовые значения TI (SNVSAL3, табл. 9-2)
YI2, YG2 = 177.8, 241.3
X2, Y2 = 182.88, 205.74
ti, tg, to = Counter(), Counter(), Counter()
ti[83.82] += 0; tg[83.82] += 0
sh.power("+12V", 83.82, YI2, 90)
sh.power("GND", 83.82, YG2, 270)
sh.place("Regulator_Switching:LMR33630ADDA", "U202", "LMR33630ADDA", "Package_SO:Texas_HSOP-8-1EP_3.9x4.9mm_P1.27mm_ThermalVias",
         X2, Y2, nets={"1": None, "2": None, "3": None, "5": None, "6": None, "7": None, "8": None, "9": None, "4": "!NC"},
         fields={"ref": (X2, Y2 - 16.51), "val": (X2, Y2 - 13.97), "just": "center"})
P2 = lambda n: sh.pin_xy("Regulator_Switching:LMR33630ADDA", X2, Y2, 0, n)
for ref, val, fp, x in [("C211", "10uF 25V", C1206, 134.62), ("C212", "220nF 50V", C0603, 149.86)]:
    vcap("Device:C", ref, val, fp, x, YI2, YG2, yc=190.5); ti[x] += 1; tg[x] += 1
(xv, yv), (xe, ye), (xc, yc) = P2(2), P2(3), P2(6)
XV = xv - 5.08                                                # VIN и EN вместе: EN можно прямо на VIN
sh.wire(xv, yv, XV, yv); sh.wire(xe, ye, XV, ye); sh.wire(XV, ye, XV, YI2); sh.junction(XV, yv); ti[XV] += 1
XC = 160.02                                                   # VCC: 1 мкФ на землю
sh.wire(xc, yc, XC, yc); sh.wire(XC, yc, XC, 218.44)
sh.place("Device:C", "C213", "1uF 16V", C0603, XC, 222.25, nets={"1": None, "2": None})
sh.wire(XC, 226.06, XC, YG2); tg[XC] += 1
gx, gy = P2(1); sh.wire(gx, gy, gx, YG2); tg[gx] += 1        # GND (1 и 9 - в одной точке)
(xb, yb), (xs, ys), (xf, yf) = P2(7), P2(8), P2(5)
sh.wire(xb, yb, xb + 5.08, yb); sh.wire(xb + 5.08, yb, xb + 5.08, 187.96); sh.wire(xb + 5.08, 187.96, xb + 10.16, 187.96)
sh.place("Device:C", "C214", "100nF", C0603, xb + 10.16, 191.77, nets={"1": None, "2": None})
sw = Counter(); sw[xs] += 0
sh.wire(xb + 10.16, 195.58, xb + 10.16, ys); sw[xb + 10.16] += 1
XL = xs + 22.86
sh.place("Device:L", "L202", "8.2uH Isat>=4A", "Inductor_SMD:L_Bourns_SRP7028A_7.3x6.6mm", XL, ys, ang=90,
         nets={"1": None, "2": None}, fields={"ref": (XL, ys - 3.81), "val": (XL, ys - 6.35), "just": "center"})
sw[XL - 3.81] += 1
rail(ys, sw)
YO2 = ys
to[XL + 3.81] += 0
YFB = 228.6
XDIV = xs + 40.64
sh.wire(xf, yf, xf + 2.54, yf); sh.wire(xf + 2.54, yf, xf + 2.54, YFB); sh.wire(xf + 2.54, YFB, XDIV, YFB)
sh.place("Device:R", "R211", "100k", R0603, XDIV, 215.9, nets={"1": None, "2": None})
sh.wire(XDIV, 212.09, XDIV, YO2); to[XDIV] += 1
sh.wire(XDIV, 219.71, XDIV, YFB)
sh.place("Device:R", "R212", "24.9k", R0603, XDIV, 234.95, nets={"1": None, "2": None})
sh.wire(XDIV, YFB, XDIV, 231.14); sh.wire(XDIV, 238.76, XDIV, YG2); tg[XDIV] += 1
sh.junction(XDIV, YFB)
for i in range(4):
    x = XDIV + 15.24 + i * 15.24
    vcap("Device:C", f"C{215 + i}", "22uF 10V", C1206, x, YO2, YG2, yc=222.25)
    to[x] += 1; tg[x] += 1
XO2 = XDIV + 15.24 * 5
to[XO2] += 0
rail(YI2, ti); rail(YG2, tg); rail(YO2, to)
sh.power("+5V", XO2, YO2, 90)

# ============ 3. 5 В -> 3,3 В (МК, логика) и +3V3A через бусину
YI3, YG3, YO3 = 190.5, 228.6, 203.2
X3, Y3 = 375.92, 205.74
ti, tg, to, ta = Counter(), Counter(), Counter(), Counter()
XS3 = 340.36
ti[XS3] += 0; tg[XS3] += 0
sh.power("+5V", XS3, YI3, 90)
sh.power("GND", XS3, YG3, 270)
sh.place("Regulator_Linear:TLV75733PDRV", "U203", "TLV75733PDRV", "Package_SON:WSON-6-1EP_2x2mm_P0.65mm_EP1x1.6mm",
         X3, Y3, nets={"1": None, "3": None, "4": None, "6": None, "7": None, "2": "!NC", "5": "!NC"},
         fields={"ref": (X3, Y3 - 13.97), "val": (X3, Y3 - 11.43), "just": "center"})
P3 = lambda n: sh.pin_xy("Regulator_Linear:TLV75733PDRV", X3, Y3, 0, n)
vcap("Device:C", "C221", "1uF 16V", C0603, 350.52, YI3, YG3, yc=215.9); ti[350.52] += 1; tg[350.52] += 1
(xi, yi), (xe, ye), (xo, yo) = P3(6), P3(4), P3(1)
XV = xi - 5.08                                                # IN и EN вместе: включён всегда
sh.wire(xi, yi, XV, yi); sh.wire(xe, ye, XV, ye); sh.wire(XV, ye, XV, YI3); sh.junction(XV, yi); ti[XV] += 1
gx, gy = P3(3); sh.wire(gx, gy, gx, YG3); tg[gx] += 1
to[xo] += 0
for ref, val, fp, x in [("C222", "1uF 16V", C0603, 396.24), ("C223", "10uF 10V X7R", C0805, 411.48)]:
    vcap("Device:C", ref, val, fp, x, YO3, YG3, yc=215.9); to[x] += 1; tg[x] += 1
XP3 = 421.64
to[XP3] += 1
sh.wire(XP3, YO3, XP3, YO3 - 5.08); sh.power("+3V3", XP3, YO3 - 5.08, 90)
XFB = 431.8
sh.place("Device:FerriteBead_Small", "FB201", "600R@100MHz", "Inductor_SMD:L_0603_1608Metric", XFB, YO3, ang=90,
         nets={"1": None, "2": None}, fields={"ref": (XFB, YO3 - 3.81), "val": (XFB, YO3 - 6.35), "just": "center"})
to[XFB - 2.54] += 0
ta[XFB + 2.54] += 0
for ref, val, fp, x in [("C224", "10uF 10V", C0805, 444.5), ("C225", "100nF", C0603, 459.74)]:
    vcap("Device:C", ref, val, fp, x, YO3, YG3, yc=215.9); ta[x] += 1; tg[x] += 1
ta[469.9] += 0
rail(YI3, ti); rail(YG3, tg); rail(YO3, to); rail(YO3, ta)
sh.label("+3V3A", 469.9, YO3, 0)

# ============ 4. Опора REF2030: VREF_3V0 и VBIAS_1V5
YI4, YG4 = 60.96, 109.22
X4, Y4 = 375.92, 81.28
ti, tg = Counter(), Counter()
XS4 = 345.44
ti[XS4] += 0; tg[XS4] += 0
sh.power("+5V", XS4, YI4, 90)
sh.power("GND", XS4, YG4, 270)
sh.place("Reference_Voltage:REF2030", "U204", "REF2030AIDDCR", "Package_TO_SOT_SMD:SOT-23-5", X4, Y4,
         nets={"1": None, "2": None, "3": None, "4": None, "5": None},
         fields={"ref": (X4 + 10.16, Y4 - 7.62), "val": (X4 + 10.16, Y4 - 5.08), "just": "left"})
P4 = lambda n: sh.pin_xy("Reference_Voltage:REF2030", X4, Y4, 0, n)
vcap("Device:C", "C231", "1uF 16V", C0603, 355.6, YI4, YG4, yc=86.36); ti[355.6] += 1; tg[355.6] += 1
(xi, yi), (xe, ye), (xg, yg), (xr, yr), (xbias, ybias) = P4(4), P4(3), P4(2), P4(5), P4(1)
sh.wire(xi, yi, xi, YI4); ti[xi] += 1                         # IN - сверху к +5V
sh.wire(xe, ye, xe - 5.08, ye); sh.wire(xe - 5.08, ye, xe - 5.08, YI4); ti[xe - 5.08] += 1   # EN - к +5V: включена всегда
sh.wire(xg, yg, xg, YG4); tg[xg] += 1
XR1 = xr + 15.24                                              # VREF -> C232 -> метка
sh.wire(xr, yr, XR1 + 5.08, yr); sh.junction(XR1, yr)
sh.label("VREF_3V0", XR1 + 5.08, yr, 0)
vcap("Device:C", "C232", "1uF 10V", C0603, XR1, yr, YG4, yc=93.98); tg[XR1] += 1
XB1 = xbias + 2.54                                            # VBIAS -> вниз к C233
sh.wire(xbias, ybias, XB1, ybias); sh.wire(XB1, ybias, XB1, 95.25)
sh.label("VBIAS_1V5", XB1, 90.17, 0)
sh.place("Device:C", "C233", "1uF 10V", C0603, XB1, 99.06, nets={"1": None, "2": None})
sh.wire(XB1, 102.87, XB1, YG4); tg[XB1] += 1
rail(YI4, ti); rail(YG4, tg)

# контрольные точки и флажки питания
for i, (ref, net) in enumerate([("TP201", "@+12V"), ("TP202", "@+5V"), ("TP203", "@+3V3"), ("TP204", "+3V3A"),
                                ("TP205", "VREF_3V0"), ("TP206", "VBIAS_1V5"), ("TP207", "@GND")]):
    sh.place("Connector:TestPoint", ref, net.lstrip("@"), TP, 101.6 + i * 22.86, 279.4, nets={"1": net},
             fields={"ref": (101.6 + i * 22.86, 270.51), "val": (101.6 + i * 22.86, 273.05), "just": "center"})
for i, net in enumerate(["@+12V", "@+5V", "+3V3A", "@GND", "VBAT_RAW"]):
    flag(sh, net, 289.56 + i * 17.78, 276.86, f"#FLG2{i + 1:02d}")

sh.text("ПИТАНИЕ ПЛАТЫ УПРАВЛЕНИЯ\n"
        "1) VBAT_RAW 40-60 В (с силовой платы, после предохранителя F301) -> 12 В: U201 LM5013 (100 В, 3,5 А).\n"
        "   Номиналы - типовая схема TI 48 -> 12 В, 300 кГц (SNVSCB3, рис. 9-1). Вход 100 В максимум: D201 SMBJ60A.\n"
        "   Запуск от ~30 В: EN/UVLO 1,5 В, делитель R201/R202. Нагрузка 12 В: драйверы, катушки контакторов, ступень 5 В.\n"
        "   [?] L201 и D202 - выбрать модели (ток насыщения дросселя не ниже ~4,5 А, диод Шоттки 100 В 5 А).\n"
        "2) 12 В -> 5 В: U202 LMR33630A, 400 кГц (SNVSAL3, табл. 9-2: 8 мкГн, 4 x 22 мкФ, 100k/24,9k). 5 В: ESP32-S3\n"
        "   (свой стабилизатор на листе ESP32-S3), изолированный DC/DC CAN, U203, REF2030.\n"
        "3) 5 В -> 3,3 В: U203 TLV75733P (1 А) - МК и логика. +3V3A - тот же 3,3 В через бусину FB201: питание\n"
        "   аналоговых микросхем (INA240, ОУ, AMC3330) от одного источника с МК - их выходы не подадут напряжение\n"
        "   на выключенный МК. Нарастание 3,3 В: не быстрее 100 мВ/мкс (SPRSP85D).\n"
        "4) Опора U204 REF2030: VREF_3V0 (3,0 В) -> VREFHI АЦП, VBIAS_1V5 (1,5 В) -> нуль двуполярных каналов.\n"
        "   3,0 В, а не 3,3 В: при внешней опоре VREFHI не выше VDDA (SPRSP85D, ADC Operating Conditions).\n"
        "   [?] Конденсаторы на выходах REF2030 - сверить с документацией.\n"
        "TP201-TP207 - контрольные точки под щуп. PWR_FLAG - пометка для проверки схемы.", 63.5, 302.26)
sh.write(os.path.join(PDIR, F), "K57 плата управления: Питание", "МК, питание, приёмники 230 В, интерфейсы", FILE_UUID)
print("ok", len(HIER))
