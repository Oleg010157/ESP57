namespace ICDIBasic
{
    public static class Yokogawa
    {

        public static ushort Update_count;
        public static float UrmsA;
        public static float UrmsAa;
        public static float UrmsB;
        public static float IrmsA;
        public static float IrmsB;
        public static float PaA;
        public static float PaB;
        public static float SΣA;
        public static float SΣB;
        public static float LambdaA;
        public static float LambdaB;
        public static float fI1;
        public static float fI3;
        public static float Speed;
        public static float Torque;     // измеренный муфтой M40F крутящий момент в Н*м
        public static float fU5;       // измеренная частота напряжения входа элемента 5 в Гц
        public static float F1;     //  F1 -> P-loss потери в двигателях спарки, Watt
        public static float F2;     //  F2 - расчитанная механическая мощность на валу Watt
        public static float F15;     // F15 - расчитанный момент на валу  н*м
        public static int[] ints;
        public static int[] ints2;
        public static float P1;
        public static float P2;
        public static float P3;
        public static float P4;
    }

    static class Yregs     // этот класс содержит адресса modbus регистров Yokogawa 
    {
        public static int Update_count = 0;

        // Wiring unit ΣA normal measurement data and delta computation data
        public static int UrmsA = 1000;
        public static int IrmsA = 1002;   // IrmsΣA True rms current of ΣA
        public static int PaA = 1004;       // PΣA   Active power of ΣA
        public static int SΣA = 0x3ee;       // Apparent power of ΣA
        public static int LambdaA = 1010;  //  LambdaΣA   Power factor of ΣA (λ)
        public static int fI1 = 116;  // Current 1 frequency

        // Wiring unit ΣB normal measurement data and delta computation data
        public static int UrmsB = 1100;
        public static int IrmsB = 1102;   // IrmsΣA True rms current of ΣA
        public static int PaB = 1104;       // PΣA   Active power of ΣA
        public static int SΣB = 0x452;       // Apparent power of ΣA
        public static int LambdaB = 1110;  //  LambdaΣA   Power factor of ΣA (λ)
        public static int fI3 = 316;  // Current 1 frequency

        // Motor input measurement data

        public static int Torgue = 1502;  //  F20  Torque расчитанный по частоте с выхода муфты
        public static int fU5 = 514;    // частота с выхода датчика момента T40F
        public static int Speed = 1500;   // Rotating speed  rpm ( с выхода датчика  T40F)

        // User-Defined Functions
        public static int F1 = 10;  //  F2 - расчитанная механическая мощность на валу
        public static int F2 = 12;  //  F2 - расчитанная механическая мощность на валу
        public static int F15 = 38;  //  F15 - расчитанный момент на валу  н*м
        public static int P2_97 = 514;    // F18 расчитанная механическая мощность на валу  P2_97 = P2*0.97

        //Element 1 normal measurement data

        public static int P1 = 0x68;  // активная мощность измеренная 1-ым элементом 
        public static int P2 = 0xcc;  // активная мощность измеренная 1-ым элементом 
        public static int P3 = 0x130;  // активная мощность измеренная 1-ым элементом 
        public static int P4 = 0x194;  // активная мощность измеренная 1-ым элементом 
    }
}