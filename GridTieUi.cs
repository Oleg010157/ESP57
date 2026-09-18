using System;
using System.Drawing;
using System.Windows.Forms;
using FlagMask; // mflag.pwm_On - бит ключей в слове 14 кадра 140

namespace ICDIBasic
{
    // Обработчики строки управления однофазным мостом: режим, уставка
    // напряжения, частота. Своей рамки у неё больше нет - органы лежат прямо
    // в groupBox_AIN1: рамка с подписью "1ф 48→220" съедала полтора десятка
    // точек по высоте и налезала на строку показаний U220/Usec под ней.
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

        // Что сейчас НАРИСОВАНО на кнопке питания. Не состояние прибора -
        // состояние живёт в бите pwm_On, - а защёлка против перерисовки на
        // каждом тике таймера: подсказку нельзя переставлять без нужды, иначе
        // она гаснет прямо под курсором. null = кнопку ещё не рисовали, и
        // первый же тик обязан её оформить, даже если надпись из дизайнера
        // случайно совпала.
        private bool? _invShown;

        // Режимы моста. Те же номера в прошивке - GRID_MODE_* в grid_tie.h.
        public const ushort GridModeOff = 0;  // АИН, автономный инвертор напряжения
        public const ushort GridModeSync = 1; // слежение, контактор разомкнут
        public const ushort GridModeGrid = 2; // выход в сеть

        private static readonly string[] GridModeName = { "АИН", "Слежение", "Выход в сеть" };

        // Обычный фон кнопок моста.
        //
        // Color.Empty здесь НЕ ГОДИТСЯ, хотя в остальном окне он и означает
        // "ничего особенного". BackColor - амбиентное свойство: пустое значение
        // значит не "цвет кнопки по умолчанию", а "наследовать от родителя".
        // Родитель здесь - groupBox_AIN1 с DarkKhaki, и кнопка от такого сброса
        // просто растворяется в фоне группы.
        private static readonly Color BtnIdle = SystemColors.ControlLight;

        // Подсказка к каждому положению. На кнопке помещается только короткое
        // имя, а "АИН" без расшифровки не говорит ничего - и не всякому
        // очевидно, что слежение от выхода в сеть отличается одним контактором.
        private static readonly string[] GridModeHint =
        {
            "Автономный инвертор напряжения.\n" +
            "Частота своя (поле F Hz), амплитуда своя (движок Uref).\n" +
            "За сетью не следим, контактор разомкнут.",

            "Слежение за напряжением сети.\n" +
            "Синус ведётся за сетью по частоте, фазе и амплитуде,\n" +
            "но контактор РАЗОМКНУТ и ток в сеть не идёт.\n" +
            "Наладочный режим: форму можно сверить осциллографом.",

            "Выход в сеть.\n" +
            "То же слежение плюс замкнутый контактор и токовый контур.\n" +
            "Пускает только галочка \"Enable сеть\"."
        };

        // Подсказка к кнопке питания. Надпись на ней - СОСТОЯНИЕ ("Выкл" - это
        // не приказ, а то, в чём ключи сейчас), и по-русски она одинаково
        // читается как состояние и как команда. Подсказка разводит одно с
        // другим: первая строка - что есть, вторая - что сделает нажатие.
        private static readonly string[] InvPowerHint =
        {
            "Ключи АИН-1 закрыты, моста на выходе нет.\n" +
            "Нажать - включить ключи.",

            "Ключи АИН-1 открыты, мост работает.\n" +
            "Нажать - выключить ключи.\n" +
            "Выключение заодно сбрасывает уставку в ноль,\n" +
            "чтобы следующий пуск не начался с полного напряжения."
        };

        private void GridNud_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (sender == nudGridFreq) GridFreq_Commit(sender, EventArgs.Empty);
            if (sender == nudGridUref) GridUref_Commit(sender, EventArgs.Empty);
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

        // Подсказка идёт за надписью: на кнопке помещается короткое имя, в
        // подсказке - что оно значит. Зовётся отовсюду, где меняется _mode.
        private void GridMode_Hint()
        {
            toolTip1.SetToolTip(btnGridMode, GridModeHint[_mode]);
            toolTip1.SetToolTip(lblGridMode, GridModeHint[_mode]);
        }

