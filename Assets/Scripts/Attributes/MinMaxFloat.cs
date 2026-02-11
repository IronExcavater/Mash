using System;
using UnityEngine;

[Serializable]
public struct MinMaxFloat
{
    public float min;
    public float max;

    public MinMaxFloat(float min, float max)
    {
        this.min = min;
        this.max = max;
    }

    public float Lerp(float t)
    {
        return Mathf.Lerp(min, max, Mathf.Clamp01(t));
    }

    public void ClampAndOrder(float hardMin, float hardMax)
    {
        min = Mathf.Clamp(min, hardMin, hardMax);
        max = Mathf.Clamp(max, hardMin, hardMax);
        if (max < min) max = min;
    }
}
