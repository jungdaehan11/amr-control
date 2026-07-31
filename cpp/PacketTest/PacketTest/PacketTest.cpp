// main.cpp - 패킷 빌더/파서 테스트
#include "Packet.h"
#include <iostream>

int main() {
    // 예: 전류값 1234mA 전송 (2바이트, 상위/하위)
    uint16_t current = 1234;
    std::vector<uint8_t> data = {
        static_cast<uint8_t>(current >> 8),
        static_cast<uint8_t>(current & 0xFF)
    };

    auto pkt = buildPacket(CMD_CURRENT, data);
    std::cout << "Built:  ";
    printHex(pkt);

    auto r = parsePacket(pkt);
    std::cout << "Parse:  " << (r.ok ? "OK" : "FAIL")
        << " (" << r.err << ")\n";
    if (r.ok) {
        uint16_t val = (r.data[0] << 8) | r.data[1];
        std::cout << "CMD=0x" << std::hex << (int)r.cmd
            << " current=" << std::dec << val << "mA\n";
    }

    // 일부러 체크섬 깨서 테스트
    pkt[3] ^= 0xFF;
    auto bad = parsePacket(pkt);
    std::cout << "Broken: " << (bad.ok ? "OK" : "FAIL")
        << " (" << bad.err << ")\n";

    return 0;
}