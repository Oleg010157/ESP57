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

        // Последний ПОДТВЕРЖДЁННЫЙ прибором режим: 0 = остров, 1 = сеть.
        // Ставит только ACK 630 - от него зависит и надпись на кнопке, и то,
        // что сделает "Пуск". Нажатие само по себе режима не меняет.
        private ushort _gridMode;

        private void GridNud_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (sender == nudGridFreq) GridFreq_Commit(sender, EventArgs.Empty);
        }

        //-------------------------------------------------------------------
        // Управление разделено на две вещи, потому что это две разные вещи:
        //
        //   btnGridMode  - ЧТО делаем: остров (синус Uref) или сеть (ток Iset).
        //                  Одна кнопка с меняющейся надписью, как ModeH у
        //                  движков: текст и есть состояние.
        //   Пуск / Стоп  - ДЕЛАЕМ ЛИ. Ключи и автомат сети.
        //
        // Раньше ключи жили в двух других местах (галочка Enable и двойной
        // клик по "Вкл."), и по кнопке режима было не понять, поедет мост или
        // нет. Теперь "Пуск" доводит до работы сам.
        //-------------------------------------------------------------------
        private void GridMode_Click(object sender, EventArgs e)
        {
            btnGridMode.BackColor = Color.Yellow;
            uint_128((ushort)(_gridMode == 0 ? 1 : 0), 3333, 0, CmdGridMode);
        }

        private void GridStart_Click(object sender, EventArgs e)
        {
            btnGridStart.BackColor = Color.Yellow;

            uint_128(0, 0, 0, 10); // снять запрет блока (ACK поставит галочку Enable)
            uint_128(0, 0, 0, 12); // PWM1_On - собственно ключи

            // Автомат синхронизации нужен только сетевому режиму. В острове
            // мост поехал уже от одних ключей: там синус берётся из таблицы,
            // а не из контура.
            if (_gridMode == 1)
                uint_128(2, 3333, 0, CmdGridRun);
        }

        private void GridStop_Click(object sender, EventArgs e)
        {
            btnGridStop.BackColor = Color.Yellow;

            uint_128(1, 3333, 0, CmdGridRun); // GT_OFF, заодно снимает аварию
            uint_128(1, 0, 0, 10);            // IGBT1_Disable

            // Уставку в ноль: иначе следующий "Пуск" начался бы сразу с
            // полного тока или полного напряжения, чего оператор от кнопки
            // "Пуск" не ждёт.
            GridSetpoint_Zero();
        }

        private void GridSetpoint_Zero()
        {
            if (M1.gain.modeH.Text == @"Uref") M1.gain.tbar.Value = 0;
            if (M1.load.modeH.Text == @"Iset") M1.load.tbar.Value = 0;
        }

        //-------------------------------------------------------------------
        // ACK 630: режим пришёл из прибора. Надпись и движок ставим здесь,
        // а не по нажатию - иначе кнопка показывала бы намерение оператора,
        // а не то, в чём прибор на самом деле.
        //-------------------------------------------------------------------
        private void GridMode_Ack(ushort d1)
        {
            _gridMode = (ushort)(d1 == 0 ? 0 : 1);

            btnGridMode.BackColor = Color.Empty;
            btnGridMode.ForeColor = Color.Green;
            btnGridMode.Text = _gridMode == 0 ? @"Инвертор" : @"Сеть";

            // Движок под уставку этого режима: Uref на Gain1, Iset на Load1.
            // Иначе на панели остался бы движок от прошлого режима, шлющий
            // не ту команду.
            if (_gridMode == 0)
            {
                M1.gain.modeL.Text = @"U";
                M1.gain.modeH.Text = @"Uref";
                Gain_Mode_Refresh(M1);
            }
            else
            {
                M1.load.modeH.Text = @"Iset";
                Load_Mode_Refresh(M1);
            }
        }

        private void GridFreq_Commit(object sender, EventArgs e)
        {
            nudGridFreq.BackColor = Color.Yellow;
            uint_128((ushort)nudGridFreq.Value, 3333, 0, CmdGridFreq);
        }

        //-------------------------------------------------------------------
        // Ключ телеметрии. Один и тот же слот кадра 140 показывает то коды
        // АЦП, то мгновенные значения, то rms - смотреть все три сразу незачем,
        // а слов в кадре нет лишних.
        //
        // Подпись меняет ACK, а не щелчок: пока прибор не ответил, метка не
        // должна обещать того, чего в четырёх полях рядом нет.
        //-------------------------------------------------------------------
        private void TeleSel_DoubleClick(object sender, EventArgs e)
        {
            I1rms_selector.BackColor = Color.Yellow;
            uint_128((ushort)((ccs.teleSel + 1) % 3), 3333, 0, CmdTeleSel);
        }

        private void TeleSel_Ack(ushort d1)
        {
            I1rms_selector.BackColor = Color.Empty;
            I1rms_selector.ForeColor = Color.Green;
            I1rms_selector.Text = TeleSelName(d1);
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
