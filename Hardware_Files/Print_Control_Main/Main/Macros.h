#include "Arduino.h"

#define NumOFMotors 2

#define StepsPerRev 800 // Microstepping 1/4 Step

#define stmp1_DirectionPin 52
#define stmp1_StepPin 53

#define stmp2_DirectionPin 50
#define stmp2_StepPin 51

#define X_Left_Switch 40
#define X_Right_Switch 46
#define Y_Bot_Switch 38
#define Y_Top_Switch 44

#define X_Step_High PORTB |= 0b00000001;
#define X_Step_Low PORTB &= ~0b00000001;
#define X_MovePos PORTB |= 0b00000010;
#define X_MoveNeg PORTB &= ~0b00000010;

#define Y_Step_High PORTB |= 0b00000100;
#define Y_Step_Low PORTB &= ~0b00000100;
#define Y_MovePos PORTB |= 0b00001000;
#define Y_MoveNeg PORTB &= ~0b00001000;

#define Enable_Timer TIMSK1 |= (1 << OCIE1A);
#define Disable_Timer TIMSK1 &= ~(1 << OCIE1A);

#define TriggeredX ((PORTE & ~(1 << 5)) == 0 || (PORTD & ~(1 << 2)) == 0)
#define TriggeredY ((PORTD & ~(0b1001)) == 0)