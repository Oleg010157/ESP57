# Лист «Интерфейсы» платы k57-control: изолированный CAN FD, FRAM, место под часы, контрольные точки.
# Запуск: python -I hardware/tools/build_ctrl_if.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-control"
PDIR = os.path.join(HW, PRJ)
F = "interfaces.kicad_sch"

root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
old = parse(open(os.path.join(PDIR, F), encoding="utf-8").read())[0]
HIER = [(str(h[1]), str(find(h, "shape")[0][1])) for h in find(old, "hierarchical_label")]
FILE_UUID = str(find(old, "uuid")[0][1])
for other in ["mcu.kicad_sch", "power.kicad_sch", "esp32.kicad_sch"]:
    t = parse(open(os.path.join(PDIR, other), encoding="utf-8").read())[0]
    for s in find(t, "symbol"):
        for p in find(s, "property"):
            if p[1] == "Reference":
                ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

def fm25v02a():
    """SPI FRAM FM25V02A, SOIC-8: стандартная цоколёвка SPI-памяти."""
    pins = [("1", "~{CS}", "input", "L", 5.08), ("6", "SCK", "input", "L", 2.54), ("5", "SI", "input", "L", 0.0),
            ("2", "SO", "output", "R", 5.08), ("3", "~{WP}", "input", "L", -2.54), ("7", "~{HOLD}", "input", "L", -5.08),
            ("8", "VDD", "power_in", "T", 0), ("4", "VSS", "power_in", "B", 0)]
    unit = ["symbol", Q("FM25V02A_1_1")]
    for num, name, typ, side, y in pins:
        x, yy, a = {"L": (-10.16, y, 0), "R": (10.16, y, 180), "T": (0, 10.16, 270), "B": (0, -10.16, 90)}[side]
        unit.append(["pin", typ, "line", ["at", x, yy, a], ["length", 2.54],
                     ["name", Q(name), ["effects", ["font", ["size", 1.27, 1.27]]]],
                     ["number", Q(num), ["effects", ["font", ["size", 1.27, 1.27]]]]])
    def prop(k, v, at, hide=True):
        p = ["property", Q(k), Q(v), ["at"] + at, ["effects", ["font", ["size", 1.27, 1.27]]]]
        if hide: p.insert(4, ["hide", "yes"])
        return p
    return ["symbol", Q("FM25V02A"), ["pin_names", ["offset", 1.016]], ["exclude_from_sim", "no"],
            ["in_bom", "yes"], ["on_board", "yes"],
            prop("Reference", "U", [0, 12.7, 0], False), prop("Value", "FM25V02A", [0, -12.7, 0], False),
            prop("Footprint", "Package_SO:SOIC-8_3.9x4.9mm_P1.27mm", [0, 0, 0]),
            prop("Datasheet", "https://www.infineon.com/dgdl/Infineon-FM25V02A-DataSheet", [0, 0, 0]),
            prop("Description", "SPI FRAM 256 Кбит (32 К x 8), 40 МГц, 2,0-3,6 В, SOIC-8", [0, 0, 0]),
            ["symbol", Q("FM25V02A_0_1"), ["rectangle", ["start", -7.62, 7.62], ["end", 7.62, -7.62],
                                           ["stroke", ["width", 0.254], ["type", "default"]], ["fill", ["type", "background"]]]],
            unit]

FRAM = fm25v02a()
upsert_lib(os.path.join(HW, "lib", "k57.kicad_sym"), [FRAM])

sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[F], paper="A3")
for lid, sym in {"Device:R": lib("Device", "R"), "Device:C": lib("Device", "C"),
                 "Device:FerriteBead_Small": lib("Device", "FerriteBead_Small"),
                 "Interface_CAN_LIN:ISOW1044": lib("Interface_CAN_LIN", "ISOW1044"),
                 "Power_Protection:NUP2105L": lib("Power_Protection", "NUP2105L"),
                 "Timer_RTC:RV-3028-C7": lib("Timer_RTC", "RV-3028-C7"),
                 "Device:Battery_Cell": lib("Device", "Battery_Cell"),
                 "Connector_Generic:Conn_01x03": lib("Connector_Generic", "Conn_01x03"),
                 "Connector_Generic:Conn_01x02": lib("Connector_Generic", "Conn_01x02"),
                 "Connector:TestPoint": lib("Connector", "TestPoint"),
                 "k57:FM25V02A": FRAM,
                 "power:GND": lib("power", "GND"), "power:+5V": lib("power", "+5V"),
                 "power:+3V3": lib("power", "+3V3"), "power:PWR_FLAG": lib("power", "PWR_FLAG")}.items():
    sh.add_lib(lid, sym)

