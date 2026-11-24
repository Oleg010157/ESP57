# Листы «Подключение АКБ», «Звено», «Измерения 48 В» платы k57-power.
# Запуск: python -I hardware/tools/build_power2.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-power"
PDIR = os.path.join(HW, PRJ)
COMMENT = "Мост, драйверы, звено, шунты, предзаряд"

root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}

# обозначения с уже нарисованных листов - чтобы не повторить
for f in ["drivers.kicad_sch", "bridge.kicad_sch"]:
    t = parse(open(os.path.join(PDIR, f), encoding="utf-8").read())[0]
    for s in find(t, "symbol"):
        for p in find(s, "property"):
            if p[1] == "Reference":
                ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

def file_uuid(fname):
    return str(find(parse(open(os.path.join(PDIR, fname), encoding="utf-8").read())[0], "uuid")[0][1])

def new_sheet(fname, extra_libs):
    sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[fname])
    for lid, (lb, nm) in {"Device:R": ("Device", "R"), "Device:C": ("Device", "C"),
                          "power:GND": ("power", "GND"), "power:+12V": ("power", "+12V"), **extra_libs}.items():
        sh.add_lib(lid, lib(lb, nm))
    return sh

def hier_block(sh, specs, x=20.32, y0=30.48, step=7.62):
    y = y0
    for net, shape in specs:
        sh.hlabel(net, shape, x, y)
        sh.wire(x, y, x + 7.62, y)
        if net in ("GND", "+12V"):
            sh.power(net, x + 7.62, y, 270 if net == "GND" else 90)
        else:
            sh.label(net, x + 7.62, y, 0)
        y += step

R0603, R0805, R1206, R2512 = ("Resistor_SMD:R_0603_1608Metric", "Resistor_SMD:R_0805_2012Metric",
                              "Resistor_SMD:R_1206_3216Metric", "Resistor_SMD:R_2512_6332Metric")
C0603, C0805 = "Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_0805_2012Metric"
M8 = "MountingHole:MountingHole_8.4mm_M8_Pad"
M6 = "MountingHole:MountingHole_6.4mm_M6_DIN965_Pad"
MKDS2 = "TerminalBlock_Phoenix:TerminalBlock_Phoenix_MKDS-1,5-2-5.08_1x02_P5.08mm_Horizontal"
JST2 = "Connector_JST:JST_XH_B2B-XH-A_1x02_P2.50mm_Vertical"

# ================================================================ ПОДКЛЮЧЕНИЕ АКБ (3xx)
F = "batt_link.kicad_sch"
sh = new_sheet(F, {"Mechanical:MountingHole_Pad": ("Mechanical", "MountingHole_Pad"),
                   "Transistor_FET:IRF9540N": ("Transistor_FET", "IRF9540N"),
                   "Transistor_FET:BSS123": ("Transistor_FET", "BSS123"),
                   "Transistor_FET:AO3400A": ("Transistor_FET", "AO3400A"),
                   "Diode:BZX84Cxx": ("Diode", "BZX84Cxx"), "Diode:B340": ("Diode", "B340"),
                   "Diode:SMAJ64A": ("Diode", "SMAJ64A"), "Device:Fuse": ("Device", "Fuse"),
                   "Connector:Screw_Terminal_01x02": ("Connector", "Screw_Terminal_01x02")})
hier_block(sh, [("PRECHARGE_EN", "input"), ("CONTACTOR_EN", "input"), ("+12V", "passive"), ("GND", "passive"),
                ("DC_POS", "passive"), ("BAT_POS", "passive"), ("VBAT_RAW", "output")])
# клеммы под болт: контактор подключается снаружи между J301 и J302
sh.place("Mechanical:MountingHole_Pad", "J301", "BAT+ (после предохранителя)", M8, 106.68, 45.72, nets={"1": "BAT_POS"})
sh.place("Mechanical:MountingHole_Pad", "J302", "DC+ (к контактору)", M8, 175.26, 45.72, nets={"1": "DC_POS"})
# предзаряд: P-MOSFET в плюсе, резистор 22-33 Ом / 50 Вт снаружи на клеммах J303
sh.place("Transistor_FET:IRF9540N", "Q301", "IRF9540N", "Package_TO_SOT_THT:TO-220-3_Vertical", 137.16, 91.44,
         nets={"1": "PG_GATE", "2": "PRE_R", "3": "BAT_POS"})
