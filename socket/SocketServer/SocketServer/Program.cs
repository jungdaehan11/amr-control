using System;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;

namespace SocketServer
{
    class Program
    {
        const byte STX = 0x02;
        const byte ETX = 0x03;
        const byte CMD_FORWARD = 0x10;
        const byte CMD_BACKWARD = 0x11;
        const byte CMD_LEFT = 0x12;
        const byte CMD_RIGHT = 0x13;
        const byte CMD_STOP = 0x14;

        static SerialPort robotPort;   // 로봇 블루투스

        static void Main(string[] args)
        {
            // 1. 로봇 시리얼(블루투스) 연결
            try
            {
                robotPort = new SerialPort("COM8", 9600);
                robotPort.Open();
                Console.WriteLine("로봇 연결됨 (COM8)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("로봇 연결 실패: " + ex.Message);
                Console.WriteLine("(로봇 없이 계속 - 명령 해석만 함)");
            }

            // 2. socket 서버 시작
            TcpListener server = new TcpListener(IPAddress.Any, 5000);
            server.Start();
            Console.WriteLine("브릿지 서버 시작 - 포트 5000 대기 중...");

            TcpClient client = server.AcceptTcpClient();
            Console.WriteLine("관제 접속됨!");

            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("연결 종료됨");
                    break;
                }

                for (int i = 0; i < bytesRead; i++)
                {
                    ProcessByte(buffer[i]);
                }
            }

            if (robotPort != null && robotPort.IsOpen) robotPort.Close();
            client.Close();
            server.Stop();
            Console.WriteLine("종료. 아무 키나 누르세요.");
            Console.ReadKey();
        }

        static byte[] packetBuf = new byte[10];
        static int packetCount = 0;
        static bool receiving = false;

        static void ProcessByte(byte b)
        {
            if (b == STX)
            {
                packetBuf[0] = b;
                packetCount = 1;
                receiving = true;
                return;
            }
            if (!receiving) return;

            packetBuf[packetCount] = b;
            packetCount++;

            if (packetCount == 5)
            {
                receiving = false;
                ValidateAndHandle();
            }
        }

        static void ValidateAndHandle()
        {
            byte len = packetBuf[1];
            byte cmd = packetBuf[2];
            byte chk = packetBuf[3];
            byte etx = packetBuf[4];

            if (etx != ETX) { Console.WriteLine("[오류] ETX 불일치"); return; }
            if (chk != (byte)(len ^ cmd)) { Console.WriteLine("[오류] 체크섬 불일치"); return; }

            string cmdName = cmd switch
            {
                CMD_FORWARD => "전진",
                CMD_BACKWARD => "후진",
                CMD_LEFT => "좌회전",
                CMD_RIGHT => "우회전",
                CMD_STOP => "정지",
                _ => "알 수 없음"
            };
            Console.WriteLine($"[명령] {cmdName} (0x{cmd:X2})");

            // ★ 로봇에 그대로 전달 (socket → 시리얼) ★
            if (robotPort != null && robotPort.IsOpen)
            {
                // 받은 패킷을 그대로 로봇에 재전송
                byte[] packet = new byte[] { STX, len, cmd, chk, ETX };
                robotPort.Write(packet, 0, packet.Length);
                Console.WriteLine("  → 로봇에 전달됨");
            }
        }
    }
}