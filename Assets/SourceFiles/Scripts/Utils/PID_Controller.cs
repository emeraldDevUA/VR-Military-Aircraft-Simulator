using UnityEngine;
using System.Collections.Generic;

public class PIDController
{
    public float Kp, Ki, Kd;
    public float OutputMin = -1f, OutputMax = 1f;

    private float integral;
    private float prevError;
    private bool first = true;

    public PIDController(float kp, float ki, float kd)
    {
        Kp = kp; Ki = ki; Kd = kd;
    }

    public float Calculate(float current, float target, float dt)
    {
        float error = target - current;
        float derivative = first ? 0f : (error - prevError) / dt;
        first = false;
        prevError = error;

        float unclamped = Kp * error + Ki * (integral + error * dt) + Kd * derivative;
        float output = Mathf.Clamp(unclamped, OutputMin, OutputMax);

        // Anti-windup: only integrate when not saturated
        if (Mathf.Approximately(unclamped, output))
            integral += error * dt;

        return output;
    }

    public void Reset() { integral = 0f; prevError = 0f; first = true; }
}
