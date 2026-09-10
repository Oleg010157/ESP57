

//#############################################################################
//
// FILE:   EEPROM_Config.h
//
// The purpose of this file is to configure various aspects of EEPROM Emulation,
// declare functions/global variables, and define unchanging values.
// The following variables can be configured in this file:
// 1. The number of sectors to use for EEPROM Emulation
// 2. EEPROM Bank Size
// 3. EEPROM Page Size
// 4. Flash Sector Size
// 5. Page_Mode or _64_BIT_MODE
//
// NOTE: To select which Flash sectors to use in EEPROM Emulation, adjust SECTOR_NUMBERS
// in EEPROM_FLASH.c
//#############################################################################

// Include Flash API Header Files
#include "driverlib.h"
#include "device.h"

//
// Include Flash API include file
//

#include "FlashTech_F28P55x_C28x.h"

// Include Flash API example header file
//
#include "flash_programming_f28p55x.h"

// K57: определение CalibBlob - что именно мы храним. Отсюда берётся DATA_SIZE.
#include "calib_store.h"

// Un-comment appropriate definition if one of the following variants is being used

#define F28P55xSJx 1
//#define F28P55xSGx 1

// Project specific defines
//#define _64_BIT_MODE 1
#define PAGE_MODE 1

// Define which Flash Bank to use for EEPROM Emulation.
// Bank 0 reserved for loading EEPROM Emulation/Flash API by default, choose Banks 1-4
// Note: Check data-sheet to verify that device has indicated bank
//
// K57: BANK4 (0x100000, 0x8000 слов = 32 сектора по 0x400). Выбран потому, что
// код исполняется из BANK0/BANK1, а во время стирания банк недоступен на чтение -
// на другом банке стирание не останавливает программу. В линкерном файле этот
// банк объявлен как FLASH_CALIB и ни одна секция туда не размещается.
//
// ВНИМАНИЕ: EEPROM_Erase() зовёт Fapi_issueBankEraseCommand(), которая стирает
// БАНК ЦЕЛИКОМ, а не только секторы из FIRST_AND_LAST_SECTOR. Значит BANK4
// принадлежит хранилищу полностью; журнал ошибок, если до него дойдёт, селить
// в BANK2 или BANK3.
#define FLASH_BANK_SELECT FlashBank4StartAddress

// Define Flash Sector Size in 16-bit words
#define FLASH_SECTOR_SIZE F28P55x_FLASH_SECTOR_SIZE

// Define how many sectors are in a Flash Bank
#define NUM_FLASH_SECTORS F28P55x_NUM_FLASH_SECTORS

// Define how many EEPROM Banks to emulate
//
// K57: считаем под BANK4, в 16-битных словах.
//
//   CalibBlob                        15 слов (2 служебных + 6 offsets +
//                                             6 kadc + 1 сумма)
//   после округления вверх до ECC    16 слов  (EEPROM_PAGE_DATA_SIZE)
//   страница = 8 статуса + 16 данных 24 слова
//   банк     = 8 + 24 * 80           1928 слов
//   16 банков                        30848 слов
//   доступно в BANK4 (32 * 0x400)    32768 слов
//
// Итого 16*80 = 1280 сохранений до первого стирания. Калибровки пишутся
// единицы раз за жизнь прибора, так что стирание практически не наступит.
//
// ЭТИ ДВА ЧИСЛА И НАДО КРУТИТЬ, если данных станет больше и перестанет
// влезать. Проверка стоит в calib_store.c (CALIB_FITS_IN_BANK) и сработает
// при сборке.
#define NUM_EEPROM_BANKS 16

// Define how many EEPROM Pages for each EEPROM Bank
#define NUM_EEPROM_PAGES 80

// Define size of data to be written to EEPROM Pages (measured in 16-bit words)
// If size is not a multiple of 4, the size of the Page data will be rounded up to the closest
// multiple of 4 to be in accordance with ECC requirements
// Example: DATA_SIZE = 3
//          This will result in EEPROM Pages using 4 16-bit words
//          for data, but will only write 3 words of valid data per page
//
// K57: числом НЕ задаём. Размер берётся из структуры CalibBlob (calib_store.h),
// то есть из тех самых offsets и kadc, которые сохраняем. Добавили поле в
// ADCpar - размер страницы пересчитается сам, править здесь ничего не нужно.
//
// На C28x адресуемая единица - 16 бит, поэтому sizeof() сразу даёт число слов,
// без деления на sizeof(uint16_t).
//
// Если данные перестанут влезать в банк, сборка остановится на проверке
// CALIB_FITS_IN_BANK в calib_store.h и скажет об этом прямо - молча испортить
// флеш не получится.
#define DATA_SIZE (sizeof(CalibBlob))


