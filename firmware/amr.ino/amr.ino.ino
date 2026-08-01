// AMR - 블루투스 + 워치독 + 전류센서 + 경고(서보) 최종 버전
// 서보: 평소 detach로 지터 방지, 경고(0x40) 시에만 attach + 좌우 스윙

#include <SoftwareSerial.h>
#include <Servo.h>

SoftwareSerial bluetooth(3, 4);
Servo warnServo;

// ---- 모터 핀맵 ----
const int ENR = 5,  IN1 = 8,  IN2 = 9;
const int ENL = 6,  IN3 = 10, IN4 = 11;
const int SPEED = 153;

// ---- 초음파 핀맵 ----
const int TRIG = 13;
const int ECHO = 12;

// ---- 서보 ----
const int SERVO_PIN = 2;

// ---- 전류센서 ----
const int CURRENT_PIN = A0;
float zeroRaw = 512.0;
bool motorRunning = false;

// ---- 주기 송신 ----
unsigned long lastSend = 0;
const unsigned long SEND_INTERVAL = 100;

// ---- 워치독 ----
unsigned long lastHeartbeat = 0;
const unsigned long WATCHDOG_TIMEOUT = 1200;
bool commAlive = false;

// ---- 경고 모드 ----
bool warningMode = false;
unsigned long lastServoMove = 0;
const unsigned long SERVO_INTERVAL = 300;
bool servoSide = false;

// ---- 패킷 상수 ----
const byte STX = 0x02;
const byte ETX = 0x03;

const byte CMD_FORWARD   = 0x10;
const byte CMD_BACKWARD  = 0x11;
const byte CMD_LEFT      = 0x12;
const byte CMD_RIGHT     = 0x13;
const byte CMD_STOP      = 0x14;
const byte CMD_DISTANCE  = 0x20;
const byte CMD_CURRENT   = 0x21;
const byte CMD_HEARTBEAT = 0x30;
const byte CMD_WARN_ON    = 0x40;
const byte CMD_WARN_OFF   = 0x41;

// ---- 명령 패킷 조립용 ----
byte rxBuf[5];
int  rxCount = 0;
bool receiving = false;

void setup() {
  bluetooth.begin(9600);
  pinMode(ENR, OUTPUT); pinMode(IN1, OUTPUT); pinMode(IN2, OUTPUT);
  pinMode(ENL, OUTPUT); pinMode(IN3, OUTPUT); pinMode(IN4, OUTPUT);
  pinMode(TRIG, OUTPUT);
  pinMode(ECHO, INPUT);

  // 서보 초기화 후 신호 끊음 (평소 떨림 방지)
  warnServo.attach(SERVO_PIN);
  warnServo.write(90);
  delay(500);
  warnServo.detach();

  stop();

  // 시작 시 영점 실측
  delay(500);
  long sum = 0;
  for (int i = 0; i < 200; i++) {
    sum += analogRead(CURRENT_PIN);
    delay(2);
  }
  zeroRaw = sum / 200.0;
}

void loop() {
  // (1) 블루투스 수신
  while (bluetooth.available() > 0) {
    byte b = bluetooth.read();
    processCommandByte(b);
  }

  // (2) 워치독 - 하트비트 끊기면 자동 정지
  if (millis() - lastHeartbeat > WATCHDOG_TIMEOUT) {
    if (commAlive) {
      stop();
      commAlive = false;
    }
  }

  // (3) 경고 모드일 때만 서보 스윙 (비블로킹)
  if (warningMode) {
    if (millis() - lastServoMove >= SERVO_INTERVAL) {
      lastServoMove = millis();
      servoSide = !servoSide;
      warnServo.write(servoSide ? 45 : 135);
    }
  }

  // (4) 100ms마다 센서 패킷 송신
  unsigned long now = millis();
  if (now - lastSend >= SEND_INTERVAL) {
    lastSend = now;

    long dist = readDistance();
    sendPacket(CMD_DISTANCE, distanceToByte(dist));

    byte cur = readCurrentDiff();
    sendPacket(CMD_CURRENT, cur);
  }
}

