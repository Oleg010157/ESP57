/*
 В этом файле собраны переменные и методы для:
 подключения CAN B адаптера,
 формирования файла быстрого кадра (spf/sp2)
*/

using Peak.Can.Basic;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ICDIBasic
{
    delegate void ReadDelegateHandler();

    public partial class Form1
    {
        #region CAN B — подключение, приём, запись быстрых кадров

        public struct Kadr
        {
            public ushort mboxCnt;  // количество Mbox-ов в кадре
            public string Dir; // путь к папке где находятся временные и выходные файлы записи данных
            public string FrameString; // имя ини файла
            public string tmp; // имя расширения временного файла
            public string spx; // имя расширения результирующего файла
            public bool running; // идёт запись быстрого *.spx файла по CANB     
        };

        Kadr spf = new Kadr
        { Dir = Application.StartupPath + "\\SPF" + "\\spf_data\\", tmp = ".tmpf", spx = ".spf" };
        Kadr sp2 = new Kadr
        { Dir = Application.StartupPath + "\\SPF" + "\\sp2_data\\", tmp = ".tmpt", spx = ".sp2" };

        private byte canb_usbchanel;

        private AutoResetEvent _mReceiveEvent;
        private ushort[] _mHandlesArray;
        public ushort _frL; // кол-во байт в кадре .spf  файла

        private ushort _frCnt;  // счётчик принятых сообщений для поворота стрелки
        private byte[] spf_Frame;  // массив данных быстрового кадра

        private string spf_tmp;
        private string spf_FileName;

        private FileStream spf_FileStream;
        private TPCANTimestamp _msgId500Ts;

        public bool _canOk;
        public bool spf_running;  // останавливаем цикл while в потоке ReadMessage()

        public bool _ToothBeginBuffFill;
        public ushort toothSyncCh = 12; // номер канала по которому синхронизируется график
        public ushort toothFrameCounter; //
        public bool tooth_enGrUpd;
        private ushort toothTrig;

        public bool spf_Tooth_F9 = false;
        public bool sp2_Tooth_F9 = false;
        private ushort kadrTrig;
        public ushort kadrCounter;
        private int kadrCount;
        public ushort kadr = 256;
        public ushort tooth;

        private bool Fcan(byte canId)
        {
            /* в этом методе 
             * проверяется к какому каналу PCAN_USBBUS ( от 1 до 8) подключён can-usb адаптер с номером canId заданным в главной форме
             * пользовательского интерфейса ( UI ). 
             */

            _mReceiveEvent = new AutoResetEvent(false);
            _mHandlesArray = new[]
            {
                          PCANBasic.PCAN_USBBUS1,
                          PCANBasic.PCAN_USBBUS2,
                          PCANBasic.PCAN_USBBUS3,
                          PCANBasic.PCAN_USBBUS4,
                          PCANBasic.PCAN_USBBUS5,
                          PCANBasic.PCAN_USBBUS6,
                          PCANBasic.PCAN_USBBUS7,
                          PCANBasic.PCAN_USBBUS8
                      };

            for (ushort i = 0; i < _mHandlesArray.Length; i++)
            {
                uint iBuffer;
                var stsResult = PCANBasic.GetValue(_mHandlesArray[i], TPCANParameter.PCAN_CHANNEL_CONDITION,
                    out iBuffer,
                    sizeof(uint));

                // проверяем состояние канала PCAN_USBBUS что адаптор подключен к компу и не инициализирован

                if (stsResult != TPCANStatus.PCAN_ERROR_OK || iBuffer != PCANBasic.PCAN_CHANNEL_AVAILABLE) continue;

                PCANBasic.Initialize(_mHandlesArray[i], TPCANBaudrate.PCAN_BAUD_1M);

                if (CAN_Get_ID((byte)_mHandlesArray[i]).Equals(CanBId))
                {
                    canb_usbchanel = (byte)_mHandlesArray[i];
                    _canOk = true;
                    return true; // соответствующий адаптер найден и инициализирован -> возврат в вызывающию прогу.
                }

                // Канал не наш — обязательно освобождаем, иначе он останется висеть
                // инициализированным в classic 1M и другой адаптер (например CAN A/FD)
                // на этом физическом канале не сможет корректно переинициализироваться в FD.
                PCANBasic.Uninitialize(_mHandlesArray[i]);
            }

            _canOk = false;
            MessageBox.Show($@"CAN Адаптер с ID {canId:X} не обнаружен.", @"Ошибка", MessageBoxButtons.OK,
                  MessageBoxIcon.Error);
            return false; // соответствующий адаптер  не найден -> возврат в вызывающию прогу. 
        }

        public static uint CAN_Get_ID(byte h)
        {
            uint iBuffer;
            var stsResult = PCANBasic.GetValue(h, TPCANParameter.PCAN_DEVICE_ID, out iBuffer, sizeof(uint));
            return stsResult == TPCANStatus.PCAN_ERROR_OK ? iBuffer : 0;
        }

        void spf_Start(ref Kadr kdr)  // запуск записи .spf или tooth файла из потока через CANB
        {
            _frL = (ushort)(kdr.mboxCnt * 4);
            spf_Frame = new byte[_frL * 2 + 1];

            try
            {
                if (Fcan(CanBId))   // if CAN-USB adapter number CanBId  exists and is  inited
                {
                    if (!Directory.Exists(kdr.Dir))
                        Directory.CreateDirectory(kdr.Dir);

                    var StartTime = DateTime.Now.ToString("dd.MM.yyyy_HH.mm.ss");
                    spf_tmp = kdr.Dir + StartTime + kdr.tmp;
                    Spl_Hdr(_frL, GetSplInfo(), kdr.FrameString, spf_tmp);

                    spf_FileStream = new FileStream(spf_tmp, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

                    probel_markerCnt = 0;
                    spf_running = true;

                    ReadMessages();
                    _canbM.BackColor = Color.LightGreen;
                }
            }
            catch
            {
                // ignored
            }
        }

        private async void ReadMessages()  // метод работает во вторичном потоке для приёма данных с CANB
        {
            TPCANMsg canMsg;
            TPCANTimestamp canTimeStamp;
            TPCANStatus stsResult;
            bool firstMsg; // признак - это первое сообщение в кадре
            bool Err = false;

            var iBuffer = Convert.ToUInt32(_mReceiveEvent.SafeWaitHandle.DangerousGetHandle().ToInt32());
            stsResult = PCANBasic.SetValue(canb_usbchanel, TPCANParameter.PCAN_RECEIVE_EVENT, ref iBuffer, sizeof(uint));
            if (stsResult != TPCANStatus.PCAN_ERROR_OK) return;
            PCANBasic.Reset(canb_usbchanel);  //  Resets the receive and transmit queues of the PCAN Channel

            await Task.Run(() =>
            {
                while (spf_running && _canOk && IsHandleCreated) //&& !Convert.ToBoolean(stsResult & TPCANStatus.PCAN_ERROR_QRCVEMPTY))      
                {
                    stsResult = PCANBasic.Read(canb_usbchanel, out canMsg, out canTimeStamp);
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK) continue;

                    #region Разбор сообщений по ID , проверка обновления по TimeStamp, получение флагов для Fast кадра

                    firstMsg = false;
                    if (canMsg.ID == 0x10 && !_msgId500Ts.Equals(canTimeStamp)) // если принято первое сообщение кадра  и время не равно времени получения предыдущего первого сообщения
                    {
                        _msgId500Ts = canTimeStamp;
                        spf_FileStream.Write(spf_Frame, 0, spf_Frame.Length);
                        firstMsg = true;
                        Err = false;

                        if (spf_Tooth_F9 == true && f9run && (frm9 != null))
                        {
                            kadrCount++;
                        }
                        if (kadrCount > kadr)
                        {
                            kadrCount = 0;
                        }
                        if (toothFrameCounter > tooth)
                        {
                            toothFrameCounter = 0;
                        }
                    }

                    if (canMsg.ID >= 0x10 && canMsg.ID <= 0x1F && canMsg.LEN == 8)
                    {
                        Buffer.BlockCopy(canMsg.DATA, 0, spf_Frame, ((int)canMsg.ID - 16) * 8 + 1, 8);
                    }
                    else
                    {
                        Err = true;
                    }
                    #endregion

                    if (!firstMsg) continue; // если это  не первое сообщение в кадре то идём к следующему сообщению т.е. к следующему while

                    if (!Err) // если нет ошибки  при приёме сообщения 
                    {
                        spf_Frame[0] = 0x55;
                        if (fast_markerSet) // если есть маркер пробела
                        {
                            fast_markerSet = false;
                            spf_Frame[0] = probel_markerCnt;
                        }

                    }
                    else   // если есть  ошибка при приёме сообщения
                    {
                        spf_Frame[0] = 255;
                    }

                   
                    #region Стрелка поварачивается через каждые 256 кадров
                    _frCnt++;
                    if ((_frCnt & 0xFF) == 0xFF)

                    {
                        Invoke(new Action(() => { Rotate_Arrow(pb_FastKadr, 1); }));

                        frm9?.Invoke(new Action(() =>
                        {

                            Rotate_Arrow(frm9.pictureBox55, 1);

                            frm9.label1.Text = frm9.seconds;
                            frm9.label1.Location = frm9.location;

                            frm9.cbindex1 = frm9.comboBox1.SelectedIndex;
                            frm9.cbindex2 = frm9.comboBox2.SelectedIndex;
                            frm9.cbindex3 = frm9.comboBox3.SelectedIndex;
                            frm9.cbindex4 = frm9.comboBox4.SelectedIndex;
                            frm9.cbindex5 = frm9.comboBox5.SelectedIndex;
                            frm9.cbindex6 = frm9.comboBox6.SelectedIndex;
                            frm9.cbindex7 = frm9.comboBox7.SelectedIndex;
                            frm9.cbindex8 = frm9.comboBox8.SelectedIndex;


                        }
                        ));
                        if (f9del && (frm9 != null))
                        {

                            Invoke(new Action(() =>
                            {
                                frm9.Close();
                            }));
                            frm9 = null;

                        }
                    }
                    #endregion

                    #region Получение и обработка данных для построения графика F9


                    if (f9run && (frm9 != null))
                    {
                        if (sp2_Tooth_F9 == true)
                        {
                            F9buff_sp2(/*frm9.tooths*/);

                        }
                        else if (spf_Tooth_F9 == true && frm9.chBox_GrUpdEn.Checked)
                        {
                            F9buff_spf();

                        }

                    }



                    #endregion
                }
            });  // await Task.Run(() =>

        }

        void F9buff_sp2(/*ushort ths*/)
        {
            var ch = (ushort)frm9.udCh.Value;
            var level = (ushort)frm9.udLvl.Value;
            var range = (ushort)frm9.udRng.Value;
            //ushort tooths = ths;
            toothTrig = //_trig это номер  зуба из текущего кадра

            (ushort)(((spf_Frame[(ch * 2) - 1]) << 8) | (spf_Frame[ch * 2]));

            if (toothTrig >= level && toothTrig <= level + range) //здесь решается с какого зуба начать заполнение масив buff
                _ToothBeginBuffFill = true; //разрешает начать заполнять масив buff

            if (toothFrameCounter < tooth && !tooth_enGrUpd && _ToothBeginBuffFill) // проверка на то что счётчик небыл больше количкства зубьев && показывает что масив buff не заполнен && разрешает начать заполнять масив buff  
            {
                for (ushort i = 0; i < spf_Frame.Length; i++)
                    frm9.buff[toothFrameCounter, i] = spf_Frame[i]; // buff это двумерный масив  у которого первое измерение это количество зубев а второе это 12 параметров (для каждого зуба записивается 12 параметров) 
                toothFrameCounter++; // номер зуба и счётчик того сколько зубьев получили свои 12 параметров
            }
            if (toothFrameCounter == tooth) // поверка на то что количество зубьев получившие свои 12 параметров совпадает с количеством зубьв счётного колеса
            {
                tooth_enGrUpd = true; // показывает что масив buff  заполнен и можно выводить на график
                _ToothBeginBuffFill = false; //запрещает начать заполнять масив buff
                toothFrameCounter = 0;

                if (frm9.chBox_GrUpdEn.Checked) // если Checked то разрешает строить график 
                {
                    frm9.CreateGraph(frm9.ZGct);
                    //                    Invoke(new Action( () =>
                    //                                      { frm9.CreateGraph(frm9.ZGct); } ));
                }

                else
                {
                    frm9.CreateGraph(frm9.ZGct);
                    tooth_enGrUpd = false; // масив buff  не заполнен
                    frm9.toothPreviousDate = DateTime.Now;  // заменяет предыдущее время на текущее,необхолимо для вычислени интервала времени
                }
            }
        }

        void F9buff_spf()
        {
            var ch = (ushort)frm9.udCh.Value;
            var level = (ushort)frm9.udLvl.Value;
            var range = (ushort)frm9.udRng.Value;

            kadrTrig = (ushort)(kadrCount - 1); //_trig это номер  зуба из текущего кадра



            if (kadrTrig >= level && kadrTrig <= level + range) //здесь решается с какого зуба начать заполнение масив buff
            {
                _ToothBeginBuffFill = true; //разрешает начать заполнять масив buff
                kadrCount = 0;
            }

            if (kadrCounter < kadr && !tooth_enGrUpd && _ToothBeginBuffFill) // проверка на то что счётчик небыл больше количкства зубьев && показывает что масив buff не заполнен && разрешает начать заполнять масив buff  
            {
                for (ushort i = 0; i < spf_Frame.Length; i++)
                    frm9.buff[kadrCounter, i] = spf_Frame[i]; // buff это двумерный масив  у которого первое измерение это количество зубев а второе это 12 параметров (для каждого зуба записивается 12 параметров) 
                kadrCounter++; // номер зуба и счётчик того сколько зубьев получили свои 12 параметров
            }
            if (kadrCounter == kadr) // поверка на то что количество зубьев получившие свои 12 параметров совпадает с количеством зубьв счётного колеса
            {
                tooth_enGrUpd = true; // показывает что масив buff  заполнен и можно выводить на график
                _ToothBeginBuffFill = false; //запрещает начать заполнять масив buff
                kadrCounter = 0;

                if (frm9.chBox_GrUpdEn.Checked) // если Checked то разрешает строить график 
                {

                    frm9.CreateGraph(frm9.ZGct);
                }

                else
                {
                    tooth_enGrUpd = false; // масив buff  не заполнен
                    frm9.toothPreviousDate = DateTime.Now;  // заменяет предыдущее время на текущее,необхолимо для вычислени интервала времени
                }
            }
        }

        #region Остановка записи Fast-Tooth
        public void spf_Stop(Kadr kdr)
        {

            try
            {
                pb_FastKadr.Visible = false;  // скрываем стрелку с главной формы
                spf_running = false;           // останавливаем цикл while в потоке ReadMessage()
                cmd_128(103); // команда в ШПСУ на выключение fast и sp2 кадра  // команда в ксп SplMode = Spl_Off и запрещаем прерывание по импульсу от счётного колеса;

                // закрываем spf_tmp файл
                if (spf_FileStream != null)
                {
                    spf_FileStream.Close();
                    spf_FileStream.Dispose();
                }

                spf_FileName = kdr.Dir + DateTime.Now.ToString("dd.MM.yyyy_HH.mm.ss") + kdr.spx;
                File.Move(spf_tmp, spf_FileName);
                Del_Files(kdr.Dir, "*.tmpf");
                //              Del_Files(sp2_Dir, "*.tmpt");

                PCANBasic.Uninitialize(canb_usbchanel);
                spf_Tooth_F9 = false;
                sp2_Tooth_F9 = false;
            
                _canbM.BackColor = Color.Empty;
            }
            catch
            {
                // ignored
            }
        }
        #endregion

        #endregion
    }
}