// Calculate the appropriate size of data in EEPROM Pages.
// To comply with ECC, must be a multiple of 4 greater than or equal to the
// DATA_SIZE specified by user
#define EEPROM_PAGE_DATA_SIZE (DATA_SIZE + 3 - ((DATA_SIZE + 3) % 4))


// Pointer Initialization
#define RESET_BANK_POINTER Bank_Pointer = (uint16 *) (FLASH_BANK_SELECT + ((uint32)FIRST_AND_LAST_SECTOR[0] * FLASH_SECTOR_SIZE))
#define RESET_PAGE_POINTER Page_Pointer = (uint16 *) (FLASH_BANK_SELECT + ((uint32)FIRST_AND_LAST_SECTOR[0] * FLASH_SECTOR_SIZE)) + 8

// Define end address of last sector to be used in EEPROM Emulation
#define END_OF_SECTOR (FLASH_BANK_SELECT + ((uint32)FIRST_AND_LAST_SECTOR[1] * FLASH_SECTOR_SIZE) + (FLASH_SECTOR_SIZE - 1));

// Bank/Page Status Definitions
#define EMPTY_BANK   0xFFFF
#define CURRENT_BANK 0x5A5A
#define BLANK_PAGE   0xFFFF
#define CURRENT_PAGE 0xA5A5

#define F28P55x_FLASH_SECTOR_SIZE 0x400

#define F28P55x_NUM_FLASH_SECTORS 128


// Function Prototypes
extern int EEPROM_Config_Check();
extern void EEPROM_GetValidBank(uint16 Read_Flag);
extern void EEPROM_Erase();
extern void EEPROM_Read(uint16* Read_Buffer);
extern void EEPROM_Write(uint16* Write_Buffer);
extern void EEPROM_UpdateBankStatus();
extern void EEPROM_UpdatePageStatus();
extern void EEPROM_UpdatePageData();
extern void EEPROM_CheckStatus(Fapi_StatusType* oReturnCheck);
extern void EEPROM_Get_64_Bit_Data_Address();
extern void EEPROM_Program_64_Bits(uint16 Num_Words, uint16* Write_Buffer);
extern uint64 Configure_Protection_Masks(uint16* Sector_Numbers, uint16 Num_EEPROM_Sectors);
extern void Sample_Error();
extern void Example_Done();
extern void ClearFSMStatus();

// Global Variables
extern uint16 *Bank_Pointer;
extern uint16 *Page_Pointer;
extern uint16 *Sector_End;
extern uint32 WE_Protection_A_Mask;
extern uint32 WE_Protection_B_Mask;
extern uint32 Bank_Size;
extern uint16 Bank_Counter;
extern uint16 Page_Counter;
extern uint16 Bank_Status[8];
extern uint16 Page_Status[8];
extern uint16 NUM_EEPROM_SECTORS;
extern uint16 FIRST_AND_LAST_SECTOR[2];

#ifndef FLASH_PROGRAMMING_F28P55X_H_
#define FLASH_PROGRAMMING_F28P55X_H_

// Bank0 Sector start addresses


