using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    public partial class AlStInfoF : Form
    {
        public AlStInfoF(DataTable t)
        {
            InitializeComponent();
            Shown += AlSt_InfoF_Shown;
            dg.DataSource = t;
        }
        private void AlSt_InfoF_Shown(object sender, EventArgs e)
        {
            dg.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dg.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;
            dg.Columns[0].MinimumWidth = dg.Columns[1].MinimumWidth = 100;
            foreach (DataGridViewColumn column in dg.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.ReadOnly = true;
            }
            dg.AllowUserToAddRows = dg.RowHeadersVisible = dg.ShowCellToolTips = dg.ShowEditingIcon =
                dg.ShowEditingIcon = dg.ColumnHeadersVisible = dg.AllowUserToResizeRows = false;
            dg.SelectionChanged += (h, n) => { dg.ClearSelection(); };
            var screenRectangle = RectangleToScreen(ClientRectangle);
            dg.Height = dg.Rows.Count * dg.RowTemplate.Height + 15;
            var hght = dg.Location.Y + dg.Height + (screenRectangle.Top - Top);
            var wdth = dg.Columns[0].Width + dg.Columns[1].Width + 20;
            dg.Width = wdth;
            MaximumSize = new Size(wdth, hght);
            MinimumSize = new Size(wdth, hght);
            Height = hght;
            Width = wdth;
            for (var i = 0; i < dg.Rows.Count; i++)
            {
                for (var j = 0; j < dg.Columns.Count; j++) dg.Rows[i].Cells[j].Style.BackColor = BackColor;
                dg.Rows[i].Cells[0].Style.ForeColor = dg.Rows[i].Cells[1].Style.ForeColor = ForeColor;
            }
            dg.ClearSelection();
            dg.Refresh();
            dg.BackgroundColor = BackColor;
        }
    }
}