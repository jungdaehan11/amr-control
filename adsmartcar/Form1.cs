using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace adsmartcar
{
    public partial class Form1 : Form
    {
        // ===== socket 통신 =====
        private TcpClient tcpClient;
        private NetworkStream stream;
        private Thread receiveThread;
        private bool connected = false;
        private const string BRIDGE_IP = "127.0.0.1";   // 브릿지 IP (같은 PC면 127.0.0.1)
        private const int BRIDGE_PORT = 5000;

        // ===== 그래프 데이터 =====
        private Queue<int> currentData = new Queue<int>();
        private const int MAX_POINTS = 100;
        private int lastDistance = 0;

        // ===== 이상 감지 =====
        private Queue<int> anomalyWindow = new Queue<int>();
        private const int WINDOW_SIZE = 20;
        private const float ANOMALY_THRESHOLD = 9.5f;
        private bool isDriving = false;

        // ===== 이물질 지속 감지 → 서보 경고 =====
        private int debrisCount = 0;
        private const int DEBRIS_TRIGGER = 20;
        private bool warningActive = false;

        // ===== 데이터 로깅 =====
        private StreamWriter logWriter = null;
        private bool isRecording = false;
        private DateTime recordStartTime;
        private string lastCommand = "STOP";
        private int logCount = 0;

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

        private const float CURRENT_PER_DIFF = 0.0489f;

        public Form1()
        {
            InitializeComponent();

            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(panel1, true, null);
            panel1.Paint += Panel1_Paint;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        // ---- 연결 / 해제 (socket) ----
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (!connected)
            {
                try
                {
                    tcpClient = new TcpClient();
                    tcpClient.Connect(BRIDGE_IP, BRIDGE_PORT);
                    stream = tcpClient.GetStream();
                    connected = true;

                    // 수신 스레드 시작
                    receiveThread = new Thread(ReceiveLoop);
                    receiveThread.IsBackground = true;
                    receiveThread.Start();

                    lblStatus.Text = "연결됨";
                    btnConnect.Text = "연결 끊기";
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "연결 실패";
                    MessageBox.Show("브릿지에 연결할 수 없습니다: " + ex.Message);
                }
            }
            else
            {
                connected = false;
                try
                {
                    if (stream != null) stream.Close();
                    if (tcpClient != null) tcpClient.Close();
                }
                catch { }
                lblStatus.Text = "연결 안됨";
                btnConnect.Text = "연결";
            }
        }

        // ---- 수신 스레드 ----
        private void ReceiveLoop()
        {
            byte[] buffer = new byte[1024];
            while (connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    for (int i = 0; i < bytesRead; i++)
                        ProcessByte(buffer[i]);
                }
                catch
                {
                    break;
                }
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

                WriteLog(diff);
                CheckAnomaly(diff);

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

        // ===== 이상 감지 + 유형 진단 + 서보 경고 =====
        private void CheckAnomaly(int diff)
        {
            anomalyWindow.Enqueue(diff);
            while (anomalyWindow.Count > WINDOW_SIZE)
                anomalyWindow.Dequeue();

            isDriving = (lastCommand == "FORWARD" || lastCommand == "BACKWARD");

            if (!isDriving)
            {
                if (warningActive) SendWarnOff();
                debrisCount = 0;
                lblAnomaly.Invoke(new Action(() =>
                {
                    lblAnomaly.Text = "상태 : 정지/회전";
                    lblAnomaly.ForeColor = Color.Gray;
                }));
                return;
            }

            if (anomalyWindow.Count < WINDOW_SIZE)
            {
                lblAnomaly.Invoke(new Action(() =>
                {
                    lblAnomaly.Text = "상태 : 측정 중...";
                    lblAnomaly.ForeColor = Color.Gray;
                }));
                return;
            }

            double avg = anomalyWindow.Average();
            double variance = anomalyWindow.Select(x => (x - avg) * (x - avg)).Average();
            double std = Math.Sqrt(variance);

            lblAnomaly.Invoke(new Action(() =>
            {
                if (avg <= ANOMALY_THRESHOLD)
                {
                    lblAnomaly.Text = $"상태 : 정상 (평균 {avg:F1}, 변동 {std:F1})";
                    lblAnomaly.ForeColor = Color.Green;
                }
                else if (avg > 12.0 || std >= 2.8)
                {
                    lblAnomaly.Text = $"상태 : ⚠ 이물질/마찰 의심 (평균 {avg:F1}, 변동 {std:F1})";
                    lblAnomaly.ForeColor = Color.Red;
                }
                else
                {
                    lblAnomaly.Text = $"상태 : ⚠ 부하 이상 (평균 {avg:F1}, 변동 {std:F1})";
                    lblAnomaly.ForeColor = Color.DarkOrange;
                }
            }));

            bool isDebris = (avg > 12.0 || std >= 2.8) && avg > ANOMALY_THRESHOLD;

            if (isDebris)
            {
                debrisCount++;
                if (debrisCount >= DEBRIS_TRIGGER && !warningActive)
                    SendWarnOn();
            }
            else
            {
                debrisCount = 0;
                if (warningActive) SendWarnOff();
            }
        }

        private void SendWarnOn()
        {
            SendRaw(0x40);
            warningActive = true;
        }
        private void SendWarnOff()
        {
            SendRaw(0x41);
            warningActive = false;
        }
        private void SendRaw(byte cmd)
        {
            if (!connected || stream == null) return;
            try
            {
                byte len = 0x00;
                byte chk = (byte)(len ^ cmd);
                byte[] packet = new byte[] { STX, len, cmd, chk, ETX };
                stream.Write(packet, 0, packet.Length);
            }
            catch { }
        }

        // ===== 기록 시작 / 중지 =====
        private void btnRecord_Click(object sender, EventArgs e)
        {
            if (!isRecording)
            {
                try
                {
                    string fileName = "log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                    string path = Path.Combine(Application.StartupPath, fileName);
                    logWriter = new StreamWriter(path, false);
                    logWriter.WriteLine("elapsed_ms,current_diff,current_A,distance_cm,command");
                    recordStartTime = DateTime.Now;
                    logCount = 0;
                    isRecording = true;
                    btnRecord.Text = "기록 중지";
                    MessageBox.Show("기록 시작\n" + path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("파일을 만들 수 없습니다: " + ex.Message);
                }
            }
            else
            {
                isRecording = false;
                btnRecord.Text = "기록 시작";
                if (logWriter != null)
                {
                    logWriter.Flush();
                    logWriter.Close();
                    logWriter = null;
                }
                MessageBox.Show("기록 완료: " + logCount + "줄 저장됨");
            }
        }

        private void WriteLog(int diff)
        {
            if (!isRecording || logWriter == null) return;
            try
            {
                long elapsed = (long)(DateTime.Now - recordStartTime).TotalMilliseconds;
                float amps = diff * CURRENT_PER_DIFF;
                logWriter.WriteLine(elapsed + "," + diff + "," + amps.ToString("F3") + "," + lastDistance + "," + lastCommand);
                logCount++;
            }
            catch { }
        }

        private void UpdateLabel()
        {
            int[] arr = currentData.ToArray();
            int diff = arr.Length > 0 ? arr[arr.Length - 1] : 0;
            float amps = diff * CURRENT_PER_DIFF;

            string distText = (lastDistance == 0) ? "-- cm" : lastDistance + " cm";
            string text = "거리 : " + distText + "    전류 : " + amps.ToString("F2") + " A";
            if (isRecording) text += "    [REC " + logCount + "]";

            lblSensor.Invoke(new Action(() =>
            {
                lblSensor.Text = text;
            }));
        }

        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = panel1.Width;
            int h = panel1.Height;

            if (currentData.Count < 2) return;

            int[] arr = currentData.ToArray();
            int maxValue = 80;
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

        // ---- 명령 전송 (socket) ----
        private void SendCommand(byte cmd, string label)
        {
            if (!connected || stream == null)
            {
                lblStatus.Text = "먼저 연결하세요";
                return;
            }
            try
            {
                byte len = 0x00;
                byte chk = (byte)(len ^ cmd);
                byte[] packet = new byte[] { STX, len, cmd, chk, ETX };
                stream.Write(packet, 0, packet.Length);
                lastCommand = label;
                warningActive = false;
                debrisCount = 0;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "전송 실패";
            }
        }

        private void btnForward_Click(object sender, EventArgs e) { SendCommand(CMD_FORWARD, "FORWARD"); }
        private void btnBackward_Click(object sender, EventArgs e) { SendCommand(CMD_BACKWARD, "BACKWARD"); }
        private void btnLeft_Click(object sender, EventArgs e) { SendCommand(CMD_LEFT, "LEFT"); }
        private void btnRight_Click(object sender, EventArgs e) { SendCommand(CMD_RIGHT, "RIGHT"); }
        private void btnStop_Click(object sender, EventArgs e) { SendCommand(CMD_STOP, "STOP"); }
        private void btnEStop_Click(object sender, EventArgs e) { SendCommand(CMD_STOP, "ESTOP"); }
    }
}