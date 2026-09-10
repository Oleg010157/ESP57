// Decompiled with JetBrains decompiler
// Type: CANTxDataTable.TxDataFrm
// Assembly: CANTxDataTable, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 910B6E21-7299-4613-9774-4ABC91FF4F8C
// Assembly location: C:\Users\olegp\Desktop\WORK\37\SHARP\Stepulyov\CANTxDataTable.dll

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Diagnostics;


#nullable disable
namespace CANTxDataTable
{
    public class TxDataFrm : Form
    {
        private const string Vname = "Имя";
        private const string Vmin = "Минимум";
        private const string Vmax = "Максимум";
        private const string Vcmd = "Команда";
        private const string Vval = "Знач.";
        private const string Vmod = "М";
        private const string Vmod2 = "Мод_";
        private const string Vcolor = "Цвет";
        private const string Vnum = "Номер";
        private const string SStart = "[Settings]";
        private const string SEnd = "[/Settings]";
        private readonly string _fp;
        private DataRow _dr;
        private DataTable _dt = new DataTable();
        private bool _en;
        private IContainer components = (IContainer)null;
        private DataGridView dg;
        private Button button1;
        private Button button2;

        // Одноразовый таймер отложенного перечитывания таблицы, см. Send().
        private readonly Timer _refreshT = new Timer();

        public TxDataFrm(string n, string t)
        {
            this.InitializeComponent();
            this._fp = n;

            this._refreshT.Interval = 300;
            this._refreshT.Tick += (s, e) =>
            {
                this._refreshT.Stop();
                this.ReadAll();
            };
        }

       

        public event TxDataFrm.DSendD SendDataFromTable;

        public event TxDataFrm.DSendD SendValuesFromTable;

