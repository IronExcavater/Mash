#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(TagSelectorAttribute))]
public class TagSelectorAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var tags = InternalEditorUtility.tags;
        if (tags == null || tags.Length == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var currentIndex = Array.IndexOf(tags, property.stringValue);
        if (currentIndex < 0) currentIndex = 0;

        var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
        var popupRect = new Rect(labelRect.xMax, position.y, position.width - labelRect.width, position.height);

        EditorGUI.LabelField(labelRect, label);

        var popupStyle = new GUIStyle(EditorStyles.popup);
        popupStyle.fontStyle = FontStyle.Bold;
        popupStyle.normal.textColor = Color.white;
        popupStyle.focused.textColor = Color.white;
        popupStyle.hover.textColor = Color.white;
        popupStyle.active.textColor = Color.white;

        var nextIndex = EditorGUI.Popup(popupRect, currentIndex, tags, popupStyle);
        if (nextIndex >= 0 && nextIndex < tags.Length) property.stringValue = tags[nextIndex];
    }
}
#endif
