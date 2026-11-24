# Символ TMS320F28P550SJ9PZ (все 100 выводов, имена TI внутри) и лист «МК» платы k57-control.
# Запуск: python -I hardware/tools/build_ctrl_mcu.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-control"
PDIR = os.path.join(HW, PRJ)
F = "mcu.kicad_sch"
G = 2.54

# ---------------------------------------------------------------- выводы корпуса PZ
# Номер -> имя по документации TI SPRSP85D, Figure 5-2 (100-Pin PZ, вид сверху)
PZ = """1 A16/B16/C16/GPIO28;2 XRSn;3 VDDIO;4 VDD;5 VSS;6 GPIO47;7 GPIO48;8 GPIO49;9 GPIO50;10 GPIO51;
11 GPIO52;12 GPIO53;13 GPIO54;14 A6/D14/E14/GPIO228;15 B2/C6/E12/GPIO226;16 B3/GPIO242/PGA2_INP;
17 A2/B6/C9/GPIO224/PGA1_INP;18 A3/B9/C7/PGA1_INM;19 A14/B14/C4/PGA1_OUT;20 A11/B10/C0/PGA2_OUT;
21 B12/C2/PGA2_INM;22 A1/B7/D11/CMP1_DACL;23 A0/B15/C15/DACA_OUT;24 VREFHI/D20/E20;25 VREFHI/D20/E20;
26 VREFLO/A13/B13/C13/D13/E13;27 VREFLO/A13/B13/C13/D13/E13;28 A12/C5;29 C1/E11/PGA3_INP;30 B11/D16/E16/PGA3_INM;
31 A7/B30/C3/D12/E30;32 B5/D15/E15/PGA3_OUT;33 VSSA;34 VDDA;35 A5;36 A4/B8;37 A8;38 A9/GPIO227;
39 B4/C8/GPIO236;40 A10/B1/C10/GPIO230;41 B0/C11/GPIO253;42 C14/GPIO247;43 GPIO55;44 GPIO60;45 VSS;
46 GPIO62;47 GPIO63;48 A17/B17/C17/GPIO20;49 A18/B18/C18/GPIO21;50 A19/B19/C19/GPIO13;51 A20/B20/C20/GPIO12;
52 A24/D0/E0/GPIO11;53 B24/D1/E1/GPIO33;54 C24/D2/E2/GPIO16;55 A25/D3/E3/GPIO17;56 B25/D4/E4/GPIO24;
57 GPIO25;58 GPIO26;59 GPIO27;60 TCK;61 GPIO37/TDO;62 TMS;63 GPIO35/TDI;64 GPIO32;65 GPIO56;66 GPIO57;
67 GPIO58;68 GPIO18/X2;69 GPIO19/X1;70 VDDIO;71 VDD;72 VSS;73 VREGENZ;74 GPIO8;75 GPIO4;76 GPIO3;
77 GPIO2;78 GPIO1;79 GPIO0;80 GPIO40;81 GPIO23;82 GPIO41;83 GPIO22;84 GPIO7;85 GPIO44;86 VSS;87 VDD;
88 VDDIO;89 GPIO5;90 GPIO9;91 GPIO61;92 GPIO59;93 GPIO10;94 GPIO34;95 GPIO15;96 GPIO14;97 GPIO6;
98 GPIO30;99 GPIO31;100 GPIO29"""
PINS = {int(e.split()[0]): e.split()[1] for e in PZ.replace("\n", "").split(";") if e.strip()}
assert sorted(PINS) == list(range(1, 101))

ANALOG = list(range(14, 24)) + list(range(28, 33)) + list(range(35, 43))
VREF = [24, 25, 26, 27]
TOP = [3, 70, 88, 34, 4, 71, 87]          # VDDIO, VDDA, VDD
BOTTOM = [5, 45, 72, 86, 33]               # VSS, VSSA
SPECIAL = [2, 62, 60, 73]                  # XRSn, TMS, TCK, VREGENZ
def gpio_no(n):
    import re
    m = re.search(r"GPIO(\d+)", PINS[n]); return int(m.group(1)) if m else 999
