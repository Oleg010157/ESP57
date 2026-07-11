/*****************************************************************************
 * can_boot_protocol.h
 *
 * Протокол CAN-загрузчика F280039C
 * Общие определения для загрузчика (MCU) и приложения
 *
 * CAN ID:  PC→MCU = 0x33,  MCU→PC = 0x32
 *
 * Протокол (тот же что был на F28335):
 *   PC: 0xCC01       → MCU: 0x02CC       (handshake)
 *   PC: 0xCC02 + erase-range(8B) → MCU: 0x0DCC (range ack) → ... → 0x05CC (erase done)
 *   PC: 0xCC07 → MCU: 0x0ACC (load ready) → PC: addr+size → данные по 8 байт → MCU: 0x06CC (block ack) ... 0x07CC (load), повторяется по сегментам
 *   PC: 0xCC08+chkH+chkL → MCU: 0x08CC   (verify checksum)
 *   PC: 0xFE17       → MCU: 0x18FE       (reboot)
 *
 *****************************************************************************/
#ifndef CAN_BOOT_PROTOCOL_H_
#define CAN_BOOT_PROTOCOL_H_

#include <stdint.h>

/* CAN message IDs: changed to avoid collision with telemetry IDs */
/* Recommended: PC → MCU = 0x33, MCU → PC = 0x32 */
/* PC → MCU */
#define BOOT_CAN_RX_ID          0x33U
/* MCU → PC */
#define BOOT_CAN_TX_ID          0x32U

/* Command words (sent as 16-bit via CAN LEN=2) */
#define CMD_HANDSHAKE           0xCC01U
#define ACK_HANDSHAKE           0x02CCU

#define CMD_ERASE               0xCC02U
#define ACK_ERASE               0x05CCU
#define ACK_ERASE_RANGE         0x0DCCU

#define CMD_CONTINUE            0xCC03U /* Developer: resume sector erase */

#define CMD_LOAD                0xCC07U
#define ACK_LOAD_READY          0x0ACCU
#define ACK_BLOCK               0x06CCU
#define ACK_LOAD                0x07CCU

/* Sent by MCU when verify failed so PC can take action
 * (e.g. request resend) before MCU performs reset). */
#define ACK_NOVERIFY            0x0ECCU

#define CMD_VERIFY              0xCC08U
#define ACK_VERIFY_READY        0x09CCU
#define ACK_VERIFY              0x08CCU

#define CMD_REBOOT              0xFE17U
#define ACK_REBOOT_READY        0x17FEU
#define ACK_REBOOT              0x18FEU

/* Flash memory map F280039C — область приложения */
#define APP_START_ADDR          0x082000UL  /* Bank0 Sec2 */
#define APP_END_ADDR            0x0AFFFFUL  /* Bank2 Sec15 */

/* Загрузчик живёт здесь (не стирается!) */
#define BOOT_START_ADDR         0x080002UL  /* Bank0 Sec0 */
#define BOOT_END_ADDR           0x081FFFUL  /* Bank0 Sec1 */

/* Точка входа приложения */
#define APP_ENTRY_ADDR          0x082000UL

void boot_can_send_word(uint16_t word);
uint16_t boot_can_recv_word(void);


#endif /* CAN_BOOT_PROTOCOL_H_ */
