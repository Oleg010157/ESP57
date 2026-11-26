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

import sch_lib
def hier_block(sh, specs):
    """Иерархические метки слева; каждая проводком к локальной метке или символу питания."""
    sch_lib.hier_block(sh, specs, ("GND", "+12V"))

R0603, R0805, R1206, R2512 = ("Resistor_SMD:R_0603_1608Metric", "Resistor_SMD:R_0805_2012Metric",
                              "Resistor_SMD:R_1206_3216Metric", "Resistor_SMD:R_2512_6332Metric")
C0603, C0805 = "Capacitor_SMD:C_0603_1608Metric", "Capacitor_SMD:C_0805_2012Metric"
M8 = "MountingHole:MountingHole_8.4mm_M8_Pad"
M6 = "MountingHole:MountingHole_6.4mm_M6_DIN965_Pad"
MKDS2 = "TerminalBlock_Phoenix:TerminalBlock_Phoenix_MKDS-1,5-2-5.08_1x02_P5.08mm_Horizontal"
JST2 = "Connector_JST:JST_XH_B2B-XH-A_1x02_P2.50mm_Vertical"

# ================================================================ ПОДКЛЮЧЕНИЕ АКБ (3xx) - силовой путь проводами
F = "batt_link.kicad_sch"
sh = new_sheet(F, {"Connector_Generic:Conn_01x01": ("Connector_Generic", "Conn_01x01"),
                   "Relay:Relay_SPST-NO": ("Relay", "Relay_SPST-NO"),
                   "Transistor_FET:IRF9540N": ("Transistor_FET", "IRF9540N"),
                   "Transistor_FET:BSS123": ("Transistor_FET", "BSS123"),
                   "Transistor_FET:AO3400A": ("Transistor_FET", "AO3400A"),
                   "Diode:BZX84Cxx": ("Diode", "BZX84Cxx"), "Diode:B340": ("Diode", "B340"),
                   "Diode:SMAJ64A": ("Diode", "SMAJ64A"), "Device:Fuse": ("Device", "Fuse"),
                   "Connector:Screw_Terminal_01x02": ("Connector", "Screw_Terminal_01x02"),
                   "Device:R_Shunt": ("Device", "R_Shunt"),
                   "Amplifier_Current:INA240A1PW": ("Amplifier_Current", "INA240A1PW")})
hier_block(sh, [("PRECHARGE_EN", "input"), ("CONTACTOR_EN", "input"), ("+12V", "passive"), ("GND", "passive"),
                ("DC_POS", "passive"), ("BAT_POS", "passive"), ("VBAT_RAW", "output"),
                ("IBAT_OUT", "output"), ("+3V3A", "passive"), ("VBIAS_1V5", "passive")])
from collections import Counter

def bus(y, taps_x):
    """Горизонтальная шина через все отводы; точки соединения внутри и на конце с двумя проводами."""
    xs = sorted(taps_x)
    for a, b in zip(xs, xs[1:]):
        sh.wire(a, y, b, y)
    for x in xs:
        if x not in (xs[0], xs[-1]) or taps_x[x] >= 2:
            sh.junction(x, y)

Y_B, Y_D = 50.8, 152.4                        # шина BAT_POS (плюс АКБ) и шина DC_POS (плюс звена)
tb, td = Counter(), Counter()
# J301 - плюс АКБ после внешнего предохранителя
sh.place("Connector_Generic:Conn_01x01", "J301", "BAT+, болт M8", M8, 88.9, Y_B, ang=180, nets={"1": None},
         fields={"ref": (88.9, Y_B - 7.62), "val": (88.9, Y_B - 5.08), "just": "center"})
tb[93.98] += 0
sh.label("BAT_POS", 99.06, Y_B, 0)
# главный контактор - на шасси: контакты между шинами, катушка - от клемм J304
KX, KY = 132.08, 101.6
sh.place("Relay:Relay_SPST-NO", "K301", "Главный контактор (на шасси)", "", KX, KY, offboard=True,
         nets={"14": None, "13": None, "A1": "@+12V", "A2": "COIL_N"},
         fields={"ref": (KX - 12.7, KY - 2.54), "val": (KX - 12.7, KY), "just": "right"})