        //-------------------------------------------------------------------
        // Подсказки к двум уставкам панели. Ставятся один раз при запуске -
        // в отличие от подсказки к кнопке режима, они от состояния не зависят.
        //
        // Подпись получает ту же подсказку, что и поле: наводят курсор на то,
        // что непонятно, а непонятно обычно как раз название.
        //-------------------------------------------------------------------
        private void GridTie_Hints()
        {
            const string uref =
                "Действующее напряжение на выходе моста, В rms.\n" +
                "\n" +
                "То же число, что на движке Gain1 в режиме Uref - это два вида\n" +
                "на одну уставку, они ведут друг друга.\n" +
                "\n" +
                "Действует только в АИН: в слежении и в сети амплитуду ведёт\n" +
                "сеть, а мост отдаёт ток (уставка Iset на движке Load1).\n" +
                "\n" +
                "Шаг 0.1 В - ровно единица команды прибора, мельче он не умеет.";

            const string freq =
                "Частота автономного инвертора, Гц.\n" +
                "\n" +
                "То же число, что на большом движке слева, пока мост в АИН.\n" +
                "\n" +
                "Действует только в АИН: в двух верхних режимах частоту диктует\n" +
                "сеть, и поле там погашено.\n" +
                "\n" +
                "Частота умножается на число точек синусоиды (sineB.ini) и даёт\n" +
                "частоту ШИМ: 50 Гц при 100 точках - это 5 кГц на ключах.";

            const string send =
                "\n\nСтрелки и набор с клавиатуры шлют команду сами, через 0.4 с\n" +
                "после последнего изменения. Enter и уход из поля - немедленно.\n" +
                "Жёлтый - ответа ещё нет. Остался жёлтым с вашим числом - прибор\n" +
                "принял другое, нажмите Enter ещё раз.";

            toolTip1.SetToolTip(nudGridUref, uref + send);
            toolTip1.SetToolTip(lblGridUref, uref + send);

            toolTip1.SetToolTip(nudGridFreq, freq + send);
            toolTip1.SetToolTip(lblGridFreq, freq + send);
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

            btnGridMode.BackColor = BtnIdle;
            btnGridMode.Text = GridModeName[_mode];
            GridMode_Hint();

            // Частоту задаёт оператор только у автономного инвертора. В двух
            // верхних режимах её диктует сеть, и поле обязано это показывать,
            // иначе введённое число молча ничего не изменит.
            nudGridFreq.Enabled = _mode == GridModeOff;

            // Уставка напряжения - тоже дело одного АИН: в двух верхних
            // режимах амплитуду ведёт слежение за сетью, а мост отдаёт ток.
            // Прибор команду 633 примет в любом режиме, но применит её только
            // в острове (grid_tie_apply_uref при !gridMode), и поле обязано
            // это показывать.
            nudGridUref.Enabled = _mode == GridModeOff;

            // Там же, где поле, подменяется и большой движок: в АИН он задаёт
            // частоту, в остальных режимах возвращается к оборотам.
            SpeedSlider_ToFreq(_mode == GridModeOff);

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
                GridMode_Hint();
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

            if (_invShown != on)
            {
                _invShown = on;
                btnInvPower.Text = on ? @"Вкл" : @"Выкл";
                btnInvPower.BackColor = on ? Color.FromArgb(0, 192, 0) : BtnIdle;
                toolTip1.SetToolTip(btnInvPower, InvPowerHint[on ? 1 : 0]);
            }

            // Авария автомата видна на кнопке режима: в GT_FAULT мост закрыт, и
            // надпись "Выход в сеть" без этого обещала бы работу, которой нет.
            //
            // Но ТОЛЬКО когда автомат вообще при делах. В режиме АИН сетевого
            // контура нет, а gt.fault в прошивке защёлкивается и живёт до
            // grid_tie_stop() - плату между сеансами никто не перезагружает.
            // Без этой проверки кнопка горела красным сразу после запуска GUI,
            // жалуясь на аварию прошлого сеанса, к которой оператор ещё и
            // отношения не имел. Заодно это отсекает мусор при отсутствии
            // связи: пока ответа на 630 не было, _mode = АИН.
            bool fault = (_mode != GridModeOff) && (ccs.gtState == 4);

            Color mode = fault ? Color.Red : BtnIdle;
            if (btnGridMode.BackColor != Color.Yellow && btnGridMode.BackColor != mode)
                btnGridMode.BackColor = mode;
        }

