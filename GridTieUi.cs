using System;
using System.Drawing;
using System.Windows.Forms;
using FlagMask; // mflag.pwm_On - бит ключей в слове 14 кадра 140

namespace ICDIBasic
{
    // Обработчики панели "1ф 48→220" (gbGridTie).
    // Сами контролы живут в MainWindow.Designer.cs и правятся в конструкторе форм.
    public partial class Form1
    {
        public const ushort CmdGridMode = 630;  // режим моста: АИН / слежение / сеть
        public const ushort CmdIsetRef = 632;
        public const ushort CmdUrefRms = 633;
        public const ushort CmdTeleSel = 634;
        public const ushort CmdGridEn = 635;    // разрешение выхода в сеть
        public const ushort CmdGridFreq = 624;
        public const ushort CmdAinDisable = 10; // ключи АИН-1: 1 = запретить
        public const ushort CmdPwm1On = 12;     // ключи АИН-1: включить

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

        // Состояние однофазного моста, каким его ПОДТВЕРДИЛ прибор. Все три
        // поля ставят только ACK-и, никогда - нажатия: кнопка показывает то,
        // в чём прибор, а не то, чего захотел оператор.
        //
        //   _mode   - режим моста, GridModeOff/Sync/Grid (ACK 630)
        //   _gridEn - разрешение выхода в сеть, галочка Enable (ACK 635)
        private ushort _mode;
        private ushort _gridEn;

        // Режимы моста. Те же номера в прошивке - GRID_MODE_* в grid_tie.h.
        public const ushort GridModeOff = 0;  // АИН, автономный инвертор напряжения
        public const ushort GridModeSync = 1; // слежение, контактор разомкнут
        public const ushort GridModeGrid = 2; // выход в сеть

        private static readonly string[] GridModeName = { "АИН", "Слежение", "Выход в сеть" };

        private void GridNud_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (sender == nudGridFreq) GridFreq_Commit(sender, EventArgs.Empty);
        }

        //-------------------------------------------------------------------
        // Три органа управления, и каждый отвечает ровно за одно:
        //
        //   btnInvPower  - ЖИВО ЛИ устройство. Ключи АИН-1, и только они.
        //                  Кнопка сама себе индикатор: надпись Вкл/Выкл, фон
        //                  зелёный/пустой. Отдельное поле-флаг рядом было бы
        //                  вторым местом правды о том же самом.
        //   btnGridMode  - НАСКОЛЬКО СВЯЗАНЫ С СЕТЬЮ. Три положения по кругу:
        //                  АИН -> Слежение -> Выход в сеть. Текст и есть
        //                  состояние, как у ModeH на движках.
        //   AIN1_Enable  - РАЗРЕШЕНО ЛИ в сеть. Блокировка, не команда:
        //                  поднятая галочка сама ничего не замыкает, она лишь
        //                  пропускает режим "Выход в сеть".
        //
        // Режимы лежат на одной оси, поэтому это одна кнопка и одна команда,
        // а не три флага:
        //
        //   АИН          - автономный инвертор напряжения. Частота своя
        //                  (nudGridFreq), амплитуда своя (движок Uref).
        //   Слежение     - синус ведётся за сетью по частоте, фазе и
        //                  амплитуде, контактор РАЗОМКНУТ. Наладочный режим:
        //                  только в нём форму можно сверить осциллографом.
        //   Выход в сеть - то же плюс замкнутый контактор и токовый контур.
        //-------------------------------------------------------------------
        private void InvPower_Click(object sender, EventArgs e)
        {
            if (InvPower_On())
            {
                uint_128(1, 3333, 0, CmdAinDisable); // IGBT1_Disable

                // Уставку в ноль: иначе следующее включение началось бы сразу
                // с полного тока или полного напряжения, чего оператор от
                // кнопки питания не ждёт.
                GridSetpoint_Zero();
            }
            else
            {
                uint_128(0, 3333, 0, CmdAinDisable); // снять запрет блока
                uint_128(0, 0, 0, CmdPwm1On);        // собственно ключи
            }
        }

