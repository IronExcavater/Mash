using UnityEngine;

public sealed class MinMaxIntAttribute : PropertyAttribute
{
    public readonly float minLimit;
    public readonly float maxLimit;

    public MinMaxIntAttribute(float minLimit, float maxLimit)
    {
        this.minLimit = minLimit;
        this.maxLimit = maxLimit;
    }
}
