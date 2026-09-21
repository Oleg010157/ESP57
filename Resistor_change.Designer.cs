namespace ICDIBasic
{
    partial class ResistorChange
    {
        /// <summary>
        /// Требуется переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Обязательный метод для поддержки конструктора - не изменяйте
        /// содержимое данного метода при помощи редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.button_save = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.label25 = new System.Windows.Forms.Label();
            this.voltMax = new System.Windows.Forms.NumericUpDown();
            this.voltMin = new System.Windows.Forms.NumericUpDown();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.voltMax)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.voltMin)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox4
            // 
            this.groupBox4.BackColor = System.Drawing.Color.BlanchedAlmond;
            this.groupBox4.Controls.Add(this.button_save);
            this.groupBox4.Controls.Add(this.label8);
            this.groupBox4.Controls.Add(this.label7);
            this.groupBox4.Controls.Add(this.label26);
            this.groupBox4.Controls.Add(this.label25);
            this.groupBox4.Controls.Add(this.voltMax);
            this.groupBox4.Controls.Add(this.voltMin);
            this.groupBox4.Location = new System.Drawing.Point(1, 0);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(215, 91);
            this.groupBox4.TabIndex = 231;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Срабатывание резистора";
            // 
            // button_save
            // 
            this.button_save.BackColor = System.Drawing.Color.SandyBrown;
            this.button_save.Location = new System.Drawing.Point(9, 63);
            this.button_save.Name = "button_save";
            this.button_save.Size = new System.Drawing.Size(75, 23);
            this.button_save.TabIndex = 237;
            this.button_save.Text = "Сохранить";
            this.button_save.UseVisualStyleBackColor = false;
            this.button_save.Click += new System.EventHandler(this.button_save_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.BackColor = System.Drawing.Color.BlanchedAlmond;
            this.label8.Location = new System.Drawing.Point(6, 45);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(99, 13);
            this.label8.TabIndex = 236;
            this.label8.Text = "Макс.напряжение";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.BackColor = System.Drawing.Color.BlanchedAlmond;
            this.label7.Location = new System.Drawing.Point(6, 19);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(93, 13);
            this.label7.TabIndex = 235;
            this.label7.Text = "Мин.напряжение";
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label26.Location = new System.Drawing.Point(176, 16);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(17, 17);
            this.label26.TabIndex = 234;
            this.label26.Text = "V";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label25.Location = new System.Drawing.Point(176, 43);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(17, 17);
            this.label25.TabIndex = 233;
            this.label25.Text = "V";
            // 
            // voltMax
            // 
            this.voltMax.Location = new System.Drawing.Point(119, 43);
            this.voltMax.Maximum = new decimal(new int[] {
            1400,
            0,
            0,
            0});
            this.voltMax.Name = "voltMax";
            this.voltMax.Size = new System.Drawing.Size(58, 20);
            this.voltMax.TabIndex = 225;
            this.voltMax.Value = new decimal(new int[] {
            1200,
            0,
            0,
            0});
            this.voltMax.ValueChanged += new System.EventHandler(this.voltMax_ValueChanged);
            // 
            // voltMin
            // 
            this.voltMin.Location = new System.Drawing.Point(119, 17);
            this.voltMin.Maximum = new decimal(new int[] {
            1350,
            0,
            0,
            0});
            this.voltMin.Name = "voltMin";
            this.voltMin.Size = new System.Drawing.Size(58, 20);
            this.voltMin.TabIndex = 224;
            this.voltMin.Value = new decimal(new int[] {
            1100,
            0,
            0,
            0});
            this.voltMin.ValueChanged += new System.EventHandler(this.voltMin_ValueChanged);
            // 
            // Resistor_change
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(215, 90);
            this.Controls.Add(this.groupBox4);
            this.Name = "ResistorChange";
            this.Text = "Защитный резистор";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Resistor_change_FormClosing);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.voltMax)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.voltMin)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button button_save;
        public System.Windows.Forms.NumericUpDown voltMax;
        public System.Windows.Forms.NumericUpDown voltMin;
    }
}