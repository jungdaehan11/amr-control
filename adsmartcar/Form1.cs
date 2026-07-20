using System;
using System.Collections.Generic;   // Queue 사용
using System.Drawing;               // 그래프 그리기 (Graphics, Pen, Color)
using System.Drawing.Drawing2D;     // 부드러운 선(안티앨리어싱)
using System.IO.Ports;
using System.Windows.Forms;

namespace adsmartcar
{
    public partial class Form1 : Form
    {
        private SerialPort port = new SerialPort("COM4", 9600);

        // ===== 그래프용 데이터 저장 =====
        // 최근 거리값들을 순서대로 보관. 큐(Queue): 먼저 들어온 게 먼저 나감
        private Queue<int> distanceData = new Queue<int>();
        private const int MAX_POINTS = 100;   // 화면에 최근 100개만 표시 (오래된 건 밀어냄)

        public Form1()
        {
            InitializeComponent();
            port.NewLine = "\n";

            // ===== 그래프 초기 설정 =====
            // 깜빡임 방지(더블 버퍼링): panel이 그려질 때 화면에서 직접 그리지 않고
            // 메모리에 완성한 뒤 한 번에 표시 → 깜빡임 없음
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(panel1, true, null);

            // panel이 다시 그려져야 할 때 호출될 함수(Paint) 연결
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

        // ---- 수신: 거리값 받아서 화면 + 그래프 갱신 ----
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = port.ReadLine().Trim();

                lblSensor.Invoke(new Action(() =>
                {
                    if (line == "-1")
                    {
                        lblSensor.Text = "거리 : -- cm";
                    }
                    else
                    {
                        lblSensor.Text = "거리 : " + line + " cm";

                        // 숫자로 바뀌면 그래프 데이터에 추가
                        if (int.TryParse(line, out int dist))
                        {
                            distanceData.Enqueue(dist);            // 새 값 뒤에 추가
                            while (distanceData.Count > MAX_POINTS) // 100개 넘으면
                                distanceData.Dequeue();            // 가장 오래된 것 버림

                            panel1.Invalidate();  // "panel 다시 그려줘" 신호 → Paint 호출됨
                        }
                    }
                }));
            }
            catch
            {
            }
        }

        // ===== 그래프 그리기 (panel이 다시 그려질 때마다 자동 호출) =====
        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;  // 선을 부드럽게

            int w = panel1.Width;
            int h = panel1.Height;

            // 데이터가 2개 미만이면 그릴 선이 없음
            if (distanceData.Count < 2) return;

            // 큐를 배열로 바꿔서 인덱스로 접근
            int[] data = distanceData.ToArray();

            // 세로축 스케일: 0 ~ 최대거리. 여기선 0~100cm를 panel 높이에 매핑
            // (100cm 넘는 값은 위쪽에 붙음. 나중에 자동 스케일로 개선 가능)
            int maxCm = 100;

            // 가로 간격: 화면 폭을 점 개수로 나눔
            float xStep = (float)w / (MAX_POINTS - 1);

            using (Pen pen = new Pen(Color.LimeGreen, 2))
            {
                for (int i = 0; i < data.Length - 1; i++)
                {
                    // 데이터값(cm)을 화면 y좌표로 변환
                    // 값이 클수록(멀수록) 위로 가도록: y = 높이 - (값/최대 * 높이)
                    float x1 = i * xStep;
                    float y1 = h - (data[i] / (float)maxCm * h);
                    float x2 = (i + 1) * xStep;
                    float y2 = h - (data[i + 1] / (float)maxCm * h);

                    g.DrawLine(pen, x1, y1, x2, y2);  // 두 점을 선으로 연결
                }
            }
        }

        // ---- 명령 전송 ----
        private void Send(char cmd)
        {
            if (port.IsOpen)
                port.Write(cmd.ToString());
            else
                lblStatus.Text = "먼저 연결하세요";
        }

        private void btnForward_Click(object sender, EventArgs e) { Send('g'); }
        private void btnBackward_Click(object sender, EventArgs e) { Send('b'); }
        private void btnLeft_Click(object sender, EventArgs e) { Send('l'); }
        private void btnRight_Click(object sender, EventArgs e) { Send('r'); }
        private void btnStop_Click(object sender, EventArgs e) { Send('s'); }
        private void btnEStop_Click(object sender, EventArgs e) { Send('s'); }
    }
}