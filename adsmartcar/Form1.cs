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

        // ===== 그래프용 데이터 =====
        private Queue<int> distanceData = new Queue<int>();
        private const int MAX_POINTS = 100;

        // ===== 패킷 조립용 버퍼 =====
        private List<byte> packetBuffer = new List<byte>();
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const int PACKET_SIZE = 6;

        public Form1()
        {
            InitializeComponent();

            // 그래프 더블버퍼링
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(panel1, true, null);
            panel1.Paint += Panel1_Paint;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        // ---- 연결 / 연결 끊기 ----
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (!port.IsOpen)
                {
                    port.DataReceived += Port_DataReceived;
                    port.Open();
                    lblStatus.Text = "연결됨";
                    btnConnect.Text = "연결 끊기";
                }
                else
                {
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

        // ---- 수신: 생바이트를 읽어서 패킷 조립기로 넘김 (별도 스레드) ----
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

            if (packetBuffer.Count == 0)
                return;

            packetBuffer.Add(b);

            if (packetBuffer.Count == PACKET_SIZE)
            {
                ValidateAndUse(packetBuffer.ToArray());
                packetBuffer.Clear();
            }
        }

        // ===== 패킷 검증 후 사용 =====
        private void ValidateAndUse(byte[] p)
        {
            byte len = p[1];
            byte cmd = p[2];
            byte data = p[3];
            byte chk = p[4];
            byte etx = p[5];

            if (etx != ETX) return;

            byte calc = (byte)(len ^ cmd ^ data);
            if (calc != chk) return;

            if (cmd == 0x20)
            {
                int dist = data;
                lblSensor.Invoke(new Action(() =>
                {
                    if (dist == 0)
                        lblSensor.Text = "거리 : -- cm";
                    else
                    {
                        lblSensor.Text = "거리 : " + dist + " cm";
                        distanceData.Enqueue(dist);
                        while (distanceData.Count > MAX_POINTS)
                            distanceData.Dequeue();
                        panel1.Invalidate();
                    }
                }));
            }
        }

        // ===== 그래프 그리기 =====
        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = panel1.Width;
            int h = panel1.Height;

            if (distanceData.Count < 2) return;

            int[] data = distanceData.ToArray();
            int maxCm = 100;
            float xStep = (float)w / (MAX_POINTS - 1);

            using (Pen pen = new Pen(Color.LimeGreen, 2))
            {
                for (int i = 0; i < data.Length - 1; i++)
                {
                    float x1 = i * xStep;
                    float y1 = h - (data[i] / (float)maxCm * h);
                    float x2 = (i + 1) * xStep;
                    float y2 = h - (data[i + 1] / (float)maxCm * h);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        // ---- 명령 CMD 상수 (패킷 규칙표와 일치) ----
        private const byte CMD_FORWARD = 0x10;
        private const byte CMD_BACKWARD = 0x11;
        private const byte CMD_LEFT = 0x12;
        private const byte CMD_RIGHT = 0x13;
        private const byte CMD_STOP = 0x14;

        // ---- 명령 패킷 전송 ----
        // 형식: [STX=0x02][LEN=0x00][CMD][CHK][ETX=0x03]  (명령은 DATA 없음, 5바이트)
        private void SendCommand(byte cmd)
        {
            if (!port.IsOpen)
            {
                lblStatus.Text = "먼저 연결하세요";
                return;
            }

            byte len = 0x00;              // 명령은 DATA 없음
            byte chk = (byte)(len ^ cmd); // 체크섬 = LEN ^ CMD (DATA 없음)

            byte[] packet = new byte[] { 0x02, len, cmd, chk, 0x03 };
            port.Write(packet, 0, packet.Length);   // 바이트 배열 그대로 전송

           
        }

        private void btnForward_Click(object sender, EventArgs e) { SendCommand(CMD_FORWARD); }
        private void btnBackward_Click(object sender, EventArgs e) { SendCommand(CMD_BACKWARD); }
        private void btnLeft_Click(object sender, EventArgs e) { SendCommand(CMD_LEFT); }
        private void btnRight_Click(object sender, EventArgs e) { SendCommand(CMD_RIGHT); }
        private void btnStop_Click(object sender, EventArgs e) { SendCommand(CMD_STOP); }
        private void btnEStop_Click(object sender, EventArgs e) { SendCommand(CMD_STOP); }
    }
}