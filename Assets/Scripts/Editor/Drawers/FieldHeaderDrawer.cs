#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FieldHeaderAttribute))]
public class FieldHeaderDrawer : PropertyDrawer
{
    private const float TopPadding = 6f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!SafeShouldShow(property)) return -(EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        var line = EditorGUIUtility.singleLineHeight;
        if (!property.hasVisibleChildren || !property.isExpanded) return TopPadding + line;

        var total = TopPadding + line + EditorGUIUtility.standardVerticalSpacing;
        var it = property.Copy();
        var end = it.GetEndProperty();
        var enterChildren = true;

        while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
        {
            total += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false;
        }

        return total;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!SafeShouldShow(property)) return;
        var data = (FieldHeaderAttribute)attribute;
        var title = string.IsNullOrWhiteSpace(data.title) ? ObjectNames.NicifyVariableName(property.name) : data.title;

        var foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        var line = EditorGUIUtility.singleLineHeight;
        var headerRect = new Rect(position.x, position.y + TopPadding, position.width, line);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, title, true, foldoutStyle);

        if (!property.hasVisibleChildren || !property.isExpanded) return;

        EditorGUI.indentLevel++;
        var y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;

        var it = property.Copy();
        var end = it.GetEndProperty();
        var enterChildren = true;
        while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
        {
            var h = EditorGUI.GetPropertyHeight(it, true);
            var r = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(r, it, true);
            y += h + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false;
        }
        EditorGUI.indentLevel--;
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

