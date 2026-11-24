# Генератор структурных схем K57 для KiCad 10: две платы, два проекта.
# Верхний лист - блоки (иерархические листы), связанные метками цепей.
# Каждый лист - иерархические метки + текст "что здесь будет".
import os, sys, uuid, json, shutil

OUT = sys.argv[1]
TEMPLATE_PRO = sys.argv[2]
DATE = "2026-11-24"
REV = "0.1 структура"
VER = "20250610"
G = 2.54

def U():
    return str(uuid.uuid4())

def q(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'

def font(size=1.27, bold=False, thick=None):
    t = f"(thickness {thick})" if thick else ""
    b = "(bold yes)" if bold else ""
    return f"(font (size {size} {size}) {t} {b})"

def title_block(title, comment):
    return (f'(title_block (title {q(title)}) (date {q(DATE)}) (rev {q(REV)})'
            f' (comment 1 {q(comment)}) (comment 2 "Техзадание: docs/k57-power-stage-spec-RU.md"))')

def text(s, x, y, size=1.27):
    return (f'(text {q(s)} (exclude_from_sim no) (at {x:.2f} {y:.2f} 0)'
            f' (effects {font(size)} (justify left top)) (uuid {q(U())}))')

def hlabel(name, shape, x, y):
    return (f'(hierarchical_label {q(name)} (shape {shape}) (at {x:.2f} {y:.2f} 180)'
            f' (effects {font()} (justify right)) (uuid {q(U())}))')

def wire(x1, y1, x2, y2):
    return (f'(wire (pts (xy {x1:.2f} {y1:.2f}) (xy {x2:.2f} {y2:.2f}))'
            f' (stroke (width 0) (type default)) (uuid {q(U())}))')

def label(name, x, y, side):
    ang, just = (0, "left bottom") if side == "R" else (180, "right bottom")
    return (f'(label {q(name)} (at {x:.2f} {y:.2f} {ang}) (fields_autoplaced yes)'
            f' (effects {font()} (justify {just})) (uuid {q(U())}))')

def subsheet_file(path, title, comment, pins, body, root_uuid):
    items = []
    y = 30.48
    for name, shape, side in pins:
        items.append(hlabel(name, shape, 25.40, y))
        y += 5.08
    items.append(text("Выводы листа (иерархические метки) — слева.\n"
                      "Сейчас лист пустой: это структура. Схема — следующим шагом.",
                      25.40, 20.32))
    items.append(text(body, 106.68, 30.48))
    s = (f'(kicad_sch (version {VER}) (generator "eeschema") (generator_version "10.0")'
         f' (uuid {q(U())}) (paper "A4") {title_block(title, comment)} (lib_symbols) '
         + " ".join(items) + ' (embedded_fonts no))')
    with open(path, "w", encoding="utf-8") as f:
        f.write(s)

def sheet_block(name, fname, x, y, pins, root_uuid, project, page):
    left = [p for p in pins if p[2] == "L"]
    right = [p for p in pins if p[2] == "R"]
    w = 25 * G
    h = (max(len(left), len(right)) + 1) * G * 2
    parts = []
    lbls = []
    for i, (pn, shape, _) in enumerate(left):
        py = y + (i + 1) * G * 2
        parts.append(f'(pin {q(pn)} {shape} (at {x:.2f} {py:.2f} 180) (uuid {q(U())})'
                     f' (effects {font()} (justify left)))')
        lbls.append(wire(x - 2 * G, py, x, py))
        lbls.append(label(pn, x - 2 * G, py, "L"))
    for i, (pn, shape, _) in enumerate(right):
        py = y + (i + 1) * G * 2
        px = x + w
        parts.append(f'(pin {q(pn)} {shape} (at {px:.2f} {py:.2f} 0) (uuid {q(U())})'
                     f' (effects {font()} (justify right)))')
        lbls.append(wire(px, py, px + 2 * G, py))
        lbls.append(label(pn, px + 2 * G, py, "R"))
    s = (f'(sheet (at {x:.2f} {y:.2f}) (size {w:.2f} {h:.2f}) (exclude_from_sim no)'
         f' (in_bom yes) (on_board yes) (dnp no) (fields_autoplaced yes)'
         f' (stroke (width 0.1524) (type solid)) (fill (color 0 0 0 0.0000)) (uuid {q(U())})'
         f' (property "Sheetname" {q(name)} (at {x:.2f} {y - 0.71:.2f} 0)'
         f' (effects {font(1.27, True, 0.254)} (justify left bottom)))'
         f' (property "Sheetfile" {q(fname)} (at {x:.2f} {y + h + 0.6:.2f} 0) (hide yes)'
         f' (effects {font()} (justify left top))) '
         + " ".join(parts) +
         f' (instances (project {q(project)} (path {q("/" + root_uuid)} (page {q(str(page))})))))')
    return s, lbls, h

def project(proj, title, comment, note, blocks):
    d = os.path.join(OUT, proj)
    os.makedirs(d, exist_ok=True)
    root_uuid = U()
    sheets, labels = [], []
    cols = [50.8, 190.5, 330.2]
    rows_y = [45.72, 45.72, 45.72]
    for i, (name, fname, pins, body) in enumerate(blocks):
        c = i % 3
        x, y = cols[c], rows_y[c]
        s, l, h = sheet_block(name, fname, x, y, pins, root_uuid, proj, i + 2)
        rows_y[c] = y + h + 15.24
        sheets.append(s); labels += l
        subsheet_file(os.path.join(d, fname), f"{title}: {name}", comment, pins, body, root_uuid)
    root = (f'(kicad_sch (version {VER}) (generator "eeschema") (generator_version "10.0")'
            f' (uuid {q(root_uuid)}) (paper "A3") {title_block(title, comment)} (lib_symbols) '
            + text(note, 25.4, 15.24) + " " + " ".join(labels) + " " + " ".join(sheets) +
            ' (sheet_instances (path "/" (page "1"))) (embedded_fonts no))')
    with open(os.path.join(d, proj + ".kicad_sch"), "w", encoding="utf-8") as f:
        f.write(root)
    with open(TEMPLATE_PRO, encoding="utf-8") as f:
        pro = json.load(f)
    pro["meta"]["filename"] = proj + ".kicad_pro"
    pro["sheets"] = [[root_uuid, "Root"]]
    with open(os.path.join(d, proj + ".kicad_pro"), "w", encoding="utf-8") as f:
        json.dump(pro, f, indent=2, ensure_ascii=False)

I, O, B, P = "input", "output", "bidirectional", "passive"

# ------------------------------------------------------------------ силовая плата
power_blocks = [
 ("Подключение АКБ", "batt_link.kicad_sch",
  [("PRECHARGE_EN", I, "L"), ("CONTACTOR_EN", I, "L"), ("+12V", P, "L"), ("GND", P, "L"),
   ("DC_POS", P, "R"), ("BAT_POS", P, "R"), ("VBAT_RAW", O, "R")],
  "ПОДКЛЮЧЕНИЕ АКБ (техзадание 6.4)\n\n"
  "- Клемма BAT+: плюс АКБ после предохранителя класса T 150 А (предохранитель вне платы).\n"
  "- Клемма DC+: плюс звена. Между BAT+ и DC+ снаружи - главный контактор 200 А / 60 В.\n"
  "  Контактор и предзаряд - только в ПЛЮСЕ: минус - земля управления.\n"
  "- Предзаряд параллельно контактору: P-MOSFET 100 В + разъём к резистору\n"
  "  22-33 Ом / 50 Вт (резистор на радиаторе, вне платы). Управление от PRECHARGE_EN.\n"
  "- Катушка контактора 12 В: N-MOSFET от CONTACTOR_EN, диод или TVS на катушке.\n"
  "- VBAT_RAW: от BAT+ через свой предохранитель - питание платы управления.\n"
  "  Берётся ДО контактора, иначе МК не сможет управлять предзарядом.\n"
  "- TVS на звене - [?] см. 6.4.\n\n"
  "Последовательность (прошивка): предзаряд -> DC+ >= ~90 % BAT+ -> контактор.\n"
  "Не дождались - авария, а не переход в работу."),
 ("Звено", "dc_link.kicad_sch",
  [("DC_POS", P, "L"), ("GND", P, "L")],
  "КОНДЕНСАТОРЫ ЗВЕНА (техзадание 6.3)\n\n"
  "- 8-10 x 4700 мкФ / 100 В snap-in (пример: Nichicon LGU2A472MELC, 35x50 мм),\n"
  "  38-47 мФ. Пульсации 60 А действующих на частоте ШИМ (моделирование).\n"
  "  [?] Серия с большим ресурсом и расчёт срока службы.\n"
  "- Плёночные 100 В - на листе \"Мост\", вплотную к ключам каждого плеча.\n"
  "- Разрядный резистор звена: после отключения АКБ звено не должно\n"
  "  держать 60 В долго.\n"
  "- Расставлять симметрично относительно плеч: ток делится по импедансу."),
 ("Мост", "bridge.kicad_sch",
  [("GATE_HA", I, "L"), ("GATE_LA", I, "L"), ("GATE_HB", I, "L"), ("GATE_LB", I, "L"),
   ("DC_POS", P, "L"), ("GND", P, "L"),
   ("PHASE_A", P, "R"), ("PHASE_B", P, "R")],
  "H-МОСТ (техзадание 6.1.1)\n\n"
  "- 8 x Infineon IRF100P218 (TO-247): по два параллельно на каждое место.\n"
  "  Плечо A: верхние (DC+ -> PHASE_A), нижние (PHASE_A -> GND). Плечо B - так же.\n"
  "- Все ключи на общем радиаторе через изолирующие прокладки.\n"
  "- У КАЖДОГО ключа свой затворный резистор 2,2-3,3 Ом у вывода затвора.\n"
  "- Плёночные конденсаторы 100 В, 2-4 x 10 мкФ у каждого плеча.\n"
  "- Клемма трансформатора XFMR_A = PHASE_A (болтовая). XFMR_B - через шунт Ia,\n"
  "  лист \"Измерения 48 В\".\n"
  "- Медь 2-3 oz, ток до 195 А пик. Петля \"звено - ключи\" минимальная."),
 ("Драйверы", "drivers.kicad_sch",
  [("PWM1A", I, "L"), ("PWM1B", I, "L"), ("PWM2A", I, "L"), ("PWM2B", I, "L"),
   ("DRV_EN", I, "L"), ("+12V", P, "L"), ("GND", P, "L"),
   ("GATE_HA", O, "R"), ("GATE_LA", O, "R"), ("GATE_HB", O, "R"), ("GATE_LB", O, "R"),
   ("PHASE_A", P, "R"), ("PHASE_B", P, "R")],
  "ДРАЙВЕРЫ ЗАТВОРОВ (техзадание 6.2)\n\n"
  "- 2 x TI UCC27282DRC (корпус с выводом EN), по одному на плечо.\n"
  "  Плечо A = EPWM1: PWM1A -> верхний (GATE_HA), PWM1B -> нижний (GATE_LA).\n"
  "  Плечо B = EPWM2: PWM2A -> GATE_HB, PWM2B -> GATE_LB.\n"
  "  Верхний - выход A модуля ШИМ, нижний - B (myEPWM.h).\n"
  "- Уровень 1 = ключ открыт (IGBT_ACTIVE_HIGH = 1).\n"
  "- Подтяжка всех входов к земле: пока МК в сбросе, ключи закрыты.\n"
  "- DRV_EN - аппаратное гашение моста.\n"
  "- Бутстреп 2,2-4,7 мкФ X7R 25 В; развязка 12 В 22-47 мкФ у каждого драйвера.\n"
  "- Драйвер - вплотную к ключам своего плеча; HS - к PHASE своего плеча.\n"
  "- Мёртвое время задаёт МК: начальное 500 нс, первое включение - 2 мкс."),
 ("Измерения 48 В", "meas_48v.kicad_sch",
  [("PHASE_B", P, "L"), ("DC_POS", P, "L"), ("BAT_POS", P, "L"), ("GND", P, "L"),
   ("+3V3A", P, "L"), ("VBIAS_1V65", P, "L"), ("VREF_3V3", P, "L"),
   ("IA_OUT", O, "R"), ("IBAT_OUT", O, "R"), ("UBAT_OUT", O, "R"),
   ("UBATT_OUT", O, "R"), ("NTC_OUT", O, "R")],
  "ИЗМЕРЕНИЯ СТОРОНЫ 48 В (техзадание 6.5)\n\n"
  "- Ia: шунт 0,2 мОм 5930 (Bourns CSS2H-5930R-L200F) между PHASE_B и клеммой\n"
  "  трансформатора XFMR_B + INA240A1 (x20). REF1/REF2 = VBIAS_1V65.\n"
  "  Шкала +-250 А -> 1,65 +- 1,0 В. Четырёхпроводное подключение разводкой.\n"
  "- iBat: такой же шунт между клеммой BAT- и GND + INA240A1.\n"
  "  GND платы - минус звена (сторона шунта у моста).\n"
  "- uBat: делитель с DC+ (звено, после контактора), 0-75 В -> 0-3,0 В + RC.\n"
  "- uBatt: такой же делитель с BAT+ (до контактора) - ход предзаряда.\n"
  "- NTC радиатора: делитель от VREF_3V3.\n"
  "- INA240 - вплотную к своему шунту. Нуль и шкала - от одной опоры REF2033\n"
  "  (на плате управления): иначе нуль Ia уплывёт на амперы.\n"
  "- Ia идёт и на компаратор защиты CMPSS2: RC перед АЦП - без лишней задержки."),
 ("Разъём к плате управления", "link_ctrl.kicad_sch",
  [("IA_OUT", I, "L"), ("IBAT_OUT", I, "L"), ("UBAT_OUT", I, "L"), ("UBATT_OUT", I, "L"),
   ("NTC_OUT", I, "L"), ("VBAT_RAW", I, "L"), ("GND", P, "L"),
   ("PWM1A", O, "R"), ("PWM1B", O, "R"), ("PWM2A", O, "R"), ("PWM2B", O, "R"),
   ("DRV_EN", O, "R"), ("PRECHARGE_EN", O, "R"), ("CONTACTOR_EN", O, "R"),
   ("+12V", P, "R"), ("+3V3A", P, "R"), ("VBIAS_1V65", P, "R"), ("VREF_3V3", P, "R")],
  "РАЗЪЁМ К ПЛАТЕ УПРАВЛЕНИЯ (k57-control, лист \"Разъём к силовой плате\")\n\n"
  "Список цепей обязан совпадать с ответным листом на плате управления.\n\n"
  "- Каждый аналоговый сигнал - рядом с выводом GND (пара сигнал / земля).\n"
  "- Возврат 12 В драйверов - свои выводы GND, не те, что у аналоговых.\n"
  "- VBAT_RAW (до 60 В) - отдельным разъёмом: не рядом с 3,3 В.\n"
  "- [?] Тип разъёма и длина кабеля - при компоновке."),
]

# ------------------------------------------------------------------ плата управления
ctrl_blocks = [
 ("Питание", "power.kicad_sch",
  [("VBAT_RAW", I, "L"), ("GND", P, "L"),
   ("+12V", P, "R"), ("+5V", P, "R"), ("+3V3", P, "R"), ("+3V3A", P, "R"),
   ("VREF_3V3", P, "R"), ("VBIAS_1V65", P, "R")],
  "ПИТАНИЕ (техзадание 6.5, 6.7)\n\n"
  "- VBAT_RAW 40-60 В -> понижающий DC/DC 12 В: драйверы, катушки двух\n"
  "  контакторов. [?] Мощность: у открытых контакторов катушка до 15-20 Вт.\n"
  "- 12 В -> 5 В: ESP32, изолированный DC/DC для CAN.\n"
  "- -> 3,3 В цифровое (+3V3: МК) и отдельное 3,3 В аналоговое\n"
  "  (+3V3A: INA240, AMC3330, ОУ).\n"
  "- TI REF2033: VREF_3V3 -> VREFHI АЦП (внешний опорный режим, правка SysConfig,\n"
  "  раздел 7 п. 5а); VBIAS_1V65 -> нуль всех двуполярных каналов на обеих платах.\n"
  "- Защита входа: предохранитель уже на силовой плате; здесь - TVS, обратная\n"
  "  полярность [?]."),
 ("МК", "mcu.kicad_sch",
  [("IA_OUT", I, "L"), ("IB_OUT", I, "L"), ("IBAT_OUT", I, "L"), ("UBAT_OUT", I, "L"),
   ("UBATT_OUT", I, "L"), ("U220_OUT", I, "L"), ("USEC_OUT", I, "L"), ("NTC_OUT", I, "L"),
   ("+3V3", P, "L"), ("+3V3A", P, "L"), ("VREF_3V3", P, "L"), ("GND", P, "L"),
   ("PWM1A", O, "R"), ("PWM1B", O, "R"), ("PWM2A", O, "R"), ("PWM2B", O, "R"),
   ("DRV_EN", O, "R"), ("PRECHARGE_EN", O, "R"), ("CONTACTOR_EN", O, "R"),
   ("GRID_CONT_EN", O, "R"), ("CAN_TX", O, "R"), ("CAN_RX", I, "R"),
   ("SPI_CLK", O, "R"), ("SPI_PICO", O, "R"), ("SPI_POCI", I, "R"), ("SPI_CS", O, "R"),
   ("ESP_RDY", I, "R"),
   ("FRAM_CLK", O, "R"), ("FRAM_PICO", O, "R"), ("FRAM_POCI", I, "R"), ("FRAM_CS", O, "R"),
   ("I2C_SDA", B, "R"), ("I2C_SCL", O, "R"),
   ("SCOPE", O, "R"), ("STROB", O, "R"), ("DAC_DBG", O, "R")],
  "МИКРОКОНТРОЛЛЕР TMS320F28P550SJ9, LQFP-100 (PZ) (техзадание 6.7, 6.7.1)\n\n"
  "Распределение выводов - docs/k57-mcu-pinout-RU.md (24.11.2026). Ножки PZ:\n"
  "  PWM1A/1B  GPIO0/1   (79/78)  EPWM1    CAN_TX/RX  GPIO4/5  (75/89) MCANA\n"
  "  PWM2A/2B  GPIO2/3   (77/76)  EPWM2    SPIA->ESP32-S3: PICO GPIO8 (74),\n"
  "  DRV_EN    GPIO40    (80)                CLK GPIO9 (90), POCI GPIO10 (93),\n"
  "  PRECHARGE_EN GPIO41 (82)                CS GPIO11 (52), RDY GPIO55 (43)\n"
  "  CONTACTOR_EN GPIO44 (85)              SPIB->FRAM: PICO GPIO50 (9),\n"
  "  GRID_CONT_EN GPIO59 (92)                POCI 51 (10), CLK 52 (11), CS 53 (12)\n"
  "  I2C SDA/SCL GPIO28/29 (1/100) DNP     SCOPE GPIO57 (66), STROB GPIO58 (67)\n"
  "  Кварц 20 МГц GPIO18/19 (68/69)        XRSn (2), TMS (62), TCK (60)\n\n"
  "  IA_OUT    B1  (40)  CMPSS2 верх+низ    USEC_OUT  A1  (22)\n"
  "  IB_OUT    B4  (39)  CMPSS4 верх+низ    UBATT_OUT B8  (36)\n"
  "  U220_OUT  A14 (19)  CMPSS3 верх -> eCAP1 (детектор нуля)\n"
  "  UBAT_OUT  A6  (14)  CMPSS1 верх - перенапряжение звена\n"
  "  IBAT_OUT  A2  (17)  CMPSS1 низ  - перегрузка АКБ\n"
  "  NTC_OUT   C5  (28)                     DAC_DBG = DACA_OUT, A0 (23)\n"
  "  VREFHI (24, 25) - REF2033 3,3 В, 2,2 мкФ к VREFLO (26, 27)\n\n"
  "- Загрузка: GPIO24 (56) и GPIO32 (64) - подтяжки 10 кОм к 3,3 В (флеш);\n"
  "  перемычка GPIO24 на землю - режим ожидания SCI (восстановление).\n"
  "- GPIO35/TDI, GPIO37/TDO (63, 61) в cJTAG свободны: кандидат на SCIA\n"
  "  (консоль через кабель J102) [?].\n"
  "- Свободен нижний CMPSS3 [?].\n"
  "- Отладка cJTAG: разъём 14 конт. 1,27 мм, разводка как J102 LaunchPad;\n"
  "  PD -> 3,3 В, TMS - подтяжка 2,2 кОм к 3,3 В (TRSTn нет).\n"
  "  Отладчик - XDS110 LaunchPad через J102 (с J101 снять перемычки).\n"
  "- Развязка питания у каждого вывода, VREFHI - конденсатор по документации."),
 ("Приёмники сигналов 230 В", "ac_receivers.kicad_sch",
  [("IB_P", I, "L"), ("IB_N", I, "L"), ("U220_P", I, "L"), ("U220_N", I, "L"),
   ("USEC_P", I, "L"), ("USEC_N", I, "L"), ("VBIAS_1V65", P, "L"), ("+3V3A", P, "L"),
   ("GND", P, "L"),
   ("IB_OUT", O, "R"), ("U220_OUT", O, "R"), ("USEC_OUT", O, "R")],
  "ПРИЁМНИКИ СИГНАЛОВ С ПЛАТЫ 230 В (техзадание 6.5)\n\n"
  "С k57-ac сигналы идут ДИФФЕРЕНЦИАЛЬНЫМИ парами: помеха на кабеле\n"
  "одинакова в обоих проводах и вычитается.\n\n"
  "- U220_P/N, USEC_P/N: выходы AMC3330 (дифференциальные). Здесь ОУ:\n"
  "  дифференциальный -> однополярный с центром VBIAS_1V65, шкала +-400 В.\n"
  "- IB_P/N: напряжение на нагрузочном резисторе трансформатора тока.\n"
  "  Здесь ОУ со смещением VBIAS_1V65. Шкала [?] +-40 А.\n"
  "- Выходы -> АЦП. IB_OUT идёт и на компаратор CMPSS4: без лишнего RC.\n"
  "- Канал U220 - синхронизация с сетью: фазовая задержка малая и известная."),
 ("ESP32-S3", "esp32.kicad_sch",
  [("SPI_CLK", I, "L"), ("SPI_PICO", I, "L"), ("SPI_POCI", O, "L"), ("SPI_CS", I, "L"),
   ("ESP_RDY", O, "L"), ("+5V", P, "L"), ("GND", P, "L")],
  "ESP32-S3 - МОСТ SPI <-> WiFi (техзадание 6.7.2)\n"
  "Второй процессор, как в проекте ESP57 (ESP32_BRIDGE): ведомый SPI у F28P55x.\n\n"
  "- Модуль ESP32-S3-WROOM-1 (антенна на модуле) или -1U (разъём под внешнюю\n"
  "  антенну, если корпус металлический) [?]. Флеш 8 МБ (N8) - запас под OTA.\n"
  "- SPI2 (FSPI) на родных выводах IOMUX: CS0 GPIO10, MOSI GPIO11,\n"
  "  SCLK GPIO12, MISO GPIO13 (документация ESP-IDF, SPI Slave Driver).\n"
  "  Ведомый SPI у S3 - до 60 МГц; обмен сейчас 4 МГц, заложено до 10 МГц.\n"
  "- ESP_RDY (\"есть данные\", активный низкий) - любой свободный GPIO.\n"
  "- Свой стабилизатор 3,3 В от +5V, не меньше 500 мА: всплески тока радио.\n"
  "- USB-C на родном USB (GPIO19/GPIO20): прошивка и консоль без\n"
  "  отдельного преобразователя USB-UART. Земля USB = земля управления =\n"
  "  минус АКБ: только для сервиса, или через USB-изолятор [?].\n"
  "- Кнопки BOOT (GPIO0) и EN (сброс).\n"
  "- Антенна - на краю платы, под ней и рядом НЕТ меди на всех слоях\n"
  "  (зона по документации модуля), вдали от силовых проводов.\n"
  "- [?] Незапаянные: F28 -> EN моста (сброс моста), мост -> XRSn F28\n"
  "  (открытый сток) - на случай зависания. Прошивка F28 через мост идёт\n"
  "  протоколом SPI (fwup.c) и отдельных линий не требует.\n"
  "- Прошивка моста: сменить цель на esp32s3 и выводы в board.h."),
 ("Интерфейсы", "interfaces.kicad_sch",
  [("CAN_TX", I, "L"), ("CAN_RX", O, "L"),
   ("FRAM_CLK", I, "L"), ("FRAM_PICO", I, "L"), ("FRAM_POCI", O, "L"), ("FRAM_CS", I, "L"),
   ("I2C_SDA", B, "L"), ("I2C_SCL", I, "L"), ("SCOPE", I, "L"), ("STROB", I, "L"),
   ("DAC_DBG", I, "L"), ("+3V3", P, "L"), ("+5V", P, "L"), ("GND", P, "L")],
  "ИНТЕРФЕЙСЫ (техзадание 6.7)\n\n"
  "- CAN FD изолированный: ISO1044 (класс) + изолированный DC/DC 5 В на стороне\n"
  "  шины, разъём под PCAN. Изоляция - потому что земля ПК и минус АКБ на токах\n"
  "  150 А будут расходиться.\n"
  "- SPI FRAM FM25V02A на SPIB: журнал аварий, учёт энергии\n"
  "  (решение 02.11.2026, docs/eeprom-fram-comparison-RU.md).\n"
  "- I2C: НЕЗАПАЯННОЕ место под часы RV-3028 или MCP7940N и батарейку.\n"
  "- Контрольные точки под осциллограф: SCOPE, STROB, DAC_DBG (= DACA_OUT, A0).\n"
  "- ESP32-S3 - отдельный лист. JTAG - на листе \"МК\"."),
 ("Разъём к силовой плате", "link_power.kicad_sch",
  [("PWM1A", I, "L"), ("PWM1B", I, "L"), ("PWM2A", I, "L"), ("PWM2B", I, "L"),
   ("DRV_EN", I, "L"), ("PRECHARGE_EN", I, "L"), ("CONTACTOR_EN", I, "L"),
   ("+12V", P, "L"), ("+3V3A", P, "L"), ("VBIAS_1V65", P, "L"), ("VREF_3V3", P, "L"),
   ("GND", P, "L"),
   ("IA_OUT", O, "R"), ("IBAT_OUT", O, "R"), ("UBAT_OUT", O, "R"), ("UBATT_OUT", O, "R"),
   ("NTC_OUT", O, "R"), ("VBAT_RAW", O, "R")],
  "РАЗЪЁМ К СИЛОВОЙ ПЛАТЕ (k57-power, лист \"Разъём к плате управления\")\n\n"
  "Список цепей обязан совпадать с ответным листом на силовой плате.\n\n"
  "- Аналоговые сигналы - в парах с GND.\n"
  "- VBAT_RAW (до 60 В) - отдельным разъёмом.\n"
  "- [?] Тип разъёма - при компоновке."),
 ("Разъём к плате 230 В", "link_ac.kicad_sch",
  [("GRID_CONT_EN", I, "L"), ("+12V", P, "L"), ("+3V3A", P, "L"), ("GND", P, "L"),
   ("IB_P", O, "R"), ("IB_N", O, "R"), ("U220_P", O, "R"), ("U220_N", O, "R"),
   ("USEC_P", O, "R"), ("USEC_N", O, "R")],
  "РАЗЪЁМ К ПЛАТЕ 230 В (k57-ac, лист \"Разъём к плате управления\")\n\n"
  "Список цепей обязан совпадать с ответным листом на k57-ac.\n"
  "Все цепи этого разъёма - низковольтные: 230 В на k57-ac остаётся за\n"
  "изоляционным барьером (AMC3330, трансформатор тока).\n\n"
  "- Дифференциальные пары - соседними выводами, лучше витыми парами.\n"
  "- [?] Тип разъёма - при компоновке."),
]

# ------------------------------------------------------------------ плата 230 В
ac_blocks = [
 ("Синус-фильтр", "sine_filter.kicad_sch",
  [("OUT_L", P, "R"), ("OUT_N", P, "R")],
  "СИНУС-ФИЛЬТР LCL (техзадание 6.8)\n"
  "Нужен в ОБОИХ режимах: автономном (АИН) - нагрузка получает синусоиду,\n"
  "- и сетевом - фильтр не пускает пульсации ШИМ в сеть.\n\n"
  "- Клеммы SEC_L / SEC_N: вторичка силового трансформатора.\n"
  "- L1 = индуктивность рассеяния трансформатора (приведённая к вторичке).\n"
  "  Трансформатора ещё нет: требование к рассеянию - в 6.8. [?]\n"
  "- C: плёночный конденсатор класса X2 / для переменного тока, 1-2 мкФ,\n"
  "  >= 275-310 В перем., последовательно - демпфирующий резистор.\n"
  "  Узел конденсатора наружу не выводится: USEC меряется ПОСЛЕ L2.\n"
  "- L2: сетевой дроссель 1-2 мГн на 13-15 А действующих (вне платы,\n"
  "  на шасси; на плате - клеммы). Без L2 жёсткая сеть закорачивает C.\n"
  "- OUT_L / OUT_N - после L2, к сетевому контактору. Здесь же датчик USEC\n"
  "  (лист \"Измерения 230 В\"): регулирование напряжения в режиме АИН.\n"
  "- Резонанс 1-2 кГц: прошивке нужны модель с C и L2 и демпфирование\n"
  "  (раздел 7)."),
 ("Сетевой контактор и защита", "grid_switch.kicad_sch",
  [("OUT_L", P, "L"), ("OUT_N", P, "L"), ("GRID_CONT_EN", I, "L"), ("+12V", P, "L"),
   ("GND", P, "L"),
   ("GRID_L", P, "R"), ("GRID_N", P, "R"), ("IB_CT_P", P, "R"), ("IB_CT_N", P, "R")],
  "СЕТЕВОЙ КОНТАКТОР, ЗАЩИТА, ТОК В СЕТЬ (техзадание 6.6)\n\n"
  "- Трансформатор тока Ib на проводе OUT_L (ток в сеть); вывод вторички\n"
  "  IB_CT_P / IB_CT_N - на лист измерений. [?] Сторона установки\n"
  "  (до или после C) - вместе с настройкой регулятора тока.\n"
  "- Сетевой контактор (двухполюсный) между OUT и GRID. Катушка 12 В,\n"
  "  N-MOSFET от GRID_CONT_EN, диод на катушке. Катушка и её цепь - на\n"
  "  стороне SELV, контакты - на стороне 230 В: зазор между ними.\n"
  "- Клеммы GRID_L / GRID_N, предохранитель, варистор.\n"
  "- ЭМС-фильтр (синфазный дроссель, X/Y-конденсаторы) - против помех\n"
  "  150 кГц-30 МГц; это отдельная задача от синус-фильтра. [?]"),
 ("Измерения 230 В", "meas_230v.kicad_sch",
  [("OUT_L", P, "L"), ("OUT_N", P, "L"), ("GRID_L", P, "L"), ("GRID_N", P, "L"),
   ("IB_CT_P", P, "L"), ("IB_CT_N", P, "L"), ("+3V3A", P, "L"), ("GND", P, "L"),
   ("IB_P", O, "R"), ("IB_N", O, "R"), ("U220_P", O, "R"), ("U220_N", O, "R"),
   ("USEC_P", O, "R"), ("USEC_N", O, "R")],
  "ИЗМЕРЕНИЯ 230 В - ИЗОЛЯЦИОННЫЙ БАРЬЕР (техзадание 6.5, 6.6)\n\n"
  "Изоляция между 230 В и выходами листа - УСИЛЕННАЯ: земля управления\n"
  "соединена с минусом АКБ, а его можно коснуться. Пути утечки ~5-6,5 мм\n"
  "[?] по IEC 61800-5-1 / IEC 62109-1. Под AMC3330 - прорезь в плате.\n\n"
  "- U220 = GRID_L - GRID_N (сеть, после контактора): делитель + AMC3330.\n"
  "- USEC = OUT_L - OUT_N: ПОСЛЕ синус-фильтра (после L2), до контактора.\n"
  "  По нему - регулятор напряжения в режиме АИН и сравнение с U220 перед\n"
  "  замыканием контактора. Делитель + AMC3330.\n"
  "  AMC3330 питается от +3V3A низкой стороны, высокую сторону питает сам\n"
  "  (встроенный изолированный DC/DC). Выход - дифференциальная пара.\n"
  "- IB: нагрузочный резистор трансформатора тока, напряжение на нём -\n"
  "  дифференциальной парой. Смещение 1,65 В - на плате управления."),
 ("Разъём к плате управления", "link_ctrl.kicad_sch",
  [("IB_P", I, "L"), ("IB_N", I, "L"), ("U220_P", I, "L"), ("U220_N", I, "L"),
   ("USEC_P", I, "L"), ("USEC_N", I, "L"),
   ("GRID_CONT_EN", O, "R"), ("+12V", P, "R"), ("+3V3A", P, "R"), ("GND", P, "R")],
  "РАЗЪЁМ К ПЛАТЕ УПРАВЛЕНИЯ (k57-control, лист \"Разъём к плате 230 В\")\n\n"
  "Список цепей обязан совпадать с ответным листом.\n"
  "Только низковольтные цепи: всё 230 В остаётся на этой плате."),
]

project("k57-power", "K57 силовая плата", "Мост, драйверы, звено, шунты, предзаряд",
        "K57 - СИЛОВАЯ ПЛАТА (k57-power). Структура, ревизия 0.1.\n"
        "Блоки связаны метками цепей: одинаковое имя - одна цепь. Двойной щелчок по блоку - открыть лист.\n"
        "Ответная плата - k57-control. Решения и расчёты - docs/k57-power-stage-spec-RU.md. [?] - не решено.",
        power_blocks)
project("k57-control", "K57 плата управления", "МК, питание, приёмники 230 В, интерфейсы",
        "K57 - ПЛАТА УПРАВЛЕНИЯ (k57-control). Структура, ревизия 0.1. Целиком низковольтная.\n"
        "Блоки связаны метками цепей: одинаковое имя - одна цепь. Двойной щелчок по блоку - открыть лист.\n"
        "Ответные платы - k57-power и k57-ac. Решения и расчёты - docs/k57-power-stage-spec-RU.md. [?] - не решено.",
        ctrl_blocks)
project("k57-ac", "K57 плата 230 В", "Синус-фильтр, сетевой контактор, измерения 230 В",
        "K57 - ПЛАТА 230 В (k57-ac). Структура, ревизия 0.1.\n"
        "ОПАСНОЕ НАПРЯЖЕНИЕ. Блоки связаны метками цепей: одинаковое имя - одна цепь.\n"
        "Ответная плата - k57-control. Решения и расчёты - docs/k57-power-stage-spec-RU.md. [?] - не решено.",
        ac_blocks)

# проверка: списки цепей ответных разъёмов совпадают
def pins_of(blocks, fname):
    return {p[0] for b in blocks if b[1] == fname for p in b[2]}
for nm, a, b in [("power<->control", pins_of(power_blocks, "link_ctrl.kicad_sch"),
                  pins_of(ctrl_blocks, "link_power.kicad_sch")),
                 ("ac<->control", pins_of(ac_blocks, "link_ctrl.kicad_sch"),
                  pins_of(ctrl_blocks, "link_ac.kicad_sch"))]:
    print(nm, "совпадают" if a == b else f"РАСХОЖДЕНИЕ: {sorted(a ^ b)}")
# проверка: каждая цепь верхнего листа встречается хотя бы у двух блоков
for nm, blocks in [("power", power_blocks), ("control", ctrl_blocks), ("ac", ac_blocks)]:
    cnt = {}
    for b in blocks:
        for p in b[2]:
            cnt[p[0]] = cnt.get(p[0], 0) + 1
    lone = sorted(k for k, v in cnt.items() if v < 2)
    print(nm, "одиночные цепи:", lone if lone else "нет")
