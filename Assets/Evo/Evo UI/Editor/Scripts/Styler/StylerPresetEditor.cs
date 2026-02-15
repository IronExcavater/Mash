using System.IO;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(StylerPreset))]
    public class StylerPresetEditor : Editor
    {
        StylerPreset spTarget;

        // Properties
        SerializedProperty audioItems;
        SerializedProperty colorItems;
        SerializedProperty fontItems;
        SerializedProperty updateMode;

        // Cache
        bool isDefaultPreset;
        bool isFallbackPreset;

        void OnEnable()
        {
            spTarget = (StylerPreset)target;

            audioItems = serializedObject.FindProperty("audioItems");
            colorItems = serializedObject.FindProperty("colorItems");
            fontItems = serializedObject.FindProperty("fontItems");
            updateMode = serializedObject.FindProperty("updateMode");

            // Register this editor for hover repaints
            EvoEditorGUI.RegisterEditor(this);

            // Check default status once when enabled
            CheckDefaultStatus();
            CheckFallbackStatus();

            string currentPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(currentPath);
            isFallbackPreset = !string.IsNullOrEmpty(resourcePath) &&  resourcePath.Replace('\\', '/') == Constants.STYLER_FALLBACK_PATH;
        }

        void OnDisable()
        {
            // Unregister from hover repaints
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
            EvoEditorGUI.BeginCenteredInspector(true);

            DrawAudioItems();
            DrawColorItems();
            DrawFontItems();
            DrawSettings();
            if (!isFallbackPreset) { DrawSetDefault(); }

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawAudioItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.audioFoldout, "Audio", EvoEditorGUI.GetIcon("UI_Audio")))
            {
                EvoEditorGUI.BeginContainer();
                DrawItemList(audioItems, Styler.ItemType.Audio);
                GUILayout.Space(2);
                if (EvoEditorGUI.DrawButton("New Audio", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    audioItems.arraySize++;
                    var newItem = audioItems.GetArrayElementAtIndex(audioItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = "New Audio";
                    newItem.FindPropertyRelative("audioAsset").objectReferenceValue = null;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawColorItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.colorFoldout, "Color", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                DrawItemList(colorItems, Styler.ItemType.Color);
                GUILayout.Space(2);
                if (EvoEditorGUI.DrawButton("New Color", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    colorItems.arraySize++;
                    var newItem = colorItems.GetArrayElementAtIndex(colorItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = $"Color {colorItems.arraySize}";
                    newItem.FindPropertyRelative("colorValue").colorValue = Color.white;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawFontItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.fontFoldout, "Font", EvoEditorGUI.GetIcon("UI_Text")))
            {
                EvoEditorGUI.BeginContainer();
                DrawItemList(fontItems, Styler.ItemType.Font);
                GUILayout.Space(2);
                if (EvoEditorGUI.DrawButton("New Font", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    fontItems.arraySize++;
                    var newItem = fontItems.GetArrayElementAtIndex(fontItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = "New Font";
                    newItem.FindPropertyRelative("fontAsset").objectReferenceValue = null;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(updateMode, "Update Mode", "Default update mode for all StylerObjects using this preset.", false, false);
                    EvoEditorGUI.BeginContainer(4);
                    string description = null;
                    if (updateMode.enumValueIndex == 0) { description = "Styler objects are updated in the editor and on every change at runtime."; }
                    else if (updateMode.enumValueIndex == 1) { description = "Styler objects are always updated in the editor and whenever the object is enabled at runtime."; }
                    GUILayout.Space(2);
                    EvoEditorGUI.DrawInfoBox(description);
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawItemList(SerializedProperty listProperty, Styler.ItemType itemType)
        {
            for (int i = 0; i < listProperty.arraySize; i++)
            {
                SerializedProperty item = listProperty.GetArrayElementAtIndex(i);
                DrawListItem(item, i, itemType, () =>
                {
                    listProperty.DeleteArrayElementAtIndex(i);
                    EditorUtility.SetDirty(spTarget);
                });
            }
        }

        void DrawListItem(SerializedProperty item, int index, Styler.ItemType itemType, System.Action deleteCallback)
        {
            SerializedProperty itemID = item.FindPropertyRelative("itemID");
            EvoEditorGUI.BeginVerticalBackground(true);

            GUILayout.BeginHorizontal();
            {
                string displayName = string.IsNullOrEmpty(itemID.stringValue) ? $"Item {index}" : itemID.stringValue;

                // Draw color preview box for color items
                if (itemType == Styler.ItemType.Color)
                {
                    SerializedProperty colorValue = item.FindPropertyRelative("colorValue");

                    Rect colorRect = GUILayoutUtility.GetRect(3, 9.5f, GUILayout.ExpandWidth(false));
                    colorRect.x += 8;
                    colorRect.y += 7;
                    EditorGUI.DrawRect(colorRect, colorValue.colorValue);

                    GUILayout.Space(-3);
                    displayName = $"   {displayName}";
                }

                if (EvoEditorGUI.DrawButton(displayName, item.isExpanded ? "Minimize" : "Expand", height: 24, normalColor: Color.clear, iconSize: 8,
                   textAlignment: TextAnchor.MiddleLeft, iconAlignment: EvoEditorGUI.ButtonAlignment.Right))
                {
                    item.isExpanded = !item.isExpanded;
                }

                if (EvoEditorGUI.DrawButton(null, "Delete", "Delete item", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                {
                    string itmName = string.IsNullOrEmpty(itemID.stringValue) ? $"Item {index}" : itemID.stringValue;
                    if (EditorUtility.DisplayDialog("Delete Item",
                        $"Are you sure you want to delete '{itmName}'?", "Delete", "Cancel"))
                    {
                        deleteCallback?.Invoke();
                        return;
                    }
                }
            }
            GUILayout.EndHorizontal();

            // Only draw the content if expanded
            if (item.isExpanded)
            {
                EvoEditorGUI.BeginContainer(3);
                {
                    EvoEditorGUI.DrawProperty(itemID, "ID", "Unique identifier for this item.");

                    if (itemType == Styler.ItemType.Audio)
                    {
                        SerializedProperty audioAsset = item.FindPropertyRelative("audioAsset");
                        EvoEditorGUI.DrawProperty(audioAsset, "Audio Clip", "The audio clip for this item.", false);
                    }

                    else if (itemType == Styler.ItemType.Color)
                    {
                        SerializedProperty colorValue = item.FindPropertyRelative("colorValue");
                        EvoEditorGUI.DrawProperty(colorValue, "Color", "The color value for this item.", false);
                    }

                    else if (itemType == Styler.ItemType.Font)
                    {
                        SerializedProperty fontAsset = item.FindPropertyRelative("fontAsset");
                        EvoEditorGUI.DrawProperty(fontAsset, "Font Asset", "The font asset for this item.", false);
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground(true);
        }

        void DrawSetDefault()
        {
            GUI.enabled = !isDefaultPreset;
            string btnText = isDefaultPreset ? "Currently Default" : "Set as Default Preset";
            if (EvoEditorGUI.DrawButton(btnText, isDefaultPreset ? "UI_DefaultStylerCheck" : null, 
                "Sets this preset as the global default. Preset must be in a Resources folder.", 
                height: 28, iconSize: 11, revertBackgroundColor: isDefaultPreset))
            {
                SetAsDefault();
            }
            GUI.enabled = true;
        }

        void SetAsDefault()
        {
            string assetPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(assetPath);

            // Validate Resources path
            if (resourcePath == null)
            {
                EditorUtility.DisplayDialog("Invalid Location", "To set this as the default preset, it must be located inside a 'Resources' folder.", "OK");
                return;
            }

            // Determine target path based on Styler.cs location
            string stylerScriptPath = FindStylerScriptPath();
            if (string.IsNullOrEmpty(stylerScriptPath))
            {
                EditorUtility.DisplayDialog("Error", "Could not locate 'Styler.cs' to determine config save location.", "OK");
                return;
            }

            int scriptsIndex = stylerScriptPath.LastIndexOf("/Scripts/");

            // Find "Scripts" and strip it to get the root "Evo UI" folder
            // Path: .../Evo UI/Scripts/Styler.cs
            string evoUiRoot;
            if (scriptsIndex != -1)
            {
                // Take everything before "/Scripts/"
                evoUiRoot = stylerScriptPath[..scriptsIndex];
            }
            else
            {
                // Fallback: If not in a "Scripts" folder, assume Styler.cs is in the root or deeper custom structure.
                // We'll just go up one level from the file to be safe.
                evoUiRoot = Path.GetDirectoryName(stylerScriptPath);
            }

            // Construct Resources path
            string resourcesDir = Path.Combine(evoUiRoot, "Resources");
            if (!Directory.Exists(resourcesDir)) { Directory.CreateDirectory(resourcesDir); }

            // Construct Config path using the Constant constant: "Styler Presets/Config"
            // This ensures we save it exactly where Styler.cs looks for it
            string fullPath = Path.Combine(resourcesDir, Constants.STYLER_CONFIG_PATH + ".txt");

            // Normalize path separators for Unity
            fullPath = fullPath.Replace('\\', '/');

            // Ensure subdirectories exist (e.g. "Styler Presets")
            string configDir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(configDir)) { Directory.CreateDirectory(configDir); }

            // Write Config
            try
            {
                File.WriteAllText(fullPath, resourcePath);
                AssetDatabase.Refresh();
                CheckDefaultStatus();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Styler] Failed to save config file: {e.Message}");
            }
        }

        void CheckDefaultStatus()
        {
            TextAsset config = Resources.Load<TextAsset>(Constants.STYLER_CONFIG_PATH);
            if (config == null) 
            { 
                isDefaultPreset = false;
                return;
            }

            string path = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(path);

            // Trim to handle potential whitespace or line endings in the text file
            isDefaultPreset = config.text.Trim() == resourcePath;
        }

        void CheckFallbackStatus()
        {
            string currentPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(currentPath);
            isFallbackPreset = !string.IsNullOrEmpty(resourcePath) && resourcePath.Replace('\\', '/') == Constants.STYLER_FALLBACK_PATH;
        }

        bool IsCurrentPresetDefault()
        {
            TextAsset config = Resources.Load<TextAsset>(Constants.STYLER_CONFIG_PATH);
            if (config == null) { return false; }

            string path = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(path);

            // Trim to handle potential whitespace or line endings in the text file
            return config.text.Trim() == resourcePath;
        }

        string GetResourcePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            // We need the path relative to Resources for the config file content
            int resourcesIndex = assetPath.LastIndexOf("/Resources/");
            if (resourcesIndex == -1) { return null; }

            string relativePath = assetPath[(resourcesIndex + 11)..]; // Length of "/Resources/"
            int extensionIndex = relativePath.LastIndexOf(".");
            if (extensionIndex != -1) { relativePath = relativePath[..extensionIndex]; }
            return relativePath;
        }

        string FindStylerScriptPath()
        {
            string[] guids = AssetDatabase.FindAssets("Styler t:Script");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "Styler.cs") { return path; }
            }
            return null;
        }
    }
}