#define     Bzero_Sector0_start         0x00080000U
#define     Bzero_Sector1_start         0x00080400U
#define     Bzero_Sector2_start         0x00080800U
#define     Bzero_Sector3_start         0x00080C00U
#define     Bzero_Sector4_start         0x00081000U
#define     Bzero_Sector5_start         0x00081400U
#define     Bzero_Sector6_start         0x00081800U
#define     Bzero_Sector7_start         0x00081C00U
#define     Bzero_Sector8_start         0x00082000U
#define     Bzero_Sector9_start         0x00082400U
#define     Bzero_Sector10_start        0x00082800U
#define     Bzero_Sector11_start        0x00082C00U
#define     Bzero_Sector12_start        0x00083000U
#define     Bzero_Sector13_start        0x00083400U
#define     Bzero_Sector14_start        0x00083800U
#define     Bzero_Sector15_start        0x00083C00U
#define     Bzero_Sector16_start        0x00084000U
#define     Bzero_Sector17_start        0x00084400U
#define     Bzero_Sector18_start        0x00084800U
#define     Bzero_Sector19_start        0x00084C00U
#define     Bzero_Sector20_start        0x00085000U
#define     Bzero_Sector21_start        0x00085400U
#define     Bzero_Sector22_start        0x00085800U
#define     Bzero_Sector23_start        0x00085C00U
#define     Bzero_Sector24_start        0x00086000U
#define     Bzero_Sector25_start        0x00086400U
#define     Bzero_Sector26_start        0x00086800U
#define     Bzero_Sector27_start        0x00086C00U
#define     Bzero_Sector28_start        0x00087000U
#define     Bzero_Sector29_start        0x00087400U
#define     Bzero_Sector30_start        0x00087800U
#define     Bzero_Sector31_start        0x00087C00U
#define     Bzero_Sector32_start        0x00088000U
#define     Bzero_Sector33_start        0x00088400U
#define     Bzero_Sector34_start        0x00088800U
#define     Bzero_Sector35_start        0x00088C00U
#define     Bzero_Sector36_start        0x00089000U
#define     Bzero_Sector37_start        0x00089400U
#define     Bzero_Sector38_start        0x00089800U
#define     Bzero_Sector39_start        0x00089C00U
#define     Bzero_Sector40_start        0x0008A000U
#define     Bzero_Sector41_start        0x0008A400U
#define     Bzero_Sector42_start        0x0008A800U
#define     Bzero_Sector43_start        0x0008AC00U
#define     Bzero_Sector44_start        0x0008B000U
#define     Bzero_Sector45_start        0x0008B400U
#define     Bzero_Sector46_start        0x0008B800U
#define     Bzero_Sector47_start        0x0008BC00U
#define     Bzero_Sector48_start        0x0008C000U
#define     Bzero_Sector49_start        0x0008C400U
#define     Bzero_Sector50_start        0x0008C800U
#define     Bzero_Sector51_start        0x0008CC00U
#define     Bzero_Sector52_start        0x0008D000U
#define     Bzero_Sector53_start        0x0008D400U
#define     Bzero_Sector54_start        0x0008D800U
#define     Bzero_Sector55_start        0x0008DC00U
#define     Bzero_Sector56_start        0x0008E000U
#define     Bzero_Sector57_start        0x0008E400U
#define     Bzero_Sector58_start        0x0008E800U
#define     Bzero_Sector59_start        0x0008EC00U
#define     Bzero_Sector60_start        0x0008F000U
#define     Bzero_Sector61_start        0x0008F400U
#define     Bzero_Sector62_start        0x0008F800U
#define     Bzero_Sector63_start        0x0008FC00U
#define     Bzero_Sector64_start        0x00090000U
#define     Bzero_Sector65_start        0x00090400U
#define     Bzero_Sector66_start        0x00090800U
#define     Bzero_Sector67_start        0x00090C00U
#define     Bzero_Sector68_start        0x00091000U
#define     Bzero_Sector69_start        0x00091400U
#define     Bzero_Sector70_start        0x00091800U
#define     Bzero_Sector71_start        0x00091C00U
#define     Bzero_Sector72_start        0x00092000U
#define     Bzero_Sector73_start        0x00092400U
#define     Bzero_Sector74_start        0x00092800U
#define     Bzero_Sector75_start        0x00092C00U
#define     Bzero_Sector76_start        0x00093000U
#define     Bzero_Sector77_start        0x00093400U
#define     Bzero_Sector78_start        0x00093800U
#define     Bzero_Sector79_start        0x00093C00U
#define     Bzero_Sector80_start        0x00094000U
#define     Bzero_Sector81_start        0x00094400U
#define     Bzero_Sector82_start        0x00094800U
#define     Bzero_Sector83_start        0x00094C00U
#define     Bzero_Sector84_start        0x00095000U
#define     Bzero_Sector85_start        0x00095400U
#define     Bzero_Sector86_start        0x00095800U
#define     Bzero_Sector87_start        0x00095C00U
#define     Bzero_Sector88_start        0x00096000U
#define     Bzero_Sector89_start        0x00096400U
#define     Bzero_Sector90_start        0x00096800U
#define     Bzero_Sector91_start        0x00096C00U
#define     Bzero_Sector92_start        0x00097000U
#define     Bzero_Sector93_start        0x00097400U
#define     Bzero_Sector94_start        0x00097800U
#define     Bzero_Sector95_start        0x00097C00U
#define     Bzero_Sector96_start        0x00098000U
#define     Bzero_Sector97_start        0x00098400U
#define     Bzero_Sector98_start        0x00098800U
#define     Bzero_Sector99_start        0x00098C00U
#define     Bzero_Sector100_start       0x00099000U
#define     Bzero_Sector101_start       0x00099400U
#define     Bzero_Sector102_start       0x00099800U
#define     Bzero_Sector103_start       0x00099C00U
#define     Bzero_Sector104_start       0x0009A000U
#define     Bzero_Sector105_start       0x0009A400U
#define     Bzero_Sector106_start       0x0009A800U
#define     Bzero_Sector107_start       0x0009AC00U
#define     Bzero_Sector108_start       0x0009B000U
#define     Bzero_Sector109_start       0x0009B400U
#define     Bzero_Sector110_start       0x0009B800U
#define     Bzero_Sector111_start       0x0009BC00U
#define     Bzero_Sector112_start       0x0009C000U
#define     Bzero_Sector113_start       0x0009C400U
#define     Bzero_Sector114_start       0x0009C800U
#define     Bzero_Sector115_start       0x0009CC00U
#define     Bzero_Sector116_start       0x0009D000U
#define     Bzero_Sector117_start       0x0009D400U
#define     Bzero_Sector118_start       0x0009D800U
#define     Bzero_Sector119_start       0x0009DC00U
#define     Bzero_Sector120_start       0x0009E000U
#define     Bzero_Sector121_start       0x0009E400U
#define     Bzero_Sector122_start       0x0009E800U
#define     Bzero_Sector123_start       0x0009EC00U
#define     Bzero_Sector124_start       0x0009F000U
#define     Bzero_Sector125_start       0x0009F400U
#define     Bzero_Sector126_start       0x0009F800U
#define     Bzero_Sector127_start       0x0009FC00U

