# Лист «ESP32-S3» платы k57-control: модуль, свой стабилизатор 3,3 В, USB-C, кнопки.
# Запуск: python -I hardware/tools/build_ctrl_esp.py hardware "C:/Program Files/KiCad/10.0/share/kicad/symbols"
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import *

HW, KL = sys.argv[1], sys.argv[2]
PRJ = "k57-control"
PDIR = os.path.join(HW, PRJ)
F = "esp32.kicad_sch"

root = parse(open(os.path.join(PDIR, PRJ + ".kicad_sch"), encoding="utf-8").read())[0]
ROOT = str(find(root, "uuid")[0][1])
SHEET = {str([p[2] for p in find(sh, "property") if p[1] == "Sheetfile"][0]): str(find(sh, "uuid")[0][1])
         for sh in find(root, "sheet")}
old = parse(open(os.path.join(PDIR, F), encoding="utf-8").read())[0]
HIER = [(str(h[1]), str(find(h, "shape")[0][1])) for h in find(old, "hierarchical_label")]
FILE_UUID = str(find(old, "uuid")[0][1])
for other in ["mcu.kicad_sch", "power.kicad_sch"]:
    t = parse(open(os.path.join(PDIR, other), encoding="utf-8").read())[0]
    for s in find(t, "symbol"):
        for p in find(s, "property"):
            if p[1] == "Reference":
                ALL_REFS.add(str(p[2]))

def lib(libname, name):
    return resolved_symbol(os.path.join(KL, libname + ".kicad_sym"), name)

sh = Sheet(PRJ, "/" + ROOT + "/" + SHEET[F], paper="A3")
for lid, sym in {"Device:R": lib("Device", "R"), "Device:C": lib("Device", "C"),
                 "RF_Module:ESP32-S3-WROOM-1": lib("RF_Module", "ESP32-S3-WROOM-1"),
                 "Regulator_Linear:TLV75733PDRV": lib("Regulator_Linear", "TLV75733PDRV"),
                 "Connector:USB_C_Receptacle_USB2.0_16P": lib("Connector", "USB_C_Receptacle_USB2.0_16P"),
                 "Power_Protection:USBLC6-2SC6": lib("Power_Protection", "USBLC6-2SC6"),
                 "Switch:SW_Push": lib("Switch", "SW_Push"),
                 "power:GND": lib("power", "GND"), "power:+5V": lib("power", "+5V")}.items():
    sh.add_lib(lid, sym)

POWER = {"GND", "+5V"}
hier_block(sh, HIER, POWER)

R0603, C0603, C0805, C1206 = ("Resistor_SMD:R_0603_1608Metric", "Capacitor_SMD:C_0603_1608Metric",
                              "Capacitor_SMD:C_0805_2012Metric", "Capacitor_SMD:C_1206_3216Metric")
def fc(x, y, dy_ref=-13.97, dy_val=15.24):
    return {"ref": (x, y + dy_ref), "val": (x, y + dy_val), "just": "center"}

# ------------------------------------------------ свой стабилизатор 3,3 В для ESP32-S3
XR, YR = 81.28, 109.22
sh.place("Regulator_Linear:TLV75733PDRV", "U301", "TLV75733PDRV", "Package_SON:WSON-6-1EP_2x2mm_P0.65mm_EP1x1.6mm",
         XR, YR, nets={"6": None, "4": None, "1": "+3V3_ESP", "3": "@GND", "7": "@GND", "2": "!NC", "5": "!NC"},
         fields=fc(XR, YR))
P = lambda n: sh.pin_xy("Regulator_Linear:TLV75733PDRV", XR, YR, 0, n)
sh.bus_power("+5V", [P(6), P(4)], -1)
sh.place("Device:C", "C301", "1uF 16V", C0603, 63.5, 132.08, nets={"1": "@+5V", "2": "@GND"})
sh.place("Device:C", "C302", "22uF 10V X7R", C1206, 104.14, 132.08, nets={"1": "+3V3_ESP", "2": "@GND"})
sh.place("Device:C", "C303", "1uF 16V", C0603, 119.38, 132.08, nets={"1": "+3V3_ESP", "2": "@GND"})

# ------------------------------------------------ модуль
XM, YM = 228.6, 127.0
ESP = {"2": "+3V3_ESP", "1": "@GND", "40": "@GND", "41": "@GND", "3": "ESP_EN", "27": "ESP_BOOT",
       "18": "SPI_CS", "19": "SPI_PICO", "20": "SPI_CLK", "21": "ESP_MISO", "22": "ESP_RDY",
       "13": "USB_DN", "14": "USB_DP"}
for n in pins_of(sh.libs["RF_Module:ESP32-S3-WROOM-1"]):
    ESP.setdefault(n, "!NC")
sh.place("RF_Module:ESP32-S3-WROOM-1", "U302", "ESP32-S3-WROOM-1-N8", "RF_Module:ESP32-S3-WROOM-1", XM, YM,
         nets=ESP, fields={"ref": (XM - 12.7, YM - 33.02), "val": (XM + 7.62, YM - 33.02), "just": "left"})
