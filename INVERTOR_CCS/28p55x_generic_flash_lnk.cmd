MEMORY
{
   BEGIN            : origin = 0x080000, length = 0x000002  // Update the codestart location as needed

   BOOT_RSVD        : origin = 0x000002, length = 0x000126     /* Part of M0, BOOT rom will use this for stack */
   RAMM0            : origin = 0x000128, length = 0x0002D8
   RAMM1            : origin = 0x000400, length = 0x000400

   RAMLS0           : origin = 0x008000, length = 0x000800
   RAMLS1           : origin = 0x008800, length = 0x000800
   RAMLS2           : origin = 0x009000, length = 0x000800
   RAMLS3           : origin = 0x009800, length = 0x000800
   RAMLS4           : origin = 0x00A000, length = 0x000800
   RAMLS5           : origin = 0x00A800, length = 0x000800
   RAMLS6           : origin = 0x00B000, length = 0x000800
   RAMLS7           : origin = 0x00B800, length = 0x000800
   RAMLS8           : origin = 0x014000, length = 0x002000  // When configured as CLA program use the address 0x4000
   RAMLS9           : origin = 0x016000, length = 0x002000  // When configured as CLA program use the address 0x6000

   // RAMLS8_CLA    : origin = 0x004000, length = 0x002000  // Use only if configured as CLA program memory
   // RAMLS9_CLA    : origin = 0x006000, length = 0x002000  // Use only if configured as CLA program memory

   RAMGS0           : origin = 0x00C000, length = 0x002000
   RAMGS1           : origin = 0x00E000, length = 0x002000
   RAMGS2           : origin = 0x010000, length = 0x002000
   RAMGS3           : origin = 0x012000, length = 0x002000

   /* Flash Banks (128 sectors each) */
   FLASH_BANK0     : origin = 0x080002, length = 0x1FFFE
   FLASH_BANK1     : origin = 0x0A0000, length = 0x20000
   FLASH_BANK2     : origin = 0x0C0000, length = 0x20000
   FLASH_BANK3     : origin = 0x0E0000, length = 0x20000
   // BANK4 целиком отдан под энергонезависимое хранилище (калибровки АЦП,
   // в перспективе журнал ошибок). Секции сюда НЕ размещать: банк стирается
   // и перезаписывается на ходу через Flash API.
   //
   // Отдельный банк выбран не ради места: во время стирания/записи банк
   // недоступен на чтение, а код идёт из BANK0/BANK1 и продолжает работать.
   // Адрес продублирован константой CALIB_FLASH_ADDR - через неё, а не через
   // символ линкера, потому что в RAM-сборке банки флеша вообще не объявлены,
   // а хранилище должно работать в обеих конфигурациях одинаково.
   FLASH_CALIB     : origin = 0x100000, length = 0x08000


   CLATOCPURAM      : origin = 0x001480,   length = 0x000080
   CPUTOCLARAM      : origin = 0x001500,   length = 0x000080
   CLATODMARAM      : origin = 0x001680,   length = 0x000080
   DMATOCLARAM      : origin = 0x001700,   length = 0x000080

   RESET            : origin = 0x3FFFC0, length = 0x000002
}


