#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(TagSelectorAttribute))]
public class TagSelectorAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

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

        EditorGUI.BeginProperty(position, label, property);
        var popupRect = EditorGUI.PrefixLabel(position, label);
        var nextIndex = EditorGUI.Popup(popupRect, currentIndex, tags);
        if (nextIndex >= 0 && nextIndex < tags.Length) property.stringValue = tags[nextIndex];
        EditorGUI.EndProperty();
    }
}
#endif