// Bank1 Sector start addresses

#define Bone_Sector0_start         0xA0000U
#define Bone_Sector1_start         0xA0400U
#define Bone_Sector2_start         0xA0800U
#define Bone_Sector3_start         0xA0C00U
#define Bone_Sector4_start         0xA1000U
#define Bone_Sector5_start         0xA1400U
#define Bone_Sector6_start         0xA1800U
#define Bone_Sector7_start         0xA1C00U
#define Bone_Sector8_start         0xA2000U
#define Bone_Sector9_start         0xA2400U
#define Bone_Sector10_start        0xA2800U
#define Bone_Sector11_start        0xA2C00U
#define Bone_Sector12_start        0xA3000U
#define Bone_Sector13_start        0xA3400U
#define Bone_Sector14_start        0xA3800U
#define Bone_Sector15_start        0xA3C00U
#define Bone_Sector16_start        0xA4000U
#define Bone_Sector17_start        0xA4400U
#define Bone_Sector18_start        0xA4800U
#define Bone_Sector19_start        0xA4C00U
#define Bone_Sector20_start        0xA5000U
#define Bone_Sector21_start        0xA5400U
#define Bone_Sector22_start        0xA5800U
#define Bone_Sector23_start        0xA5C00U
#define Bone_Sector24_start        0xA6000U
#define Bone_Sector25_start        0xA6400U
#define Bone_Sector26_start        0xA6800U
#define Bone_Sector27_start        0xA6C00U
#define Bone_Sector28_start        0xA7000U
#define Bone_Sector29_start        0xA7400U
#define Bone_Sector30_start        0xA7800U
#define Bone_Sector31_start        0xA7C00U
#define Bone_Sector32_start        0xA8000U
#define Bone_Sector33_start        0xA8400U
#define Bone_Sector34_start        0xA8800U
#define Bone_Sector35_start        0xA8C00U
#define Bone_Sector36_start        0xA9000U
#define Bone_Sector37_start        0xA9400U
#define Bone_Sector38_start        0xA9800U
#define Bone_Sector39_start        0xA9C00U
#define Bone_Sector40_start        0xAA000U
#define Bone_Sector41_start        0xAA400U
#define Bone_Sector42_start        0xAA800U
#define Bone_Sector43_start        0xAAC00U
#define Bone_Sector44_start        0xAB000U
#define Bone_Sector45_start        0xAB400U
#define Bone_Sector46_start        0xAB800U
#define Bone_Sector47_start        0xABC00U
#define Bone_Sector48_start        0xAC000U
#define Bone_Sector49_start        0xAC400U
#define Bone_Sector50_start        0xAC800U
#define Bone_Sector51_start        0xACC00U
#define Bone_Sector52_start        0xAD000U
#define Bone_Sector53_start        0xAD400U
#define Bone_Sector54_start        0xAD800U
#define Bone_Sector55_start        0xADC00U
#define Bone_Sector56_start        0xAE000U
#define Bone_Sector57_start        0xAE400U
#define Bone_Sector58_start        0xAE800U
#define Bone_Sector59_start        0xAEC00U
#define Bone_Sector60_start        0xAF000U
#define Bone_Sector61_start        0xAF400U
#define Bone_Sector62_start        0xAF800U
#define Bone_Sector63_start        0xAFC00U
#define Bone_Sector64_start        0xB0000U
#define Bone_Sector65_start        0xB0400U
#define Bone_Sector66_start        0xB0800U
#define Bone_Sector67_start        0xB0C00U
#define Bone_Sector68_start        0xB1000U
#define Bone_Sector69_start        0xB1400U
#define Bone_Sector70_start        0xB1800U
#define Bone_Sector71_start        0xB1C00U
#define Bone_Sector72_start        0xB2000U
#define Bone_Sector73_start        0xB2400U
#define Bone_Sector74_start        0xB2800U
#define Bone_Sector75_start        0xB2C00U
#define Bone_Sector76_start        0xB3000U
#define Bone_Sector77_start        0xB3400U
#define Bone_Sector78_start        0xB3800U
#define Bone_Sector79_start        0xB3C00U
#define Bone_Sector80_start        0xB4000U
#define Bone_Sector81_start        0xB4400U
#define Bone_Sector82_start        0xB4800U
#define Bone_Sector83_start        0xB4C00U
#define Bone_Sector84_start        0xB5000U
#define Bone_Sector85_start        0xB5400U
#define Bone_Sector86_start        0xB5800U
#define Bone_Sector87_start        0xB5C00U
#define Bone_Sector88_start        0xB6000U
#define Bone_Sector89_start        0xB6400U
#define Bone_Sector90_start        0xB6800U
#define Bone_Sector91_start        0xB6C00U
#define Bone_Sector92_start        0xB7000U
#define Bone_Sector93_start        0xB7400U
#define Bone_Sector94_start        0xB7800U
#define Bone_Sector95_start        0xB7C00U
#define Bone_Sector96_start        0xB8000U
#define Bone_Sector97_start        0xB8400U
#define Bone_Sector98_start        0xB8800U
#define Bone_Sector99_start        0xB8C00U
#define Bone_Sector100_start       0xB9000U
#define Bone_Sector101_start       0xB9400U
#define Bone_Sector102_start       0xB9800U
#define Bone_Sector103_start       0xB9C00U
#define Bone_Sector104_start       0xBA000U
#define Bone_Sector105_start       0xBA400U
#define Bone_Sector106_start       0xBA800U
#define Bone_Sector107_start       0xBAC00U
#define Bone_Sector108_start       0xBB000U
#define Bone_Sector109_start       0xBB400U
#define Bone_Sector110_start       0xBB800U
#define Bone_Sector111_start       0xBBC00U
#define Bone_Sector112_start       0xBC000U
#define Bone_Sector113_start       0xBC400U
#define Bone_Sector114_start       0xBC800U
#define Bone_Sector115_start       0xBCC00U
#define Bone_Sector116_start       0xBD000U
#define Bone_Sector117_start       0xBD400U
#define Bone_Sector118_start       0xBD800U
#define Bone_Sector119_start       0xBDC00U
#define Bone_Sector120_start       0xBE000U
#define Bone_Sector121_start       0xBE400U
#define Bone_Sector122_start       0xBE800U
#define Bone_Sector123_start       0xBEC00U
#define Bone_Sector124_start       0xBF000U
#define Bone_Sector125_start       0xBF400U
#define Bone_Sector126_start       0xBF800U
#define Bone_Sector127_start       0xBFC00U

