# Распиновка разъёмов между платами K57 - одна на обе стороны каждого кабеля.
# Скрипты обеих плат берут её отсюда; check_links.py сверяет итоговые схемы.

# Шлейф «силовая плата J601 <-> плата управления J501», IDC 2x20, прямой кабель (контакт 1 - в контакт 1).
# Нечётные - сигналы, чётные - земля: на шлейфе каждый сигнал идёт между двумя проводами земли.
POWER_LINK = {1: "+12V", 2: "+12V", 3: "+12V", 4: "+12V",
              5: "GND", 6: "GND", 7: "GND", 8: "GND",
              9: "PWM1A", 11: "PWM1B", 13: "PWM2A", 15: "PWM2B",
              17: "DRV_EN", 19: "PRECHARGE_EN", 21: "CONTACTOR_EN",
              23: "IA_OUT", 25: "IBAT_OUT", 27: "UBAT_OUT", 29: "UBATT_OUT", 31: "NTC_OUT",
              33: "+3V3A", 35: "VBIAS_1V5", 37: "VREF_3V0", 39: None, 40: None}
for _p in range(10, 39, 2):
    POWER_LINK.setdefault(_p, "GND")
assert sorted(POWER_LINK) == list(range(1, 41))

# Питание платы управления от АКБ: силовая J602 <-> управления J502, 2 контакта
VBAT_LINK = {1: "VBAT_RAW", 2: "GND"}

POWER_NETS = ("GND", "+12V", "+5V", "+3V3")

def place_idc(sh, conn_lib, ref, value, fp, cx, cy, pinmap, fields=None):
    """Поставить разъём 2xN по карте контактов. Цепи питания и земли одной стороны
    (нечётные слева, чётные справа) собираются в шину с одним символом."""
    nets, groups = {}, {}
    for p, net in pinmap.items():
        if net is None:
            nets[str(p)] = "!NC"
        elif net in POWER_NETS:
            nets[str(p)] = None
            groups.setdefault((net, p % 2), []).append(p)
        else:
            nets[str(p)] = net
    sh.place(conn_lib, ref, value, fp, cx, cy, nets=nets, fields=fields)
    for (net, odd), pins in sorted(groups.items()):
        pts = [sh.pin_xy(conn_lib, cx, cy, 0, p) for p in pins]
        sh.bus_power(net, pts, -1 if odd else +1, tail=27.94 if (odd and net == "GND") else 0.0)
