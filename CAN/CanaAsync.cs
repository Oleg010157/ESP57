using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ICDIBasic.Form1;

namespace ICDIBasic
{
    public partial class Form1
    {
        private void F2_RxData(TpcanMsg msg)  // программа работает в рабочем ассинхронном потоке
        {
         //   if (IsDisposed) return;
          //  var mId = msg.Id;
            //      ushort comand; 
            //      var d = new ushort[4];
            //      d[0] = BitConverter.ToUInt16(new[] { msg.Data[1], msg.Data[0] }, 0);  
            //      d[1] = BitConverter.ToUInt16(new[] { msg.Data[3], msg.Data[2] }, 0);
            //     d[2] = BitConverter.ToUInt16(new[] { msg.Data[5], msg.Data[4] }, 0);
            //      d[3] = BitConverter.ToUInt16(new[] { msg.Data[7], msg.Data[6] }, 0);
            //      comand = d[0];

            if (msg.Id == 2)
            {

          //      _newGenF?.GData(msg);
          //      _tempsFrm?.Invoke(new Action(() => _tempsFrm.SetTemps(msg)));

          //      foreach (var t in _txFl) Invoke(new Action(() => t.RxFb(msg.Data)));
          //      foreach (var t in _txFlT) Invoke(new Action(() => t.RxFb(msg.Data)));
          //      _adcInfoKsp?.Ut.Invoke(msg.Data);
          //      _igbtTestF?.FeedBack(msg);
                Mbox13Rx(msg);
            }
            else
            {
                Kadr60Rx(msg);
                if (!spl_running) return;

                // если задан режим вывода кадра по CANA
                // 1. обязательно должно быть изменение счётчика кадров k60Cnt
                if (_cntRef == ccs.k60Cnt) return;

                if (_rden)
                    try
                    {

                        F60GetData();

                        var t = new ushort[FrlM];
                        Array.Copy(spL, t, FrlM);
                        foreach (var f in _rxFl) Invoke(new Action(() => { f.RxD(t); }));
                        foreach (var f in _rxFlT) Invoke(new Action(() => { f.RxD(t); }));

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

                        using (var stream = new FileStream(spl_tmp, FileMode.Append))
                        {
                            stream.Write(frame, 0, frame.Length);
                        }
                        BeginInvoke(new Action(() =>
                        {
                            if (!spl_running)
                            {
                                pbMainKadr.Visible = pbMainKadr.Enabled = false;
                                return;
                            }

                            _ract++;
                            if (_ract <= 10) return;
                            _ract = 0;
                            pbMainKadr.Visible = pbMainKadr.Enabled = true;
                            pbMainKadr.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            pbMainKadr.Refresh();
                        }));
                    }
                    catch
                    {
                        spl_Stop();
                        Del_Files(spl_Dir, "*.tmpm");
                    }
                _cntRef = ccs.k60Cnt;
            }
        }
    }
}
