/*****************************************************************************
 * can_boot_protocol.h
 *
 * Протокол CAN-загрузчика F28P55x. Общие определения для загрузчика (MCU) и
 * приложения. Вторая сторона протокола - Bootloader/Bootloader.cs в проекте
 * Motor_Control; правки здесь без правок там не имеют смысла.
 *
 * ШИНА. CAN-FD, 1 Мбит/с арбитраж и 2.5 Мбит/с данные. Загрузчик перенастраивает
 * MCAN под себя и ставит свой тайминг (boot_can_init). Он согласован с настройкой
 * PCAN на стороне ПК (Bootloader.cs: BootFdBitrate), и разойтись им нельзя. С
 * приложением совпадать не обязан: оно может идти на 5 Мбит/с.
 *
 * CAN ID:  ПК -> MCU = 0x33,  MCU -> ПК = 0x32
 *
 * КАДРЫ. Два формата, и только два:
 *   - служебный: 8 байт = 4 слова. Слово 0 - команда или квитанция, слова 1..3 -
 *     параметры. Классический кадр, без FDF и BRS. Все кадры MCU -> ПК такие.
 *   - кадр данных: 64 байта = 32 слова, FDF=1, BRS=1, DLC=15. Только ПК -> MCU,
 *     только в фазе загрузки.
 *
 * Почему тело прошивки идёт кусками ровно по 64 байта: это 32 слова, то есть
 * ровно один вызов Fapi_issueAutoEcc512ProgrammingCommand(). Кадр ложится в
 * запись флеша один в один, без накопления и без остатка.
 *
 * ПОСЛЕДОВАТЕЛЬНОСТЬ.
 *   ПК: 0xCC01                       -> MCU: 0x02CC    (связь)
 *   ПК: 0xCC02, затем кадр диапазона -> MCU: 0x0DCC    (диапазон принят)
 *                                    -> MCU: 0xEBnn    (nn = проценты стирания)
 *                                    -> MCU: 0x05CC    (стёрто)
 *   ПК: 0xCC07                       -> MCU: 0x0ACC    (готов принимать)
 *   ПК: кадр заголовка [адрес, размер]
 *   ПК: кадры данных по 64 байта     -> MCU: 0x06CC    (на каждый кадр)
 *                                    -> MCU: 0x07CC    (сегмент записан)
 *       ... повторяется по сегментам ...
 *   ПК: 0xCC08                       -> MCU: 0x09CC    (готов сверять)
 *   ПК: chkHi, chkLo (два кадра)     -> MCU: 0x08CC    (сумма сошлась)
 *                                       или 0x0ECC     (не сошлась)
 *   ПК: 0xFE17                       -> MCU: 0x17FE    (готов к пуску)
 *   ПК: кадр с адресом входа         -> MCU: 0x18FE    (переход)
 *
 *****************************************************************************/
#ifndef CAN_BOOT_PROTOCOL_H_
#define CAN_BOOT_PROTOCOL_H_

#include <stdint.h>

/* CAN message IDs: выбраны так, чтобы не пересекаться с телеметрией.
 * Приложение слушает ID 2 и отвечает на 1/103/131/132/140 - см. myMCAN.c. */
/* ПК -> MCU */
#define BOOT_CAN_RX_ID          0x33U
/* MCU -> ПК */
#define BOOT_CAN_TX_ID          0x32U

/* Служебный кадр: 8 байт = 4 слова. */
#define BOOT_CMD_WORDS          4U

/* Кадр данных: 64 байта = 32 слова = одна запись флеша на 512 бит. */
#define BOOT_DATA_BYTES         64U
#define BOOT_DATA_WORDS         32U
#define BOOT_DATA_DLC           15U

/* Command words (первое слово служебного кадра) */
#define CMD_HANDSHAKE           0xCC01U
#define ACK_HANDSHAKE           0x02CCU