GPIO = sorted([n for n in PINS if n not in ANALOG + VREF + TOP + BOTTOM + SPECIAL], key=gpio_no)
RIGHT = [n for n in GPIO if gpio_no(n) < 40]
LEFT_GPIO = [n for n in GPIO if gpio_no(n) >= 40]

def ptype(n):
    nm = PINS[n]
    if nm in ("VDDIO", "VDD", "VDDA", "VSS", "VSSA"):
        return "power_in"
    if nm.startswith(("VREFHI", "VREFLO")):
        return "passive"
    if nm in ("TCK", "VREGENZ"):
        return "input"
    if "GPIO" in nm or nm in ("XRSn", "TMS"):
        return "bidirectional"
    return "input"                          # аналоговые входы без GPIO

def mcu_symbol():
    name = "TMS320F28P550SJ9PZ"
    W = 38.1                                # полуширина корпуса символа
    left = ANALOG + [None] + VREF + [None] + SPECIAL + [None] + LEFT_GPIO
    rows = max(len(left), len(RIGHT)) + 1
    top_y = rows / 2 * G
    top_y = round(top_y / G) * G
    unit = ["symbol", Q(name + "_1_1")]
    body = ["symbol", Q(name + "_0_1"),
            ["rectangle", ["start", -W, top_y], ["end", W, -top_y - G],
             ["stroke", ["width", 0.254], ["type", "default"]], ["fill", ["type", "background"]]]]
    def pin(n, x, y, a):
        unit.append(["pin", ptype(n), "line", ["at", round(x, 3), round(y, 3), a], ["length", 2.54],
                     ["name", Q(PINS[n]), ["effects", ["font", ["size", 1.016, 1.016]]]],
                     ["number", Q(str(n)), ["effects", ["font", ["size", 1.016, 1.016]]]]])
    y = top_y - G
    for n in left:
        if n is not None:
            pin(n, -W - 2.54, y, 0)
        y -= G
    y = top_y - G
    for n in RIGHT:
        pin(n, W + 2.54, y, 180)
        y -= G
    for i, n in enumerate(TOP):
        pin(n, (i - (len(TOP) - 1) / 2) * 5.08, top_y + 2.54, 270)
    for i, n in enumerate(BOTTOM):
        pin(n, (i - (len(BOTTOM) - 1) / 2) * 5.08, -top_y - G - 2.54, 90)
    def prop(k, v, at, hide=True):
        p = ["property", Q(k), Q(v), ["at"] + at, ["effects", ["font", ["size", 1.27, 1.27]]]]
        if hide: p.insert(4, ["hide", "yes"])
        return p
    return ["symbol", Q(name), ["pin_names", ["offset", 0.762]],
            ["exclude_from_sim", "no"], ["in_bom", "yes"], ["on_board", "yes"],
            prop("Reference", "U", [-W, top_y + 6.35, 0], False),
            prop("Value", name, [W, top_y + 6.35, 0], False),
            prop("Footprint", "Package_QFP:LQFP-100_14x14mm_P0.5mm", [0, 0, 0]),
            prop("Datasheet", "https://www.ti.com/lit/ds/symlink/tms320f28p550sj.pdf", [0, 0, 0]),
            prop("Description", "C2000 МК F28P550SJ9, 150 МГц, LQFP-100 (PZ). Имена выводов - по SPRSP85D, Figure 5-2", [0, 0, 0]),
            body, unit]

MCU = mcu_symbol()
upsert_lib(os.path.join(HW, "lib", "k57.kicad_sym"), [MCU])

# ---------------------------------------------------------------- лист «МК»
root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
old = parse(open(os.path.join(PDIR, F), encoding="utf-8").read())[0]
HIER = [(str(h[1]), str(find(h, "shape")[0][1])) for h in find(old, "hierarchical_label")]
FILE_UUID = str(find(old, "uuid")[0][1])

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[F], paper="A2")
for lid, (lb, nm) in {"Device:R": ("Device", "R"), "Device:C": ("Device", "C"),
                      "Device:Crystal_GND24": ("Device", "Crystal_GND24"), "Switch:SW_Push": ("Switch", "SW_Push"),
                      "Connector_Generic:Conn_02x07_Odd_Even": ("Connector_Generic", "Conn_02x07_Odd_Even"),
                      "Connector_Generic:Conn_01x02": ("Connector_Generic", "Conn_01x02"),
                      "power:GND": ("power", "GND"), "power:+3V3": ("power", "+3V3"),
                      "power:PWR_FLAG": ("power", "PWR_FLAG")}.items():
    sh.add_lib(lid, lib(lb, nm))
