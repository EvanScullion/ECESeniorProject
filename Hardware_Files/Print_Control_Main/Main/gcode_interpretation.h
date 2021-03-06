//Created by Nathan Page for Senior Project GCode interpretation on November 1, 2019
// #include <String>
#include "Stepper_Motor.h"
#include "Macros.h"

class gcode_interpretation
{

    public:
    gcode_interpretation();
    String parse_commands(int buffer_length);
    bool interpret_gcode(String command);
    bool rapid_positioning(String command);
    bool linear_interpolation(String command);
    bool circ_interpolation_cw(String command);
    bool circ_interpolation_ccw(String command);
    void drop_medium();
    void raise_medium();
    bool GetHomingx(){ return homingx; }
    bool GetHomingy(){ return homingy; }
    void SetHomingx(bool homing){ homingx = homing; }
    void SetHomingy(bool homing){ homingy = homing; }
    void Home();

    Stepper_Motor* GetMotors(){ return stpms; }

    private:
    volatile uint32_t stepRatio = StepsPerRev;
    volatile bool homingx = false;
    volatile bool homingy = false;
    Stepper_Motor stpms[NumOFMotors];

};