sh.place("Device:R", "R301", "10k", R0805, 101.6, 91.44, nets={"1": "BAT_POS", "2": "PG_GATE"})
sh.place("Diode:BZX84Cxx", "D301", "BZX84C12", "Package_TO_SOT_SMD:SOT-23", 101.6, 116.84,
         nets={"3": "BAT_POS", "1": "PG_GATE", "2": "!NC"}, fields={"ref": (101.6, 110.49), "val": (101.6, 123.19), "just": "center"})
sh.place("Device:R", "R302", "10k", R0805, 137.16, 127.0, nets={"1": "PG_GATE", "2": "PG_DRV"})
sh.place("Transistor_FET:BSS123", "Q302", "BSS123", "Package_TO_SOT_SMD:SOT-23", 137.16, 152.4,
         nets={"1": "PG_G", "2": "@GND", "3": "PG_DRV"})
sh.place("Device:R", "R303", "100", R0603, 106.68, 152.4, nets={"1": "PRECHARGE_EN", "2": "PG_G"})
sh.place("Device:R", "R304", "100k", R0603, 119.38, 165.1, nets={"1": "PG_G", "2": "@GND"})
sh.place("Connector:Screw_Terminal_01x02", "J303", "Резистор предзаряда 22-33 Ом 50 Вт", MKDS2, 187.96, 91.44,
         nets={"1": "PRE_R", "2": "DC_POS"})
# катушка главного контактора 12 В
sh.place("Connector:Screw_Terminal_01x02", "J304", "Катушка контактора 12 В", MKDS2, 284.48, 76.2,
         nets={"1": "@+12V", "2": "COIL_N"})
sh.place("Diode:B340", "D302", "B340", "Diode_SMD:D_SMC", 248.92, 91.44, nets={"1": "@+12V", "2": "COIL_N"}, fields={"ref": (248.92, 85.09), "val": (248.92, 97.79), "just": "center"})
sh.place("Transistor_FET:AO3400A", "Q303", "AO3400A", "Package_TO_SOT_SMD:SOT-23", 271.78, 124.46,
         nets={"1": "CT_G", "2": "@GND", "3": "COIL_N"})
sh.place("Device:R", "R305", "100", R0603, 241.3, 124.46, nets={"1": "CONTACTOR_EN", "2": "CT_G"})
sh.place("Device:R", "R306", "100k", R0603, 254.0, 137.16, nets={"1": "CT_G", "2": "@GND"})
# питание платы управления - от АКБ до контактора, через свой предохранитель
sh.place("Device:Fuse", "F301", "2A 125V (NANO2)", "Fuse:Fuse_Littelfuse-NANO2-451_453", 330.2, 91.44,
         nets={"1": "BAT_POS", "2": "VBAT_RAW"})
# TVS на звене
sh.place("Diode:SMAJ64A", "D303", "SMCJ64A", "Diode_SMD:D_SMC", 330.2, 137.16, nets={"1": "DC_POS", "2": "@GND"},
         fields={"ref": (330.2, 130.81), "val": (330.2, 125.73), "just": "center"})
sh.text("ПОДКЛЮЧЕНИЕ АКБ (техзадание 6.4)\n"
        "J301 BAT+ - плюс АКБ после предохранителя класса T 150 А (вне платы). J302 DC+ - плюс звена.\n"
        "Главный контактор 200 А / 60 В - снаружи, болтами между J301 и J302. Контактор и предзаряд - только\n"
        "в ПЛЮСЕ: минус АКБ - земля платы управления.\n"
        "Предзаряд: Q301 (P-MOSFET 100 В) + резистор 22-33 Ом / 50 Вт на радиаторе (клеммы J303).\n"
        "  Закрыт: R301 держит затвор у истока. PRECHARGE_EN = 1 -> Q302 тянет затвор вниз через R302,\n"
        "  D301 (12 В) ограничивает Uзи. Ток через R301/R302 около 5 мА при 60 В.\n"
        "  [?] IRF9540N - кандидат; проверить импульс энергии предзаряда (~85 Дж) по SOA.\n"
        "Катушка контактора: J304, Q303 от CONTACTOR_EN, D302 - диод на катушке.\n"
        "F301 2 А -> VBAT_RAW: питание платы управления до контактора, иначе МК не управлял бы предзарядом.\n"
        "D303 - TVS на звене; ключи он не защищает (ограничение выше 100 В), гасит выброс кабелей. [?]\n"
        "Последовательность (прошивка): предзаряд -> DC+ >= ~90 % BAT+ -> контактор; не дождались - авария.",
        81.28, 200.66)
