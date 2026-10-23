using UnityEngine;

enum OrbType{
    UniDirectional,
    MultiDirectional
}

class HeatOrb{
    float m_intensity{get; set;} = 0;

    Vector3 m_position{get; set;} = new Vector3(0,0,0);
    Vector3 m_direction{get; set;} = new Vector3(0,0,0);

    OrbType m_type{get; set;} = OrbType.UniDirectional;


    public HeatOrb(Vector3 position, Vector3 direction, OrbType type, float intensity){
        m_position = position;
        m_direction = direction;
        m_type = type;
        m_intensity = intensity;
    }


    public float GetHeatValue(InfraredHomingSystem homingSystem)
    {
        Vector3 offset = m_position - homingSystem.m_position;

        float distanceSqr = offset.sqrMagnitude;
        float distanceFactor = 1.0f / Mathf.Max(distanceSqr, 0.01f);
        float directionalFactor = 1.0f;

        if (m_type == OrbType.UniDirectional)
        {
            Vector3 directionToOrb = offset.normalized;
            Vector3 heatDirection = m_direction.normalized;

            float dotProduct = Vector3.Dot(heatDirection, directionToOrb);

            directionalFactor = Mathf.Max(0.0f, dotProduct);
        }

        return m_intensity * directionalFactor * distanceFactor;
    }


}




class RadarOrb{
    float m_area{get; set;} = 0;

    Vector3 m_position{get; set;} = new Vector3(0,0,0);
   // Vector3 velocity{get; set;} = new Vector3(0,0,0);

    public RadarOrb(Vector3 position, float area){
        m_position = position;
        m_area = area;
    }

    // random noise will depend in its magnitude on area and distance
    public Vector3 getNoisyData(){
        return m_position + UnityEngine.Random.insideUnitSphere;
    }
}