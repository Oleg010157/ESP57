namespace ICDIBasic
{
    partial class Form3
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.zedGraph2 = new ZedGraph.ZedGraphControl();
            this.value1 = new System.Windows.Forms.TextBox();
            this.value2 = new System.Windows.Forms.TextBox();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.checkBox3 = new System.Windows.Forms.CheckBox();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.comboBox3 = new System.Windows.Forms.ComboBox();
            this.value3 = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.numericUpDown4 = new System.Windows.Forms.NumericUpDown();
            this.labelPeriod = new System.Windows.Forms.Label();
            this.labelSpan = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).BeginInit();
            this.SuspendLayout();
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // zedGraph2
            // 
            this.zedGraph2.Location = new System.Drawing.Point(0, -1);
            this.zedGraph2.Name = "zedGraph2";
            this.zedGraph2.ScrollGrace = 0D;
            this.zedGraph2.ScrollMaxX = 0D;
            this.zedGraph2.ScrollMaxY = 0D;
            this.zedGraph2.ScrollMaxY2 = 0D;
            this.zedGraph2.ScrollMinX = 0D;
            this.zedGraph2.ScrollMinY = 0D;
            this.zedGraph2.ScrollMinY2 = 0D;
            this.zedGraph2.Size = new System.Drawing.Size(752, 332);
            this.zedGraph2.TabIndex = 0;
            // 
            // value1
            // 
            this.value1.Location = new System.Drawing.Point(243, 336);
            this.value1.Name = "value1";
            this.value1.ReadOnly = true;
            this.value1.Size = new System.Drawing.Size(93, 20);
            this.value1.TabIndex = 1;
            this.value1.TabStop = false;
            this.value1.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // value2
            // 
            this.value2.Location = new System.Drawing.Point(243, 366);
            this.value2.Name = "value2";
            this.value2.ReadOnly = true;
            this.value2.Size = new System.Drawing.Size(93, 20);
            this.value2.TabIndex = 2;
            this.value2.TabStop = false;
            this.value2.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(136, 336);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(84, 21);
            this.comboBox1.TabIndex = 6;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.ForeColor = System.Drawing.Color.Red;
            this.checkBox1.Location = new System.Drawing.Point(8, 340);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(106, 17);
            this.checkBox1.TabIndex = 8;
            this.checkBox1.Text = "Первый график";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // checkBox2
            // 
            this.checkBox2.AutoSize = true;
            this.checkBox2.ForeColor = System.Drawing.Color.Yellow;
            this.checkBox2.Location = new System.Drawing.Point(8, 370);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(102, 17);
            this.checkBox2.TabIndex = 9;
            this.checkBox2.Text = "Второй график";
            this.checkBox2.UseVisualStyleBackColor = true;
            // 
            // checkBox3
            // 
            this.checkBox3.AutoSize = true;
            this.checkBox3.ForeColor = System.Drawing.Color.Purple;
            this.checkBox3.Location = new System.Drawing.Point(8, 400);
            this.checkBox3.Name = "checkBox3";
            this.checkBox3.Size = new System.Drawing.Size(102, 17);
            this.checkBox3.TabIndex = 10;
            this.checkBox3.Text = "Третий график";
            this.checkBox3.UseVisualStyleBackColor = true;
            // 
            // comboBox2
            // 
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Location = new System.Drawing.Point(136, 366);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(84, 21);
            this.comboBox2.TabIndex = 11;
            // 
            // comboBox3
            // 
            this.comboBox3.FormattingEnabled = true;
            this.comboBox3.Location = new System.Drawing.Point(136, 396);
            this.comboBox3.Name = "comboBox3";
            this.comboBox3.Size = new System.Drawing.Size(84, 21);
            this.comboBox3.TabIndex = 12;
            // 
            // value3
            // 
            this.value3.Location = new System.Drawing.Point(243, 396);
            this.value3.Name = "value3";
            this.value3.ReadOnly = true;
            this.value3.Size = new System.Drawing.Size(93, 20);
            this.value3.TabIndex = 13;
            this.value3.TabStop = false;
            this.value3.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.Khaki;
            this.button1.Location = new System.Drawing.Point(649, 366);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(103, 23);
            this.button1.TabIndex = 14;
            this.button1.Text = "Зафиксировать";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // numericUpDown4
            // 
            this.numericUpDown4.Increment = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numericUpDown4.Location = new System.Drawing.Point(436, 336);
            this.numericUpDown4.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numericUpDown4.Minimum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numericUpDown4.Name = "numericUpDown4";
            this.numericUpDown4.Size = new System.Drawing.Size(70, 20);
            this.numericUpDown4.TabIndex = 15;
            this.numericUpDown4.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.numericUpDown4.ValueChanged += new System.EventHandler(this.numericUpDown4_ValueChanged);
            // 
            // labelPeriod
            // 
            this.labelPeriod.AutoSize = true;
            this.labelPeriod.Location = new System.Drawing.Point(360, 339);
            this.labelPeriod.Name = "labelPeriod";
            this.labelPeriod.Size = new System.Drawing.Size(67, 13);
            this.labelPeriod.TabIndex = 18;
            this.labelPeriod.Text = "Период, мс";
            // 
            // labelSpan
            // 
            this.labelSpan.AutoSize = true;
            this.labelSpan.Location = new System.Drawing.Point(360, 369);
            this.labelSpan.Name = "labelSpan";
            this.labelSpan.Size = new System.Drawing.Size(64, 13);
            this.labelSpan.TabIndex = 19;
            this.labelSpan.Text = "Окно: 50 с";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(649, 337);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(103, 23);
            this.button2.TabIndex = 17;
            this.button2.Text = "Очистить";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // Form3
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.ClientSize = new System.Drawing.Size(764, 422);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.labelSpan);
            this.Controls.Add(this.labelPeriod);
            this.Controls.Add(this.numericUpDown4);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.value3);
            this.Controls.Add(this.comboBox3);
            this.Controls.Add(this.comboBox2);
            this.Controls.Add(this.checkBox3);
            this.Controls.Add(this.checkBox2);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.value2);
            this.Controls.Add(this.value1);
            this.Controls.Add(this.zedGraph2);
            this.Name = "Form3";
            this.Text = "Form3";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form3_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ZedGraph.ZedGraphControl zedGraph2;
        public System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TextBox value1;
        private System.Windows.Forms.TextBox value2;
        private System.Windows.Forms.TextBox value3;
        public System.Windows.Forms.ComboBox comboBox1;
        public System.Windows.Forms.ComboBox comboBox2;
        public System.Windows.Forms.ComboBox comboBox3;
        public System.Windows.Forms.CheckBox checkBox1;
        public System.Windows.Forms.CheckBox checkBox2;
        public System.Windows.Forms.CheckBox checkBox3;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.NumericUpDown numericUpDown4;
        private System.Windows.Forms.Label labelPeriod;
        private System.Windows.Forms.Label labelSpan;
        private System.Windows.Forms.Button button2;
    }
}