POWER = {"GND", "+5V", "+3V3"}
hier_block(sh, HIER, POWER)

R0603, C0402, C0603, C1206 = ("Resistor_SMD:R_0603_1608Metric", "Capacitor_SMD:C_0402_1005Metric",
                              "Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_1206_3216Metric")
FB0402 = "Inductor_SMD:L_0402_1005Metric"
TP = "TestPoint:TestPoint_Loop_D2.50mm_Drill1.0mm"
def fc(x, y, dr=-22.86, dv=22.86):
    return {"ref": (x, y + dr), "val": (x, y + dv), "just": "center"}

# ------------------------------------------------ CAN FD с изоляцией: ISOW1044 (SLLSFF7B)
XC, YC = 175.26, 83.82
sh.place("Interface_CAN_LIN:ISOW1044", "U401", "ISOW1044", "Package_SO:SOIC-20W_7.5x12.8mm_P1.27mm", XC, YC,
         nets={"1": None, "6": None, "10": None, "9": "@+5V",
               "3": "CAN_TX", "5": "CAN_RX", "4": None, "8": "CAN_EN", "2": None, "7": "!NC", "14": "!NC",
               "12": None, "13": None, "11": "GND2_ISO", "20": "VISO",
               "15": "GND_ISO", "16": "GND_ISO", "17": "GND_ISO", "19": "CANH", "18": "CANL"},
         fields={"ref": (XC + 7.62, YC + 19.05), "val": (XC + 7.62, YC + 21.59), "just": "left"})
P = lambda n: sh.pin_xy("Interface_CAN_LIN:ISOW1044", XC, YC, 0, n)
# VIO (1) и VDD (9) - соседние выводы: +3V3 отводим влево, чтобы символы питания не слиплись
(x1, y1) = P(1)
sh.wire(x1, y1, x1, y1 - 2.54); sh.wire(x1, y1 - 2.54, x1 - 7.62, y1 - 2.54)
sh.power("+3V3", x1 - 7.62, y1 - 2.54, 90)
sh.hbus([P(6), P(10)], "@GND", up=False)                  # GNDIO и GND1 снизу - одним символом
sh.bus_power("GND", [P(2), P(4)], -1, tail=10.16)         # IN и STB: земля левее метки CAN_EN
# VISOOUT (12) и VSIN (13) - соединить прямо на плате (Pin Functions)
(x12, y12), (x13, y13) = P(12), P(13)
sh.wire(x12, y12, x12 + 5.08, y12); sh.wire(x13, y13, x13, y13 - 5.08)
sh.wire(x13, y13 - 5.08, x12 + 5.08, y13 - 5.08); sh.wire(x12 + 5.08, y13 - 5.08, x12 + 5.08, y12)
sh.label("VISO_OUT", x12 + 5.08, y12, 0)
sh.place("Device:R", "R401", "10k", R0603, 129.54, 101.6, nets={"1": "@+3V3", "2": "CAN_EN"})
sh.place("Device:C", "C401", "100nF", C0603, 129.54, 45.72, nets={"1": "@+3V3", "2": "@GND"})
sh.place("Device:C", "C402", "10nF", C0402, 144.78, 45.72, nets={"1": "@+5V", "2": "@GND"})
sh.place("Device:C", "C403", "47uF 10V X5R", C1206, 157.48, 45.72, nets={"1": "@+5V", "2": "@GND"})
sh.place("Device:C", "C404", "10nF", C0402, 220.98, 45.72, nets={"1": "VISO_OUT", "2": "GND2_ISO"})
sh.place("Device:C", "C405", "47uF 10V X5R", C1206, 233.68, 45.72, nets={"1": "VISO_OUT", "2": "GND2_ISO"})
# бусины между выходом преобразователя и питанием приёмопередатчика (пример разводки TI, BLM15EX331SN1)
sh.place("Device:FerriteBead_Small", "FB401", "BLM15EX331SN1", FB0402, 248.92, 66.04, nets={"1": "VISO_OUT", "2": "VISO"})
sh.place("Device:FerriteBead_Small", "FB402", "BLM15EX331SN1", FB0402, 276.86, 66.04, nets={"1": "GND2_ISO", "2": "GND_ISO"})
sh.place("Device:C", "C406", "100nF", C0603, 304.8, 66.04, nets={"1": "VISO", "2": "GND_ISO"})
# шина: защита от статики, терминатор с перемычкой, разъём как J14 LaunchPad (1 CAN-H, 2 CAN-L, 3 GND)
sh.place("Power_Protection:NUP2105L", "D401", "NUP2105L", "Package_TO_SOT_SMD:SOT-23", 233.68, 119.38,
         nets={"1": "CANH", "2": "CANL", "3": "GND_ISO"},
         fields={"ref": (241.3, 116.84), "val": (241.3, 121.92), "just": "left"})