        private void ReadIniFile()
        {
            this._dt = new DataTable();
            this._dt.Columns.Add("Имя", typeof(string));
            this._dt.Columns.Add("Минимум", typeof(short));
            this._dt.Columns.Add("Максимум", typeof(short));
            this._dt.Columns.Add("Команда", typeof(ushort));
            this._dt.Columns.Add("Знач.", typeof(short));
            this._dt.Columns.Add("М", typeof(bool));
            this._dt.Columns.Add("Мод_", typeof(ushort));
            this._dt.Columns.Add("Цвет", typeof(ushort));
            this._dt.Columns.Add("Номер", typeof(ushort));
            foreach (string[] strArray in ((IEnumerable<string>)File.ReadAllLines(this._fp)).TakeWhile<string>((System.Func<string, bool>)(s => !s.Contains("[Hidden]"))).Where<string>((System.Func<string, bool>)(s => !s.StartsWith("//") && s.Length >= 5 && !s.Equals(string.Empty) && !s.Equals("[Active]"))).Select<string, string>((System.Func<string, string>)(sb => sb.Replace(" ", string.Empty).Replace("\t", string.Empty).Replace(",", "|").Replace("=", "|").Replace("/", "|"))).ToList<string>().Select<string, string[]>((System.Func<string, string[]>)(i => i.Split('|'))))
            {
                this._dr = this._dt.NewRow();
                try
                {
                    this._dr["Имя"] = (object)Convert.ToString(strArray[0]);
                    this._dr["Минимум"] = (object)Convert.ToInt16(strArray[1]);
                    this._dr["Максимум"] = (object)Convert.ToInt16(strArray[2]);
                    this._dr["Команда"] = (object)Convert.ToUInt16(strArray[3]);
                    this._dr["Знач."] = (object)0;
                    this._dr["М"] = (object)false;
                    this._dr["Мод_"] = (object)Convert.ToUInt16(strArray[4]);
                    this._dr["Цвет"] = (object)Convert.ToUInt16(strArray[5]);
                    this._dr["Номер"] = (object)Convert.ToUInt16(strArray[6]);
                }
                catch
                {
                    this._dr["Имя"] = (object)Convert.ToString("Ошибка в ini файле");
                    this._dr["Минимум"] = this._dr["Максимум"] = this._dr["Команда"] = this._dr["Знач."] = this._dr["Мод_"] = this._dr["Цвет"] = this._dr["Номер"] = (object)0;
                    this._dr["М"] = (object)false;
                }
                finally
                {
                    this._dt.Rows.Add(this._dr);
                }
            }
            this.dg.DataSource = (object)this._dt;
            DataGridViewButtonColumn viewButtonColumn1 = new DataGridViewButtonColumn();
            viewButtonColumn1.HeaderText = "-";
            viewButtonColumn1.Text = "-";
            viewButtonColumn1.Name = "bdecr";
            viewButtonColumn1.UseColumnTextForButtonValue = true;
            viewButtonColumn1.Width = 20;
            viewButtonColumn1.ReadOnly = true;
            DataGridViewButtonColumn viewButtonColumn2 = viewButtonColumn1;
            DataGridViewButtonColumn viewButtonColumn3 = new DataGridViewButtonColumn();
            viewButtonColumn3.HeaderText = "+";
            viewButtonColumn3.Text = "+";
            viewButtonColumn3.Name = "btn";
            viewButtonColumn3.UseColumnTextForButtonValue = true;
            viewButtonColumn3.Width = 20;
            viewButtonColumn3.ReadOnly = true;
            DataGridViewButtonColumn viewButtonColumn4 = viewButtonColumn3;
            this.dg.Columns.Insert(5, (DataGridViewColumn)viewButtonColumn2);
            this.dg.Columns.Insert(6, (DataGridViewColumn)viewButtonColumn4);
            this.dg.Columns[0].ReadOnly = this.dg.Columns[1].ReadOnly = this.dg.Columns[2].ReadOnly = this.dg.Columns[9].ReadOnly = this.dg.Columns[10].ReadOnly = this.dg.Columns[8].ReadOnly = this.dg.Columns[3].ReadOnly = true;
            this.dg.Columns[4].ReadOnly = this.dg.Columns[7].ReadOnly = false;
            this.dg.Columns[0].Visible = true;
            this.dg.Columns[1].Visible = this.dg.Columns[2].Visible = this.dg.Columns[9].Visible = this.dg.Columns[10].Visible = this.dg.Columns[8].Visible = this.dg.Columns[3].Visible = false;
            foreach (DataGridViewColumn column in (BaseCollection)this.dg.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            this.dg.Columns["Имя"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            this.dg.Columns["Знач."].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            this.dg.Columns["М"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        }

        private void TxDataFrm_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this._fp))
                return;
            this.dg.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            this.dg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.dg.AllowUserToAddRows = this.dg.RowHeadersVisible = false;
            this.dg.AllowUserToResizeRows = false;
            this.dg.CellMouseDown += (DataGridViewCellMouseEventHandler)((y, m) => this._en = true);
            this.dg.CellMouseUp += new DataGridViewCellMouseEventHandler(this.dg_CellMouseUp);
            this.dg.CellValueChanged += new DataGridViewCellEventHandler(this.dg_CellValueChanged);
            this.ReadIniFile();
            Rectangle screen = this.RectangleToScreen(this.ClientRectangle);
            this.dg.Height = this.dg.Rows.Count * this.dg.RowTemplate.Height + this.dg.ColumnHeadersHeight + 10;
            int height = this.dg.Location.Y + this.dg.Height + (screen.Top - this.Top);
            int width = this.dg.Columns[0].Width + this.dg.Columns[4].Width + this.dg.Columns[5].Width + this.dg.Columns[6].Width + this.dg.Columns[7].Width + 20;
            this.dg.Width = width;
            this.MaximumSize = new Size(width, height);
            this.MinimumSize = new Size(width, height);
            this.Height = height;
            this.Width = width;
            this.dg.DataError += (DataGridViewDataErrorEventHandler)((a, b) => this.dg.CancelEdit());
            this.button1.Click += (EventHandler)((a, z) =>
            {
                this.button1.Enabled = this.button2.Enabled = false;
                this.LdVl();
                this.button1.Enabled = this.button2.Enabled = true;
            });
            this.button2.Click += (EventHandler)((a, z) =>
            {
                this.button1.Enabled = this.button2.Enabled = false;
                this.SvVl();
                this.button1.Enabled = this.button2.Enabled = true;
            });
            this.Shown += (EventHandler)((j, k) =>
            {
                for (int index = 0; index < this._dt.Rows.Count; ++index)
                {
                    ushort uint16 = Convert.ToUInt16(this._dt.Rows[index]["Цвет"]);
                    Color color = Color.Black;
                    if (uint16 == (ushort)1)
                        color = Color.Red;
                    if (uint16 == (ushort)2)
                        color = Color.Orange;
                    if (uint16 == (ushort)3)
                        color = Color.YellowGreen;
                    if (uint16 == (ushort)4)
                        color = Color.Green;
                    if (uint16 == (ushort)5)
                        color = Color.Blue;
                    if (uint16 == (ushort)6)
                        color = Color.DarkBlue;
                    if (uint16 == (ushort)7)
                        color = Color.Violet;
                    this.dg.Rows[index].Cells[0].Style.ForeColor = color;
                    this.Send(index, (short)0, false);
                }
                this.dg.SelectionChanged += (EventHandler)((a, s) => this.dg.ClearSelection());
                this.dg.ClearSelection();
                this.dg.Refresh();
            });
        }