// Bank2 Sector start addresses

#define Btwo_Sector0_start         0xC0000U
#define Btwo_Sector1_start         0xC0400U
#define Btwo_Sector2_start         0xC0800U
#define Btwo_Sector3_start         0xC0C00U
#define Btwo_Sector4_start         0xC1000U
#define Btwo_Sector5_start         0xC1400U
#define Btwo_Sector6_start         0xC1800U
#define Btwo_Sector7_start         0xC1C00U
#define Btwo_Sector8_start         0xC2000U
#define Btwo_Sector9_start         0xC2400U
#define Btwo_Sector10_start        0xC2800U
#define Btwo_Sector11_start        0xC2C00U
#define Btwo_Sector12_start        0xC3000U
#define Btwo_Sector13_start        0xC3400U
#define Btwo_Sector14_start        0xC3800U
#define Btwo_Sector15_start        0xC3C00U
#define Btwo_Sector16_start        0xC4000U
#define Btwo_Sector17_start        0xC4400U
#define Btwo_Sector18_start        0xC4800U
#define Btwo_Sector19_start        0xC4C00U
#define Btwo_Sector20_start        0xC5000U
#define Btwo_Sector21_start        0xC5400U
#define Btwo_Sector22_start        0xC5800U
#define Btwo_Sector23_start        0xC5C00U
#define Btwo_Sector24_start        0xC6000U
#define Btwo_Sector25_start        0xC6400U
#define Btwo_Sector26_start        0xC6800U
#define Btwo_Sector27_start        0xC6C00U
#define Btwo_Sector28_start        0xC7000U
#define Btwo_Sector29_start        0xC7400U
#define Btwo_Sector30_start        0xC7800U
#define Btwo_Sector31_start        0xC7C00U
#define Btwo_Sector32_start        0xC8000U
#define Btwo_Sector33_start        0xC8400U
#define Btwo_Sector34_start        0xC8800U
#define Btwo_Sector35_start        0xC8C00U
#define Btwo_Sector36_start        0xC9000U
#define Btwo_Sector37_start        0xC9400U
#define Btwo_Sector38_start        0xC9800U
#define Btwo_Sector39_start        0xC9C00U
#define Btwo_Sector40_start        0xCA000U
#define Btwo_Sector41_start        0xCA400U
#define Btwo_Sector42_start        0xCA800U
#define Btwo_Sector43_start        0xCAC00U
#define Btwo_Sector44_start        0xCB000U
#define Btwo_Sector45_start        0xCB400U
#define Btwo_Sector46_start        0xCB800U
#define Btwo_Sector47_start        0xCBC00U
#define Btwo_Sector48_start        0xCC000U
#define Btwo_Sector49_start        0xCC400U
#define Btwo_Sector50_start        0xCC800U
#define Btwo_Sector51_start        0xCCC00U
#define Btwo_Sector52_start        0xCD000U
#define Btwo_Sector53_start        0xCD400U
#define Btwo_Sector54_start        0xCD800U
#define Btwo_Sector55_start        0xCDC00U
#define Btwo_Sector56_start        0xCE000U
#define Btwo_Sector57_start        0xCE400U
#define Btwo_Sector58_start        0xCE800U
#define Btwo_Sector59_start        0xCEC00U
#define Btwo_Sector60_start        0xCF000U
#define Btwo_Sector61_start        0xCF400U
#define Btwo_Sector62_start        0xCF800U
#define Btwo_Sector63_start        0xCFC00U
#define Btwo_Sector64_start        0xD0000U
#define Btwo_Sector65_start        0xD0400U
#define Btwo_Sector66_start        0xD0800U
#define Btwo_Sector67_start        0xD0C00U
#define Btwo_Sector68_start        0xD1000U
#define Btwo_Sector69_start        0xD1400U
#define Btwo_Sector70_start        0xD1800U
#define Btwo_Sector71_start        0xD1C00U
#define Btwo_Sector72_start        0xD2000U
#define Btwo_Sector73_start        0xD2400U
#define Btwo_Sector74_start        0xD2800U
#define Btwo_Sector75_start        0xD2C00U
#define Btwo_Sector76_start        0xD3000U
#define Btwo_Sector77_start        0xD3400U
#define Btwo_Sector78_start        0xD3800U
#define Btwo_Sector79_start        0xD3C00U
#define Btwo_Sector80_start        0xD4000U
#define Btwo_Sector81_start        0xD4400U
#define Btwo_Sector82_start        0xD4800U
#define Btwo_Sector83_start        0xD4C00U
#define Btwo_Sector84_start        0xD5000U
#define Btwo_Sector85_start        0xD5400U
#define Btwo_Sector86_start        0xD5800U
#define Btwo_Sector87_start        0xD5C00U
#define Btwo_Sector88_start        0xD6000U
#define Btwo_Sector89_start        0xD6400U
#define Btwo_Sector90_start        0xD6800U
#define Btwo_Sector91_start        0xD6C00U
#define Btwo_Sector92_start        0xD7000U
#define Btwo_Sector93_start        0xD7400U
#define Btwo_Sector94_start        0xD7800U
#define Btwo_Sector95_start        0xD7C00U
#define Btwo_Sector96_start        0xD8000U
#define Btwo_Sector97_start        0xD8400U
#define Btwo_Sector98_start        0xD8800U
#define Btwo_Sector99_start        0xD8C00U
#define Btwo_Sector100_start       0xD9000U
#define Btwo_Sector101_start       0xD9400U
#define Btwo_Sector102_start       0xD9800U
#define Btwo_Sector103_start       0xD9C00U
#define Btwo_Sector104_start       0xDA000U
#define Btwo_Sector105_start       0xDA400U
#define Btwo_Sector106_start       0xDA800U
#define Btwo_Sector107_start       0xDAC00U
#define Btwo_Sector108_start       0xDB000U
#define Btwo_Sector109_start       0xDB400U
#define Btwo_Sector110_start       0xDB800U
#define Btwo_Sector111_start       0xDBC00U
#define Btwo_Sector112_start       0xDC000U
#define Btwo_Sector113_start       0xDC400U
#define Btwo_Sector114_start       0xDC800U
#define Btwo_Sector115_start       0xDCC00U
#define Btwo_Sector116_start       0xDD000U
#define Btwo_Sector117_start       0xDD400U
#define Btwo_Sector118_start       0xDD800U
#define Btwo_Sector119_start       0xDDC00U
#define Btwo_Sector120_start       0xDE000U
#define Btwo_Sector121_start       0xDE400U
#define Btwo_Sector122_start       0xDE800U
#define Btwo_Sector123_start       0xDEC00U
#define Btwo_Sector124_start       0xDF000U
#define Btwo_Sector125_start       0xDF400U
#define Btwo_Sector126_start       0xDF800U
#define Btwo_Sector127_start       0xDFC00U