sh.wire(KX + 7.62, KY - 7.62, KX + 7.62, Y_B); tb[KX + 7.62] += 1
sh.wire(KX + 5.08, KY + 7.62, KX + 5.08, Y_D); td[KX + 5.08] += 1
# J302 - плюс звена (от контактора)
sh.place("Connector_Generic:Conn_01x01", "J302", "DC+, болт M8", M8, 152.4, Y_D + 5.08, ang=270, nets={"1": None},
         fields={"ref": (154.94, Y_D + 7.62), "val": (154.94, Y_D + 10.16), "just": "left"})
td[152.4] += 1
# предзаряд: P-MOSFET Q301 (исток - к АКБ, сток - к внешнему резистору на клеммах J303)
QX, QY = 165.1, 76.2
sh.place("Transistor_FET:IRF9540N", "Q301", "IRF9540N", "Package_TO_SOT_THT:TO-220-3_Vertical", QX, QY, ang=180,
         nets={"1": None, "2": None, "3": None}, fields={"ref": (QX - 7.62, QY - 1.27), "val": (QX - 7.62, QY + 1.27), "just": "right"})
sh.wire(QX - 2.54, QY - 5.08, QX - 2.54, Y_B); tb[QX - 2.54] += 1
sh.place("Connector:Screw_Terminal_01x02", "J303", "Резистор предзаряда (снаружи)", MKDS2, 172.72, 116.84,
         nets={"1": None, "2": None}, fields={"ref": (170.18, 124.46), "val": (170.18, 127.0), "just": "left"})
sh.wire(QX - 2.54, QY + 5.08, QX - 2.54, 116.84); sh.wire(QX - 2.54, 116.84, 167.64, 116.84)
sh.wire(167.64, 119.38, 167.64, Y_D); td[167.64] += 1
# затвор Q301: R301 к истоку, стабилитрон D301 12 В, R302 к ключу Q302
GY = QY
sh.wire(QX + 5.08, GY, 195.58, GY)
sh.place("Device:R", "R301", "10k", R0805, 180.34, 63.5, nets={"1": None, "2": None},
         fields={"ref": (176.53, 62.23), "val": (176.53, 64.77), "just": "right"})
sh.wire(180.34, 59.69, 180.34, Y_B); tb[180.34] += 1
sh.wire(180.34, 67.31, 180.34, GY); sh.junction(180.34, GY)
sh.place("Diode:BZX84Cxx", "D301", "BZX84C12", "Package_TO_SOT_SMD:SOT-23", 190.5, 63.5, ang=270,
         nets={"3": None, "1": None, "2": "!NC"})
sh.wire(190.5, 59.69, 190.5, Y_B); tb[190.5] += 1
sh.wire(190.5, 67.31, 190.5, GY); sh.junction(190.5, GY)
sh.place("Device:R", "R302", "10k", R0805, 195.58, 86.36, nets={"1": None, "2": None})
sh.wire(195.58, GY, 195.58, 82.55)
QX2, QY2 = 215.9, 101.6
sh.wire(195.58, 90.17, 195.58, 93.98); sh.wire(195.58, 93.98, QX2 + 2.54, 93.98); sh.wire(QX2 + 2.54, 93.98, QX2 + 2.54, QY2 - 5.08)
sh.place("Transistor_FET:BSS123", "Q302", "BSS123", "Package_TO_SOT_SMD:SOT-23", QX2, QY2,
         nets={"1": None, "2": "@GND", "3": None}, fields={"ref": (QX2 + 5.08, QY2 - 1.27), "val": (QX2 + 5.08, QY2 + 1.27), "just": "left"})
sh.place("Device:R", "R303", "100", R0603, QX2 - 12.7, QY2, ang=90, nets={"1": "PRECHARGE_EN", "2": None},
         fields={"ref": (QX2 - 12.7, QY2 - 3.81), "val": (QX2 - 12.7, QY2 + 3.81), "just": "center"})
sh.wire(QX2 - 8.89, QY2, QX2 - 5.08, QY2)
sh.place("Device:R", "R304", "100k", R0603, QX2 - 7.62, QY2 + 7.62, nets={"1": None, "2": "@GND"},
         fields={"ref": (QX2 - 11.43, QY2 + 6.35), "val": (QX2 - 11.43, QY2 + 8.89), "just": "right"})
