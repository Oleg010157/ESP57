# Плата k57-control: листы «Приёмники сигналов 230 В» и «Разъём к плате 230 В» (ответная часть J401 платы k57-ac).
# Запуск: python -I hardware/tools/build_ctrl_ac.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *
from k57_links import AC_LINK, place_idc

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-control"
PDIR = os.path.join(HW, PRJ)
COMMENT = "МК, питание, приёмники 230 В, интерфейсы"
MINE = ("ac_receivers.kicad_sch", "link_ac.kicad_sch")

root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
for f in [f for f in os.listdir(PDIR) if f.endswith(".kicad_sch") and f not in MINE]:
    t = parse(open(os.path.join(PDIR, f), encoding="utf-8").read())[0]
    for s in find(t, "symbol"):
        for p in find(s, "property"):
            if p[1] == "Reference":
                ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

def sheet_pins(fname):
    for sh in find(root, "sheet"):
        if [p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0] == fname:
            return [(str(p[1]), str(p[2])) for p in find(sh, "pin")]

def file_uuid(fname):
    return str(find(parse(open(os.path.join(PDIR, fname), encoding="utf-8").read())[0], "uuid")[0][1])

def new_sheet(fname, extra, paper="A3"):
    sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[fname], paper=paper)
    for lid, (lb, nm) in {"Device:R": ("Device", "R"), "Device:C": ("Device", "C"),
                          "power:GND": ("power", "GND"), "power:+12V": ("power", "+12V"), **extra}.items():
        sh.add_lib(lid, lib(lb, nm))
    return sh

R0603, C0603 = "Resistor_SMD:R_0603_1608Metric", "Capacitor_SMD:C_0603_1608Metric"

# ================================================================ «Приёмники сигналов 230 В»
F = "ac_receivers.kicad_sch"
OPA = "Amplifier_Operational:TLV9064"
OFP = "Package_SO:TSSOP-14_4.4x5mm_P0.65mm"
sh = new_sheet(F, {OPA: ("Amplifier_Operational", "TLV9064")})
hier_block(sh, sheet_pins(F), ("GND",))
XO = 243.84
# (сотня обозначений, имя, вход Rin, Rf = Rg, выход, секция, выводы +, -, выход)
CH = [(710, "IB", "10k 0.1%", "IB_OUT", 1, "3", "2", "1"),
      (720, "U220", "20k 0.1%", "U220_OUT", 2, "5", "6", "7"),
      (730, "USEC", "20k 0.1%", "USEC_OUT", 3, "10", "9", "8")]
for k, (n, t, rin, out, unit, pp, pn, po) in enumerate(CH):
    y = 50.8 + k * 50.8
    # дифференциальный усилитель: Uвых = VBIAS_1V5 + (Rf/Rin) * (P - N)
    sh.place("Device:R", f"R{n + 1}", rin, R0603, 101.6, y, nets={"1": f"{t}_P", "2": f"{t}_PI"})
    sh.place("Device:R", f"R{n + 2}", "10k 0.1%", R0603, 121.92, y, nets={"1": f"{t}_PI", "2": "VBIAS_1V5"})
    sh.place("Device:C", f"C{n + 2}", "100pF C0G", C0603, 142.24, y, nets={"1": f"{t}_PI", "2": "VBIAS_1V5"})
    sh.place("Device:R", f"R{n + 3}", rin, R0603, 162.56, y, nets={"1": f"{t}_N", "2": f"{t}_NI"})
    sh.place("Device:R", f"R{n + 4}", "10k 0.1%", R0603, 182.88, y, nets={"1": f"{t}_NI", "2": f"{t}_AMP"})
    sh.place("Device:C", f"C{n + 1}", "100pF C0G", C0603, 203.2, y, nets={"1": f"{t}_NI", "2": f"{t}_AMP"})
    sh.place(OPA, "U701", "TLV9064IPWR", OFP, XO, y, unit=unit,
             fields={"ref": (XO, y - 7.62), "val": (XO, y + 7.62), "just": "center"},
             nets={pp: f"{t}_PI", pn: f"{t}_NI", po: f"{t}_AMP"})
    sh.place("Device:R", f"R{n + 5}", "47", R0603, 279.4, y, nets={"1": f"{t}_AMP", "2": out})