sh.write(os.path.join(PDIR, F), "K57 силовая плата: Подключение АКБ", COMMENT, file_uuid(F))

# ================================================================ ЗВЕНО (4xx)
F = "dc_link.kicad_sch"
sh = new_sheet(F, {"Device:C_Polarized": ("Device", "C_Polarized"), "Device:LED": ("Device", "LED")})
hier_block(sh, [("DC_POS", "passive"), ("GND", "passive")])
for i in range(10):
    x = 81.28 + i * 27.94
    sh.place("Device:C_Polarized", f"C{401 + i}", "4700uF 100V", "Capacitor_THT:CP_Radial_D35.0mm_P10.00mm_SnapIn",
             x, 63.5, nets={"1": "DC_POS", "2": "@GND"}, extra={"Part": "Nichicon LGU2A472MELC или аналог 105 °C"})
for i in range(3):
    sh.place("Device:R", f"R{401 + i}", "6.8k 1W", R2512, 81.28 + i * 27.94, 114.3, nets={"1": "DC_POS", "2": "@GND"})
sh.place("Device:R", "R404", "10k", R1206, 182.88, 106.68, nets={"1": "DC_POS", "2": "LED_M"})
sh.place("Device:R", "R405", "10k", R1206, 182.88, 137.16, nets={"1": "LED_M", "2": "LED_A"})
sh.place("Device:LED", "D401", "красный", "LED_SMD:LED_0805_2012Metric", 210.82, 162.56, nets={"2": "LED_A", "1": "@GND"},
         fields={"ref": (210.82, 156.21), "val": (210.82, 168.91), "just": "center"})
sh.text("ЗВЕНО (техзадание 6.3)\n"
        "C401-C410 - 10 x 4700 мкФ 100 В (47 мФ). Пульсации на частоте ШИМ - до 60 А действующих\n"
        "(моделирование), ~6 А на банку с запасом по ресурсу. [?] Серия с большим ресурсом и расчёт срока службы.\n"
        "Ставить симметрично относительно плеч моста. Плёнка и керамика у ключей - на листе \"Мост\".\n"
        "R401-R403 - разряд звена: 3 x 6,8 кОм 1 Вт параллельно (2,27 кОм, ~0,5 Вт на каждый при 60 В).\n"
        "  Постоянная времени ~107 с: с 60 до 20 В примерно за 2 минуты.\n"
        "D401 - светодиод \"звено под напряжением\" (R404 + R405, ~2,5 мА при 50 В): горит - не трогать.",
        81.28, 170.18)
sh.write(os.path.join(PDIR, F), "K57 силовая плата: Звено", COMMENT, file_uuid(F))

# ================================================================ ИЗМЕРЕНИЯ 48 В (5xx)
F = "meas_48v.kicad_sch"
sh = new_sheet(F, {"Device:R_Shunt": ("Device", "R_Shunt"), "Amplifier_Current:INA240A1PW": ("Amplifier_Current", "INA240A1PW"),
                   "Amplifier_Operational:TLV9064": ("Amplifier_Operational", "TLV9064"),
                   "Mechanical:MountingHole_Pad": ("Mechanical", "MountingHole_Pad"),
                   "Device:Thermistor_NTC": ("Device", "Thermistor_NTC"),
                   "Connector_Generic:Conn_01x02": ("Connector_Generic", "Conn_01x02"),
                   "power:+3V3": ("power", "+3V3")})