sh.wire(QX2 - 7.62, QY2, QX2 - 7.62, QY2 + 3.81); sh.junction(QX2 - 7.62, QY2)
# питание платы управления - от АКБ до контактора, через свой предохранитель
sh.place("Device:Fuse", "F301", "2A 125V (NANO2)", "Fuse:Fuse_Littelfuse-NANO2-451_453", 231.14, 63.5,
         nets={"1": None, "2": "VBAT_RAW"})
sh.wire(231.14, 59.69, 231.14, Y_B); tb[231.14] += 1
# TVS на звене
sh.place("Diode:SMAJ64A", "D303", "SMCJ64A", "Diode_SMD:D_SMC", 226.06, Y_D + 7.62, ang=270, nets={"1": None, "2": "@GND"})
sh.wire(226.06, Y_D + 3.81, 226.06, Y_D); td[226.06] += 1
td[238.76] += 0
bus(Y_B, tb); bus(Y_D, td)
sh.label("DC_POS", 238.76, Y_D, 0)

# катушка контактора: клеммы J304, ключ Q303, диод D302
JX, JY = 114.3, 172.72
sh.place("Connector:Screw_Terminal_01x02", "J304", "Катушка контактора 12 В", MKDS2, JX, JY, nets={"1": None, "2": None},
         fields={"ref": (JX - 2.54, JY - 7.62), "val": (JX - 2.54, JY - 5.08), "just": "left"})
sh.wire(JX - 5.08, JY, 96.52, JY)
sh.power("+12V", 96.52, JY, 90)
sh.place("Diode:B340", "D302", "B340", "Diode_SMD:D_SMC", 96.52, JY + 7.62, ang=270, nets={"1": None, "2": None},
         fields={"ref": (92.71, JY + 6.35), "val": (92.71, JY + 8.89), "just": "right"})
sh.wire(96.52, JY, 96.52, JY + 3.81)
sh.wire(JX - 5.08, JY + 2.54, 101.6, JY + 2.54); sh.wire(101.6, JY + 2.54, 101.6, JY + 11.43)
sh.wire(96.52, JY + 11.43, 101.6, JY + 11.43); sh.junction(101.6, JY + 11.43)
sh.label("COIL_N", 101.6, JY + 15.24, 0)                # на стоке Q303 - подальше от номеров выводов J304
QX3, QY3 = 99.06, JY + 24.13
sh.place("Transistor_FET:AO3400A", "Q303", "AO3400A", "Package_TO_SOT_SMD:SOT-23", QX3, QY3,
         nets={"1": None, "2": "@GND", "3": None}, fields={"ref": (QX3 + 5.08, QY3 - 1.27), "val": (QX3 + 5.08, QY3 + 1.27), "just": "left"})
sh.wire(101.6, JY + 11.43, 101.6, QY3 - 5.08)
sh.place("Device:R", "R305", "100", R0603, QX3 - 12.7, QY3, ang=90, nets={"1": "CONTACTOR_EN", "2": None},
         fields={"ref": (QX3 - 12.7, QY3 - 3.81), "val": (QX3 - 12.7, QY3 + 3.81), "just": "center"})
sh.wire(QX3 - 8.89, QY3, QX3 - 5.08, QY3)
sh.place("Device:R", "R306", "100k", R0603, QX3 - 7.62, QY3 + 7.62, nets={"1": None, "2": "@GND"},
         fields={"ref": (QX3 - 11.43, QY3 + 6.35), "val": (QX3 - 11.43, QY3 + 8.89), "just": "right"})
sh.wire(QX3 - 7.62, QY3, QX3 - 7.62, QY3 + 3.81); sh.junction(QX3 - 7.62, QY3)

# ток АКБ: J502 (BAT-) -> шунт R511 -> GND. Шунт развёрнут: вывод 1 - к GND, 4 - к BAT-;
# IN+ INA240 - со стороны GND, как и раньше: разряд АКБ даёт положительный сигнал
SX, SY = 304.8, 177.8
sh.place("Connector_Generic:Conn_01x01", "J502", "BAT-, болт M8", M8, SX - 30.48, SY, ang=180, nets={"1": None},
         fields={"ref": (SX - 30.48, SY - 7.62), "val": (SX - 30.48, SY - 5.08), "just": "center"})
