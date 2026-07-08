#region сборка CANRxDataTable, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// C:\Users\User\Desktop\Work\920 — копия\SHARP\Stepulyov\CANRxDataTable.dll
// Decompiled with ICSharpCode.Decompiler 6.1.0.5902
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CANRxDataTable
{
    public class RxDataFrm : Form
    {
        private readonly List<string[]> _parLeft = new List<string[]>();

        private readonly List<string[]> _parRight = new List<string[]>();

        private ushort _ctx;

        private DataRow _dr;

        private DataRow _dr2;

        private DataTable _dt = new DataTable();

        private DataTable _dt2 = new DataTable();

        private bool _rdy;

        private IContainer components = null;

        private DataGridView dg;

        public string T
        {
            get;
        }

        public RxDataFrm(string n, string t)
        {
            RxDataFrm rxDataFrm = this;
            T = t;
            InitializeComponent();
            base.Shown += delegate
            {
                try
                {
                    rxDataFrm.ReadIniFile(n);
                }
                catch
                {
                    rxDataFrm.Close();
                }
            };
        }

        public void ReadIniFile(string f)
        {
            bool flag = false;
            _dt = new DataTable();
            _dt2 = new DataTable();
            _dt.Clear();
            _dt2.Clear();
            _dt.Columns.Add("PName", typeof(string));
            _dt.Columns.Add("PSign", typeof(bool));
            _dt.Columns.Add("PChDot", typeof(ushort));
            _dt.Columns.Add("PMult", typeof(float));
            _dt.Columns.Add("PShift", typeof(float));
            _dt.Columns.Add("PByte", typeof(ulong));
            _dt.Columns.Add("PColor", typeof(ushort));
            _dt.Columns.Add("PLeft", typeof(bool));
            _dt2.Columns.Add("LN", typeof(string));
            _dt2.Columns.Add("LV", typeof(string));
            _dt2.Columns.Add("LC", typeof(string));
            _dt2.Columns.Add("RN", typeof(string));
            _dt2.Columns.Add("RV", typeof(string));
            _dt2.Columns.Add("RC", typeof(string));
            foreach (string[] item in from t in (from t in File.ReadAllLines(f)
                                                 select t.Replace("\t", string.Empty).Replace(" ", string.Empty).Replace(",", "|") into t
                                                 where !t.Equals(string.Empty) && !t.StartsWith(";") && t.Length >= 3 && !t.Contains("@")
                                                 select t).TakeWhile((string t) => !t.ToLower().Equals("[new]"))
                                      select t.Replace("=", "|").Replace(";", "|") into t
                                      select t.Split('|'))
            {
                if (item[0].ToLower().Equals("[parleft]"))
                {
                    flag = true;  
                    continue;
                }

                if (item[0].ToLower().Equals("[parright]"))
                {
                    flag = false;
                    continue;
                }

                bool flag2;
                if (item[1].StartsWith("^"))
                {
                    flag2 = true;
                    item[1] = item[1].Replace("^", string.Empty);
                }
                else
                {
                    flag2 = false;
                }

                _dr = _dt.NewRow();
                _dr[0] = item[0];
                _dr[1] = flag2;
                _dr[2] = item[1];
                _dr[3] = item[2];
                _dr[4] = item[3];
                _dr[5] = Convert.ToInt32(item[5]) / 2;
                _dr[6] = item[6];
                _dr[7] = flag;
                _dt.Rows.Add(_dr);
            }

            dg.DataSource = _dt2;
      //      dg.DataSource = _dt;

            foreach (DataGridViewColumn column in dg.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.ReadOnly = true;
            }

            dg.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dg.CellBorderStyle = DataGridViewCellBorderStyle.None;
            int num3 = dg.Columns[1].MinimumWidth = (dg.Columns[4].MinimumWidth = 100);
            DataGridViewColumn dataGridViewColumn2 = dg.Columns[2];
            DataGridViewColumn dataGridViewColumn3 = dg.Columns[5];
            DataGridView dataGridView = dg;
            DataGridView dataGridView2 = dg;
            DataGridView dataGridView3 = dg;
            DataGridView dataGridView4 = dg;
            DataGridView dataGridView5 = dg;
            DataGridView dataGridView6 = dg;
            bool flag4 = dg.AllowUserToResizeRows = false;
            bool flag6 = dataGridView6.ColumnHeadersVisible = flag4;
            bool flag8 = dataGridView5.ShowEditingIcon = flag6;
            bool flag10 = dataGridView4.ShowEditingIcon = flag8;
            bool flag12 = dataGridView3.ShowCellToolTips = flag10;
            bool flag14 = dataGridView2.RowHeadersVisible = flag12;
            bool flag16 = dataGridView.AllowUserToAddRows = flag14;
            bool visible = dataGridViewColumn3.Visible = flag16;
            dataGridViewColumn2.Visible = visible;
            dg.SelectionChanged += delegate
            {
                dg.ClearSelection();
            };
            dg.Refresh();
            _rdy = true;
        }

        public void RxD(ushort[] d)
        {
            if (!_rdy)
            {
                return;
            }

            if (_ctx < 10)
            {
                _ctx++;
                return;
            }

            _ctx = 0;
            _parLeft.Clear();
            _parRight.Clear();
            _dt2.Clear();
            try
            {
                for (int i = 0; i < _dt.Rows.Count; i++)
                {
                    string text = Convert.ToString(_dt.Rows[i][0]);
                    bool flag = Convert.ToBoolean(_dt.Rows[i][1]);
                    ushort num = Convert.ToUInt16(_dt.Rows[i][2]);
                    double num2 = Convert.ToDouble(_dt.Rows[i][3]);
                    double num3 = Convert.ToDouble(_dt.Rows[i][4]);
                    ushort num4 = Convert.ToUInt16(_dt.Rows[i][5]);
                    string text2 = Convert.ToString(_dt.Rows[i][6]);
                    bool flag2 = Convert.ToBoolean(_dt.Rows[i][7]);
                    num4 = (ushort)(num4 - 1);
                    double num5 = flag ? Convert.ToDouble((short)d[num4]) : Convert.ToDouble(d[num4]);
                    string text3 = string.Format((num > 0) ? ("{0:N" + num + "}") : "{0}", Math.Round(num5 * num2 + num3, num), CultureInfo.InvariantCulture);
                    if (flag2)
                    {
                        _parLeft.Add(new string[3]
                        {
                            text,
                            text3,
                            text2
                        });
                    }
                    else
                    {
                        _parRight.Add(new string[3]
                        {
                            text,
                            text3,
                            text2
                        });
                    }
                }

                int num6 = (_parLeft.Count > _parRight.Count) ? _parLeft.Count : _parRight.Count;
                for (int j = 0; j < num6; j++)
                {
                    _dr2 = _dt2.NewRow();
                    if (j < _parLeft.Count)
                    {
                        _dr2[0] = _parLeft[j][0];
                        _dr2[1] = _parLeft[j][1];
                        _dr2[2] = _parLeft[j][2];
                    }

                    if (j < _parRight.Count)
                    {
                        _dr2[3] = _parRight[j][0];
                        _dr2[4] = _parRight[j][1];
                        _dr2[5] = _parRight[j][2];
                    }

                    _dt2.Rows.Add(_dr2);
                }

                Rectangle rectangle = RectangleToScreen(base.ClientRectangle);
                dg.Height = dg.Rows.Count * dg.RowTemplate.Height + 7;
                int height = dg.Location.Y + dg.Height + (rectangle.Top - base.Top);
                int width = dg.Columns[0].Width + dg.Columns[1].Width + dg.Columns[3].Width + dg.Columns[4].Width + 7;
                dg.Width = width;
                MaximumSize = new Size(width, height);
                MinimumSize = new Size(width, height);
                base.Height = height;
                base.Width = width;
                for (int k = 0; k < dg.Rows.Count; k++)
                {
                    for (int l = 0; l < dg.Columns.Count; l++)
                    {
                        dg.Rows[k].Cells[l].Style.BackColor = Color.Black;
                    }

                    Color color3 = dg.Rows[k].Cells[0].Style.ForeColor = (dg.Rows[k].Cells[1].Style.ForeColor = SetColor(Convert.ToString(_dt2.Rows[k][2])));
                    color3 = (dg.Rows[k].Cells[3].Style.ForeColor = (dg.Rows[k].Cells[4].Style.ForeColor = SetColor(Convert.ToString(_dt2.Rows[k][5]))));
                }

                dg.Refresh();
            }
            catch
            {
            }
        }

        private static Color SetColor(string c)
        {
            if (c.Equals("0"))
            {
                return Color.White;
            }

            if (c.Equals("1"))
            {
                return Color.Red;
            }

            if (c.Equals("2"))
            {
                return Color.Orange;
            }

            if (c.Equals("3"))
            {
                return Color.YellowGreen;
            }

            if (c.Equals("4"))
            {
                return Color.LightGreen;
            }

            if (c.Equals("5"))
            {
                return Color.Blue;
            }

            if (c.Equals("6"))
            {
                return Color.Yellow;
            }

            if (c.Equals("7"))
            {
                return Color.Violet;
            }

            return Color.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            dg = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)dg).BeginInit();
            SuspendLayout();
            dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dg.Location = new System.Drawing.Point(0, 0);
            dg.Margin = new System.Windows.Forms.Padding(5);
            dg.Name = "dg";
            dg.Size = new System.Drawing.Size(333, 154);
            dg.TabIndex = 0;
            base.AutoScaleDimensions = new System.Drawing.SizeF(10f, 20f);
            base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            base.ClientSize = new System.Drawing.Size(338, 155);
            base.Controls.Add(dg);
            Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
            base.Margin = new System.Windows.Forms.Padding(5);
            base.Name = "RxDataFrm";
            Text = "Параметры";
            base.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)dg).EndInit();
            ResumeLayout(false);
        }
    }
}
#if false // Журнал декомпиляции
Элементов в кэше: "20"
------------------
Разрешить: "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Найдена одна сборка: "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Загрузить из: "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll"
------------------
Разрешить: "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Найдена одна сборка: "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Загрузить из: "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Windows.Forms.dll"
------------------
Разрешить: "System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Найдена одна сборка: "System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Загрузить из: "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Data.dll"
------------------
Разрешить: "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Найдена одна сборка: "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Загрузить из: "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.dll"
------------------
Разрешить: "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
Найдена одна сборка: "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
Загрузить из: "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Drawing.dll"
------------------
Разрешить: "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Найдена одна сборка: "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Загрузить из: "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Core.dll"
#endif
