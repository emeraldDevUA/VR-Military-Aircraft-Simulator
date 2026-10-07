using UnityEngine;
using System.Collections;
using System.Collections.Generic;

class Gun{
    Vector3 m_gunPoint;
    Vector3 m_gunDirection;

    int m_ammoQuantity;

    public Gun(Vector3 gunPoint, Vector3 gunDirection, int ammoQuantity){
        m_gunPoint = gunPoint;
        m_gunDirection = gunDirection;
        m_ammoQuantity = ammoQuantity;
    }


}


class Missile{
    // RigidBody



}