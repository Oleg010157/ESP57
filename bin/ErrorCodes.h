#ifndef ERRORCODES_H_
#define ERRORCODES_H_
// Файл описания кодов ошибок.
// Формат: |#define| |обозначение ошибки|  |индекс ошибки|  |// Описание ошибки|
// Пример: #define Err100 25 // Ошибка 100
// Ошибка Err100 с индексом 25 и описанием `Ошибка 100`
// Код ошибки - string, Индекс ошибки Uint16 - значение от 0 до 255 в десятичном представлении

// Номера 1..27 освободились: там сидели коды платы B81 (DC-DC конвертор
// 48/600 В плюс трёхфазный инвертор) - cmp*_Ia/Ib/Ic, TRIG1/TRIG2,
// inv_T1..inv_T7, conv_T1..conv_T4, VT*_2. Этого железа на плате нет.
// Номера оставшихся кодов не сдвигались: счётчики истории в EEPROM
// привязаны к номеру, и перенумерация переписала бы чужую статистику.

#define CANA_Ewarn 28 // Ошибка CAN Ewarn
#define CANA_Epass 29 // Ошибка CAN Epass
#define CANA_Boff  30 // Ошибка CAN Bus_Off
#define CANA_LEC3  31 // Ошибка CAN LEC=3 no ACK

// Trip-zone. Номер 32, а не 31: прежний trip4_uknow делил 31 с CANA_LEC3,
// и окно кодов показывало для этого номера первое совпадение по файлу.
#define tz_unknown 32 // Сработала trip-zone, источник не опознан

#define Udc_Over      40 // Перенапряжение Udc
#define IGBT_OverTemp 41 // Перегрев модуля IGBT

// Сетевой режим (grid-tie), my_Files/grid_tie.c
#define gt_noGrid    60 // Пропала сеть: нет захватов нуля U220 более 1.5 периода
#define gt_overIpri  61 // Превышен ток первички трансформатора
#define gt_overIsec  62 // Превышен ток вторички (ток в сеть)
#define gt_UdcRange  63 // Напряжение АКБ вне рабочего окна
#define gt_mSat      64 // Систематический упор индекса модуляции: не хватает Udc или n
#define gt_syncLost  65 // Потерян синхронизм с сетью (частота, амплитуда или фаза)
#define gt_dcSat     66 // Упор поправки постоянки: контур её больше не вытягивает
#define gt_uOutSat   67 // Упор контура напряжения острова: уставка недостижима

//  EEPROM
#define Offsets_ERR 50   // не совпадает контрольная сумма offsets считанных из EEPROM.
#define Offsets_NOCAL 51 // не совпадает контрольная сумма offsets считанных из EEPROM.
#define Kadc_ERR 52      // не совпадает контрольная сумма коэф. kadc считанных из EEPROM.
#define Kadc_NOCAL 53    // не совпадает контрольная сумма коэф. kadc считанных из EEPROM.
#define S_Off_ERR 54     // Ошибка сохранения оффсетов (Нет связи с FRAM)
#define L_Off_ERR 55     // Ошибка загрузки оффсетов (Нет связи с FRAM)
#define CE01 56          // Ошибка FRAM

#endif /* ERRORCODES_H_ */
