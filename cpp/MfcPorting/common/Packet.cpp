// Packet.cpp - AMR 패킷 프로토콜 구현
#include "Packet.h"
#include <iostream>
#include <iomanip>

// XOR 체크섬: CMD + DATA 전체
uint8_t calcChecksum(uint8_t cmd, const std::vector<uint8_t>& data) {
    uint8_t chk = cmd;
    for (uint8_t b : data) chk ^= b;
    return chk;
}

// 패킷 조립: [STX][LEN][CMD][DATA...][CHK][ETX]
std::vector<uint8_t> buildPacket(uint8_t cmd, const std::vector<uint8_t>& data) {
    std::vector<uint8_t> pkt;
    pkt.push_back(STX);
    pkt.push_back(static_cast<uint8_t>(data.size()));
    pkt.push_back(cmd);
    for (uint8_t b : data) pkt.push_back(b);
    pkt.push_back(calcChecksum(cmd, data));
    pkt.push_back(ETX);
    return pkt;
}

// 패킷 검증/분해
ParseResult parsePacket(const std::vector<uint8_t>& pkt) {
    if (pkt.size() < 5)     return { false, 0, {}, "too short" };
    if (pkt.front() != STX) return { false, 0, {}, "no STX" };
    if (pkt.back() != ETX) return { false, 0, {}, "no ETX" };

    uint8_t len = pkt[1];
    if (pkt.size() != static_cast<size_t>(len) + 5)
        return { false, 0, {}, "len mismatch" };

    uint8_t cmd = pkt[2];
    std::vector<uint8_t> data(pkt.begin() + 3, pkt.begin() + 3 + len);
    uint8_t chk = pkt[3 + len];

    if (chk != calcChecksum(cmd, data))
        return { false, 0, {}, "checksum fail" };

    return { true, cmd, data, "ok" };
}

// 디버그 출력
void printHex(const std::vector<uint8_t>& pkt) {
    std::cout << std::hex << std::uppercase << std::setfill('0');
    for (uint8_t b : pkt)
        std::cout << std::setw(2) << static_cast<int>(b) << ' ';
    std::cout << std::dec << '\n';
}