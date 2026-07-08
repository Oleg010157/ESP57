namespace ICDIBasic
{
    partial class SettingsForm
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
            this.readCanTimer = new System.Windows.Forms.NumericUpDown();
            this.displayTimer = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.settingsOk = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.readCanTimer)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.displayTimer)).BeginInit();
            this.SuspendLayout();
            // 
            // readCanTimer
            // 
            this.readCanTimer.Location = new System.Drawing.Point(32, 32);
            this.readCanTimer.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.readCanTimer.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.readCanTimer.Name = "readCanTimer";
            this.readCanTimer.Size = new System.Drawing.Size(120, 20);
            this.readCanTimer.TabIndex = 0;
            this.readCanTimer.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // displayTimer
            // 
            this.displayTimer.Location = new System.Drawing.Point(32, 83);
            this.displayTimer.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.displayTimer.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.displayTimer.Name = "displayTimer";
            this.displayTimer.Size = new System.Drawing.Size(120, 20);
            this.displayTimer.TabIndex = 1;
            this.displayTimer.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(29, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(169, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Частота считывания с CAN (мс)";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(29, 67);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(182, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Частота отображения данных (мс)";
            // 
            // settingsOk
            // 
            this.settingsOk.Location = new System.Drawing.Point(207, 120);
            this.settingsOk.Name = "settingsOk";
            this.settingsOk.Size = new System.Drawing.Size(75, 23);
            this.settingsOk.TabIndex = 4;
            this.settingsOk.Text = "Ok";
            this.settingsOk.UseVisualStyleBackColor = true;
            this.settingsOk.Click += new System.EventHandler(this.settingsOk_Click);
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(294, 155);
            this.Controls.Add(this.settingsOk);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.displayTimer);
            this.Controls.Add(this.readCanTimer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(300, 300);
            this.MinimumSize = new System.Drawing.Size(300, 180);
            this.Name = "SettingsForm";
            this.Text = "Настройка";
            this.Load += new System.EventHandler(this.SettingsForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.readCanTimer)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.displayTimer)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown readCanTimer;
        private System.Windows.Forms.NumericUpDown displayTimer;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button settingsOk;
    }
}