// Bank3 Sector start addresses

#define Bthree_Sector0_start         0xE0000U
#define FlashBank3deltaStartAddress  0xE0040U
#define Bthree_Sector1_start         0xE0400U
#define Bthree_Sector2_start         0xE0800U
#define Bthree_Sector3_start         0xE0C00U
#define Bthree_Sector4_start         0xE1000U
#define Bthree_Sector5_start         0xE1400U
#define Bthree_Sector6_start         0xE1800U
#define Bthree_Sector7_start         0xE1C00U
#define Bthree_Sector8_start         0xE2000U
#define Bthree_Sector9_start         0xE2400U
#define Bthree_Sector10_start        0xE2800U
#define Bthree_Sector11_start        0xE2C00U
#define Bthree_Sector12_start        0xE3000U
#define Bthree_Sector13_start        0xE3400U
#define Bthree_Sector14_start        0xE3800U
#define Bthree_Sector15_start        0xE3C00U
#define Bthree_Sector16_start        0xE4000U
#define Bthree_Sector17_start        0xE4400U
#define Bthree_Sector18_start        0xE4800U
#define Bthree_Sector19_start        0xE4C00U
#define Bthree_Sector20_start        0xE5000U
#define Bthree_Sector21_start        0xE5400U
#define Bthree_Sector22_start        0xE5800U
#define Bthree_Sector23_start        0xE5C00U
#define Bthree_Sector24_start        0xE6000U
#define Bthree_Sector25_start        0xE6400U
#define Bthree_Sector26_start        0xE6800U
#define Bthree_Sector27_start        0xE6C00U
#define Bthree_Sector28_start        0xE7000U
#define Bthree_Sector29_start        0xE7400U
#define Bthree_Sector30_start        0xE7800U
#define Bthree_Sector31_start        0xE7C00U
#define Bthree_Sector32_start        0xE8000U
#define Bthree_Sector33_start        0xE8400U
#define Bthree_Sector34_start        0xE8800U
#define Bthree_Sector35_start        0xE8C00U
#define Bthree_Sector36_start        0xE9000U
#define Bthree_Sector37_start        0xE9400U
#define Bthree_Sector38_start        0xE9800U
#define Bthree_Sector39_start        0xE9C00U
#define Bthree_Sector40_start        0xEA000U
#define Bthree_Sector41_start        0xEA400U
#define Bthree_Sector42_start        0xEA800U
#define Bthree_Sector43_start        0xEAC00U
#define Bthree_Sector44_start        0xEB000U
#define Bthree_Sector45_start        0xEB400U
#define Bthree_Sector46_start        0xEB800U
#define Bthree_Sector47_start        0xEBC00U
#define Bthree_Sector48_start        0xEC000U
#define Bthree_Sector49_start        0xEC400U
#define Bthree_Sector50_start        0xEC800U
#define Bthree_Sector51_start        0xECC00U
#define Bthree_Sector52_start        0xED000U
#define Bthree_Sector53_start        0xED400U
#define Bthree_Sector54_start        0xED800U
#define Bthree_Sector55_start        0xEDC00U
#define Bthree_Sector56_start        0xEE000U
#define Bthree_Sector57_start        0xEE400U
#define Bthree_Sector58_start        0xEE800U
#define Bthree_Sector59_start        0xEEC00U
#define Bthree_Sector60_start        0xEF000U
#define Bthree_Sector61_start        0xEF400U
#define Bthree_Sector62_start        0xEF800U
#define Bthree_Sector63_start        0xEFC00U
#define Bthree_Sector64_start        0xF0000U
#define Bthree_Sector65_start        0xF0400U
#define Bthree_Sector66_start        0xF0800U
#define Bthree_Sector67_start        0xF0C00U
#define Bthree_Sector68_start        0xF1000U
#define Bthree_Sector69_start        0xF1400U
#define Bthree_Sector70_start        0xF1800U
#define Bthree_Sector71_start        0xF1C00U
#define Bthree_Sector72_start        0xF2000U
#define Bthree_Sector73_start        0xF2400U
#define Bthree_Sector74_start        0xF2800U
#define Bthree_Sector75_start        0xF2C00U
#define Bthree_Sector76_start        0xF3000U
#define Bthree_Sector77_start        0xF3400U
#define Bthree_Sector78_start        0xF3800U
#define Bthree_Sector79_start        0xF3C00U
#define Bthree_Sector80_start        0xF4000U
#define Bthree_Sector81_start        0xF4400U
#define Bthree_Sector82_start        0xF4800U
#define Bthree_Sector83_start        0xF4C00U
#define Bthree_Sector84_start        0xF5000U
#define Bthree_Sector85_start        0xF5400U
#define Bthree_Sector86_start        0xF5800U
#define Bthree_Sector87_start        0xF5C00U
#define Bthree_Sector88_start        0xF6000U
#define Bthree_Sector89_start        0xF6400U
#define Bthree_Sector90_start        0xF6800U
#define Bthree_Sector91_start        0xF6C00U
#define Bthree_Sector92_start        0xF7000U
#define Bthree_Sector93_start        0xF7400U
#define Bthree_Sector94_start        0xF7800U
#define Bthree_Sector95_start        0xF7C00U
#define Bthree_Sector96_start        0xF8000U
#define Bthree_Sector97_start        0xF8400U
#define Bthree_Sector98_start        0xF8800U
#define Bthree_Sector99_start        0xF8C00U
#define Bthree_Sector100_start       0xF9000U
#define Bthree_Sector101_start       0xF9400U
#define Bthree_Sector102_start       0xF9800U
#define Bthree_Sector103_start       0xF9C00U
#define Bthree_Sector104_start       0xFA000U
#define Bthree_Sector105_start       0xFA400U
#define Bthree_Sector106_start       0xFA800U
#define Bthree_Sector107_start       0xFAC00U
#define Bthree_Sector108_start       0xFB000U
#define Bthree_Sector109_start       0xFB400U
#define Bthree_Sector110_start       0xFB800U
#define Bthree_Sector111_start       0xFBC00U
#define Bthree_Sector112_start       0xFC000U
#define Bthree_Sector113_start       0xFC400U
#define Bthree_Sector114_start       0xFC800U
#define Bthree_Sector115_start       0xFCC00U
#define Bthree_Sector116_start       0xFD000U
#define Bthree_Sector117_start       0xFD400U
#define Bthree_Sector118_start       0xFD800U
#define Bthree_Sector119_start       0xFDC00U
#define Bthree_Sector120_start       0xFE000U
#define Bthree_Sector121_start       0xFE400U
#define Bthree_Sector122_start       0xFE800U
#define Bthree_Sector123_start       0xFEC00U
#define Bthree_Sector124_start       0xFF000U
#define Bthree_Sector125_start       0xFF400U
#define Bthree_Sector126_start       0xFF800U
#define Bthree_Sector127_start       0xFFC00U


