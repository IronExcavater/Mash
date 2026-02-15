using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Tools
{
    using Button = UnityEngine.UIElements.Button;

    public class StylerPresetBrowser : EditorWindow, IEvoEditorGUIHandler
    {
        readonly List<StylerPreset> presets = new();
        StylerPreset selectedPreset;

        VisualElement detailsPanel;
        VisualElement headerContainer;
        VisualElement scrollShadow;
        ScrollView detailsScrollView;
        ToolbarMenu presetMenu;
        Texture2D shadowTexture;

        const int ACTION_ITEM_SIZE = 24;
        const float TOOLBAR_HEIGHT = 30f;
        const float WINDOW_MARGIN = 5f;
        const float ACTION_SPACING = 5f;

        [MenuItem("Tools/Evo UI/Open Styler Browser", false, 0)]
        public static void OpenWindow()
        {
            var window = GetWindow<StylerPresetBrowser>();
            window.titleContent = new GUIContent("Styler Browser", Resources.Load<Texture2D>("Editor Textures/Icon-UI_StylerBrowser"));
            window.minSize = new Vector2(300, 300);
            window.RefreshPresetList();
        }

        void OnEnable()
        {
            ConstructWindow();
            RefreshPresetList();
            Selection.selectionChanged += OnSelectionChange;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChange;
            if (shadowTexture != null) { DestroyImmediate(shadowTexture); }
        }

        void Update()
        {
            // Repaint on hover for hover effects
            if (mouseOverWindow == this) { Repaint(); }

            // Auto-refresh if the list is lost (e.g., after compilation or assembly reload)
            if (presets == null || presets.Count == 0) { RefreshPresetList(); }
        }

        void OnSelectionChange()
        {
            // Only rebuild UI if the selection is relevant
            if (Selection.activeGameObject != null || Selection.activeObject == null)
            {
                // Defer UI update to next frame to avoid layout errors during selection event
                rootVisualElement.schedule.Execute(UpdateDetailsPanel);
            }
        }

        void ConstructWindow()
        {
            rootVisualElement.Clear();

            // Toolbar
            var toolbar = new Toolbar();
            toolbar.style.height = TOOLBAR_HEIGHT;
            toolbar.style.marginRight = -1;
            toolbar.style.alignItems = Align.Center;

            // Spacer
            toolbar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            // Refresh Button
            toolbar.Add(CreateToolbarButton("Refresh", "Refresh", RefreshPresetList));

            // Preset Selector Dropdown
            presetMenu = new ToolbarMenu
            {
                text = "Selected Preset: <b>None</b> ",
                style =
                {
                    width = StyleKeyword.Auto,
                    minWidth = 120,
                    alignSelf = Align.Stretch,
                    marginRight = -1,
                    paddingLeft = 10,
                    paddingRight = 9,
                }
            };
            toolbar.Add(presetMenu);

            var arrowIcon = presetMenu.Q(className: "unity-toolbar-menu__arrow");
            if (arrowIcon != null) { arrowIcon.style.marginLeft = 6; }

            rootVisualElement.Add(toolbar);

            // Main Content Area
            detailsPanel = new VisualElement();
            detailsPanel.style.flexGrow = 1;

            // Fixed Header Area
            headerContainer = new VisualElement();
            headerContainer.style.marginLeft = headerContainer.style.marginRight = WINDOW_MARGIN;

            // Track geometry to position shadow correctly under header
            headerContainer.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (scrollShadow != null)
                {
                    scrollShadow.style.top = evt.newRect.y + evt.newRect.height;
                }
            });
            detailsPanel.Add(headerContainer);

            // Scrollable Content Area (Embedded Inspector)
            detailsScrollView = new ScrollView();
            detailsScrollView.style.flexGrow = 1;
            detailsScrollView.style.marginLeft = detailsScrollView.style.marginRight = WINDOW_MARGIN;
            detailsScrollView.verticalScroller.valueChanged += OnScrollChanged;
            detailsPanel.Add(detailsScrollView);

            // Add Shadow (last so it renders on top)
            scrollShadow = new VisualElement();
            scrollShadow.style.position = Position.Absolute;
            scrollShadow.style.left = 0;
            scrollShadow.style.right = 0;
            scrollShadow.style.height = 15;
            scrollShadow.style.opacity = 0; // Hidden by default
            scrollShadow.pickingMode = PickingMode.Ignore;
            scrollShadow.style.backgroundImage = GenerateShadowTexture();
            detailsPanel.Add(scrollShadow);

            rootVisualElement.Add(detailsPanel);
        }

        Texture2D GenerateShadowTexture()
        {
            if (shadowTexture != null)
                return shadowTexture;

            shadowTexture = new Texture2D(1, 16, TextureFormat.ARGB32, false)
            {
                alphaIsTransparency = true,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < 16; y++)
            {
                float alpha = (y / 15f) * 0.3f;
                shadowTexture.SetPixel(0, y, new Color(0, 0, 0, alpha));
            }

            shadowTexture.Apply();
            return shadowTexture;
        }

        void OnScrollChanged(float value)
        {
            if (scrollShadow == null) { return; }
            scrollShadow.style.opacity = value > 1f ? 1f : 0f; // Show shadow when scrolled down
        }

        ToolbarButton CreateToolbarButton(string text, string iconName, Action onClick)
        {
            var btn = new ToolbarButton(onClick) { focusable = false };
            btn.style.alignSelf = Align.Stretch;
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;

            var icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon != null)
            {
                var iconImage = new Image { image = icon };
                iconImage.style.width = 14;
                iconImage.style.height = 14;
                iconImage.style.marginLeft = 2;
                iconImage.style.marginRight = 5;
                btn.Add(iconImage);
            }

            btn.Add(new Label(text));
            return btn;
        }

        void RefreshPresetList()
        {
            presets.Clear();
            string[] guids = AssetDatabase.FindAssets("t:StylerPreset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StylerPreset>(path);
                if (asset != null) { presets.Add(asset); }
            }

            // Selection recovery
            if (selectedPreset != null)
            {
                if (!presets.Contains(selectedPreset))
                {
                    // Try recovering by name
                    var recovered = presets.FirstOrDefault(p => p.name == selectedPreset.name);
                    selectedPreset = recovered != null ? recovered : (presets.Count > 0 ? presets[0] : null);
                }
            }
            else if (presets.Count > 0)
            {
                selectedPreset = presets[0];
            }

            UpdateDetailsPanel();
            UpdatePresetDropdown();
        }

        void UpdatePresetDropdown()
        {
            if (presetMenu == null)
                return;

            presetMenu.menu.MenuItems().Clear();
            for (int i = 0; i < presets.Count; i++)
            {
                var preset = presets[i];
                if (preset == null) { continue; }

                presetMenu.menu.AppendAction(preset.name,
                    (action) =>
                    {
                        SelectPreset(preset);
                    },
                    (action) =>
                    {
                        return (selectedPreset == preset) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                    });
            }

            string presetName = selectedPreset != null ? selectedPreset.name : "None";
            string label = $"Selected Preset: <b>{presetName}</b>";
            if (presetMenu.text != label) { presetMenu.text = label; }
        }

        void SelectPreset(StylerPreset preset)
        {
            if (selectedPreset == preset)
                return;

            selectedPreset = preset;
            UpdateDetailsPanel();
            UpdatePresetDropdown();
        }

        void UpdateDetailsPanel()
        {
            if (detailsScrollView == null || headerContainer == null)
                return;

            headerContainer.Clear();
            detailsScrollView.Clear();

            // Reset shadow state
            if (scrollShadow != null) { scrollShadow.style.opacity = 0; }

            // Check for preset
            if (selectedPreset == null)
            {
                var emptyLabel = new Label();
                if (presets.Count == 0) { emptyLabel.text = "No Styler Preset found in the project."; }
                else { emptyLabel.text = "Select a preset to see actions."; }
                emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                emptyLabel.style.marginTop = WINDOW_MARGIN;
                headerContainer.Add(emptyLabel);
                return;
            }

            // Asset Name Field
            var renameField = new TextField { value = selectedPreset.name };
            renameField.style.fontSize = 14;
            renameField.style.height = ACTION_ITEM_SIZE;
            renameField.style.marginTop = (WINDOW_MARGIN * 2) - 2;
            renameField.style.marginBottom = ACTION_SPACING;
            renameField.style.unityFontStyleAndWeight = FontStyle.Bold;
            renameField.RegisterCallback<FocusOutEvent>(evt => RenamePreset(selectedPreset, renameField.value));
            headerContainer.Add(renameField);

            // File Actions Row
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            headerContainer.Add(actionsRow);

            // Open in Inspector
            var openBtn = CreateIconButton("Open in Inspector", "UnityEditor.InspectorWindow", () => Selection.activeObject = selectedPreset);
            openBtn.style.flexGrow = 1;
            openBtn.style.height = ACTION_ITEM_SIZE;
            actionsRow.Add(openBtn);

            // Duplicate
            var dupBtn = CreateIconButton("Duplicate", "CreateAddNew@2x", () => DuplicatePreset(selectedPreset));
            dupBtn.style.flexGrow = 1;
            dupBtn.style.height = ACTION_ITEM_SIZE;
            actionsRow.Add(dupBtn);

            // Delete
            var delBtn = new Button(() => DeletePreset(selectedPreset));
            delBtn.style.width = ACTION_ITEM_SIZE;
            delBtn.style.height = ACTION_ITEM_SIZE;
            delBtn.style.alignItems = Align.Center;
            delBtn.style.justifyContent = Justify.Center;
            delBtn.tooltip = "Delete preset";

            var delIcon = new Image { image = EditorGUIUtility.IconContent("Cancel@2x").image };
            delIcon.style.width = 16;
            delIcon.style.height = 16;

            delBtn.Add(delIcon);
            actionsRow.Add(delBtn);

            // Global/Apply Actions Row
            var globalActionsRow = new VisualElement();
            globalActionsRow.style.flexDirection = FlexDirection.Row;
            globalActionsRow.style.marginTop = ACTION_SPACING;
            headerContainer.Add(globalActionsRow);

            // Apply to Scene (Global)
            var applySceneBtn = CreateIconButton("Apply to Scene", "SceneAsset Icon", () => ApplyPresetToScene());
            applySceneBtn.style.flexGrow = 1;
            applySceneBtn.style.height = ACTION_ITEM_SIZE;
            globalActionsRow.Add(applySceneBtn);

            // Apply to Selection Button
            GameObject activeObj = Selection.activeGameObject;

            // Support both StylerObject and any IStylerHandler
            IStylerHandler stylerHandler = activeObj != null ? activeObj.GetComponent<IStylerHandler>() : null;
            if (stylerHandler != null)
            {
                var applyBtn = CreateIconButton($"Apply to '{activeObj.name}'", "Dependency@2x", () => ApplyPresetToSelection(stylerHandler, activeObj.name));
                applyBtn.style.flexGrow = 1;
                applyBtn.style.marginLeft = ACTION_SPACING;
                applyBtn.style.height = ACTION_ITEM_SIZE;
                globalActionsRow.Add(applyBtn);
            }

            // Separator
            var sepContainer = new VisualElement();
            sepContainer.style.flexDirection = FlexDirection.Row;
            sepContainer.style.alignItems = Align.Center;
            sepContainer.style.marginTop = ACTION_SPACING * 2;
            sepContainer.style.marginBottom = ACTION_SPACING;

            var sepIcon = new Image { image = EditorGUIUtility.IconContent("Preset.Context").image };
            sepIcon.style.width = 16;
            sepIcon.style.height = 16;
            sepIcon.style.marginLeft = 1;
            sepIcon.style.marginRight = 2;
            sepContainer.Add(sepIcon);

            var sepLabel = new Label("Preset Properties");
            sepLabel.style.fontSize = 13;
            sepContainer.Add(sepLabel);

            var sepLine = new VisualElement();
            sepLine.style.flexGrow = 1;
            sepLine.style.height = 1;
            sepLine.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            sepLine.style.marginLeft = ACTION_SPACING;
            sepLine.style.marginRight = WINDOW_MARGIN;
            sepContainer.Add(sepLine);

            headerContainer.Add(sepContainer);

            // Embedded Inspector
            var serializedPreset = new SerializedObject(selectedPreset);
            var inspector = new InspectorElement(serializedPreset);
            inspector.style.marginTop = -10;
            inspector.style.marginLeft = inspector.style.marginRight = -10;
            detailsScrollView.Add(inspector);

            var bottomSpacer = new VisualElement { style = { height = 30 } };
            detailsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            detailsScrollView.Add(bottomSpacer);
        }

        Button CreateIconButton(string text, string iconName, Action onClick)
        {
            var btn = new Button(onClick) { focusable = false };
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.justifyContent = Justify.Center;

            var icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon != null)
            {
                var iconImage = new Image { image = icon };
                iconImage.style.width = 14;
                iconImage.style.height = 14;
                iconImage.style.marginLeft = 2;
                iconImage.style.marginRight = 5;
                btn.Add(iconImage);
            }

            btn.Add(new Label(text));
            return btn;
        }

        void ApplyPresetToScene()
        {
            if (selectedPreset == null)
                return;

            if (!EditorUtility.DisplayDialog("Apply Preset to Scene",
                $"Are you sure you want to apply '{selectedPreset.name}' to all Styler objects in the open scene?",
                "Apply", "Cancel"))
            {
                return;
            }

            // Find ALL handlers (StylerObjects AND custom interfaces like PieChart)
            // We use FindObjectsByType to ensure we catch everything in the Editor 
            // (Styler.ActiveObjects list might be empty if not playing)
            var allHandlers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<IStylerHandler>()
                .ToList();

            if (allHandlers.Count == 0)
                return;

            // Register Undo for all objects at once
            // We need to cast back to UnityEngine.Object for the Undo system
            var undoObjects = allHandlers.Where(h => h is Component).Select(h => h as Component).ToArray();
            Undo.RecordObjects(undoObjects, $"Apply Styler Preset '{selectedPreset.name}'");

            // Apply to all
            foreach (var handler in allHandlers)
            {
                handler.Preset = selectedPreset;
                if (handler is Component comp) { EditorUtility.SetDirty(comp); }
            }

            Debug.Log($"[Styler] Applied '{selectedPreset.name}' to {allHandlers.Count} objects.");
        }

        void ApplyPresetToSelection(IStylerHandler handler, string objectName)
        {
            if (handler == null || selectedPreset == null)
                return;

            // We need to cast to Object to use Undo system
            if (handler is UnityEngine.Object obj) { Undo.RecordObject(obj, $"Apply Styler Preset to {objectName}"); }

            handler.Preset = selectedPreset;

            // Trigger update just in case, though Property setter should handle it
            // handler.UpdateStyler(); // Not needed if property setter handles it
            if (handler is UnityEngine.Object dirtyObj)
            {
                EditorUtility.SetDirty(dirtyObj);
            }
        }

        void RenamePreset(StylerPreset asset, string newName)
        {
            if (asset == null || string.IsNullOrEmpty(newName) || asset.name == newName)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            string result = AssetDatabase.RenameAsset(path, newName);
            if (string.IsNullOrEmpty(result))
            {
                AssetDatabase.SaveAssets();
                RefreshPresetList();
            }
        }

        void DuplicatePreset(StylerPreset asset)
        {
            if (asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CopyAsset(path, newPath);
            AssetDatabase.SaveAssets();

            RefreshPresetList();

            var newAsset = AssetDatabase.LoadAssetAtPath<StylerPreset>(newPath);
            if (newAsset) { SelectPreset(newAsset); }
        }

        void DeletePreset(StylerPreset asset)
        {
            if (asset == null) { return; }
            if (EditorUtility.DisplayDialog("Delete Styler Preset", $"Are you sure you want to delete {asset.name}?", "Delete", "Cancel"))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
                selectedPreset = null;
                RefreshPresetList();
            }
        }
    }
}