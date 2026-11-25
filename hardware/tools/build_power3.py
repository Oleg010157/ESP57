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

y = 30.48
for net, shape in [("IA_OUT", "input"), ("IBAT_OUT", "input"), ("UBAT_OUT", "input"), ("UBATT_OUT", "input"),
                   ("NTC_OUT", "input"), ("VBAT_RAW", "input"), ("GND", "passive"),
                   ("PWM1A", "output"), ("PWM1B", "output"), ("PWM2A", "output"), ("PWM2B", "output"),
                   ("DRV_EN", "output"), ("PRECHARGE_EN", "output"), ("CONTACTOR_EN", "output"),
                   ("+12V", "passive"), ("+3V3A", "passive"), ("VBIAS_1V5", "passive"), ("VREF_3V0", "passive")]:
    sh.hlabel(net, shape, 20.32, y)
    sh.wire(20.32, y, 27.94, y)
    if net in ("GND", "+12V"):
        sh.power(net, 27.94, y, 270 if net == "GND" else 90)
    else:
        sh.label(net, 27.94, y, 0)
    y += 7.62

# Распиновка 2x20: нечётные - сигналы, чётные - земля. На плоском шлейфе провода идут
# 1, 2, 3, 4...: каждый сигнал оказывается между двумя проводами земли.
PINS = {9: "PWM1A", 11: "PWM1B", 13: "PWM2A", 15: "PWM2B",
        17: "DRV_EN", 19: "PRECHARGE_EN", 21: "CONTACTOR_EN",
        23: "IA_OUT", 25: "IBAT_OUT", 27: "UBAT_OUT", 29: "UBATT_OUT", 31: "NTC_OUT",
        33: "+3V3A", 35: "VBIAS_1V5", 37: "VREF_3V0", 39: "!NC", 40: "!NC"}
CONN = "Connector_Generic:Conn_02x20_Odd_Even"
CX, CY = 177.8, 101.6
sh.place(CONN, "J601", "К плате управления (IDC 2x20)",
         "Connector_IDC:IDC-Header_2x20_P2.54mm_Vertical", CX, CY,
         nets={str(k): v for k, v in PINS.items()},
         fields={"ref": (CX, 45.72), "val": (CX, 48.26), "just": "center"})
P = lambda n: sh.pin_xy(CONN, CX, CY, 0, n)
# питание и земля разъёма - короткие отводы в общую шину, по одному символу на шину
sh.bus_power("+12V", [P(1), P(3)], -1)
sh.bus_power("+12V", [P(2), P(4)], +1)
sh.bus_power("GND", [P(5), P(7)], -1, tail=27.94)
sh.bus_power("GND", [P(n) for n in range(6, 39, 2)], +1)
# питание платы управления от АКБ - отдельный разъём, подальше от сигналов 3,3 В
sh.place("Connector:Screw_Terminal_01x02", "J602", "VBAT_RAW к плате управления",
         "TerminalBlock_Phoenix:TerminalBlock_Phoenix_MKDS-1,5-2-5.08_1x02_P5.08mm_Horizontal", 297.18, 76.2,
         nets={"1": "VBAT_RAW", "2": "@GND"})
# Питание приходит с другой платы: флажки говорят проверке схемы, что цепи запитаны
for i, net in enumerate(["@+12V", "@GND", "+3V3A", "VBIAS_1V5", "VREF_3V0"]):
    sh.place("power:PWR_FLAG", f"#FLG{601 + i}", "PWR_FLAG", "", 284.48 + i * 17.78, 127.0, nets={"1": net},
             fields={"ref": (284.48 + i * 17.78, 120.65), "val": (284.48 + i * 17.78, 123.19), "just": "center"})
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
