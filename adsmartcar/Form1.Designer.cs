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
            SuspendLayout();
            // 
            // btnEStop
            // 
            btnEStop.Location = new Point(23, 30);
            btnEStop.Name = "btnEStop";
            btnEStop.Size = new Size(368, 190);
            btnEStop.TabIndex = 0;
            btnEStop.Text = "비상정지";
            btnEStop.UseVisualStyleBackColor = true;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(150, 329);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(112, 34);
            btnStop.TabIndex = 1;
            btnStop.Text = "정지";
            btnStop.UseVisualStyleBackColor = true;
            // 
            // btnForward
            // 
            btnForward.Location = new Point(150, 264);
            btnForward.Name = "btnForward";
            btnForward.Size = new Size(112, 34);
            btnForward.TabIndex = 2;
            btnForward.Text = "전진";
            btnForward.UseVisualStyleBackColor = true;
            btnForward.Click += button2_Click;
            // 
            // btnLeft
            // 
            btnLeft.Location = new Point(23, 329);
            btnLeft.Name = "btnLeft";
            btnLeft.Size = new Size(112, 34);
            btnLeft.TabIndex = 3;
            btnLeft.Text = "좌";
            btnLeft.UseVisualStyleBackColor = true;
            // 
            // btnRight
            // 
            btnRight.Location = new Point(279, 329);
            btnRight.Name = "btnRight";
            btnRight.Size = new Size(112, 34);
            btnRight.TabIndex = 4;
            btnRight.Text = "우";
            btnRight.UseVisualStyleBackColor = true;
            // 
            // btnBackward
            // 
            btnBackward.Location = new Point(150, 397);
            btnBackward.Name = "btnBackward";
            btnBackward.Size = new Size(112, 34);
            btnBackward.TabIndex = 5;
            btnBackward.Text = "후진";
            btnBackward.UseVisualStyleBackColor = true;
            btnBackward.Click += button5_Click;
            // 
            // lblSensor
            // 
            lblSensor.AutoSize = true;
            lblSensor.Location = new Point(417, 30);
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
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(838, 489);
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
    }
}
