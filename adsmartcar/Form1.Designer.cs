namespace adsmartcar
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnEStop = new Button();
            btnStop = new Button();
            btnForward = new Button();
            btnLeft = new Button();
            btnRight = new Button();
            btnBackward = new Button();
            lblSensor = new Label();
            panel1 = new Panel();
            btnConnect = new Button();
            lblStatus = new Label();
            btnRecord = new Button();
            lblAnomaly = new Label();
            SuspendLayout();
            // 
            // btnEStop
            // 
            btnEStop.Location = new Point(23, 45);
            btnEStop.Name = "btnEStop";
            btnEStop.Size = new Size(368, 190);
            btnEStop.TabIndex = 0;
            btnEStop.Text = "비상정지";
            btnEStop.UseVisualStyleBackColor = true;
            btnEStop.Click += btnEStop_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(151, 340);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(112, 34);
            btnStop.TabIndex = 1;
            btnStop.Text = "정지";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // btnForward
            // 
            btnForward.Location = new Point(151, 275);
            btnForward.Name = "btnForward";
            btnForward.Size = new Size(112, 34);
            btnForward.TabIndex = 2;
            btnForward.Text = "전진";
            btnForward.UseVisualStyleBackColor = true;
            btnForward.Click += btnForward_Click;
            // 
            // btnLeft
            // 
            btnLeft.Location = new Point(24, 340);
            btnLeft.Name = "btnLeft";
            btnLeft.Size = new Size(112, 34);
            btnLeft.TabIndex = 3;
            btnLeft.Text = "좌";
            btnLeft.UseVisualStyleBackColor = true;
            btnLeft.Click += btnLeft_Click;
            // 
            // btnRight
            // 
            btnRight.Location = new Point(280, 340);
            btnRight.Name = "btnRight";
            btnRight.Size = new Size(112, 34);
            btnRight.TabIndex = 4;
            btnRight.Text = "우";
            btnRight.UseVisualStyleBackColor = true;
            btnRight.Click += btnRight_Click;
            // 
            // btnBackward
            // 
            btnBackward.Location = new Point(151, 408);
            btnBackward.Name = "btnBackward";
            btnBackward.Size = new Size(112, 34);
            btnBackward.TabIndex = 5;
            btnBackward.Text = "후진";
            btnBackward.UseVisualStyleBackColor = true;
            btnBackward.Click += btnBackward_Click;
            // 
            // lblSensor
            // 
            lblSensor.AutoSize = true;
            lblSensor.Location = new Point(417, 45);
            lblSensor.Name = "lblSensor";
            lblSensor.Size = new Size(103, 25);
            lblSensor.TabIndex = 6;
            lblSensor.Text = "거리 : --cm";
            // 
            // panel1
            // 
            panel1.BorderStyle = BorderStyle.FixedSingle;
            panel1.Location = new Point(417, 212);
            panel1.Name = "panel1";
            panel1.Size = new Size(409, 241);
            panel1.TabIndex = 7;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(23, 5);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(112, 34);
            btnConnect.TabIndex = 8;
            btnConnect.Text = "연결";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(141, 9);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(90, 25);
            lblStatus.TabIndex = 9;
            lblStatus.Text = "연결 안됨";
            // 
            // btnRecord
            // 
            btnRecord.Location = new Point(279, 5);
            btnRecord.Name = "btnRecord";
            btnRecord.Size = new Size(112, 34);
            btnRecord.TabIndex = 10;
            btnRecord.Text = "기록 시작";
            btnRecord.UseVisualStyleBackColor = true;
            btnRecord.Click += btnRecord_Click;
            // 
            // lblAnomaly
            // 
            lblAnomaly.AutoSize = true;
            lblAnomaly.Location = new Point(417, 104);
            lblAnomaly.Name = "lblAnomaly";
            lblAnomaly.Size = new Size(78, 25);
            lblAnomaly.TabIndex = 11;
            lblAnomaly.Text = "상태 : --";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(871, 523);
            Controls.Add(lblAnomaly);
            Controls.Add(btnRecord);
            Controls.Add(lblStatus);
            Controls.Add(btnConnect);
            Controls.Add(panel1);
            Controls.Add(lblSensor);
            Controls.Add(btnBackward);
            Controls.Add(btnRight);
            Controls.Add(btnLeft);
            Controls.Add(btnForward);
            Controls.Add(btnStop);
            Controls.Add(btnEStop);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnEStop;
        private Button btnStop;
        private Button btnForward;
        private Button btnLeft;
        private Button btnRight;
        private Button btnBackward;
        private Label lblSensor;
        private Panel panel1;
        private Button btnConnect;
        private Label lblStatus;
        private Button btnRecord;
        private Label lblAnomaly;
    }
}