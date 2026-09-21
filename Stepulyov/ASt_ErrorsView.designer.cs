using System.ComponentModel;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    partial class ErrorsView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            var dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItemErrs = new System.Windows.Forms.ToolStripMenuItem();
            this.ColumnsViewMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ErrCodeMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ErrBinCodeMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.CurrErrMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.OneTErrorMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.MultTErrorMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ErrCounterMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ErrDescMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.ClearErrorsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.ecodeMenu = new System.Windows.Forms.ToolStripTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToResizeColumns = false;
            this.dataGridView1.AllowUserToResizeRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView1.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.Disable;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView1.ColumnHeadersHeight = 40;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridView1.Location = new System.Drawing.Point(0, 27);
            this.dataGridView1.MultiSelect = false;
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.Size = new System.Drawing.Size(967, 136);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.VirtualMode = true;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemErrs});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(967, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItemErrs
            // 
            this.toolStripMenuItemErrs.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ColumnsViewMenu,
            this.toolStripSeparator1,
            this.ClearErrorsMenu,
            this.toolStripSeparator2,
            this.ecodeMenu});
            this.toolStripMenuItemErrs.Name = "toolStripMenuItemErrs";
            this.toolStripMenuItemErrs.Size = new System.Drawing.Size(59, 20);
            this.toolStripMenuItemErrs.Text = "Ошибки";
            // 
            // ColumnsViewMenu
            // 
            this.ColumnsViewMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ErrCodeMenu,
            this.ErrBinCodeMenu,
            this.CurrErrMenu,
            this.OneTErrorMenu,
            this.MultTErrorMenu,
            this.ErrCounterMenu,
            this.ErrDescMenu});
            this.ColumnsViewMenu.Name = "ColumnsViewMenu";
            this.ColumnsViewMenu.Size = new System.Drawing.Size(222, 22);
            this.ColumnsViewMenu.Text = "Отображать столбцы";
            // 
            // ErrCodeMenu
            // 
            this.ErrCodeMenu.Checked = true;
            this.ErrCodeMenu.CheckOnClick = true;
            this.ErrCodeMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ErrCodeMenu.Name = "ErrCodeMenu";
            this.ErrCodeMenu.Size = new System.Drawing.Size(199, 22);
            this.ErrCodeMenu.Text = "ID ошибки";
            // 
            // ErrBinCodeMenu
            // 
            this.ErrBinCodeMenu.Checked = true;
            this.ErrBinCodeMenu.CheckOnClick = true;
            this.ErrBinCodeMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ErrBinCodeMenu.Name = "ErrBinCodeMenu";
            this.ErrBinCodeMenu.Size = new System.Drawing.Size(199, 22);
            this.ErrBinCodeMenu.Text = "Код ошибки";
            // 
            // CurrErrMenu
            // 
            this.CurrErrMenu.Checked = true;
            this.CurrErrMenu.CheckOnClick = true;
            this.CurrErrMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.CurrErrMenu.Name = "CurrErrMenu";
            this.CurrErrMenu.Size = new System.Drawing.Size(199, 22);
            this.CurrErrMenu.Text = "Текущая ошибка";
            // 
            // OneTErrorMenu
            // 
            this.OneTErrorMenu.Checked = true;
            this.OneTErrorMenu.CheckOnClick = true;
            this.OneTErrorMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.OneTErrorMenu.Name = "OneTErrorMenu";
            this.OneTErrorMenu.Size = new System.Drawing.Size(199, 22);
            this.OneTErrorMenu.Text = "Однократная ошибка";
            // 
            // MultTErrorMenu
            // 
            this.MultTErrorMenu.Checked = true;
            this.MultTErrorMenu.CheckOnClick = true;
            this.MultTErrorMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.MultTErrorMenu.Name = "MultTErrorMenu";
            this.MultTErrorMenu.Size = new System.Drawing.Size(199, 22);
            this.MultTErrorMenu.Text = "Многократная ошибка";
            // 
            // ErrCounterMenu
            // 
            this.ErrCounterMenu.Checked = true;
            this.ErrCounterMenu.CheckOnClick = true;
            this.ErrCounterMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ErrCounterMenu.Name = "ErrCounterMenu";
            this.ErrCounterMenu.Size = new System.Drawing.Size(199, 22);
            this.ErrCounterMenu.Text = "Счётчик ошибок";
            // 
            // ErrDescMenu
            // 
            this.ErrDescMenu.Checked = true;
            this.ErrDescMenu.CheckOnClick = true;
            this.ErrDescMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ErrDescMenu.Name = "ErrDescMenu";
            this.ErrDescMenu.Size = new System.Drawing.Size(199, 22);
            this.ErrDescMenu.Text = "Описание ошибок";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(219, 6);
            // 
            // ClearErrorsMenu
            // 
            this.ClearErrorsMenu.Name = "ClearErrorsMenu";
            this.ClearErrorsMenu.Size = new System.Drawing.Size(222, 22);
            this.ClearErrorsMenu.Text = "Очистить историю ошибок";
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(219, 6);
            // 
            // ecodeMenu
            // 
            this.ecodeMenu.MaxLength = 3;
            this.ecodeMenu.Name = "ecodeMenu";
            this.ecodeMenu.Size = new System.Drawing.Size(130, 21);
            this.ecodeMenu.Text = "Имитировать ошибку";
            this.ecodeMenu.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ecodeMenu_KeyPress);
            this.ecodeMenu.Click += new System.EventHandler(this.ecodeMenu_Click);
            // 
            // ErrorsView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(967, 164);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "ErrorsView";
            this.Text = "Просмотр ошибок";
            this.TopMost = false;
            this.Load += new System.EventHandler(this.ErrorsView_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion
        private DataGridView dataGridView1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem toolStripMenuItemErrs;
        private ToolStripMenuItem ColumnsViewMenu;
        private ToolStripMenuItem ErrCodeMenu;
        private ToolStripMenuItem CurrErrMenu;
        private ToolStripMenuItem OneTErrorMenu;
        private ToolStripMenuItem MultTErrorMenu;
        private ToolStripMenuItem ErrCounterMenu;
        private ToolStripMenuItem ErrDescMenu;
        private ToolStripMenuItem ErrBinCodeMenu;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem ClearErrorsMenu;
        private ToolStripTextBox ecodeMenu;
        private ToolStripSeparator toolStripSeparator2;
    }
}
