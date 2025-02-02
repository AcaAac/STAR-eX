#include <Servo.h>

Servo thumbServo;
Servo indexServo;
Servo middleServo;
Servo ringServo;
Servo pinkyServo;

enum State {
  IDLE,
  DOWN,
  PRESSED,
  UP
};

State currentStateThumb = IDLE;
State currentStateIndex = IDLE;
State currentStateMiddle = IDLE;
State currentStateRing = IDLE;
State currentStatePinky = IDLE;

unsigned long stateStartTimeThumb = 0;
unsigned long stateStartTimeIndex = 0;
unsigned long stateStartTimeMiddle = 0;
unsigned long stateStartTimeRing = 0;
unsigned long stateStartTimePinky = 0;

const unsigned long transitionDelay = 100; // 100 ms

const int thumbPin = 11;
const int thumbDownPosition = 30;
const int thumbUpPosition = 90;
const float proportionalGainThumb = 0.1;

const int indexServoPin = 3;
const int indexDownPosition = 45;
const int indexUpPosition = 90;
const float proportionalGainIndex = 0.1;

const int middleServoPin = 6;
const int middleDownPosition = 30;
const int middleUpPosition = 90;
const float proportionalGainMiddle = 0.1;

const int ringServoPin = 5;
const int ringDownPosition = 150;
const int ringUpPosition = 90;
const float proportionalGainRing = 0.1;

const int pinkyServoPin = 10;
const int pinkyDownPosition = 140;
const int pinkyUpPosition = 90;
const float proportionalGainPinky = 0.1;

void setup() {
  Serial.begin(256000);

  thumbServo.attach(thumbPin);
  thumbServo.write(thumbUpPosition);

  indexServo.attach(indexServoPin);
  indexServo.write(indexUpPosition);

  middleServo.attach(middleServoPin);
  middleServo.write(middleUpPosition);

  ringServo.attach(ringServoPin);
  ringServo.write(ringUpPosition);

  pinkyServo.attach(pinkyServoPin);
  pinkyServo.write(pinkyUpPosition);
}

void loop() {
  if (Serial.available() > 0) {
    String input = Serial.readStringUntil('\n');
    int fingerIndex = input.substring(0, input.indexOf(',')).toInt();
    int eventFlag = input.substring(input.indexOf(',') + 1).toInt();

    switch (fingerIndex) {
      case 0:
        handleState(eventFlag, currentStateThumb, stateStartTimeThumb, thumbServo, thumbUpPosition, thumbDownPosition, proportionalGainThumb);
        break;
      case 1:
        handleState(eventFlag, currentStateIndex, stateStartTimeIndex, indexServo, indexUpPosition, indexDownPosition, proportionalGainIndex);
        break;
      case 2:
        handleState(eventFlag, currentStateMiddle, stateStartTimeMiddle, middleServo, middleUpPosition, middleDownPosition, proportionalGainMiddle);
        break;
      case 3:
        handleState(eventFlag, currentStateRing, stateStartTimeRing, ringServo, ringUpPosition, ringDownPosition, proportionalGainRing);
        break;
      case 4:
        handleState(eventFlag, currentStatePinky, stateStartTimePinky, pinkyServo, pinkyUpPosition, pinkyDownPosition, proportionalGainPinky);
        break;
    }
  }

  updateState(currentStateThumb, stateStartTimeThumb, thumbServo, thumbUpPosition, thumbDownPosition);
  updateState(currentStateIndex, stateStartTimeIndex, indexServo, indexUpPosition, indexDownPosition);
  updateState(currentStateMiddle, stateStartTimeMiddle, middleServo, middleUpPosition, middleDownPosition);
  updateState(currentStateRing, stateStartTimeRing, ringServo, ringUpPosition, ringDownPosition);
  updateState(currentStatePinky, stateStartTimePinky, pinkyServo, pinkyUpPosition, pinkyDownPosition);
}

void handleState(int eventFlag, State& currentState, unsigned long& stateStartTime, Servo& servo, int upPosition, int downPosition, float proportionalGain) {
  switch (currentState) {
    case IDLE:
      if (eventFlag == 1) { // Key Pressed
        currentState = DOWN;
        stateStartTime = millis();
      }
      servo.write(upPosition);
      break;

    case DOWN:
      if (eventFlag == 1) { // Key Pressed
        // Stay in DOWN state
        float currentPosition = servo.read();
        float newPosition = currentPosition - proportionalGain * (currentPosition - downPosition);
        servo.write(newPosition);
      } else if (eventFlag == 0) { // Key Unpressed
        // Transition happens in loop based on time
      }
      break;

    case PRESSED:
      if (eventFlag == 1) { // Key Pressed
        // Stay in PRESSED state
      } else if (eventFlag == 0) { // Key Unpressed
        currentState = UP;
        stateStartTime = millis();
      }
      servo.write(downPosition);
      break;

    case UP:
      if (eventFlag == 0) { // Key Unpressed
        // Stay in UP state
        float currentPosition = servo.read();
        float newPosition = currentPosition + proportionalGain * (upPosition - currentPosition);
        servo.write(newPosition);
      } else if (eventFlag == 1) { // Key Pressed
        currentState = DOWN;
        stateStartTime = millis();
      }
      break;
  }
}

void updateState(State& currentState, unsigned long& stateStartTime, Servo& servo, int upPosition, int downPosition) {
  if ((currentState == DOWN || currentState == UP) && millis() - stateStartTime >= transitionDelay) {
    if (currentState == DOWN) {
      currentState = PRESSED;
      servo.write(downPosition);
    } else if (currentState == UP) {
      currentState = IDLE;
      servo.write(upPosition);
    }
    stateStartTime = millis();
  }
}
