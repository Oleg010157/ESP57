using System.Windows.Forms;
using Peak.Can.Basic;

using System;
using System.Threading.Tasks;


namespace ICDIBasic
{
    public partial class Form1

    {
        // метод поиска и подключения CAN адаптера к одному из свободных USB-портов: 
        private async void ReadCANAasync()
        {
            TpcanStatus stsResult;
            await Task.Run(() =>
            {
                while (_rden)
                {
                    stsResult = PcanBasic.Read(Ch, out var canMsg, out var _);
                if (stsResult != TpcanStatus.PcanErrorOk) continue;

                var msg = canMsg;
                var mId = msg.Id;
                ushort comand;
                var d = new ushort[4];
                d[0] = BitConverter.ToUInt16(new[] { msg.Data[1], msg.Data[0] }, 0);
                d[1] = BitConverter.ToUInt16(new[] { msg.Data[3], msg.Data[2] }, 0);
                d[2] = BitConverter.ToUInt16(new[] { msg.Data[5], msg.Data[4] }, 0);
                d[3] = BitConverter.ToUInt16(new[] { msg.Data[7], msg.Data[6] }, 0);
                comand = d[0];

                    if (mId == 11)
                    {

                        _newGenF?.GData(msg);
                        _tempsFrm?.Invoke(new Action(() => _tempsFrm.SetTemps(msg)));

                        foreach (var t in _txFl) Invoke(new Action(() => t.RxFb(msg.Data)));
                        foreach (var t in _txFlT) Invoke(new Action(() => t.RxFb(msg.Data)));
                        _adcInfoKsp?.Ut.Invoke(msg.Data);
                        _igbtTestF?.FeedBack(msg);
                        Invoke(new Action(() => Mbox13Rx(msg))); 
                    }





                }
            });
            


        }

    }
}