SECTIONS
{
   codestart        : > BEGIN
   .text            : >> FLASH_BANK0 | FLASH_BANK1, ALIGN(8)
   .cinit           : > FLASH_BANK0, ALIGN(8)
   .switch          : > FLASH_BANK0, ALIGN(8)
   .reset           : > RESET, TYPE = DSECT /* not used, */

   .stack           : > RAMM1
#if defined(__TI_EABI__)
   .bss             : > RAMLS5
   .bss:output      : > RAMLS3
   .init_array      : > FLASH_BANK0, ALIGN(8)
   .const           : > FLASH_BANK0, ALIGN(8)
   .data            : > RAMLS5  | RAMLS6
   .sysmem          : > RAMLS4
#else
   .pinit           : > FLASH_BANK0, ALIGN(8)
   .ebss            : >> RAMLS5 | RAMLS6
   .econst          : > FLASH_BANK0, ALIGN(8)
   .esysmem         : > RAMLS5
#endif

   ramgs0 : > RAMGS0
   ramgs1 : > RAMGS1
   ramgs2 : > RAMGS2

   #if defined(__TI_EABI__)
       .TI.ramfunc : {} LOAD = FLASH_BANK0,
                        RUN = RAMGS0,
                        LOAD_START(RamfuncsLoadStart),
                        LOAD_SIZE(RamfuncsLoadSize),
                        LOAD_END(RamfuncsLoadEnd),
                        RUN_START(RamfuncsRunStart),
                        RUN_SIZE(RamfuncsRunSize),
                        RUN_END(RamfuncsRunEnd),
                        ALIGN(8)
   #else
       .TI.ramfunc : {} LOAD = FLASH_BANK0,
                        RUN = RAMGS0,
                        LOAD_START(_RamfuncsLoadStart),
                        LOAD_SIZE(_RamfuncsLoadSize),
                        LOAD_END(_RamfuncsLoadEnd),
                        RUN_START(_RamfuncsRunStart),
                        RUN_SIZE(_RamfuncsRunSize),
                        RUN_END(_RamfuncsRunEnd),
                        ALIGN(8)
   #endif

   /* CLA specific sections
    *
    * Cla1Prog и .const_cla размещаются с LOAD/RUN-разделением, а не одной
    * строкой "> RAMLSx". Причина в том, что при сборке во флеш программу CLA
    * надо СКОПИРОВАТЬ из флеша в LS-RAM перед стартом задач, и делает это
    * memcpy в сгенерированном SysConfig CLA_init() (CPU1_FLASH/syscfg/board.c).
    * Ему нужны символы границ: без LOAD_START/RUN_START/LOAD_SIZE линковка
    * падает с "unresolved symbols: Cla1ProgLoadStart ...".
    *
    * Порядок в Board_init() важен и уже верен: CLA_init() (копирование, пока
    * LS1 ещё принадлежит CPU) идёт ДО MEMCFG_init(), который отдаёт LS1 в CLA
    * как программную память. Менять местами нельзя.
    *
    * RUN-блоки те же, что были: LS1 - программа, LS2 - данные, LS7 - константы.
    * Соответствие с SysConfig обязательно (memcfg: LS1 = CLA_prog,
    * LS2 и LS7 = CLA_data); разъедутся - задача пойдёт по чужой памяти.
    * У F28P55x блоки LS0-LS7 видны CLA по тем же адресам, что и CPU, - алиасы
    * 0x4000/0x6000 есть только у LS8/LS9 (см. комментарии в MEMORY выше).
    */
#if defined(__TI_EABI__)
   Cla1Prog        : LOAD = FLASH_BANK0,
                     RUN = RAMLS1,
                     LOAD_START(Cla1ProgLoadStart),
                     RUN_START(Cla1ProgRunStart),
                     LOAD_SIZE(Cla1ProgLoadSize),
                     ALIGN(4)
#else
   Cla1Prog        : LOAD = FLASH_BANK0,
                     RUN = RAMLS1,
                     LOAD_START(_Cla1ProgLoadStart),
                     RUN_START(_Cla1ProgRunStart),
                     LOAD_SIZE(_Cla1ProgLoadSize),
                     ALIGN(4)
#endif

   .scratchpad     : > RAMLS2
   .bss_cla        : > RAMLS2
   cla_shared      : > RAMLS2, ALIGN(4)

#if defined(__TI_EABI__)
   .const_cla      : LOAD = FLASH_BANK0,
                     RUN = RAMLS7,
                     LOAD_START(Cla1ConstLoadStart),
                     RUN_START(Cla1ConstRunStart),
                     LOAD_SIZE(Cla1ConstLoadSize),
                     ALIGN(4)
#else
   .const_cla      : LOAD = FLASH_BANK0,
                     RUN = RAMLS7,
                     LOAD_START(_Cla1ConstLoadStart),
                     RUN_START(_Cla1ConstRunStart),
                     LOAD_SIZE(_Cla1ConstLoadSize),
                     ALIGN(4)
#endif

   Cla1ToCpuMsgRAM : > CLATOCPURAM
   CpuToCla1MsgRAM : > CPUTOCLARAM

}

/*
//===========================================================================
// End of file.
//===========================================================================
*/
