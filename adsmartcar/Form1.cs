using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO.Ports;
using System.Windows.Forms;

namespace adsmartcar
{
    public partial class Form1 : Form
    {
        private SerialPort port = new SerialPort("COM8", 9600);
        private System.Windows.Forms.Timer heartbeatTimer = new System.Windows.Forms.Timer();

        // ===== 그래프 데이터 (전류만) =====
        private Queue<int> currentData = new Queue<int>();
        private const int MAX_POINTS = 100;

        // 최근 거리값 (라벨 표시용)
        private int lastDistance = 0;

        // ===== 패킷 조립 =====
        private List<byte> packetBuffer = new List<byte>();
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const int PACKET_SIZE = 6;

        // ===== CMD =====
        private const byte CMD_FORWARD = 0x10;
        private const byte CMD_BACKWARD = 0x11;
        private const byte CMD_LEFT = 0x12;
        private const byte CMD_RIGHT = 0x13;
        private const byte CMD_STOP = 0x14;
        private const byte CMD_DISTANCE = 0x20;
        private const byte CMD_CURRENT = 0x21;
        private const byte CMD_HEARTBEAT = 0x30;

        // 전류 환산: diff 1당 약 0.0489A
        private const float CURRENT_PER_DIFF = 0.0489f;

        public Form1()
        {
            InitializeComponent();

            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(panel1, true, null);
            panel1.Paint += Panel1_Paint;

            heartbeatTimer.Interval = 200;
            heartbeatTimer.Tick += HeartbeatTimer_Tick;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        // ---- 연결 / 해제 ----
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (!port.IsOpen)
                {
                    port.DataReceived += Port_DataReceived;
                    port.Open();
                    heartbeatTimer.Start();
                    lblStatus.Text = "연결됨";
                    btnConnect.Text = "연결 끊기";
                }
                else
                {
                    heartbeatTimer.Stop();
                    port.DataReceived -= Port_DataReceived;
                    port.Close();
                    lblStatus.Text = "연결 안됨";
                    btnConnect.Text = "연결";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "연결 실패";
                MessageBox.Show("포트를 열 수 없습니다: " + ex.Message);
            }
        }

        // ---- 하트비트 ----
        private void HeartbeatTimer_Tick(object sender, EventArgs e)
        {
            if (port.IsOpen)
            {
                byte len = 0x00;
                byte cmd = CMD_HEARTBEAT;
                byte chk = (byte)(len ^ cmd);
                byte[] packet = new byte[] { 0x02, len, cmd, chk, 0x03 };
                port.Write(packet, 0, packet.Length);
            }
        }

        // ---- 수신 ----
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int n = port.BytesToRead;
                byte[] buf = new byte[n];
                port.Read(buf, 0, n);
                foreach (byte b in buf)
                    ProcessByte(b);
            }
            catch
            {
            }
        }

        // ===== 패킷 조립기 =====
        private void ProcessByte(byte b)
        {
            if (b == STX)
            {
                packetBuffer.Clear();
                packetBuffer.Add(b);
                return;
            }
            if (packetBuffer.Count == 0) return;

            packetBuffer.Add(b);

            if (packetBuffer.Count == PACKET_SIZE)
            {
                ValidateAndUse(packetBuffer.ToArray());
                packetBuffer.Clear();
            }
        }

        // ===== 패킷 검증 후 처리 =====
        private void ValidateAndUse(byte[] p)
        {
            byte len = p[1];
            byte cmd = p[2];
            byte data = p[3];
            byte chk = p[4];
            byte etx = p[5];

            if (etx != ETX) return;
            if ((byte)(len ^ cmd ^ data) != chk) return;

            if (cmd == CMD_DISTANCE)
            {
                lastDistance = data;
                UpdateLabel();
            }
            else if (cmd == CMD_CURRENT)
            {
                int diff = data;
                lblSensor.Invoke(new Action(() =>
                {
                    currentData.Enqueue(diff);
                    while (currentData.Count > MAX_POINTS)
                        currentData.Dequeue();
                    panel1.Invalidate();
                }));
                UpdateLabel();
            }
        }

        // ===== 라벨에 거리 + 전류 함께 표시 =====
        private void UpdateLabel()
        {
            int diff = currentData.Count > 0 ? currentData.ToArray()[currentData.Count - 1] : 0;
            float amps = diff * CURRENT_PER_DIFF;

            string distText = (lastDistance == 0) ? "-- cm" : lastDistance + " cm";
            string text = "거리 : " + distText + "    전류 : " + amps.ToString("F2") + " A";

            lblSensor.Invoke(new Action(() =>
            {
                lblSensor.Text = text;
            }));
        }

        // ===== 전류 그래프 =====
        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = panel1.Width;
            int h = panel1.Height;

            if (currentData.Count < 2) return;

            int[] arr = currentData.ToArray();
            int maxValue = 80;                     // diff 스케일 (0~80)
            float xStep = (float)w / (MAX_POINTS - 1);

            using (Pen pen = new Pen(Color.OrangeRed, 2))
            {
                for (int i = 0; i < arr.Length - 1; i++)
                {
                    float x1 = i * xStep;
                    float y1 = h - (arr[i] / (float)maxValue * h);
                    float x2 = (i + 1) * xStep;
                    float y2 = h - (arr[i + 1] / (float)maxValue * h);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        // ---- 명령 전송 ----
        private void SendCommand(byte cmd)
        {
            if (!port.IsOpen)
            {
                lblStatus.Text = "먼저 연결하세요";
                return;
            }
            byte len = 0x00;
            byte chk = (byte)(len ^ cmd);
            byte[] packet = new byte[] { 0x02, len, cmd, chk, 0x03 };
            port.Write(packet, 0, packet.Length);
        }

        private void btnForward_Click(object sender, EventArgs e) { SendCommand(CMD_FORWARD); }
        private void btnBackward_Click(object sender, EventArgs e) { SendCommand(CMD_BACKWARD); }
        private void btnLeft_Click(object sender, EventArgs e) { SendCommand(CMD_LEFT); }
        private void btnRight_Click(object sender, EventArgs e) { SendCommand(CMD_RIGHT); }
        private void btnStop_Click(object sender, EventArgs e) { SendCommand(CMD_STOP); }
        private void btnEStop_Click(object sender, EventArgs e) { SendCommand(CMD_STOP); }
    }
}