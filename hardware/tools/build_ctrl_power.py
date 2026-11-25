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
y = 30.48
for net, shape in HIER:
    sh.hlabel(net, shape, 20.32, y)
    sh.wire(20.32, y, 27.94, y)
    if net in POWER:
        sh.power(net, 27.94, y, 270 if net == "GND" else 90)
    else:
        sh.label(net, 27.94, y, 0)
    y += 7.62

R0603, R0805 = "Resistor_SMD:R_0603_1608Metric", "Resistor_SMD:R_0805_2012Metric"
C0603, C0805, C1206, C1210 = ("Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_0805_2012Metric",
                              "Capacitor_SMD:C_1206_3216Metric", "Capacitor_SMD:C_1210_3225Metric")
TP = "TestPoint:TestPoint_Loop_D2.50mm_Drill1.0mm"
CEN = {"just": "center"}
def fc(x, y):
    return {"ref": (x, y - 13.97), "val": (x, y + 15.24), "just": "center"}

# ============ 1. VBAT_RAW (40-60 В) -> 12 В: LM5013, типовая схема TI 48 -> 12 В (SNVSCB3, рис. 9-1)
X1, Y1 = 152.4, 81.28
sh.place("k57:LM5013DDA", "U201", "LM5013DDA", "Package_SO:HSOP-8-1EP_3.9x4.9mm_P1.27mm_EP2.41x3.1mm_ThermalVias",
         X1, Y1, nets={"2": "VBAT_RAW", "3": "EN_12V", "4": "RON_12V", "1": "@GND", "9": "@GND",
                       "7": "BST_12V", "8": "SW_12V", "5": "FB_12V", "6": "!NC"}, fields=fc(X1, Y1))
sh.place("Diode:SMAJ60A", "D201", "SMBJ60A", "Diode_SMD:D_SMB", 63.5, 63.5, nets={"1": "VBAT_RAW", "2": "@GND"},
         fields={"ref": (63.5, 58.42), "val": (63.5, 68.58), "just": "center"})
sh.place("Device:C_Polarized", "C201", "47uF 100V", "Capacitor_SMD:CP_Elec_10x10.5", 83.82, 63.5,
         nets={"1": "VBAT_RAW", "2": "@GND"})
sh.place("Device:C", "C202", "2.2uF 100V X7R", C1210, 99.06, 63.5, nets={"1": "VBAT_RAW", "2": "@GND"})
sh.place("Device:C", "C203", "2.2uF 100V X7R", C1210, 114.3, 63.5, nets={"1": "VBAT_RAW", "2": "@GND"})
sh.place("Device:R", "R201", "576k", R0805, 99.06, 99.06, nets={"1": "VBAT_RAW", "2": "EN_12V"})
sh.place("Device:R", "R202", "30.1k", R0603, 99.06, 121.92, nets={"1": "EN_12V", "2": "@GND"})
sh.place("Device:R", "R203", "100k", R0603, 119.38, 121.92, nets={"1": "RON_12V", "2": "@GND"})
sh.place("Device:C", "C204", "2.2nF 50V X7R", C0603, 187.96, 53.34, nets={"1": "BST_12V", "2": "SW_12V"})
sh.place("Device:D_Schottky", "D202", "SS510 (100V 5A)", "Diode_SMD:D_SMC", 205.74, 106.68,
         nets={"1": "SW_12V", "2": "@GND"}, fields={"ref": (205.74, 101.6), "val": (205.74, 111.76), "just": "center"})
sh.place("Device:L", "L201", "33uH Isat>=4.5A", "Inductor_SMD:L_Bourns_SRP1038C_10.0x10.0mm", 220.98, 71.12,
         nets={"1": "SW_12V", "2": "@+12V"})
sh.place("Device:R", "R204", "453k", R0603, 246.38, 99.06, nets={"1": "@+12V", "2": "FB_12V"})
sh.place("Device:R", "R205", "49.9k", R0603, 246.38, 121.92, nets={"1": "FB_12V", "2": "@GND"})
# цепь пульсаций (тип 3): RA от SW, CA к выходу, CB к FB
sh.place("Device:R", "R206", "453k", R0603, 205.74, 137.16, nets={"1": "SW_12V", "2": "RA_12V"})
sh.place("Device:C", "C205", "3.3nF", C0603, 218.44, 154.94, nets={"1": "@+12V", "2": "RA_12V"})
sh.place("Device:C", "C206", "56pF C0G", C0603, 233.68, 154.94, nets={"1": "RA_12V", "2": "FB_12V"})
sh.place("Device:C", "C207", "22uF 25V X7R", C1210, 266.7, 99.06, nets={"1": "@+12V", "2": "@GND"})
sh.place("Device:C", "C208", "22uF 25V X7R", C1210, 281.94, 99.06, nets={"1": "@+12V", "2": "@GND"})
sh.place("Device:C_Polarized", "C209", "220uF 25V", "Capacitor_SMD:CP_Elec_8x10.5", 297.18, 99.06,
         nets={"1": "@+12V", "2": "@GND"})

# ============ 2. 12 В -> 5 В: LMR33630A (400 кГц), типовые значения TI (SNVSAL3, табл. 9-2)
X2, Y2 = 152.4, 205.74
sh.place("Regulator_Switching:LMR33630ADDA", "U202", "LMR33630ADDA", "Package_SO:Texas_HSOP-8-1EP_3.9x4.9mm_P1.27mm_ThermalVias",
         X2, Y2, nets={"2": None, "3": None, "6": "VCC_5V", "1": "@GND", "9": "@GND",
                       "7": "BOOT_5V", "8": "SW_5V", "5": "FB_5V", "4": "!NC"}, fields=fc(X2, Y2))
