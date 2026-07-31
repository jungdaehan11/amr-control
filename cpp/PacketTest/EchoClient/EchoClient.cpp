// EchoClient.cpp - AMR 패킷 에코 클라이언트 (Winsock)
// 서버에 접속해 패킷을 보내고, 되돌아온 응답을 파싱해 확인
#include "Packet.h"
#include <iostream>
#include <winsock2.h>
#include <ws2tcpip.h>

#pragma comment(lib, "ws2_32.lib")

constexpr int PORT = 9000;

int main() {
    // 1. Winsock 초기화
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        std::cerr << "WSAStartup failed\n";
        return 1;
    }

    // 2. 소켓 생성
    SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (sock == INVALID_SOCKET) {
        std::cerr << "socket failed\n";
        WSACleanup();
        return 1;
    }

    // 3. 서버 주소 설정 (127.0.0.1:9000 = 내 PC)
    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(PORT);
    inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr);

    // 4. 서버 접속
    if (connect(sock, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
        std::cerr << "connect failed (서버 먼저 실행했나요?)\n";
        closesocket(sock);
        WSACleanup();
        return 1;
    }
    std::cout << "[Client] connected to server\n";

    // 5. 패킷 만들기: 전류값 1234mA (프로젝트1 프로토콜 그대로)
    uint16_t current = 1234;
    std::vector<uint8_t> data = {
        static_cast<uint8_t>(current >> 8),
        static_cast<uint8_t>(current & 0xFF)
    };
    auto pkt = buildPacket(CMD_CURRENT, data);

    std::cout << "[Client] send: ";
    printHex(pkt);

    // 6. 전송
    send(sock, (char*)pkt.data(), (int)pkt.size(), 0);

    // 7. 에코 응답 수신
    uint8_t buf[256];
    int len = recv(sock, (char*)buf, sizeof(buf), 0);
    if (len > 0) {
        std::vector<uint8_t> echo(buf, buf + len);
        std::cout << "[Client] echo: ";
        printHex(echo);

        auto r = parsePacket(echo);
        if (r.ok) {
            uint16_t val = (r.data[0] << 8) | r.data[1];
            std::cout << "[Client] echo parsed OK, current="
                << val << "mA\n";
        }
    }

    // 8. 정리
    closesocket(sock);
    WSACleanup();
    std::cout << "[Client] done\n";
    std::cin.get();   // 창이 닫히지 않게 대기
    return 0;
}