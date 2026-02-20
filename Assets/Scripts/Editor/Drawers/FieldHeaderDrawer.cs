#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FieldHeaderAttribute))]
public class FieldHeaderDrawer : PropertyDrawer
{
    private const float TopPadding = 6f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!SafeShouldShow(property)) return 0f;
        return TopPadding + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing +
               EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!SafeShouldShow(property)) return;
        var data = (FieldHeaderAttribute)attribute;
        var title = string.IsNullOrWhiteSpace(data.title) ? ObjectNames.NicifyVariableName(property.name) : data.title;

        var headerRect = new Rect(position.x, position.y + TopPadding, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(headerRect, title, EditorStyles.boldLabel);

        var propertyY = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
        var propertyRect = new Rect(position.x, propertyY, position.width, EditorGUI.GetPropertyHeight(property, label, true));
        EditorGUI.PropertyField(propertyRect, property, label, true);
    }

    private bool SafeShouldShow(SerializedProperty property)
    {
        if (property == null) return false;
        try
        {
            return ShouldShow(property);
        }
        catch
        {
            return false;
        }
    }

    private bool ShouldShow(SerializedProperty property)
    {
        var data = (FieldHeaderAttribute)attribute;
        if (string.IsNullOrWhiteSpace(data.conditionField)) return true;

        var condition = FindSiblingProperty(property, data.conditionField);
        if (condition == null) return true;
        if (!data.useEnumCondition) return true;

        if (condition.propertyType == SerializedPropertyType.Enum)
            return condition.enumValueIndex == data.expectedEnumValue;
        if (condition.propertyType == SerializedPropertyType.Integer)
            return condition.intValue == data.expectedEnumValue;
        return true;
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