sh.add_lib("k57:TMS320F28P550SJ9PZ", MCU)

POWER = {"GND", "+3V3"}
y = 30.48
for net, shape in HIER:
    sh.hlabel(net, shape, 20.32, y)
    sh.wire(20.32, y, 27.94, y)
    if net in POWER:
        sh.power(net, 27.94, y, 270 if net == "GND" else 90)
    else:
        sh.label(net, 27.94, y, 0)
    y += 6.35

R0603, C0603, C0805 = "Resistor_SMD:R_0603_1608Metric", "Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_0805_2012Metric"

# выводы МК -> цепи (docs/k57-mcu-pinout-RU.md)
NET = {79: "PWM1A", 78: "PWM1B", 77: "PWM2A", 76: "PWM2B",
       80: "DRV_EN", 82: "PRECHARGE_EN", 85: "CONTACTOR_EN", 92: "GRID_CONT_EN",
       75: "CAN_TX", 89: "CAN_RX",
       74: "SPI_PICO", 90: "SPI_CLK", 93: "SPI_POCI", 52: "SPI_CS", 43: "ESP_RDY",
       9: "FRAM_PICO", 10: "FRAM_POCI", 11: "FRAM_CLK", 12: "FRAM_CS",
       1: "I2C_SDA", 100: "I2C_SCL", 66: "SCOPE", 67: "STROB",
       69: "XTAL_X1", 68: "XTAL_X2",
       40: "IA_ADC", 39: "IB_ADC", 17: "IBAT_ADC", 14: "UBAT_ADC", 36: "UBATT_ADC",
       19: "U220_ADC", 22: "USEC_ADC", 28: "NTC_ADC", 23: "DACA",
       24: "VREF_3V3", 25: "VREF_3V3", 26: "@GND", 27: "@GND",
       3: "@+3V3", 70: "@+3V3", 88: "@+3V3", 34: "+3V3A", 4: "VDD_1V2", 71: "VDD_1V2", 87: "VDD_1V2",
       5: "@GND", 45: "@GND", 72: "@GND", 86: "@GND", 33: "@GND", 73: "@GND",
       2: "XRSn", 62: "TMS", 60: "TCK", 63: "SCI_RX", 61: "SCI_TX",
       56: "BOOT_GPIO24", 64: "BOOT_GPIO32"}
for n in (26, 27, 73, 3, 70, 88, 4, 71, 87):
    NET[n] = None                           # эти соединяются проводниками ниже
for n in PINS:
    NET.setdefault(n, "!NC")                # свободные выводы - пометка «не подключено»
MX, MY = 254.0, 210.82
sh.place("k57:TMS320F28P550SJ9PZ", "U1", "TMS320F28P550SJ9PZ", "Package_QFP:LQFP-100_14x14mm_P0.5mm", MX, MY,
         nets={str(k): v for k, v in NET.items()},
         fields={"ref": (MX - 38.1, MY - 101.6), "val": (MX + 15.24, MY - 101.6), "just": "left"})

P = lambda n: sh.pin_xy("k57:TMS320F28P550SJ9PZ", MX, MY, 0, n)
sh.bus_power("GND", [P(26), P(27)], -1, tail=15.24)     # VREFLO
sh.bus_power("GND", [P(73)], -1, tail=15.24)            # VREGENZ - внутренний регулятор включён
sh.hbus([P(3), P(70), P(88)], "@+3V3")                  # VDDIO
sh.hbus([P(4), P(71), P(87)], "VDD_1V2")                # VDD - все три соединены (SPRSP85D)

