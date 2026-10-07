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
            this.components = new System.ComponentModel.Container();
            this.btnTest = new System.Windows.Forms.Button();
            this.gbBridge = new System.Windows.Forms.GroupBox();
            this.lblLegB = new System.Windows.Forms.Label();
            this.lblLegA = new System.Windows.Forms.Label();
            this.cbVT4 = new System.Windows.Forms.CheckBox();
            this.cbVT3 = new System.Windows.Forms.CheckBox();
            this.cbVT2 = new System.Windows.Forms.CheckBox();
            this.cbVT1 = new System.Windows.Forms.CheckBox();
            this.btnAllOff = new System.Windows.Forms.Button();
            this.lblUdcName = new System.Windows.Forms.Label();
            this.lblUdc = new System.Windows.Forms.Label();
            this.lblIpriName = new System.Windows.Forms.Label();
            this.lblIpri = new System.Windows.Forms.Label();
            this.lblState = new System.Windows.Forms.Label();
            this.lblIlimName = new System.Windows.Forms.Label();
            this.nudIlim = new System.Windows.Forms.NumericUpDown();
            this.lblIlimFact = new System.Windows.Forms.Label();
            this.timerView = new System.Windows.Forms.Timer(this.components);
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.gbBridge.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudIlim)).BeginInit();
            this.SuspendLayout();
            //
            // btnTest
            //
            this.btnTest.Location = new System.Drawing.Point(12, 12);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(90, 25);
            this.btnTest.TabIndex = 0;
            this.btnTest.Text = "Вкл";
            this.btnTest.UseVisualStyleBackColor = true;
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            //
            // btnAllOff
            //
            this.btnAllOff.Location = new System.Drawing.Point(108, 12);
            this.btnAllOff.Name = "btnAllOff";
            this.btnAllOff.Size = new System.Drawing.Size(100, 25);
            this.btnAllOff.TabIndex = 1;
            this.btnAllOff.Text = "Закрыть все";
            this.btnAllOff.UseVisualStyleBackColor = true;
            this.btnAllOff.Click += new System.EventHandler(this.btnAllOff_Click);
            //
            // gbBridge
            //
            this.gbBridge.Controls.Add(this.lblLegB);
            this.gbBridge.Controls.Add(this.lblLegA);
            this.gbBridge.Controls.Add(this.cbVT4);
            this.gbBridge.Controls.Add(this.cbVT3);
            this.gbBridge.Controls.Add(this.cbVT2);
            this.gbBridge.Controls.Add(this.cbVT1);
            this.gbBridge.Location = new System.Drawing.Point(12, 45);
            this.gbBridge.Name = "gbBridge";
            this.gbBridge.Size = new System.Drawing.Size(196, 100);
            this.gbBridge.TabIndex = 2;
            this.gbBridge.TabStop = false;
            this.gbBridge.Text = "Ключи моста";
            //
            // lblLegA
            //
            this.lblLegA.AutoSize = true;
            this.lblLegA.Location = new System.Drawing.Point(14, 20);
            this.lblLegA.Name = "lblLegA";
            this.lblLegA.Text = "Плечо A";
            //
            // lblLegB
            //
            this.lblLegB.AutoSize = true;
            this.lblLegB.Location = new System.Drawing.Point(108, 20);
            this.lblLegB.Name = "lblLegB";
            this.lblLegB.Text = "Плечо B";
            //
            // cbVT1
            //
            this.cbVT1.AutoCheck = false;
            this.cbVT1.AutoSize = true;
            this.cbVT1.Location = new System.Drawing.Point(17, 42);
            this.cbVT1.Name = "cbVT1";
            this.cbVT1.TabIndex = 0;
            this.cbVT1.Text = "VT1 верх";
            this.cbVT1.Click += new System.EventHandler(this.cbKey_Click);
            //
            // cbVT2
            //
            this.cbVT2.AutoCheck = false;
            this.cbVT2.AutoSize = true;
            this.cbVT2.Location = new System.Drawing.Point(17, 68);
            this.cbVT2.Name = "cbVT2";
            this.cbVT2.TabIndex = 1;
            this.cbVT2.Text = "VT2 низ";
            this.cbVT2.Click += new System.EventHandler(this.cbKey_Click);
            //
            // cbVT3
            //
            this.cbVT3.AutoCheck = false;
            this.cbVT3.AutoSize = true;
            this.cbVT3.Location = new System.Drawing.Point(111, 42);
            this.cbVT3.Name = "cbVT3";
            this.cbVT3.TabIndex = 2;
            this.cbVT3.Text = "VT3 верх";
            this.cbVT3.Click += new System.EventHandler(this.cbKey_Click);
            //
            // cbVT4
            //
            this.cbVT4.AutoCheck = false;
            this.cbVT4.AutoSize = true;
            this.cbVT4.Location = new System.Drawing.Point(111, 68);
            this.cbVT4.Name = "cbVT4";
            this.cbVT4.TabIndex = 3;
            this.cbVT4.Text = "VT4 низ";
            this.cbVT4.Click += new System.EventHandler(this.cbKey_Click);
            //
            // lblUdcName
            //
            this.lblUdcName.AutoSize = true;
            this.lblUdcName.Location = new System.Drawing.Point(14, 155);
            this.lblUdcName.Name = "lblUdcName";
            this.lblUdcName.Text = "Udc, В";
            //
            // lblUdc
            //
            this.lblUdc.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold);
            this.lblUdc.Location = new System.Drawing.Point(100, 152);
            this.lblUdc.Name = "lblUdc";
            this.lblUdc.Size = new System.Drawing.Size(100, 18);
            this.lblUdc.Text = "-";
            this.lblUdc.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // lblIpriName
            //
            this.lblIpriName.AutoSize = true;
            this.lblIpriName.Location = new System.Drawing.Point(14, 179);
            this.lblIpriName.Name = "lblIpriName";
            this.lblIpriName.Text = "Iперв, А";
            //
            // lblIpri
            //
            this.lblIpri.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold);
            this.lblIpri.Location = new System.Drawing.Point(100, 176);
            this.lblIpri.Name = "lblIpri";
            this.lblIpri.Size = new System.Drawing.Size(100, 18);
            this.lblIpri.Text = "-";
            this.lblIpri.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // lblIlimName
            //
            this.lblIlimName.AutoSize = true;
            this.lblIlimName.Location = new System.Drawing.Point(14, 205);
            this.lblIlimName.Name = "lblIlimName";
            this.lblIlimName.Text = "Порог Iперв, А";
            //
            // nudIlim
            //
            this.nudIlim.DecimalPlaces = 1;
            this.nudIlim.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            this.nudIlim.Location = new System.Drawing.Point(120, 202);
            this.nudIlim.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            this.nudIlim.Name = "nudIlim";
            this.nudIlim.Size = new System.Drawing.Size(80, 20);
            this.nudIlim.TabIndex = 3;
            this.nudIlim.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.nudIlim.ValueChanged += new System.EventHandler(this.nudIlim_ValueChanged);
            //
            // lblIlimFact
            //
            this.lblIlimFact.Location = new System.Drawing.Point(14, 228);
            this.lblIlimFact.Name = "lblIlimFact";
            this.lblIlimFact.Size = new System.Drawing.Size(186, 16);
            this.lblIlimFact.Text = "факт: -";
            this.lblIlimFact.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // lblState
            //
            this.lblState.Location = new System.Drawing.Point(12, 252);
            this.lblState.Name = "lblState";
            this.lblState.Size = new System.Drawing.Size(196, 58);
            this.lblState.Text = "Тест выключен";
            //
            // timerView
            //
            this.timerView.Interval = 500;
            this.timerView.Tick += new System.EventHandler(this.timerView_Tick);
            //
            // AlStIgbtTest
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(220, 316);
            this.Controls.Add(this.lblState);
            this.Controls.Add(this.lblIlimFact);
            this.Controls.Add(this.nudIlim);
            this.Controls.Add(this.lblIlimName);
            this.Controls.Add(this.lblIpri);
            this.Controls.Add(this.lblIpriName);
            this.Controls.Add(this.lblUdc);
            this.Controls.Add(this.lblUdcName);
            this.Controls.Add(this.gbBridge);
            this.Controls.Add(this.btnAllOff);
            this.Controls.Add(this.btnTest);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "AlStIgbtTest";
            this.Text = "Тест IGBT";
            this.gbBridge.ResumeLayout(false);
            this.gbBridge.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudIlim)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Button btnTest;
        private Button btnAllOff;
        private GroupBox gbBridge;
        private Label lblLegA;
        private Label lblLegB;
        private CheckBox cbVT1;
        private CheckBox cbVT2;
        private CheckBox cbVT3;
        private CheckBox cbVT4;
        private Label lblUdcName;
        private Label lblUdc;
        private Label lblIpriName;
        private Label lblIpri;
        private Label lblState;
        private Label lblIlimName;
        private NumericUpDown nudIlim;
        private Label lblIlimFact;
        private Timer timerView;
        private ToolTip toolTip1;
    }
}
