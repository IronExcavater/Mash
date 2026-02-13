#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MinMaxIntAttribute))]
public class MinMaxIntDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var minProp = property.FindPropertyRelative("min");
        var maxProp = property.FindPropertyRelative("max");
        if (minProp == null || maxProp == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var range = (MinMaxIntAttribute)attribute;
        var minValue = minProp.floatValue;
        var maxValue = maxProp.floatValue;

        var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
        var minRect = new Rect(labelRect.xMax, position.y, 48f, position.height);
        var sliderRect = new Rect(minRect.xMax + 4f, position.y, position.width - EditorGUIUtility.labelWidth - 112f, position.height);
        var maxRect = new Rect(sliderRect.xMax + 4f, position.y, 48f, position.height);

        EditorGUI.LabelField(labelRect, label);
        minValue = EditorGUI.FloatField(minRect, minValue);
        EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, range.minLimit, range.maxLimit);
        maxValue = EditorGUI.FloatField(maxRect, maxValue);

        minValue = Mathf.Clamp(minValue, range.minLimit, range.maxLimit);
        maxValue = Mathf.Clamp(maxValue, range.minLimit, range.maxLimit);
        if (maxValue < minValue) maxValue = minValue;

        minProp.floatValue = minValue;
        maxProp.floatValue = maxValue;
    }
}
#endif