        // Ключи включены? Единственный источник правды - бит pwm_On в слове 14
        // кадра 140 (REF-кадр, KSP/REF_Kadr.c). Кэшировать его в поле незачем:
        // кадр идёт постоянно, а своя копия неизбежно разошлась бы с прибором
        // после аварийного закрытия ключей, о котором GUI никто не спрашивал.
        private bool InvPower_On()
        {
            return (M1.ccs.flags & mflag.pwm_On) != 0;
        }

        private void GridMode_Click(object sender, EventArgs e)
        {
            btnGridMode.BackColor = Color.Yellow;

            // По кругу. Через "Выход в сеть" перешагиваем, если разрешение не
            // поднято: иначе кнопка молча упиралась бы в отказ прибора.
            ushort next = (ushort)((_mode + 1) % 3);
            if ((next == GridModeGrid) && (_gridEn == 0)) next = GridModeOff;

            uint_128(next, 3333, 0, CmdGridMode);
        }

        private void GridSetpoint_Zero()
        {
            if (M1.gain.modeH.Text == @"Uref") M1.gain.tbar.Value = 0;
            if (M1.load.modeH.Text == @"Iset") M1.load.tbar.Value = 0;
        }

        //-------------------------------------------------------------------
        // ACK 630: режим пришёл из прибора. Надпись и движок ставим здесь, а не
        // по нажатию - иначе кнопка показывала бы намерение оператора, а не то,
        // в чём прибор на самом деле. Прибор может режим и не принять (нет
        // разрешения) - тогда придёт прежний, и кнопка честно вернётся.
        //-------------------------------------------------------------------
        private void GridMode_Ack(ushort d1)
        {
            _mode = d1 < GridModeName.Length ? d1 : GridModeOff;

            btnGridMode.BackColor = Color.Empty;
            btnGridMode.ForeColor = Color.Green;
            btnGridMode.Text = GridModeName[_mode];

            // Частоту задаёт оператор только у автономного инвертора. В двух
            // верхних режимах её диктует сеть, и поле обязано это показывать,
            // иначе введённое число молча ничего не изменит.
            nudGridFreq.Enabled = _mode == GridModeOff;

            // Движок под уставку этого режима: Uref на Gain1, Iset на Load1.
            // Иначе на панели остался бы движок от прошлого режима, шлющий
            // не ту команду.
            if (_mode == GridModeOff)
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

        //-------------------------------------------------------------------
        // ACK 635: разрешение выхода в сеть (галочка Enable).
        //-------------------------------------------------------------------
        private void GridEn_Ack(ushort d1)
        {
            _gridEn = (ushort)(d1 == 0 ? 0 : 1);

            AIN1_Enable.BackColor = Color.Empty;
            AIN1_Enable.Checked = _gridEn == 1;

            // Снятое разрешение роняет мост из "Выход в сеть" в "Слежение" -
            // это делает прошивка (case 635). GUI сходится с ней сразу, не
            // дожидаясь ответа на 630, иначе кнопка врёт, что мост в сети.
            if ((_gridEn == 0) && (_mode == GridModeGrid))
            {
                _mode = GridModeSync;
                btnGridMode.Text = GridModeName[_mode];
            }
        }

        //-------------------------------------------------------------------
        // Живая часть, из meterTimer_Tick: кнопка питания и авария моста.
        //
        // Оба берутся из кадра 140 (REF-кадр) - он идёт постоянно, в отличие
        // от кадра 60, который приходит только по F2. Ответов на команды тут
        // ждать нечего: ключи может закрыть защита, а автомат уходит из
        // GT_SYNC в GT_RUN сам, и ни о том, ни о другом GUI не спрашивал.
        //-------------------------------------------------------------------
        private void GridUi_Refresh()
        {
            bool on = InvPower_On();

            string text = on ? @"Вкл" : @"Выкл";
            if (btnInvPower.Text != text) btnInvPower.Text = text;

            Color back = on ? Color.FromArgb(0, 192, 0) : Color.Empty;
            if (btnInvPower.BackColor != back) btnInvPower.BackColor = back;

            // Авария автомата гасит зелень режима: в GT_FAULT мост закрыт, и
            // надпись "Выход в сеть" без этого обещала бы работу, которой нет.
            Color mode = (ccs.gtState == 4) ? Color.Red : Color.Empty;
            if (btnGridMode.BackColor != Color.Yellow && btnGridMode.BackColor != mode)
                btnGridMode.BackColor = mode;
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
