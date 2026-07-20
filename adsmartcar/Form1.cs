using System;
using System.IO.Ports;
using System.Windows.Forms;
using System.Drawing;   // Form1_Load에서 안 쓰면 없어도 되지만, 색 지정 등 대비해 둠

namespace adsmartcar
{
    public partial class Form1 : Form
    {
        // 클래스 전체에서 쓰는 시리얼 포트 (아두이노 = COM4, 9600bps)
        private SerialPort port = new SerialPort("COM4", 9600);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 지금은 비워둠 (나중에 초기화 코드 자리)
        }

        // ---- 연결 / 연결 끊기 ----
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (!port.IsOpen)
                {
                    port.Open();
                    lblStatus.Text = "연결됨";
                    btnConnect.Text = "연결 끊기";
                }
                else
                {
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

        // ---- 명령 한 글자 전송 (공통) ----
        private void Send(char cmd)
        {
            if (port.IsOpen)
            {
                port.Write(cmd.ToString());
            }
            else
            {
                lblStatus.Text = "먼저 연결하세요";
            }
        }

        // ---- 방향 / 정지 버튼 ----
        private void btnForward_Click(object sender, EventArgs e) { Send('g'); }
        private void btnBackward_Click(object sender, EventArgs e) { Send('b'); }
        private void btnLeft_Click(object sender, EventArgs e) { Send('l'); }
        private void btnRight_Click(object sender, EventArgs e) { Send('r'); }
        private void btnStop_Click(object sender, EventArgs e) { Send('s'); }

        // ---- 비상정지 = 정지와 동일한 's' ----
        private void btnEStop_Click(object sender, EventArgs e) { Send('s'); }
    }
}