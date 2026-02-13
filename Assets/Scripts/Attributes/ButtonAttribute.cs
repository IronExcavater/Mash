using UnityEngine;

public class ButtonAttribute : PropertyAttribute
{
    public readonly string methodName;
    public readonly string label;

    public ButtonAttribute(string methodName, string label = null)
    {
        this.methodName = methodName;
        this.label = label;
    }
}