        // Разрешено ли править значение строки.
        //
        // Раньше здесь было "Мод_ && М || !Мод_", то есть при Мод_ = 1 правка
        // запиралась снятой галочкой "М". Для параметров, где "М" - длящееся
        // состояние (зафиксировать угол, включить строб), это осмысленно.
        //
        // Но "М" бывает и однократным действием: в Offsets.ini галочка у строки
        // канала запускает его автокалибровку и тут же сбрасывается - прошивка
        // отвечает нулём. Действие не может служить замком для правки: чтобы
        // поправить ноль руками, пришлось бы сперва запустить калибровку,
        // которая этот ноль перезапишет.
        //
        // Поэтому Мод_ теперь означает ровно одно - галочка живая, - а правка
        // значения разрешена всегда. Побочно это открыло правку значений без
        // установки "М" в STROB.ini и rg_fix.ini; по прошивке безобидно:
        // записанные rg.Out и rgStrob лежат без дела, пока их режим выключен.
        private bool Wren(int i)
        {
            return true;
        }

        private void dg_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (!this._en)
                return;
            if ((e.ColumnIndex == 4 || e.ColumnIndex == 5 || e.ColumnIndex == 6 || e.ColumnIndex == 7) && e.RowIndex >= 0)
            {
                short int16_1 = Convert.ToInt16(this._dt.Rows[e.RowIndex]["Минимум"]);
                short int16_2 = Convert.ToInt16(this._dt.Rows[e.RowIndex]["Максимум"]);
                short int16_3 = Convert.ToInt16(this._dt.Rows[e.RowIndex]["Знач."]);
                bool boolean1 = Convert.ToBoolean(this._dt.Rows[e.RowIndex]["М"]);
                bool boolean2 = Convert.ToBoolean(this._dt.Rows[e.RowIndex]["Мод_"]);
                switch (e.ColumnIndex)
                {
                    case 4:
                        if (this.Wren(e.RowIndex))
                        {
                            this.dg.BeginEdit(false);
                            break;
                        }
                        this.dg.CancelEdit();
                        break;
                    case 5:
                        if (this.Wren(e.RowIndex))
                        {
                            if ((int)int16_3 > (int)int16_1)
                                --int16_3;
                            this._dt.Rows[e.RowIndex]["Знач."] = (object)int16_3;
                            this.Send(e.RowIndex, (short)1, false);
                            break;
                        }
                        break;
                    case 6:
                        if (this.Wren(e.RowIndex))
                        {
                            if ((int)int16_3 < (int)int16_2)
                                ++int16_3;
                            this._dt.Rows[e.RowIndex]["Знач."] = (object)int16_3;
                            this.Send(e.RowIndex, (short)1, false);
                            break;
                        }
                        break;
                    case 7:
                        if (boolean2)
                            this.Send(e.RowIndex, (short)2, !boolean1);
                        this.dg.CancelEdit();
                        break;
                }
            }
            else if (e.ColumnIndex == 4 && e.RowIndex >= 0)
                this.Send(e.RowIndex, (short)0, false);
            else if (e.ColumnIndex == 4 && e.RowIndex == -1)
            {
                for (int r = 0; r < this._dt.Rows.Count; ++r)
                    this.Send(r, (short)0, false);
            }
            this.dg.Refresh();
            this._en = false;
        }

