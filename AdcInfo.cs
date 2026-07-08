using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ADCTest
{
    public class AdcInfo : Form
    {
        public delegate void SendCommand(CommandArgs e);

        public delegate void UpdT(byte[] m);

        public class CommandArgs : EventArgs
        {
            public short Cmd;
        }

        private const string Channel = "Канал";

        private const string Parameter = "Парам.";

        private const string ValueCode = "Значение";

        private const string OffsetCode = "Оффсет";

        private const string OffsetVoltage = "Оффсет [U]";

        private const string NormalCode = "Норма";

        private const string NormalVoltage = "Норма [U]";

        private const string Comment = "Комментарий";

        private const float AdcResol = 4096f;

        private const float AdcUnorm = 0f;

        private readonly float _adcInorm;

        public readonly ushort[] _adcOffsets = new ushort[16];

        private readonly float _adcRef = 3.3f;

        private readonly ushort[] _adcValues = new ushort[16];

        private DataTable _dt;

        private bool _em;

        private bool _saveFile;

        private bool _saveFileBegin;

        public UpdT Ut;

        private static readonly string[] AdcChannels = new string[16]
        {
            "A0 [0]", "B0 [1]", "A1 [2]", "B1 [3]", "A2 [4]", "B2 [5]", "A3 [6]", "B3 [7]", "A4 [8]", "B4 [9]",
            "A5 [10]", "B5 [11]", "A6 [12]", "B6 [13]", "A7 [14]", "B7 [15]"
        };

        private static readonly string[] AdcCommentsKsp = new string[16]
        {
            "Ток фазы A - АИН1", "Ток фазы A - АИН2", "Ток фазы B - АИН1", "Ток фазы B - АИН2", "Ток фазы C - АИН1", "Ток фазы C - АИН2", "Ток АИНа 1 (Синий шкаф)", "Ток АИНа 2 (Синий шкаф)", "Входное линейное напряжение (Синий шкаф)", "Ток утечки на корпус",
            "Напряжение звена пост. тока (DCLink)", "Резерв", "Резерв", "Резерв", "Резерв", "Резерв"
        };

        private static readonly string[] AdcCommentsSvtg = new string[16]
        {
            "Ток моста 1, фаза A", "Температура 1", "Ток моста 1, фаза B", "Температура 2", "Ток моста 1, фаза C", "Напряжение звена пост. тока (DCLink)", "Ток моста 2, фаза A", "Резерв", "Ток моста 2, фаза B", "Резерв",
            "Ток моста 2, фаза C", "Резерв", "Ток возбуждения генератора", "Резерв", "Резерв", "Резерв"
        };

        private static readonly string[] AdcParNamesKsp = new string[16]
        {
            "Ia1", "Ia2", "Ib1", "Ib2", "Ic1", "Ic2", "Ir1", "Ir2", "Ugen", "Ic",
            "Udc", "-", "-", "-", "-", "-"
        };

        private static readonly string[] AdcParNamesSvtg = new string[16]
        {
            "IbrA1", "T1", "IbrB1", "T2", "IbrC1", "Udc", "IbrA2", "-", "IbrB2", "-",
            "IbrC2", "-", "Igen", "-", "-", "-"
        };

        private IContainer components = null;

        private DataGridView dg;

        private MenuStrip menuStrip1;

        private ToolStripMenuItem tsmCalibr;

        private ToolStripMenuItem tsmSave;

        public sealed override string Text
        {
            get
            {
                return base.Text;
            }
            set
            {
                base.Text = value;
            }
        }

        public bool IsKsp { get; }

        public event SendCommand SendC;

        public AdcInfo(bool ksp)
        {
            IsKsp = ksp;
            InitializeComponent();
            base.Shown += ADC_Info_Shown;
            if (ksp)
            {
                tsmCalibr.Click += delegate
                {
                    this.SendC?.Invoke(new CommandArgs
                    {
                        Cmd = 70
                    });
                };
            }
            else
            {
                Text = "АЦП - СВТГ";
            }

            _adcInorm = (IsKsp ? 2048 : 1400);
            _adcRef = (IsKsp ? 3f : 2.5f);
        }

        private void ADC_Info_Shown(object sender, EventArgs e)
        {
            Ut = UpdateTable;
            FillTable();
            dg.DataSource = _dt;
            dg.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            DataGridView dataGridView = dg;
            DataGridView dataGridView2 = dg;
            bool flag2 = (dg.AllowUserToResizeRows = false);
            bool allowUserToAddRows = (dataGridView2.RowHeadersVisible = flag2);
            dataGridView.AllowUserToAddRows = allowUserToAddRows;
            foreach (DataGridViewColumn column in dg.Columns)
            {
                column.ReadOnly = true;
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }

            dg.SelectionChanged += delegate
            {
                dg.ClearSelection();
            };
            tsmSave.Click += delegate
            {
                if (!_saveFile)
                {
                    tsmSave.BackColor = Color.Yellow;
                    _saveFile = true;
                }
            };
            ResizeTable();
        }

        private void ResizeTable()
        {
            if (base.IsDisposed)
            {
                return;
            }

            foreach (DataGridViewColumn column in dg.Columns)
            {
                column.MinimumWidth = 20;
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }

            Rectangle rectangle = RectangleToScreen(base.ClientRectangle);
            dg.Height = dg.Rows.Count * dg.RowTemplate.Height + dg.ColumnHeadersHeight + 10;
            int num = dg.Location.Y + dg.Height + (rectangle.Top - base.Top);
            int width = 20 + dg.Columns.Cast<DataGridViewColumn>().Sum((DataGridViewColumn dgColumn) => dgColumn.Width);
            dg.Width = width;
            Size size3 = (MaximumSize = (MinimumSize = new Size(width, num)));
            base.Height = num + menuStrip1.Height;
            base.Width = width;
            dg.Update();
        }

        private void FillTable()
        {
            _dt = new DataTable();
            _dt.Columns.Add("Канал", typeof(string));
            _dt.Columns.Add("Парам.", typeof(string));
            _dt.Columns.Add("Значение", typeof(string));
            _dt.Columns.Add("Оффсет", typeof(ushort));
            _dt.Columns.Add("Оффсет [U]", typeof(string));
            _dt.Columns.Add("Норма", typeof(ushort));
            _dt.Columns.Add("Норма [U]", typeof(string));
            _dt.Columns.Add("Комментарий", typeof(string));
            string[] array = (IsKsp ? AdcParNamesKsp : AdcParNamesSvtg);
            string[] array2 = (IsKsp ? AdcCommentsKsp : AdcCommentsSvtg);
            for (short num = 0; num < _adcValues.Length; num = (short)(num + 1))
            {
                DataRow dataRow = _dt.NewRow();
                dataRow["Канал"] = AdcChannels[num];
                dataRow["Парам."] = array[num];
                dataRow["Значение"] = 0;
                dataRow["Оффсет"] = 0;
                dataRow["Оффсет [U]"] = string.Format("{0} V", Math.Round(_adcRef / 4096f * Convert.ToSingle(dataRow["Оффсет"]), 3));
                dataRow["Норма"] = (IsKsp ? ((num == 10 || num == 11) ? 0f : _adcInorm) : ((num == 5 || num == 12) ? 0f : _adcInorm));
                dataRow["Норма [U]"] = string.Format("{0} V", Math.Round(_adcRef / 4096f * Convert.ToSingle(dataRow["Норма"]), 3));
                dataRow["Комментарий"] = array2[num];
                _dt.Rows.Add(dataRow);
            }
        }

        private void UpdateTable(byte[] m)
        {
            try
            {
                ushort num = BitConverter.ToUInt16(new byte[2]
                {
                    m[5],
                    m[4]
                }, 0);
                if (num != 32015 && num != 32016 && num != 32017 && num != 32018)
                {
                    return;
                }

                ushort num2 = BitConverter.ToUInt16(new byte[2]
                {
                    m[3],
                    m[2]
                }, 0);
                ushort num3 = BitConverter.ToUInt16(new byte[2]
                {
                    m[1],
                    m[0]
                }, 0);
                ushort num4 = BitConverter.ToUInt16(new byte[2]
                {
                    m[7],
                    m[6]
                }, 0);
                if (num == 32015)
                {
                    _em = true;
                }

                if (_em && num == 32016 && num4 < 16)
                {
                    _adcValues[num4] = num2;
                    _adcOffsets[num4] = num3;
                }

                if (_em && num == 32017)
                {
                    UpdTblData(_adcValues, _adcOffsets);
                    Text = ((num4 == 32017) ? "АЦП - Оффсеты из FRAM" : "АЦП - Оффсеты по умолчанию");
                    _em = false;
                }

                if (num == 32018)
                {
                    if (num3 == 0 && num4 == 0)
                    {
                        tsmCalibr.BackColor = Color.Yellow;
                        tsmCalibr.Text = $"Калибровка. Канал {num2}";
                    }
                    else
                    {
                        tsmCalibr.BackColor = SystemColors.Control;
                        tsmCalibr.Text = "Откалибровать";
                    }
                }
            }
            catch
            {
            }
        }

        public void UpdTblData(IReadOnlyList<ushort> values, IReadOnlyList<ushort> offsets)
        {
            try
            {
                string path = $"{Application.StartupPath}\\Offsets.txt";
                if (_dt == null)
                {
                    return;
                }

                if (IsKsp && _saveFile)
                {
                    _saveFileBegin = true;
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    File.AppendAllText(path, "volatile struct ADC_RESULT_MIRROR_REGS AdcDefaultOffsets = \r\n{\r\n");
                }

                for (int i = 0; i < offsets.Count; i++)
                {
                    _dt.Rows[i]["Значение"] = values[i];
                    _dt.Rows[i]["Оффсет"] = offsets[i];
                    _dt.Rows[i]["Оффсет [U]"] = string.Format("{0} V", Math.Round(_adcRef / 4096f * Convert.ToSingle(_dt.Rows[i]["Оффсет"]), 3));
                    float num = Convert.ToSingle(_dt.Rows[i]["Оффсет"]);
                    float num2 = Convert.ToSingle(_dt.Rows[i]["Норма"]);
                    if (Convert.ToString(_dt.Rows[i]["Парам."]).Equals("-"))
                    {
                        Color color2 = (dg.Rows[i].Cells[5].Style.BackColor = (dg.Rows[i].Cells[6].Style.BackColor = Color.LightGray));
                    }
                    else
                    {
                        Color color2 = (dg.Rows[i].Cells[5].Style.BackColor = (dg.Rows[i].Cells[6].Style.BackColor = ((Math.Abs(num - num2) > 100f) ? Color.Salmon : dg.DefaultCellStyle.BackColor)));
                    }

                    if (IsKsp && _saveFile && _saveFileBegin)
                    {
                        File.AppendAllText(path, string.Format(".ADCRESULT{0} = {1},\r\n", i, _dt.Rows[i]["Оффсет"]));
                    }
                }

                if (IsKsp && _saveFile && _saveFileBegin)
                {
                    File.AppendAllText(path, "};\r\n");
                    tsmSave.BackColor = SystemColors.Control;
                    _saveFile = (_saveFileBegin = false);
                }

                ResizeTable();
            }
            catch
            {
            }
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
            menuStrip1 = new System.Windows.Forms.MenuStrip();
            tsmCalibr = new System.Windows.Forms.ToolStripMenuItem();
            tsmSave = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)dg).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dg.Dock = System.Windows.Forms.DockStyle.Top;
            dg.Location = new System.Drawing.Point(0, 31);
            dg.Margin = new System.Windows.Forms.Padding(5);
            dg.Name = "dg";
            dg.Size = new System.Drawing.Size(473, 143);
            dg.TabIndex = 0;
            menuStrip1.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
            menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[2] { tsmSave, tsmCalibr });
            menuStrip1.Location = new System.Drawing.Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new System.Windows.Forms.Padding(10, 3, 0, 3);
            menuStrip1.Size = new System.Drawing.Size(473, 31);
            menuStrip1.TabIndex = 1;
            tsmCalibr.Name = "tsmCalibr";
            tsmCalibr.Size = new System.Drawing.Size(143, 25);
            tsmCalibr.Text = "Откалибровать";
            tsmSave.Name = "tsmSave";
            tsmSave.Size = new System.Drawing.Size(111, 25);
            tsmSave.Text = "Сохр. файл";
            base.AutoScaleDimensions = new System.Drawing.SizeF(10f, 20f);
            base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            base.ClientSize = new System.Drawing.Size(473, 122);
            base.Controls.Add(dg);
            base.Controls.Add(menuStrip1);
            Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
            base.MainMenuStrip = menuStrip1;
            base.Margin = new System.Windows.Forms.Padding(5);
            base.Name = "AdcInfo";
            Text = "ADC_Info";
            ((System.ComponentModel.ISupportInitialize)dg).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
