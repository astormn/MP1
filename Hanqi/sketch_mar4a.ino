#include "I2Cdev.h"
#include "MPU6050_6Axis_MotionApps20.h"
#include "Wire.h"

MPU6050 mpu;

bool dmpReady = false;
uint8_t devStatus;
uint16_t packetSize;
uint16_t fifoCount;
uint8_t fifoBuffer[64];

Quaternion q;

unsigned long lastSend = 0;
const int sendInterval = 10;   // 10 ms → 100 Hz

void setup() {

  Wire.begin();
  Wire.setClock(400000);

  Serial.begin(115200);

  mpu.initialize();

  devStatus = mpu.dmpInitialize();

  // calibration offsets
  mpu.setXGyroOffset(220);
  mpu.setYGyroOffset(76);
  mpu.setZGyroOffset(-85);
  mpu.setZAccelOffset(1788);

  if (devStatus == 0) {

    mpu.CalibrateAccel(6);
    mpu.CalibrateGyro(6);

    mpu.setDMPEnabled(true);
    dmpReady = true;

    packetSize = mpu.dmpGetFIFOPacketSize();

  } else {

    Serial.print("DMP Initialization failed (code ");
    Serial.print(devStatus);
    Serial.println(")");
  }
}

void loop() {

  if (!dmpReady) return;

  fifoCount = mpu.getFIFOCount();

  // 防止 FIFO overflow
  if (fifoCount >= 1024) {
    mpu.resetFIFO();
    return;
  }

  while (fifoCount >= packetSize) {

    mpu.getFIFOBytes(fifoBuffer, packetSize);
    fifoCount -= packetSize;

    // 控制发送频率
    if (millis() - lastSend < sendInterval) return;

    lastSend = millis();

    mpu.dmpGetQuaternion(&q, fifoBuffer);

    Serial.print("T:");
    Serial.print(lastSend);
    Serial.print(",");

    Serial.print(q.w, 6);
    Serial.print(",");
    Serial.print(q.x, 6);
    Serial.print(",");
    Serial.print(q.y, 6);
    Serial.print(",");
    Serial.println(q.z, 6);
  }
}