// ===== 전류 측정 (동적 영점 보정) =====
byte readCurrentDiff() {
  long sum = 0;
  for (int i = 0; i < 20; i++) sum += analogRead(CURRENT_PIN);
  int raw = sum / 20;

  // 모터 정지 중이면 영점 자동 갱신 (지수 이동평균)
  if (!motorRunning) {
    zeroRaw = zeroRaw * 0.95 + raw * 0.05;
  }

  float diff = raw - zeroRaw;
  if (diff < 0)   diff = 0;
  if (diff > 255) diff = 255;
  return (byte)diff;
}

byte distanceToByte(long dist) {
  if (dist <= 0)  return 0;
  if (dist > 255) return 255;
  return (byte)dist;
}

// ===== 패킷 송신 =====
void sendPacket(byte cmd, byte data) {
  byte len = 0x01;
  byte chk = len ^ cmd ^ data;
  bluetooth.write(STX);
  bluetooth.write(len);
  bluetooth.write(cmd);
  bluetooth.write(data);
  bluetooth.write(chk);
  bluetooth.write(ETX);
}

// ===== 명령 패킷 조립기 =====
void processCommandByte(byte b) {
  if (b == STX) {
    rxBuf[0] = b;
    rxCount = 1;
    receiving = true;
    return;
  }
  if (!receiving) return;

  rxBuf[rxCount] = b;
  rxCount++;

  if (rxCount == 5) {
    receiving = false;
    byte len = rxBuf[1];
    byte cmd = rxBuf[2];
    byte chk = rxBuf[3];
    byte etx = rxBuf[4];
    if (etx == ETX && chk == (byte)(len ^ cmd)) {
      handlePacket(cmd);
    }
  }
}

void handlePacket(byte cmd) {
  lastHeartbeat = millis();
  commAlive = true;

  if (cmd == CMD_HEARTBEAT) return;

  switch (cmd) {
    case CMD_FORWARD:  endWarning(); forward();  break;
    case CMD_BACKWARD: endWarning(); backward(); break;
    case CMD_LEFT:     endWarning(); left();     break;
    case CMD_RIGHT:    endWarning(); right();    break;
    case CMD_STOP:     stop();     break;

    case CMD_WARN_ON:                   // 경고 시작
      stop();
      warnServo.attach(SERVO_PIN);
      warningMode = true;
      break;

    case CMD_WARN_OFF:                  // 경고 해제
      endWarning();
      break;
  }
}

// ===== 경고 종료 (서보 정면 복귀 후 신호 끊기) =====
void endWarning() {
  if (warningMode) {
    warningMode = false;
    warnServo.write(90);
    delay(300);
    warnServo.detach();
  }
}

// ---- 초음파 ----
long readDistance() {
  digitalWrite(TRIG, LOW);
  delayMicroseconds(2);
  digitalWrite(TRIG, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG, LOW);
  long duration = pulseIn(ECHO, HIGH, 30000);
  if (duration == 0) return 0;
  return duration / 58;
}

// ---- 구동 함수 ----
void forward() {
  analogWrite(ENR, SPEED); analogWrite(ENL, SPEED);
  digitalWrite(IN1, HIGH); digitalWrite(IN2, LOW);
  digitalWrite(IN3, HIGH); digitalWrite(IN4, LOW);
  motorRunning = true;
}
void backward() {
  analogWrite(ENR, SPEED); analogWrite(ENL, SPEED);
  digitalWrite(IN1, LOW); digitalWrite(IN2, HIGH);
  digitalWrite(IN3, LOW); digitalWrite(IN4, HIGH);
  motorRunning = true;
}
void left() {
  analogWrite(ENR, SPEED); analogWrite(ENL, SPEED);
  digitalWrite(IN1, HIGH); digitalWrite(IN2, LOW);
  digitalWrite(IN3, LOW);  digitalWrite(IN4, HIGH);
  motorRunning = true;
}
void right() {
  analogWrite(ENR, SPEED); analogWrite(ENL, SPEED);
  digitalWrite(IN1, LOW);  digitalWrite(IN2, HIGH);
  digitalWrite(IN3, HIGH); digitalWrite(IN4, LOW);
  motorRunning = true;
}
void stop() {
  analogWrite(ENR, 0); analogWrite(ENL, 0);
  motorRunning = false;
}