sh.place("Device:C", "C304", "10uF 10V X7R", C0805, 251.46, 76.2, nets={"1": "+3V3_ESP", "2": "@GND"})
sh.place("Device:C", "C305", "100nF", C0603, 266.7, 76.2, nets={"1": "+3V3_ESP", "2": "@GND"})
# EN: 10 кОм + 1 мкФ (задержка запуска по рекомендации Espressif) и кнопка
sh.place("Device:R", "R301", "10k", R0603, 162.56, 88.9, nets={"1": "+3V3_ESP", "2": "ESP_EN"})
sh.place("Device:C", "C306", "1uF 16V", C0603, 147.32, 116.84, nets={"1": "ESP_EN", "2": "@GND"})
sh.place("Switch:SW_Push", "SW301", "EN (сброс ESP32)", "Button_Switch_SMD:SW_SPST_TL3342", 180.34, 116.84,
         nets={"1": "ESP_EN", "2": "@GND"}, fields=fc(180.34, 116.84, -10.16, -7.62))
# BOOT: GPIO0 на землю при включении - режим прошивки
sh.place("Device:R", "R302", "10k", R0603, 147.32, 88.9, nets={"1": "+3V3_ESP", "2": "ESP_BOOT"})
sh.place("Switch:SW_Push", "SW302", "BOOT", "Button_Switch_SMD:SW_SPST_TL3342", 180.34, 147.32,
         nets={"1": "ESP_BOOT", "2": "@GND"}, fields=fc(180.34, 147.32, -10.16, -7.62))
# SPI: подтяжка CS (ведомый не выбран, пока F28 стартует), 33 Ом на выходе MISO
sh.place("Device:R", "R303", "10k", R0603, 147.32, 165.1, nets={"1": "+3V3_ESP", "2": "SPI_CS"})
sh.place("Device:R", "R304", "33", R0603, 162.56, 190.5, nets={"1": "ESP_MISO", "2": "SPI_POCI"})

# ------------------------------------------------ USB-C: прошивка и консоль (USB Serial/JTAG ESP32-S3)
XU, YU = 337.82, 132.08
USB = {"A6": "USB_C_DP", "B6": "USB_C_DP", "A7": "USB_C_DN", "B7": "USB_C_DN",
       "A5": "CC1", "B5": "CC2", "A8": "!NC", "B8": "!NC",
       "A4": "!NC", "A9": "!NC", "B4": "!NC", "B9": "!NC",
       "A1": "@GND", "B1": "@GND", "A12": "@GND", "B12": "@GND", "SH": "@GND"}
sh.place("Connector:USB_C_Receptacle_USB2.0_16P", "J301", "USB-C (сервис)",
         "Connector_USB:USB_C_Receptacle_GCT_USB4105-xx-A_16P_TopMnt_Horizontal", XU, YU, ang=0,
         nets=USB, fields={"ref": (XU, YU - 30.48), "val": (XU, YU - 27.94), "just": "center"})
sh.place("Device:R", "R305", "5.1k", R0603, 373.38, 160.02, nets={"1": "CC1", "2": "@GND"})
sh.place("Device:R", "R306", "5.1k", R0603, 386.08, 160.02, nets={"1": "CC2", "2": "@GND"})
sh.place("Power_Protection:USBLC6-2SC6", "U303", "USBLC6-2SC6", "Package_TO_SOT_SMD:SOT-23-6", 299.72, 172.72,
         nets={"1": "USB_C_DP", "6": "USB_DP", "3": "USB_C_DN", "4": "USB_DN", "2": "@GND", "5": "@+5V"},
         fields={"ref": (303.53, 162.56), "val": (303.53, 165.1), "just": "left"})   # сбоку от +5V, не над ним

sh.text("ESP32-S3 - МОСТ SPI <-> WiFi (техзадание 6.7.2)\n"
        "U302 ESP32-S3-WROOM-1-N8 (антенна на модуле). [?] -1U с разъёмом, если корпус металлический.\n"
        "Ведомый SPI2 на выводах IOMUX: CS GPIO10, MOSI GPIO11, CLK GPIO12, MISO GPIO13 (через R304 33 Ом).\n"
        "  \"Есть данные\" (ESP_RDY, активный низкий) - GPIO14. R303 держит CS в 1, пока F28 стартует.\n"
        "U301 TLV75733P - свой 3,3 В от +5V: всплески тока радио не идут в питание МК. C302 22 мкФ + C304/C305 у модуля.\n"
        "EN: R301 10 кОм + C306 1 мкФ (задержка запуска), кнопка SW301. BOOT: GPIO0, кнопка SW302 (держать при сбросе -\n"
        "  режим прошивки). Выводы выбора режима GPIO3, GPIO45, GPIO46 не подключены: остаются по умолчанию.\n"
        "J301 USB-C: родной USB ESP32-S3 (GPIO19/GPIO20) - прошивка и консоль без преобразователя USB-UART.\n"
        "  CC1/CC2 - 5,1 кОм на землю (устройство). U303 - защита от статики. VBUS не подключён: плату питать\n"
        "  от своего источника. Земля USB = земля управления = минус АКБ: только для сервиса [?] USB-изолятор.\n"
        "Антенна - на краю платы, под ней и рядом без меди на всех слоях (зона по документации модуля).\n"
        "Прошивка моста: цель esp32s3, SPI2_HOST, выводы - в ESP32_BRIDGE/main/board.h (техзадание, раздел 7).",
        63.5, 218.44)
sh.write(os.path.join(PDIR, F), "K57 плата управления: ESP32-S3", "МК, питание, приёмники 230 В, интерфейсы", FILE_UUID)
print("ok", len(HIER))
