#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ButtonAttribute))]
public class ButtonDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var buttonAttribute = attribute as ButtonAttribute;
        if (buttonAttribute == null)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        var buttonLabel = string.IsNullOrWhiteSpace(buttonAttribute.label)
            ? ObjectNames.NicifyVariableName(buttonAttribute.methodName)
            : buttonAttribute.label;

        if (!GUI.Button(position, buttonLabel)) return;

        var targets = property.serializedObject.targetObjects;
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target == null) continue;

            InvokeMethod(target, buttonAttribute.methodName);
            EditorUtility.SetDirty(target);
        }

        if (property.propertyType == SerializedPropertyType.Boolean)
        {
            property.boolValue = false;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void InvokeMethod(UnityEngine.Object target, string methodName)
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName)) return;

        var type = target.GetType();
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (method == null)
        {
            Debug.LogWarning($"Button: method '{methodName}' not found on {type.Name}.", target);
            return;
        }

        try
        {
            method.Invoke(target, null);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, target);
        }
    }
}
#endif
