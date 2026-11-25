# Лист «Разъём к силовой плате» платы k57-control - ответная часть J601/J602 силовой платы.
# Запуск: python -I hardware/tools/build_ctrl_link_power.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *
from k57_links import POWER_LINK, VBAT_LINK, place_idc

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-control"
PDIR = os.path.join(HW, PRJ)
F = "link_power.kicad_sch"

root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
old = parse(open(os.path.join(PDIR, F), encoding="utf-8").read())[0]
HIER = [(str(h[1]), str(find(h, "shape")[0][1])) for h in find(old, "hierarchical_label")]
FILE_UUID = str(find(old, "uuid")[0][1])
for other in ["mcu.kicad_sch", "power.kicad_sch", "esp32.kicad_sch", "interfaces.kicad_sch"]:
    t = parse(open(os.path.join(PDIR, other), encoding="utf-8").read())[0]
    for s in find(t, "symbol"):
        for p in find(s, "property"):
            if p[1] == "Reference":
                ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[F])
CONN = "Connector_Generic:Conn_02x20_Odd_Even"
for lid, sym in {"power:GND": lib("power", "GND"), "power:+12V": lib("power", "+12V"),
                 CONN: lib("Connector_Generic", "Conn_02x20_Odd_Even"),
                 "Connector:Screw_Terminal_01x02": lib("Connector", "Screw_Terminal_01x02")}.items():
    sh.add_lib(lid, sym)

y = 30.48
for net, shape in HIER:
    sh.hlabel(net, shape, 20.32, y)
    sh.wire(20.32, y, 27.94, y)
    if net in ("GND", "+12V"):
        sh.power(net, 27.94, y, 270 if net == "GND" else 90)
    else:
        sh.label(net, 27.94, y, 0)
    y += 7.62

CX, CY = 177.8, 101.6
place_idc(sh, CONN, "J501", "К силовой плате (IDC 2x20)", "Connector_IDC:IDC-Header_2x20_P2.54mm_Vertical",
          CX, CY, POWER_LINK, fields={"ref": (CX, 45.72), "val": (CX, 48.26), "just": "center"})
sh.place("Connector:Screw_Terminal_01x02", "J502", "VBAT_RAW с силовой платы",
         "TerminalBlock_Phoenix:TerminalBlock_Phoenix_MKDS-1,5-2-5.08_1x02_P5.08mm_Horizontal", 297.18, 76.2,
         nets={str(k): ("@" + v if v == "GND" else v) for k, v in VBAT_LINK.items()})
sh.text("РАЗЪЁМ К СИЛОВОЙ ПЛАТЕ (k57-power, лист \"Разъём к плате управления\")\n"
        "J501 - ответная часть J601 силовой платы: IDC 2x20, прямой плоский шлейф (контакт 1 - в контакт 1).\n"
        "  Распиновка - общая для обеих плат (hardware/tools/k57_links.py), сверяется скриптом check_links.py.\n"
        "  1-4 +12V к силовой плате (драйверы, катушка главного контактора), 5-8 и все чётные - GND.\n"
        "  9-21 управление: PWM1A/1B/2A/2B, DRV_EN, PRECHARGE_EN, CONTACTOR_EN.\n"
        "  23-31 аналоговые сигналы с силовой платы: IA, IBAT, UBAT, UBATT, NTC (RC у входов АЦП - на листе МК).\n"
        "  33 +3V3A, 35 VBIAS_1V5, 37 VREF_3V0 - питание и опора измерений силовой платы.\n"
        "J502 - VBAT_RAW (до 60 В) от силовой платы: питание платы управления до контактора.\n"
        "  Отдельной клеммой - подальше от сигналов 3,3 В.", 81.28, 190.5)
sh.write(os.path.join(PDIR, F), "K57 плата управления: Разъём к силовой плате", "МК, питание, приёмники 230 В, интерфейсы", FILE_UUID)
print("ok", len(HIER))