P2 = lambda n: sh.pin_xy("Regulator_Switching:LMR33630ADDA", X2, Y2, 0, n)
sh.bus_power("+12V", [P2(2), P2(3)], -1)                 # VIN и EN вместе (EN можно прямо на VIN)
sh.place("Device:C", "C211", "10uF 25V X7R", C1206, 99.06, 193.04, nets={"1": "@+12V", "2": "@GND"})
sh.place("Device:C", "C212", "220nF 50V", C0603, 114.3, 193.04, nets={"1": "@+12V", "2": "@GND"})
sh.place("Device:C", "C213", "1uF 16V", C0603, 114.3, 228.6, nets={"1": "VCC_5V", "2": "@GND"})
sh.place("Device:C", "C214", "100nF", C0603, 182.88, 182.88, nets={"1": "BOOT_5V", "2": "SW_5V"})
sh.place("Device:L", "L202", "8.2uH Isat>=4A", "Inductor_SMD:L_Bourns_SRP7028A_7.3x6.6mm", 203.2, 195.58,
         nets={"1": "SW_5V", "2": "@+5V"})
sh.place("Device:R", "R211", "100k", R0603, 226.06, 213.36, nets={"1": "@+5V", "2": "FB_5V"})
sh.place("Device:R", "R212", "24.9k", R0603, 226.06, 236.22, nets={"1": "FB_5V", "2": "@GND"})
for i in range(4):
    sh.place("Device:C", f"C{215 + i}", "22uF 10V X7R", C1206, 246.38 + i * 15.24, 213.36, nets={"1": "@+5V", "2": "@GND"})

# ============ 3. 5 В -> 3,3 В (МК, логика) и +3V3A через бусину; опора REF2030
X3, Y3 = 335.28, 205.74
sh.place("Regulator_Linear:TLV75733PDRV", "U203", "TLV75733PDRV", "Package_SON:WSON-6-1EP_2x2mm_P0.65mm_EP1x1.6mm",
         X3, Y3, nets={"6": None, "4": None, "1": "@+3V3", "3": "@GND", "7": "@GND", "2": "!NC", "5": "!NC"},
         fields=fc(X3, Y3))
P3 = lambda n: sh.pin_xy("Regulator_Linear:TLV75733PDRV", X3, Y3, 0, n)
sh.bus_power("+5V", [P3(6), P3(4)], -1)                  # IN и EN вместе: включён всегда
sh.place("Device:C", "C221", "1uF 16V", C0603, 312.42, 223.52, nets={"1": "@+5V", "2": "@GND"})
sh.place("Device:C", "C222", "1uF 16V", C0603, 360.68, 223.52, nets={"1": "@+3V3", "2": "@GND"})
sh.place("Device:C", "C223", "10uF 10V X7R", C0805, 373.38, 223.52, nets={"1": "@+3V3", "2": "@GND"})
sh.place("Device:FerriteBead_Small", "FB201", "600R@100MHz", "Inductor_SMD:L_0603_1608Metric", 342.9, 246.38,
         nets={"1": "@+3V3", "2": "+3V3A"})
sh.place("Device:C", "C224", "10uF 10V X7R", C0805, 360.68, 259.08, nets={"1": "+3V3A", "2": "@GND"})
sh.place("Device:C", "C225", "100nF", C0603, 373.38, 259.08, nets={"1": "+3V3A", "2": "@GND"})
X4, Y4 = 335.28, 81.28
sh.place("Reference_Voltage:REF2030", "U204", "REF2030AIDDCR", "Package_TO_SOT_SMD:SOT-23-5", X4, Y4,
         nets={"4": "@+5V", "3": "@+5V", "2": "@GND", "5": "VREF_3V0", "1": "VBIAS_1V5"}, fields=fc(X4, Y4))
sh.place("Device:C", "C231", "1uF 16V", C0603, 312.42, 99.06, nets={"1": "@+5V", "2": "@GND"})
sh.place("Device:C", "C232", "1uF 10V X7R", C0603, 365.76, 99.06, nets={"1": "VREF_3V0", "2": "@GND"})
sh.place("Device:C", "C233", "1uF 10V X7R", C0603, 381.0, 99.06, nets={"1": "VBIAS_1V5", "2": "@GND"})

# контрольные точки и флажки питания
for i, (ref, net) in enumerate([("TP201", "@+12V"), ("TP202", "@+5V"), ("TP203", "@+3V3"), ("TP204", "+3V3A"),
                                ("TP205", "VREF_3V0"), ("TP206", "VBIAS_1V5"), ("TP207", "@GND")]):
    sh.place("Connector:TestPoint", ref, net.lstrip("@"), TP, 101.6 + i * 22.86, 279.4, nets={"1": net},
             fields={"ref": (101.6 + i * 22.86, 270.51), "val": (101.6 + i * 22.86, 273.05), "just": "center"})
for i, net in enumerate(["@+12V", "@+5V", "+3V3A", "@GND", "VBAT_RAW"]):
    sh.place("power:PWR_FLAG", f"#FLG2{i + 1:02d}", "PWR_FLAG", "", 289.56 + i * 17.78, 279.4, nets={"1": net},
             fields={"ref": (289.56 + i * 17.78, 273.05), "val": (289.56 + i * 17.78, 275.59), "just": "center"})

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