sh.place("Device:R", "R402", "120", "Resistor_SMD:R_1206_3216Metric", 259.08, 106.68, nets={"1": "CANH", "2": "TERM"})
sh.place("Connector_Generic:Conn_01x02", "JP401", "Терминатор 120 Ом", "Connector_PinHeader_2.54mm:PinHeader_1x02_P2.54mm_Vertical",
         281.94, 121.92, nets={"1": "TERM", "2": "CANL"})
sh.place("Connector_Generic:Conn_01x03", "J401", "CAN (как J14 LaunchPad)", "Connector_PinHeader_2.54mm:PinHeader_1x03_P2.54mm_Vertical",
         304.8, 93.98, nets={"1": "CANH", "2": "CANL", "3": "GND_ISO"})
# VISOOUT и GND2 у ISOW1044 - сами выходы питания; флажки - только на цепях за бусинами и на батарейке
for i, net in enumerate(["GND_ISO", "VISO", "VBACKUP"]):
    flag(sh, net, 330.2 + i * 15.24, 50.8, f"#FLG4{i + 1:02d}")

# ------------------------------------------------ FRAM FM25V02A на SPIB (решение 02.11.2026)
XF, YF = 99.06, 180.34
sh.place("k57:FM25V02A", "U402", "FM25V02A-G", "Package_SO:SOIC-8_3.9x4.9mm_P1.27mm", XF, YF,
         nets={"1": "FRAM_CS", "6": "FRAM_CLK", "5": "FRAM_PICO", "2": "FRAM_POCI", "3": None, "7": None,
               "8": "@+3V3", "4": "@GND"},
         fields={"ref": (XF + 3.81, YF - 15.24), "val": (XF + 3.81, YF - 12.7), "just": "left"})
PF = lambda n: sh.pin_xy("k57:FM25V02A", XF, YF, 0, n)
sh.bus_power("+3V3", [PF(3), PF(7)], -1, offset=5.08, tail=12.7)    # WP и HOLD - в 1: запись разрешена, без паузы
sh.place("Device:R", "R403", "10k", R0603, 73.66, 157.48, nets={"1": "@+3V3", "2": "FRAM_CS"})
sh.place("Device:C", "C407", "100nF", C0603, 124.46, 167.64, nets={"1": "@+3V3", "2": "@GND"})

# ------------------------------------------------ часы RV-3028-C7 и батарейка - НЕ ЗАПАИВАТЬ (место на плате)
XR, YR = 213.36, 180.34
sh.place("Timer_RTC:RV-3028-C7", "U403", "RV-3028-C7", "Package_SON:MicroCrystal_C7_SON-8_1.5x3.2mm_P0.9mm", XR, YR, dnp=True,
         nets={"7": None, "5": "@GND", "3": "I2C_SCL", "4": "I2C_SDA", "6": "VBACKUP", "8": "@GND",
               "1": "!NC", "2": "!NC"},
         fields={"ref": (XR + 5.08, YR - 15.24), "val": (XR + 5.08, YR - 12.7), "just": "left"})
