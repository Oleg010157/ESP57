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
sh.place("k57:CT_1000", "T201", "ТТ 1:1000 [?]", "Transformer_THT:Transformer_Zeming_ZMCT103C", 129.54, 101.6,
         nets={"1": "IB_CT_P", "2": "IB_CT_N"})
for i, (ref, a, b) in enumerate((("K201", "OUT_L", "GRID_L"), ("K202", "OUT_N", "GRID_N"))):
    x = 190.5 + i * 50.8
    sh.place("k57:ALFG2PF12", ref, "ALFG2PF12", "Relay_THT:Relay_SPST_Panasonic_ALFG_FormA", x, 101.6,
             nets={"11": a, "14": b, "A1": "@+12V", "A2": "COIL_N"},
             fields={"ref": (x + 12.7, 99.06), "val": (x + 12.7, 101.6), "just": "left"})
    sh.place("Diode:1N4148W", f"D20{i + 1}", "1N4148W", "Diode_SMD:D_SOD-123", x - 20.32, 76.2, ang=270,
             nets={"1": "@+12V", "2": "COIL_N"}, fields={"ref": (x - 17.78, 74.93), "val": (x - 17.78, 77.47), "just": "left"})
sh.place("Transistor_FET:AO3400A", "Q201", "AO3400A", "Package_TO_SOT_SMD:SOT-23", 218.44, 152.4,
         nets={"1": "GATE_K", "3": "COIL_N", "2": "@GND"}, fields={"ref": (223.52, 151.13), "val": (223.52, 153.67), "just": "left"})
sh.place("Device:R", "R201", "100", R0603, 180.34, 160.02, ang=90, nets={"1": "GRID_CONT_EN", "2": "GATE_K"},
         fields={"ref": (180.34, 156.21), "val": (180.34, 163.83), "just": "center"})
sh.place("Device:R", "R202", "100k", R0603, 205.74, 172.72, nets={"1": "GATE_K", "2": "@GND"})
sh.place("Device:C", "C201", "10uF 25V X7R", C1206, 147.32, 152.4, nets={"1": "@+12V", "2": "@GND"})
sh.place("Device:Varistor", "RV201", "S20K275", "Varistor:RV_Disc_D21.5mm_W5.1mm_P10mm", 309.88, 101.6,
         nets={"1": "GRID_L", "2": "GRID_N"})
sh.place(TERM, "J201", "Сеть / нагрузка (через внешний автомат)", TB2, 350.52, 101.6,
         nets={"1": "GRID_L", "2": "GRID_N"}, fields={"ref": (350.52, 91.44), "val": (350.52, 93.98), "just": "center"})
sh.text("СЕТЕВОЙ КОНТАКТОР И ЗАЩИТА (техзадание 6.6)\n"
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

def amc_channel(n, y, hot, cold, outp, outn, t, flag_ref):
    """Канал напряжения на AMC3330: делитель 4 x 1 МОм + 10 кОм, развязка по SBASA34B (рис. 7-5)."""
    XA = 254.0
    u = f"U{n + 1}"
    sh.place(AMC, u, "AMC3330DWE", "Package_SO:SOIC-16W_7.5x10.3mm_P1.27mm", XA, y,
             nets={"1": f"{t}_HDC", "3": f"{t}_HDC", "2": cold, "7": cold, "8": cold, "4": "!NC",
                   "5": f"{t}_HLDO", "6": f"{t}_INP", "9": "@GND", "15": "@GND", "12": f"{t}_VDD",
                   "13": f"{t}_LDC", "16": f"{t}_LDC", "11": f"{t}_OP", "10": f"{t}_ON", "14": "!NC"},
             fields={"ref": (XA - 17.78, y - 22.86), "val": (XA - 17.78, y - 20.32), "just": "left"})
    # делитель: четыре резистора подряд, соединены проводниками; 400 В пик -> 1,0 В
    xs = [106.68 + i * 10.16 for i in range(4)]
    yd = y - 30.48
    for i, x in enumerate(xs):
        nets = {"1": hot if i == 0 else None, "2": f"{t}_INP" if i == 3 else None}
        sh.place("Device:R", f"R{n + 1 + i}", "1M 1%", R1206, x, yd, ang=90, nets=nets,
                 fields={"ref": (x, yd - 3.81), "val": (x, yd + 3.81), "just": "center"})
    for xa, xb in zip(xs, xs[1:]):
        sh.wire(xa + 3.81, yd, xb - 3.81, yd)
    sh.place("Device:R", f"R{n + 5}", "10k 0.1%", R0603, 160.02, y - 15.24, nets={"1": f"{t}_INP", "2": cold})
    sh.place("Device:C", f"C{n + 1}", "1nF C0G", C0603, 175.26, y - 15.24, nets={"1": f"{t}_INP", "2": cold})
    # высокая сторона: DC/DC 1 мкФ + 1 нФ, вход HLDO 100 нФ, выход HLDO 100 нФ NP0 + 1 нФ
    for i, (ref, val, fp, net) in enumerate([(f"C{n + 2}", "1uF 25V X7R", C0603, f"{t}_HDC"),
                                             (f"C{n + 3}", "1nF X7R", C0603, f"{t}_HDC"),
                                             (f"C{n + 4}", "100nF X7R", C0603, f"{t}_HDC"),
                                             (f"C{n + 5}", "100nF NP0", C1206, f"{t}_HLDO"),
                                             (f"C{n + 6}", "1nF X7R", C1206, f"{t}_HLDO")]):
        sh.place("Device:C", ref, val, fp, 106.68 + i * 20.32, y + 30.48, nets={"1": net, "2": cold})
    # низкая сторона: вход DC/DC 100 нФ, VDD 1 нФ + 1 мкФ, бусина от +3V3A
    for i, (ref, val, fp, net) in enumerate([(f"C{n + 7}", "100nF X7R", C0603, f"{t}_LDC"),
                                             (f"C{n + 8}", "1nF X7R", C1206, f"{t}_VDD"),
                                             (f"C{n + 9}", "1uF 25V X7R", C1206, f"{t}_VDD")]):
        sh.place("Device:C", ref, val, fp, 304.8 + i * 20.32, y + 30.48, nets={"1": net, "2": "@GND"})
    sh.place("Device:FerriteBead_Small", f"FB{n + 1}", "BLM15HD182SH1", "Inductor_SMD:L_0402_1005Metric",
             304.8, y - 25.4, nets={"1": "+3V3A", "2": f"{t}_VDD"})
    flag(sh, f"{t}_VDD", 289.56, y - 33.02, flag_ref)       # VDD за бусиной: питание приходит со шлейфа
    sh.place("Device:R", f"R{n + 6}", "49.9", R0603, 337.82, y - 7.62, nets={"1": f"{t}_OP", "2": outp})
    sh.place("Device:R", f"R{n + 7}", "49.9", R0603, 353.06, y - 7.62, nets={"1": f"{t}_ON", "2": outn})

amc_channel(310, 101.6, "GRID_L", "GRID_N", "U220_P", "U220_N", "U220", "#FLG301")
amc_channel(330, 223.52, "OUT_L", "OUT_N", "USEC_P", "USEC_N", "USEC", "#FLG302")
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
        "  Развязка - по рисунку 7-5 и табл. 7-3: VDD 1 нФ + 1 мкФ, DC/DC 100 нФ / 1 мкФ + 1 нФ, HLDO 100 нФ и 100 нФ NP0 + 1 нФ.\n"
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