// Bank4 Sector start addresses
#define Bfour_Sector0_start         0x100000U
#define Bfour_Sector1_start         0x100400U
#define Bfour_Sector2_start         0x100800U
#define Bfour_Sector3_start         0x100C00U
#define Bfour_Sector4_start         0x101000U
#define Bfour_Sector5_start         0x101400U
#define Bfour_Sector6_start         0x101800U
#define Bfour_Sector7_start         0x101C00U
#define Bfour_Sector8_start         0x102000U
#define Bfour_Sector9_start         0x102400U
#define Bfour_Sector10_start        0x102800U
#define Bfour_Sector11_start        0x102C00U
#define Bfour_Sector12_start        0x103000U
#define Bfour_Sector13_start        0x103400U
#define Bfour_Sector14_start        0x103800U
#define Bfour_Sector15_start        0x103C00U
#define Bfour_Sector16_start        0x104000U
#define Bfour_Sector17_start        0x104400U
#define Bfour_Sector18_start        0x104800U
#define Bfour_Sector19_start        0x104C00U
#define Bfour_Sector20_start        0x105000U
#define Bfour_Sector21_start        0x105400U
#define Bfour_Sector22_start        0x105800U
#define Bfour_Sector23_start        0x105C00U
#define Bfour_Sector24_start        0x106000U
#define Bfour_Sector25_start        0x106400U
#define Bfour_Sector26_start        0x106800U
#define Bfour_Sector27_start        0x106C00U
#define Bfour_Sector28_start        0x107000U
#define Bfour_Sector29_start        0x107400U
#define Bfour_Sector30_start        0x107800U
#define Bfour_Sector31_start        0x107C00U


