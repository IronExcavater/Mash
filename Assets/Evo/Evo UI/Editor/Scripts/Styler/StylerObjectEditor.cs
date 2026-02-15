using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(StylerObject))]
    public class StylerObjectEditor : Editor
    {
        // Target
        StylerObject soTarget;

        // Properties
        SerializedProperty preset;
        SerializedProperty targetImage;
        SerializedProperty targetText;
        SerializedProperty objectType;
        SerializedProperty colorID;
        SerializedProperty fontID;
        SerializedProperty useCustomColor;
        SerializedProperty overrideAlpha;
        SerializedProperty alphaOverride;

        // Interaction properties
        SerializedProperty enableInteraction;
        SerializedProperty interactableObject;
        SerializedProperty disabledColor;
        SerializedProperty normalColor;
        SerializedProperty highlightedColor;
        SerializedProperty pressedColor;
        SerializedProperty selectedColor;

        void OnEnable()
        {
            soTarget = (StylerObject)target;

            preset = serializedObject.FindProperty("preset");
            targetImage = serializedObject.FindProperty("targetImage");
            targetText = serializedObject.FindProperty("targetText");
            objectType = serializedObject.FindProperty("objectType");
            colorID = serializedObject.FindProperty("colorID");
            fontID = serializedObject.FindProperty("fontID");
            useCustomColor = serializedObject.FindProperty("useCustomColor");
            overrideAlpha = serializedObject.FindProperty("overrideAlpha");
            alphaOverride = serializedObject.FindProperty("alphaOverride");

            enableInteraction = serializedObject.FindProperty("enableInteraction");
            interactableObject = serializedObject.FindProperty("interactableObject");
            disabledColor = serializedObject.FindProperty("disabledColor");
            normalColor = serializedObject.FindProperty("normalColor");
            highlightedColor = serializedObject.FindProperty("highlightedColor");
            pressedColor = serializedObject.FindProperty("pressedColor");
            selectedColor = serializedObject.FindProperty("selectedColor");

            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable()
        {
            EvoEditorGUI.UnregisterEditor(this);
        }

        public override void OnInspectorGUI()
        {
            if (!EvoEditorSettings.IsCustomEditorEnabled(Constants.CUSTOM_EDITOR_ID)) { DrawDefaultInspector(); }
            else
            {
                DrawCustomGUI();
                EvoEditorGUI.HandleInspectorGUI();
            }
        }

        void DrawCustomGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawReferences();
            DrawSettings();
            DrawInteraction();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawReferences()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref soTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(preset, "Styler Preset", "The preset containing style definitions.", true, true, true);
                    if (objectType.enumValueIndex == 0) { EvoEditorGUI.DrawProperty(targetImage, "Target Image", "Image component to style.", false, true, true); }
                    else if (objectType.enumValueIndex == 1) { EvoEditorGUI.DrawProperty(targetText, "Target Text", "TextMeshPro component to style.", false, true, true); }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref soTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    // Object Type
                    EvoEditorGUI.DrawProperty(objectType, "Object Type", "Type of UI object to style.", true, true, true);

                    // Draw fields
                    GUI.enabled = preset.objectReferenceValue;
                    if (objectType.enumValueIndex == 1) { StylerEditor.DrawItemDropdown(preset, fontID, Styler.ItemType.Font, "Font ID", true, true, true); }
                    GUI.enabled = !useCustomColor.boolValue && !enableInteraction.boolValue;
                    StylerEditor.DrawItemDropdown(preset, colorID, Styler.ItemType.Color, "Color ID", true, true, true);

                    // Override Alpha section
                    GUI.enabled = !useCustomColor.boolValue;
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(overrideAlpha, "Override Alpha", "Override the alpha channel with a custom value.", false, true, true, bypassNormalBackground: true);
                    if (overrideAlpha.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(alphaOverride, "Alpha", "Alpha value to apply (0 = transparent, 1 = opaque).", false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
                    GUI.enabled = true;

                    EvoEditorGUI.DrawToggle(useCustomColor, "Use Custom Color", "Set a current color instead of getting from the preset.", false, true, true);
                    if (!enableInteraction.boolValue && !preset.objectReferenceValue && !useCustomColor.boolValue)
                    {
                        GUILayout.Space(4);
                        EvoEditorGUI.DrawInfoBox("No preset attached. Please assign a valid Styler Preset to use the Styler system.", null, true);
                    }
                    else if (enableInteraction.boolValue && interactableObject.objectReferenceValue)
                    {
                        GUILayout.Space(4);
                        EvoEditorGUI.DrawInfoBox("Interaction is enabled; color will be handled by the interaction system.", null, true);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawInteraction()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref soTarget.interactionFoldout, "Interaction", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(enableInteraction, "Enable Interaction", "Enable state-based color animation.", false, true, true, bypassNormalBackground: true);
                    if (!enableInteraction.boolValue) { EvoEditorGUI.EndVerticalBackground(); }
                    else
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(interactableObject, "Target Object", null, false, true);
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndVerticalBackground(true);
                        if (soTarget.interactableObject)
                        {
                            EvoEditorGUI.BeginVerticalBackground(true);
                            EvoEditorGUI.BeginContainer("Color When", 3);
                            DrawInteractionColors();
                            EvoEditorGUI.EndContainer();
                            EvoEditorGUI.EndVerticalBackground();
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawInteractionColors()
        {
            // Array of ColorMapping properties matching InteractionState enum order
            SerializedProperty[] colorMappings = new[]
            {
                disabledColor,
                normalColor,
                highlightedColor,
                pressedColor,
                selectedColor
            };

            string[] stateNames = Interactive.GetInteractionStateIDs();

            // Draw each state's color mapping
            for (int i = 0; i < stateNames.Length; i++)
            {
                SerializedProperty mapping = colorMappings[i];
                SerializedProperty color = mapping.FindPropertyRelative("color");
                SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                bool isLastItem = i >= stateNames.Length - 1;

                // If preset is assigned, use dropdown
                if (preset.objectReferenceValue != null && !useCustomColor.boolValue)
                {
                    StylerEditor.DrawItemDropdown(preset, stylerID, Styler.ItemType.Color, stateNames[i], !isLastItem);
                }
                else
                {
                    // Fallback to custom color field when preset is missing
                    EvoEditorGUI.DrawProperty(color, stateNames[i], null, !isLastItem, true);
                }
            }
        }
    }
}