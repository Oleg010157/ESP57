using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    //-------------------------------------------------------------------
    // Тест IGBT однофазного моста. На мост подают малое напряжение, ключи
    // открывают по одному или диагональю и смотрят ток по датчику первички:
    //
    //   плечо A: VT1 верх, VT2 низ        плечо B: VT3 верх, VT4 низ
    //   VT1+VT4 и VT3+VT2 - ток через первичку разных знаков
    //   один ключ - тока нет, если исправен второй ключ того же плеча
    //
    // Команда 338, прошивка - KSP/pwm_funcs.c, блок "ТЕСТ IGBT". Сквозной ток
    // (оба ключа плеча) не допускают обе стороны: здесь галочка соседа по
    // плечу заперта, а прибор такую маску не исполняет и закрывает мост.
    // Токовая защита компараторов в тесте работает как при пуске.
    //
    // ПРОВЕРКА ЗАЩИТЫ. Порог первички на время теста задаётся здесь же
    // (nudIlim) - ниже тока диагонали, и тогда защита срабатывает уже на
    // малом Udc. Прибор отвечает фактическим порогом из кодов ЦАП, а после
    // аварии - какая защёлка сработала (компаратор и знак) и ток первички
    // последнего отсчёта АЦП перед аварией. Знак сверяется с диагональю:
    // VT1+VT4 и VT3+VT2 должны давать разные защёлки.
    //
    // Галочки ставятся только из ответа прибора (data3 - открытые ключи и
    // флаги), нажатие лишь шлёт команду и красит контрол жёлтым до ACK.
    //-------------------------------------------------------------------
    public partial class AlStIgbtTest : Form
    {
        public Form1.DSend128 Send128;

        private const ushort Cmd      = 338;
        private const ushort CmdSend  = 0xA7A7; // открыть ключи по маске d2
        private const ushort CmdOn    = 0xA4A4; // войти в тест (и сброс после аварии)
        private const ushort CmdOff   = 0xA2A2; // выйти из теста
        private const ushort CmdState = 0xA0A0; // только спросить состояние
        private const ushort CmdIlim  = 0xA8A8; // порог первички на время теста, 0.1 А

        // Слово состояния в data3 ответа - Bridge_ManualState() в прошивке
        private const ushort StKeys = 0x000F;
        private const ushort StTest = 0x0100;
        private const ushort StTrip = 0x0200;
        private const int    StSrcShift = 10;   // биты 10..13: какая защёлка компаратора
        private static readonly string[] SrcNames = { "Iперв+", "Iперв−", "Iвтор+", "Iвтор−" };
        private const ushort LegA   = 0x0003; // VT1 | VT2
        private const ushort LegB   = 0x000C; // VT3 | VT4

        private readonly CheckBox[] _keys;
        private ushort _state;
        private double _tripI;      // ток первички перед аварией, А (data2 ответа)
        private Control _pending;   // ждёт ACK, горит жёлтым
        private ushort _pendingCmd;
        private int _pollDiv;

        public AlStIgbtTest()
        {
            InitializeComponent();
            _keys = new[] { cbVT1, cbVT2, cbVT3, cbVT4 };

            toolTip1.SetToolTip(btnTest,
                "Ручное управление ключами моста.\n" +
                "Включается только при выключенном мосте:\n" +
                "сначала снимите питание моста в главном окне.");
            toolTip1.SetToolTip(btnAllOff, "Закрыть все четыре ключа, тест остаётся включённым");
            toolTip1.SetToolTip(gbBridge,
                "Диагональ VT1+VT4 или VT3+VT2 даёт ток через первичку.\n" +
                "Оба ключа одного плеча открыть нельзя — это сквозной ток.");
            toolTip1.SetToolTip(nudIlim,
                "Порог компаратора тока первички на время теста.\n" +
                "Ставится ниже тока диагонали, чтобы проверить защиту при малом Udc.\n" +
                "0 — рабочий порог (из Imax). При выходе из теста рабочий возвращается сам.");
            toolTip1.SetToolTip(lblIlimFact,
                "Порог, пересчитанный прибором из кодов ЦАП компаратора.\n" +
                "С заданным расходится на шаг ЦАП и на ограничения диапазона.");

            Load += (s, e) =>
            {
                ApplyState(0, false);
                Send128(CmdState, 0, 0, Cmd); // вдруг тест остался включённым
                Send128(CmdIlim, IlimCode(), 0, Cmd); // и узнать действующий порог
                timerView.Start();
            };

            FormClosing += (s, e) =>
            {
                timerView.Stop();
                if ((_state & StTest) != 0)
                    Send128(CmdOff, 0, 0, Cmd); // ключи не оставляем открытыми
            };
        }

        //---------------------------------------------------------------
        // Ответ на 338, зовётся из Mbox13Rx (MainWindow.cs) в потоке UI.
        //---------------------------------------------------------------
        public void FeedBack(ushort d1, ushort d2, ushort d3)
        {
            if ((_pending != null) && (d1 == _pendingCmd))
            {
                _pending.BackColor = Color.Empty;
                _pending = null;
            }

            // data2: на порог - фактический порог, на остальное - ток
            // первички перед аварией. Оба в 0.1 А со знаком.
            double a = (short)d2 * 0.1;
            if (d1 == CmdIlim)
                lblIlimFact.Text = (nudIlim.Value == 0 ? "рабочий: " : "факт: ") + $@"{a:0.0} А";
            else
                _tripI = a;

            // Вход в тест не состоялся - мост работает, прибор отказал.
            bool refused = (d1 == CmdOn) && ((d3 & StTest) == 0);
            bool entered = (d1 == CmdOn) && ((d3 & StTest) != 0) && ((_state & StTest) == 0);

            ApplyState(d3, refused);

            // Порог теста прибор помнит, но входом в тест мы его подтверждаем:
            // ответ покажет, какой порог действует именно сейчас.
            if (entered)
                Send(nudIlim, CmdIlim, IlimCode());
        }

        private ushort IlimCode()
        {
            return (ushort)Math.Round(nudIlim.Value * 10);
        }

        private void nudIlim_ValueChanged(object sender, EventArgs e)
        {
            Send(nudIlim, CmdIlim, IlimCode());
        }

        private void ApplyState(ushort st, bool refused)
        {
            _state = st;

            bool test = (st & StTest) != 0;
            bool trip = (st & StTrip) != 0;
            bool live = test && !trip;

            for (int i = 0; i < _keys.Length; i++)
            {
                bool on = ((st >> i) & 1) != 0;
                _keys[i].Checked   = on;
                _keys[i].ForeColor = on ? Color.Red : SystemColors.ControlText;
            }

            // Открытый ключ запирает соседа по плечу
            cbVT1.Enabled = live && !cbVT2.Checked;
            cbVT2.Enabled = live && !cbVT1.Checked;
            cbVT3.Enabled = live && !cbVT4.Checked;
            cbVT4.Enabled = live && !cbVT3.Checked;
            btnAllOff.Enabled = live;

            btnTest.Text = !test ? "Вкл" : trip ? "Сброс" : "Выкл";

            lblState.ForeColor = (trip || refused) ? Color.Red : SystemColors.ControlText;
            if (refused)
                lblState.Text = "Прибор отказал: мост работает. Выключите питание моста в главном окне.";
            else if (!test)
                lblState.Text = "Тест выключен";
            else if (trip)
                lblState.Text = $@"Сработала защита: {TripSource(st)}. " +
                                $@"Ток перед аварией {_tripI:0.0} А. Ключи закрыты, «Сброс» — продолжить.";
            else if ((st & StKeys) == 0)
                lblState.Text = "Тест включён, все ключи закрыты";
            else
                lblState.Text = "Тест включён, ключи открыты";
        }

        private static string TripSource(ushort st)
        {
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < SrcNames.Length; i++)
                if (((st >> (StSrcShift + i)) & 1) != 0)
                    names.Add(SrcNames[i]);

            // Защёлки могли снять раньше, чем их прочитал обработчик
            return names.Count > 0 ? string.Join(", ", names) : "компаратор не определён";
        }

        private void Send(Control c, ushort d1, ushort d2)
        {
            if (_pending != null)
                _pending.BackColor = Color.Empty; // прежний ответ уже не ждём

            _pending    = c;
            _pendingCmd = d1;
            c.BackColor = Color.Yellow;
            Send128(d1, d2, 0, Cmd);
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            bool live = ((_state & StTest) != 0) && ((_state & StTrip) == 0);
            Send(btnTest, live ? CmdOff : CmdOn, 0);
        }

        private void btnAllOff_Click(object sender, EventArgs e)
        {
            Send(btnAllOff, CmdSend, 0);
        }

        private void cbKey_Click(object sender, EventArgs e)
        {
            var cb = (CheckBox)sender;
            int bit = Array.IndexOf(_keys, cb);
            ushort mask = (ushort)((_state & StKeys) ^ (1 << bit));

            // Сюда не пускает и Enabled, но маску проверяем ещё раз:
            // цена ошибки - сквозной ток.
            if (((mask & LegA) == LegA) || ((mask & LegB) == LegB))
                return;

            Send(cb, CmdSend, mask);
        }

        //---------------------------------------------------------------
        // Показания и опрос. Udc и ток первички - из кадра 140, который
        // идёт постоянно. Ток - в том наборе, что выбран ключом телеметрии
        // в главном окне (коды / мгновенные / rms); для теста нужен
        // мгновенный - только в нём виден знак тока диагонали.
        //
        // Раз в секунду, пока тест включён, спрашиваем состояние: о том,
        // что токовая защита закрыла ключи, прибор сам не сообщает.
        //---------------------------------------------------------------
        private void timerView_Tick(object sender, EventArgs e)
        {
            lblUdc.Text = $@"{ccs.UdcBat:0.0}";

            switch (ccs.teleSel)
            {
                case 0:
                    lblIpriName.Text = @"Iперв, код";
                    lblIpri.Text = $@"{ccs.iPri:0}";
                    break;
                case 1:
                    lblIpriName.Text = @"Iперв, А";
                    lblIpri.Text = $@"{ccs.iPri:0.00}";
                    break;
                default:
                    lblIpriName.Text = @"Iперв rms, А";
                    lblIpri.Text = $@"{ccs.iPri:0.00}";
                    break;
            }

            if ((++_pollDiv >= 2) && (_pending == null) && ((_state & StTest) != 0))
            {
                _pollDiv = 0;
                Send128(CmdState, 0, 0, Cmd);
            }
        }
    }
}