        //-------------------------------------------------------------------
        // Частота: стрелки поля работают наравне с набором числа.
        //
        // Прежде команда уходила только по Enter и по уходу фокуса, а стрелки
        // молча меняли число в поле - прибор о них не знал. Стрелками правят
        // частоту чаще всего: поймать на осциллографе нужное значение проще,
        // шагая по герцу, чем набирая вслепую.
        //
        // Но слать на КАЖДОЕ изменение нельзя. Стрелка щёлкает по разу на
        // герц, и десять щелчков превратились бы в десять команд подряд, а при
        // наборе с клавиатуры в прибор уезжали бы ещё и промежуточные числа:
        // "60" по дороге проходит через "6", и мост успел бы сходить на 6 Гц.
        //
        // Поэтому посылка отложенная: таймер перезапускается на каждом
        // изменении и стреляет, когда щёлкать перестали. Enter и уход фокуса
        // шлют немедленно, отменяя отложенную, - у них намерение однозначное.
        //-------------------------------------------------------------------
        private readonly Timer _gridFreqT = new Timer();

        private void GridFreq_Init()
        {
            _gridFreqT.Interval = 400;
            _gridFreqT.Tick += (s, e) =>
            {
                _gridFreqT.Stop();
                GridFreq_Commit(null, EventArgs.Empty);
            };
        }

        private void GridFreq_Changed(object sender, EventArgs e)
        {
            // Жёлтым красим сразу, а не в момент посылки: поле уже показывает
            // не то, что в приборе, и это должно быть видно всё время
            // ожидания, а не последние его миллисекунды.
            nudGridFreq.BackColor = Color.Yellow;

            _gridFreqT.Stop();
            _gridFreqT.Start();
        }

        private void GridFreq_Commit(object sender, EventArgs e)
        {
            _gridFreqT.Stop(); // явный ввод отменяет отложенную посылку

            // Движок ведём за полем - зеркально тому, как SpeedRef_send ведёт
            // поле за движком. Оба показывают одно число, и разойтись им
            // нельзя: сверка в ccs_To_NUD идёт по tbar.Value, и со старым
            // значением движка жёлтый на нём не погас бы.
            if (_freqOnSpeed)
            {
                NUD_RPM.ValueChanged -= NUD_RPM_ValueChanged;
                Tbar_speed.ValueChanged -= Tbar_speed_ValueChanged;

                NUD_RPM.Value = Satur(nudGridFreq.Value, NUD_RPM.Minimum, NUD_RPM.Maximum);
                Tbar_speed.Value = Satur((int)nudGridFreq.Value, Tbar_speed.Minimum, Tbar_speed.Maximum);
                NUD_RPM.BackColor = Color.Yellow;

                Tbar_speed.ValueChanged += Tbar_speed_ValueChanged;
                NUD_RPM.ValueChanged += NUD_RPM_ValueChanged;
            }

            nudGridFreq.BackColor = Color.Yellow;
            uint_128((ushort)nudGridFreq.Value, 3333, 0, CmdGridFreq);
        }

        //-------------------------------------------------------------------
        // Уставка напряжения рядом с частотой.
        //
        // Действующее значение - другого у неё и нет: gt.UrefRms в прошивке
        // задан в вольтах rms, и пересчёт в амплитуду делает grid_tie_apply_uref
        // вместе с коэффициентом трансформации и напряжением звена.
        //
        // Число то же, что на движке Gain1 в режиме Uref, - это второй вид на
        // одну уставку, а не вторая уставка. Поле удобнее, когда значение
        // известно заранее ("хочу 220"), движок - когда его подбирают на ходу.
        // Оба шлют 633, оба ведут друг друга и оба гаснут по ответу на неё.
        //
        // Единицы команды - 0.1 В, поэтому у поля один знак после запятой и
        // шаг 0.1: шаг мельче единицы команды был бы обманом, крупнее - терял
        // бы разрешение, которое прибор умеет.
        //-------------------------------------------------------------------
        private readonly Timer _gridUrefT = new Timer();

        private void GridUref_Init()
        {
            _gridUrefT.Interval = 400;
            _gridUrefT.Tick += (s, e) =>
            {
                _gridUrefT.Stop();
                GridUref_Commit(null, EventArgs.Empty);
            };
        }

        private void GridUref_Changed(object sender, EventArgs e)
        {
            nudGridUref.BackColor = Color.Yellow;

            _gridUrefT.Stop();
            _gridUrefT.Start();
        }