# секция D не используется: повторитель, вход на земле
YD = 203.2
sh.place(OPA, "U701", "TLV9064IPWR", OFP, XO, YD, unit=4,
         fields={"ref": (XO, YD - 7.62), "val": (XO, YD + 7.62), "just": "center"},
         nets={"12": None, "13": "OPA7_D", "14": "OPA7_D"})
gx, gy = sh.pin_xy(OPA, XO, YD, 0, 12, unit=4)
sh.wire(gx, gy, gx - 12.7, gy)
sh.power("GND", gx - 12.7, gy, 270)
sh.place(OPA, "U701", "TLV9064IPWR", OFP, 279.4, YD, unit=5, nets={"4": "+3V3A", "11": "@GND"})
sh.place("Device:C", "C701", "100nF", C0603, 299.72, YD, nets={"1": "+3V3A", "2": "@GND"})
sh.text("ПРИЁМНИКИ СИГНАЛОВ 230 В (техзадание 6.5): дифференциальные пары с платы k57-ac -> вход АЦП 1,5 ± 1,0 В\n"
        "U701 TLV9064, три разностных усилителя: Uвых = VBIAS_1V5 + (Rf/Rin) * (P - N). Нуль - от REF2030, как у INA240.\n"
        "  Ib:   ТТ 1:1000 на 24,9 Ом даёт ±1,0 В при ±40 А; Rin = Rf = 10 кОм, усиление 1   -> IB_OUT (B4, CMPSS4).\n"
        "  U220: AMC3330 даёт ±2,0 В при ±400 В;      Rin 20 кОм, Rf 10 кОм, усиление 0,5 -> U220_OUT (A14, CMPSS3, eCAP1).\n"
        "  uSec: так же, как U220                                                        -> USEC_OUT (A1).\n"
        "Резисторы 0,1 %: от их согласования зависит подавление синфазной помехи. 100 пФ параллельно Rf и Rg - полоса ~160 кГц,\n"
        "  одинаковая у U220 и uSec: при сравнении фаз перед замыканием реле задержки каналов взаимно гасятся.\n"
        "  RC 100 Ом + 1 нФ у входов АЦП - на листе МК. Секция D U701 не используется.", 81.28, 228.6)
sh.write(os.path.join(PDIR, F), "K57 плата управления: Приёмники сигналов 230 В", COMMENT, file_uuid(F))

# ================================================================ «Разъём к плате 230 В»
F = "link_ac.kicad_sch"
CONN = "Connector_Generic:Conn_02x08_Odd_Even"
sh = new_sheet(F, {CONN: ("Connector_Generic", "Conn_02x08_Odd_Even")})
hier_block(sh, sheet_pins(F), ("GND", "+12V"))
CX, CY = 177.8, 101.6
place_idc(sh, CONN, "J801", "К плате 230 В (IDC 2x8)", "Connector_IDC:IDC-Header_2x08_P2.54mm_Vertical",
          CX, CY, AC_LINK, fields={"ref": (CX, 83.82), "val": (CX, 86.36), "just": "center"})
sh.text("РАЗЪЁМ К ПЛАТЕ 230 В (k57-ac, лист «Разъём к плате управления»)\n"
        "J801 - ответная часть J401 платы 230 В: IDC 2x8, прямой плоский шлейф (контакт 1 - в контакт 1).\n"
        "  Распиновка - общая для обеих плат (hardware/tools/k57_links.py, AC_LINK), сверяется скриптом check_links.py.\n"
        "  2-3 Ib, 5-6 U220, 8-9 uSec - дифференциальные пары; 11 GRID_CONT_EN - реле сети (GPIO59);\n"
        "  13 +3V3A - питание AMC3330 (до 82 мА на оба); 15-16 +12V - катушки реле (~0,25 А); остальные - GND.", 81.28, 165.1)
sh.write(os.path.join(PDIR, F), "K57 плата управления: Разъём к плате 230 В", COMMENT, file_uuid(F))
print("ok")
