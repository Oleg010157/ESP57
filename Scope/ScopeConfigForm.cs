using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace ICDIBasic
{
    /*
     * Настройка вида каналов - по образцу окна Config в Wscope, которым этот
     * прибор настраивают уже не первый год.
     *
     * Слева список всех каналов кадра, справа две группы по шесть слотов. На
     * график попадает только то, что перенесено в слот: показывать все
     * пятнадцать кривых разом бессмысленно, а выбор - дело оператора.
     *
     * КОЭФФИЦИЕНТА И СМЕЩЕНИЯ ЗДЕСЬ НЕТ, в отличие от окна Wscope. По опыту их
     * не применяют, а в нашем случае они и не нужны: пересчёт кода в физику уже
     * сделан картой каналов (K и A из .ini). Осталось то, ради чего в ту
     * таблицу и заглядывают, - минимум, максимум и цвет.
     *
     * Пустые минимум и максимум означают автоматический масштаб. Отдельной
     * галочки для этого нет намеренно: пустая клетка и так читается как
     * «не задано», а галочка рядом с двумя пустыми полями только спрашивала бы
     * об одном и том же дважды.
     */
    internal sealed class ScopeConfigForm : Form
    {
        private const int ColName = 0;
        private const int ColMin = 1;
        private const int ColMax = 2;
        private const int ColColor = 3;

        private readonly IniChannelMap _map;
        private readonly ScopeView _view;

        private readonly ListBox _list;
        private readonly DataGridView[] _grid = new DataGridView[ScopeView.Groups];

        /// <summary>Правим копию: Отмена должна возвращать как было.</summary>
        public ScopeView Result { get { return _view; } }

        public ScopeConfigForm(IniChannelMap map, ScopeView view)
        {
            _map = map;
            _view = view.Clone();

            Text = "Каналы - " + map.Template;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(700, 420);
            MinimumSize = new Size(620, 360);

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                SelectionMode = SelectionMode.One,
            };
            _list.MouseDown += ListMouseDown;
            _list.MouseMove += ListMouseMove;
            _list.DoubleClick += ListDoubleClick;

            for (int i = 0; i < map.Count; i++) _list.Items.Add(map[i].Name);

            var leftBox = new GroupBox { Dock = DockStyle.Fill, Text = "Каналы кадра" };
            leftBox.Controls.Add(new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 2, 6, 6) });
            ((Panel)leftBox.Controls[0]).Controls.Add(_list);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = ScopeView.Groups,
            };
            for (int g = 0; g < ScopeView.Groups; g++)
                right.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            for (int g = 0; g < ScopeView.Groups; g++)
            {
                _grid[g] = NewGrid(g);

                var box = new GroupBox
                {
                    Dock = DockStyle.Fill,
                    Text = g == 0 ? "Верхнее поле" : "Нижнее поле",
                    Padding = new Padding(6, 2, 6, 6),
                };
                box.Controls.Add(_grid[g]);
                right.Controls.Add(box, 0, g);
            }

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 180,
            };
            split.Panel1.Controls.Add(leftBox);
            split.Panel2.Controls.Add(right);

            var ok = new Button { Text = "ОК", DialogResult = DialogResult.OK, Width = 90 };
            var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90 };
            ok.Click += OkClick;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                Padding = new Padding(8, 6, 8, 6),
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            var hint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(10, 4, 0, 0),
                Text = "Перетащите канал в поле или щёлкните дважды. Delete убирает, " +
                       "щелчок по цвету меняет. Пустые границы - масштаб сам.",
            };

            Controls.Add(split);
            Controls.Add(hint);
            Controls.Add(buttons);

            AcceptButton = ok;
            CancelButton = cancel;

            Fill();
        }

        private DataGridView NewGrid(int group)
        {
            var dg = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AllowDrop = true,
                BackgroundColor = SystemColors.Window,
                EditMode = DataGridViewEditMode.EditOnEnter,
                Tag = group,
            };

            dg.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Канал",
                ReadOnly = true,
                FillWeight = 120,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });
            dg.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Мин", FillWeight = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dg.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Макс", FillWeight = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dg.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Цвет",
                ReadOnly = true,
                FillWeight = 50,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });

            for (int s = 0; s < ScopeView.SlotsPerGroup; s++) dg.Rows.Add();

            dg.DragEnter += GridDragEnter;
            dg.DragDrop += GridDragDrop;
            dg.CellClick += GridCellClick;
            dg.KeyDown += GridKeyDown;
            dg.CellValidating += GridCellValidating;

            return dg;
        }

        /*-------------------------------------------------------------------
         * Таблица <-> модель
         *------------------------------------------------------------------*/

        private void Fill()
        {
            for (int g = 0; g < ScopeView.Groups; g++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                    FillRow(g, s);
        }

        private void FillRow(int g, int s)
        {
            ScopeSlot sl = _view[g, s];
            DataGridViewRow row = _grid[g].Rows[s];

            row.Cells[ColName].Value = sl.Empty ? "" : sl.Name;
            row.Cells[ColMin].Value = float.IsNaN(sl.Min) ? "" : Txt(sl.Min);
            row.Cells[ColMax].Value = float.IsNaN(sl.Max) ? "" : Txt(sl.Max);

            row.Cells[ColColor].Style.BackColor = sl.Empty ? SystemColors.Control : sl.Color;
            row.Cells[ColColor].Style.SelectionBackColor = row.Cells[ColColor].Style.BackColor;

            /* Пустой слот не редактируем: границы без канала не к чему отнести. */
            row.Cells[ColMin].ReadOnly = sl.Empty;
            row.Cells[ColMax].ReadOnly = sl.Empty;
        }

        private static string Txt(float v)
        {
            return v.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        /// <summary>Снять границы из таблицы в модель. Негодное поле считаем пустым.</summary>
        private void Harvest()
        {
            for (int g = 0; g < ScopeView.Groups; g++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                {
                    ScopeSlot sl = _view[g, s];
                    if (sl.Empty) continue;

                    sl.Min = Parse(_grid[g].Rows[s].Cells[ColMin].Value);
                    sl.Max = Parse(_grid[g].Rows[s].Cells[ColMax].Value);
                }
        }

        private static float Parse(object o)
        {
            if (o == null) return float.NaN;

            string s = o.ToString().Trim().Replace(',', '.');
            if (s.Length == 0) return float.NaN;

            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                ? v : float.NaN;
        }

        private void GridCellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != ColMin && e.ColumnIndex != ColMax) return;

            string s = (e.FormattedValue ?? "").ToString().Trim();
            if (s.Length == 0) return;

            if (float.IsNaN(Parse(s)))
            {
                /* Не ругаемся окном: просто не пускаем мусор в поле, из которого
                 * потом вышел бы невидимый масштаб. */
                e.Cancel = true;
            }
        }

        /*-------------------------------------------------------------------
         * Перетаскивание
         *------------------------------------------------------------------*/
        private int _dragIndex = -1;

        private void ListMouseDown(object sender, MouseEventArgs e)
        {
            _dragIndex = _list.IndexFromPoint(e.Location);
        }

        private void ListMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _dragIndex < 0) return;

            _list.DoDragDrop(_list.Items[_dragIndex].ToString(), DragDropEffects.Copy);
            _dragIndex = -1;
        }

        private void GridDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(string)) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void GridDragDrop(object sender, DragEventArgs e)
        {
            var dg = (DataGridView)sender;
            int g = (int)dg.Tag;

            if (!e.Data.GetDataPresent(typeof(string))) return;

            Point p = dg.PointToClient(new Point(e.X, e.Y));
            DataGridView.HitTestInfo hit = dg.HitTest(p.X, p.Y);

            int s = hit.RowIndex;
            if (s < 0 || s >= ScopeView.SlotsPerGroup) return;

            Place(g, s, (string)e.Data.GetData(typeof(string)));
        }

        private void ListDoubleClick(object sender, EventArgs e)
        {
            if (_list.SelectedItem == null) return;

            /* Первый свободный слот, сверху вниз: мышью попадать в строку не
             * всегда удобно, а действие то же самое. */
            for (int g = 0; g < ScopeView.Groups; g++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                    if (_view[g, s].Empty)
                    {
                        Place(g, s, _list.SelectedItem.ToString());
                        return;
                    }
        }

        private void Place(int g, int s, string name)
        {
            int ci = ScopeView.IndexOf(_map, name);
            if (ci < 0) return;

            /* Границы и цвет пересобираем с нуля: в слоте мог стоять другой
             * канал, и его вольты на амперах выглядели бы как настройка. */
            ScopeSlot sl = _view[g, s];

            sl.Name = _map[ci].Name;
            sl.Color = _map[ci].Color;
            sl.Min = float.NaN;
            sl.Max = float.NaN;

            FillRow(g, s);
        }

        private void GridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;

            var dg = (DataGridView)sender;
            int g = (int)dg.Tag;

            if (dg.CurrentCell == null) return;

            int s = dg.CurrentCell.RowIndex;
            if (s < 0 || s >= ScopeView.SlotsPerGroup) return;

            /* В клетках границ Delete должен стирать текст, а не выкидывать
             * канал: иначе правка размаха превращалась бы в ловушку. */
            if (dg.CurrentCell.ColumnIndex == ColMin || dg.CurrentCell.ColumnIndex == ColMax) return;

            _view[g, s].Name = null;
            _view[g, s].Min = float.NaN;
            _view[g, s].Max = float.NaN;

            FillRow(g, s);
            e.Handled = true;
        }

        private void GridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != ColColor || e.RowIndex < 0) return;

            var dg = (DataGridView)sender;
            int g = (int)dg.Tag;
            int s = e.RowIndex;

            if (_view[g, s].Empty) return;

            using (var d = new ColorDialog { Color = _view[g, s].Color, FullOpen = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;

                _view[g, s].Color = d.Color;
                FillRow(g, s);
            }
        }

        private void OkClick(object sender, EventArgs e)
        {
            /* Закончить правку клетки до снятия значений: иначе набранное, но не
             * подтверждённое Enter'ом, потерялось бы молча. */
            for (int g = 0; g < ScopeView.Groups; g++) _grid[g].EndEdit();

            Harvest();
        }
    }
}
