using System.Drawing;

namespace ICDIBasic
{
    /// <summary>
    /// Один канал быстрой ленты: что это, в чём измеряется и как рисовать.
    /// </summary>
    internal sealed class F3Channel
    {
        public F3Channel(string name, string unit, float scale, bool signed,
                         Color color, bool shown)
        {
            Name = name;
            Unit = unit;
            Scale = scale;
            Signed = signed;
            Color = color;
            Shown = shown;
        }

        /// <summary>Имя канала - то же, что в прошивке и в настройках Wscope.</summary>
        public string Name { get; }

        /// <summary>Единица измерения, пустая строка для безразмерных.</summary>
        public string Unit { get; }

        /// <summary>Цена младшего разряда: физика = код * Scale.</summary>
        public float Scale { get; }

        /// <summary>Знаковый ли код в кадре. См. примечание о знаковости ниже.</summary>
        public bool Signed { get; }

        public Color Color { get; }

        /// <summary>Показывать сразу при открытии окна.</summary>
        public bool Shown { get; }
    }

    /*
     * Раскладка кадра 103, набор F3_convertor.
     *
     * ИСТОЧНИК ПРАВДЫ - ПРОШИВКА, my_Files/1ph_Sin.c, сборка кадра в
     * INT_myADCB_1_ISR. Таблица в grid_tie.txt разделе 6.2 - пересказ, и она уже
     * один раз отстала от кода: слово 14 там значится резервом, а прошивка с
     * 25.09.2026 отправляет там uOutCor.
     *
     * Читателей у раскладки теперь ЧЕТЫРЕ, и совпадать должны все: прошивка,
     * разбор в Oleg/Kadr60.cs (ProcessFrame103), настройки каналов Wscope
     * (bin/Wscope/F3_convertor.ini) и эта таблица. Имён в кадре нет, читается он
     * по НОМЕРУ СЛОВА - расхождение проявится не ошибкой, а молча неверными
     * числами на графике.
     *
     * МАСШТАБЫ ЗАШИТЫ И ТУТ ТОЖЕ, ровно по той причине, по которой они зашиты в
     * прошивке: масштаб, который покрутили в отладчике и забыли, даёт на графике
     * правдоподобную ложь - худший вид ошибки измерения.
     *
     * ЗНАКОВОСТЬ ПОКАНАЛЬНАЯ, на кадр целиком её задать нельзя. Слово 9 (dacb) -
     * сырые коды АЦП 0..4095, беззнаковые; остальные знаковые. Беззнаковый
     * разбор знакового канала превращает мелкое отрицательное значение в число
     * под 65535, и график встаёт частоколом до потолка - выглядит как настоящая
     * помеха в сигнале. На этом уже наступали, см. grid_tie.txt 6.2.
     */
    internal static class F3Channels
    {
        /// <summary>Каналов в кадре: слова 0..14. Последнее слово - такт, оно не канал.</summary>
        public const int Count = 15;

        /// <summary>
        /// Канал синхронизации развёртки - uGrid. Всё в этом приборе привязано к
        /// сети, синхронизация по любому другому каналу дала бы ту же точку,
        /// только через лишнее звено.
        /// </summary>
        public const int Trig = 1;

        /// <summary>
        /// Номинальная частота такта управления, Гц. Прерывание ADCB INT1,
        /// my_Files/grid_tie.h. Настоящая измеряется по такту - см. ScopeRing.
        /// </summary>
        public const float NominalHz = 5000f;