#define CMD_ERASE               0xCC02U
#define ACK_ERASE               0x05CCU
#define ACK_ERASE_RANGE         0x0DCCU

#define CMD_CONTINUE            0xCC03U /* Отладочное: продолжить посекторное стирание */

#define CMD_LOAD                0xCC07U
#define ACK_LOAD_READY          0x0ACCU
#define ACK_BLOCK               0x06CCU
#define ACK_LOAD                0x07CCU

/* Шлётся при несовпадении суммы, чтобы ПК успел вмешаться до перезапуска. */
#define ACK_NOVERIFY            0x0ECCU

#define CMD_VERIFY              0xCC08U
#define ACK_VERIFY_READY        0x09CCU
#define ACK_VERIFY              0x08CCU

#define CMD_REBOOT              0xFE17U
#define ACK_REBOOT_READY        0x17FEU
#define ACK_REBOOT              0x18FEU

/*---------------------------------------------------------------------------
 * Маркеры хода работы (MCU -> ПК). UI показывает их строкой состояния,
 * а Bootloader_trace.log - как есть.
 *---------------------------------------------------------------------------*/
/* Ход стирания: младший байт - проценты 0..100.
 *
 * На F280039C здесь стоял 0xEB{банк}{сектор} - по четыре бита на поле. У
 * F28P55x секторов 128 на банк, в четыре бита они не лезут, поэтому шлём
 * готовый процент: номер сектора UI всё равно не показывал. */
#define MARK_ERASE_PROGRESS     0xEB00U

#define MARK_ERASE_ENTER        0xEE00U /* вошли в процедуру стирания */
#define MARK_ERASE_BEGIN        0xEE01U /* диапазон принят, начинаем */
#define MARK_FAPI_OK            0xEE02U /* Flash API поднялся */
#define MARK_BOOT_ENTER         0xEF01U /* вошли в bootloader_main() */
#define MARK_CAN_READY          0xEF02U /* MCAN перенастроен под загрузчик */

#define MARK_FAPI_ERROR         0xDE00U /* | код Fapi_StatusType - отказ инициализации */
#define MARK_ERASE_TIMEOUT      0xD000U /* | младшие биты номера сектора */
#define MARK_PROG_ERROR         0xDF00U /* | код Fapi_StatusType - отказ записи */
#define MARK_FAIL               0xDEADU /* общий отказ, ждём CMD_ERASE от ПК */

/*---------------------------------------------------------------------------
 * Карта флеша F28P55x
 *
 * Пять банков по 256 КБ (0x20000 слов), в каждом 128 секторов по 0x400 слов.
 * BANK0..BANK3 - код приложения, их загрузчик стирает и пишет целиком, включая
 * нулевой сектор с codestart: только так прибор поднимется по питанию, а не
 * одним лишь переходом по адресу от ПК.
 *
 * BANK4 (0x100000) НЕ ТРОГАЕТСЯ НИКОГДА - там калибровки и настройка прибора,
 * см. FLASH_CALIB в 28p55x_generic_flash_lnk.cmd и my_Files/calib_store.c.
 * Защита тут не программная, а по построению: диапазон стирания зажимается
 * APP_END_ADDR, и ни один адрес BANK4 в команды Fapi не попадает.
 *---------------------------------------------------------------------------*/
#define APP_START_ADDR          0x080000UL  /* BANK0, сектор 0 (codestart) */
#define APP_END_ADDR            0x0FFFFFUL  /* BANK3, сектор 127 */

#define FLASH_SECTOR_WORDS      0x400UL     /* 2 КБ = 0x400 слов */

/* Запись на 512 бит требует выравнивания адреса на 32 слова. */
#define FLASH_PROG_WORDS        BOOT_DATA_WORDS

void     boot_can_send_word(uint16_t word);
uint16_t boot_can_recv_word(void);

#endif /* CAN_BOOT_PROTOCOL_H_ */
