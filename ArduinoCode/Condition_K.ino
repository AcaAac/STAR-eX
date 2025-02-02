#include <Servo.h>

const int numServos = 5;
const int servoPins[numServos] = {11, 3, 6, 5, 10};
Servo servos[numServos];

const int initialAngles[numServos] = {80, 80, 80, 100, 100}; // Initial angles for each servo
int currentAngles[numServos];

void setup() {
  Serial.begin(256000);

  for (int i = 0; i < numServos; i++) {
    servos[i].attach(servoPins[i]);
    servos[i].write(initialAngles[i]); // Set initial position
    currentAngles[i] = initialAngles[i];
  }
}

void loop() {
  if (Serial.available() > 0) {
    String receivedString = Serial.readStringUntil('\n');
    if (receivedString.length() > 0) {
      int commaIndex = receivedString.indexOf(',');
      int servoIndex = receivedString.substring(0, commaIndex).toInt();
      int angleChange = receivedString.substring(commaIndex + 1).toInt();

      if (servoIndex >= 0 && servoIndex < numServos) {
        moveServo(servoIndex, angleChange);
        Serial.print("Servo ");
        Serial.print(servoIndex);
        Serial.print(" moved to ");
        Serial.println(currentAngles[servoIndex]);
      }
    }
  }
}

void moveServo(int index, int angleChange) {
  int targetAngle = initialAngles[index] + angleChange;
  servos[index].write(targetAngle);
  delay(200); // Shorten the hold position duration
  servos[index].write(initialAngles[index]); // Return to initial position
  currentAngles[index] = initialAngles[index]; // Reset current angle
}