        private void GridUref_Commit(object sender, EventArgs e)
        {
            _gridUrefT.Stop();

            ushort kod = (ushort)Math.Round(nudGridUref.Value * 10m);

            // Движок ведём за полем - как GridFreq_Commit ведёт движок
            // оборотов. Без этого сверка в ccs_To_NUD шла бы со старым
            // tbar.Value, и жёлтый на Gain1 не погас бы никогда.
            //
            // Только если движок сейчас действительно под Uref: в моторном
            // режиме у него своя команда, и класть туда вольты нельзя.
            if (M1.gain.modeH.Text == @"Uref")
            {
                NUD_Gain1.ValueChanged -= NUD_Gain1_ValueChanged;
                Tbar_Gain1.ValueChanged -= Tbar_Gain1_ValueChanged;

                M1.gain.nud.Value = Satur(nudGridUref.Value, M1.gain.nud.Minimum, M1.gain.nud.Maximum);
                M1.gain.tbar.Value = Satur((int)kod, M1.gain.tbar.Minimum, M1.gain.tbar.Maximum);
                M1.gain.nud.BackColor = Color.Yellow;

                Tbar_Gain1.ValueChanged += Tbar_Gain1_ValueChanged;
                NUD_Gain1.ValueChanged += NUD_Gain1_ValueChanged;
            }

            // Та же память, что ведёт Tbar_Send: уставка обратно из прибора не
            // приходит, и после ухода в моторный режим и обратно движок должен
            // встать на неё, а не в ноль.
            _urefSet = nudGridUref.Value;

            nudGridUref.BackColor = Color.Yellow;
            uint_128(kod, 3333, 0, CmdUrefRms);
        }

        //-------------------------------------------------------------------
        // ACK 633 в части поля. Движок разбирает ccs_To_NUD там же, в case 633.
        // Закон тот же, что у частоты: на чтение присваиваем, на запись гасим
        // жёлтый только при совпадении, иначе оставляем заданное на виду.
        //-------------------------------------------------------------------
        private void GridUref_Ack(ushort d1, ushort d2)
        {
            decimal volts = d1 / 10m;

            if (d2 != 3333)
            {
                _gridUrefT.Stop();

                nudGridUref.ValueChanged -= GridUref_Changed;
                nudGridUref.Value = Satur(volts, nudGridUref.Minimum, nudGridUref.Maximum);
                nudGridUref.ValueChanged += GridUref_Changed;

                nudGridUref.BackColor = Color.Empty;
                nudGridUref.ForeColor = Color.Green;
            }
            else if (volts == nudGridUref.Value)
            {
                nudGridUref.BackColor = Color.Empty;
                nudGridUref.ForeColor = Color.Green;
            }
        }

        //-------------------------------------------------------------------
        // Частота на большом движке.
        //
        // Пара Tbar_speed + NUD_RPM (задание оборотов, команда 72) обкатана:
        // движок и поле ведут друг друга, отписываясь на время правки, чтобы
        // не гонять событие по кругу, а значение приходит из ответа прибора
        // через ccs_To_NUD. Городить рядом вторую такую пару ради частоты
        // незачем - у моста оборотов нет, и в АИН движок всё равно простаивает.
        //
        // Подменяется он ТОЛЬКО в АИН, как Gain1 подменяется под Uref, а Load1
        // под Iset в GridMode_Ack. В слежении и в сети частоту диктует сеть,
        // и движок возвращается к оборотам вместе с прежним диапазоном.
        //
        // Поле nudGridFreq на панели моста остаётся - это одно и то же число в
        // двух местах, и разойтись они не могут: оба шлют 624 и оба ставятся
        // из ответа на неё (GridFreq_Ack).
        //-------------------------------------------------------------------
        private bool _freqOnSpeed;

        // Что было на движке до подмены. Диапазон оборотов 0..6000 в диапазон
        // частоты 40..70 не превратить без потерь, поэтому и значение, и
        // границы запоминаются целиком.
        private int _rpmMin, _rpmMax, _rpmVal;
        private decimal _nudRpmMin, _nudRpmMax, _nudRpmVal;

