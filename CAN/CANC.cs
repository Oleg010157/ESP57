using Peak.Can.Basic;
using System;
using System.Drawing;

namespace ICDIBasic
{
    public partial class Form1
    {
        #region CAN C Новый
        private ACtl _newCanc;
        private void ConnectCANCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_newCanc == null)
            {
                _newCanc = new ACtl(true);
                // Вызываем конструктор класса, параметры байт - ID CAN адаптера, true=1 Mbit, false - 250k
                _newCanc.CanO += _newCANC_CanO; // событие CAN успешно подключен
                _newCanc.CanD += _newCANC_CanD; // событие CAN успешно отключен 
                _newCanc.CanE += _newCANC_CanE; // событие ошибка CANа 
                _newCanc.Prms = ParseMsg; // привязка делегата функции обработки  принятых сообщений

                //    _newCANC.Send(10, 10, 10, 10, TPCANMessageType.PCAN_MESSAGE_STANDARD, 100); // пример функции отправки стандартного сообщения
                //     _newCANC.Send(10, 10, 10, 10, TPCANMessageType.PCAN_MESSAGE_EXTENDED, 1000); // пример функции отправки расширенного  сообщения
            }
            if (_newCanc != null)
                _newCanc.CanF(0xc); //вызов функции подключения адаптера  !!! повторный вызов -> отключение адаптера
        }
        private void _newCANC_CanE(object o, EventArgs a)
        {
            ConnectCANCToolStripMenuItem.BackColor = Color.Salmon;
            ConnectCANCToolStripMenuItem.Text = @"Ошибка CAN C";
        }
        private void _newCANC_CanD(object o, EventArgs e)
        {
            ConnectCANCToolStripMenuItem.BackColor = SystemColors.Control;
            ConnectCANCToolStripMenuItem.Text = @"Подключить CAN C";
        }
        private void _newCANC_CanO(object o, EventArgs a)
        {
            ConnectCANCToolStripMenuItem.BackColor = Color.LightGreen;
            ConnectCANCToolStripMenuItem.Text = @"Отключить CAN C";
        }
        private void ParseMsg(TpcanMsg msg)
        {
            var s1 = BitConverter.ToInt16(new[] { msg.Data[1], msg.Data[0] }, 0);
            var s2 = BitConverter.ToInt16(new[] { msg.Data[3], msg.Data[2] }, 0);
            var s3 = BitConverter.ToInt16(new[] { msg.Data[5], msg.Data[4] }, 0);
            var s4 = BitConverter.ToInt16(new[] { msg.Data[7], msg.Data[6] }, 0);
            var u1 = (ushort)s1;
            var u2 = (ushort)s2;
            var u3 = (ushort)s3;
            var u4 = (ushort)s4;
            switch (msg.Id)
            {
                case 100:
                    Canc.I1A = s1;
                    Canc.I1B = s2;
                    Canc.I1C = s3;
                    Canc.I1N = s4;
                    break;
                case 101:
                    Canc.V1A = s1;
                    Canc.V1B = s2;
                    Canc.V1C = s3;
                    Canc.Udc = s4;
                    break;
                case 102:
                    Canc.Pk1Id = s1;
                    Canc.Pk1Iq = s2;
                    Canc.Pk1Im = s3;
                    Canc.Flags = u4;
                    break;
                case 103:
                    Canc.I2A = s1;
                    Canc.I2B = s2;
                    Canc.I2C = s3;
                    Canc.I2N = s4;
                    break;
                case 104:
                    Canc.V2A = s1;
                    Canc.V2B = s2;
                    Canc.V2C = s3;
                    Canc.F1Act = s4;
                    break;
                case 105:
                    Canc.Pk2Id = s1;
                    Canc.Pk2Iq = s2;
                    Canc.Pk2Im = s3;
                    Canc.F2Act = s4;
                    break;
                case 106:
                    Canc.R1Fr = s1;
                    Canc.R2Fr = s2;
                    Canc.R1Pr = s3;
                    Canc.R2Pr = s4;
                    break;
                case 107:
                    Canc.R1Dangl = s1;
                    Canc.R2Dangl = s2;
                    Canc.A1Qep = s3;
                    Canc.A2Qep = s4;
                    break;
                case 108:
                    Canc.Timefr = u1;
                    Canc.K60Cnt = u2;
                    Canc.Cntfr = u3;
                    Canc.IsrTicker = u4;
                    //               send_Int16_129(Canc.R1Fr, Canc.R2Fr, Canc.Udc, (ushort) Canc.Pk1Im);
                    // пример функции отправки стандартного сообщения
                    break;
            }

            //    _newCANC.Send(10, 10, 10, 10, TPCANMessageType.PCAN_MESSAGE_STANDARD, 100); // пример функции отправки стандартного сообщения
        }
        #endregion  CAN C Новый
    }
}