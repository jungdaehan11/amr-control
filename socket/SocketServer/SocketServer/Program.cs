using System;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;
using System.Threading;

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
        const byte CMD_HEARTBEAT = 0x30;

        static SerialPort robotPort;
        static NetworkStream clientStream;   // 관제로 보낼 stream
        static bool running = true;

        static void Main(string[] args)
        {
            // 1. 로봇 시리얼 연결
            try
            {
                robotPort = new SerialPort("COM8", 9600);
                robotPort.Open();
                Console.WriteLine("로봇 연결됨 (COM8)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("로봇 연결 실패: " + ex.Message);
            }

            // 2. 하트비트 스레드
            Thread heartbeatThread = new Thread(HeartbeatLoop);
            heartbeatThread.IsBackground = true;
            heartbeatThread.Start();

            // 3. 로봇 시리얼 수신 스레드 (로봇 → 관제)
            Thread serialThread = new Thread(SerialReceiveLoop);
            serialThread.IsBackground = true;
            serialThread.Start();
            Console.WriteLine("하트비트 + 센서 중계 시작");

            // 4. socket 서버
            TcpListener server = new TcpListener(IPAddress.Any, 5000);
            server.Start();
            Console.WriteLine("브릿지 서버 시작 - 포트 5000 대기 중...");

            TcpClient client = server.AcceptTcpClient();
            Console.WriteLine("관제 접속됨!");

            clientStream = client.GetStream();
            byte[] buffer = new byte[1024];

            // 메인 스레드: socket 수신 (관제 → 명령)
            while (true)
            {
                int bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("관제 연결 종료됨");
                    break;
                }
                for (int i = 0; i < bytesRead; i++)
                {
                    ProcessCommandByte(buffer[i]);
                }
            }

            running = false;
            if (robotPort != null && robotPort.IsOpen) robotPort.Close();
            client.Close();
            server.Stop();
            Console.WriteLine("종료.");
        }

        // ===== 하트비트 스레드 =====
        static void HeartbeatLoop()
        {
            while (running)
            {
                if (robotPort != null && robotPort.IsOpen)
                {
                    try
                    {
                        byte len = 0x00;
                        byte chk = (byte)(len ^ CMD_HEARTBEAT);
                        byte[] hb = new byte[] { STX, len, CMD_HEARTBEAT, chk, ETX };
                        robotPort.Write(hb, 0, hb.Length);
                    }
                    catch { }
                }
                Thread.Sleep(100);
            }
        }

        // ===== 로봇 시리얼 수신 스레드 (로봇 → 관제) =====
        static void SerialReceiveLoop()
        {
            byte[] buf = new byte[256];
            while (running)
            {
                if (robotPort != null && robotPort.IsOpen)
                {
                    try
                    {
                        int n = robotPort.BytesToRead;
                        if (n > 0)
                        {
                            int read = robotPort.Read(buf, 0, n);
                            // 로봇에서 온 센서 데이터를 그대로 관제로 socket 전송
                            if (clientStream != null)
                            {
                                clientStream.Write(buf, 0, read);
                            }
                        }
                    }
                    catch { }
                }
                Thread.Sleep(10);
            }
        }

        // ===== 관제 명령 패킷 조립기 (관제 → 로봇) =====
        static byte[] packetBuf = new byte[10];
        static int packetCount = 0;
        static bool receiving = false;

        static void ProcessCommandByte(byte b)
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
                byte len = packetBuf[1];
                byte cmd = packetBuf[2];
                byte chk = packetBuf[3];
                byte etx = packetBuf[4];

                if (etx != ETX) return;
                if (chk != (byte)(len ^ cmd)) return;

                string cmdName = cmd switch
                {
                    CMD_FORWARD => "전진",
                    CMD_BACKWARD => "후진",
                    CMD_LEFT => "좌회전",
                    CMD_RIGHT => "우회전",
                    CMD_STOP => "정지",
                    _ => "?"
                };
                Console.WriteLine($"[명령] {cmdName}");

                // 로봇에 전달
                if (robotPort != null && robotPort.IsOpen)
                {
                    byte[] packet = new byte[] { STX, len, cmd, chk, ETX };
                    robotPort.Write(packet, 0, packet.Length);
                }
            }
        }
    }
}