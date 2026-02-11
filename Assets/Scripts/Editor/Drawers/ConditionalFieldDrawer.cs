#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ConditionalFieldAttribute))]
public class ConditionalFieldDrawer : PropertyDrawer
{
    private const float HeaderTopPadding = 6f;
    private static readonly float HeaderHeight = HeaderTopPadding + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    private static readonly float HiddenHeight = -(EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property)) return HiddenHeight;
        var height = EditorGUI.GetPropertyHeight(property, label, true);
        var data = (ConditionalFieldAttribute)attribute;
        if (string.IsNullOrWhiteSpace(data.header)) return height;
        return HeaderHeight + height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property)) return;
        var data = (ConditionalFieldAttribute)attribute;

        if (!string.IsNullOrWhiteSpace(data.header))
        {
            var headerRect = new Rect(position.x, position.y + HeaderTopPadding, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(headerRect, data.header, EditorStyles.boldLabel);
            position = new Rect(position.x, position.y + HeaderHeight, position.width, position.height - HeaderHeight);
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    private bool ShouldShow(SerializedProperty property)
    {
        var data = (ConditionalFieldAttribute)attribute;
        var condition = FindSiblingProperty(property, data.fieldName);
        if (condition == null) return false;

        if (!data.useEnum)
        {
            if (condition.propertyType != SerializedPropertyType.Boolean) return false;
            return condition.boolValue == data.boolValue;
        }

        if (condition.propertyType == SerializedPropertyType.Enum) return condition.enumValueIndex == data.enumValue;
        if (condition.propertyType == SerializedPropertyType.Integer) return condition.intValue == data.enumValue;
        return false;
    }

    private static SerializedProperty FindSiblingProperty(SerializedProperty property, string fieldName)
    {
        var path = property.propertyPath;
        var split = path.LastIndexOf('.');
        if (split < 0) return property.serializedObject.FindProperty(fieldName);
        var parentPath = path.Substring(0, split + 1);
        return property.serializedObject.FindProperty(parentPath + fieldName);
    }
}
#endif