#define     FlashBank0StartAddress      0x80000U
#define     FlashBank0EndAddress        0x9FFFFU
#define     FlashBank1StartAddress      0xA0000U
#define     FlashBank1EndAddress        0xBFFFFU
#define     FlashBank2StartAddress      0xC0000U
#define     FlashBank2EndAddress        0xDFFFFU
#define     FlashBank3StartAddress      0xE0000U
#define     FlashBank3EndAddress        0xFFFFFU
#define     FlashBank4StartAddress      0x100000U
#define     FlashBank4EndAddress        0x107FFFU

//Sector length in number of 16bits
#define Sector2KB_u16length   0x400U
#define Bank256KB_u16length   0x20000U

//Sector length in number of 32bits
#define Sector2KB_u32length   0x200U
#define Bank256KB_u32length   0x10000U

//Flash wrapper base address
#define  FLASH_WRAPPER_PROGRAM_BASE         0x51000U /*(FLASH0CMD_BASE - 0x1000UL)*/

/*!
    \brief Define to map the direct access to the FMC registers.
*/
#define FlashTech_CPU0_BASE_ADDRESS ((Fapi_FmcRegistersType *)FLASH_WRAPPER_PROGRAM_BASE)

#endif /* FLASH_PROGRAMMING_F28P55X_H_ */
