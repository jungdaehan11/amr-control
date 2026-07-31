using System;
using System.Net.Sockets;

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

        static void Main(string[] args)
        {
            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 5000);   // 데스크톱(브릿지) IP
            Console.WriteLine("브릿지에 접속됨!");
            Console.WriteLine("명령: w=전진 s=후진 a=좌 d=우 x=정지 q=종료");

            NetworkStream stream = client.GetStream();

            while (true)
            {
                Console.Write("> ");
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
                    default:
                        Console.WriteLine("모르는 명령");
                        continue;
                }

                // AMR 패킷 생성: STX LEN CMD CHK ETX
                byte len = 0x00;
                byte chk = (byte)(len ^ cmd);
                byte[] packet = new byte[] { STX, len, cmd, chk, ETX };

                stream.Write(packet, 0, packet.Length);
                Console.WriteLine($"[전송] 명령 0x{cmd:X2}");
            }

            client.Close();
            Console.WriteLine("종료.");
        }
    }
}