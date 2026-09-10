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
        private volatile bool connected = false;
        private const string BRIDGE_IP = "127.0.0.1";   // 브릿지 IP (같은 PC면 127.0.0.1)
        private const int BRIDGE_PORT = 5000;

        // 송신 직렬화용 락.
        // stream.Write() 는 UI 스레드(SendCommand)와 수신 스레드(SendWarnOn/Off) 양쪽에서
        // 호출되므로, 락이 없으면 두 패킷이 섞여 나가 프로토콜이 깨질 수 있다.
        private readonly object sendLock = new object();

        // 판정 상태 보호용 락. anomalyWindow / debrisCount / warningActive / 구간 상태는
        // 수신 스레드에서 갱신되고 UI 스레드(명령 전송)에서도 건드리므로 보호가 필요하다.
        private readonly object stateLock = new object();

        // ===== 그래프 데이터 (UI 스레드 전용) =====
        private Queue<int> currentData = new Queue<int>();
        private const int MAX_POINTS = 100;
        private volatile int lastDistance = 0;
        private volatile int lastDiff = 0;      // 최근 전류 원시값 (UpdateLabel 표시용)

        // ===== 이상 감지 =====
        private Queue<int> anomalyWindow = new Queue<int>();
        private const int WINDOW_SIZE = 20;

        // 현재 누적 중인 '구동 구간'의 명령. 명령이 바뀌면 구간이 끊긴 것으로 보고
        // 윈도우 버퍼를 비운다. (아래 CheckAnomaly 주석 참조)
        private string segmentCommand = "STOP";

        // 진단 대상 구간. 경계값이 FORWARD 실측 데이터만으로 도출됐으므로 FORWARD 한정.
        // (회전은 양 바퀴가 역방향이라 전류가 구조적으로 높고, 후진은 실측 데이터가 없다)
        private const string DIAG_COMMAND = "FORWARD";

        // ===== 상태 진단 판정 경계 =====
        // 근거: 실측 FORWARD 구간 슬라이딩 윈도우(W=20) 분석 — analysis/classify.py
        //   데이터: 정상/부하/이물질 각 3세트, 약 8분, 총 3,376 윈도우
        //   윈도우 평균 분포 → 부하 8.95~11.35 / 이물질 13.20~15.65 (분리 구간 존재)
        //
        // NORMAL_MAX(9.4): 정상 오탐과 부하 놓침이 균형을 이루는 지점.
        // DEBRIS_MIN(12.0): 부하 최대(11.35)와 이물질 최소(13.20) 사이. 이물질 재현율 100%.
        //
        // 성능: 전체 정확도 99.3% / Macro-F1 99.2% / 이물질 재현율 100%
        //       세트 단위 3-fold 교차검증 99.57% (과적합 폭 0.10%p)
        //
        // 표준편차 조건(std >= 2.8)은 제거함:
        //   이물질은 평균만으로 이미 100% 검출되어 std가 추가로 건질 대상이 없고,
        //   변동이 큰 부하 구간을 이물질로 오분류시켜 불필요한 자동 정지를 유발했다.
        //   제거 결과 전체 정확도 94.3% → 98.5%, 부하 재현율 83.6% → 97.7%.
        //   (std는 운전자 참고용으로 화면에만 표시)
        private const double NORMAL_MAX = 9.4;    // 정상 / 부하 경계
        private const double DEBRIS_MIN = 12.0;   // 부하 / 이물질 경계

        private enum MotorState { Normal, Overload, Debris }

        // 판정 로직 단일 진입점. analysis/classify.py의 classify()와 동일한 규칙을 유지한다.
        private static MotorState Classify(double windowMean)
        {
            if (windowMean < NORMAL_MAX) return MotorState.Normal;
            if (windowMean < DEBRIS_MIN) return MotorState.Overload;
            return MotorState.Debris;
        }

        // ===== 이물질 지속 감지 → 서보 경고 =====
        private int debrisCount = 0;
        private const int DEBRIS_TRIGGER = 20;
        private bool warningActive = false;

        // ===== 데이터 로깅 =====
        private StreamWriter logWriter = null;
        private volatile bool isRecording = false;
        private DateTime recordStartTime;
        private volatile string lastCommand = "STOP";
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
        private const byte CMD_WARN_ON = 0x40;
        private const byte CMD_WARN_OFF = 0x41;

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

                // 수신 스레드가 빠져나올 시간을 짧게 준다
                if (receiveThread != null && receiveThread.IsAlive)
                    receiveThread.Join(300);
                receiveThread = null;

                // 연결이 끊기면 진단 상태도 초기화 (끊긴 구간의 데이터가 다음 구간에 섞이지 않게)
                lock (stateLock)
                {
                    anomalyWindow.Clear();
                    segmentCommand = "STOP";
                    debrisCount = 0;
                    warningActive = false;
                }

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
                lastDiff = diff;

                WriteLog(diff);
                CheckAnomaly(diff);

                // 그래프 버퍼는 UI 스레드에서만 만지도록 Invoke 안에서 처리한다.
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
        //
        // ★ 구동 구간 단위 윈도우 (analysis/classify.py 의 load_segments() 와 동일한 개념)
        //
        //   한 번의 주행(FORWARD 연속 구간)이 끝나고 다음 주행이 시작되면, 두 주행은
        //   서로 다른 구간이다. 하나의 윈도우가 두 구간에 걸치면 앞 주행의 끝과
        //   다음 주행의 시작이 한 윈도우에 섞인다.
        //
        //   문제는 주행 시작마다 '모터 기동 돌입전류'가 나타난다는 점이다.
        //   정지 상태의 DC 모터는 역기전력(E=k*omega)이 0이라 기동 순간 전류가 V/R 까지
        //   치솟고, 회전이 붙으면서 정상값으로 내려온다.
        //   실측에서 구간 시작값이 본체 평균의 최대 2.58배까지 튀었다.
        //
        //   따라서 명령이 바뀌면(=구간이 끊기면) 윈도우 버퍼를 비우고, 주행 구간에서만
        //   값을 누적한다. 분석 결과 정상 오탐률 2.43% -> 0.41% 로 감소했다.
        //
        //   ※ 이 방식을 analysis/classify.py 와 반드시 동일하게 유지할 것.
        //      한쪽만 바꾸면 검증 결과와 실제 앱 동작이 어긋난다.
        private void CheckAnomaly(int diff)
        {
            string cmd = lastCommand;
            bool diagnosable = (cmd == DIAG_COMMAND);

            bool filling;          // 아직 윈도우가 다 안 참
            double avg = 0, std = 0;
            MotorState state = MotorState.Normal;
            bool needWarnOn = false, needWarnOff = false;

            // --- 상태 갱신은 락 안에서, UI 갱신/송신은 락 밖에서 ---
            // (락을 쥔 채 Invoke 하면 UI 스레드와 교착될 수 있다)
            lock (stateLock)
            {
                // ★ 구간이 바뀌었으면 버퍼를 비운다
                if (cmd != segmentCommand)
                {
                    anomalyWindow.Clear();
                    segmentCommand = cmd;
                    debrisCount = 0;
                    if (warningActive) needWarnOff = true;
                }

                if (!diagnosable)
                {
                    // 진단 대상 구간이 아니면 값을 누적하지 않는다.
                    // (정지/회전 구간의 값이 다음 주행 윈도우에 섞이는 것을 막는다)
                    if (warningActive) needWarnOff = true;
                    debrisCount = 0;
                }
                else
                {
                    anomalyWindow.Enqueue(diff);
                    while (anomalyWindow.Count > WINDOW_SIZE)
                        anomalyWindow.Dequeue();
                }

                filling = anomalyWindow.Count < WINDOW_SIZE;

                if (diagnosable && !filling)
                {
                    avg = anomalyWindow.Average();
                    double variance = anomalyWindow.Select(x => (x - avg) * (x - avg)).Average();
                    std = Math.Sqrt(variance);
                    state = Classify(avg);

                    if (state == MotorState.Debris)
                    {
                        debrisCount++;
                        if (debrisCount >= DEBRIS_TRIGGER && !warningActive)
                            needWarnOn = true;
                    }
                    else
                    {
                        debrisCount = 0;
                        if (warningActive) needWarnOff = true;
                    }
                }
            }

            // --- 락 밖: 서보 경고 송신 ---
            if (needWarnOn) SendWarnOn();
            else if (needWarnOff) SendWarnOff();

            // --- 락 밖: UI 갱신 ---
            string text;
            Color color;
            if (!diagnosable)
            {
                text = "상태 : 정지/회전";
                color = Color.Gray;
            }
            else if (filling)
            {
                text = "상태 : 측정 중...";
                color = Color.Gray;
            }
            else
            {
                switch (state)
                {
                    case MotorState.Overload:
                        text = $"상태 : ⚠ 부하 이상 (평균 {avg:F1}, 변동 {std:F1})";
                        color = Color.DarkOrange;
                        break;
                    case MotorState.Debris:
                        text = $"상태 : ⚠ 이물질/마찰 의심 (평균 {avg:F1}, 변동 {std:F1})";
                        color = Color.Red;
                        break;
                    default:
                        text = $"상태 : 정상 (평균 {avg:F1}, 변동 {std:F1})";
                        color = Color.Green;
                        break;
                }
            }

            try
            {
                lblAnomaly.Invoke(new Action(() =>
                {
                    lblAnomaly.Text = text;
                    lblAnomaly.ForeColor = color;
                }));
            }
            catch { }   // 폼이 닫히는 중이면 무시
        }

        private void SendWarnOn()
        {
            SendRaw(CMD_WARN_ON);
            lock (stateLock) { warningActive = true; }
        }

        private void SendWarnOff()
        {
            SendRaw(CMD_WARN_OFF);
            lock (stateLock) { warningActive = false; }
        }

        private void SendRaw(byte cmd)
        {
            if (!connected || stream == null) return;
            try
            {
                byte len = 0x00;
                byte chk = (byte)(len ^ cmd);
                byte[] packet = new byte[] { STX, len, cmd, chk, ETX };
                // UI 스레드와 수신 스레드가 동시에 쓰지 못하도록 직렬화
                lock (sendLock)
                {
                    stream.Write(packet, 0, packet.Length);
                }
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
                lock (sendLock)     // logWriter 접근 직렬화
                {
                    if (logWriter != null)
                    {
                        logWriter.Flush();
                        logWriter.Close();
                        logWriter = null;
                    }
                }
                MessageBox.Show("기록 완료: " + logCount + "줄 저장됨");
            }
        }

        // 주의(알려진 한계): command 열에는 'PC가 마지막으로 보낸 명령'이 기록된다.
        // 로봇이 워치독으로 자율 정지하거나 주행이 끝나도 PC는 그것을 모르므로,
        // 전류가 0인데 command 가 FORWARD 로 남는 '꼬리 0 블록'이 생긴다.
        // 분석 쪽(analysis/classify.py)에서 이 구간을 제거하는 전처리로 대응하고 있으며,
        // 근본 해결은 로봇이 실제 구동 상태를 패킷으로 보고하도록 프로토콜을 넓히는 것이다.
        private void WriteLog(int diff)
        {
            if (!isRecording) return;
            try
            {
                lock (sendLock)
                {
                    if (logWriter == null) return;
                    long elapsed = (long)(DateTime.Now - recordStartTime).TotalMilliseconds;
                    float amps = diff * CURRENT_PER_DIFF;
                    logWriter.WriteLine(elapsed + "," + diff + "," + amps.ToString("F3") + ","
                                        + lastDistance + "," + lastCommand);
                    logCount++;
                }
            }
            catch { }
        }

        private void UpdateLabel()
        {
            // currentData(그래프 버퍼)는 UI 스레드 전용이므로 여기서 만지지 않는다.
            // 최근 값은 lastDiff 로 따로 들고 있다.
            int diff = lastDiff;
            float amps = diff * CURRENT_PER_DIFF;

            string distText = (lastDistance == 0) ? "-- cm" : lastDistance + " cm";
            string text = "거리 : " + distText + "    전류 : " + amps.ToString("F2") + " A";
            if (isRecording) text += "    [REC " + logCount + "]";

            try
            {
                lblSensor.Invoke(new Action(() =>
                {
                    lblSensor.Text = text;
                }));
            }
            catch { }   // 폼이 닫히는 중이면 무시
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

            // 경고 중이었다면 실제로 서보를 내린 뒤 상태를 초기화한다.
            bool wasWarning;
            lock (stateLock) { wasWarning = warningActive; }
            if (wasWarning) SendWarnOff();

            try
            {
                byte len = 0x00;
                byte chk = (byte)(len ^ cmd);
                byte[] packet = new byte[] { STX, len, cmd, chk, ETX };
                lock (sendLock)
                {
                    stream.Write(packet, 0, packet.Length);
                }

                lastCommand = label;
                // 실제 버퍼 비우기는 CheckAnomaly 가 명령 변화를 감지해 처리한다.
                // (판정 상태를 한 곳에서만 바꾸도록 유지)
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