sh.wire(SX - 25.4, SY, SX - 5.08, SY)
sh.place("Device:R_Shunt", "R511", "0.2mR 15W 5930", "", SX, SY, ang=270, nets={"1": None, "2": None, "3": None, "4": None},
         extra={"Part": "Bourns CSS2H-5930R-L200F или Milliohm HoLRS5930-15W-0.2mR"},
         fields={"ref": (SX, SY - 6.35), "val": (SX, SY - 3.81), "just": "center"})
sh.wire(SX + 5.08, SY, SX + 17.78, SY)
sh.power("GND", SX + 17.78, SY, 270)
XP, XN = SX + 2.54, SX - 5.08
IX, IY = SX + 15.24, SY + 38.1
sh.wire(XP, SY + 2.54, XP, SY + 10.16)
sh.wire(SX - 2.54, SY + 2.54, SX - 2.54, SY + 5.08); sh.wire(SX - 2.54, SY + 5.08, XN, SY + 5.08); sh.wire(XN, SY + 5.08, XN, SY + 10.16)
sh.place("Device:R", "R512", "0", R0603, XP, SY + 13.97, nets={"1": None, "2": None},
         fields={"ref": (XP + 3.81, SY + 12.7), "val": (XP + 3.81, SY + 15.24), "just": "left"})
sh.place("Device:R", "R513", "0", R0603, XN, SY + 13.97, nets={"1": None, "2": None},
         fields={"ref": (XN - 3.81, SY + 12.7), "val": (XN - 3.81, SY + 15.24), "just": "right"})
sh.wire(XP, SY + 17.78, XP, IY - 2.54); sh.wire(XP, IY - 2.54, IX - 7.62, IY - 2.54)
sh.wire(XN, SY + 17.78, XN, IY + 2.54); sh.wire(XN, IY + 2.54, IX - 7.62, IY + 2.54)
sh.place("Device:C", "C511", "100nF", C0603, (XP + XN) / 2, SY + 25.4, ang=270, dnp=True, nets={"1": None, "2": None},
         fields={"ref": ((XP + XN) / 2, SY + 21.59), "val": ((XP + XN) / 2, SY + 29.21), "just": "center"})
sh.junction(XP, SY + 25.4); sh.junction(XN, SY + 25.4)
sh.place("Amplifier_Current:INA240A1PW", "U511", "INA240A1PW", "Package_SO:TSSOP-8_4.4x3mm_P0.65mm", IX, IY,
         nets={"2": None, "3": None, "1": None, "4": None, "5": "+3V3A", "6": None, "7": None, "8": None},
         fields={"ref": (IX + 5.08, IY - 8.89), "val": (IX + 5.08, IY + 12.7), "just": "left"})
PI = lambda k: sh.pin_xy("Amplifier_Current:INA240A1PW", IX, IY, 0, k)
gx, gy = PI(4)
sh.wire(gx, gy, gx, gy + 2.54); sh.wire(gx, gy + 2.54, gx - 5.08, gy + 2.54)
sh.power("GND", gx - 5.08, gy + 2.54, 270)
sh.hbus([PI(6), PI(7)], "VBIAS_1V5", up=False, label_at="right")
ox, oy = PI(8)
sh.place("Device:R", "R514", "47", R0603, ox + 10.16, oy, ang=90, nets={"1": None, "2": "IBAT_OUT"},
         fields={"ref": (ox + 10.16, oy - 3.81), "val": (ox + 10.16, oy + 3.81), "just": "center"})
sh.wire(ox, oy, ox + 6.35, oy)
sh.place("Device:C", "C512", "100nF", C0603, IX + 22.86, IY - 17.78, nets={"1": "+3V3A", "2": "@GND"})
sh.place("Device:C", "C513", "100nF", C0603, IX + 35.56, IY - 17.78, nets={"1": "VBIAS_1V5", "2": "@GND"})

