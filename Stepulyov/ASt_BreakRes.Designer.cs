using System.ComponentModel;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    partial class AStBreakRes
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
            this.tbBrRes1 = new System.Windows.Forms.TrackBar();
            this.nudBrRes1 = new System.Windows.Forms.NumericUpDown();
            this.nudBrRes2 = new System.Windows.Forms.NumericUpDown();
            this.tbBrRes2 = new System.Windows.Forms.TrackBar();
            this.cbSymmetry = new System.Windows.Forms.CheckBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.tbBrRes1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBrRes1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBrRes2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbBrRes2)).BeginInit();
            this.SuspendLayout();
            // 
            // tbBrRes1
            // 
            this.tbBrRes1.BackColor = System.Drawing.SystemColors.ScrollBar;
            this.tbBrRes1.Location = new System.Drawing.Point(0, 0);
            this.tbBrRes1.Maximum = 100;
            this.tbBrRes1.Name = "tbBrRes1";
            this.tbBrRes1.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbBrRes1.Size = new System.Drawing.Size(42, 414);
            this.tbBrRes1.TabIndex = 0;
            this.tbBrRes1.TickFrequency = 10;
            // 
            // nudBrRes1
            // 
            this.nudBrRes1.Location = new System.Drawing.Point(0, 421);
            this.nudBrRes1.Name = "nudBrRes1";
            this.nudBrRes1.Size = new System.Drawing.Size(45, 20);
            this.nudBrRes1.TabIndex = 1;
            // 
            // nudBrRes2
            // 
            this.nudBrRes2.Location = new System.Drawing.Point(66, 421);
            this.nudBrRes2.Name = "nudBrRes2";
            this.nudBrRes2.Size = new System.Drawing.Size(45, 20);
            this.nudBrRes2.TabIndex = 3;
            // 
            // tbBrRes2
            // 
            this.tbBrRes2.BackColor = System.Drawing.SystemColors.ScrollBar;
            this.tbBrRes2.Location = new System.Drawing.Point(66, 0);
            this.tbBrRes2.Maximum = 100;
            this.tbBrRes2.Name = "tbBrRes2";
            this.tbBrRes2.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbBrRes2.Size = new System.Drawing.Size(42, 414);
            this.tbBrRes2.TabIndex = 2;
            this.tbBrRes2.TickFrequency = 10;
            // 
            // cbSymmetry
            // 
            this.cbSymmetry.AutoSize = true;
            this.cbSymmetry.Location = new System.Drawing.Point(12, 447);
            this.cbSymmetry.Name = "cbSymmetry";
            this.cbSymmetry.Size = new System.Drawing.Size(84, 17);
            this.cbSymmetry.TabIndex = 4;
            this.cbSymmetry.Text = "Симметрия";
            this.cbSymmetry.UseVisualStyleBackColor = true;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(12, 470);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(107, 17);
            this.checkBox1.TabIndex = 5;
            this.checkBox1.Text = "Закрепить окно";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // BrResForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(115, 496);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.cbSymmetry);
            this.Controls.Add(this.nudBrRes2);
            this.Controls.Add(this.tbBrRes2);
            this.Controls.Add(this.nudBrRes1);
            this.Controls.Add(this.tbBrRes1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "BrResForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Резисторы (%)";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.BrResForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.tbBrRes1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBrRes1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBrRes2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbBrRes2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TrackBar tbBrRes1;
        private NumericUpDown nudBrRes1;
        private NumericUpDown nudBrRes2;
        private TrackBar tbBrRes2;
        private CheckBox cbSymmetry;
        private CheckBox checkBox1;
    }
}