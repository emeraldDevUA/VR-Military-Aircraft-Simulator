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
}




class RadarOrb{
    float m_area{get; set;} = 0;

    Vector3 m_position{get; set;} = new Vector3(0,0,0);
   // Vector3 velocity{get; set;} = new Vector3(0,0,0);

    public RadarOrb(Vector3 position, float area){
        m_position = position;
        m_area = area;
    }


    public Vector3 getNoisyData(){
        return m_position + UnityEngine.Random.insideUnitSphere;
    }
}