using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic
{
    // Обработчики панели "1ф 48→220" (gbGridTie).
    // Сами контролы живут в MainWindow.Designer.cs и правятся в конструкторе форм.
    public partial class Form1
    {
        public const ushort CmdGridMode = 630;
        public const ushort CmdGridRun = 631;
        public const ushort CmdIsetRef = 632;
        public const ushort CmdUrefRms = 633;
        public const ushort CmdTeleSel = 634;
        public const ushort CmdGridFreq = 624;

        // Наборы величин в словах 4..7 кадра 140. Те же номера в прошивке -
        // TELE_KOD/TELE_INS/TELE_RMS, my_Files/myDCAN.h.
        public const ushort TeleKod = 0;
        public const ushort TeleIns = 1;
        public const ushort TeleRms = 2;

        // Состояния автомата подключения, enum GridState в my_Files/grid_tie.h.
        private static readonly string[] GtStateName = { "OFF", "SYNC", "RAMP", "RUN", "FAULT" };

        // Последние посланные уставки: движок Gain1 в режиме Uref и Load1
        // в режиме Iset. Обратно из прибора они не приходят - в кадре 140 под
        // них нет слова, а ответ на команду 632/633 разбирают только окна
        // таблиц. Пишет Tbar_Send, читает *_Mode_Refresh.
        private decimal _urefSet = 220m;
        private decimal _isetSet;

        private void GridNud_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (sender == nudGridFreq) GridFreq_Commit(sender, EventArgs.Empty);
        }

        //-------------------------------------------------------------------
        // Выбор режима. Каждая кнопка сама доводит прибор до рабочего
        // состояния: отдельного "Пуска" нет, потому что промежуточное
        // состояние "режим выбран, но не запущен" оператору не нужно, а забыть
        // про него легко.
        //
        // Уставки живут на движках: Uref на Gain1, Iset на Load1. Кнопка
        // ставит движку нужный режим - иначе после переключения на панели
        // остался бы движок от прошлого режима, посылающий не ту команду.
        //-------------------------------------------------------------------
        private void ModeInv_Click(object sender, EventArgs e)
        {
            // Прошивка по этой команде сама делает grid_tie_stop(), gridMode = 0
            // и пересчитывает UrefRms -> sineB.gain.
            uint_128(0, 3333, 0, CmdGridMode);

            GridMode_Buttons(false);

            M1.gain.modeL.Text = @"U";
            M1.gain.modeH.Text = @"Uref";
            Gain_Mode_Refresh(M1); // выставляет масштаб и шлёт текущую уставку
        }

        private void ModeGrid_Click(object sender, EventArgs e)
        {
            uint_128(1, 3333, 0, CmdGridMode);

            GridMode_Buttons(true);

            M1.load.modeH.Text = @"Iset";
            Load_Mode_Refresh(M1); // масштаб + текущая уставка тока

            uint_128(2, 3333, 0, CmdGridRun); // GT_OFF -> GT_SYNC
        }

        private void GridStop_Click(object sender, EventArgs e)
        {
            uint_128(1, 3333, 0, CmdGridRun);
        }

        // Активный режим - жёлтым, как у кнопок режима движков.
        private void GridMode_Buttons(bool grid)
        {
            btnModeInv.BackColor = grid ? SystemColors.Control : Color.Yellow;
            btnModeGrid.BackColor = grid ? Color.Yellow : SystemColors.Control;
        }

        private void GridFreq_Commit(object sender, EventArgs e)
        {
            uint_128((ushort)nudGridFreq.Value, 3333, 0, CmdGridFreq);
        }

        //-------------------------------------------------------------------
        // Ключ телеметрии. Один и тот же слот кадра 140 показывает то коды
        // АЦП, то мгновенные значения, то rms - смотреть все три сразу незачем,
        // а слов в кадре нет лишних.
        //
        // Подпись меняем не сами: её ставит эхо ключа из слова 13, когда
        // прибор ответит. Иначе после потери связи метка врала бы о том, что
        // показывают четыре поля рядом.
        //-------------------------------------------------------------------
        private void TeleSel_DoubleClick(object sender, EventArgs e)
        {
            ushort next = (ushort)((ccs.teleSel + 1) % 3);
            uint_128(next, 3333, 0, CmdTeleSel);
        }

        private static string TeleSelName(ushort sel)
        {
            switch (sel)
            {
                case TeleKod: return "код";
                case TeleIns: return "мгн";
                default: return "rms";
            }
        }
    }
}