# своё питание аналоговой части: +3V3A приходит иерархической меткой
hier_block(sh, [("PHASE_B", "passive"), ("DC_POS", "passive"), ("BAT_POS", "passive"), ("GND", "passive"),
                ("+3V3A", "passive"), ("VBIAS_1V65", "passive"), ("VREF_3V3", "passive"),
                ("IA_OUT", "output"), ("IBAT_OUT", "output"), ("UBAT_OUT", "output"),
                ("UBATT_OUT", "output"), ("NTC_OUT", "output")])
SHUNT_FP = ""   # [?] посадочное место шунта 5930 с четырёхпроводным подключением - создать по документации Bourns

def current_channel(n, y, hi_net, lo_net, sense_p_side, out_net, title):
    """Шунт + INA240A1: n - сотня позиционных обозначений (50x / 51x)."""
    rs, u = f"R{n + 1}", f"U{n + 1}"
    sp, sn = f"{title}_SP", f"{title}_SN"
    # шунт: 1 и 4 - силовые, 2 и 3 - измерительные (2 у вывода 1, 3 у вывода 4)
    s_nets = {"1": hi_net, "4": lo_net}
    if sense_p_side == 1:
        s_nets.update({"2": sp, "3": sn})
    else:
        s_nets.update({"2": sn, "3": sp})
    sh.place("Device:R_Shunt", rs, "0.2mR 15W 5930", SHUNT_FP, 111.76, y, nets=s_nets,
             extra={"Part": "Bourns CSS2H-5930R-L200F или Milliohm HoLRS5930-15W-0.2mR"},
             fields={"ref": (106.68, y - 1.27), "val": (106.68, y + 1.27), "just": "right"})
    sh.place("Device:R", f"R{n + 2}", "0", R0603, 142.24, y - 12.7, nets={"1": sp, "2": f"{title}_INP"})
    sh.place("Device:R", f"R{n + 3}", "0", R0603, 154.94, y - 12.7, nets={"1": sn, "2": f"{title}_INN"})
    sh.place("Device:C", f"C{n + 1}", "100nF", C0603, 167.64, y - 12.7, dnp=True,
             nets={"1": f"{title}_INP", "2": f"{title}_INN"})
    sh.place("Amplifier_Current:INA240A1PW", u, "INA240A1PW", "Package_SO:TSSOP-8_4.4x3mm_P0.65mm", 205.74, y,
             nets={"2": f"{title}_INP", "3": f"{title}_INN", "1": "@GND", "4": "@GND", "5": "+3V3A",
                   "6": "VBIAS_1V65", "7": "VBIAS_1V65", "8": f"{title}_AMP"},
             fields={"ref": (215.9, y - 8.89), "val": (215.9, y + 10.16), "just": "left"})
    sh.place("Device:C", f"C{n + 2}", "100nF", C0603, 236.22, y - 15.24, nets={"1": "+3V3A", "2": "@GND"})
    sh.place("Device:C", f"C{n + 3}", "100nF", C0603, 254.0, y - 15.24, nets={"1": "VBIAS_1V65", "2": "@GND"})
    sh.place("Device:R", f"R{n + 4}", "47", R0603, 236.22, y + 10.16, nets={"1": f"{title}_AMP", "2": out_net})

current_channel(500, 50.8, "PHASE_B", "XFMR_B", 1, "IA_OUT", "IA")
sh.place("Mechanical:MountingHole_Pad", "J501", "XFMR_B", M6, 81.28, 76.2, nets={"1": "XFMR_B"})
current_channel(510, 116.84, "BAT_NEG", "@GND", 4, "IBAT_OUT", "IBAT")
sh.place("Mechanical:MountingHole_Pad", "J502", "BAT- АКБ", M8, 81.28, 142.24, nets={"1": "BAT_NEG"})

# делители напряжения и NTC + буферы TLV9064
def divider(n, y, src, node):
    sh.place("Device:R", f"R{n + 1}", "120k 0.1%", R1206, 299.72, y, nets={"1": src, "2": f"{node}_M"})
    sh.place("Device:R", f"R{n + 2}", "120k 0.1%", R1206, 299.72, y + 25.4, nets={"1": f"{node}_M", "2": node})
    sh.place("Device:R", f"R{n + 3}", "10k 0.1%", R0603, 312.42, y + 43.18, nets={"1": node, "2": "@GND"})
    sh.place("Device:C", f"C{n + 1}", "4.7nF C0G", C0603, 327.66, y + 43.18, nets={"1": node, "2": "@GND"})

