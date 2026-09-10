// Packet.h - AMR 패킷 프로토콜 선언
#pragma once
#include <vector>
#include <cstdint>

constexpr uint8_t STX = 0x02;
constexpr uint8_t ETX = 0x03;

enum Cmd : uint8_t {
    CMD_MOVE_FWD = 0x10,
    CMD_MOVE_BACK = 0x11,
    CMD_MOVE_LEFT = 0x12,
    CMD_MOVE_RIGHT = 0x13,
    CMD_MOVE_STOP = 0x14,
    CMD_DIST = 0x20,
    CMD_CURRENT = 0x21,
    CMD_HEARTBEAT = 0x30,
    CMD_WARN_ON = 0x40,
    CMD_WARN_OFF = 0x41,
};

struct ParseResult {
    bool ok;
    uint8_t cmd;
    std::vector<uint8_t> data;
    const char* err;
};

uint8_t calcChecksum(uint8_t cmd, const std::vector<uint8_t>& data);
std::vector<uint8_t> buildPacket(uint8_t cmd, const std::vector<uint8_t>& data);
ParseResult parsePacket(const std::vector<uint8_t>& pkt);
void printHex(const std::vector<uint8_t>& pkt);