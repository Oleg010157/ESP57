# Лист «Разъём к плате управления» платы k57-power.
# Запуск: python -I hardware/tools/build_power3.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-power"
PDIR = os.path.join(HW, PRJ)
F = "link_ctrl.kicad_sch"

root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
for f in ["drivers.kicad_sch", "bridge.kicad_sch", "batt_link.kicad_sch", "dc_link.kicad_sch", "meas_48v.kicad_sch"]:
    t = parse(open(os.path.join(PDIR, f), encoding="utf-8").read())[0]
    for s in find(t, "symbol"):
        for p in find(s, "property"):
            if p[1] == "Reference":
                ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[F])
for lid, (lb, nm) in {"power:GND": ("power", "GND"), "power:+12V": ("power", "+12V"),
                      "power:PWR_FLAG": ("power", "PWR_FLAG"),
                      "Connector_Generic:Conn_02x20_Odd_Even": ("Connector_Generic", "Conn_02x20_Odd_Even"),
                      "Connector:Screw_Terminal_01x02": ("Connector", "Screw_Terminal_01x02")}.items():
    sh.add_lib(lid, lib(lb, nm))

hier_block(sh, [("IA_OUT", "input"), ("IBAT_OUT", "input"), ("UBAT_OUT", "input"), ("UBATT_OUT", "input"),
                   ("NTC_OUT", "input"), ("VBAT_RAW", "input"), ("GND", "passive"),
                   ("PWM1A", "output"), ("PWM1B", "output"), ("PWM2A", "output"), ("PWM2B", "output"),
                   ("DRV_EN", "output"), ("PRECHARGE_EN", "output"), ("CONTACTOR_EN", "output"),
                   ("+12V", "passive"), ("+3V3A", "passive"), ("VBIAS_1V5", "passive"), ("VREF_3V0", "passive")], ("GND", "+12V"))

# Распиновка - общая для обеих плат: k57_links.POWER_LINK
from k57_links import POWER_LINK, place_idc
CONN = "Connector_Generic:Conn_02x20_Odd_Even"
CX, CY = 177.8, 101.6
place_idc(sh, CONN, "J601", "К плате управления (IDC 2x20)", "Connector_IDC:IDC-Header_2x20_P2.54mm_Vertical",
          CX, CY, POWER_LINK, fields={"ref": (CX, 45.72), "val": (CX, 48.26), "just": "center"})
# питание платы управления от АКБ - отдельный разъём, подальше от сигналов 3,3 В
sh.place("Connector:Screw_Terminal_01x02", "J602", "VBAT_RAW к плате управления",
         "TerminalBlock_Phoenix:TerminalBlock_Phoenix_MKDS-1,5-2-5.08_1x02_P5.08mm_Horizontal", 297.18, 76.2,
         nets={"1": "VBAT_RAW", "2": "@GND"})
# Питание приходит с другой платы: флажки говорят проверке схемы, что цепи запитаны
for i, net in enumerate(["@+12V", "@GND", "+3V3A", "VBIAS_1V5", "VREF_3V0"]):
    flag(sh, net, 284.48 + i * 17.78, 124.46, f"#FLG{601 + i}")
sh.text("РАЗЪЁМ К ПЛАТЕ УПРАВЛЕНИЯ (k57-control, лист \"Разъём к силовой плате\")\n"
        "J601 - IDC 2x20, шаг 2,54 мм, плоский шлейф. Нечётные контакты - сигналы, чётные - земля:\n"
        "  каждый сигнал на шлейфе идёт между двумя проводами земли.\n"
        "  1-4 +12V (драйверы и катушка контактора, до ~1 А на контакт), 5-8 GND - возврат питания.\n"
        "  9-21 управление (PWM, DRV_EN, PRECHARGE_EN, CONTACTOR_EN), 23-31 аналоговые выходы,\n"
        "  33 +3V3A, 35 VBIAS_1V5, 37 VREF_3V0 - питание и опора измерений с платы управления.\n"
        "  Ответная часть и список цепей - на плате управления; распиновка обязана совпадать.\n"
        "J602 - VBAT_RAW (до 60 В, после предохранителя F301) - отдельным разъёмом.\n"
        "PWR_FLAG - пометка для проверки схемы: эти цепи питаются с другой платы.\n"
        "[?] Длина шлейфа - как можно короче; при длине больше ~20 см - пересмотреть.", 81.28, 190.5)
fu = str(find(parse(open(os.path.join(PDIR, F), encoding="utf-8").read())[0], "uuid")[0][1])
sh.write(os.path.join(PDIR, F), "K57 силовая плата: Разъём к плате управления", "Мост, драйверы, звено, шунты, предзаряд", fu)
print("ok")