        private void dg_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 4 && e.ColumnIndex != 7)
                return;
            if ((int)Convert.ToInt16(this._dt.Rows[e.RowIndex]["Знач."]) > (int)Convert.ToInt16(this._dt.Rows[e.RowIndex]["Максимум"]))
                this._dt.Rows[e.RowIndex]["Знач."] = (object)Convert.ToInt16(this._dt.Rows[e.RowIndex]["Максимум"]);
            if ((int)Convert.ToInt16(this._dt.Rows[e.RowIndex]["Знач."]) < (int)Convert.ToInt16(this._dt.Rows[e.RowIndex]["Минимум"]))
                this._dt.Rows[e.RowIndex]["Знач."] = (object)Convert.ToInt16(this._dt.Rows[e.RowIndex]["Минимум"]);
            if (!this.Wren(e.RowIndex))
                return;
            this.Send(e.RowIndex, (short)1, false);
        }

        private void Send(int r, short mf, bool m)
        {
            this.dg.Rows[r].Cells[4].Style.BackColor = Color.Red;
            this.dg.Rows[r].Cells[4].Style.ForeColor = Color.Yellow;
            if (Convert.ToBoolean(this._dt.Rows[r]["Мод_"]))
            {
                this.dg.Rows[r].Cells[7].Style.BackColor = Color.Red;
                this.dg.Rows[r].Cells[7].Style.ForeColor = Color.Yellow;
            }
            else
            {
                this.dg.Rows[r].Cells[7].Style.BackColor = Color.Gray;
                this.dg.Rows[r].Cells[7].Style.ForeColor = Color.Gray;
            }
            this.dg.Refresh();
            int num;
            switch (mf)
            {
                case 1:
                    num = 3333;
                    break;
                case 2:
                    num = 5555;
                    break;
                default:
                    num = 0;
                    break;
            }
            TxDataFrm.DSendD sendDataFromTable = this.SendDataFromTable;
            if (sendDataFromTable == null)
                return;
            sendDataFromTable(new TxDataFrm.SendEventArgs()
            {
                D1 = (int)Convert.ToInt16(this._dt.Rows[r]["Знач."]),
                D2 = num,
                D3 = (int)Convert.ToInt16(m),
                Cmd = Convert.ToUInt16(this._dt.Rows[r]["Команда"])
            });

            // После любой команды, которая что-то меняет в приборе, перечитать
            // всю таблицу - как при щелчке по заголовку "Знач.".
            //
            // Одной строкой дело не ограничивается: сброс к заводским меняет
            // сразу все каналы и контрольную сумму, калибровка - ноль канала и
            // ту же сумму, ручная правка - сумму. Без перечитывания в таблице
            // осталось бы то, что оператор ввёл, а не то, что в приборе.
            //
            // Задержка нужна, потому что ответ приходит раньше, чем действие
            // заканчивается: калибровка занимает около 51 мс. Таймер
            // перезапускается на каждой посылке, поэтому серия правок подряд
            // даёт одно перечитывание в конце, а не по одному на каждую.
            if (num != 0)
            {
                this._refreshT.Stop();
                this._refreshT.Start();
            }
        }

        // Перечитать значения всех строк. То же, что щелчок по заголовку
        // столбца "Знач.": посылка с D2 = 0 - это чтение.
        private void ReadAll()
        {
            for (int r = 0; r < this._dt.Rows.Count; ++r)
                this.Send(r, (short)0, false);
        }

      
        public void RxFb(ushort cmd, short val, ushort flagRaw)
        {
            bool matched = false;
            bool flag = (flagRaw != 0);  // или flagRaw == 1 — зависит от твоей спецификации

            for (int index = 0; index < this._dt.Rows.Count; ++index)
            {
                if ((int)Convert.ToUInt16(this._dt.Rows[index]["Команда"]) == (int)cmd)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                return;

            for (int index = 0; index < this._dt.Rows.Count; ++index)
            {
                if ((int)Convert.ToUInt16(this._dt.Rows[index]["Команда"]) == (int)cmd)
                {
                    this.Sg(index);

                    short min = Convert.ToInt16(this._dt.Rows[index]["Минимум"]);
                    short max = Convert.ToInt16(this._dt.Rows[index]["Максимум"]);
                    ushort paramNum = Convert.ToUInt16(this._dt.Rows[index]["Номер"]);

                    this._dt.Rows[index]["Знач."] = val >= min
                        ? (val <= max ? (object)val : (object)max)
                        : (object)min;

                    if (Convert.ToBoolean(this._dt.Rows[index]["Мод_"]))
                        this._dt.Rows[index]["М"] = (object)flag;
                    else
                        this._dt.Rows[index]["М"] = (object)false;

                    TxDataFrm.DSendD sendValuesFromTable = this.SendValuesFromTable;
                    if (sendValuesFromTable != null)
                    {
                        sendValuesFromTable(new TxDataFrm.SendEventArgs()
                        {
                            Index = paramNum,
                            Val = val
                        });
                    }
                }
            }

            this.dg.Refresh();
        }
        public void RxFb(byte[] data)
        {
            ushort cmd = BitConverter.ToUInt16(new[] { data[0], data[1] }, 0);
            short val = BitConverter.ToInt16(new[] { data[2], data[3] }, 0);
            ushort flag = BitConverter.ToUInt16(new[] { data[6], data[7] }, 0);
            RxFb(cmd, val, flag);
        }



        private void Sg(int i)
        {
            this.dg.Rows[i].Cells[4].Style.BackColor = Color.LightGreen;
            this.dg.Rows[i].Cells[4].Style.ForeColor = Color.Black;
            if (Convert.ToBoolean(this._dt.Rows[i]["Мод_"]))
            {
                this.dg.Rows[i].Cells[7].Style.BackColor = Color.LightGreen;
                this.dg.Rows[i].Cells[7].Style.ForeColor = Color.Black;
            }
            else
                this.dg.Rows[i].Cells[7].Style.BackColor = this.dg.Rows[i].Cells[7].Style.ForeColor = Color.Gray;
        }

        private void LdVl()
        {
            try
            {
                List<TxDataFrm.Stc> stcList = new List<TxDataFrm.Stc>();
                foreach (string readAllLine in File.ReadAllLines(this._fp))
                {
                    if (readAllLine.StartsWith("[Settings]") && readAllLine.EndsWith("[/Settings]"))
                        stcList = (List<TxDataFrm.Stc>)new XmlSerializer(typeof(List<TxDataFrm.Stc>)).Deserialize((TextReader)new StringReader(Encoding.UTF8.GetString(Convert.FromBase64String(readAllLine.Replace("[Settings]", string.Empty).Replace("[/Settings]", string.Empty)))));
                }
                for (short index = 0; (int)index < this._dt.Rows.Count; ++index)
                {
                    foreach (TxDataFrm.Stc stc in stcList)
                    {
                        if (stc.Name.Equals(this._dt.Rows[(int)index]["Имя"]))
                        {
                            this._dt.Rows[(int)index]["Знач."] = (object)stc.Val;
                            this._dt.Rows[(int)index]["М"] = (object)stc.Flag;
                            this.Send((int)index, (short)1, false);
                            this.Send((int)index, (short)2, stc.Flag);
                        }
                    }
                }
                this.dg.Refresh();
            }
            catch
            {
            }
        }

        private void SvVl()
        {
            try
            {
                List<TxDataFrm.Stc> o = new List<TxDataFrm.Stc>();
                o.Clear();
                for (short index = 0; (int)index < this._dt.Rows.Count; ++index)
                    o.Add(new TxDataFrm.Stc()
                    {
                        Name = Convert.ToString(this._dt.Rows[(int)index]["Имя"]),
                        Val = Convert.ToInt16(this._dt.Rows[(int)index]["Знач."]),
                        Flag = Convert.ToBoolean(this._dt.Rows[(int)index]["М"])
                    });
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<TxDataFrm.Stc>));
                TextWriter textWriter = (TextWriter)new StringWriter();
                xmlSerializer.Serialize(textWriter, (object)o);
                string contents1 = string.Format("{0}{1}{2}", (object)"[Settings]", (object)Convert.ToBase64String(Encoding.UTF8.GetBytes(textWriter.ToString())), (object)"[/Settings]");
                string[] contents2 = File.ReadAllLines(this._fp);
                bool flag = false;
                for (short index = 0; (int)index < contents2.Length; ++index)
                {
                    if (contents2[(int)index].StartsWith("[Settings]") && contents2[(int)index].EndsWith("[/Settings]"))
                    {
                        contents2[(int)index] = contents1;
                        flag = true;
                    }
                }
                File.WriteAllLines(this._fp, contents2);
                if (flag)
                    return;
                File.AppendAllText(this._fp, contents1);
            }
            catch
            {
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!TxDataFrm.CloseCancel())
                return;
            this.SvVl();
        }

        public static bool CloseCancel()
        {
            return MessageBox.Show("Сохранить параметры ?", "Сохранение параметров", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.components != null)
                this.components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.dg = new DataGridView();
            this.button1 = new Button();
            this.button2 = new Button();
            ((ISupportInitialize)this.dg).BeginInit();
            this.SuspendLayout();
            this.dg.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Location = new Point(-1, 23);
            this.dg.Margin = new Padding(6);
            this.dg.Name = "dg";
            this.dg.SelectionMode = DataGridViewSelectionMode.CellSelect;
            this.dg.Size = new Size(356, 51);
            this.dg.TabIndex = 0;
            this.dg.VirtualMode = true;
            this.button1.BackgroundImageLayout = ImageLayout.Stretch;
            this.button1.Font = new Font("Microsoft Sans Serif", 9.75f, FontStyle.Bold, GraphicsUnit.Point, (byte)204);
            this.button1.Location = new Point(99, 0);
            this.button1.Name = "button1";
            this.button1.Size = new Size(94, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "Загрузить";
            this.button1.UseVisualStyleBackColor = true;
            this.button2.BackgroundImageLayout = ImageLayout.Stretch;
            this.button2.Font = new Font("Microsoft Sans Serif", 9.75f, FontStyle.Bold, GraphicsUnit.Point, (byte)204);
            this.button2.Location = new Point(-1, 0);
            this.button2.Name = "button2";
            this.button2.Size = new Size(94, 23);
            this.button2.TabIndex = 2;
            this.button2.Text = "Сохранить";
            this.button2.UseVisualStyleBackColor = true;
            this.AutoScaleDimensions = new SizeF(12f, 24f);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(357, 75);
            this.Controls.Add((Control)this.button2);
            this.Controls.Add((Control)this.button1);
            this.Controls.Add((Control)this.dg);
            this.Font = new Font("Microsoft Sans Serif", 14.25f, FontStyle.Bold, GraphicsUnit.Point, (byte)204);
            this.Margin = new Padding(6);
            this.Name = nameof(TxDataFrm);
            this.Text = "Прямое управление";
            this.TopMost = true;
            this.Load += new EventHandler(this.TxDataFrm_Load);
            ((ISupportInitialize)this.dg).EndInit();
            this.ResumeLayout(false);
        }

        public delegate void DSendD(TxDataFrm.SendEventArgs e);

        public class SendEventArgs : EventArgs
        {
            public ushort Cmd;
            public int D1;
            public int D2;
            public int D3;
            public ushort Index;
            public short Val;
        }

        public class Stc
        {
            public string Name { get; set; }

            public short Val { get; set; }

            public bool Flag { get; set; }
        }
    }
}
