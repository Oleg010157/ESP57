using System.ComponentModel;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    partial class TemperatureView
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lbMot1_1 = new System.Windows.Forms.Label();
            this.lbADC = new System.Windows.Forms.Label();
            this.lbVT1_1 = new System.Windows.Forms.Label();
            this.lbVT2_1 = new System.Windows.Forms.Label();
            this.lbVT3_1 = new System.Windows.Forms.Label();
            this.lbVT4_1 = new System.Windows.Forms.Label();
            this.lbVT5_1 = new System.Windows.Forms.Label();
            this.lbIut1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lbVT5_2 = new System.Windows.Forms.Label();
            this.lbVT4_2 = new System.Windows.Forms.Label();
            this.lbVT3_2 = new System.Windows.Forms.Label();
            this.lbVT2_2 = new System.Windows.Forms.Label();
            this.lbVT1_2 = new System.Windows.Forms.Label();
            this.lbMot2_1 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.SystemColors.GradientInactiveCaption;
            this.groupBox1.Controls.Add(this.lbMot1_1);
            this.groupBox1.Controls.Add(this.lbADC);
            this.groupBox1.Controls.Add(this.lbVT1_1);
            this.groupBox1.Controls.Add(this.lbVT2_1);
            this.groupBox1.Controls.Add(this.lbVT3_1);
            this.groupBox1.Controls.Add(this.lbVT4_1);
            this.groupBox1.Controls.Add(this.lbVT5_1);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.groupBox1.Location = new System.Drawing.Point(0, -1);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(182, 224);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "АИН1";
            // 
            // lbMot1_1
            // 
            this.lbMot1_1.AutoSize = true;
            this.lbMot1_1.Location = new System.Drawing.Point(12, 25);
            this.lbMot1_1.Name = "lbMot1_1";
            this.lbMot1_1.Size = new System.Drawing.Size(165, 24);
            this.lbMot1_1.TabIndex = 0;
            this.lbMot1_1.Text = "M1 обм: 100.00 °C";
            // 
            // lbADC
            // 
            this.lbADC.AutoSize = true;
            this.lbADC.Location = new System.Drawing.Point(12, 191);
            this.lbADC.Name = "lbADC";
            this.lbADC.Size = new System.Drawing.Size(154, 24);
            this.lbADC.TabIndex = 15;
            this.lbADC.Text = "T АЦП: 100.00 °C";
            // 
            // lbVT1_1
            // 
            this.lbVT1_1.AutoSize = true;
            this.lbVT1_1.Location = new System.Drawing.Point(12, 61);
            this.lbVT1_1.Name = "lbVT1_1";
            this.lbVT1_1.Size = new System.Drawing.Size(134, 24);
            this.lbVT1_1.TabIndex = 0;
            this.lbVT1_1.Text = "VT1: 100.00 °C";
            // 
            // lbVT2_1
            // 
            this.lbVT2_1.AutoSize = true;
            this.lbVT2_1.Location = new System.Drawing.Point(12, 85);
            this.lbVT2_1.Name = "lbVT2_1";
            this.lbVT2_1.Size = new System.Drawing.Size(134, 24);
            this.lbVT2_1.TabIndex = 0;
            this.lbVT2_1.Text = "VT2: 100.00 °C";
            // 
            // lbVT3_1
            // 
            this.lbVT3_1.AutoSize = true;
            this.lbVT3_1.Location = new System.Drawing.Point(12, 109);
            this.lbVT3_1.Name = "lbVT3_1";
            this.lbVT3_1.Size = new System.Drawing.Size(134, 24);
            this.lbVT3_1.TabIndex = 0;
            this.lbVT3_1.Text = "VT3: 100.00 °C";
            // 
            // lbVT4_1
            // 
            this.lbVT4_1.AutoSize = true;
            this.lbVT4_1.Location = new System.Drawing.Point(12, 133);
            this.lbVT4_1.Name = "lbVT4_1";
            this.lbVT4_1.Size = new System.Drawing.Size(134, 24);
            this.lbVT4_1.TabIndex = 0;
            this.lbVT4_1.Text = "VT4: 100.00 °C";
            // 
            // lbVT5_1
            // 
            this.lbVT5_1.AutoSize = true;
            this.lbVT5_1.Location = new System.Drawing.Point(12, 157);
            this.lbVT5_1.Name = "lbVT5_1";
            this.lbVT5_1.Size = new System.Drawing.Size(134, 24);
            this.lbVT5_1.TabIndex = 0;
            this.lbVT5_1.Text = "VT5: 100.00 °C";
            // 
            // lbIut1
            // 
            this.lbIut1.AutoSize = true;
            this.lbIut1.Location = new System.Drawing.Point(12, 191);
            this.lbIut1.Name = "lbIut1";
            this.lbIut1.Size = new System.Drawing.Size(112, 24);
            this.lbIut1.TabIndex = 0;
            this.lbIut1.Text = "Iут: 0.01 mA";
            // 
            // groupBox2
            // 
            this.groupBox2.BackColor = System.Drawing.Color.SkyBlue;
            this.groupBox2.Controls.Add(this.lbIut1);
            this.groupBox2.Controls.Add(this.lbVT5_2);
            this.groupBox2.Controls.Add(this.lbVT4_2);
            this.groupBox2.Controls.Add(this.lbVT3_2);
            this.groupBox2.Controls.Add(this.lbVT2_2);
            this.groupBox2.Controls.Add(this.lbVT1_2);
            this.groupBox2.Controls.Add(this.lbMot2_1);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.groupBox2.Location = new System.Drawing.Point(183, -1);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(182, 224);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "АИН2";
            // 
            // lbVT5_2
            // 
            this.lbVT5_2.AutoSize = true;
            this.lbVT5_2.Location = new System.Drawing.Point(12, 157);
            this.lbVT5_2.Name = "lbVT5_2";
            this.lbVT5_2.Size = new System.Drawing.Size(134, 24);
            this.lbVT5_2.TabIndex = 0;
            this.lbVT5_2.Text = "VT5: 100.00 °C";
            // 
            // lbVT4_2
            // 
            this.lbVT4_2.AutoSize = true;
            this.lbVT4_2.Location = new System.Drawing.Point(12, 133);
            this.lbVT4_2.Name = "lbVT4_2";
            this.lbVT4_2.Size = new System.Drawing.Size(134, 24);
            this.lbVT4_2.TabIndex = 0;
            this.lbVT4_2.Text = "VT4: 100.00 °C";
            // 
            // lbVT3_2
            // 
            this.lbVT3_2.AutoSize = true;
            this.lbVT3_2.Location = new System.Drawing.Point(12, 109);
            this.lbVT3_2.Name = "lbVT3_2";
            this.lbVT3_2.Size = new System.Drawing.Size(134, 24);
            this.lbVT3_2.TabIndex = 0;
            this.lbVT3_2.Text = "VT3: 100.00 °C";
            // 
            // lbVT2_2
            // 
            this.lbVT2_2.AutoSize = true;
            this.lbVT2_2.Location = new System.Drawing.Point(12, 85);
            this.lbVT2_2.Name = "lbVT2_2";
            this.lbVT2_2.Size = new System.Drawing.Size(134, 24);
            this.lbVT2_2.TabIndex = 0;
            this.lbVT2_2.Text = "VT2: 100.00 °C";
            // 
            // lbVT1_2
            // 
            this.lbVT1_2.AutoSize = true;
            this.lbVT1_2.Location = new System.Drawing.Point(12, 61);
            this.lbVT1_2.Name = "lbVT1_2";
            this.lbVT1_2.Size = new System.Drawing.Size(134, 24);
            this.lbVT1_2.TabIndex = 0;
            this.lbVT1_2.Text = "VT1: 100.00 °C";
            // 
            // lbMot2_1
            // 
            this.lbMot2_1.AutoSize = true;
            this.lbMot2_1.Location = new System.Drawing.Point(12, 25);
            this.lbMot2_1.Name = "lbMot2_1";
            this.lbMot2_1.Size = new System.Drawing.Size(165, 24);
            this.lbMot2_1.TabIndex = 0;
            this.lbMot2_1.Text = "M1 обм: 100.00 °C";
            // 
            // TemperatureView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(363, 219);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.MaximumSize = new System.Drawing.Size(379, 258);
            this.MinimumSize = new System.Drawing.Size(379, 258);
            this.Name = "TemperatureView";
            this.Text = "Датчики тока утечки и температуры";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private GroupBox groupBox1;
        private Label lbVT1_1;
        private Label lbVT5_1;
        private Label lbVT4_1;
        private Label lbVT3_1;
        private Label lbVT2_1;
        private GroupBox groupBox2;
        private Label lbVT5_2;
        private Label lbVT4_2;
        private Label lbVT3_2;
        private Label lbVT2_2;
        private Label lbVT1_2;
        private Label lbMot1_1;
        private Label lbIut1;
        private Label lbMot2_1;
        private Label lbADC;
    }
}