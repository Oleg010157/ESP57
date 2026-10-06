using ADCTest;
using EthLogger;
using FlagMask;
using FuncBuilder;
using ICDIBasic.Properties;
using ICDIBasic.Stepulyov;
using MiscFuncs;
using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static EthLogger.AlStEthLog;
using Timer = System.Windows.Forms.Timer;
using CANTxDataTable;
using CANRxDataTable;


// ReSharper disable CheckNamespace
namespace ICDIBasic
{
    public partial class Form1
    {
        #region 01 - Общее
        #region Переменные
        #region переменные инфы о версии прошивки КСП
        public readonly StringBuilder _s0 = new StringBuilder();
        public bool _srx;
        public ushort Bcnt; // количество Mbox-ов в spf кадре
        public string Date;
        public string BuildDate;
        public string BuildTime;
        //      public string FastFrameString; // имя ini файла для spf кадра
        public bool IdRcvd = false;
        public string Kk1;
        public string Kk2;
        public string Kk3;
        public string KspIdString; // строка с описанием версии программы
        public string Kuk1;
        public string Kuk2;
        public string Kuk3;
        //       public string ToothFrameString;  // имя ini файла для tooth кадра
        //       public int boxesT; // количество  Mbox-ов в Tooth кадре
        //       public int boxesF; // количество  Mbox-ов in Fast кадре
        #endregion

        Queue<byte[]> mbox13FIFO = new();

        public Tooth_F9 frm9;  // ссылка на объект формы Tooth_F9

