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
     * Слева список всех каналов шаблона, справа два поля по шесть слотов. На
     * график попадает только то, что перенесено в слот: показывать все
     * пятнадцать кривых разом бессмысленно, а выбор - дело оператора.
     *
     * ВЫЧИСЛЯЕМЫЕ КАНАЛЫ В СПИСКЕ ЕСТЬ И ПОКАЗАНЫ КРАСНЫМ, как в Wscope, но
     * перетащить их нельзя. Это не полумера, а честный показ положения дел: в
     * шаблоне они объявлены, и не увидев их, оператор искал бы их глазами зря;
     * но считает их выражения только Wscope, и нарисовать их мы не можем.
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

        /// <summary>Ширина списка каналов. Хватает на самое длинное имя с запасом.</summary>
        private const int ListWidth = 210;

        private readonly IniChannelMap _map;
        private readonly ScopeView _view;

        private readonly ListBox _list;
        private readonly DataGridView[] _grid = new DataGridView[ScopeView.Groups];

        /// <summary>Правим копию: Отмена должна возвращать как было.</summary>
        public ScopeView Result { get { return _view; } }

        /// <summary>Строка списка: имя и признак «вычисляемый», чтобы красить.</summary>
        private sealed class Item
        {
            public Item(string name, bool calculated)
            {
                Name = name;
                Calculated = calculated;
            }

            public readonly string Name;
            public readonly bool Calculated;

            public override string ToString()
            {
                return Name;
            }
        }

        public ScopeConfigForm(IniChannelMap map, ScopeView view)
        {
            _map = map;
            _view = view.Clone();

            Text = "Каналы - " + map.Template;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;

            /* Ширина с запасом на четыре столбца в обоих полях: прежние 700
             * обрезали «Макс» и «Цвет». */
            ClientSize = new Size(880, 460);
            MinimumSize = new Size(760, 400);

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                SelectionMode = SelectionMode.One,
                DrawMode = DrawMode.OwnerDrawFixed,
            };
            _list.DrawItem += ListDrawItem;
            _list.MouseDown += ListMouseDown;
            _list.MouseMove += ListMouseMove;
            _list.MouseUp += ListMouseUp;
            _list.DoubleClick += ListDoubleClick;

            FillList();

            var leftBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Каналы кадра",
                Padding = new Padding(6, 4, 6, 6),
            };
            leftBox.Controls.Add(_list);

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
                    Padding = new Padding(6, 4, 6, 6),
                };
                box.Controls.Add(_grid[g]);
                right.Controls.Add(box, 0, g);
            }

            /* TableLayoutPanel, а не SplitContainer: у того SplitterDistance,
             * заданный в конструкторе, не держится - контрол тогда ещё узкий, и
             * значение обрезается по его ширине. Именно из-за этого список
             * занимал почти всё окно, а поля уезжали за правый край. */
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8, 8, 8, 4),
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ListWidth));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            main.Controls.Add(leftBox, 0, 0);
            main.Controls.Add(right, 1, 0);

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
                Height = 38,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(10, 2, 10, 0),
                Text = "Перетащите канал в поле или щёлкните дважды. Delete убирает, " +
                       "щелчок по цвету меняет. Пустые границы - масштаб сам." +
                       Environment.NewLine +
                       "Красные - вычисляемые каналы шаблона: их считает только Wscope, " +
                       "сюда их перенести нельзя.",
            };

            Controls.Add(main);
            Controls.Add(hint);
            Controls.Add(buttons);

            AcceptButton = ok;
            CancelButton = cancel;

            Fill();
        }

        /// <summary>
        /// Список каналов по алфавиту, без учёта регистра - как в Wscope: на
        /// двадцати именах искать нужное в порядке слов кадра неудобно.
        /// Вычисляемые идут вперемешку с измеряемыми, отличаясь цветом.
        /// </summary>
        private void FillList()
        {
            var items = new System.Collections.Generic.List<Item>();

            for (int i = 0; i < _map.Count; i++) items.Add(new Item(_map[i].Name, false));
            foreach (string n in _map.Calculated) items.Add(new Item(n, true));

            items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            foreach (Item it in items) _list.Items.Add(it);
        }

        private void ListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var it = (Item)_list.Items[e.Index];

            e.DrawBackground();

            bool sel = (e.State & DrawItemState.Selected) != 0;

            Color fore = it.Calculated
                ? (sel ? Color.FromArgb(255, 200, 200) : Color.Firebrick)
                : (sel ? SystemColors.HighlightText : SystemColors.WindowText);

            TextRenderer.DrawText(e.Graphics, it.Name, e.Font, e.Bounds, fore,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            e.DrawFocusRectangle();
        }

        private DataGridView NewGrid(int group)
        {
            var dg = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AllowDrop = true,
                BackgroundColor = SystemColors.Window,
                Tag = group,
            };

            /* EditOnEnter здесь СТОЯЛ И ЛОМАЛ ПЕРЕТАСКИВАНИЕ: текущая клетка
             * сразу уходила в правку, поверх неё ложился редактор TextBox, у
             * которого AllowDrop нет, и курсор с грузом попадал на него, а не на
             * таблицу. Снаружи это выглядело как «перетаскивание не работает».
             * Оставляем обычный режим: правка по щелчку или F2. */

            dg.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Канал",
                ReadOnly = true,
                FillWeight = 130,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });
            dg.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Мин",
                FillWeight = 85,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });
            dg.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Макс",
                FillWeight = 85,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });
            dg.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Цвет",
                ReadOnly = true,
                FillWeight = 55,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });

            for (int s = 0; s < ScopeView.SlotsPerGroup; s++) dg.Rows.Add();

            dg.DragEnter += GridDragOver;
            dg.DragOver += GridDragOver;
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
            {
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++) FillRow(g, s);

                ClearPick(g);
            }
        }

        /// <summary>
        /// Снять выделение: таблица сама подсвечивает первую клетку, и пустой
        /// слот с синей полосой читается как занятый.
        /// </summary>
        private void ClearPick(int g)
        {
            _grid[g].ClearSelection();
            _grid[g].CurrentCell = null;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            /* Повторяем после показа: в конструкторе это не держится - таблица
             * восстанавливает текущую клетку, когда получает фокус. Фокус отдаём
             * списку, с него и начинают. */
            ActiveControl = _list;

            for (int g = 0; g < ScopeView.Groups; g++) ClearPick(g);
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

            /* Запятая и точка оба годятся: в клетку набирают как привычнее. */
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
        private Point _dragFrom;

        private void ListMouseDown(object sender, MouseEventArgs e)
        {
            int i = _list.IndexFromPoint(e.Location);

            _dragIndex = i;
            _dragFrom = e.Location;

            if (i >= 0) _list.SelectedIndex = i;
        }

        private void ListMouseUp(object sender, MouseEventArgs e)
        {
            _dragIndex = -1;
        }

        private void ListMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _dragIndex < 0) return;

            /* Порог системы: без него любое дрожание руки при щелчке начинало бы
             * перетаскивание, и выбрать канал в списке было бы нельзя. */
            Size d = SystemInformation.DragSize;
            if (Math.Abs(e.X - _dragFrom.X) < d.Width && Math.Abs(e.Y - _dragFrom.Y) < d.Height)
                return;

            var it = (Item)_list.Items[_dragIndex];
            _dragIndex = -1;

            /* Вычисляемые не переносим - считать их мы не умеем. */
            if (it.Calculated) return;

            _list.DoDragDrop(new DataObject(DataFormats.StringFormat, it.Name),
                             DragDropEffects.Copy);
        }

        /// <summary>
        /// Эффект ставится и на DragEnter, и на DragOver. Одного DragEnter мало:
        /// пока груз ходит над таблицей, она пересчитывает состояние, и без
        /// DragOver курсор показывает запрет, а бросок не доходит.
        /// </summary>
        private void GridDragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.StringFormat)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void GridDragDrop(object sender, DragEventArgs e)
        {
            var dg = (DataGridView)sender;
            int g = (int)dg.Tag;

            if (!e.Data.GetDataPresent(DataFormats.StringFormat)) return;

            Point p = dg.PointToClient(new Point(e.X, e.Y));
            DataGridView.HitTestInfo hit = dg.HitTest(p.X, p.Y);

            int s = hit.RowIndex;

            /* Бросок мимо строки - в шапку или ниже последней - кладём в первый
             * свободный слот этого поля, а не теряем: попасть в строку высотой
             * в два десятка пикселей выходит не всегда. */
            if (s < 0 || s >= ScopeView.SlotsPerGroup) s = FreeSlot(g);
            if (s < 0) return;

            Place(g, s, (string)e.Data.GetData(DataFormats.StringFormat));
        }

        private int FreeSlot(int g)
        {
            for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                if (_view[g, s].Empty) return s;

            return -1;
        }

        private void ListDoubleClick(object sender, EventArgs e)
        {
            var it = _list.SelectedItem as Item;
            if (it == null || it.Calculated) return;

            /* Первый свободный слот, сверху вниз: мышью попадать в строку не
             * всегда удобно, а действие то же самое. */
            for (int g = 0; g < ScopeView.Groups; g++)
            {
                int s = FreeSlot(g);
                if (s < 0) continue;

                Place(g, s, it.Name);
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
