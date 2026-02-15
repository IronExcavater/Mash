using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    public class CreateMenu : Editor
    {
        static string objectPath;
        static bool isPathCached;

        const int MENU_ORDER = 7;
        const string MENU_PREFIX = "GameObject/Evo UI/";

        static void GetObjectPath()
        {
            // Return cached path if available
            if (isPathCached && !string.IsNullOrEmpty(objectPath))
                return;

            // Try primary method first
            var stylerAsset = Resources.Load(Constants.STYLER_FALLBACK_PATH);
            if (stylerAsset != null)
            {
                objectPath = AssetDatabase.GetAssetPath(stylerAsset);
                objectPath = objectPath.Replace($"Resources/{Constants.STYLER_FALLBACK_PATH}.asset", "").TrimEnd('/') + "/Prefabs/";
                isPathCached = true;
                return;
            }

            // Fallback: Search for any Evo UI folder structure
            string[] folders = AssetDatabase.FindAssets("Evo UI t:folder");
            foreach (string guid in folders)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Evo UI") || path.EndsWith("Evo UI/"))
                {
                    objectPath = path + "/Prefabs/";
                    isPathCached = true;
                    return;
                }
            }

            // Return to default if no fallback
            objectPath = "Assets/Evo/Evo UI/Prefabs/";
            isPathCached = true;
        }

        static Canvas GetCanvas()
        {
#if UNITY_2023_2_OR_NEWER
            var tCanvas = FindFirstObjectByType<Canvas>();
            if (tCanvas != null) { return FindFirstObjectByType<Canvas>(); }
#else
            var canvases = FindObjectsOfType<Canvas>();
            if (canvases.Length > 0) { return canvases[0]; }
#endif

            // Create if not found
            var canvasGO = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasGO.GetComponent<Canvas>();
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Set options
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440);
            scaler.matchWidthOrHeight = 0.5f;

            // Create EventSystem if not present
#if UNITY_2023_2_OR_NEWER
            if (FindFirstObjectByType<EventSystem>() == null)
#else
            if (FindObjectOfType<EventSystem>() == null)