divider(520, 38.1, "DC_POS", "UBAT_DIV")
divider(530, 101.6, "BAT_POS", "UBATT_DIV")
sh.place("Device:R", "R541", "10k 0.1%", R0603, 299.72, 167.64, nets={"1": "VREF_3V3", "2": "NTC_DIV"})
sh.place("Connector_Generic:Conn_01x02", "J503", "NTC радиатора 10k B3950", JST2, 284.48, 190.5,
         nets={"1": "NTC_DIV", "2": "@GND"})
sh.place("Device:C", "C541", "100nF", C0603, 325.12, 182.88, nets={"1": "NTC_DIV", "2": "@GND"})
OPA = "Amplifier_Operational:TLV9064"
OFP = "Package_SO:TSSOP-14_4.4x5mm_P0.65mm"
for unit, (inp, amp, out, y) in enumerate([("UBAT_DIV", "UBAT_BUF", "UBAT_OUT", 58.42),
                                           ("UBATT_DIV", "UBATT_BUF", "UBATT_OUT", 121.92),
                                           ("NTC_DIV", "NTC_BUF", "NTC_OUT", 177.8)], start=1):
    sh.place(OPA, "U521", "TLV9064IPWR", OFP, 360.68, y, unit=unit,
             fields={"ref": (360.68, y - 7.62), "val": (360.68, y + 7.62), "just": "center"},
             nets={["3", "5", "10"][unit - 1]: inp, ["2", "6", "9"][unit - 1]: amp, ["1", "7", "8"][unit - 1]: amp})
    sh.place("Device:R", f"R5{unit + 1}4", "47", R0603, 383.54, y + 12.7, nets={"1": amp, "2": out})
# секция D - не используется: повторитель с входом на земле
sh.place(OPA, "U521", "TLV9064IPWR", OFP, 360.68, 223.52, unit=4,
         fields={"ref": (360.68, 215.9), "val": (360.68, 231.14), "just": "center"}, nets={"12": "@GND", "13": "OPA_D", "14": "OPA_D"})
sh.place(OPA, "U521", "TLV9064IPWR", OFP, 386.08, 223.52, unit=5, nets={"4": "+3V3A", "11": "@GND"})
sh.place("Device:C", "C551", "100nF", C0603, 403.86, 223.52, nets={"1": "+3V3A", "2": "@GND"})
sh.text("ИЗМЕРЕНИЯ 48 В (техзадание 6.5)\n"
        "Ia: шунт R501 0,2 мОм между PHASE_B и клеммой трансформатора J501 (XFMR_B) + U501 INA240A1 (x20).\n"
        "iBat: шунт R511 между клеммой J502 (BAT-) и GND + U511 INA240A1. GND платы - минус звена.\n"
        "  REF1/REF2 = VBIAS_1V65 от REF2033 на плате управления: нуль и шкала АЦП от одной опоры.\n"
        "  Шкала +-250 А -> 1,65 +- 1,0 В. R502/R503 (0 Ом) и C501 (не запаивать) - место под фильтр.\n"
        "  Знак тока задаёт прошивка (gt.iSign); измерительные выводы шунтов - отдельными дорожками.\n"
        "  [?] Посадочное место шунта 5930 с четырёхпроводным подключением - создать.\n"
        "uBat (звено, после контактора) и uBatt (клеммы АКБ): 2 x 120 кОм + 10 кОм 0,1 %, 75 В -> 3,0 В,\n"
        "  4,7 нФ (~3,5 кГц) + повторитель U521 (TLV9064): сигнал по кабелю идёт с низким сопротивлением.\n"
        "  uBat идёт и на компаратор защиты от перенапряжения (CMPSS1 на плате управления).\n"
        "NTC радиатора 10 кОм B3950 (J503): делитель от VREF_3V3, повторитель. Секция D U521 не используется.\n"
        "Выходы - через 47 Ом к разъёму; RC перед АЦП - на плате управления.", 81.28, 238.76)
sh.write(os.path.join(PDIR, F), "K57 силовая плата: Измерения 48 В", COMMENT, file_uuid(F))
print("ok")
