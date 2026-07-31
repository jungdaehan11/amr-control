// EchoServer.cpp - AMR 패킷 에코 서버 (Winsock)
// 클라이언트가 보낸 패킷을 받아 파싱 후 그대로 되돌려줌
#include "Packet.h"
#include <iostream>
#include <winsock2.h>
#include <ws2tcpip.h>

#pragma comment(lib, "ws2_32.lib")  // Winsock 라이브러리 링크

constexpr int PORT = 9000;

int main() {
    // 1. Winsock 초기화 (C#은 자동, C++은 수동)
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        std::cerr << "WSAStartup failed\n";
        return 1;
    }

    // 2. 리슨 소켓 생성
    SOCKET listenSock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listenSock == INVALID_SOCKET) {
        std::cerr << "socket failed\n";
        WSACleanup();
        return 1;
    }

    // 3. 주소 바인딩 (0.0.0.0:9000)
    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = INADDR_ANY;
    addr.sin_port = htons(PORT);

    if (bind(listenSock, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
        std::cerr << "bind failed\n";
        closesocket(listenSock);
        WSACleanup();
        return 1;
    }

    // 4. 리슨 시작
    listen(listenSock, SOMAXCONN);
    std::cout << "[Server] listening on port " << PORT << "...\n";

    // 5. 클라이언트 접속 대기 (블로킹)
    SOCKET clientSock = accept(listenSock, nullptr, nullptr);
    if (clientSock == INVALID_SOCKET) {
        std::cerr << "accept failed\n";
        closesocket(listenSock);
        WSACleanup();
        return 1;
    }
    std::cout << "[Server] client connected\n";

    // 6. 수신 루프
    uint8_t buf[256];
    while (true) {
        int len = recv(clientSock, (char*)buf, sizeof(buf), 0);
        if (len <= 0) {
            std::cout << "[Server] client disconnected\n";
            break;
        }

        // 받은 바이트를 vector로
        std::vector<uint8_t> pkt(buf, buf + len);
        std::cout << "[Server] recv: ";
        printHex(pkt);

        // 파싱해서 내용 확인
        auto r = parsePacket(pkt);
        if (r.ok) {
            std::cout << "[Server] parsed OK, CMD=0x"
                << std::hex << (int)r.cmd << std::dec << '\n';
        }
        else {
            std::cout << "[Server] parse FAIL: " << r.err << '\n';
        }

        // 에코: 받은 그대로 돌려보냄
        send(clientSock, (char*)buf, len, 0);
    }

    // 7. 정리
    closesocket(clientSock);
    closesocket(listenSock);
    WSACleanup();
    std::cout << "\n종료하려면 Enter...\n";
    std::cin.get();   // 창이 닫히지 않게 대기
    return 0;
}