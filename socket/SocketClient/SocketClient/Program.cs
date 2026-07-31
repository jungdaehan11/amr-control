using System;
using System.Net.Sockets;
using System.Threading;

namespace SocketClient
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
        const byte CMD_DISTANCE = 0x20;
        const byte CMD_CURRENT = 0x21;

        static NetworkStream stream;
        static bool running = true;

        static void Main(string[] args)
        {
            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 5000);
            Console.WriteLine("브릿지에 접속됨!");
            Console.WriteLine("w=전진 s=후진 a=좌 d=우 x=정지 q=종료");

            stream = client.GetStream();

            // 센서 수신 스레드 (로봇 → 관제)
            Thread receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // 메인 스레드: 키보드 입력 → 명령 전송
            while (true)
            {
                string input = Console.ReadLine();
                if (input == "q") break;

                byte cmd;
                switch (input)
                {
                    case "w": cmd = CMD_FORWARD; break;
                    case "s": cmd = CMD_BACKWARD; break;
                    case "a": cmd = CMD_LEFT; break;
                    case "d": cmd = CMD_RIGHT; break;
                    case "x": cmd = CMD_STOP; break;
                    default: continue;
                }

                byte len = 0x00;
                byte chk = (byte)(len ^ cmd);
                byte[] packet = new byte[] { STX, len, cmd, chk, ETX };
                stream.Write(packet, 0, packet.Length);
            }

            running = false;
            client.Close();
        }

        // ===== 센서 수신 스레드 =====
        static void ReceiveLoop()
        {
            byte[] buffer = new byte[1024];
            while (running)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        ProcessSensorByte(buffer[i]);
                    }
                }
                catch { break; }
            }
        }

        // ===== 센서 패킷 조립기 =====
        static byte[] sensorBuf = new byte[10];
        static int sensorCount = 0;
        static bool sensorReceiving = false;

        static void ProcessSensorByte(byte b)
        {
            if (b == STX)
            {
                sensorBuf[0] = b;
                sensorCount = 1;
                sensorReceiving = true;
                return;
            }
            if (!sensorReceiving) return;

            sensorBuf[sensorCount] = b;
            sensorCount++;

            // 센서 패킷은 6바이트: STX LEN CMD DATA CHK ETX
            if (sensorCount == 6)
            {
                sensorReceiving = false;
                byte len = sensorBuf[1];
                byte cmd = sensorBuf[2];
                byte data = sensorBuf[3];
                byte chk = sensorBuf[4];
                byte etx = sensorBuf[5];

                if (etx != ETX) return;
                if (chk != (byte)(len ^ cmd ^ data)) return;

                if (cmd == CMD_DISTANCE)
                    Console.WriteLine($"   [센서] 거리: {data} cm");
                else if (cmd == CMD_CURRENT)
                    Console.WriteLine($"   [센서] 전류 diff: {data}");
            }
        }
    }
}