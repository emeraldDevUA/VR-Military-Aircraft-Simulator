using UnityEngine;
using System.Collections;
using System.Collections.Generic;


enum Pylon{
    Central,
    FarRight,
    FarLeft
};

enum WingRole{
    Hull,
    RightWing,
    LeftWing,
    Tail
}


class Airplane{

    // Engine
    Engine m_engine;

    // Wings
    Dictionary<WingRole, Wing> wings = new Dictionary<WingRole, Wing>();

    // Controls
    Vector3 controls = new Vector3(0, 0, 0);
    // Gun
    Gun m_gun;
    // Attached missiles
    Dictionary<Pylon, Missile> missiles = new Dictionary<Pylon, Missile>();
    // flaps -- state?
    bool enableFlaps = false; // has to be an enum or a struct, will change later
    // wheels -- state
    bool chassisDown = true;
    // airbrake -- state
    bool airbrakeEnabled = false;

    // Probably rigid body too

    public Airplane(Engine engine, Gun gun){
        m_engine = engine;
        m_gun = gun;
    }


    public void computeForce(Rigidbody rigidBody)
    {

    }



    public void computeTorque(Rigidbody rigidBody)
    {

    }


}