        private void SpeedSlider_ToFreq(bool on)
        {
            if (on == _freqOnSpeed) return;
            _freqOnSpeed = on;

            // Обработчики снимаем на время перестройки: правка Minimum и
            // Maximum сама двигает Value, и каждая такая подвижка ушла бы в
            // прибор отдельной командой - то оборотами, то частотой.
            Tbar_speed.ValueChanged -= Tbar_speed_ValueChanged;
            NUD_RPM.ValueChanged -= NUD_RPM_ValueChanged;

            if (on)
            {
                _rpmMin = Tbar_speed.Minimum;
                _rpmMax = Tbar_speed.Maximum;
                _rpmVal = Tbar_speed.Value;
                _nudRpmMin = NUD_RPM.Minimum;
                _nudRpmMax = NUD_RPM.Maximum;
                _nudRpmVal = NUD_RPM.Value;

                // Границы берём у поля на панели моста, чтобы предел был один
                // на оба органа и не пришлось помнить его в двух местах.
                Tbar_speed.Minimum = (int)nudGridFreq.Minimum;
                Tbar_speed.Maximum = (int)nudGridFreq.Maximum;
                NUD_RPM.Minimum = nudGridFreq.Minimum;
                NUD_RPM.Maximum = nudGridFreq.Maximum;

                NUD_RPM.Value = nudGridFreq.Value;
                Tbar_speed.Value = (int)nudGridFreq.Value;

                // Кнопка режима поля к частоте отношения не имеет. Пусть
                // вместо неё пишет, чем движок управляет сейчас: иначе
                // единственным признаком подмены была бы съехавшая шкала.
                button_Field_RPM.Text = @"F Hz";
                button_Field_RPM.Enabled = false;
            }
            else
            {
                // Сперва границы, потом значение: обороты не влезают в
                // частотную шкалу и обрезались бы о ещё не поднятый Maximum.
                Tbar_speed.Minimum = _rpmMin;
                Tbar_speed.Maximum = _rpmMax;
                NUD_RPM.Minimum = _nudRpmMin;
                NUD_RPM.Maximum = _nudRpmMax;

                NUD_RPM.Value = _nudRpmVal;
                Tbar_speed.Value = _rpmVal;

                button_Field_RPM.Text = @"Поле";
                button_Field_RPM.Enabled = true;
            }

            Tbar_speed.ValueChanged += Tbar_speed_ValueChanged;
            NUD_RPM.ValueChanged += NUD_RPM_ValueChanged;

            toolTip1.SetToolTip(Tbar_speed, on
                ? "Частота автономного инвертора, Гц.\nТо же число, что в поле F Hz на панели моста."
                : "Задание оборотов.");

            // Спросить прибор, что у него на самом деле. Читающая посылка -
            // d2 = 0, ответ придёт в свой case и ляжет в органы штатным путём.
            //
            // Спрашиваем в ОБЕ стороны: входя в АИН - частоту, выходя -
            // обороты. Восстановленное выше число - это память GUI, а она за
            // время работы в другом режиме могла устареть: обороты меняет и
            // сам прибор, и другие окна. Держим его только чтобы движок не
            // стоял на обрезанном границами значении, пока идёт ответ, и чтобы
            // было что показать, если связи нет.
            if (on)
                uint_128(0, 0, 0, CmdGridFreq);
            else
                int_128(0, 0, 0, 72);
        }

        //-------------------------------------------------------------------
        // ACK 624: частота пришла из прибора.
        //
        // Разбор ровно тот же, что у ccs_To_NUD, и это не совпадение - в окне
        // один закон на все уставки, и частота не повод его нарушать:
        //
        //   ответ на ЧТЕНИЕ (d2 != 3333) - прибор единственный источник,
        //       его число кладём в поле и гасим жёлтый;
        //
        //   ответ на ЗАПИСЬ (d2 = 3333), число совпало - оператор получил то,
        //       что просил, гасим жёлтый;
        //
        //   ответ на ЗАПИСЬ, число НЕ совпало - не трогаем ничего. Заданное
        //       остаётся в поле, поле остаётся жёлтым. Затирать заданное
        //       приборным здесь нельзя: оператор потеряет и то, что вводил, и
        //       сам факт расхождения, а повторный Enter - штатный способ
        //       дожать команду, которую прибор не принял.
        //-------------------------------------------------------------------
        private void GridFreq_Ack(ushort d1, ushort d2)
        {
            if (d2 != 3333)
            {
                _gridFreqT.Stop(); // поле сейчас перепишется прибором

                nudGridFreq.ValueChanged -= GridFreq_Changed;
                nudGridFreq.Value = Satur((decimal)d1, nudGridFreq.Minimum, nudGridFreq.Maximum);
                nudGridFreq.ValueChanged += GridFreq_Changed;

                nudGridFreq.BackColor = Color.Empty;
                nudGridFreq.ForeColor = Color.Green;
            }
            else if (d1 == (ushort)nudGridFreq.Value)
            {
                nudGridFreq.BackColor = Color.Empty;
                nudGridFreq.ForeColor = Color.Green;
            }

            if (!_freqOnSpeed) return;

            NUD_RPM.ValueChanged -= NUD_RPM_ValueChanged;
            Tbar_speed.ValueChanged -= Tbar_speed_ValueChanged;

            // d2 передаём как пришло: ccs_To_NUD знает тот же закон и разберёт
            // чтение и запись сама.
            ccs_To_NUD(NUD_RPM, Tbar_speed, d1, d2, 0, 1);

            Tbar_speed.ValueChanged += Tbar_speed_ValueChanged;
            NUD_RPM.ValueChanged += NUD_RPM_ValueChanged;
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