sh.text("ПОДКЛЮЧЕНИЕ АКБ (техзадание 6.4)\n"
        "Сверху - плюс АКБ (BAT_POS, клемма J301 после внешнего предохранителя класса T 150 А), снизу - плюс звена (DC_POS, J302).\n"
        "K301 - главный контактор 200 А / 60 В на шасси, болтами между J301 и J302: на плату и в закупку не входит,\n"
        "  нарисован для наглядности. Контактор и предзаряд - только в ПЛЮСЕ: минус АКБ - земля платы управления.\n"
        "Предзаряд - параллельно контактору: Q301 (P-MOSFET 100 В) + резистор 22-33 Ом / 50 Вт на радиаторе (клеммы J303).\n"
        "  Закрыт: R301 держит затвор у истока. PRECHARGE_EN = 1 -> Q302 тянет затвор вниз через R302,\n"
        "  D301 (12 В) ограничивает Uзи. Ток через R301/R302 около 5 мА при 60 В.\n"
        "  [?] IRF9540N - кандидат; проверить импульс энергии предзаряда (~85 Дж) по SOA.\n"
        "Катушка контактора: J304, Q303 от CONTACTOR_EN, D302 - диод на катушке.\n"
        "F301 2 А -> VBAT_RAW: питание платы управления до контактора, иначе МК не управлял бы предзарядом.\n"
        "D303 - TVS на звене; ключи он не защищает (ограничение выше 100 В), гасит выброс кабелей. [?]\n"
        "iBat: минус АКБ (J502) -> шунт R511 0,2 мОм -> GND платы (минус звена). Измерение - как у Ia на листе «Мост»:\n"
        "  выводы 2 и 3 шунта - отдельными дорожками, R512/R513 (0 Ом), C511 (не запаивать), U511 INA240A1.\n"
        "  IN+ - со стороны GND: разряд АКБ -> сигнал выше 1,5 В. +-250 А -> 1,5 +- 1,0 В.\n"
        "Последовательность (прошивка): предзаряд -> DC+ >= ~90 % BAT+ -> контактор; не дождались - авария.",
        76.2, 246.38)
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
hier_block(sh, [("DC_POS", "passive"), ("BAT_POS", "passive"), ("GND", "passive"),
                ("+3V3A", "passive"), ("VREF_3V0", "passive"),
                ("UBAT_OUT", "output"),
                ("UBATT_OUT", "output"), ("NTC_OUT", "output")])
# каналы тока (шунты с INA240) нарисованы у силовых цепей: Ia - на листе «Мост», iBat - «Подключение АКБ»

# делители напряжения и NTC + буферы TLV9064
def divider(n, y, src, node):
    sh.place("Device:R", f"R{n + 1}", "120k 0.1%", R1206, 299.72, y, nets={"1": src, "2": None})
    sh.place("Device:R", f"R{n + 2}", "120k 0.1%", R1206, 299.72, y + 25.4, nets={"1": None, "2": node})
    # средняя точка - проводником, метка поперёк (две вертикальные метки упирались друг в друга)
    (xa, ya), (xb, yb) = sh.pin_xy("Device:R", 299.72, y, 0, 2), sh.pin_xy("Device:R", 299.72, y + 25.4, 0, 1)
    sh.wire(xa, ya, xb, yb)
    sh.label(f"{node}_M", xa, (ya + yb) / 2, 0)
    sh.place("Device:R", f"R{n + 3}", "10k 0.1%", R0603, 312.42, y + 43.18, nets={"1": node, "2": "@GND"})
    sh.place("Device:C", f"C{n + 1}", "4.7nF C0G", C0603, 327.66, y + 43.18, nets={"1": node, "2": "@GND"})

divider(520, 38.1, "DC_POS", "UBAT_DIV")
divider(530, 101.6, "BAT_POS", "UBATT_DIV")
sh.place("Device:R", "R541", "10k 0.1%", R0603, 299.72, 167.64, nets={"1": "VREF_3V0", "2": "NTC_DIV"})
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
         fields={"ref": (360.68, 215.9), "val": (360.68, 231.14), "just": "center"}, nets={"12": None, "13": "OPA_D", "14": "OPA_D"})
