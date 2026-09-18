using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic
{
    // Обработчики панели "1ф 48→220" (gbGridTie).
    // Сами контролы живут в MainWindow.Designer.cs и правятся в конструкторе форм.
    public partial class Form1
    {
        public const ushort CmdGridTrack = 630; // слежение за напряжением сети
        public const ushort CmdGridConn = 631;  // запрос на контактор
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
        //   _track  - слежение за сетью (ACK 630)
        //   _gridEn - разрешение выхода в сеть, галочка Enable (ACK 635)
        //   _conn   - запрос на контактор принят (ACK 631)
        //   _power  - ключи АИН-1 включены (ACK 10 и 12, слово d3)
        private ushort _track;
        private ushort _gridEn;
        private ushort _conn;
        private ushort _power;

        private void GridNud_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (sender == nudGridFreq) GridFreq_Commit(sender, EventArgs.Empty);
        }

        //-------------------------------------------------------------------
        // Управление разбито на четыре независимые вещи, потому что это четыре
        // разные вещи, и раньше они были свалены в две кнопки:
        //
        //   btnInvPower   - ЖИВО ЛИ устройство. Ключи АИН-1, и только они.
        //                   Индикатор AIN1_flag: зелёный = ключи включены.
        //   btnGridTrack  - ВЕДЁМ ЛИ синус за сетью. При включённом слежении
        //                   мост держит клон сети (частота, фаза, амплитуда)
        //                   вхолостую, контактор разомкнут: на наладке это
        //                   единственный способ сверить форму осциллографом.
        //                   При выключенном - независимый источник, частоту
        //                   задаёт nudGridFreq, амплитуду - движок Uref.
        //   AIN1_Enable   - РАЗРЕШЕНО ЛИ в сеть. Блокировка, не команда:
        //                   поднятая галочка сама ничего не замыкает.
        //   btnGridConn   - ЗАМЫКАЕМ ЛИ контактор. Работает только когда
        //                   слежение включено и разрешение поднято.
        //
        // Раньше "Пуск" одним нажатием и включал ключи, и уводил мост в сеть,
        // а промежуточное состояние - синус уже ведётся, контактор ещё
        // разомкнут - назвать было нечем, хотя вся наладка идёт именно в нём.
        //-------------------------------------------------------------------
        private void InvPower_Click(object sender, EventArgs e)
        {
            btnInvPower.BackColor = Color.Yellow;

            if (_power == 0)
            {
                uint_128(0, 3333, 0, CmdAinDisable); // снять запрет блока
                uint_128(0, 0, 0, CmdPwm1On);       // собственно ключи
            }
            else
            {
                uint_128(1, 3333, 0, CmdAinDisable); // IGBT1_Disable

                // Уставку в ноль: иначе следующее включение началось бы сразу
                // с полного тока или полного напряжения, чего оператор от
                // кнопки питания не ждёт.
                GridSetpoint_Zero();
            }
        }

        private void GridTrack_Click(object sender, EventArgs e)
        {
            btnGridTrack.BackColor = Color.Yellow;
            uint_128((ushort)(_track == 0 ? 1 : 0), 3333, 0, CmdGridTrack);
        }

        private void GridConn_Click(object sender, EventArgs e)
        {
            btnGridConn.BackColor = Color.Yellow;
            uint_128((ushort)(_conn == 0 ? 2 : 1), 3333, 0, CmdGridConn);
        }

        private void GridSetpoint_Zero()
        {
            if (M1.gain.modeH.Text == @"Uref") M1.gain.tbar.Value = 0;
            if (M1.load.modeH.Text == @"Iset") M1.load.tbar.Value = 0;
        }

        //-------------------------------------------------------------------
        // ACK 10 и 12: d3 - фактическое состояние ключей (motors[0].pwm_On).
        // Индикатор красит прибор, а не нажатие: жёлтая кнопка гаснет только
        // когда ключи действительно перешли в новое состояние.
        //-------------------------------------------------------------------
        private void InvPower_Ack(ushort d3)
        {
            _power = (ushort)(d3 == 0 ? 0 : 1);

            btnInvPower.BackColor = Color.Empty;
            btnInvPower.ForeColor = Color.Green;
            btnInvPower.Text = _power == 0 ? @"Выкл." : @"Вкл.";

            AIN1_flag.BackColor = _power == 0
                ? Color.Red
                : Color.FromArgb(0, 192, 0);
            AIN1_flag.Text = _power == 0 ? @"Выкл" : @"Вкл.";
        }

        //-------------------------------------------------------------------
        // ACK 630: слежение пришло из прибора. Надпись и движок ставим здесь,
        // а не по нажатию - иначе кнопка показывала бы намерение оператора,
        // а не то, в чём прибор на самом деле.
        //-------------------------------------------------------------------
        private void GridTrack_Ack(ushort d1)
        {
            _track = (ushort)(d1 == 0 ? 0 : 1);

            btnGridTrack.BackColor = Color.Empty;
            btnGridTrack.ForeColor = Color.Green;
            btnGridTrack.Text = _track == 0 ? @"Слежение выкл" : @"Слежение вкл";

            // Выключенное слежение снимает и запрос на контактор: в прошивке
            // это делает grid_tie_stop(), и GUI обязан сойтись с ней, не дожидаясь
            // отдельного ответа на 631 - иначе кнопка врёт, что мост в сети.
            if (_track == 0) _conn = 0;

            // Частоту задаёт оператор только у независимого источника. При
            // слежении её диктует сеть, и поле обязано это показывать, иначе
            // введённое число молча ничего не изменит.
            nudGridFreq.Enabled = _track == 0;

            // Движок под уставку этого режима: Uref на Gain1, Iset на Load1.
            // Иначе на панели остался бы движок от прошлого режима, шлющий
            // не ту команду.
            if (_track == 0)
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

            GridConn_Enable();
        }

        //-------------------------------------------------------------------
        // ACK 635: разрешение выхода в сеть (галочка Enable).
        //-------------------------------------------------------------------
        private void GridEn_Ack(ushort d1)
        {
            _gridEn = (ushort)(d1 == 0 ? 0 : 1);

            AIN1_Enable.BackColor = Color.Empty;
            AIN1_Enable.Checked = _gridEn == 1;

            // Снятое разрешение обнуляет запрос и в прошивке (case 635) - здесь
            // повторяем то же самое, чтобы надпись на кнопке не расходилась
            // с прибором до следующего ответа на 631.
            if (_gridEn == 0) _conn = 0;

            GridConn_Enable();
        }

        //-------------------------------------------------------------------
        // ACK 631: d1 - принят ли запрос на контактор, d3 - состояние автомата.
        //
        // Прибор может запрос и отклонить (нет слежения, нет разрешения) -
        // тогда d1 придёт нулём, и кнопка честно вернётся в исходное.
        //-------------------------------------------------------------------
        private void GridConn_Ack(ushort d1, ushort d3)
        {
            _conn = (ushort)(d1 == 0 ? 0 : 1);

            btnGridConn.BackColor = Color.Empty;
            btnGridConn.ForeColor = Color.Green;
            GridConn_Text(d3);
        }

        // Надпись по состоянию автомата. Зовётся и из ACK, и из разбора кадра
        // 60: переход SYNC -> RAMP -> RUN происходит сам, без ответа на команду.
        private void GridConn_Refresh()
        {
            GridConn_Text(ccs.gtState);
        }

        private void GridConn_Text(ushort state)
        {
            if (btnGridConn.BackColor == Color.Yellow) return; // ждём ответа

            // Пока автомат не довёл дело до RAMP/RUN, контактор ещё разомкнут:
            // мост набирает синхронизм. Показываем именно это, а не "в сети".
            bool inGrid = (state >= 2) && (state <= 3); // GT_RAMP, GT_RUN

            string text = _conn == 0
                ? @"Выход в сеть"
                : (inGrid ? @"Отключить" : @"Ждём синхр.");

            if (btnGridConn.Text != text) btnGridConn.Text = text;
        }

        // Замыкать контактор без слежения и без разрешения нельзя, и кнопка
        // не должна делать вид, что можно.
        private void GridConn_Enable()
        {
            btnGridConn.Enabled = (_track == 1) && (_gridEn == 1);
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
