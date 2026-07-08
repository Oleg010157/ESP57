namespace FlagMask
{
    #region Маски Флагов управления для передачи/приёма в CCS
    class Mask
    {
        public const uint RampOff        = Bit.Bit0;      // 0
        public const uint SLimit         = Bit.Bit1;      // 1 Ограничение скольжения
        public const uint TransistorImit = Bit.Bit2;      // 2 Имитация транзистора

        public const uint ResolInverse   = Bit.Bit3;      // 3
        public const uint Reverse        = Bit.Bit4;      // 4
        public const uint L1Test         = Bit.Bit5;      // 5
        public const uint L2Test         = Bit.Bit6;      // 6

        public const uint Calibr         = Bit.Bit7;      // 7 требование выполнить калибровку  = 1 иниц.
        public const uint SlipFreqRpm    = Bit.Bit8;      // 8
        public const uint InitialCalibr  = Bit.Bit9;      // 9

        public const uint RezimPodhvata  = Bit.Bit10;     // 10
        public const uint SwitchZero     = Bit.Bit11;     // 11
        public const uint GenEngineSw1   = Bit.Bit12;     // 12 меняет положение генератор/двигатель если = 0 то двигатель слева т.е. АИН1
        public const uint GenEngineSw2   = Bit.Bit13;     // 13 флаг держать заданные обороты двигателя

        public const uint Stability      = Bit.Bit14;     // 14

        public const uint Ain2Disable    = Bit.Bit15;     // 15
        public const uint HeatResistor   = Bit.Bit16;     // 16
        public const uint OffsetFlag     = Bit.Bit17;     // 17

        public const uint UnaF           = Bit.Bit18;     // 18
        public const uint VoltStab       = Bit.Bit19;     // 19
        public const uint VRegul         = Bit.Bit20;     // 20

        public const uint KvuOn          = Bit.Bit21;     // 21
        public const uint Ain1Disable    = Bit.Bit22;     // 22  =1 запрещает включение ШИМ АИН1
        public const uint Restore        = Bit.Bit23;     // 23
        public const uint NeedBreak      = Bit.Bit24;     // 24
        public const uint InstVolts      = Bit.Bit25;     // 25
        public const uint GainNullfreq   = Bit.Bit26;     // 26  =1 разрешает изменять напряжение на двигателе когда нет вращения магнитного поля.

        public const uint CanHeaderInit  = Bit.Bit27;     // 27

        public const uint RpmImit        = Bit.Bit28;     // 28
        public const uint Pusk           = Bit.Bit29;     // 29 кнопка "Пуск" 1- активна,
        public const uint Stop           = Bit.Bit30;     // 30 кнопка "Стоп" 1- активна,
        public const uint Connect        = Bit.Bit31;     // 31 кнопка "Подключить" 1- была нажата,
    }
    #endregion Маски Флагов управления для передачи/приёма в CCS

    #region Маски битов управления общие для обоих АИН
    class sbit
    {
        public const ushort Connect = Bit16.Bit0;      // 0  кнопка "Подключить/Отключить" -> 1/0
        public const ushort Reverse = Bit16.Bit1;      // 1  кнопка "Реверс" глобальный
        public const ushort ResolInverse = Bit16.Bit2; // 2  кнопка "Резолвер Инв"   
        public const ushort Pusk = Bit16.Bit3;         // 3  кнопка "Пуск/Стоп" -> 1/0
        public const ushort motfast = Bit16.Bit4;      // 4 бит управляет выбором мотора для записи в быстрый кадр 0- мотор1, 1- мотор2
        public const ushort motK60 = Bit16.Bit5;       // 5 бит управляет выбором мотора для записи в медленный  кадр
    }
    #endregion Маски битов управления общие для обоих АИН 

    #region маски  битов управления АИН
    class mbits
    {
        public const ushort emadd          = Bit16.Bit0;      // 0
        public const ushort rgfix          = Bit16.Bit1;      // 1 включение режима задания частоты магнитного поля с ограничением скольжения.Работает при разгоне и замедлении.
        public const ushort Iq_ref         = Bit16.Bit2;      // 2
        public const ushort Id_ref         = Bit16.Bit3;      // 3
        public const ushort kIqd           = Bit16.Bit4;      // 4
        public const ushort revers         = Bit16.Bit5;      // 5
        public const ushort AIN_Disable    = Bit16.Bit6;      // 6
        public const ushort epsi           = Bit16.Bit7;      // 7 требование выполнить калибровку  = 1 иниц.
        public const ushort emstep         = Bit16.Bit8;      // 8
        public const ushort Im             = Bit16.Bit9;      // 9
        public const ushort Ltest          = Bit16.Bit10;     // 10
        public const ushort sMax_On        = Bit16.Bit11;     // 11
        public const ushort reserv12       = Bit16.Bit12;     // 12 
        public const ushort Gen_Engine_SW  = Bit16.Bit13;     // 13 флаг держать заданные обороты двигателя
        public const ushort FOC_mode       = Bit16.Bit14;     // 14
        public const ushort rez15          = Bit16.Bit15;     // 15
    }
    #endregion маски битов управления АИН

    #region маски флагов срабатывания защиты 
    class errorFlag
    {
        public const ushort Ia_Overload          = Bit16.Bit0;      // 0
        public const ushort Ib_Overload          = Bit16.Bit1;      // 1
        public const ushort Ic_Overload          = Bit16.Bit2;      // 2
        public const ushort Idc_Overload          = Bit16.Bit3;      // 3
        public const ushort Udc_Overload         = Bit16.Bit4;      // 4
        public const ushort Vin_Overload          = Bit16.Bit5;      // 5
        public const ushort IGBT_OverTemperature = Bit16.Bit6;      // 6
    }
    #endregion


    class ccsFlag
    {
        public const uint ToothSyncro   = Bit.Bit0;   // 1
        public const uint Udc_IGBToff   = Bit.Bit1;   // 2
        public const uint Pwm1On        = Bit.Bit2;   // 3
        public const uint Pwm2On        = Bit.Bit3;   // 4
        public const uint I1AOverload   = Bit.Bit4;   // 5
        public const uint I1BOverload   = Bit.Bit5;   // 6
        public const uint I1COverload   = Bit.Bit6;   // 7
        public const uint I2AOverload   = Bit.Bit7;   // 8
        public const uint I2BOverload   = Bit.Bit8;   // 9
        public const uint I2COverload   = Bit.Bit9;   // 10
        public const uint Optic1Err1    = Bit.Bit10;  // 11 T1
        public const uint Optic1Err2    = Bit.Bit11;  // 12 T2
        public const uint Optic1Err3    = Bit.Bit12;  // 13 T3
        public const uint Optic1Err4    = Bit.Bit13;  // 14 T4
        public const uint Optic1Err5    = Bit.Bit14;  // 15 T5
        public const uint Optic1Err6    = Bit.Bit15;  // 16 T6
        public const uint Optic1Err7    = Bit.Bit16;  // 17 RT1
        public const uint Ain1Trig      = Bit.Bit17;  // 18 Триггер АИН1 Мотор  GpioDataRegs.GPADAT.bit.GPIO16
        public const uint Rt1Trig       = Bit.Bit18;  // 19 Триггер RT1
        public const uint Optic2Err1    = Bit.Bit19;  // 20 T1
        public const uint Optic2Err2    = Bit.Bit20;  // 21 T2
        public const uint Optic2Err3    = Bit.Bit21;  // 22 T3
        public const uint Optic2Err4    = Bit.Bit22;  // 23 T4
        public const uint Optic2Err5    = Bit.Bit23;  // 24 Т5
        public const uint Optic2Err6    = Bit.Bit24;  // 25 Т6
        public const uint Optic2Err7    = Bit.Bit25;  // 26 RT2
        public const uint Ain2Trig      = Bit.Bit26;  // 27 Триггер АИН2 Мотор  GpioDataRegs.GPADAT.bit.GPIO17;
        public const uint Rt2Trig       = Bit.Bit27;  // 28 выход RS тригера захвата ошибки по оптике тормозного резистора
        public const uint ResetTrig     = Bit.Bit28;  // 29 сброс тригеров оптических неисправностей  GpioDataRegs.GPCDAT.bit.GPIO71;
        public const uint Rt1On         = Bit.Bit29;  // 30 GpioDataRegs.GPADAT.bit.GPIO26;    // выход управления тормозного резистора  RT1
        public const uint Rt2On         = Bit.Bit30;  // 31 GpioDataRegs.GPADAT.bit.GPIO27;     // выход управления тормозного резистора RT2
        public const uint ResolverFault = Bit.Bit31;  // 32
    }

    class KuKs
    {
        // ----  kuk1 ----------------------------------------------------------------------------------------------
        public const ushort ControlCard  = Bit16.Bit0;   // 0 кук для работы на отладочной платы ControlCard
        public const ushort KspBoard     = Bit16.Bit1;   // 1 кук для работы на плате КСП
        public const ushort Fram         = Bit16.Bit2;   // 2 используется FRAM
        public const ushort Qep1Rpm1     = Bit16.Bit3;   // 3 на QEP1 подключён датчик оборотов  первого двигателя  
        public const ushort Qep1Rpm2     = Bit16.Bit4;   // 4 на QEP1 подключён датчик оборотов  второго двигателя
        public const ushort Cap1Rpm1     = Bit16.Bit5;   // 5 на CAP1 подключён датчик оборотов  первого двигателя  
        public const ushort Cap1Rpm2     = Bit16.Bit6;   // 6 на CAP1 подключён датчик оборотов  второго двигателя
        public const ushort Qep2Rpm1     = Bit16.Bit7;   // 7 на QEP2 подключён датчик оборотов  первого двигателя  
        public const ushort Qep2Rpm2     = Bit16.Bit8;   // 8 на QEP2 подключён датчик оборотов  второго двигателя
        public const ushort Cap2Rpm1     = Bit16.Bit9;   // 9 на CAP2 подключён датчик оборотов  первого двигателя  
        public const ushort Cap2Rpm2     = Bit16.Bit10;  // 10 на CAP2 подключён датчик оборотов  второго двигателя
        public const ushort Resolver     = Bit16.Bit11;  // 11 используется Resolver
        public const ushort Tad1Z        = Bit16.Bit12;  // 12 используется индексный датчик TAD170 со счётным колесом с одним зубом.
        public const ushort ReverseDet   = Bit16.Bit13;  // 13 Режим с обнаружением направления вращения
        public const ushort ZToothRes    = Bit16.Bit14;  // 14 Сброс нулевого зуба от второго датчика
        public const ushort Tooth        = Bit16.Bit15;  // 16 кук коррекции зубьев счётного диска
        // ----  kuk2 ----------------------------------------------------------------------------------------------
    }

    #region bit.BITmask
    class Bit
    {
        public const uint Bit0  = 0x0001;
        public const uint Bit1  = 0x0002;
        public const uint Bit2  = 0x0004;
        public const uint Bit3  = 0x0008;
        public const uint Bit4  = 0x0010;
        public const uint Bit5  = 0x0020;
        public const uint Bit6  = 0x0040;
        public const uint Bit7  = 0x0080;
        public const uint Bit8  = 0x0100;
        public const uint Bit9  = 0x0200;
        public const uint Bit10 = 0x0400;
        public const uint Bit11 = 0x0800;
        public const uint Bit12 = 0x1000;
        public const uint Bit13 = 0x2000;
        public const uint Bit14 = 0x4000;
        public const uint Bit15 = 0x8000;
        public const uint Bit16 = 0x10000;
        public const uint Bit17 = 0x20000;
        public const uint Bit18 = 0x40000;
        public const uint Bit19 = 0x80000;
        public const uint Bit20 = 0x100000;
        public const uint Bit21 = 0x200000;
        public const uint Bit22 = 0x400000;
        public const uint Bit23 = 0x800000;
        public const uint Bit24 = 0x1000000;
        public const uint Bit25 = 0x2000000;
        public const uint Bit26 = 0x4000000;
        public const uint Bit27 = 0x8000000;
        public const uint Bit28 = 0x10000000;
        public const uint Bit29 = 0x20000000;
        public const uint Bit30 = 0x40000000;
        public const uint Bit31 = 0x80000000;
    }
    #endregion bit.BITmask

    public static class Bit16
    {
        public const ushort Bit0   = 0x0001;
        public const ushort Bit1   = 0x0002;
        public const ushort Bit2   = 0x0004;
        public const ushort Bit3   = 0x0008;
        public const ushort Bit4   = 0x0010;
        public const ushort Bit5   = 0x0020;
        public const ushort Bit6   = 0x0040;
        public const ushort Bit7   = 0x0080;
        public const ushort Bit8   = 0x0100;
        public const ushort Bit9   = 0x0200;
        public const ushort Bit10  = 0x0400;
        public const ushort Bit11  = 0x0800;
        public const ushort Bit12  = 0x1000;
        public const ushort Bit13  = 0x2000;
        public const ushort Bit14  = 0x4000;
        public const ushort Bit15  = 0x8000;
    }

    public static class shrFlag
    {
        public const ushort Inp1 = Bit16.Bit15;
        public const ushort Inp2 = Bit16.Bit14;
        public const ushort Inp3 = Bit16.Bit13;
        public const ushort Inp4 = Bit16.Bit12;
        public const ushort TS2 = Bit16.Bit11;
        public const ushort TS1 = Bit16.Bit10;

        public const ushort T5 = Bit16.Bit9;
        public const ushort T6 = Bit16.Bit8;
        public const ushort T4 = Bit16.Bit7;
        public const ushort T3 = Bit16.Bit6;
        public const ushort T2 = Bit16.Bit5;
        public const ushort T1 = Bit16.Bit4;

        public const ushort VT4 = Bit16.Bit3;
        public const ushort VT3 = Bit16.Bit2;
        public const ushort VT1 = Bit16.Bit1;
        public const ushort VT2 = Bit16.Bit0;
    }

    #region маски флагов состояния АИН
    class mflag
    {
        public const uint pwm_On = Bit.Bit0;
        public const uint Vd_sat = Bit.Bit1;
        public const uint Vq_sat = Bit.Bit2;
        public const uint Id_sat = Bit.Bit3;

        public const uint Iq_sat = Bit.Bit4;
        public const uint Im_sat = Bit.Bit5;
        public const uint Irpm_sat = Bit.Bit6;
        public const uint kIqd = Bit.Bit7;

        public const uint Urms_sat = Bit.Bit8;
        public const uint epsi = Bit.Bit9;
        public const uint emstep = Bit.Bit10;
        public const uint Im = Bit.Bit11;

        public const uint Ltest = Bit.Bit12;
        public const uint sMax_On = Bit.Bit13;
        public const uint sRPM = Bit.Bit14;
        public const uint Gen_Engine_SW = Bit.Bit15;

    }
    #endregion маски флагов состояния АИН
}

