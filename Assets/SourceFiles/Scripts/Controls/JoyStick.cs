

class Joystick{
    public int num_axis{get; set;}
    public int num_hats{get; set;}
    public int num_buttons{get; set;}

    float aileron{get; set;}
    float elevator{get; set;}
    float rudder{get; set;}

    float throttle{get; set;}
    float trim{get; set;}


    public Joystick(){
        num_axis = 0;
        num_hats = 0;
        num_buttons = 0;

        aileron = 0f;
        elevator = 0f;
        rudder = 0f;

        throttle = 0f;
        trim = 0f;
    }

}