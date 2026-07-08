namespace ICDIBasic
{
    partial class form_max_sAbs
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
            System.Windows.Forms.Label label_slipabs;
            this.text_sAbs_max = new System.Windows.Forms.TextBox();
            this.text_sRel_max = new System.Windows.Forms.TextBox();
            this.label_sAbs_max = new System.Windows.Forms.Label();
            this.label_srel_max = new System.Windows.Forms.Label();
            this.lb_sRel_max = new System.Windows.Forms.Label();
            label_slipabs = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label_slipabs
            // 
            label_slipabs.Location = new System.Drawing.Point(-1, 6);
            label_slipabs.Name = "label_slipabs";
            label_slipabs.Size = new System.Drawing.Size(147, 20);
            label_slipabs.TabIndex = 1;
            label_slipabs.Text = "макс. абс. скольжение";
            // 
            // text_sAbs_max
            // 
            this.text_sAbs_max.Location = new System.Drawing.Point(151, 6);
            this.text_sAbs_max.Name = "text_sAbs_max";
            this.text_sAbs_max.Size = new System.Drawing.Size(74, 20);
            this.text_sAbs_max.TabIndex = 0;
            this.text_sAbs_max.Text = "50";
            this.text_sAbs_max.KeyUp += new System.Windows.Forms.KeyEventHandler(this.text_sabs_max_KeyUp);
            // 
            // text_sRel_max
            // 
            this.text_sRel_max.Location = new System.Drawing.Point(151, 28);
            this.text_sRel_max.Name = "text_sRel_max";
            this.text_sRel_max.Size = new System.Drawing.Size(74, 20);
            this.text_sRel_max.TabIndex = 7;
            this.text_sRel_max.Text = "10";
            this.text_sRel_max.KeyUp += new System.Windows.Forms.KeyEventHandler(this.text_sRel_max_KeyUp);
            // 
            // label_sAbs_max
            // 
            this.label_sAbs_max.AutoSize = true;
            this.label_sAbs_max.Location = new System.Drawing.Point(232, 12);
            this.label_sAbs_max.Name = "label_sAbs_max";
            this.label_sAbs_max.Size = new System.Drawing.Size(44, 13);
            this.label_sAbs_max.TabIndex = 8;
            this.label_sAbs_max.Text = "об/мин";
            // 
            // label_srel_max
            // 
            this.label_srel_max.AutoSize = true;
            this.label_srel_max.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label_srel_max.Location = new System.Drawing.Point(232, 28);
            this.label_srel_max.Name = "label_srel_max";
            this.label_srel_max.Size = new System.Drawing.Size(19, 15);
            this.label_srel_max.TabIndex = 9;
            this.label_srel_max.Text = "%";
            // 
            // lb_sRel_max
            // 
            this.lb_sRel_max.AutoSize = true;
            this.lb_sRel_max.Location = new System.Drawing.Point(-1, 31);
            this.lb_sRel_max.Name = "lb_sRel_max";
            this.lb_sRel_max.Size = new System.Drawing.Size(130, 13);
            this.lb_sRel_max.TabIndex = 10;
            this.lb_sRel_max.Text = "макс.относ.скольжение";
            // 
            // form_max_sAbs
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(309, 96);
            this.Controls.Add(this.lb_sRel_max);
            this.Controls.Add(this.label_srel_max);
            this.Controls.Add(this.label_sAbs_max);
            this.Controls.Add(this.text_sRel_max);
            this.Controls.Add(label_slipabs);
            this.Controls.Add(this.text_sAbs_max);
            this.Name = "form_max_sAbs";
            this.Text = "  максимальные значения регулятора";
            this.Load += new System.EventHandler(this.form_max_sAbs_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.form_max_sAbs_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox text_sAbs_max;
        private System.Windows.Forms.TextBox text_sRel_max;
        private System.Windows.Forms.Label label_sAbs_max;
        private System.Windows.Forms.Label label_srel_max;
        private System.Windows.Forms.Label lb_sRel_max;
    }
}