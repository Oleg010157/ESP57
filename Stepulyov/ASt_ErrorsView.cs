using FlagMask;
using Peak.Can.Basic;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
namespace ICDIBasic.Stepulyov
{
    public partial class ErrorsView : Form
    {
        Form1 errfrm;  // адресс ссылка на Form1
        public delegate void DClearErr(ushort cmd);
        public delegate void DImitErr(ushort cmd, ushort code);
        private const string SErrCode = "ID ошибки";
        private const string SBinCode = "Код ошибки";
        private const string SCurrErr = "Текущая ошибка";
        private const string SOneTErr = "Однократная ошибка";
        private const string SMultTErr = "Многократная ошибка";
        private const string SSessionCnt = "Счётчик сессии";
        private const string SHistoryCnt = "Счётчик истории";
        private const string SLastTime = "Последний раз";
        private const string SErrDesc = "Описание ошибки";
        private const string SStart = "#####*****";
        private const string SEnd = "*****#####";
        private readonly Bitmap _greenLed;
        private readonly Bitmap _redLed;
        private ushort _ctrI;
        public bool _enTableUpd;
        private DataTable _errCodesHeaderTable;
        public ErrorsData[] _errorsArray;
        private DataTable _errsViewTable;
        private bool _loaded;
        private bool _str;

        private void EnsureErrorsViewTableSchema()
        {
            if (_errsViewTable.Columns.Count > 0) return;

            _errsViewTable.Columns.Add(SErrCode, typeof(string));
            _errsViewTable.Columns.Add(SBinCode, typeof(ushort));
            _errsViewTable.Columns.Add(SCurrErr, typeof(Bitmap));
            _errsViewTable.Columns.Add(SOneTErr, typeof(Bitmap));
            _errsViewTable.Columns.Add(SMultTErr, typeof(Bitmap));
            _errsViewTable.Columns.Add(SSessionCnt, typeof(ushort));
            _errsViewTable.Columns.Add(SHistoryCnt, typeof(ushort));
            _errsViewTable.Columns.Add(SLastTime, typeof(string));
            _errsViewTable.Columns.Add(SErrDesc, typeof(string));
        }

        private void EnableGridDoubleBuffering()
        {
            var pi = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi?.SetValue(dataGridView1, true, null);
        }

        public ErrorsView(Bitmap redL, Bitmap greenL)
        {
            InitializeComponent();
            _redLed = redL;
            _greenLed = greenL;
        }

        public event DImitErr ImErr;
        public event DClearErr ClErr;

        private void ErrorsView_Load(object sender, EventArgs e)
        {
            var gr = Graphics.FromImage(_greenLed);
            gr.Clear(dataGridView1.DefaultCellStyle.BackColor);
            _errsViewTable = new DataTable();
            _errCodesHeaderTable = new DataTable();
            _errorsArray = new ErrorsData[256];
            for (ushort i = 0; i < 256; i++) _errorsArray[i] = new ErrorsData();
            EnsureErrorsViewTableSchema();
            dataGridView1.DataSource = _errsViewTable;
            EnableGridDoubleBuffering();
            ErrCodeMenu.CheckedChanged += (df, de) =>
            {
                UpdateErrorList();
                SaveSettings();
            };
            ErrBinCodeMenu.CheckedChanged += (df, de) =>
            {
                UpdateErrorList();
                SaveSettings();
            };
            CurrErrMenu.CheckedChanged += (df, de) =>
            {
                UpdateErrorList();
                SaveSettings();
            };
            OneTErrorMenu.CheckedChanged += (df, de) =>
            {
                UpdateErrorList();
                SaveSettings();
            };
            MultTErrorMenu.CheckedChanged += (df, de) =>
            {
                UpdateErrorList();
                SaveSettings();
            };
            ErrCounterMenu.CheckedChanged += (df, de) =>
            {
                UpdateErrorList();
                SaveSettings();
            };
            ErrDescMenu.CheckedChanged += (df, de) =>
            {
                UpdateErrorList();
                SaveSettings();
            };
            ClearErrorsMenu.Click += (fg, fh) => { ClErr?.Invoke(110); };
            
            dataGridView1.SelectionChanged += (f, g) => { dataGridView1.ClearSelection(); };
            ReadSettings();
            ParseErrorDefsHeaderFile();
            UpdateErrorList();
        }

