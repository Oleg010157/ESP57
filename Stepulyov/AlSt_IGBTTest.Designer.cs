using System.ComponentModel;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    partial class AlStIgbtTest
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
            this.OnOff_IGBT1 = new System.Windows.Forms.Button();
            this.OnOff_IGBT2 = new System.Windows.Forms.Button();
            this.gbAIN1 = new System.Windows.Forms.GroupBox();
            this.cbVT7_1 = new System.Windows.Forms.CheckBox();
            this.cbVT6_1 = new System.Windows.Forms.CheckBox();
            this.cbVT5_1 = new System.Windows.Forms.CheckBox();
            this.cbVT4_1 = new System.Windows.Forms.CheckBox();
            this.cbVT3_1 = new System.Windows.Forms.CheckBox();
            this.cbVT2_1 = new System.Windows.Forms.CheckBox();
            this.cbVT1_1 = new System.Windows.Forms.CheckBox();
            this.gbAIN2 = new System.Windows.Forms.GroupBox();
            this.cbVT7_2 = new System.Windows.Forms.CheckBox();
            this.cbVT6_2 = new System.Windows.Forms.CheckBox();
            this.cbVT5_2 = new System.Windows.Forms.CheckBox();
            this.cbVT4_2 = new System.Windows.Forms.CheckBox();
            this.cbVT3_2 = new System.Windows.Forms.CheckBox();
            this.cbVT2_2 = new System.Windows.Forms.CheckBox();
            this.cbVT1_2 = new System.Windows.Forms.CheckBox();
            this.gbAIN1.SuspendLayout();
            this.gbAIN2.SuspendLayout();
            this.SuspendLayout();
            // 
            // OnOff_IGBT1
            // 
            this.OnOff_IGBT1.Location = new System.Drawing.Point(12, 12);
            this.OnOff_IGBT1.Name = "OnOff_IGBT1";
            this.OnOff_IGBT1.Size = new System.Drawing.Size(82, 23);
            this.OnOff_IGBT1.TabIndex = 0;
            this.OnOff_IGBT1.Text = "Вкл";
            this.OnOff_IGBT1.UseVisualStyleBackColor = true;
            this.OnOff_IGBT1.Click += new System.EventHandler(this.OnOff_IGBT1_Click);
            // 
            // OnOff_IGBT2
            // 
            this.OnOff_IGBT2.Location = new System.Drawing.Point(100, 12);
            this.OnOff_IGBT2.Name = "OnOff_IGBT2";
            this.OnOff_IGBT2.Size = new System.Drawing.Size(82, 23);
            this.OnOff_IGBT2.TabIndex = 1;
            this.OnOff_IGBT2.Text = "Вкл";
            this.OnOff_IGBT2.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.OnOff_IGBT2.UseVisualStyleBackColor = true;
            this.OnOff_IGBT2.Click += new System.EventHandler(this.OnOff_IGBT2_Click);
            // 
            // gbAIN1
            // 
            this.gbAIN1.Controls.Add(this.cbVT7_1);
            this.gbAIN1.Controls.Add(this.cbVT6_1);
            this.gbAIN1.Controls.Add(this.cbVT5_1);
            this.gbAIN1.Controls.Add(this.cbVT4_1);
            this.gbAIN1.Controls.Add(this.cbVT3_1);
            this.gbAIN1.Controls.Add(this.cbVT2_1);
            this.gbAIN1.Controls.Add(this.cbVT1_1);
            this.gbAIN1.Location = new System.Drawing.Point(12, 54);
            this.gbAIN1.Name = "gbAIN1";
            this.gbAIN1.Size = new System.Drawing.Size(82, 190);
            this.gbAIN1.TabIndex = 2;
            this.gbAIN1.TabStop = false;
            this.gbAIN1.Text = "АИН 1";
            // 
            // cbVT7_1
            // 
            this.cbVT7_1.AutoSize = true;
            this.cbVT7_1.Location = new System.Drawing.Point(6, 130);
            this.cbVT7_1.Name = "cbVT7_1";
            this.cbVT7_1.Size = new System.Drawing.Size(46, 17);
            this.cbVT7_1.TabIndex = 9;
            this.cbVT7_1.Text = "VT7";
            this.cbVT7_1.UseVisualStyleBackColor = true;
            this.cbVT7_1.CheckedChanged += new System.EventHandler(this.cbVT7_1_CheckedChanged);
            // 
            // cbVT6_1
            // 
            this.cbVT6_1.AutoSize = true;
            this.cbVT6_1.Location = new System.Drawing.Point(6, 107);
            this.cbVT6_1.Name = "cbVT6_1";
            this.cbVT6_1.Size = new System.Drawing.Size(46, 17);
            this.cbVT6_1.TabIndex = 8;
            this.cbVT6_1.Text = "VT6";
            this.cbVT6_1.UseVisualStyleBackColor = true;
            this.cbVT6_1.CheckedChanged += new System.EventHandler(this.cbVT6_1_CheckedChanged);
            // 
            // cbVT5_1
            // 
            this.cbVT5_1.AutoSize = true;
            this.cbVT5_1.Location = new System.Drawing.Point(6, 93);
            this.cbVT5_1.Name = "cbVT5_1";
            this.cbVT5_1.Size = new System.Drawing.Size(46, 17);
            this.cbVT5_1.TabIndex = 7;
            this.cbVT5_1.Text = "VT5";
            this.cbVT5_1.UseVisualStyleBackColor = true;
            this.cbVT5_1.CheckedChanged += new System.EventHandler(this.cbVT5_1_CheckedChanged);
            // 
            // cbVT4_1
            // 
            this.cbVT4_1.AutoSize = true;
            this.cbVT4_1.Location = new System.Drawing.Point(6, 70);
            this.cbVT4_1.Name = "cbVT4_1";
            this.cbVT4_1.Size = new System.Drawing.Size(46, 17);
            this.cbVT4_1.TabIndex = 6;
            this.cbVT4_1.Text = "VT4";
            this.cbVT4_1.UseVisualStyleBackColor = true;
            this.cbVT4_1.CheckedChanged += new System.EventHandler(this.cbVT4_1_CheckedChanged);
            // 
            // cbVT3_1
            // 
            this.cbVT3_1.AutoSize = true;
            this.cbVT3_1.Location = new System.Drawing.Point(6, 56);
            this.cbVT3_1.Name = "cbVT3_1";
            this.cbVT3_1.Size = new System.Drawing.Size(46, 17);
            this.cbVT3_1.TabIndex = 5;
            this.cbVT3_1.Text = "VT3";
            this.cbVT3_1.UseVisualStyleBackColor = true;
            this.cbVT3_1.CheckedChanged += new System.EventHandler(this.cbVT3_1_CheckedChanged);
            // 
            // cbVT2_1
            // 
            this.cbVT2_1.AutoSize = true;
            this.cbVT2_1.Location = new System.Drawing.Point(6, 33);
            this.cbVT2_1.Name = "cbVT2_1";
            this.cbVT2_1.Size = new System.Drawing.Size(46, 17);
            this.cbVT2_1.TabIndex = 4;
            this.cbVT2_1.Text = "VT2";
            this.cbVT2_1.UseVisualStyleBackColor = true;
            this.cbVT2_1.CheckedChanged += new System.EventHandler(this.cbVT2_1_CheckedChanged);
            // 
            // cbVT1_1
            // 
            this.cbVT1_1.AutoSize = true;
            this.cbVT1_1.Location = new System.Drawing.Point(6, 19);
            this.cbVT1_1.Name = "cbVT1_1";
            this.cbVT1_1.Size = new System.Drawing.Size(46, 17);
            this.cbVT1_1.TabIndex = 3;
            this.cbVT1_1.Text = "VT1";
            this.cbVT1_1.UseVisualStyleBackColor = true;
            this.cbVT1_1.CheckedChanged += new System.EventHandler(this.cbVT1_1_CheckedChanged);
            // 
            // gbAIN2
            // 
            this.gbAIN2.Controls.Add(this.cbVT7_2);
            this.gbAIN2.Controls.Add(this.cbVT6_2);
            this.gbAIN2.Controls.Add(this.cbVT5_2);
            this.gbAIN2.Controls.Add(this.cbVT4_2);
            this.gbAIN2.Controls.Add(this.cbVT3_2);
            this.gbAIN2.Controls.Add(this.cbVT2_2);
            this.gbAIN2.Controls.Add(this.cbVT1_2);
            this.gbAIN2.Location = new System.Drawing.Point(100, 54);
            this.gbAIN2.Name = "gbAIN2";
            this.gbAIN2.Size = new System.Drawing.Size(82, 190);
            this.gbAIN2.TabIndex = 3;
            this.gbAIN2.TabStop = false;
            this.gbAIN2.Text = "Инвертор";
            // 
            // cbVT7_2
            // 
            this.cbVT7_2.AutoSize = true;
            this.cbVT7_2.Location = new System.Drawing.Point(6, 130);
            this.cbVT7_2.Name = "cbVT7_2";
            this.cbVT7_2.Size = new System.Drawing.Size(46, 17);
            this.cbVT7_2.TabIndex = 9;
            this.cbVT7_2.Text = "VT7";
            this.cbVT7_2.UseVisualStyleBackColor = true;
            // 
            // cbVT6_2
            // 
            this.cbVT6_2.AutoSize = true;
            this.cbVT6_2.Location = new System.Drawing.Point(6, 107);
            this.cbVT6_2.Name = "cbVT6_2";
            this.cbVT6_2.Size = new System.Drawing.Size(46, 17);
            this.cbVT6_2.TabIndex = 8;
            this.cbVT6_2.Text = "VT6";
            this.cbVT6_2.UseVisualStyleBackColor = true;
            this.cbVT6_2.CheckedChanged += new System.EventHandler(this.cbVT6_2_CheckedChanged);
            // 
            // cbVT5_2
            // 
            this.cbVT5_2.AutoSize = true;
            this.cbVT5_2.Location = new System.Drawing.Point(6, 93);
            this.cbVT5_2.Name = "cbVT5_2";
            this.cbVT5_2.Size = new System.Drawing.Size(46, 17);
            this.cbVT5_2.TabIndex = 7;
            this.cbVT5_2.Text = "VT5";
            this.cbVT5_2.UseVisualStyleBackColor = true;
            this.cbVT5_2.CheckedChanged += new System.EventHandler(this.cbVT5_2_CheckedChanged);
            // 
            // cbVT4_2
            // 
            this.cbVT4_2.AutoSize = true;
            this.cbVT4_2.Location = new System.Drawing.Point(6, 70);
            this.cbVT4_2.Name = "cbVT4_2";
            this.cbVT4_2.Size = new System.Drawing.Size(46, 17);
            this.cbVT4_2.TabIndex = 6;
            this.cbVT4_2.Text = "VT4";
            this.cbVT4_2.UseVisualStyleBackColor = true;
            this.cbVT4_2.CheckedChanged += new System.EventHandler(this.cbVT4_2_CheckedChanged);
            // 
            // cbVT3_2
            // 
            this.cbVT3_2.AutoSize = true;
            this.cbVT3_2.Location = new System.Drawing.Point(6, 56);
            this.cbVT3_2.Name = "cbVT3_2";
            this.cbVT3_2.Size = new System.Drawing.Size(46, 17);
            this.cbVT3_2.TabIndex = 5;
            this.cbVT3_2.Text = "VT3";
            this.cbVT3_2.UseVisualStyleBackColor = true;
            this.cbVT3_2.CheckedChanged += new System.EventHandler(this.cbVT3_2_CheckedChanged);
            // 
            // cbVT2_2
            // 
            this.cbVT2_2.AutoSize = true;
            this.cbVT2_2.Location = new System.Drawing.Point(6, 33);
            this.cbVT2_2.Name = "cbVT2_2";
            this.cbVT2_2.Size = new System.Drawing.Size(46, 17);
            this.cbVT2_2.TabIndex = 4;
            this.cbVT2_2.Text = "VT2";
            this.cbVT2_2.UseVisualStyleBackColor = true;
            this.cbVT2_2.CheckedChanged += new System.EventHandler(this.cbVT2_2_CheckedChanged);
            // 
            // cbVT1_2
            // 
            this.cbVT1_2.AutoSize = true;
            this.cbVT1_2.Location = new System.Drawing.Point(6, 19);
            this.cbVT1_2.Name = "cbVT1_2";
            this.cbVT1_2.Size = new System.Drawing.Size(46, 17);
            this.cbVT1_2.TabIndex = 3;
            this.cbVT1_2.Text = "VT1";
            this.cbVT1_2.UseVisualStyleBackColor = true;
            this.cbVT1_2.CheckedChanged += new System.EventHandler(this.cbVT1_2_CheckedChanged);
            // 
            // AlStIgbtTest
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(195, 256);
            this.Controls.Add(this.gbAIN2);
            this.Controls.Add(this.gbAIN1);
            this.Controls.Add(this.OnOff_IGBT2);
            this.Controls.Add(this.OnOff_IGBT1);
            this.Name = "AlStIgbtTest";
            this.Text = "IGBT";
            this.gbAIN1.ResumeLayout(false);
            this.gbAIN1.PerformLayout();
            this.gbAIN2.ResumeLayout(false);
            this.gbAIN2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        public Button OnOff_IGBT1;
        public Button OnOff_IGBT2;
        public GroupBox gbAIN1;
        public CheckBox cbVT7_1;
        public CheckBox cbVT6_1;
        public CheckBox cbVT5_1;
        public CheckBox cbVT4_1;
        public CheckBox cbVT3_1;
        public CheckBox cbVT2_1;
        public CheckBox cbVT1_1;
        public GroupBox gbAIN2;
        public CheckBox cbVT7_2;
        public CheckBox cbVT6_2;
        public CheckBox cbVT5_2;
        public CheckBox cbVT4_2;
        public CheckBox cbVT3_2;
        public CheckBox cbVT2_2;
        public CheckBox cbVT1_2;
    }
}