# аналоговые входы: 100 Ом + 1 нФ C0G у самого вывода
AIN = [("IA_OUT", "IA_ADC"), ("IB_OUT", "IB_ADC"), ("IBAT_OUT", "IBAT_ADC"), ("UBAT_OUT", "UBAT_ADC"),
       ("UBATT_OUT", "UBATT_ADC"), ("U220_OUT", "U220_ADC"), ("USEC_OUT", "USEC_ADC"), ("NTC_OUT", "NTC_ADC")]
for i, (src, adc) in enumerate(AIN):
    x = 83.82 + (i % 4) * 25.4
    yy = 63.5 + (i // 4) * 38.1
    sh.place("Device:R", f"R{11 + i}", "100", R0603, x, yy, nets={"1": src, "2": adc})
    sh.place("Device:C", f"C{11 + i}", "1nF C0G", C0603, x + 10.16, yy + 12.7, nets={"1": adc, "2": "@GND"})
sh.place("Device:R", "R19", "100", R0603, 83.82, 152.4, nets={"1": "DACA", "2": "DAC_DBG"})

# питание: VDDIO 3 x 100 нФ + 10 мкФ, VDDA 2,2 мкФ + 100 нФ, VREFHI 2,2 мкФ, VDD 3 x 3,3 мкФ
CX, CY = 190.5, 330.2
caps = [("C21", "100nF", C0603, "@+3V3"), ("C22", "100nF", C0603, "@+3V3"), ("C23", "100nF", C0603, "@+3V3"),
        ("C24", "10uF 10V X7R", C0805, "@+3V3"),
        ("C25", "2.2uF 10V X7R", C0603, "+3V3A"), ("C26", "100nF", C0603, "+3V3A"),
        ("C27", "2.2uF 10V X7R", C0603, "VREF_3V3"),
        ("C28", "3.3uF 10V X7R", C0603, "VDD_1V2"), ("C29", "3.3uF 10V X7R", C0603, "VDD_1V2"),
        ("C30", "3.3uF 10V X7R", C0603, "VDD_1V2")]
for i, (ref, val, fp, net) in enumerate(caps):
    sh.place("Device:C", ref, val, fp, CX + i * 20.32, CY, nets={"1": net, "2": "@GND"})
sh.place("power:PWR_FLAG", "#FLG01", "PWR_FLAG", "", CX + 10 * 20.32, CY - 7.62, nets={"1": "VDD_1V2"},
         fields={"ref": (CX + 203.2, CY - 13.97), "val": (CX + 203.2, CY - 11.43), "just": "center"})

# сброс: 2,2 кОм к 3,3 В, 100 нФ, кнопка
sh.place("Device:R", "R31", "2.2k", R0603, 381.0, 63.5, nets={"1": "@+3V3", "2": "XRSn"})
sh.place("Device:C", "C31", "100nF", C0603, 393.7, 88.9, nets={"1": "XRSn", "2": "@GND"})
sh.place("Switch:SW_Push", "SW1", "Сброс", "Button_Switch_SMD:SW_SPST_TL3342", 411.48, 88.9,
         nets={"1": "XRSn", "2": "@GND"})
# кварц 20 МГц
sh.place("Device:Crystal_GND24", "Y1", "20MHz CL=9pF", "Crystal:Crystal_SMD_3225-4Pin_3.2x2.5mm", 381.0, 127.0,
         nets={"1": "XTAL_X1", "3": "XTAL_X2", "2": "@GND", "4": "@GND"},
         fields={"ref": (381.0, 119.38), "val": (381.0, 121.92), "just": "center"})
sh.place("Device:C", "C32", "12pF C0G", C0603, 368.3, 144.78, nets={"1": "XTAL_X1", "2": "@GND"})
sh.place("Device:C", "C33", "12pF C0G", C0603, 393.7, 144.78, nets={"1": "XTAL_X2", "2": "@GND"})
# выводы выбора загрузки: подтяжки, перемычка GPIO24 -> земля = ожидание SCI
sh.place("Device:R", "R32", "10k", R0603, 431.8, 63.5, nets={"1": "@+3V3", "2": "BOOT_GPIO24"})
sh.place("Device:R", "R33", "10k", R0603, 452.12, 63.5, nets={"1": "@+3V3", "2": "BOOT_GPIO32"})
sh.place("Connector_Generic:Conn_01x02", "JP1", "BOOT: замкнуть = ожидание SCI",
         "Connector_PinHeader_2.54mm:PinHeader_1x02_P2.54mm_Vertical", 444.5, 96.52,
         nets={"1": "BOOT_GPIO24", "2": "@GND"})
# отладка cJTAG: разъём 2x7 1,27 мм, разводка как J102 LAUNCHXL-F28P55X (SPRR499, лист 7)
sh.place("Device:R", "R34", "2.2k", R0603, 431.8, 152.4, nets={"1": "@+3V3", "2": "TMS"})
sh.place("Connector_Generic:Conn_02x07_Odd_Even", "J1", "Отладка cJTAG (к J102 LaunchPad)",
         "Connector_PinHeader_1.27mm:PinHeader_2x07_P1.27mm_Vertical", 469.9, 190.5,
         nets={"1": "!NC", "2": "TMS", "3": "@GND", "4": "TCK", "5": "@GND", "6": "!NC", "7": "!NC",
               "8": "!NC", "9": "@GND", "10": "DBG_RST", "11": "@GND", "12": "SCI_RX", "13": "@GND", "14": "SCI_TX"},
         fields={"ref": (469.9, 177.8), "val": (469.9, 180.34), "just": "center"})
sh.place("Device:R", "R35", "0", R0603, 431.8, 215.9, dnp=True, nets={"1": "DBG_RST", "2": "XRSn"})

sh.text("МИКРОКОНТРОЛЛЕР U1 - TMS320F28P550SJ9, LQFP-100 (PZ)\n"
        "Распределение выводов - docs/k57-mcu-pinout-RU.md. Имена выводов в символе - как в документации TI (SPRSP85D).\n"
        "Аналоговые входы: 100 Ом + 1 нФ C0G у самого вывода (R11-R18, C11-C18). DACA (A0) -> 100 Ом -> DAC_DBG.\n"
        "Питание (SPRSP85D, Pin Attributes): VDDIO - 0,1 мкФ на вывод (C21-C23) + 10 мкФ; VDDA - 2,2 мкФ (C25) + 100 нФ;\n"
        "  VDD 1,2 В от встроенного регулятора (VREGENZ на землю): все три вывода соединить, всего ~10 мкФ (C28-C30);\n"
        "  VREFHI <- REF2033 3,3 В, 2,2 мкФ к VREFLO (C27). PWR_FLAG на VDD_1V2: её питает встроенный регулятор.\n"
        "  Нарастание 3,3 В - 20-100 мВ/мкс (SRVDD33) - учесть на листе \"Питание\".\n"
        "Сброс XRSn: 2,2 кОм к 3,3 В (2,2-10 кОм по документации), 100 нФ (не больше), кнопка SW1.\n"
        "Кварц 20 МГц Y1, C32/C33 12 пФ под нагрузку 9 пФ [?] - по выбранному кварцу.\n"
        "Загрузка: GPIO24 и GPIO32 подтянуты к 3,3 В (запуск из флеша). JP1 замкнуть - режим ожидания SCI.\n"
        "Отладка J1 (2x7, 1,27 мм) - разводка как у J102 LaunchPad: 2 TMS, 4 TCK, 12 XDS_TXD -> SCI_RX (GPIO35),\n"
        "  14 XDS_RXD <- SCI_TX (GPIO37), 3/5/9/11/13 GND. Вывод 1 - это +3,3 В самого отладчика: НЕ подключать.\n"
        "  6/8 (TDO/TDI) в режиме cJTAG не нужны. 10 - сброс от отладчика: через R35 (не запаивать).\n"
        "  TMS - подтяжка 2,2 кОм (вывода TRSTn нет). Консоль UART идёт по тому же кабелю.\n"
        "Свободные выводы помечены \"не подключено\". GPIO14/15/22/23 в SysConfig отданы EPWM3/EPWM4 [?].",
        83.82, 355.6)
sh.write(os.path.join(PDIR, F), "K57 плата управления: МК", "МК, питание, приёмники 230 В, интерфейсы", FILE_UUID)
print("ok", len(HIER), "иерархических меток")
