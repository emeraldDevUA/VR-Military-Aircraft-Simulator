using UnityEngine;


public class Target{

    public HeatOrb m_heatSignature;
    public RadarOrb m_radarSignature;

    private Rigidbody m_rigidBody;

    public Target(Rigidbody rigidBody, HeatOrb heatSignature, RadarOrb radarSignature){
        m_rigidBody = rigidBody;
        m_heatSignature = heatSignature;
        m_radarSignature = radarSignature;
    }


}
