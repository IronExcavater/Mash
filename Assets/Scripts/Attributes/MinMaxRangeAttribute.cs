using UnityEngine;

public sealed class MinMaxRangeAttribute : PropertyAttribute
{
    public readonly float minLimit;
    public readonly float maxLimit;

    public MinMaxRangeAttribute(float minLimit, float maxLimit)
    {
        this.minLimit = minLimit;
        this.maxLimit = maxLimit;
    }
}
