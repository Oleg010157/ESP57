using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Peak.Can.Basic;
using ZedGraph;


namespace ICDIBasic.Stepulyov
{
    public partial class AStSplFast : Form
    {

      
       
       
        public AStSplFast(bool ft, Image arrow, ushort thsC, ushort boxes, byte canId)
        {
          
            _canid = canId;
            _tooths = thsC;
            FstTth = ft;
            _frL = (ushort)(FstTth ? boxes * 4 : 12);
            _fFrame = new byte[_frL * 2 + 1];
            _fBuff = new byte[_tooths, _fFrame.Length];
            
            InitializeComponent();
            if (!ft) pictureBox55.Image = arrow;
        }



        #region Запуск формы Fast-Tooth и поиск CAN адаптера
        private void Spl_FAST_Shown(object sender, EventArgs e)
        {
     
            if (WindowState != FormWindowState.Minimized) WindowState = FormWindowState.Minimized;
            if (FstTth)
            {
                Hide();
            }
            else
            {
                Spl_Fast_Resize(this, null);
                CreateGraph(ZGct, true);
                Text = @"SPL_Tooth_Test";
            }
            /////
            cb_tm.CheckedChanged += (h, j) => TopMost = cb_tm.Checked;
            button1.Click += (h, j) =>
            {
                if (!FstTth) ImitByCan(102);
            };

            /////
            udLvl.ValueChanged += (a, s) => _syncLvl = (short)udLvl.Value;
            udCh.ValueChanged += (a, s) => _syncCh = (ushort)udCh.Value;
            udRng.ValueChanged += (a, s) => _syncRange = (ushort)udRng.Value;
            cbXY.CheckedChanged += (a, s) =>
            {
                ZGct.GraphPane.XAxis.IsVisible =
                    ZGct.GraphPane.YAxis.IsVisible =
                        ZGct.GraphPane.Legend.IsVisible = cbXY.Checked;
                ZGct.Refresh();
            };
            _formIsShown = true;
            Spl_Start_F(_canid);
        }
        #endregion
        #region Закрытие формы Fast-Tooth
        private void Spl_Fast_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (_canOk)
                {
                    Spl_Stop_F();
                    _canOk = false;
                    PcanBasic.Uninitialize(_mPcanHandle);
                    _formIsShown = false;
                    if (_mReadThread != null)
                    {
                        _mReadThread.Abort();
                        _mReadThread.Join();
                        _mReadThread = null;
                    }
                }
                if (_fStrm != null)
                {
                    _fStrm.Close();
                    _fStrm.Dispose();
                }
            }
            catch
            {
                // ignored
            }
        }
        #endregion
        #region Изменение размеров формы и перерисовка графика
        private void Spl_Fast_Resize(object sender, EventArgs e)
        {
            ZGct.Location = new Point(0, udRng.Location.Y + udRng.Size.Height + 10);
            ZGct.Size = new Size(ClientRectangle.Width,
                ClientRectangle.Height - (udRng.Location.Y + udRng.Size.Height + 10));
            ZGct.Refresh();
        }
        #endregion
        #region Поток чтения данных из CAN по событию
        private void ReadTh()
        {
            var iBuffer = Convert.ToUInt32(_mReceiveEvent.SafeWaitHandle.DangerousGetHandle().ToInt32());
            var stsResult = PcanBasic.SetValue(_mPcanHandle, TpcanParameter.PcanReceiveEvent, ref iBuffer,
                sizeof(uint));
            if (stsResult != TpcanStatus.PcanErrorOk) return;
            while (_canOk && _formIsShown && IsHandleCreated)
            {
                if (!_mReceiveEvent.WaitOne(50)) continue;
           //     Invoke(_mReadDelegate);
                _mReadDelegate();
                Thread.Sleep(1);
            }
        }
        #endregion
       

   

        #region Инициализация формы Fast-Tooth
        private byte _canid;
        #endregion

        #region Запуск записи Fast-Tooth
        private void Fcan()
        {
            _mReadDelegate = ReadMessages;
            _mReceiveEvent = new AutoResetEvent(false);
            _mHandlesArray = new[]
            {
                PcanBasic.PcanUsbbus1,
                PcanBasic.PcanUsbbus2,
                PcanBasic.PcanUsbbus3,
                PcanBasic.PcanUsbbus4,
                PcanBasic.PcanUsbbus5,
                PcanBasic.PcanUsbbus6,
                PcanBasic.PcanUsbbus7,
                PcanBasic.PcanUsbbus8
            };
      
            try
            {
                for (ushort i = 0; i < _mHandlesArray.Length; i++)
                {
                    uint iBuffer;
                    var stsResult = PcanBasic.GetValue(_mHandlesArray[i], TpcanParameter.PcanChannelCondition,
                        out iBuffer,
                        sizeof(uint));
                    if (stsResult != TpcanStatus.PcanErrorOk || iBuffer != PcanBasic.PcanChannelAvailable ||
                        iBuffer == PcanBasic.PcanChannelOccupied) continue;
                    CAN_Init(_mHandlesArray[i]);
                    Thread.Sleep(50);

     
                    if (CAN_Get_ID(_mHandlesArray[i] ).Equals(_canid))
                    {
                        PcanBasic.Uninitialize(_mHandlesArray[i]);
                        _mPcanHandle = _mHandlesArray[i];
                        _canOk = true;
                        break;
                    }
                    PcanBasic.Uninitialize(_mHandlesArray[i]);
                    _canOk = false;
                }
            }
           catch
            {
               _canOk = false;
                MessageBox.Show(@"Ошибка CAN", @"Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    
        #endregion

        #region Переменные
        private delegate void ReadDelegateHandler();
        private byte _mPcanHandle;
        private byte _mr;
        private ReadDelegateHandler _mReadDelegate;
        private AutoResetEvent _mReceiveEvent;
        private Thread _mReadThread;
        private byte[] _mHandlesArray;
        private readonly ushort _frL;
        private readonly ushort _tooths;
        private ushort _frCnt;
        private readonly byte[] _fFrame;
        private readonly byte[,] _fBuff;
        private readonly Form1 form1;
        private string _splFIniFileName;
        private string _splFFileName;
        private string _splFStartTime;
        private string _fileNameFStopTime;
        private string _splDir = Application.StartupPath + "\\Spl";
        //Папка с записанными Spl файлами , Wscope`ом и INI файлом

        public DRotateArrow MainFrmRotArr;
        public Form1.DDelFiles DelF;
        public Form1.DSplHdr CrHdr;
        public Form1.DGetSplInfo GetSplInfo;
        public Form1.Fname SetFname;
        public Form1.DImitByCan ImitByCan;
        public Form1.DCreateSplDir CreateSplDir;
        public Form1.DCloseWscope CloseWscope;
        public Form1.DGetIniFnames GetIniFileNames;
        private FileStream _fStrm;
        private TpcanTimestamp _msgId500Ts;
        private bool _msgId500Upd;
        public bool _canOk;
        private bool _readEnabled;
        private bool _enGrUpd;
        private bool _formIsShown;
        private bool _markerSet;
        private bool _beginBuffFill;
        public bool SplRunningF; // true -fast
        public bool FstTth = true; // true -fast
        private DateTime _previousDt;
        private ushort _syncCh = 12;
        private ushort _frameCounter;
        private ushort _syncRange;
        private short _syncLvl;
        private short _trig;
        #endregion

        #region Основной блок обработки данных Fast-Tooth - делегат из потока
        private void ReadMessages()
        {
            TpcanStatus stsResult;
            do
            {
                TpcanMsg canMsg;
                TpcanTimestamp canTimeStamp;
                stsResult = PcanBasic.Read(_mPcanHandle, out canMsg, out canTimeStamp);
                if (stsResult != TpcanStatus.PcanErrorOk) continue;
                #region Разбор сообщений по ID , проверка обновления по TimeStamp, получение флагов для Fast кадра
                if (canMsg.Id == 0x10 && !_msgId500Ts.Equals(canTimeStamp))
                {
                    _msgId500Ts = canTimeStamp;
                    _fStrm.Write(_fFrame, 0, _fFrame.Length);
                    _msgId500Upd = true;
                }
                var Err = false;

                if (canMsg.Id >= 0x10 && canMsg.Id <= 0x1F && canMsg.Len == 8 && stsResult == TpcanStatus.PcanErrorOk)
                {
                    Err = false;
                    Buffer.BlockCopy(canMsg.Data, 0, _fFrame, ((int)canMsg.Id - 16) * 8 + 1, 8);
                }

                else
                {
                    Err = true;
                }


                #endregion
                if (!_msgId500Upd) continue;
                _msgId500Upd = false;


                if (!Err)
                {
                    _fFrame[0] = 0x55;
                    if (_markerSet)
                    {
                        _markerSet = false;
                        _fFrame[0] = _mr;
                    }

                }
                else
                {
                    _fFrame[0] = 255;
                }



                #region Получение и обработка данных для построения графика
                if (!FstTth)
                {
                    _trig =
                        (short)(((_fFrame[(ushort)(_syncCh * 2) - 1] & 0xFF) << 8) | (_fFrame[_syncCh * 2] & 0xFF));
                    if (_trig >= _syncLvl && _trig <= _syncLvl + _syncRange) _beginBuffFill = true;
                    if (_frameCounter < _tooths && !_enGrUpd && _beginBuffFill)
                    {
                        for (ushort i = 0; i < _fFrame.Length; i++) _fBuff[_frameCounter, i] = _fFrame[i];
                        _frameCounter++;
                    }
                    if (_frameCounter == _tooths)
                    {
                        _enGrUpd = true;
                        _beginBuffFill = false;
                        _frameCounter = 0;
                        if (chBox_GrUpdEn.Checked)
                        {
                            CreateGraph(ZGct, false);
                        }
                        else
                        {
                            _enGrUpd = false;
                            _previousDt = DateTime.Now;
                            label1.Text = $@"Инт. обн: {"--"}.{"---"} s";
                            label1.Location = new Point(pictureBox55.Location.X - label1.Size.Width - 10, 5);
                        }
                    }
                }
                #endregion
                #region Стрелка
                _frCnt++;
                if ((_frCnt & 0xFF) != 0xFF) continue;
                if (!FstTth)
                {
                    Invoke(MainFrmRotArr, (ushort) 2);
                    Rotate_Arrow2(true);
                }
                else
                {
                    Invoke(MainFrmRotArr, (ushort) 1  );
                }
                #endregion
            } while (_readEnabled && _canOk && _formIsShown && IsHandleCreated &&
                         !Convert.ToBoolean(stsResult & TpcanStatus.PcanErrorQrcvempty));
        }

        public void SetMarker(byte m)
        {
            _mr = m;
            _markerSet = true;
        }
        #endregion
        #region Инициализация и получение ID CAN
        private static void CAN_Init(byte h)
        {
            PcanBasic.Initialize(h, TpcanBaudrate.PcanBaud_1M, 0, 0, 0);
        }
        
        public static uint CAN_Get_ID(byte h)
        {
            uint iBuffer;
            var stsResult = PcanBasic.GetValue(h, TpcanParameter.PcanDeviceNumber, out iBuffer, sizeof(uint));
            return stsResult == TpcanStatus.PcanErrorOk ? iBuffer : 0;
        }
        // debug
  
        #endregion
        #region Построение графика
        private void CreateGraph(ZedGraphControl zgc, bool initGr)
        {
            var pn = zgc.GraphPane;
            if (initGr)
            {
                pn.XAxis.Scale.Max = _tooths;
                zgc.IsEnableHZoom = false;
                zgc.IsEnableVZoom = true;
                pn.Fill.Type = FillType.Solid;
                pn.Fill.Color = BackColor;
                pn.Chart.Fill.Type = FillType.Solid;
                pn.Chart.Fill.Color = Color.Black;
                pn.IsFontsScaled = false;
                pn.XAxis.Scale.FontSpec.Size =
                    pn.YAxis.Scale.FontSpec.Size =
                        pn.XAxis.Title.FontSpec.Size =
                            pn.YAxis.Title.FontSpec.Size =
                                pn.Legend.FontSpec.Size =
                                    pn.Title.FontSpec.Size = 10.0f;
                ZGct.GraphPane.Title.IsVisible =
                    ZGct.GraphPane.XAxis.IsVisible =
                        ZGct.GraphPane.YAxis.IsVisible =
                            ZGct.GraphPane.Legend.IsVisible = false;
                pn.Title.Text = @"Тест";
                pn.XAxis.Title.Text = @"Время";
                pn.YAxis.Title.Text = @"Значение";
            }
            var l1 = new PointPairList();
            var l2 = new PointPairList();
            var l3 = new PointPairList();
            var l4 = new PointPairList();
            var l5 = new PointPairList();
            var l6 = new PointPairList();
            var l7 = new PointPairList();
            var l8 = new PointPairList();
            var l9 = new PointPairList();
            var l10 = new PointPairList();
            var l11 = new PointPairList();
            var l12 = new PointPairList();
            for (var i = 0; i < _tooths; i++)
            {
                if (cbc1.Checked) l1.Add(i, (short)(((_fBuff[i, 1] & 0xFF) << 8) | (_fBuff[i, 2] & 0xFF)));
                if (cbc2.Checked) l2.Add(i, (short)(((_fBuff[i, 3] & 0xFF) << 8) | (_fBuff[i, 4] & 0xFF)));
                if (cbc3.Checked) l3.Add(i, (short)(((_fBuff[i, 5] & 0xFF) << 8) | (_fBuff[i, 6] & 0xFF)));
                if (cbc4.Checked) l4.Add(i, (short)(((_fBuff[i, 7] & 0xFF) << 8) | (_fBuff[i, 8] & 0xFF)));
                if (cbc5.Checked) l5.Add(i, (short)(((_fBuff[i, 9] & 0xFF) << 8) | (_fBuff[i, 10] & 0xFF)));
                if (cbc6.Checked) l6.Add(i, (short)(((_fBuff[i, 11] & 0xFF) << 8) | (_fBuff[i, 12] & 0xFF)));
                if (cbc7.Checked) l7.Add(i, (short)(((_fBuff[i, 13] & 0xFF) << 8) | (_fBuff[i, 14] & 0xFF)));
                if (cbc8.Checked) l8.Add(i, (short)(((_fBuff[i, 15] & 0xFF) << 8) | (_fBuff[i, 16] & 0xFF)));
                if (cbc9.Checked) l9.Add(i, (short)(((_fBuff[i, 17] & 0xFF) << 8) | (_fBuff[i, 18] & 0xFF)));
                if (cbc10.Checked) l10.Add(i, (short)(((_fBuff[i, 19] & 0xFF) << 8) | (_fBuff[i, 20] & 0xFF)));
                if (cbc11.Checked) l11.Add(i, (short)(((_fBuff[i, 21] & 0xFF) << 8) | (_fBuff[i, 22] & 0xFF)));
                if (cbc12.Checked) l12.Add(i, (short)(((_fBuff[i, 23] & 0xFF) << 8) | (_fBuff[i, 24] & 0xFF)));
            }
            pn.CurveList.Clear();
            if (cbc1.Checked) pn.AddCurve(cbc1.Text, l1, cbc1.BackColor, SymbolType.None);
            if (cbc2.Checked) pn.AddCurve(cbc2.Text, l2, cbc2.BackColor, SymbolType.None);
            if (cbc3.Checked) pn.AddCurve(cbc3.Text, l3, cbc3.BackColor, SymbolType.None);
            if (cbc4.Checked) pn.AddCurve(cbc4.Text, l4, cbc4.BackColor, SymbolType.None);
            if (cbc5.Checked) pn.AddCurve(cbc5.Text, l5, cbc5.BackColor, SymbolType.None);
            if (cbc6.Checked) pn.AddCurve(cbc6.Text, l6, cbc6.BackColor, SymbolType.None);
            if (cbc7.Checked) pn.AddCurve(cbc7.Text, l7, cbc7.BackColor, SymbolType.None);
            if (cbc8.Checked) pn.AddCurve(cbc8.Text, l8, cbc8.BackColor, SymbolType.None);
            if (cbc9.Checked) pn.AddCurve(cbc9.Text, l9, cbc9.BackColor, SymbolType.None);
            if (cbc10.Checked) pn.AddCurve(cbc10.Text, l10, cbc10.BackColor, SymbolType.None);
            if (cbc11.Checked) pn.AddCurve(cbc11.Text, l11, cbc11.BackColor, SymbolType.None);
            if (cbc12.Checked) pn.AddCurve(cbc12.Text, l12, cbc12.BackColor, SymbolType.None);
            zgc.AxisChange();
            zgc.Invalidate();
            if (DateTime.Now != _previousDt)
            {
                var span = DateTime.Now - _previousDt;
                _previousDt = DateTime.Now;
                label1.Text = $@"Инт. обн: {span.Seconds}.{span.Milliseconds} s";
                label1.Location = new Point(pictureBox55.Location.X - label1.Size.Width - 10, 5);
            }
            _enGrUpd = false;
        }

        private void Rotate_Arrow2(bool en)
        {
            if (en)
            {
                if (!pictureBox55.Visible) pictureBox55.Visible = true;
                pictureBox55.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                pictureBox55.Refresh();
            }
            else
            {
                pictureBox55.Visible = false;
            }
        }
        #endregion
    }
}