        #region Пути к директориям и файлам
        private string _oNm;
        private string spl_Dir = Application.StartupPath + "\\SPL" + "\\spl_data\\";
        private string spf_Dir = Application.StartupPath + "\\SPF" + "\\spf_data\\";
        private string sp2_Dir = Application.StartupPath + "\\SPF" + "\\sp2_data\\";
        private string wscope_Dir = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "Wscope"));
        private string spl_tmp;
        private FileStream _splStream; // держим файл .tmpm открытым на весь сеанс записи, не открываем/закрываем на каждый кадр
        private string F2Kadr_IniFileName; // имя ini файла заданное из мотор control формы
        private string spl_StartTime;
        private string spl_FileName;
        #endregion

        #region Экземпляры делегатов для внешних форм
        public DRotateArrow MainFrmRotArr;
        public DDelFiles DelF;
        public DSplHdr CrHdr;
        public Fname SetFname;
        public DImitByCan ImitByCan;
        public DCreateSplDir CreateSplDir;
        public DCloseWscope CloseWscope;
        public DGetIniFnames GetIniFileNames;
        #endregion

        //
        private const string CanAText = @"[CAN A] =";
        private const string CanBText = @"[CAN B] =";

        //
        private readonly Image _brOn = (Image)Resources.ResourceManager.GetObject("resistor2");
        private readonly Image _brOff = (Image)Resources.ResourceManager.GetObject("resistor2j");
        private AlStInfoF _infof;
        private AlStEthLog _ethLog;
        private short _ract;
        //     private readonly RxStr kspID = new RxStr();
        public AlStIgbtTest igbt; // ссылка на экземпляр формы AlStIgbtTest
        private readonly List<string> _k1 = new List<string>();
        private readonly List<string> _k2 = new List<string>();
        private readonly List<string> _k3 = new List<string>();
        private readonly List<TxDataFrm> _txFl = new List<TxDataFrm>();
        private readonly List<TxDataFrm> _txFlT = new List<TxDataFrm>();
        private readonly List<RxDataFrm> _rxFl = new List<RxDataFrm>();
        private readonly List<RxDataFrm> _rxFlT = new List<RxDataFrm>();
        private readonly Timer _rqTmr = new Timer { Enabled = true, Interval = 1000 };
        private List<string> _splIniL = new List<string>();
        private AStBreakRes _brResFRm;
        private AdcInfo _adcInfoKsp;

        private bool F2kadr_markerSet;
        private bool fast_markerSet; // признак - нажата клавиша пробел для обработки в кадре spl 
        private bool spl_running;
        private const byte Idbr = 112;
        private const string WscopeFile = @"Wscope.exe";
        private ErrorsView _errFrm;
        private readonly Bitmap _greenLed = new Bitmap(20, 20);
        private readonly Bitmap _redLed = new Bitmap(20, 20);
        private Color _connectDefaultBackColor;
        private bool _manualCanaCheckPending;
        private Font _checkCanAMenuDefaultFont;
        private Font _checkCanAMenuPulseFont;
        private Timer _checkCanAPulseTimer;
                                                  //      private static ushort[] splArr = new ushort[FrlM]; // массив Параметров передаваемыч в SPL кадр
        private ushort[] spL = new ushort[100]; // массив Параметров передаваемых в SPL кадр
                                                //       private  ushort[] spL;   // = new ushort[FrlM]; // массив Параметров передаваемых в SPL кадр


    
        private TemperatureView _tempsFrm;
        public SharedFuncs SharedFx = new SharedFuncs();

        #region Менюшки
        private readonly ToolStripTextBox _canaM = new ToolStripTextBox
        {
            Name = "CanAm_",
            MaxLength = CanAText.Length + 2
        };

        public static ToolStripTextBox _canbM = new ToolStripTextBox
        {
            Name = "CanBm_",
            MaxLength = CanBText.Length + 2
        };

        private readonly ToolStripMenuItem _abmn = new ToolStripMenuItem { Name = "Abmn", Text = @"О программе" };
        private readonly ToolStripMenuItem _adcInfoMenu =
            new ToolStripMenuItem { Name = "adcInfoMenu", Text = @"Инфо АЦП" };
        private readonly ToolStripMenuItem _reInitCanBMenu =
            new ToolStripMenuItem { Name = "ReInitCanBMenu_", Text = @"Реинициализация CAN B" };
        private readonly ToolStripMenuItem _reInitCanAMenu =
            new ToolStripMenuItem { Name = "ReInitCanAMenu_", Text = @"Реинициализация CAN A" };
        private readonly ToolStripMenuItem _pwmBrResMenu =
            new ToolStripMenuItem { Name = "PWM_BrResMenu_", Text = @"Управление тормозными резисторами" };
        private readonly ToolStripMenuItem _rslvResMenu =
            new ToolStripMenuItem { Name = "RslvResMenu_", Text = @"Сброс ошибки резолвера" };
        private readonly ToolStripMenuItem _txDataMenu =
            new ToolStripMenuItem { Name = "TxDataMenu_", Text = @"Окно передачи данных" };
        private readonly ToolStripMenuItem _txDataMenuT =
            new ToolStripMenuItem { Name = "TxDataMenuT_", Text = @"Тест [TX]" };
        private readonly ToolStripMenuItem _rxDataMenu =
            new ToolStripMenuItem { Name = "RxDataMenu_", Text = @"Окно приёма данных" };
        private readonly ToolStripMenuItem _rxDataMenuT =
            new ToolStripMenuItem { Name = "RxDataMenuT_", Text = @"Тест [RX]" };
        private readonly ToolStripMenuItem _testMenu = new ToolStripMenuItem { Name = "TestMenu", Text = @"Тест" };
        private readonly ToolStripMenuItem _ethLogMenu =
            new ToolStripMenuItem { Name = "EthLogMenu_", Text = @"Ethernet КВУ" };
        private readonly ToolStripMenuItem _igbtTestMenu =
            new ToolStripMenuItem { Name = "IGBTTestMenu", Text = @"Тест IGBT" };
        private readonly ToolStripMenuItem _funcTblMenu1 =
            new ToolStripMenuItem { Name = "FuncTblMenu1", Text = @"Таблица функц. АИН1" };
        private readonly ToolStripMenuItem _funcTblMenu2 =
            new ToolStripMenuItem { Name = "FuncTblMenu2", Text = @"Таблица функц. АИН2" };
        private readonly ToolStripMenuItem _checkCanAMenu =
            new ToolStripMenuItem { Name = "CheckCanAMenu_", Text = @"Проверить связь CANA" };
        #endregion

        private TPCANStatus _stsResult;
        private ushort _cntRef;

        private byte probel_markerCnt = 250; // циклический счётчик инкрементируеся при каждом нажатии клавиши пробел. Значение сохраняется в кадре.
        private ushort _tooths = 80;

        private FuncBuilderForm _approxLoadSabs1, _approxLoadSabs2;
        public delegate int DGetToothNum();
        public delegate string DGetIniFnames(bool fasttooth);
        public delegate string DGetSplInfo();
        public delegate void DChangeFrmTxt(bool st);
        public delegate void DCloseWscope();
        public delegate void DCreateSplDir(string dir);
        public delegate void DDelFiles(string type);
        public delegate void DImitByCan(ushort cmd);
        public delegate void DLdt();
        public delegate void DRotateArrow(ushort md);
        public delegate void DSaveSvtg(string p);
        public delegate void DSend128(ushort d1, ushort d2, ushort d3, ushort c);
        public delegate void DSendInts(int d1, int d2, int d3, ushort c);
        public delegate void DSendMbox127(ushort d1, ushort d2, ushort d3, ushort d4);
        public delegate void DSplHdr(ushort frLen, string splInfo, string iniFile, string splFile);
        public delegate void Fname(string fname);
        public delegate bool Grden();
        public ushort CntData { get; set; }

        public ushort MboxCnt(ushort d1, ushort d2)
        {
            string s = Convert.ToString(d1, 2) + Convert.ToString(d2, 2);
            return (ushort)s.Replace(@"0", string.Empty).Length;
        }
        private static string Reverse(string s)
        {
            var charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        #endregion

        #region Синхронизация Spl_Main, получение данных телеметрии с CANA 
        private void CAN_RxData(TPCANMsgFD msg) // эта функция работает во вторичном потоке
        {
            if (IsDisposed) return;
          
            {
                Kadr60Rx(msg); // читаем данные со всех msgId , кроме msgId =1
                if (!spl_running) return;

                // если задан режим вывода кадра по CANA
                // 1. обязательно должно быть изменение счётчика кадров k60Cnt.
                // это условие необходимо для подтверждения что все переменные кадра были обновленны.
                if (_cntRef == ccs.k60Cnt) return;

                if (_rden)
                    try
                    {

                        F60GetData(); // вот здесь формируем кадр (массив данных) в соответствии с заданным именем 

                        var t = new ushort[FrlM];
                        Array.Copy(spL, t, FrlM);
                //        foreach (var f in _rxFl) Invoke(new Action(() => { f.RxD(t); }));
                //        foreach (var f in _rxFlT) Invoke(new Action(() => { f.RxD(t); }));

                        byte[] frame = new byte[FrlM * 2 + 1]; // Кадр
                        for (ushort index = 0; index < FrlM; index++) // инвертируем положение байт в ushort
                            t[index] =
                                (ushort)((ushort)((t[index] & 0x00FF) << 8) | (ushort)((t[index] & 0xFF00) >> 8));
                        Buffer.BlockCopy(t, 0, frame, 1, FrlM * 2);


                        if (F2kadr_markerSet)
                        {
                            frame[0] = probel_markerCnt;
                            F2kadr_markerSet = false;
                        }
                        else
                        {
                            frame[0] = 0x55;
                        }

                        _splStream?.Write(frame, 0, frame.Length);

                        // поворачиваем стрелку на 90 градусов через каждые 10 кадров.
                        // Счётчик считаем здесь, во вторичном потоке, и обращаемся к UI (BeginInvoke)
                        // только раз в 10 кадров — иначе при частой телеметрии BeginInvoke летит
                        // в очередь UI-потока на каждый кадр и забивает её, не давая вовремя
                        // выполниться другим UI-событиям (например, отправке команд с трек-бара).
                        _ract++;
                        if (_ract > 10)
                        {
                            _ract = 0;
                            BeginInvoke(new Action(() =>
                            {
                                pbMainKadr.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                pbMainKadr.Refresh();
                            }));
                        }
                    }
                    catch
                    {
                        spl_Stop();
                        Del_Files(spl_Dir, "*.tmpm");
                    }
                _cntRef = ccs.k60Cnt;
            }
        }
        #endregion



        #region Управление от функциональных кнопок  F2 - F12

        // переменные для работы в ассинхронном потоке
        public bool f9run = false;  // запрос на обход вычисления метода F9buff() формы F9 во вторичном потоке, сдесь форму не закрываем иначе будет Exception
        public bool f9del;    // запрос во вторичный поток на закрытие формы F9

        public static string f2kadrName; // = F2_Kadr_Name.Text;

        #region Лента быстрого кадра: кто её просит

        /*
         * У кадра 103 ДВА НЕЗАВИСИМЫХ ЗАКАЗЧИКА - запись в файл (F2/Alt+F2) и
         * окно осциллографа (открытие/закрытие), - а ресурс один: команда CAN
         * 1001, которой прибор включает передачу. Поэтому счёт идёт не
         * «включено/выключено», а «кто просит»: лента идёт, пока её просит хоть
         * один, и снимается последним ушедшим.
         *
         * ЗАЧЕМ ТАК. Раньше это было одно: F2 включал и ленту, и запись, Alt+F2
         * выключал оба. Смотреть осциллографом без записи было поэтому нельзя, и
         * файл писался в холостую - его длина тому и цена. Теперь Alt+F2 не
         * гасит картинку, а закрытие окна не рвёт ленту живого опыта.
         *
         * ТОЛЬКО ИЗ UI-ПОТОКА. uint_128 читает tsmConnect.Text и показывает
         * MessageBox при ошибке - из потока CAN это обращение к контролу чужого
         * потока. Поэтому spl_Stop ленту НЕ снимает, хотя это и выглядело бы
         * естественно: его зовут в том числе из CAN_RxData по ошибке записи и из
         * Spl_Writers_Stop. Снимает её TapeRelease с секундного таймера -
         * разрешение в секунду здесь ни на что не влияет.
         */

        /// <summary>Код набора, который прибор шлёт сейчас. 0 - лента снята.</summary>
        private ushort _tapeOn;

        /// <summary>
        /// Код набора для команды 1001. Имя не из четырёх - медленный кадр: так
        /// было и до разделения.
        /// </summary>
        private static ushort TapeCode(string frame)
        {
            switch (frame)
            {
                case "F1_convertor": return 1;
                case "F1_invertor":  return 2;
                case "F3_invertor":  return 3;
                case "F3_convertor": return 4;
                default:             return 5;
            }
        }

        /// <summary>
        /// Пустить ленту под набор, выбранный в поле «F2 Кадр». Повторный вызов
        /// под тот же набор команду прибору не посылает.
        ///
        /// НАБОР ЗАПЕРТ НА ВРЕМЯ ЗАПИСИ: сменить его на ходу значит дописать в
        /// тот же .spl данные другой раскладки, а имена каналам файл даёт по
        /// имени набора - вышли бы чужие числа под правильными именами.
        /// </summary>
        public void TapeStart()
        {
            if (!_rden || spl_running) return;

            ushort code = TapeCode(F2_Kadr_Name.Text);

            /* f2kadrName ставим ВСЕГДА, даже когда команда не нужна: по нему
             * осциллограф выбирает шаблон, а запись - раскладку кадра. */
            f2kadrName = F2_Kadr_Name.Text;

            if (code == _tapeOn) return;

            uint_128(0, 0, code, 1001);
            _tapeOn = code;
        }

        /// <summary>Снять ленту, если её больше никто не просит.</summary>
        public void TapeRelease()
        {
            if (_tapeOn == 0) return;

            /* Закрытие программы при открытом окне осциллографа: окно закрывается
             * вслед за главным и приходит сюда, когда главная форма уже
             * разбирается. uint_128 читает tsmConnect.Text, то есть обратился бы
             * к контролу, которого может уже не быть. Прибор в этом случае
             * останется с включённой лентой - так было и до разделения, F2 без
             * Alt+F2 оставлял её точно так же. */
            if (IsDisposed || Disposing) return;

            /* Связи нет - состояние прибора неизвестно, и команду посылать
             * некуда. Забываем своё: после подключения TapeStart пошлёт её
             * заново и этим сам всё и выровняет. */
            if (!_rden) { _tapeOn = 0; return; }

            if (spl_running || ScopeForm.IsOpen) return;

            uint_128(0, 0, 0, 1001);
            _tapeOn = 0;
        }

        #endregion

        private void BControl(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.F2 && !e.Alt && _rden)
            {
                e.SuppressKeyPress = true;

                if (!spl_running)
                {
                    probel_markerCnt = 0;
                    pbMainKadr.Visible = pbMainKadr.Enabled = true;

                    // F2 - ТОЛЬКО ЗАПИСЬ. Лента нужна ей как источник, поэтому
                    // просим её здесь; если её уже пустило окно осциллографа,
                    // команда прибору второй раз не уйдёт.
                    TapeStart();

                    F60GetData();
                    spl_Start();
                }
            }
            if (e.KeyCode == Keys.F2 && e.Alt)
            {
                e.SuppressKeyPress = true;

                // Alt+F2 - ТОЛЬКО ЗАПИСЬ. Лента остаётся, если на неё смотрит
                // осциллограф: гасить картинку за компанию незачем.
                spl_Stop();
                TapeRelease();
            }

            //  Запуск быстрого  кадра F3 по CANB
            if (e.KeyCode == Keys.F3 && !e.Alt && _rden)
            {
                e.SuppressKeyPress = true;
                f9run = false;   // запрос на обход вычисления метода F9buff() формы F9 во вторичный поток, сдесь форму не закрываем иначе будет Exception
                sp2.running = false;

                if (!spf_running)
                {
                    cmd_128(100);  // нажата F3 -> запустить фаст кадр ( команда на ШПСУ)
                }
                else // запись spf кадра уже идёт
                {
                    //  
                }
            }

            //-------  Останов  быстрого  кадра F3 по CANB  ------------------------------------------------------
            if (e.KeyCode == Keys.F3 && e.Alt) // Остановка записи Fast  файла
            {
                e.SuppressKeyPress = true;
                if (!spf_running) return;
                spf_Stop(spf);
                if (frm9 != null)
                {
                    frm9.Tooth_F9_Form_NeededToStop();
                }
            }


            if (e.KeyCode == Keys.F4 && !e.Alt)  // запуск sp2 кадра 
            {
                e.SuppressKeyPress = true;
             

                if (!sp2.running) // первое нажатие F4
                {
                    cmd_128(101); // команда в ксп SplMode = Spl_Tooth;
                }
            }

            if (e.KeyCode == Keys.F4 && e.Alt) // останов записи sp2 кадра
            {
                e.SuppressKeyPress = true;
                spf_Stop(sp2);  //
                if (frm9 != null)
                {
                    frm9.Tooth_F9_Form_NeededToStop();
                }

            }

            /*
             * F5 И Alt+F5 - НАСЛЕДИЕ ДВУХКАНАЛЬНОЙ СХЕМЫ, в этом проекте не
             * работают.
             *
             * Они затевались там, где CAN было два: CANA под команды и медленные
             * кадры, CANB под быстрые. Отсюда и смысл F5 - пустить разом запись
             * медленного файла (spl) и быстрого (spf, он же F3/F4 по CANB).
             * Здесь канал один, CANB дремлет, и половина F5 никуда не ведёт.
             *
             * ПОЭТОМУ ЛЕНТУ F5 НЕ ПРОСИТ, хотя spl_Start без неё пишет файл в
             * пустоту, а F60GetData не зовётся и в шапку уходит FrlM от прошлого
             * сеанса. Чинить это, пока второго канала нет, значит оживлять
             * мёртвое: заработает оно только вместе с CANB, и тогда же станет
             * видно, что именно чинить. Alt+F5 ленту отпускать тоже не обязан -
             * её снимет TapeRelease с секундного таймера, см. регион «Лента
             * быстрого кадра» выше.
             */
            if (e.KeyCode == Keys.F5 && !e.Alt && _rden) // F5 -> одновременный старт записи Spl_main  и Fast-Tooth  файлов
            {
                e.SuppressKeyPress = true;
                probel_markerCnt = 0;

                if (!spl_running) spl_Start();
                if (!spf.running) cmd_128(100);  // нажата F3 -> запустить фаст кадр ( команда на ШПСУ)
            }

            if (e.KeyCode == Keys.F5 && e.Alt) //  Alt+F5
            {
                e.SuppressKeyPress = true;
                spl_Stop();  // Остановка записи spl файла
                spf_Stop(spf);       // Остановка записи fast  файла
            }

            if (e.KeyCode == Keys.F6)  // вывод F2_Kadra на WScope
            {
                e.SuppressKeyPress = true;
                Open_WScope(spl_FileName);
            }

            if (e.KeyCode == Keys.F7) // вывод fastKadra на WScope
            {
                e.SuppressKeyPress = true;
                Open_WScope(spf_FileName);
            }

            if (e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                ProbelCounter();
            }



            // -------------- F9 ---------------------------------------------------
            if (e.KeyCode == Keys.F9 && !e.Alt)  // запуск   F9_form -> вывод графиков переменных в реальном времени
            {
                e.SuppressKeyPress = true;
             

                if (spf_running)
                {
                    if (frm9 == null) // первое нажатие F9
                    {
                        if (sp2_Tooth_F9 == true)
                        {
                            ushort m = tooth = ccs.f9_tooths;
                            frm9 = new Tooth_F9(this, m) { KeyPreview = true };
                        }
                        else if (spf_Tooth_F9 == true)
                        {
                            ushort m = kadr;
                            frm9 = new Tooth_F9(this, m) { KeyPreview = true };
                        }
                        frm9.KeyDown += BControl;
                        frm9.Show();
                        f9run = true;
                        //                      f9del = false;

                    }
                    else   // последующие  нажатия F9 
                    {
                        if (frm9.Visible)
                        {
                            f9run = false;   // запрос на обход вычисления метода F9buff() формы F9 во вторичном потоке, сдесь форму не закрываем иначе будет Exception
                            frm9.Hide();
                        }
                        else
                        {
                            f9run = true;
                            frm9.Show();
                        }
                    }
                }

                else //  нажатие F9  при не запущенном  кадре по CANB  
                {

                    // тело когда поток не включён
                }
            }

            if (e.KeyCode == Keys.F9 && e.Alt) // останов осциллографа и закрытие формы F9 после полного прохода вторичного потока, f9del = true обеспечивает выход из бесконечного цикла while
            {
                e.SuppressKeyPress = true;

                if (frm9 != null)
                {
                    frm9.Hide();
                    f9run = false;
                    //           f9del = true;
                }
            }

          

        }
        #endregion

        #region Получение инфы из KSP_ID и версии этой проги
        private string GetSplInfo()
        {
            return $"{KspIdString};{Date}";
        }
        #endregion

        #region Запуск записи Spl_Main
        private void spl_Start()
        {
            if (spl_running) return;

            try
            {

                Del_Files(spl_Dir, "*.tmpm");
                //              Thread.Sleep(500);
                ClWs();  // 

                if (!Directory.Exists(spl_Dir)) Directory.CreateDirectory(spl_Dir);

                // День.месяц.год, как везде в проекте: быстрые файлы в CANB.cs,
                // прошивка в Bootloader.cs, журнал в MainWindow.cs. Здесь стоял
                // "yy.MM.dd" - год первым и двумя цифрами, - и это читалось как
                // день: "26.10.04" выглядело как 26 октября вместо 4 октября
                // 2026. Год четырьмя цифрами, чтобы спутать его с днём было
                // нельзя даже в принципе.
                spl_StartTime = DateTime.Now.ToString("dd.MM.yyyy_HH.mm.ss");
                spl_tmp = spl_Dir + spl_StartTime + ".tmpm";
                Spl_Hdr(FrlM, GetSplInfo(), F2Kadr_IniFileName, spl_tmp);

                _splStream = new FileStream(spl_tmp, FileMode.Append, FileAccess.Write, FileShare.Read);

                //     pbMainKadr.Enabled = pbMainKadr.Visible = true;
                spl_running = true;

            }
            catch
            {
                // ignored
            }
        }
        #endregion

        #region Остановка записи Spl_main
        private void spl_Stop()
        {
            pbMainKadr.Enabled = pbMainKadr.Visible = false;
            _ract = 0;

            if (!spl_running) return;

            try
            {
                _splStream?.Flush();
                _splStream?.Dispose();
                _splStream = null;

                // Тот же формат, что у .tmpm выше. ВРЕМЯ ЗДЕСЬ - МОМЕНТ
                // ОСТАНОВА (Alt+F2), а не старта: имя берётся свежим
                // DateTime.Now, и у длинной записи в имени стоит её конец.
                // Стартовое время лежит рядом, в spl_StartTime.
                spl_FileName = spl_Dir + DateTime.Now.ToString("dd.MM.yyyy_HH.mm.ss") + ".spl";

                if (File.Exists(spl_tmp))
                {
                    File.Move(spl_tmp, spl_FileName);
                }
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"spl_Stop: file move failed: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"spl_Stop: access denied during file move: {ex.Message}");
            }
            finally
            {
                spl_running = false;
                //       PCANBasic.Reset(Ch);
            }
        }
        #endregion

        #region Остановка записи Main и Fast-Tooth файлов
        private void Spl_Writers_Stop()
        {
            try
            {
                _brResFRm?.Close();
         //       SVtgFrm?.Close();
                _errFrm?.Close();
                _tempsFrm?.Close();
                spl_Stop();
                spf_Stop(sp2);       // Остановка записи Fast-Tooth  файла

                ClWs();
            }
            catch
            {
                // ignored
            }
        }
        #endregion
        #region Стрелки
        public void Rotate_Arrow(PictureBox pb, ushort md)
        {
            if (md == 0)
            {
                pb.Visible = false;
                return;
            }
            pb.Visible = true;
            pb.Image?.RotateFlip(RotateFlipType.Rotate90FlipNone);  // поворачивает стрелку на 90 градусов
            pb.Refresh();  // обновляет изображени и её положени стрелки (без этого не будут поворота 90 градусов у стрелки)


        }
        #endregion

        #region Управление Wscope
        private void Open_WScope(string splfile)
        {
            /* этот метод передаёт программе WScope.exe записанный splfile файл .spl или .spf типа
             */
            try
            {
                if (spl_running || spf_running || string.IsNullOrWhiteSpace(splfile)) return;

                var wscopePath = Path.Combine(wscope_Dir, WscopeFile);
                if (!File.Exists(wscopePath))
                {
                    var localWscopePath = Path.Combine(Application.StartupPath, WscopeFile);
                    wscopePath = File.Exists(localWscopePath) ? localWscopePath : string.Empty;
                }

                if (string.IsNullOrWhiteSpace(wscopePath))
                {
                    MessageBox.Show(this,
                        @"Не найден Wscope.exe.\nПроверьте расположение файла в папке Wscope рядом с bin или в папке запуска приложения.",
                        @"WScope",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                ClWs();
                var wSc = new Process
                {
                    StartInfo =
                    {
                        FileName = wscopePath,
                        Arguments = splfile,
                        WindowStyle = ProcessWindowStyle.Maximized
                    }
                };
                wSc.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, @"Ошибка запуска WScope", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ClWs()
        {
            var pname = Process.GetProcessesByName(@"Wscope");
            if (pname.Length == 0) return;
            pname[0].Kill();
            Thread.Sleep(50);
        }
        #endregion
        #region Проверка значений параметров Spl_Main
        // ReSharper disable once UnusedMember.Local
        private static ushort ChkRn(float inpt, float mult, bool sgnd)
        {
            try
            {
                var r = inpt * mult;
                if (sgnd)
                {
                    if (r < short.MinValue) return (ushort)Convert.ToInt16(short.MinValue);
                    if (r > short.MaxValue) return (ushort)Convert.ToInt16(short.MaxValue);
                    return (ushort)Convert.ToInt16(r);
                }
                if (r < ushort.MinValue) return ushort.MinValue;
                return r > ushort.MaxValue ? ushort.MaxValue : Convert.ToUInt16(r);
            }
            catch
            {
                return 0;
            }
        }
        #endregion
        #region Основной блок Spl_Main
        #endregion
        #region удаление записанных файлов
        private void Del_Files(string dir, string type)
        {
            try
            {
                foreach (var newPath in Directory.GetFiles(dir, type, SearchOption.AllDirectories))
                    File.Delete(newPath);
            }
            catch
            {
                // ignored
            }
        }
        #endregion

        #region Заголовок Spl
        private void Spl_Hdr(ushort frLen, string splInfo, string iniFile, string splFile)
        {
            try
            {
                SharedFx.Spl_Header_Create(frLen, splInfo, iniFile, splFile);
            }
            catch
            {
                Spl_Writers_Stop();  //Останавливаем запись .spl .spf  файлов и удаляем все *.tmp файлы
                if (splFile.Contains(".tmpm")) Del_Files(spl_Dir, "*.tmpm");
                if (splFile.Contains(".tmpf")) Del_Files(spf_Dir, "*.tmpf");
                if (splFile.Contains(".tmpt")) Del_Files(sp2_Dir, "*.tmpt");
            }
        }
        #endregion
        #region Разное
        #region Инит при старте
        private void AddMenus()
        {
            var canASetting = string.IsNullOrWhiteSpace(Settings.Default.CanAId)
                ? "A"
                : Settings.Default.CanAId.Trim().ToUpper();
            var canBSetting = string.IsNullOrWhiteSpace(Settings.Default.CanBId)
                ? "B"
                : Settings.Default.CanBId.Trim().ToUpper();

            if (canASetting == canBSetting)
            {
                canASetting = "A";
                canBSetting = "B";
                Settings.Default.CanAId = canASetting;
                Settings.Default.CanBId = canBSetting;
                Settings.Default.Save();
            }

            _canaM.Text = CanAText + canASetting;
            _canbM.Text = CanBText + canBSetting;
            _canaM.TextChanged += CanaM_TextChanged;
            _canbM.TextChanged += CanaM_TextChanged;

            МенюУправленияToolStripMenuItem.DropDownItems.Add(_pwmBrResMenu);
            МенюУправленияToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            МенюУправленияToolStripMenuItem.DropDownItems.Add(_rslvResMenu);

            измеренияToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            измеренияToolStripMenuItem.DropDownItems.Add(_txDataMenu);
            измеренияToolStripMenuItem.DropDownItems.Add(_rxDataMenu);

            управлениеToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            управлениеToolStripMenuItem.DropDownItems.Add(_canaM);
            управлениеToolStripMenuItem.DropDownItems.Add(_reInitCanAMenu);
            управлениеToolStripMenuItem.DropDownItems.Add(_checkCanAMenu);
            управлениеToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            управлениеToolStripMenuItem.DropDownItems.Add(_canbM);
            управлениеToolStripMenuItem.DropDownItems.Add(_reInitCanBMenu);
            управлениеToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            управлениеToolStripMenuItem.DropDownItems.Add(yokogawaWTToolStripMenuItem);

            debugToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            debugToolStripMenuItem.DropDownItems.Add(_ethLogMenu);
            menuStrip1.Items.Add(_testMenu);
            _testMenu.DropDownItems.Add(_txDataMenuT);
            _testMenu.DropDownItems.Add(_rxDataMenuT);
            _testMenu.DropDownItems.Add(new ToolStripSeparator());
            _testMenu.DropDownItems.Add(_adcInfoMenu);
            _testMenu.DropDownItems.Add(new ToolStripSeparator());
            _testMenu.DropDownItems.Add(_igbtTestMenu);
            _testMenu.DropDownItems.Add(new ToolStripSeparator());
            _testMenu.DropDownItems.Add(_funcTblMenu1);
            _testMenu.DropDownItems.Add(_funcTblMenu2);
        }

        private void _M_INit()
        {
            _connectDefaultBackColor = tsmConnect.BackColor;
            const ushort sc = 15;
            var gGreen = Graphics.FromImage(_greenLed);
            var gRed = Graphics.FromImage(_redLed);
            KeyPreview = true;
            KeyDown += BControl;

            gGreen.FillEllipse(Brushes.PaleGreen, 0, 0, sc, sc);
            gRed.FillEllipse(Brushes.Salmon, 0, 0, sc, sc);
            gRed.DrawEllipse(Pens.Black, 0, 0, sc, sc);
            gGreen.DrawEllipse(Pens.Black, 0, 0, sc, sc);

            pb_ErrView.Image = new Bitmap(_redLed);
            pb_ErrView.MouseDoubleClick += (o, l) => MenuErrorView_Click(this, null);
            pb_ErrView.Enabled = pb_ErrView.Visible = false;

            Ip3.MouseDoubleClick += (m, n) => uint_128(0, 0, 0, 71);
            Ip1.MouseDoubleClick += (m, n) => uint_128(0, 0, 0, 71);
            Ip4.MouseDoubleClick += (m, n) => uint_128(0, 0, 0, 71);
            Ip4.MouseDoubleClick += (m, n) => uint_128(0, 0, 0, 71);
            AddMenus();
            _funcTblMenu1.Click += (a, b) =>
            {
                if (_approxLoadSabs1 == null)
                {
                    _approxLoadSabs1 = new FuncBuilderForm(@"FTableSabs1.txt") { WindowState = FormWindowState.Normal };
                    _approxLoadSabs1.Shown += (x, z) => _funcTblMenu1.BackColor = Color.LightGreen;
                    _approxLoadSabs1.XValueChanged += _approxLoadSabs1_XValueChanged;
                    _approxLoadSabs1.Closed += (g, h) =>
                    {
                        _approxLoadSabs1 = null;
                        _funcTblMenu1.BackColor = Color.Empty;
                    };
                    _approxLoadSabs1.Show();
                    _approxLoadSabs1.BringToFront();
                }
                else
                {
                    _approxLoadSabs1.Close();
                }
            };
            _funcTblMenu2.Click += (a, b) =>
            {
                if (_approxLoadSabs2 == null)
                {
                    _approxLoadSabs2 = new FuncBuilderForm(@"FTableSabs2.txt") { WindowState = FormWindowState.Normal };
                    _approxLoadSabs2.Shown += (x, z) => _funcTblMenu2.BackColor = Color.LightGreen;
                    _approxLoadSabs2.XValueChanged += _approxLoadSabs2_XValueChanged;
                    _approxLoadSabs2.Closed += (g, h) =>
                    {
                        _approxLoadSabs2 = null;
                        _funcTblMenu2.BackColor = Color.Empty;
                    };
                    _approxLoadSabs2.Show();
                    _approxLoadSabs2.BringToFront();
                }
                else
                {
                    _approxLoadSabs2.Close();
                }
            };
            menuStrip1.Items.Add(_abmn);
            _abmn.Click += (a, z) => InfoFrm();
            //
            _txDataMenu.Click += (z, x) =>
            {
                var n = Settings.Default.LastCTxn;
                var t = Settings.Default.LastCTxt;
                if (!string.IsNullOrEmpty(n)) OpenTxF(n, t);
            };
            _txDataMenuT.Click += (z, x) =>
            {
                var n = Settings.Default.LastCTxTn;
                var t = Settings.Default.LastCTxTt;
                if (!string.IsNullOrEmpty(n)) OpenTxFt(n, t);
            };
            _rxDataMenu.Click += (z, x) =>
            {
                var n = Settings.Default.LastCRxn;
                var t = Settings.Default.LastCRxt;
                if (!string.IsNullOrEmpty(n)) OpenRxF(n, t);
            };
            _rxDataMenuT.Click += (z, x) =>
            {
                var n = Settings.Default.LastCRxTn;
                var t = Settings.Default.LastCRxTt;
                if (!string.IsNullOrEmpty(n)) OpenRxFt(n, t);
            };
            _adcInfoMenu.Click += (f, g) =>
            {
                if (_adcInfoKsp != null) return;
                _adcInfoKsp = new AdcInfo(true);
                _adcInfoKsp.SendC += s => cmd_128((ushort)s.Cmd);
                _adcInfoKsp.Show();
                _adcInfoMenu.Enabled = false;
                _adcInfoKsp.Closed += (q, a) =>
                {
                    _adcInfoKsp.Dispose();
                    _adcInfoKsp = null;
                    _adcInfoMenu.Enabled = true;
                };
            };
            _igbtTestMenu.Click += (f, g) =>
            {
                if (igbt != null) return;
                igbt = new AlStIgbtTest { Send128 = uint_128 };
                igbt.Show();
                _igbtTestMenu.Enabled = false;
                igbt.Closed += (q, a) =>
                {
                    igbt.Dispose();
                    igbt = null;
                    _igbtTestMenu.Enabled = true;
                };
            };
            RefreshL();
            _reInitCanAMenu.Click += ReInitCanAMenu_Click;
            _reInitCanBMenu.Click += (a, s) => uint_128(0, 0, 0, 102);
            _pwmBrResMenu.Click += (a, s) =>
            {
                if (_brResFRm == null)
                {
                    uint_128(0, 0, 0, Idbr);
                    _brResFRm = new AStBreakRes(Idbr) { KeyPreview = true };
                    _brResFRm.Show();
                    _pwmBrResMenu.Enabled = false;
                    _brResFRm.KeyDown += BControl;
                    _brResFRm.FormClosed += (d, f) =>
                    {
                        uint_128(0, 0, 0, Idbr);
                        _brResFRm = null;
                        _pwmBrResMenu.Enabled = true;
                    };
                    _brResFRm.Ds128 = uint_128;
                    Invoke(new Action(() => { _brResFRm.SetLoc(new Point(Location.X + 157, Location.Y + 115)); }));
                }
                LocationChanged +=
                    (d, k) =>
                    {
                        _brResFRm?.Invoke(
                            new Action(() => { _brResFRm.SetLoc(new Point(Location.X + 157, Location.Y + 115)); }));
                    };
            };
        
            _rslvResMenu.Click += (a, s) => uint_128(0, 0, 0, 113);
            Rkf();
            SlInit();
            _rqTmr.Tick += (g, n) =>
            {
                if (!_rden) return;
                ushort Req = 1; // Отправка лампочки ошибок в случае их наличия
                if (_errFrm != null) Req |= 2; // Отправка списка ошибок
                if (_tempsFrm != null) Req |= 4; // Отправка температур
                if (_adcInfoKsp != null && _adcInfoKsp.IsKsp) Req |= 8; // Отправка таблицы АЦП
                uint_128(Req, 0, 0, 336);
         //       uint_128(1, 2, 3, 336);
            };
            _ethLogMenu.Click += _EthLogMenu_Click;
            _checkCanAMenu.Click += CheckCanAMenu_Click;
            _canaM.DoubleClick += (a, z) =>
            {
                if (tsmConnect.Text != @"Подключить") return;
                if (!_rden)
                    CanaConnect();
                else
                    CanaDisconnect();
            };

            //
            Binit();
            CanaConnect();
        }

        private void CheckCanAMenu_Click(object sender, EventArgs e)
        {
            _canaM.BackColor = Color.Yellow;
            PulseCanCheckMenu();

            if (!_rden || Ch == 0)
            {
                CanaConnect();
            }

            if (Ch == 0)
            {
                return;
            }

            cmd_128(0);
        }

        private void PulseCanCheckMenu()
        {
            _checkCanAMenuDefaultFont ??= _checkCanAMenu.Font;
            _checkCanAMenuPulseFont ??= new Font(_checkCanAMenuDefaultFont, FontStyle.Bold);

            _checkCanAMenu.Checked = true;
            _checkCanAMenu.Font = _checkCanAMenuPulseFont;

            _checkCanAPulseTimer ??= new Timer { Interval = 1000 };
            _checkCanAPulseTimer.Tick -= CheckCanAPulseTimer_Tick;
            _checkCanAPulseTimer.Tick += CheckCanAPulseTimer_Tick;
            _checkCanAPulseTimer.Stop();
            _checkCanAPulseTimer.Start();
        }

        private void CheckCanAPulseTimer_Tick(object sender, EventArgs e)
        {
            _checkCanAPulseTimer.Stop();
            _checkCanAMenu.Checked = false;
            if (_checkCanAMenuDefaultFont != null)
                _checkCanAMenu.Font = _checkCanAMenuDefaultFont;
        }

        private void _approxLoadSabs1_XValueChanged(object sender, FuncBuilderForm.XeventArgs e)
        {
            if (_funcTblMenu1.BackColor != Color.LightGreen) return;
            Tbar_Load1.Value = Satur(Convert.ToInt16(e.YOut * 100.0D), Tbar_Load1.Minimum, Tbar_Load1.Maximum);
            Tbar_Load1_ValueChanged(Tbar_Load1, EventArgs.Empty);
            Tbar_Load1_Scroll(Tbar_Load1, EventArgs.Empty);
        }

        private void _approxLoadSabs2_XValueChanged(object sender, FuncBuilderForm.XeventArgs e)
        {
            if (Load_Sym.Checked) return;
            if (_funcTblMenu2.BackColor != Color.LightGreen) return;
            Tbar_Load2.Value = Satur(Convert.ToInt16(e.YOut * 100.0D), Tbar_Load2.Minimum, Tbar_Load2.Maximum);
            Tbar_Load2_ValueChanged(Tbar_Load2, EventArgs.Empty);
            Tbar_Load2_Scroll(Tbar_Load2, EventArgs.Empty);
        }

        #region Задание ИД CANов
        private void CanaM_TextChanged(object sender, EventArgs e)
        {
            if (!_canaM.Text.StartsWith(CanAText)) _canaM.Text = CanAText;
            if (!_canbM.Text.StartsWith(CanBText)) _canbM.Text = CanBText;
            var g = _canaM.Text.Split('=');
            try
            {
                CanAId = byte.Parse(g[1], NumberStyles.HexNumber);
            }
            catch
            {
                _canaM.Text = CanAText;
            }
            //------------------------------------------------------------------------------------------------
            g = _canbM.Text.Split('=');
            try
            {
                CanBId = byte.Parse(g[1], NumberStyles.HexNumber);
            }
            catch
            {
                _canbM.Text = CanBText;
            }
            //-----------------------------------------------------------------------------------------
            _canaM.Text = _canaM.Text.ToUpper();
            _canbM.Text = _canbM.Text.ToUpper();
            Settings.Default.CanAId = CanAId.ToString("x").ToUpper();
            Settings.Default.CanBId = CanBId.ToString("x").ToUpper();
            Settings.Default.Save();
        }
        #endregion
        #endregion Разное

        private void MenuErrorView_Click(object sender, EventArgs e)
        {
            if (_errFrm == null)
            {
                _errFrm = new ErrorsView(new Bitmap(_redLed), new Bitmap(_greenLed))
                {
                    KeyPreview = true
                };
                _errFrm.ClErr += cmd_128;
                _errFrm.Show();
                _errFrm.KeyDown += BControl;
                _errFrm.FormClosed += (tg, jh) =>
                {
                    _errFrm.Dispose();
                    _errFrm = null;
                    uint_128(0, 0, 0, 111);
                };
            }
            else if (_errFrm != null && !_errFrm.Visible)
            {
                _errFrm.Show();
                _errFrm.WindowState = FormWindowState.Normal;
            }
        }

        private void IGBT_Temperatures_Menu_Click(object sender, EventArgs e)
        {
            if (_tempsFrm != null) return;
            _tempsFrm = new TemperatureView { KeyPreview = true };
            _tempsFrm.Show();
            _tempsFrm.KeyDown += BControl;
            _tempsFrm.FormClosed += (h, k) => { _tempsFrm = null; };
        }

        private void _EthLogMenu_Click(object sender, EventArgs e)
        {
            if (_ethLog != null) return;
            _ethLog = new AlStEthLog(true) { KeyPreview = true };
            _ethLog.KeyDown += BControl;
            _ethLog.MsgReceived += x => RxEthM(x.EthMsg);
            _ethLog.Show();
            _ethLog.Closed += (q, a) =>
            {
                _ethLog.Dispose();
                _ethLog = null;
            };
        }

        private void RefreshL()
        {
            const string ext = "*.ini";
            var txFilesPath = $"{Application.StartupPath}\\CanTxFiles";
            var txTFilesPath = $"{Application.StartupPath}\\TEST_TX";
            var rxFilesPath = $"{Application.StartupPath}\\CanRxFiles";
            var rxTFilesPath = $"{Application.StartupPath}\\TEST_RX";
            var canTxFiles = Directory.GetFiles(txFilesPath, ext);
            var canTxTFiles = Directory.GetFiles(txTFilesPath, ext);
            var canRxFiles = Directory.GetFiles(rxFilesPath, ext);
            var canRxTFiles = Directory.GetFiles(rxTFilesPath, ext);
            const string rfs = @"Refr";
            _txDataMenu.DropDownItems.Clear();
            _txDataMenuT.DropDownItems.Clear();
            _rxDataMenu.DropDownItems.Clear();
            _rxDataMenuT.DropDownItems.Clear();
            const string rls = @"Обновить список";
            _txDataMenu.DropDownItems.Add(new ToolStripMenuItem { Name = rfs, Text = rls });
            _txDataMenu.DropDownItems.Add(new ToolStripSeparator());
            _txDataMenuT.DropDownItems.Add(new ToolStripMenuItem { Name = rfs, Text = rls });
            _txDataMenuT.DropDownItems.Add(new ToolStripSeparator());
            _rxDataMenu.DropDownItems.Add(new ToolStripMenuItem { Name = rfs, Text = rls });
            _rxDataMenu.DropDownItems.Add(new ToolStripSeparator());
            _rxDataMenuT.DropDownItems.Add(new ToolStripMenuItem { Name = rfs, Text = rls });
            _rxDataMenuT.DropDownItems.Add(new ToolStripSeparator());

            foreach (var t in canTxFiles)
                _txDataMenu.DropDownItems.Add(new ToolStripMenuItem
                {
                    Name = t,
                    Text =
                        t.Replace(txFilesPath, string.Empty).Replace("\\", string.Empty).Replace(@".ini", string.Empty)
                });
            foreach (var t in canTxTFiles)
                _txDataMenuT.DropDownItems.Add(new ToolStripMenuItem
                {
                    Name = t,
                    Text =
                        t.Replace(txTFilesPath, string.Empty).Replace("\\", string.Empty).Replace(@".ini", string.Empty)
                });
            foreach (var t in canRxFiles)
                _rxDataMenu.DropDownItems.Add(new ToolStripMenuItem
                {
                    Name = t,
                    Text =
                        t.Replace(rxFilesPath, string.Empty).Replace("\\", string.Empty).Replace(@".ini", string.Empty)
                });
            foreach (var t in canRxTFiles)
                _rxDataMenuT.DropDownItems.Add(new ToolStripMenuItem
                {
                    Name = t,
                    Text =
                        t.Replace(rxTFilesPath, string.Empty).Replace("\\", string.Empty).Replace(@".ini", string.Empty)
                });
            foreach (var item in _txDataMenu.DropDownItems.OfType<ToolStripMenuItem>())
                if (item.Name.Equals(rfs)) item.Click += (a, s) => RefreshL();
                else item.Click += (a, s) => OpenTxF(((ToolStripMenuItem)a).Name, ((ToolStripMenuItem)a).Text);
            foreach (var item in _txDataMenuT.DropDownItems.OfType<ToolStripMenuItem>())
                if (item.Name.Equals(rfs)) item.Click += (a, s) => RefreshL();
                else item.Click += (a, s) => OpenTxFt(((ToolStripMenuItem)a).Name, ((ToolStripMenuItem)a).Text);
            foreach (var item in _rxDataMenu.DropDownItems.OfType<ToolStripMenuItem>())
                if (item.Name.Equals(rfs)) item.Click += (a, s) => RefreshL();
                else item.Click += (a, s) => OpenRxF(((ToolStripMenuItem)a).Name, ((ToolStripMenuItem)a).Text);
            foreach (var item in _rxDataMenuT.DropDownItems.OfType<ToolStripMenuItem>())
                if (item.Name.Equals(rfs)) item.Click += (a, s) => RefreshL();
                else item.Click += (a, s) => OpenRxFt(((ToolStripMenuItem)a).Name, ((ToolStripMenuItem)a).Text);
        }

        private void OpenTxF(string n, string t)
        {
            var txDFrm = new TxDataFrm(n, t)
            {
                Name = n,
                Text = $@"[{t}]",
                KeyPreview = true
            };
            txDFrm.SendDataFromTable += a => int_128(a.D1, a.D2, a.D3, a.Cmd);
            txDFrm.SendValuesFromTable += a => { Общак.TxTable[a.Index] = a.Val; };
            var exs = false;
            foreach (var dummy in _txFl.Where(f => f.Name.Equals(txDFrm.Name)))
                exs = true;
            if (!exs)
            {
                _txFl.Add(txDFrm);
                txDFrm.Show();
                txDFrm.KeyDown += BControl;
                txDFrm.Closed += (o, a) => { _txFl.Remove((TxDataFrm)o); };
            }
            Settings.Default.LastCTxn = n;
            Settings.Default.LastCTxt = t;
            Settings.Default.Save();
            if (File.Exists(n)) return;
            RefreshL();
        }

        private void OpenTxFt(string n, string t)
        {
            var txDFrm = new TxDataFrm(n, t) { Name = n, Text = $@"Тест [TX] [{t}]", KeyPreview = true };
            txDFrm.SendDataFromTable += a => int_128(a.D1, a.D2, a.D3, a.Cmd);
            txDFrm.SendValuesFromTable += a => { Общак.TxTable[a.Index] = a.Val; };
            var exs = false;
            foreach (var dummy in _txFlT.Where(f => f.Name.Equals(txDFrm.Name))
                )
                exs = true;
            if (!exs)
            {
                _txFlT.Add(txDFrm);
                txDFrm.Show();
                txDFrm.KeyDown += BControl;
                txDFrm.Closed += (o, a) => { _txFlT.Remove((TxDataFrm)o); };
            }
            Settings.Default.LastCTxTn = n;
            Settings.Default.LastCTxTt = t;
            Settings.Default.Save();
            if (File.Exists(n)) return;
            RefreshL();
        }

        private void OpenRxF(string n, string t)
        {
            var rxDFrm = new RxDataFrm(n, t) { Name = n, Text = $@"Параметры [{t}]", KeyPreview = true };
            var exs = false;
            foreach (var dummy in _rxFl.Where(f => f.Name.Equals(rxDFrm.Name)))
                exs = true;
            if (!exs)
            {
                _rxFl.Add(rxDFrm);
                rxDFrm.Show();
                rxDFrm.KeyDown += BControl;
                rxDFrm.Closed += (o, a) => { _rxFl.Remove((RxDataFrm)o); };
            }
            Settings.Default.LastCRxn = n;
            Settings.Default.LastCRxt = t;
            Settings.Default.Save();
            if (File.Exists(n)) return;
            RefreshL();
        }

        private void OpenRxFt(string n, string t)
        {
            var rxDFrm = new RxDataFrm(n, t) { Name = n, Text = $@"Тест [RX] [{t}]", KeyPreview = true };
            var exs = false;
            foreach (var dummy in _rxFlT.Where(f => f.Name.Equals(rxDFrm.Name)))
                exs = true;
            if (!exs)
            {
                _rxFlT.Add(rxDFrm);
                rxDFrm.Show();
                rxDFrm.KeyDown += BControl;
                rxDFrm.Closed += (o, a) => { _rxFlT.Remove((RxDataFrm)o); };
            }
            Settings.Default.LastCRxTn = n;
            Settings.Default.LastCRxTt = t;
            Settings.Default.Save();
            if (File.Exists(n)) return;
            RefreshL();
        }

        private void Rkf()
        {
            bool b1 = false, b2 = false, b3 = false;
            var path = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "config_switch.h"));
           
            if (!File.Exists(path)) return;

            var f = File.ReadAllLines(path, Encoding.UTF8);
            foreach (var s in f.Where(s => !string.Empty.Equals(s)))
            {
                if (s.TrimStart().Contains("struct kuk1 {"))
                {
                    b1 = true;
                    continue;
                }
                if (b1 && s.TrimStart().Contains("}"))
                {
                    b1 = false;
                    continue;
                }
                if (b1 && s.Contains("Uint16")) _k1.Add(s);
                if (s.TrimStart().Contains("struct kuk2 {"))
                {
                    b2 = true;
                    continue;
                }
                if (b2 && s.TrimStart().Contains("}"))
                {
                    b2 = false;
                    continue;
                }
                if (b2 && s.Contains("Uint16")) _k2.Add(s);
                if (s.TrimStart().Contains("struct kuk3 {"))
                {
                    b3 = true;
                    continue;
                }
                if (b3 && s.TrimStart().Contains("}"))
                {
                    b3 = false;
                    continue;
                }
                if (b3 && s.Contains("Uint16")) _k3.Add(s);
            }
        }

        private void SlInit()
        {
            try
            {
                _splIniL =
                    Directory.GetFiles($"{Application.StartupPath}\\Spl", @"*.ini")
                        .Where(t => !t.Contains("WScope"))
                        .Select(
                            rr =>
                                rr.Replace($"{Application.StartupPath}\\Spl\\", string.Empty)
                                    .Replace(".ini", string.Empty))
                        .ToList();
                foreach (var q in _splIniL.Where(t => !t.StartsWith(@"F"))) F2_Kadr_Name.Items.Add(q);
                F2_Kadr_Name.SelectedIndexChanged += (h, k) =>
                {
                    Settings.Default.LastSplMf = F2_Kadr_Name.SelectedIndex;
                    Settings.Default.Save();
                    F2Kadr_IniFileName = $"{F2_Kadr_Name.SelectedItem.ToString()}.ini";

                    /* СМЕНА НАБОРА НА ХОДУ. Команда прибору раньше не уходила
                     * вовсе, и на шине оставался прежний кадр при новом имени;
                     * окно осциллографа ловило это по длине кадра и просило
                     * Alt+F2, затем F2. Пока лента идёт только для него, делать
                     * это руками незачем. На время записи TapeStart заперт, так
                     * что дописать в .spl чужую раскладку этим нельзя. */
                    if (_tapeOn != 0) TapeStart();
                };
                F2_Kadr_Name.SelectedIndex = Settings.Default.LastSplMf <= F2_Kadr_Name.Items.Count
                    ? Settings.Default.LastSplMf
                    : 0;
                F2Kadr_IniFileName = $"{F2_Kadr_Name.SelectedItem}.ini";
            }
            catch
            {
                F2Kadr_IniFileName = @"ERROR.ini";
                F2_Kadr_Name.BackColor = Color.Salmon;
                F2_Kadr_Name.Items.Clear();
                F2_Kadr_Name.Items.Add("Ошибка");
                F2_Kadr_Name.SelectedIndex = 0;
            }
        }


        private void InfoFrm()
        {
            var hasVersionData = !string.IsNullOrWhiteSpace(KspIdString)
                                 || !string.IsNullOrWhiteSpace(Date)
                                 || !string.IsNullOrWhiteSpace(BuildDate)
                                 || !string.IsNullOrWhiteSpace(BuildTime);
            if (!IdRcvd && !hasVersionData)
            {
                cmd_128(333);
                MessageBox.Show(this, @"Данные о версии ещё не получены от КСП. Запрос отправлен, повторите через 1-2 секунды.",
                    _abmn.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dd = new List<string>();
            var dtt = new DataTable();
            dtt.Columns.Add("PName", typeof(string));
            dtt.Columns.Add("PVal", typeof(string));

            var kuk1 = Kuk1 ?? string.Empty;
            var kuk2 = Kuk2 ?? string.Empty;
            var kuk3 = Kuk3 ?? string.Empty;

            int i;
            for (i = 0; i < _k1.Count && i < kuk1.Length; i++) if (Convert.ToChar(kuk1[i]).Equals('1')) dd.Add(_k1[i]);
            for (i = 0; i < _k2.Count && i < kuk2.Length; i++) if (Convert.ToChar(kuk2[i]).Equals('1')) dd.Add(_k2[i]);
            for (i = 0; i < _k3.Count && i < kuk3.Length; i++) if (Convert.ToChar(kuk3[i]).Equals('1')) dd.Add(_k3[i]);
            var dr = dtt.NewRow();
            dr[0] = "Версия прошивки:";
            dr[1] = string.IsNullOrWhiteSpace(KspIdString) ? "нет данных" : KspIdString;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "Дата сборки:";
            dr[1] = string.IsNullOrWhiteSpace(BuildDate) ? "нет данных" : BuildDate;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "Время сборки:";
            dr[1] = string.IsNullOrWhiteSpace(BuildTime) ? "нет данных" : BuildTime;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = string.Empty;
            dr[1] = string.Empty;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "Main кадр:";
            dr[1] = F2Kadr_IniFileName; // 
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "Fast кадр:";
            dr[1] = spf.FrameString;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "Tooth кадр:";
            dr[1] = sp2.FrameString;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = string.Empty;
            dr[1] = string.Empty;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "KUK1:";
            dr[1] = string.IsNullOrWhiteSpace(Kk1) ? "нет данных" : Kk1;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "KUK2:";
            dr[1] = string.IsNullOrWhiteSpace(Kk2) ? "нет данных" : Kk2;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = "KUK3:";
            dr[1] = string.IsNullOrWhiteSpace(Kk3) ? "нет данных" : Kk3;
            dtt.Rows.Add(dr);
            dr = dtt.NewRow();
            dr[0] = string.Empty;
            dr[1] = string.Empty;
            dtt.Rows.Add(dr);
            foreach (var v in dd)
            {
                var t = v;
                dr = dtt.NewRow();
                var f = t.Split(new[] { @"//" }, StringSplitOptions.None);
                dr[0] = f[0].Replace("Uint16", string.Empty).Replace(":1;", string.Empty).Trim();
                dr[1] = f[1].Trim();
                dtt.Rows.Add(dr);
            }
            if (_infof != null) return;
            _infof = new AlStInfoF(dtt) { Text = _abmn.Text, KeyPreview = true };
            _infof.Show();
            _abmn.Enabled = false;
            _infof.KeyDown += BControl;
            _infof.FormClosed += (l, x) =>
            {
                _infof = null;
                _abmn.Enabled = true;
            };
        }

        private void ProbelCounter()
        {
            probel_markerCnt++;
            if (probel_markerCnt == 0) probel_markerCnt = 1;
            if (probel_markerCnt == 0x54) probel_markerCnt = 0x56;

            fast_markerSet = true;
            F2kadr_markerSet = true;
        }

        private bool Grd()
        {
            return _rden;
        }


        #endregion

    }
}
#endregion