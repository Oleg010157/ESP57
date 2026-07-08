namespace ICDIBasic
{
    partial class FormEngName
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
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button_chose = new System.Windows.Forms.Button();
            this.button_save = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(191, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "Введите марку двигателя:";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(209, 7);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(171, 20);
            this.textBox1.TabIndex = 1;
            // 
            // button_chose
            // 
            this.button_chose.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.button_chose.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.button_chose.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.button_chose.Location = new System.Drawing.Point(12, 30);
            this.button_chose.Name = "button_chose";
            this.button_chose.Size = new System.Drawing.Size(188, 23);
            this.button_chose.TabIndex = 2;
            this.button_chose.Text = "Выбрать из доступной базы ...";
            this.button_chose.UseVisualStyleBackColor = false;
            this.button_chose.Click += new System.EventHandler(this.button_chose_Click);
            // 
            // button_save
            // 
            this.button_save.Location = new System.Drawing.Point(305, 30);
            this.button_save.Name = "button_save";
            this.button_save.Size = new System.Drawing.Size(75, 23);
            this.button_save.TabIndex = 3;
            this.button_save.Text = "Сохранить";
            this.button_save.UseVisualStyleBackColor = true;
            this.button_save.Click += new System.EventHandler(this.button_save_Click);
            // 
            // Form_EngName
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(392, 55);
            this.Controls.Add(this.button_save);
            this.Controls.Add(this.button_chose);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.label1);
            this.Name = "FormEngName";
            this.Text = "Марка Двигателя";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.f_engname_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button button_chose;
        private System.Windows.Forms.Button button_save;
    }
}