        /*
         * Цвета сгруппированы по смыслу: напряжения - холодные, токи - тёплые,
         * безразмерные - сиреневые и серые. На пятнадцати каналах полностью
         * различимых цветов не набрать, и это терпимо: сразу показываются три,
         * у каждой кривой рядом стоит подпись с масштабом, а кнопки каналов
         * покрашены теми же цветами, что и линии.
         */
        public static readonly F3Channel[] All =
        {
            /*  0 */ new F3Channel("phaseErr", "",     1e-4f,  true,  Color.FromArgb(121, 134, 203), false),
            /*  1 */ new F3Channel("uGrid",    "В",    0.1f,   true,  Color.FromArgb(100, 181, 246), true),
            /*  2 */ new F3Channel("iref",     "А",    0.01f,  true,  Color.FromArgb(255, 213,  79), true),
            /*  3 */ new F3Channel("iSec",     "А",    0.01f,  true,  Color.FromArgb(129, 199, 132), true),
            /*  4 */ new F3Channel("iPri",     "А",    0.01f,  true,  Color.FromArgb(255, 138, 101), false),
            /*  5 */ new F3Channel("m",        "",     1e-4f,  true,  Color.FromArgb(186, 104, 200), false),
            /*  6 */ new F3Channel("duty",     "",     1e-4f,  true,  Color.FromArgb(158, 158, 158), false),
            /*  7 */ new F3Channel("uBat",     "В",    0.1f,   true,  Color.FromArgb(229, 115, 115), false),
            /*  8 */ new F3Channel("mDC",      "",     1e-5f,  true,  Color.FromArgb(236,  64, 122), false),
            /*  9 */ new F3Channel("dacb",     "коды", 1f,     false, Color.FromArgb(161, 136, 127), false),
            /* 10 */ new F3Channel("uSec",     "В",    0.1f,   true,  Color.FromArgb( 38, 198, 218), false),
            /* 11 */ new F3Channel("iBat",     "А",    0.01f,  true,  Color.FromArgb(212, 225,  87), false),
            /* 12 */ new F3Channel("iMag",     "А",    0.01f,  true,  Color.FromArgb(255, 183,  77), false),
            /* 13 */ new F3Channel("p",        "Вт",   1f,     true,  Color.FromArgb( 77, 182, 172), false),
            /* 14 */ new F3Channel("uOutCor",  "В",    0.1f,   true,  Color.FromArgb(144, 164, 174), false),
        };

        /// <summary>
        /// Сколько слов принёс кадр. Ровно та же проверка, что в
        /// ProcessFrame103: со старой прошивкой слов двенадцать, и читать
        /// шестнадцать значит показать мусор из хвоста 64-байтного буфера как
        /// измерение.
        /// </summary>
        public static int WordsIn(byte dlc)
        {
            return dlc >= 13 ? 16 : 12;
        }

        /// <summary>
        /// Сколько каналов в кадре настоящие. Такт ОБЯЗАН быть последним словом,
        /// поэтому каналов всегда на один меньше, чем слов.
        /// </summary>
        public static int ChannelsIn(int words)
        {
            int n = words - 1;
            return n > Count ? Count : n;
        }

        /// <summary>
        /// Разобрать кадр: <paramref name="Count"/> значений в физике кладутся в
        /// <paramref name="dst"/> начиная с <paramref name="at"/>, возвращается
        /// счётчик тактов.
        ///
        /// Каналов, которых в кадре нет, кладём нули - как и ProcessFrame103.
        /// Чтобы нуль не прочитали как измерение, окно гасит и отключает кнопки
        /// этих каналов, см. ScopeForm.
        /// </summary>
        public static ushort Decode(byte[] d, int words, float[] dst, int at)
        {
            int live = ChannelsIn(words);

            for (int c = 0; c < live; c++)
            {
                int raw = (d[c * 2 + 1] << 8) | d[c * 2];

                F3Channel ch = All[c];
                if (ch.Signed) raw = (short)raw;

                dst[at + c] = raw * ch.Scale;
            }

            for (int c = live; c < Count; c++)
                dst[at + c] = 0f;

            // Такт - последнее слово кадра, и место у него не произвольное:
            // Motor_Control считает запись в файл готовой именно по изменению
            // последнего слова (ASt_MainF.cs, CAN_RxData). Здесь он нужен для
            // другого - по разрывам в нём видны пропущенные кадры.
            int t = words - 1;
            return (ushort)((d[t * 2 + 1] << 8) | d[t * 2]);
        }
    }
}