#endif
            {
#if ENABLE_INPUT_SYSTEM
                var eventGO = new GameObject("Event System", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
                var eventGO = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
                Undo.RegisterCreatedObjectUndo(eventGO, "Create EventSystem");
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            Selection.activeObject = canvasGO;

            return canvas;
        }

        static void CreateObject(string path)
        {
            GetObjectPath();
            string fullPath = objectPath + path + ".prefab";

            // Check if prefab exists before trying to load
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Evo UI", $"Prefab not found at: {fullPath}\nPlease check your Evo UI installation.", "OK");
                return;
            }

            GameObject clone = Instantiate(AssetDatabase.LoadAssetAtPath(fullPath, typeof(GameObject)), Vector3.zero, Quaternion.identity) as GameObject;
            clone.name = clone.name.Replace("(Clone)", "").Trim();

            if (Selection.activeGameObject != null) { clone.transform.SetParent(Selection.activeGameObject.transform, false); }
            else { clone.transform.SetParent(GetCanvas().transform, false); }
 
            Undo.RegisterCreatedObjectUndo(clone, $"Create {clone.name}");
            Selection.activeObject = clone;
        }

        [MenuItem(MENU_PREFIX + "Animated/Counter", false, MENU_ORDER)]
        static void CreateCounter() { CreateObject("Animated/Counter"); }

        [MenuItem(MENU_PREFIX + "Button/Default", false, MENU_ORDER)]
        static void CreateButton() { CreateObject("Button/Button"); }

        [MenuItem(MENU_PREFIX + "Button/Rounded", false, MENU_ORDER)]
        static void CreateButtonRounded() { CreateObject("Button/Button (Rounded)"); }

        [MenuItem(MENU_PREFIX + "Button/Rectangle", false, MENU_ORDER)]
        static void CreateButtonRectangle() { CreateObject("Button/Button (Rectangle)"); }

        [MenuItem(MENU_PREFIX + "Button/Default (Gradient)", false, MENU_ORDER)]
        static void CreateButtonGradientD() { CreateObject("Button/Button (Gradient)"); }

        [MenuItem(MENU_PREFIX + "Button/Rectangle (Gradient)", false, MENU_ORDER)]
        static void CreateButtonGradientR() { CreateObject("Button/Button (Rectangle Gradient)"); }

        [MenuItem(MENU_PREFIX + "Button/Rounded (Gradient)", false, MENU_ORDER)]
        static void CreateButtonGradientRo() { CreateObject("Button/Button (Rounded Gradient)"); }

        [MenuItem(MENU_PREFIX + "Button/Default (Outline)", false, MENU_ORDER)]
        static void CreateButtonOutlineD() { CreateObject("Button/Button (Outline)"); }

        [MenuItem(MENU_PREFIX + "Button/Rectangle (Outline)", false, MENU_ORDER)]
        static void CreateButtonOutlineR() { CreateObject("Button/Button (Rectangle Outline)"); }

        [MenuItem(MENU_PREFIX + "Button/Rounded (Outline)", false, MENU_ORDER)]
        static void CreateButtonOutlineRo() { CreateObject("Button/Button (Rounded Outline)"); }

        [MenuItem(MENU_PREFIX + "Button/Icon Only", false, MENU_ORDER)]
        static void CreateButtonIO() { CreateObject("Button/Button (Icon Only)"); }

        [MenuItem(MENU_PREFIX + "Button/Icon Sway", false, MENU_ORDER)]
        static void CreateButtonIS() { CreateObject("Button/Button (Icon Sway)"); }  

        [MenuItem(MENU_PREFIX + "Button/Radio Button", false, MENU_ORDER)]
        static void CreateRadioButton() { CreateObject("Button/Radio Button"); }

        [MenuItem(MENU_PREFIX + "Button/Radio Button (Alt)", false, MENU_ORDER)]
        static void CreateRadioButtonAlt() { CreateObject("Button/Radio Button (Alt)"); }

        [MenuItem(MENU_PREFIX + "Button/Radio Button Group", false, MENU_ORDER)]
        static void CreateRadioButtonGroup() { CreateObject("Button/Radio Button Group"); }

        [MenuItem(MENU_PREFIX + "Button/Radio Button Group (Alt)", false, MENU_ORDER)]
        static void CreateRadioButtonGroupAlt() { CreateObject("Button/Radio Button Group (Alt)"); }

        [MenuItem(MENU_PREFIX + "Button/Progress Button", false, MENU_ORDER)]
        static void CreateProgressButton() { CreateObject("Button/Progress Button"); }

        [MenuItem(MENU_PREFIX + "Button/Progress Button (Alt)", false, MENU_ORDER)]
        static void CreateProgressButtonAlt() { CreateObject("Button/Progress Button (Alt)"); }

        [MenuItem(MENU_PREFIX + "Carousel/Default", false, MENU_ORDER)]
        static void CreateCarousel() { CreateObject("Carousel/Carousel"); }

        [MenuItem(MENU_PREFIX + "Carousel/Alternative", false, MENU_ORDER)]
        static void CreateCarouselAlt() { CreateObject("Carousel/Carousel (Alternative)"); }

        [MenuItem(MENU_PREFIX + "Charts/Horizontal Chart", false, MENU_ORDER)]
        static void CreateHorizontalChart() { CreateObject("Charts/Horizontal Chart"); }

        [MenuItem(MENU_PREFIX + "Charts/Line Chart", false, MENU_ORDER)]
        static void CreateLineChart() { CreateObject("Charts/Line Chart"); }

        [MenuItem(MENU_PREFIX + "Charts/Pie Chart", false, MENU_ORDER)]
        static void CreatePieChart() { CreateObject("Charts/Pie Chart"); }

        [MenuItem(MENU_PREFIX + "Charts/Radar Chart", false, MENU_ORDER)]
        static void CreateRadarChart() { CreateObject("Charts/Radar Chart"); }

        [MenuItem(MENU_PREFIX + "Charts/Vertical Chart", false, MENU_ORDER)]
        static void CreateVerticalChart() { CreateObject("Charts/Vertical Chart"); }

        [MenuItem(MENU_PREFIX + "Color Picker/Compact", false, MENU_ORDER)]
        static void CreateColorPickerC() { CreateObject("Color Picker/Color Picker (Compact)"); }

        [MenuItem(MENU_PREFIX + "Color Picker/Square", false, MENU_ORDER)]
        static void CreateColorPickerS() { CreateObject("Color Picker/Color Picker (Square)"); }

        [MenuItem(MENU_PREFIX + "Color Picker/Wheel", false, MENU_ORDER)]
        static void CreateColorPickerW() { CreateObject("Color Picker/Color Picker (Wheel)"); }

        [MenuItem(MENU_PREFIX + "Date and Time/Calendar", false, MENU_ORDER)]
        static void CreateDatePicker() { CreateObject("Date & Time/Calendar"); }

        [MenuItem(MENU_PREFIX + "Date and Time/Countdown", false, MENU_ORDER)]
        static void CreateCountdown() { CreateObject("Date & Time/Countdown"); }

        [MenuItem(MENU_PREFIX + "Date and Time/Timer (Horizontal)", false, MENU_ORDER)]
        static void CreateTimerH() { CreateObject("Timer/Timer (Horizontal)"); }

        [MenuItem(MENU_PREFIX + "Date and Time/Timer (Vertical)", false, MENU_ORDER)]
        static void CreateTimerV() { CreateObject("Timer/Timer (Vertical)"); }

        [MenuItem(MENU_PREFIX + "Date and Time/Timer (Radial)", false, MENU_ORDER)]
        static void CreateTimerR() { CreateObject("Timer/Timer (Radial)"); }

        [MenuItem(MENU_PREFIX + "Dropdown/Dropdown", false, MENU_ORDER)]
        static void CreateDropdown() { CreateObject("Dropdown/Dropdown"); }

        [MenuItem(MENU_PREFIX + "Dropdown/Input (Combo Box)", false, MENU_ORDER)]
        static void CreateDropdownInput() { CreateObject("Dropdown/Dropdown (Input)"); }

        [MenuItem(MENU_PREFIX + "Input Field/Default", false, MENU_ORDER)]
        static void CreateInputField() { CreateObject("Input Field/Input Field"); }

        [MenuItem(MENU_PREFIX + "Input Field/With Icon", false, MENU_ORDER)]
        static void CreateInputFieldWithIcon() { CreateObject("Input Field/Input Field (With Icon)"); }

        [MenuItem(MENU_PREFIX + "Input Field/Line", false, MENU_ORDER)]
        static void CreateInputFieldLine() { CreateObject("Input Field/Input Field (Line)"); }

        [MenuItem(MENU_PREFIX + "Input Field/Multi Line", false, MENU_ORDER)]
        static void CreateInputFieldMultiLine() { CreateObject("Input Field/Input Field (Multi Line)"); }

        [MenuItem(MENU_PREFIX + "Input Field/Multi Line (Scrollbar)", false, MENU_ORDER)]
        static void CreateInputFieldMultiLineScrollbar() { CreateObject("Input Field/Input Field (Multi Line Scrollbar)"); }

        [MenuItem(MENU_PREFIX + "List View/Default", false, MENU_ORDER)]
        static void CreateListView() { CreateObject("List View/List View"); }

        [MenuItem(MENU_PREFIX + "List View/Masked", false, MENU_ORDER)]
        static void CreateListViewMasked() { CreateObject("List View/List View (Masked)"); }

        [MenuItem(MENU_PREFIX + "Menu Bar/Menu Bar Preset", false, MENU_ORDER)]
        static void CreateMenuBar() { CreateObject("Menu Bar/Menu Bar Preset"); }

        [MenuItem(MENU_PREFIX + "Modal Window/Default", false, MENU_ORDER)]
        static void CreateModalWindow() { CreateObject("Modal Window/Modal Window"); }

        [MenuItem(MENU_PREFIX + "Modal Window/Alternative", false, MENU_ORDER)]
        static void CreateModalWindowAlt() { CreateObject("Modal Window/Modal Window (Alternative)"); }

        [MenuItem(MENU_PREFIX + "Notification/Default", false, MENU_ORDER)]
        static void CreateNotification() { CreateObject("Notification/Notification"); }

        [MenuItem(MENU_PREFIX + "Pages/Horizontal", false, MENU_ORDER)]
        static void CreatePages() { CreateObject("Pages/Pages Preset"); }

        [MenuItem(MENU_PREFIX + "Pages/Horizontal (Alt)", false, MENU_ORDER)]
        static void CreatePagesAlt() { CreateObject("Pages/Pages Preset (Alt)"); }

        [MenuItem(MENU_PREFIX + "Pages/Vertical", false, MENU_ORDER)]
        static void CreatePagesVertical() { CreateObject("Pages/Pages Preset (Vertical)"); }

        [MenuItem(MENU_PREFIX + "Pages/Vertical (Alt)", false, MENU_ORDER)]
        static void CreatePagesVerticalAlt() { CreateObject("Pages/Pages Preset (Vertical Alt)"); }

        [MenuItem(MENU_PREFIX + "Progress Bar/Horizontal", false, MENU_ORDER)]
        static void CreateProgressBarH() { CreateObject("Progress Bar/Progress Bar (Horizontal)"); }

        [MenuItem(MENU_PREFIX + "Progress Bar/Vertical", false, MENU_ORDER)]
        static void CreateProgressBarV() { CreateObject("Progress Bar/Progress Bar (Vertical)"); }

        [MenuItem(MENU_PREFIX + "Progress Bar/Radial", false, MENU_ORDER)]
        static void CreateProgressBarR() { CreateObject("Progress Bar/Progress Bar (Radial)"); }

        [MenuItem(MENU_PREFIX + "Scrollbar/Horizontal", false, MENU_ORDER)]
        static void CreateScrollbarH() { CreateObject("Scrollbar/Scrollbar (Horizontal)"); }

        [MenuItem(MENU_PREFIX + "Scrollbar/Vertical", false, MENU_ORDER)]
        static void CreateScrollbarV() { CreateObject("Scrollbar/Scrollbar (Vertical)"); }

        [MenuItem(MENU_PREFIX + "Selector/Horizontal", false, MENU_ORDER)]
        static void CreateSelectorH() { CreateObject("Selector/Selector (Horizontal)"); }

        [MenuItem(MENU_PREFIX + "Selector/Vertical", false, MENU_ORDER)]
        static void CreateSelectorV() { CreateObject("Selector/Selector (Vertical)"); }

        [MenuItem(MENU_PREFIX + "Selector/Quantity", false, MENU_ORDER)]
        static void CreateSelectorQuantity() { CreateObject("Selector/Selector (Quantity)"); }

        [MenuItem(MENU_PREFIX + "Showcase Panel/Default", false, MENU_ORDER)]
        static void CreateShowcasePanel() { CreateObject("Showcase Panel/Showcase Panel"); }

        [MenuItem(MENU_PREFIX + "Slider/Horizontal", false, MENU_ORDER)]
        static void CreateSliderH() { CreateObject("Slider/Slider (Horizontal)"); }

        [MenuItem(MENU_PREFIX + "Slider/Horizontal (Input)", false, MENU_ORDER)]
        static void CreateSliderHI() { CreateObject("Slider/Slider (Horizontal Input)"); }

        [MenuItem(MENU_PREFIX + "Slider/Horizontal (Popup Value)", false, MENU_ORDER)]
        static void CreateSliderHP() { CreateObject("Slider/Slider (Horizontal Popup Value)"); }

        [MenuItem(MENU_PREFIX + "Slider/Vertical", false, MENU_ORDER)]
        static void CreateSliderV() { CreateObject("Slider/Slider (Vertical)"); }

        [MenuItem(MENU_PREFIX + "Slider/Vertical (Input)", false, MENU_ORDER)]
        static void CreateSliderVI() { CreateObject("Slider/Slider (Vertical Input)"); }

        [MenuItem(MENU_PREFIX + "Slider/Vertical (Popup Value)", false, MENU_ORDER)]
        static void CreateSliderVP() { CreateObject("Slider/Slider (Vertical Popup Value)"); }

        [MenuItem(MENU_PREFIX + "Slider/Radial", false, MENU_ORDER)]
        static void CreateSliderR() { CreateObject("Slider/Slider (Radial)"); }

        [MenuItem(MENU_PREFIX + "Slider/Radial (Knob)", false, MENU_ORDER)]
        static void CreateSliderRK() { CreateObject("Slider/Slider (Radial Knob)"); }

        [MenuItem(MENU_PREFIX + "Spinner/Horizontal", false, MENU_ORDER)]
        static void CreateSpinnerH() { CreateObject("Spinner/Spinner (Horizontal)"); }

        [MenuItem(MENU_PREFIX + "Spinner/Vertical", false, MENU_ORDER)]
        static void CreateSpinnerV() { CreateObject("Spinner/Spinner (Vertical)"); }

        [MenuItem(MENU_PREFIX + "Spinner/Radial", false, MENU_ORDER)]
        static void CreateSpinnerR() { CreateObject("Spinner/Spinner (Radial)"); }

        [MenuItem(MENU_PREFIX + "Switch/Default", false, MENU_ORDER)]
        static void CreateSwitch() { CreateObject("Switch/Switch"); }

        [MenuItem(MENU_PREFIX + "Switch/With Indicator", false, MENU_ORDER)]
        static void CreateSwitchWI() { CreateObject("Switch/Switch (Indicator)"); }

        [MenuItem(MENU_PREFIX + "Switch/With Label", false, MENU_ORDER)]
        static void CreateSwitchWL() { CreateObject("Switch/Switch (Label)"); }

        [MenuItem(MENU_PREFIX + "Switch/With Indicator + Label", false, MENU_ORDER)]
        static void CreateSwitchWIL() { CreateObject("Switch/Switch (Indicator + Label)"); }

        [MenuItem(MENU_PREFIX + "Tabs/Tabs Preset", false, MENU_ORDER)]
        static void CreateTabs() { CreateObject("Tabs/Tabs Preset"); }

        [MenuItem(MENU_PREFIX + "Tabs/Tabs Preset (Down)", false, MENU_ORDER)]
        static void CreateTabsDown() { CreateObject("Tabs/Tabs Preset (Down)"); }

        [MenuItem(MENU_PREFIX + "Tabs/Tabs Preset (Left)", false, MENU_ORDER)]
        static void CreateTabsLeft() { CreateObject("Tabs/Tabs Preset (Left)"); }

        [MenuItem(MENU_PREFIX + "Tabs/Tabs Preset (Right)", false, MENU_ORDER)]
        static void CreateTabsRight() { CreateObject("Tabs/Tabs Preset (Right)"); }

        [MenuItem(MENU_PREFIX + "Tabs/Tab Button", false, MENU_ORDER)]
        static void CreateTabButton() { CreateObject("Tabs/Tab Button"); }
    }
}