(x7, y7) = sh.pin_xy("Timer_RTC:RV-3028-C7", XR, YR, 0, 7)     # VDD рядом с VBACKUP: +3V3 отводим влево
sh.wire(x7, y7, x7, y7 - 2.54); sh.wire(x7, y7 - 2.54, x7 - 7.62, y7 - 2.54)
sh.power("+3V3", x7 - 7.62, y7 - 2.54, 90)
sh.place("Device:R", "R404", "4.7k", R0603, 175.26, 160.02, dnp=True, nets={"1": "@+3V3", "2": "I2C_SCL"})
sh.place("Device:R", "R405", "4.7k", R0603, 187.96, 160.02, dnp=True, nets={"1": "@+3V3", "2": "I2C_SDA"})
sh.place("Device:C", "C408", "100nF", C0603, 238.76, 167.64, dnp=True, nets={"1": "@+3V3", "2": "@GND"})
sh.place("Device:Battery_Cell", "BT401", "CR2032", "Battery:BatteryHolder_Keystone_3034_1x20mm", 254.0, 190.5, dnp=True,
         nets={"1": "VBACKUP", "2": "@GND"})

# ------------------------------------------------ контрольные точки под осциллограф
for i, (ref, net) in enumerate([("TP401", "SCOPE"), ("TP402", "STROB"), ("TP403", "DAC_DBG"), ("TP404", "@GND")]):
    sh.place("Connector:TestPoint", ref, net.lstrip("@"), TP, 289.56 + i * 20.32, 160.02, nets={"1": net},
             fields={"ref": (289.56 + i * 20.32, 151.13), "val": (289.56 + i * 20.32, 153.67), "just": "center"})

sh.text("ИНТЕРФЕЙСЫ (техзадание 6.7)\n"
        "CAN FD: U401 ISOW1044 - изолированный приёмопередатчик со встроенным изолированным DC/DC (SLLSFF7B).\n"
        "  VDD 5 В: 10 нФ 0402 вплотную (до 1 мм) + 47 мкФ (C402/C403); VISOOUT - так же (C404/C405); VIO и VISOIN - 100 нФ.\n"
        "  VISOOUT соединён с VSIN (12-13). FB401/FB402 - бусины между выходом преобразователя и питанием/землёй\n"
        "  приёмопередатчика, как в примере разводки TI. STB на земле - обычный режим. EN/FLT подтянут (R401) - DC/DC включён.\n"
        "  Изолированная сторона (VISO, GND_ISO) - за барьером: под ISOW1044 и до J401 без меди на других слоях\n"
        "  ближе 4 мм к выводам изолированной стороны (раздел Layout).\n"
        "  J401 - как J14 LaunchPad: 1 CAN-H, 2 CAN-L, 3 GND; тот же кабель к D-Sub9 PCAN (7 CAN-H, 2 CAN-L, 3 GND),\n"
        "  docs/can-pcan-wiring-RU.md. Терминатор 120 Ом R402 включается перемычкой JP401. D401 - защита от статики.\n"
        "FRAM: U402 FM25V02A на SPIB (журнал аварий, учёт энергии), WP и HOLD в 1, CS подтянут R403.\n"
        "Часы U403 RV-3028-C7, батарейка BT401, подтяжки I2C R404/R405 - НЕ ЗАПАИВАТЬ: место на будущее\n"
        "  (docs/eeprom-fram-comparison-RU.md).\n"
        "TP401-TP404 - контрольные точки: SCOPE (GPIO57), STROB (GPIO58), DAC_DBG (DACA через 100 Ом), GND.",
        63.5, 228.6)
sh.write(os.path.join(PDIR, F), "K57 плата управления: Интерфейсы", "МК, питание, приёмники 230 В, интерфейсы", FILE_UUID)
print("ok", len(HIER))
