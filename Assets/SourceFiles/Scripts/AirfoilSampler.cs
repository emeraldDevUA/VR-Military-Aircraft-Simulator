using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public struct Point{
    double m_x;
    double m_y;

    public Point(double x, double y){
        m_x = x;
        m_y = y;
    }
};

class Airfoil{


    List<Dictionary<string, object>> m_data{get; set;}

    public Airfoil(List<Dictionary<string, object>> data){
        m_data = data;
    }


    Point sample(double cL){

      double stored_value = -999;
      foreach (var row in m_data)
        {
            if((double)row["alpha"] < cL){
                stored_value = (double)row["alpha"];
            }else if((double)row["alpha"] >= cL){
                // do linear interpolation
            }
            // Debug.Log($"Alpha: {row["alpha"]}, CL: {row["cl"]}, CD: {row["cd"]}");
        }

        return new Point(0.0, 0.0);
    }

}