gx, gy = sh.pin_xy(OPA, 360.68, 223.52, 0, 12, unit=4)     # земля входа - подальше от метки OPA_D
sh.wire(gx, gy, gx - 12.7, gy)
sh.power("GND", gx - 12.7, gy, 270)
sh.place(OPA, "U521", "TLV9064IPWR", OFP, 386.08, 223.52, unit=5, nets={"4": "+3V3A", "11": "@GND"})
sh.place("Device:C", "C551", "100nF", C0603, 403.86, 223.52, nets={"1": "+3V3A", "2": "@GND"})
sh.text("ИЗМЕРЕНИЯ 48 В (техзадание 6.5)\n"
        "Токи - у своих силовых цепей: Ia (R501, U501) - на листе «Мост», iBat (R511, U511) - «Подключение АКБ».\n"
        "uBat (звено, после контактора) и uBatt (клеммы АКБ): 2 x 120 кОм + 10 кОм 0,1 %, 75 В -> 3,0 В,\n"
        "  4,7 нФ (~3,5 кГц) + повторитель U521 (TLV9064): сигнал по кабелю идёт с низким сопротивлением.\n"
        "  uBat идёт и на компаратор защиты от перенапряжения (CMPSS1 на плате управления).\n"
        "NTC радиатора 10 кОм B3950 (J503): делитель от VREF_3V0, повторитель. Секция D U521 не используется.\n"
        "Выходы - через 47 Ом к разъёму; RC перед АЦП - на плате управления.", 81.28, 238.76)
sh.write(os.path.join(PDIR, F), "K57 силовая плата: Измерения 48 В", COMMENT, file_uuid(F))
# ---------------------------------------------------------------- верхний лист: выводы блоков по новому содержимому листов
import copy
ROOTF = os.path.join(PDIR, PRJ + ".kicad_sch")
root = parse(open(ROOTF, encoding="utf-8").read())[0]
changed = False
def blk(fname):
    return next(s for s in find(root, "sheet") if [p[2] for p in find(s, "property") if p[1] == "Sheetfile"][0] == fname)
rs = Sheet(PRJ, "/" + ROOT)

def add_right_pins(fname, specs):
    """Добавить выводы на правый край блока листа (под последним), с проводком и меткой; блок растёт вниз."""
    global changed
    b = blk(fname)
    bx, by = (float(v) for v in find(b, "at")[0][1:3])
    bw, bh = (float(v) for v in find(b, "size")[0][1:3])
    right = [p for p in find(b, "pin") if abs(float(find(p, "at")[0][1]) - (bx + bw)) < 0.01]
    have = {str(p[1]) for p in find(b, "pin")}
    y = max(float(find(p, "at")[0][2]) for p in right)
    for nm, typ in specs:
        if nm in have:
            continue
        y = round(y + 5.08, 2)
        p = copy.deepcopy(right[-1])
        p[1], p[2] = Q(nm), typ
        at = find(p, "at")[0]; at[2] = y
        find(p, "uuid")[0][1] = Q(U())
        b.append(p)
        rs.wire(bx + bw, y, bx + bw + 5.08, y)
        rs.label(nm, bx + bw + 5.08, y, 0)
        changed = True
    if y + 5.08 > by + bh:                                    # блок мал - растягиваем, имя файла - под блок
        nh = round(y + 5.08 - by, 2)
        find(b, "size")[0][2] = nh
        for pr in find(b, "property"):
            if pr[1] == "Sheetfile":
                find(pr, "at")[0][2] = round(by + nh + 0.6, 2)

def remove_pins(fname, names):
    global changed
    b = blk(fname)
    for p in [p for p in find(b, "pin") if str(p[1]) in names]:
        px, py = (float(v) for v in find(p, "at")[0][1:3])
        b.remove(p)
        def touches(e):
            if not (isinstance(e, list) and e and e[0] in ("wire", "label")):
                return False
            pts = ([tuple(float(v) for v in xy[1:3]) for xy in find(find(e, "pts")[0], "xy")] if e[0] == "wire"
                   else [tuple(float(v) for v in find(e, "at")[0][1:3])])
            return all(abs(q[0] - px) < 5.2 and abs(q[1] - py) < 0.01 for q in pts)
        root[:] = [e for e in root if not touches(e)]
        changed = True

add_right_pins("batt_link.kicad_sch", [("IBAT_OUT", "output"), ("+3V3A", "passive"), ("VBIAS_1V5", "passive")])
remove_pins("meas_48v.kicad_sch", ("IBAT_OUT", "VBIAS_1V5"))
if changed:
    i = next(k for k, e in enumerate(root) if isinstance(e, list) and e and e[0] == "sheet")
    root[i:i] = rs.items
    guard(ROOTF)
    open(ROOTF, "w", encoding="utf-8").write(dump(root))
    remember(ROOTF)
print("ok")
