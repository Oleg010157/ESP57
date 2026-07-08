using System.ComponentModel;
using System.Windows.Forms;
using ZedGraph;
using Label = System.Windows.Forms.Label;

namespace ICDIBasic.Stepulyov
{
    public partial class AStSplFast
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
            this.components = new System.ComponentModel.Container();
            this.ZGct = new ZedGraph.ZedGraphControl();
            this.cbc1 = new System.Windows.Forms.CheckBox();
            this.cbc2 = new System.Windows.Forms.CheckBox();
            this.cbc3 = new System.Windows.Forms.CheckBox();
            this.cbc4 = new System.Windows.Forms.CheckBox();
            this.cbc5 = new System.Windows.Forms.CheckBox();
            this.cbc6 = new System.Windows.Forms.CheckBox();
            this.cbc7 = new System.Windows.Forms.CheckBox();
            this.cbc8 = new System.Windows.Forms.CheckBox();
            this.cbc9 = new System.Windows.Forms.CheckBox();
            this.cbc10 = new System.Windows.Forms.CheckBox();
            this.cbc11 = new System.Windows.Forms.CheckBox();
            this.cbc12 = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cbXY = new System.Windows.Forms.CheckBox();
            this.pictureBox55 = new System.Windows.Forms.PictureBox();
            this.udLvl = new System.Windows.Forms.NumericUpDown();
            this.udCh = new System.Windows.Forms.NumericUpDown();
            this.udRng = new System.Windows.Forms.NumericUpDown();
            this.chBox_GrUpdEn = new System.Windows.Forms.CheckBox();
            this.cb_tm = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox55)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udLvl)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udCh)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udRng)).BeginInit();
            this.SuspendLayout();
            // 
            // ZGct
            // 
            this.ZGct.IsShowPointValues = true;
            this.ZGct.Location = new System.Drawing.Point(1, 19);
            this.ZGct.Name = "ZGct";
            this.ZGct.ScrollGrace = 0D;
            this.ZGct.ScrollMaxX = 0D;
            this.ZGct.ScrollMaxY = 0D;
            this.ZGct.ScrollMaxY2 = 0D;
            this.ZGct.ScrollMinX = 0D;
            this.ZGct.ScrollMinY = 0D;
            this.ZGct.ScrollMinY2 = 0D;
            this.ZGct.Size = new System.Drawing.Size(1016, 470);
            this.ZGct.TabIndex = 0;
            // 
            // cbc1
            // 
            this.cbc1.AutoSize = true;
            this.cbc1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(128)))));
            this.cbc1.Location = new System.Drawing.Point(12, 0);
            this.cbc1.Name = "cbc1";
            this.cbc1.Size = new System.Drawing.Size(32, 17);
            this.cbc1.TabIndex = 1;
            this.cbc1.Text = "1";
            this.cbc1.UseVisualStyleBackColor = false;
            // 
            // cbc2
            // 
            this.cbc2.AutoSize = true;
            this.cbc2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(192)))), ((int)(((byte)(255)))));
            this.cbc2.Location = new System.Drawing.Point(44, 0);
            this.cbc2.Name = "cbc2";
            this.cbc2.Size = new System.Drawing.Size(32, 17);
            this.cbc2.TabIndex = 2;
            this.cbc2.Text = "2";
            this.cbc2.UseVisualStyleBackColor = false;
            // 
            // cbc3
            // 
            this.cbc3.AutoSize = true;
            this.cbc3.BackColor = System.Drawing.SystemColors.HotTrack;
            this.cbc3.Location = new System.Drawing.Point(76, 0);
            this.cbc3.Name = "cbc3";
            this.cbc3.Size = new System.Drawing.Size(32, 17);
            this.cbc3.TabIndex = 3;
            this.cbc3.Text = "3";
            this.cbc3.UseVisualStyleBackColor = false;
            // 
            // cbc4
            // 
            this.cbc4.AutoSize = true;
            this.cbc4.BackColor = System.Drawing.Color.PeachPuff;
            this.cbc4.Location = new System.Drawing.Point(108, 0);
            this.cbc4.Name = "cbc4";
            this.cbc4.Size = new System.Drawing.Size(32, 17);
            this.cbc4.TabIndex = 4;
            this.cbc4.Text = "4";
            this.cbc4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.cbc4.UseVisualStyleBackColor = false;
            // 
            // cbc5
            // 
            this.cbc5.AutoSize = true;
            this.cbc5.BackColor = System.Drawing.Color.Cyan;
            this.cbc5.Location = new System.Drawing.Point(140, 0);
            this.cbc5.Name = "cbc5";
            this.cbc5.Size = new System.Drawing.Size(32, 17);
            this.cbc5.TabIndex = 5;
            this.cbc5.Text = "5";
            this.cbc5.UseVisualStyleBackColor = false;
            // 
            // cbc6
            // 
            this.cbc6.AutoSize = true;
            this.cbc6.BackColor = System.Drawing.Color.Yellow;
            this.cbc6.Location = new System.Drawing.Point(172, 0);
            this.cbc6.Name = "cbc6";
            this.cbc6.Size = new System.Drawing.Size(32, 17);
            this.cbc6.TabIndex = 6;
            this.cbc6.Text = "6";
            this.cbc6.UseVisualStyleBackColor = false;
            // 
            // cbc7
            // 
            this.cbc7.AutoSize = true;
            this.cbc7.BackColor = System.Drawing.Color.Lime;
            this.cbc7.Location = new System.Drawing.Point(204, 0);
            this.cbc7.Name = "cbc7";
            this.cbc7.Size = new System.Drawing.Size(32, 17);
            this.cbc7.TabIndex = 7;
            this.cbc7.Text = "7";
            this.cbc7.UseVisualStyleBackColor = false;
            // 
            // cbc8
            // 
            this.cbc8.AutoSize = true;
            this.cbc8.BackColor = System.Drawing.Color.Fuchsia;
            this.cbc8.Location = new System.Drawing.Point(236, 0);
            this.cbc8.Name = "cbc8";
            this.cbc8.Size = new System.Drawing.Size(32, 17);
            this.cbc8.TabIndex = 8;
            this.cbc8.Text = "8";
            this.cbc8.UseVisualStyleBackColor = false;
            // 
            // cbc9
            // 
            this.cbc9.AutoSize = true;
            this.cbc9.BackColor = System.Drawing.Color.LimeGreen;
            this.cbc9.Location = new System.Drawing.Point(268, 0);
            this.cbc9.Name = "cbc9";
            this.cbc9.Size = new System.Drawing.Size(32, 17);
            this.cbc9.TabIndex = 9;
            this.cbc9.Text = "9";
            this.cbc9.UseVisualStyleBackColor = false;
            // 
            // cbc10
            // 
            this.cbc10.AutoSize = true;
            this.cbc10.BackColor = System.Drawing.Color.DarkKhaki;
            this.cbc10.Location = new System.Drawing.Point(300, 0);
            this.cbc10.Name = "cbc10";
            this.cbc10.Size = new System.Drawing.Size(38, 17);
            this.cbc10.TabIndex = 10;
            this.cbc10.Text = "10";
            this.cbc10.UseVisualStyleBackColor = false;
            // 
            // cbc11
            // 
            this.cbc11.AutoSize = true;
            this.cbc11.BackColor = System.Drawing.Color.Cornsilk;
            this.cbc11.Location = new System.Drawing.Point(338, 0);
            this.cbc11.Name = "cbc11";
            this.cbc11.Size = new System.Drawing.Size(38, 17);
            this.cbc11.TabIndex = 11;
            this.cbc11.Text = "11";
            this.cbc11.UseVisualStyleBackColor = false;
            // 
            // cbc12
            // 
            this.cbc12.AutoSize = true;
            this.cbc12.BackColor = System.Drawing.SystemColors.Highlight;
            this.cbc12.Location = new System.Drawing.Point(376, 0);
            this.cbc12.Name = "cbc12";
            this.cbc12.Size = new System.Drawing.Size(38, 17);
            this.cbc12.TabIndex = 12;
            this.cbc12.Text = "12";
            this.cbc12.UseVisualStyleBackColor = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(675, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 14;
            this.label1.Text = "label1";
            // 
            // cbXY
            // 
            this.cbXY.AutoSize = true;
            this.cbXY.Location = new System.Drawing.Point(420, 0);
            this.cbXY.Name = "cbXY";
            this.cbXY.Size = new System.Drawing.Size(43, 17);
            this.cbXY.TabIndex = 15;
            this.cbXY.Text = "Y-X";
            this.cbXY.UseVisualStyleBackColor = true;
            // 
            // pictureBox55
            // 
            this.pictureBox55.Location = new System.Drawing.Point(985, 0);
            this.pictureBox55.Name = "pictureBox55";
            this.pictureBox55.Size = new System.Drawing.Size(19, 19);
            this.pictureBox55.TabIndex = 279;
            this.pictureBox55.TabStop = false;
            this.pictureBox55.Visible = false;
            // 
            // udLvl
            // 
            this.udLvl.Location = new System.Drawing.Point(469, -1);
            this.udLvl.Maximum = new decimal(new int[] {
            1200,
            0,
            0,
            0});
            this.udLvl.Minimum = new decimal(new int[] {
            1200,
            0,
            0,
            -2147483648});
            this.udLvl.Name = "udLvl";
            this.udLvl.Size = new System.Drawing.Size(59, 20);
            this.udLvl.TabIndex = 280;
            // 
            // udCh
            // 
            this.udCh.Location = new System.Drawing.Point(571, -1);
            this.udCh.Maximum = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.udCh.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.udCh.Name = "udCh";
            this.udCh.Size = new System.Drawing.Size(52, 20);
            this.udCh.TabIndex = 281;
            this.udCh.Value = new decimal(new int[] {
            12,
            0,
            0,
            0});
            // 
            // udRng
            // 
            this.udRng.Location = new System.Drawing.Point(528, -1);
            this.udRng.Name = "udRng";
            this.udRng.Size = new System.Drawing.Size(43, 20);
            this.udRng.TabIndex = 282;
            // 
            // chBox_GrUpdEn
            // 
            this.chBox_GrUpdEn.AutoSize = true;
            this.chBox_GrUpdEn.Checked = true;
            this.chBox_GrUpdEn.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chBox_GrUpdEn.Location = new System.Drawing.Point(623, 2);
            this.chBox_GrUpdEn.Name = "chBox_GrUpdEn";
            this.chBox_GrUpdEn.Size = new System.Drawing.Size(15, 14);
            this.chBox_GrUpdEn.TabIndex = 283;
            this.chBox_GrUpdEn.UseVisualStyleBackColor = true;
            // 
            // cb_tm
            // 
            this.cb_tm.AutoSize = true;
            this.cb_tm.Location = new System.Drawing.Point(638, 2);
            this.cb_tm.Name = "cb_tm";
            this.cb_tm.Size = new System.Drawing.Size(15, 14);
            this.cb_tm.TabIndex = 284;
            this.cb_tm.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(653, -2);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(22, 23);
            this.button1.TabIndex = 285;
            this.button1.UseVisualStyleBackColor = true;
            // 
            // AStSplFast
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1016, 492);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.cb_tm);
            this.Controls.Add(this.chBox_GrUpdEn);
            this.Controls.Add(this.udRng);
            this.Controls.Add(this.udCh);
            this.Controls.Add(this.udLvl);
            this.Controls.Add(this.pictureBox55);
            this.Controls.Add(this.cbXY);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbc12);
            this.Controls.Add(this.cbc11);
            this.Controls.Add(this.cbc10);
            this.Controls.Add(this.cbc9);
            this.Controls.Add(this.cbc8);
            this.Controls.Add(this.cbc7);
            this.Controls.Add(this.cbc6);
            this.Controls.Add(this.cbc5);
            this.Controls.Add(this.cbc4);
            this.Controls.Add(this.cbc3);
            this.Controls.Add(this.cbc2);
            this.Controls.Add(this.cbc1);
            this.Controls.Add(this.ZGct);
            this.DoubleBuffered = true;
            this.MinimumSize = new System.Drawing.Size(1024, 512);
            this.Name = "AStSplFast";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Spl_Fast_FormClosing);
            this.Shown += new System.EventHandler(this.Spl_FAST_Shown);
            this.Resize += new System.EventHandler(this.Spl_Fast_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox55)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udLvl)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udCh)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udRng)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion
        private ZedGraphControl ZGct;
        private CheckBox cbc1;
        private CheckBox cbc2;
        private CheckBox cbc3;
        private CheckBox cbc4;
        private CheckBox cbc5;
        private CheckBox cbc6;
        private CheckBox cbc7;
        private CheckBox cbc8;
        private CheckBox cbc9;
        private CheckBox cbc10;
        private CheckBox cbc11;
        private CheckBox cbc12;
        private Label label1;
        private CheckBox cbXY;
        private PictureBox pictureBox55;
        private NumericUpDown udLvl;
        private NumericUpDown udCh;
        private NumericUpDown udRng;
        private CheckBox chBox_GrUpdEn;
        private CheckBox cb_tm;
        private Button button1;
    }
}