        public void UpdateErrorList()
        {
            try
            {
                if (_enTableUpd && Visible)
                {
                    SuspendLayout();
                    var vScrollPos = dataGridView1.FirstDisplayedScrollingRowIndex;
                    _errsViewTable.Clear();
                    EnsureErrorsViewTableSchema();
                    for (ushort i = 0; i < _errorsArray.Length; i++)
                    {
                        var rows = _errCodesHeaderTable.Select($"hBinErrorCode = {i}");
                        if (rows.Length == 0) continue; // в таблицу выводим только коды, определённые в ErrorCodes.h

                        if ((!_errorsArray[i].CurrErr || !CurrErrMenu.Checked) &&
                            (!_errorsArray[i].OneTErr || !OneTErrorMenu.Checked) &&
                            (!_errorsArray[i].MultError || !MultTErrorMenu.Checked) &&
                            (_errorsArray[i].SessionCnt <= 0 || !ErrCounterMenu.Checked)) continue;
                        _errsViewTable.Rows.Add(rows[0].ItemArray[0], i,
                            _errorsArray[i].CurrErr ? _redLed : _greenLed,
                            _errorsArray[i].OneTErr ? _redLed : _greenLed,
                            _errorsArray[i].MultError ? _redLed : _greenLed, _errorsArray[i].SessionCnt,
                            _errorsArray[i].HistoryCnt,
                            _errorsArray[i].LastTimeText,
                            rows[0].ItemArray[2]);
                    }
                    if (!ReferenceEquals(dataGridView1.DataSource, _errsViewTable))
                    {
                        dataGridView1.DataSource = _errsViewTable;
                    }
                    #region Скрытие столбцов DataGrid
                    var o1 = dataGridView1.Columns[SErrCode];
                    if (o1 != null) o1.Visible = ErrCodeMenu.Checked;
                    var o2 = dataGridView1.Columns[SBinCode];
                    if (o2 != null) o2.Visible = ErrBinCodeMenu.Checked;
                    var o3 = dataGridView1.Columns[SCurrErr];
                    if (o3 != null) o3.Visible = CurrErrMenu.Checked;
                    var o4 = dataGridView1.Columns[SOneTErr];
                    if (o4 != null) o4.Visible = OneTErrorMenu.Checked;
                    var o5 = dataGridView1.Columns[SMultTErr];
                    if (o5 != null) o5.Visible = MultTErrorMenu.Checked;
                    var o6 = dataGridView1.Columns[SSessionCnt];
                    if (o6 != null) o6.Visible = ErrCounterMenu.Checked;
                    var o7 = dataGridView1.Columns[SHistoryCnt];
                    if (o7 != null) o7.Visible = ErrCounterMenu.Checked;
                    var o8 = dataGridView1.Columns[SErrDesc];
                    if (o8 != null) o8.Visible = ErrDescMenu.Checked;
                    var o9 = dataGridView1.Columns[SLastTime];
                    if (o9 != null) o9.Visible = ErrCounterMenu.Checked;

                    if (o1 != null) o1.Width = 110;
                    if (o2 != null) o2.Width = 55;
                    if (o3 != null) o3.Width = 80;
                    if (o4 != null) o4.Width = 95;
                    if (o5 != null) o5.Width = 100;
                    if (o6 != null) o6.Width = 75;
                    if (o7 != null) o7.Width = 80;
                    if (o9 != null) o9.Width = 150;
                    if (o8 != null) o8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                    if (o2 != null)
                    {
                        o2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        o2.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    if (o6 != null)
                    {
                        o6.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        o6.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    if (o7 != null)
                    {
                        o7.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        o7.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    if (o9 != null)
                    {
                        o9.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        o9.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }
                    #endregion
                    foreach (DataGridViewColumn column in dataGridView1.Columns)
                    {
                        column.DefaultCellStyle.Font =
                            new Font("Microsoft Sans Serif", 14F, FontStyle.Bold, GraphicsUnit.Pixel);
                        column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    }
                    dataGridView1.Update();
                    if (vScrollPos > 0) dataGridView1.FirstDisplayedScrollingRowIndex = vScrollPos;
                    var screenRectangle = RectangleToScreen(ClientRectangle);
                    dataGridView1.Height =
                        (dataGridView1.Rows.Count < 25 ? dataGridView1.Rows.Count : 25) *
                        dataGridView1.RowTemplate.Height + dataGridView1.ColumnHeadersHeight + 5;
                    var hght = dataGridView1.Location.Y + dataGridView1.Height + (screenRectangle.Top - Top);
                    MaximumSize = new Size(1280, hght);
                    MinimumSize = new Size(512, hght);
                    Height = hght;
                    ResumeLayout();
                }
                foreach (var t in _errorsArray) t.Input = 0;
                _enTableUpd = false;
            }
            catch
            {
                foreach (var t in _errorsArray) t.Input = 0;
                _enTableUpd = false;
                _errsViewTable.Clear();
                _errsViewTable.Columns.Clear();
            }
        }

        private void ParseErrorDefsHeaderFile()
        {
            try
            {
                _errCodesHeaderTable.Columns.Clear();
                _errCodesHeaderTable.Rows.Clear();
                _errCodesHeaderTable.Columns.Add("hErrorCode", typeof(string));
                _errCodesHeaderTable.Columns.Add("hBinErrorCode", typeof(ushort));
                _errCodesHeaderTable.Columns.Add("hErrDesc", typeof(string));
                using (var streamReader = File.OpenText(Application.StartupPath + "\\..\\ErrorCodes.h"))
                {
                    var hLines = streamReader.ReadToEnd()
                        .Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    foreach (var hLine in hLines.Where(hLine => hLine.StartsWith("#define") && hLine.Contains("//")))
                    {
                        var rsl = hLine.Split(new[] { "//" }, StringSplitOptions.None);
                        var hDefine = rsl[0];
                        var hComment = rsl[1].Trim();
                        if (hDefine.StartsWith("#define")) hDefine = hDefine.Replace("#define", string.Empty);
                        rsl = hDefine.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        _errCodesHeaderTable.Rows.Add(rsl[0].Trim(), Convert.ToUInt16(rsl[1]), hComment);
                    }
                }
            }
            catch
            {
                MessageBox.Show(@"Файл ErrorCodes.h отсутствует или имеет неправильный формат");
            }
        }

        public void GetErrorsArray_(ushort[] d)
        {
            switch (d[0])
            {
                case 32009:     // старт кадров ошибок
                    _str = true;
                    break;

                case 32010:
                  
                    {
                        _errorsArray[d[1]].Input = d[2];
                        _errorsArray[d[1] + 1].Input = d[3];
                        _enTableUpd = true;
                        UpdateErrorList();
                    }
                    break;

                case 32011:     // конец вывода кадра ошибок
                    _str = false;
                    _enTableUpd = true;
                    UpdateErrorList();
                    break;
            }
/*
            if (d[0] == 32009 && d[1] == 32009 && d[2] == 32009 && d[3] == 32009)
            {
                _str = true;
               if (d[0] == 32010)
                {
                    _errorsArray[d[0]].Input = d[1];
                    _errorsArray[d[0] + 1].Input = d[2];
                }
                if (d[0] != 32011 || d[1] != 32011 || d[2] != 32011 || d[3] != 32011) return;
                _str = false;
                _enTableUpd = true;
                UpdateErrorList();
            }
*/
        }

        private void ecodeMenu_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
           
            if (e.KeyChar != '\r' || ecodeMenu.Text.Length <= 0) return;
            _ctrI++;
            var r = ushort.TryParse(ecodeMenu.Text, out var code);
            if (r && code < 256) ImErr?.Invoke(_ctrI, code);
            else ecodeMenu.Text = @"Значение от 0 до 255";
        }

        private void ecodeMenu_Click(object sender, EventArgs e)
        {
            ecodeMenu.Text = string.Empty;
        }

        private void ReadSettings()
        {
            if (File.Exists(Application.StartupPath + "\\EVS"))
                try
                {
                    using (var streamReader = new StreamReader(Application.StartupPath + "\\EVS", Encoding.UTF8))
                    {
                        var text = streamReader.ReadToEnd();
                        if (text.StartsWith(SStart) && text.EndsWith(SEnd))
                        {
                            text = text.Replace(SStart, string.Empty);
                            text = text.Replace(SEnd, string.Empty);
                            ErrCodeMenu.Checked = text[0] == '1';
                            ErrBinCodeMenu.Checked = text[1] == '1';
                            CurrErrMenu.Checked = text[2] == '1';
                            OneTErrorMenu.Checked = text[3] == '1';
                            MultTErrorMenu.Checked = text[4] == '1';
                            ErrCounterMenu.Checked = text[5] == '1';
                            ErrDescMenu.Checked = text[6] == '1';
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            _loaded = true;
        }

        private void SaveSettings()
        {
            if (!_loaded) return;
            char[] settings =
            {
                ErrCodeMenu.Checked ? '1' : '0', ErrBinCodeMenu.Checked ? '1' : '0', CurrErrMenu.Checked ? '1' : '0',
                OneTErrorMenu.Checked ? '1' : '0', MultTErrorMenu.Checked ? '1' : '0',
                ErrCounterMenu.Checked ? '1' : '0', ErrDescMenu.Checked ? '1' : '0'
            };
            try
            {
                File.WriteAllText(Application.StartupPath + "\\EVS", SStart + new string(settings) + SEnd);
            }
            catch
            {
                // ignored
            }
        }

        public class ErrorsData
        {
            public ErrorsData()
            {
                Word = 0;
            }

            public ushort Input
            {
                set => Word = value;
            }
            public ushort SessionCnt => (ushort)(Word & 0x00FF);
            public ushort HistoryCnt => (ushort)((Word >> 8) & 0x007F);

            public bool CurrErr => (Word & Bit16.Bit15) == Bit16.Bit15; // флаг текущей ошибки
      
            public bool OneTErr => SessionCnt != 0; // была ошибка в текущей сессии
            public bool MultError => HistoryCnt != 0; // ошибка встречалась в предыдущих сессиях
            public ushort ErrCnt => SessionCnt;
            public ushort Word { get; set; }

            // UTC последней сессии с этой ошибкой, кадр 32013; 0 - прибор не знал
            // времени (пульт с его включения не подключался) или прошивка старая.
            public uint LastTime { get; set; }

            public string LastTimeText => LastTime == 0
                ? "—"
                : DateTimeOffset.FromUnixTimeSeconds(LastTime).LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss");


        }
    }
}