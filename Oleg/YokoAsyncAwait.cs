using EasyModbus;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static EasyModbus.ModbusClient;

namespace ICDIBasic
{

    public partial class Form1 : Form
    {
        #region Yokogawa  ModBus

        //      Yokogawa Yoko = new Yokogawa();



        private ushort YokoLAN = 0;
        private Stopwatch yokotick = new Stopwatch();
        ushort yokoTick = 0;
        int yokoCathCount = 0;
        bool firstCatch = false;
        bool firstMesbox = false;
        static bool readYoko = false;

        ModbusClient YmodbusClient;

        private void yokogawaWTToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (yokogawaWTToolStripMenuItem.BackColor == Color.LightGreen)
            {
                readYoko = false;
            }
            else
            {
                YmodbusClient = new ModbusClient("172.16.8.32", 502);

                try
                {

                    YmodbusClient.Connect();

                }
                catch (Exception)
                {
                    var msg = new MyMsgBox("Нет соединения с Yokogawa");
                    msg.Show("Нет соединения с Yokogawa");
                    yokogawaWTToolStripMenuItem.BackColor = Color.Empty;
                    readYoko = false;
                }

                if (YmodbusClient.Connected)
                {
                    yokogawaWTToolStripMenuItem.BackColor = Color.LightGreen;
                    readYoko = true;
                    YokogawaAsync();
                }
                else
                {
                    readYoko = false;
                }
            }
        }


        private async void YokogawaAsync()
        {
            yokoCathCount = 0;
            firstCatch = false;
            firstMesbox = true;      // разрешаем вывод  одного messageboxa

            await Task.Run(() =>
           {

               while (readYoko && !firstCatch)
               {

                   yokotick.Restart();
                   yokoTick++;
                   try
                   {

                       //          Console.WriteLine(" tryID = " + Thread.CurrentThread.ManagedThreadId + "  Tick =  " + yokoTick + " CathCount =  " + yokoCathCount);

                       Yokogawa.Update_count = (ushort)ConvertRegistersToInt(YmodbusClient.ReadInputRegisters(0, 2));

                       Yokogawa.Speed = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.Speed, 2), RegisterOrder.LowHigh);
                       Yokogawa.Torque = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.Torgue, 2), RegisterOrder.LowHigh);
                       Yokogawa.fU5 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.fU5, 2), RegisterOrder.LowHigh);

                       Yokogawa.IrmsA = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.IrmsA, 2), RegisterOrder.LowHigh);
                       Yokogawa.UrmsA = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.UrmsA, 2), RegisterOrder.LowHigh);
                       Yokogawa.PaA = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.PaA, 2), RegisterOrder.LowHigh);
                       Yokogawa.SΣA = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.SΣA, 2), RegisterOrder.LowHigh);
                       Yokogawa.LambdaA = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.LambdaA, 2), RegisterOrder.LowHigh);
                       Yokogawa.fI1 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.fI1, 2), RegisterOrder.LowHigh);

                       Yokogawa.IrmsB = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.IrmsB, 2), RegisterOrder.LowHigh);
                       Yokogawa.UrmsB = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.UrmsB, 2), RegisterOrder.LowHigh);
                       Yokogawa.PaB = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.PaB, 2), RegisterOrder.LowHigh);
                       Yokogawa.SΣB = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.SΣB, 2), RegisterOrder.LowHigh);
                       Yokogawa.LambdaB = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.LambdaB, 2), RegisterOrder.LowHigh);
                       Yokogawa.fI3 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.fI3, 2), RegisterOrder.LowHigh);
                       Yokogawa.F1 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.F1, 2), RegisterOrder.LowHigh);
                       Yokogawa.F2 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.F2, 2), RegisterOrder.LowHigh);
                       Yokogawa.F15 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.F15, 2), RegisterOrder.LowHigh);

                       Yokogawa.P1 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.P1, 2), RegisterOrder.LowHigh);
                       Yokogawa.P2 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.P2, 2), RegisterOrder.LowHigh);
                       Yokogawa.P3 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.P3, 2), RegisterOrder.LowHigh);
                       Yokogawa.P4 = ConvertRegistersToFloat(YmodbusClient.ReadInputRegisters(Yregs.P4, 2), RegisterOrder.LowHigh);

          //             Console.WriteLine(" IrmsSA =  " + Yokogawa.IrmsA + "  IrmsSB  =  " + Yokogawa.IrmsB);
                   }

                   catch
                   {
                       yokoCathCount++;
                       firstCatch = true;
                     //  Console.WriteLine(" cathID = " + Thread.CurrentThread.ManagedThreadId + "  Tick =  " + yokoTick + " CathCount =  " + yokoCathCount);
                   }


                   yokotick.Stop();
                   YokoLAN = (ushort)yokotick.ElapsedMilliseconds;
               //    Console.WriteLine(" endID = " + Thread.CurrentThread.ManagedThreadId + "  Tick =  " + yokoTick + " yokoCathCount =  " + yokoCathCount + "  Time = " + YokoLAN + " ms");
               }

           });

            //-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            //  основной поток действия при сбое или выключении получения данных от Yokogawa 
            YmodbusClient.Disconnect();
            yokogawaWTToolStripMenuItem.BackColor = Color.Empty;
            readYoko = false;
      //      Console.WriteLine(" mainID = " + Thread.CurrentThread.ManagedThreadId + "  Tick =  " + yokoTick + " CathCount =  " + yokoCathCount);

            if (firstCatch)
                if (firstMesbox)
                {
                    firstMesbox = false;  // защёлка обеспечивает вывод только одного messagebox.
                    var msg = new MyMsgBox("Потеря связи с Yokogawa");
                    msg.Show("Потеря связи с Yokogawa");

                }

        }

        #endregion Yokogawa
    }
}

