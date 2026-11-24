# Листы «Драйверы» и «Мост» платы k57-power + библиотека hardware/lib/k57.kicad_sym
import sys, os, re
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

def hier_block(sh, specs, x=20.32, y0=30.48, step=7.62):
    """Иерархические метки слева; каждая проводком к локальной метке или символу питания."""
    y = y0
    for net, shape in specs:
        sh.hlabel(net, shape, x, y)
        sh.wire(x, y, x + 7.62, y)
        if net in ("GND", "+12V"):
            sh.power(net, x + 7.62, y, 270 if net == "GND" else 90)
        else:
            sh.label(net, x + 7.62, y, 0)
        y += step

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

# ================================================================ МОСТ
sh = new_sheet("bridge.kicad_sch")
sh.add_lib("k57:IRF100P218", irf100p218())
sh.add_lib("Mechanical:MountingHole_Pad", std("Mechanical", "MountingHole_Pad"))
hier_block(sh, [("GATE_HA", "input"), ("GATE_LA", "input"), ("GATE_HB", "input"), ("GATE_LB", "input"),
                ("DC_POS", "passive"), ("GND", "passive"), ("PHASE_A", "passive"), ("PHASE_B", "passive")])
# место -> (затвор, сток, исток)
places = [("HA", "GATE_HA", "DC_POS", "PHASE_A"), ("LA", "GATE_LA", "PHASE_A", "@GND"),
          ("HB", "GATE_HB", "DC_POS", "PHASE_B"), ("LB", "GATE_LB", "PHASE_B", "@GND")]
q = 1
for col, (pos, gate, drain, source) in enumerate(places):
    for k in range(2):
        x = 91.44 + col * 76.2 + k * 33.02
        y = 63.5
        qn = f"Q{200 + q}"
        gnet = f"G_{qn}"
        sh.place("k57:IRF100P218", qn, "IRF100P218", "Package_TO_SOT_THT:TO-247-3_Vertical", x, y,
                 nets={"1": gnet, "2": drain, "3": source})
        sh.place("Device:R", f"R{200 + q}", "2.7", R1206, x - 12.7, y + 25.4, nets={"1": gate, "2": gnet})
        src_net = source if not source.startswith("@") else "@GND"
        sh.place("Device:R", f"R{210 + q}", "10k", R0603, x, y + 25.4, nets={"1": gnet, "2": src_net})
        q += 1
    # RC-снаббер на место ключа - не запаивать, место на плате
    xs = 91.44 + col * 76.2 + 16.51
    sh.place("Device:R", f"R{220 + col + 1}", "4.7", R1206, xs, 106.68, dnp=True, nets={"1": drain, "2": f"SN_{pos}"})
    sh.place("Device:C", f"C{220 + col + 1}", "2.2nF 100V C0G", C1206, xs, 134.62, dnp=True,
             nets={"1": f"SN_{pos}", "2": source})
# конденсаторы у каждого плеча: 2 x 10 мкФ плёнка + 2 x 100 нФ керамика
FILM = "Capacitor_THT:C_Rect_L26.5mm_W10.5mm_P22.50mm_MKS4"
c = 1
for leg in range(2):
    for k in range(2):
        x = 91.44 + leg * 152.4 + k * 55.88
        sh.place("Device:C", f"C{200 + c}", "10uF 100V MKP", FILM, x, 167.64, nets={"1": "DC_POS", "2": "@GND"})
        sh.place("Device:C", f"C{210 + c}", "100nF 100V X7R", C1206, x + 27.94, 167.64, nets={"1": "DC_POS", "2": "@GND"})
        c += 1
# клемма трансформатора XFMR_A = PHASE_A (болт M6 под наконечник)
sh.place("Mechanical:MountingHole_Pad", "J201", "XFMR_A", "MountingHole:MountingHole_6.4mm_M6_DIN965_Pad", 381.0, 63.5,
         nets={"1": "PHASE_A"})
sh.text("H-МОСТ (техзадание 6.1.1)\n"
        "Q201,Q202 - верхний плеча A; Q203,Q204 - нижний A; Q205,Q206 - верхний B; Q207,Q208 - нижний B.\n"
        "Infineon IRF100P218, TO-247, все на общем радиаторе через изолирующие прокладки.\n"
        "У каждого ключа свой затворный резистор 2,7 Ом (R201-R208) у вывода затвора\n"
        "и 10 кОм затвор-исток (R211-R218): ключ закрыт, пока драйвер без питания.\n"
        "R221-R224, C221-C224 - RC-снабберы, НЕ ЗАПАИВАТЬ: место на случай звона при испытаниях.\n"
        "C201-C204 - плёнка 10 мкФ 100 В у каждого плеча, C211-C214 - керамика 100 нФ: петля\n"
        "\"конденсатор - верхний ключ - нижний ключ\" минимальная. [?] Плёнку - по выбранной модели.\n"
        "J201 - клемма трансформатора XFMR_A (PHASE_A), болт M6. XFMR_B - через шунт Ia.\n"
        "Медь 2-3 oz, ток до 195 А пик.", 81.28, 203.2)
sh.write(os.path.join(PDIR, "bridge.kicad_sch"), "K57 силовая плата: Мост",
         "Мост, драйверы, звено, шунты, предзаряд", file_uuid("bridge.kicad_sch"))
print("ok")
