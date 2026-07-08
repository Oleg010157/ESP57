namespace ICDIBasic
{
    partial class EngineLog
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
            this.label1 = new System.Windows.Forms.Label();
            this.label_pole_pair = new System.Windows.Forms.Label();
            this.label_Rstator = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.label_Rrotor = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioButton_percent1 = new System.Windows.Forms.RadioButton();
            this.radioButton_abs1 = new System.Windows.Forms.RadioButton();
            this.label14 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.label9 = new System.Windows.Forms.Label();
            this.textBox6 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.textBox5 = new System.Windows.Forms.TextBox();
            this.textBox4 = new System.Windows.Forms.TextBox();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBox_pole = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label1.Location = new System.Drawing.Point(6, 8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(120, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Тип Двигателя";
            // 
            // label_pole_pair
            // 
            this.label_pole_pair.AutoSize = true;
            this.label_pole_pair.Font = new System.Drawing.Font("Times New Roman", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_pole_pair.Location = new System.Drawing.Point(6, 47);
            this.label_pole_pair.Name = "label_pole_pair";
            this.label_pole_pair.Size = new System.Drawing.Size(135, 17);
            this.label_pole_pair.TabIndex = 1;
            this.label_pole_pair.Text = "Кол-во пар полюсов:";
            // 
            // label_Rstator
            // 
            this.label_Rstator.AutoSize = true;
            this.label_Rstator.Font = new System.Drawing.Font("Times New Roman", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_Rstator.Location = new System.Drawing.Point(6, 72);
            this.label_Rstator.Name = "label_Rstator";
            this.label_Rstator.Size = new System.Drawing.Size(196, 17);
            this.label_Rstator.TabIndex = 2;
            this.label_Rstator.Text = "Сопротивление статора (акт.):";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(308, 244);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "Сохранить";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label_Rrotor
            // 
            this.label_Rrotor.AutoSize = true;
            this.label_Rrotor.Font = new System.Drawing.Font("Times New Roman", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_Rrotor.Location = new System.Drawing.Point(6, 97);
            this.label_Rrotor.Name = "label_Rrotor";
            this.label_Rrotor.Size = new System.Drawing.Size(189, 17);
            this.label_Rrotor.TabIndex = 4;
            this.label_Rrotor.Text = "Сопротивление ротора (акт.):";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Times New Roman", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(6, 122);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(153, 17);
            this.label5.TabIndex = 5;
            this.label5.Text = "Индуктивность ротора:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Times New Roman", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(6, 147);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(160, 17);
            this.label6.TabIndex = 6;
            this.label6.Text = "Индуктивность статора:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioButton_percent1);
            this.groupBox1.Controls.Add(this.radioButton_abs1);
            this.groupBox1.Controls.Add(this.label14);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.checkBox1);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.textBox6);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.textBox5);
            this.groupBox1.Controls.Add(this.textBox4);
            this.groupBox1.Controls.Add(this.textBox3);
            this.groupBox1.Controls.Add(this.textBox2);
            this.groupBox1.Controls.Add(this.textBox_pole);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.label_Rrotor);
            this.groupBox1.Controls.Add(this.label_Rstator);
            this.groupBox1.Controls.Add(this.label_pole_pair);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(8, 1);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(384, 237);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            // 
            // radioButton_percent1
            // 
            this.radioButton_percent1.AutoSize = true;
            this.radioButton_percent1.Checked = true;
            this.radioButton_percent1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.radioButton_percent1.Location = new System.Drawing.Point(305, 191);
            this.radioButton_percent1.Name = "radioButton_percent1";
            this.radioButton_percent1.Size = new System.Drawing.Size(38, 21);
            this.radioButton_percent1.TabIndex = 23;
            this.radioButton_percent1.TabStop = true;
            this.radioButton_percent1.Text = "%";
            this.radioButton_percent1.UseVisualStyleBackColor = true;
            this.radioButton_percent1.CheckedChanged += new System.EventHandler(this.radioButton_percent1_CheckedChanged);
            // 
            // radioButton_abs1
            // 
            this.radioButton_abs1.AutoSize = true;
            this.radioButton_abs1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.radioButton_abs1.Location = new System.Drawing.Point(304, 168);
            this.radioButton_abs1.Name = "radioButton_abs1";
            this.radioButton_abs1.Size = new System.Drawing.Size(75, 21);
            this.radioButton_abs1.TabIndex = 22;
            this.radioButton_abs1.Text = "об./мин";
            this.radioButton_abs1.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(305, 150);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(19, 13);
            this.label14.TabIndex = 21;
            this.label14.Text = "Гн";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(305, 125);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(19, 13);
            this.label13.TabIndex = 20;
            this.label13.Text = "Гн";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(305, 100);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(26, 13);
            this.label12.TabIndex = 19;
            this.label12.Text = " Ом";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(305, 76);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(26, 13);
            this.label11.TabIndex = 18;
            this.label11.Text = " Ом";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(305, 50);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(27, 13);
            this.label10.TabIndex = 17;
            this.label10.Text = "Пар";
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(255, 217);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(15, 14);
            this.checkBox1.TabIndex = 16;
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Times New Roman", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(6, 217);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(243, 17);
            this.label9.TabIndex = 15;
            this.label9.Text = "Выставить параметры по умолчанию";
            // 
            // textBox6
            // 
            this.textBox6.Location = new System.Drawing.Point(235, 169);
            this.textBox6.Name = "textBox6";
            this.textBox6.Size = new System.Drawing.Size(63, 20);
            this.textBox6.TabIndex = 14;
            this.textBox6.Text = "2.5";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Times New Roman", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(6, 172);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(183, 17);
            this.label8.TabIndex = 13;
            this.label8.Text = "Критическое скольжение (!)";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label7.Location = new System.Drawing.Point(151, 8);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(58, 16);
            this.label7.TabIndex = 12;
            this.label7.Text = "ТАД 350";
            // 
            // textBox5
            // 
            this.textBox5.Location = new System.Drawing.Point(235, 144);
            this.textBox5.Name = "textBox5";
            this.textBox5.Size = new System.Drawing.Size(63, 20);
            this.textBox5.TabIndex = 11;
            this.textBox5.Text = "0.316423";
            // 
            // textBox4
            // 
            this.textBox4.Location = new System.Drawing.Point(235, 119);
            this.textBox4.Name = "textBox4";
            this.textBox4.Size = new System.Drawing.Size(63, 20);
            this.textBox4.TabIndex = 10;
            this.textBox4.Text = "0.316423";
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(235, 94);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(63, 20);
            this.textBox3.TabIndex = 9;
            this.textBox3.Text = "0.2";
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(235, 71);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(63, 20);
            this.textBox2.TabIndex = 8;
            this.textBox2.Text = "0.4";
            // 
            // textBox_pole
            // 
            this.textBox_pole.Location = new System.Drawing.Point(235, 44);
            this.textBox_pole.Name = "textBox_pole";
            this.textBox_pole.Size = new System.Drawing.Size(63, 20);
            this.textBox_pole.TabIndex = 7;
            this.textBox_pole.Text = "3";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(8, 244);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 8;
            this.button2.Text = "Изменить";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "ТАД-170",
            "ТАД-350"});
            this.comboBox1.Location = new System.Drawing.Point(220, 246);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(121, 21);
            this.comboBox1.TabIndex = 9;
            // 
            // Engine_log
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(395, 279);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.button1);
            this.Name = "EngineLog";
            this.Text = "Параметры Двигателя";
            this.Load += new System.EventHandler(this.Engine_log_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Engine_log_FormClosing);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label_pole_pair;
        private System.Windows.Forms.Label label_Rstator;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label_Rrotor;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textBox5;
        private System.Windows.Forms.TextBox textBox4;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBox_pole;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox textBox6;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label13;
        public System.Windows.Forms.Label label7;
        private System.Windows.Forms.RadioButton radioButton_percent1;
        private System.Windows.Forms.RadioButton radioButton_abs1;
        private System.Windows.Forms.ComboBox comboBox1;
    }
}