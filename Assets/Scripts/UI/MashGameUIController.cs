using Evo.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/UI/Mash Game UI Controller")]
public class MashGameUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private bool autoAssignUiBindings = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private GameFlowController gameFlow;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterCapacity helicopterCapacity;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelipadZone helipadZone;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterCollisionHandler crashHandler;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private SoldierSpawnManager soldierSpawner;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private TerrainGenerator terrainGenerator;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterFlightController helicopterFlight;

    [ConditionalField("autoAssignUiBindings", false, "Roots")]
    [SerializeField] private CanvasGroup mainMenuRoot;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private CanvasGroup mainMenuContentRoot;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private CanvasGroup pauseMenuRoot;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private CanvasGroup hudRoot;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private CanvasGroup crashRoot;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private CanvasGroup settingsRoot;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private CanvasGroup loadingRoot;

    [ConditionalField("autoAssignUiBindings", false, "Buttons")]
    [SerializeField] private Button startButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button resumeButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button restartButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button quitButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button pauseQuitButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button settingsButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button settingsBackButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button crashRestartButton;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private Button crashQuitButton;

    [ConditionalField("autoAssignUiBindings", false, "HUD Text")]
    [SerializeField] private TMP_Text inHelicopterText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text rescuedText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text missionPhaseText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text missionStateText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private OffScreenIndicator helipadIndicator;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text mainMenuTitleText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text mainMenuSubtitleText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private UnityEngine.UI.Toggle displayFullscreenToggle;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private UnityEngine.UI.Slider soundMasterVolumeSlider;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Dropdown gameplayControlModeDropdown;
    [ConditionalField("autoAssignUiBindings", false, "Loading")]
    [SerializeField] private TMP_Text loadingLabelText;

    [Header("Settings")]
    [SerializeField] private string inHelicopterFormat = "In Heli: {0}/{1}";
    [SerializeField] private string rescuedFormat = "Rescued: {0}";
    [SerializeField] private string waitingFormat = "Not Rescued: {0}";
    [SerializeField] private bool showMissionStateText = false;
    [SerializeField, Min(0f)] private float panelFadeDuration = 0.22f;
    [SerializeField] private UnityEngine.Events.UnityEvent onSettingsPressed;
    [SerializeField] private bool hideTerrainLoadingWithFade = true;
    [ConditionalField("hideTerrainLoadingWithFade", true)]
    [SerializeField, Min(0.01f)] private float loadingMinimumVisibleTime = 0.35f;
    [SerializeField, Min(0f)] private float crashMenuDelaySeconds = 5.25f;

    private bool listenersBound;
    private bool settingsOpen;
    private bool isLoadingTerrain;
    private float loadingShownAtUnscaledTime;
    private Coroutine loadingHideRoutine;
    private bool terrainEventsSubscribed;
    private bool waitingOutcomeMenuDelay;
    private float outcomeDetectedAtUnscaledTime;
    private readonly Dictionary<CanvasGroup, Coroutine> fadeRoutines = new Dictionary<CanvasGroup, Coroutine>();
    private readonly Dictionary<CanvasGroup, bool> panelVisibilityTargets = new Dictionary<CanvasGroup, bool>();
    private bool initialLayoutPrepared;

    private void Awake()
    {
        if (crashMenuDelaySeconds < 0f) crashMenuDelaySeconds = 0f;
        ResolveReferences();
        BindButtonListeners();
        RefreshUI(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButtonListeners();
        BindTerrainEvents();
        RefreshUI(true);
    }

    private void Start()
    {
        PrepareInitialLayout();
    }

    private void Update()
    {
        RefreshUI(false);
        HandleMenuHotkeys();
    }

    private void ResolveReferences()
    {
        if (!autoAssignReferences) return;
        gameFlow ??= FindFirstObjectByType<GameFlowController>();
        helicopterCapacity ??= FindFirstObjectByType<HelicopterCapacity>();
        helipadZone ??= FindFirstObjectByType<HelipadZone>();
        crashHandler ??= FindFirstObjectByType<HelicopterCollisionHandler>();
        soldierSpawner ??= FindFirstObjectByType<SoldierSpawnManager>();
        terrainGenerator ??= FindFirstObjectByType<TerrainGenerator>();
        helicopterFlight ??= FindFirstObjectByType<HelicopterFlightController>();
        if (!autoAssignUiBindings) return;

        AutoAssign(ref mainMenuRoot, "MainMenuPanel");
        AutoAssign(ref mainMenuContentRoot, "MainMenuContent");
        AutoAssign(ref pauseMenuRoot, "PauseMenuPanel");
        AutoAssign(ref hudRoot, "HUDPanel");
        AutoAssign(ref crashRoot, "CrashOverlayPanel");
        AutoAssign(ref settingsRoot, "SettingsPanel");
        AutoAssign(ref loadingRoot, "LoadingOverlayPanel");

        AutoAssign(ref startButton, "StartMissionButton");
        AutoAssign(ref resumeButton, "ResumeGameButton");
        AutoAssign(ref restartButton, "RestartGameButton");
        AutoAssign(ref settingsButton, "SettingsButton");
        AutoAssign(ref settingsBackButton, "SettingsBackButton");
        AutoAssign(ref quitButton, "MainMenuQuitButton");
        AutoAssign(ref pauseQuitButton, "PauseMenuQuitButton");
        AutoAssign(ref crashRestartButton, "CrashRestartButton");
        AutoAssign(ref crashQuitButton, "CrashQuitButton");

        inHelicopterText ??= FindTextByName("HelicopterCapacityRow/ValueLabel");
        rescuedText ??= FindTextByName("RescuedSoldiersRow/ValueLabel");
        missionPhaseText ??= FindTextByName("WaitingSoldiersRow/ValueLabel");
        missionStateText ??= FindTextByName("MissionStateLabel");
        AutoAssign(ref helipadIndicator, "HelipadOffscreenIndicator");
        mainMenuTitleText ??= FindTextByName("MainMenuTitleLabel");
        mainMenuSubtitleText ??= FindTextByName("MainMenuSubtitleLabel");
        AutoAssign(ref displayFullscreenToggle, "DisplayFullscreenToggle");
        AutoAssign(ref displayModeDropdown, "DisplayModeDropdown");
        AutoAssign(ref soundMasterVolumeSlider, "SoundMasterVolumeSlider");
        AutoAssign(ref gameplayControlModeDropdown, "GameplayControlModeDropdown");
        loadingLabelText ??= FindTextByName("LoadingOverlayPanel/LoadingLabel");
    }

    private void BindButtonListeners()
    {
        if (listenersBound) return;
        listenersBound = true;

        if (startButton != null) startButton.onClick.AddListener(OnStartPressed);
        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumePressed);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartPressed);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsPressed);
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitPressed);
        if (pauseQuitButton != null) pauseQuitButton.onClick.AddListener(OnQuitPressed);
        if (crashRestartButton != null) crashRestartButton.onClick.AddListener(OnRestartPressed);
        if (crashQuitButton != null) crashQuitButton.onClick.AddListener(OnQuitPressed);

        if (displayFullscreenToggle != null)
        {
            displayFullscreenToggle.SetIsOnWithoutNotify(IsFullscreenEnabled());
            displayFullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        if (displayModeDropdown != null)
        {
            ConfigureDisplayModeDropdown();
            displayModeDropdown.onValueChanged.AddListener(OnDisplayModeChanged);
        }

        if (soundMasterVolumeSlider != null)
        {
            soundMasterVolumeSlider.minValue = 0f;
            soundMasterVolumeSlider.maxValue = 1f;
            soundMasterVolumeSlider.wholeNumbers = false;
            soundMasterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
            soundMasterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (gameplayControlModeDropdown != null)
        {
            ConfigureControlModeDropdown();
            gameplayControlModeDropdown.onValueChanged.AddListener(OnControlModeChanged);
        }
    }

    private void RefreshUI(bool force)
    {
        ResolveReferences();
        var state = gameFlow?.State ?? GameFlowController.SessionState.Playing;
        var showCrashRaw = crashHandler != null && (crashHandler.IsCrashing || crashHandler.IsCrashComplete);
        var showMissionCompleteRaw = state == GameFlowController.SessionState.MissionComplete;
        var showOutcomeRaw = showCrashRaw || showMissionCompleteRaw;
        if (showOutcomeRaw)
        {
            if (!waitingOutcomeMenuDelay)
            {
                waitingOutcomeMenuDelay = true;
                outcomeDetectedAtUnscaledTime = Time.unscaledTime;
            }
        }
        else
        {
            waitingOutcomeMenuDelay = false;
            outcomeDetectedAtUnscaledTime = 0f;
        }

        var outcomeDelayElapsed = !showOutcomeRaw || (Time.unscaledTime - outcomeDetectedAtUnscaledTime) >= crashMenuDelaySeconds;
        var showCrash = showCrashRaw && outcomeDelayElapsed;
        var showMissionComplete = showMissionCompleteRaw && outcomeDelayElapsed;
        var showMenu = state == GameFlowController.SessionState.MainMenu ||
                       state == GameFlowController.SessionState.Paused ||
                       showMissionComplete ||
                       showCrash;
        var showPause = state == GameFlowController.SessionState.Paused;
        var showHUD = state == GameFlowController.SessionState.Playing;
        var showLoading = hideTerrainLoadingWithFade && isLoadingTerrain;
        var lockHelicopter = showMenu || showLoading;

        ApplyPanelVisibility(force, mainMenuRoot, showMenu);
        ApplyPanelVisibility(force, pauseMenuRoot, false);
        ApplyPanelVisibility(force, hudRoot, showHUD && !showMenu);
        ApplyPanelVisibility(force, crashRoot, false);
        ApplyPanelVisibility(force, settingsRoot, showMenu && settingsOpen);
        ApplyPanelVisibility(force, mainMenuContentRoot, showMenu && !settingsOpen);
        ApplyPanelVisibility(force, loadingRoot, showLoading);
        UpdateLoadingOverlayVisuals(showLoading);

        var boarded = helicopterCapacity?.BoardedCount ?? 0;
        var maxSeats = helicopterCapacity?.MaxSeats ?? 0;
        var rescued = helicopterCapacity?.TotalRescuedCount ?? 0;
        var required = gameFlow?.RequiredSoldierCount ?? Mathf.Max(1, rescued);
        var notRescued = Mathf.Max(0, required - rescued);
        var phase = gameFlow?.Phase.ToString() ?? "Unknown";
        var stateText = state.ToString();

        if (crashHandler != null && (crashHandler.IsCrashing || crashHandler.IsCrashComplete))
            stateText = "Game Over";
        else if (state == GameFlowController.SessionState.MissionComplete)
            stateText = "You Win";

        if (inHelicopterText != null)
            inHelicopterText.text = string.Format(inHelicopterFormat, boarded, maxSeats);
        if (rescuedText != null)
            rescuedText.text = string.Format(rescuedFormat, rescued);
        if (missionPhaseText != null)
            missionPhaseText.text = string.Format(waitingFormat, notRescued);
        if (missionStateText != null)
        {
            missionStateText.gameObject.SetActive(showMissionStateText);
            if (showMissionStateText)
                missionStateText.text = stateText + " / " + phase;
        }

        if (helipadIndicator != null)
            UpdateHelipadIndicatorVisibility(showHUD && !showMenu);

        if (helicopterFlight != null)
            helicopterFlight.SetControlLock(lockHelicopter);

        var isMainMenuState = state == GameFlowController.SessionState.MainMenu && !showCrash;
        var settingsClosed = !settingsOpen;
        SetButtonVisible(startButton, showMenu && settingsClosed && isMainMenuState);
        SetButtonVisible(resumeButton, showMenu && settingsClosed && showPause);
        SetButtonVisible(restartButton, showMenu && settingsClosed && !isMainMenuState);
        SetButtonVisible(settingsButton, showMenu && settingsClosed);
        SetButtonVisible(quitButton, showMenu && settingsClosed);
        SetButtonVisible(settingsBackButton, showMenu && settingsOpen);
        SetButtonVisible(crashRestartButton, false);
        SetButtonVisible(crashQuitButton, false);
        SetButtonVisible(pauseQuitButton, false);

        UpdateSharedMenuContent(state, showCrash);
    }

    private static void SetCanvasGroup(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        if (group.gameObject.activeSelf != visible) group.gameObject.SetActive(visible);
    }

    private void PrepareInitialLayout()
    {
        if (initialLayoutPrepared) return;
        initialLayoutPrepared = true;

        ForceLayout(mainMenuRoot);
        ForceLayout(mainMenuContentRoot);
        ForceLayout(pauseMenuRoot);
        ForceLayout(hudRoot);
        ForceLayout(settingsRoot);
        ForceLayout(loadingRoot);
        Canvas.ForceUpdateCanvases();
        ForceRefreshBlurImages();
        RefreshUI(true);
    }

    private static void ForceLayout(CanvasGroup group)
    {
        if (group == null) return;
        var rect = group.transform as RectTransform;
        if (rect == null) return;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private static void ForceRefreshBlurImages()
    {
        var blurImages = FindObjectsByType<Kamgam.UGUIBlurredBackground.BlurredBackgroundImage>(FindObjectsSortMode.None);
        for (var i = 0; i < blurImages.Length; i++)
        {
            var blur = blurImages[i];
            if (blur == null) continue;
            blur.SetVerticesDirty();
            blur.SetMaterialDirty();
        }
    }

    private void UpdateLoadingOverlayVisuals(bool visible)
    {
        if (!visible)
        {
            if (loadingLabelText != null) loadingLabelText.text = "LOADING";
            return;
        }

        var elapsed = Mathf.Max(0f, Time.unscaledTime - loadingShownAtUnscaledTime);
        var labelDots = Mathf.FloorToInt(elapsed * 2.5f) % 4;
        if (loadingLabelText != null)
            loadingLabelText.text = "LOADING" + new string('.', labelDots);
    }

    private void ApplyPanelVisibility(bool immediate, CanvasGroup group, bool visible)
    {
        if (immediate) SetCanvasGroup(group, visible);
        else SetCanvasGroupFaded(group, visible);
    }

    private void SetCanvasGroupFaded(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        if (panelVisibilityTargets.TryGetValue(group, out var previousTarget) && previousTarget == visible)
            return;
        panelVisibilityTargets[group] = visible;

        if (panelFadeDuration <= 0.001f)
        {
            SetCanvasGroup(group, visible);
            return;
        }

        if (fadeRoutines.TryGetValue(group, out var running) && running != null)
            StopCoroutine(running);

        var routine = StartCoroutine(FadeCanvasGroup(group, visible, panelFadeDuration));
        fadeRoutines[group] = routine;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, bool visible, float duration)
    {
        if (group == null) yield break;
        if (!group.gameObject.activeSelf) group.gameObject.SetActive(true);

        var start = group.alpha;
        var target = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k);
            group.alpha = Mathf.Lerp(start, target, k);
            yield return null;
        }

        group.alpha = target;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        if (!visible) group.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        UnbindTerrainEvents();
        foreach (var kv in fadeRoutines)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        StopLoadingHideRoutine();
        fadeRoutines.Clear();
        panelVisibilityTargets.Clear();
    }

    private void OnStartPressed()
    {
        if (gameFlow == null) return;
        if (gameFlow.State == GameFlowController.SessionState.MainMenu)
            gameFlow.StartGame();
    }

    private void OnResumePressed()
    {
        if (gameFlow == null) return;
        if (gameFlow.State == GameFlowController.SessionState.Paused)
            gameFlow.TogglePause();
    }

    private void OnRestartPressed()
    {
        if (gameFlow != null)
        {
            gameFlow.ResetGame(true);
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnSettingsPressed()
    {
        settingsOpen = true;
        onSettingsPressed?.Invoke();
    }

    private void CloseSettings()
    {
        settingsOpen = false;
    }

    private void AutoAssign<T>(ref T field, string nameOrPath) where T : Component
    {
        if (field != null) return;
        field = FindByNameOrPath(nameOrPath)?.GetComponent<T>();
    }

    private Transform FindByNameOrPath(string nameOrPath)
    {
        return transform.Find(nameOrPath) ?? FindDeepChildByName(transform, nameOrPath);
    }

    private TMP_Text FindTextByName(string pathOrName)
    {
        var t = FindByNameOrPath(pathOrName);
        if (t == null) return null;

        var direct = t.GetComponent<TMP_Text>();
        if (direct != null) return direct;

        var mainText = t.Find("Main")?.GetComponent<TMP_Text>();
        if (mainText != null) return mainText;

        var texts = t.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null) continue;
            if (text.transform.parent != null && text.transform.parent.name == "Shadow") continue;
            return text;
        }

        return null;
    }

    private void UpdateHelipadIndicatorVisibility(bool shouldBeVisible)
    {
        if (helipadIndicator == null) return;

        var cam = Camera.main ?? FindFirstObjectByType<Camera>();

        if (helipadZone == null || cam == null)
        {
            helipadIndicator.gameObject.SetActive(false);
            return;
        }

        helipadIndicator.targetTransform = helipadZone.transform;
        helipadIndicator.targetCamera = cam;
        helipadIndicator.trackUIElement = false;
        helipadIndicator.hideWhenOnScreen = true;

        helipadIndicator.gameObject.SetActive(shouldBeVisible);
    }

    private void HandleMenuHotkeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        var mainShown = IsMenuVisible(mainMenuRoot);
        var pauseShown = gameFlow?.State == GameFlowController.SessionState.Paused && mainShown;
        if (!mainShown) return;

        if (settingsOpen)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.oKey.wasPressedThisFrame)
                CloseSettings();
            return;
        }

        if (kb.qKey.wasPressedThisFrame)
        {
            OnQuitPressed();
            return;
        }

        if (pauseShown && kb.pKey.wasPressedThisFrame)
        {
            OnResumePressed();
            return;
        }

        if ((pauseShown || mainShown) && kb.rKey.wasPressedThisFrame)
        {
            OnRestartPressed();
            return;
        }

        if (kb.oKey.wasPressedThisFrame)
        {
            OnSettingsPressed();
            return;
        }

        if (gameFlow?.State == GameFlowController.SessionState.MainMenu &&
            (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            OnStartPressed();
        }
    }

    private void UpdateSharedMenuContent(GameFlowController.SessionState state, bool showCrash)
    {
        if (mainMenuTitleText == null || mainMenuSubtitleText == null) return;

        if (showCrash)
        {
            mainMenuTitleText.text = "HELI DOWN";
            mainMenuSubtitleText.text = "Mission failed. Restart and redeploy.";
            if (restartButton != null) restartButton.SetText("REDEPLOY [R]");
            return;
        }

        if (state == GameFlowController.SessionState.MissionComplete)
        {
            mainMenuTitleText.text = "MISSION WON";
            mainMenuSubtitleText.text = "All soldiers rescued. Restart for another run.";
            if (restartButton != null) restartButton.SetText("REDEPLOY [R]");
            return;
        }

        mainMenuTitleText.text = "MASH RESCUE";
        mainMenuSubtitleText.text = "Rescue every soldier and return to base.";
        if (startButton != null) startButton.SetText("START [ENTER]");
        if (restartButton != null) restartButton.SetText("RESTART [R]");
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button == null) return;
        if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
    }

    private static bool IsFullscreenEnabled()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return false;
#else
        return Screen.fullScreen;
#endif
    }

    private static void OnFullscreenChanged(bool enabled)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        Screen.fullScreen = enabled;
#endif
    }

    private static void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
    }

    private void OnControlModeChanged(int selectedIndex)
    {
        if (helicopterFlight == null) return;
        var scheme = selectedIndex <= 0
            ? HelicopterFlightController.ControlScheme.Simple
            : HelicopterFlightController.ControlScheme.Complex;
        helicopterFlight.SetControlScheme(scheme);
    }

    private void ConfigureControlModeDropdown()
    {
        if (gameplayControlModeDropdown == null) return;
        gameplayControlModeDropdown.ClearOptions();
        gameplayControlModeDropdown.AddOptions(new List<string>
        {
            "TOP DOWN",
            "THIRD PERSON"
        });

        var selected = helicopterFlight != null &&
                       helicopterFlight.CurrentControlScheme == HelicopterFlightController.ControlScheme.Complex
            ? 1
            : 0;
        gameplayControlModeDropdown.SetValueWithoutNotify(selected);
    }

    private void ConfigureDisplayModeDropdown()
    {
        if (displayModeDropdown == null) return;
        displayModeDropdown.ClearOptions();

#if UNITY_WEBGL && !UNITY_EDITOR
        displayModeDropdown.AddOptions(new List<string> { "BROWSER MANAGED" });
        displayModeDropdown.SetValueWithoutNotify(0);
        displayModeDropdown.interactable = false;
        if (displayFullscreenToggle != null) displayFullscreenToggle.gameObject.SetActive(false);
#else
        displayModeDropdown.AddOptions(new List<string>
        {
            "FULLSCREEN",
            "BORDERLESS",
            "WINDOWED"
        });
        displayModeDropdown.interactable = true;
        if (displayFullscreenToggle != null) displayFullscreenToggle.gameObject.SetActive(true);
        displayModeDropdown.SetValueWithoutNotify(GetCurrentDisplayModeIndex());
#endif
    }

    private static int GetCurrentDisplayModeIndex()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return 0;
#else
        return Screen.fullScreenMode switch
        {
            FullScreenMode.ExclusiveFullScreen => 0,
            FullScreenMode.FullScreenWindow => 1,
            FullScreenMode.MaximizedWindow => 1,
            _ => 2
        };
#endif
    }

    private void OnDisplayModeChanged(int selectedIndex)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        var mode = selectedIndex switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed
        };
        Screen.fullScreenMode = mode;
        if (displayFullscreenToggle != null)
            displayFullscreenToggle.SetIsOnWithoutNotify(mode != FullScreenMode.Windowed);
#endif
    }

    private static bool IsMenuVisible(CanvasGroup group)
    {
        return group != null &&
               group.gameObject.activeInHierarchy &&
               group.alpha > 0.01f &&
               group.interactable;
    }

    private static Transform FindDeepChildByName(Transform root, string nameOrPath)
    {
        if (root == null) return null;
        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == nameOrPath || child.GetHierarchyPath().EndsWith(nameOrPath))
                return child;
            var nested = FindDeepChildByName(child, nameOrPath);
            if (nested != null) return nested;
        }

        return null;
    }

    private void BindTerrainEvents()
    {
        if (terrainEventsSubscribed) return;
        terrainGenerator ??= FindFirstObjectByType<TerrainGenerator>();
        if (terrainGenerator == null) return;

        terrainGenerator.GenerationStarted += HandleTerrainGenerationStarted;
        terrainGenerator.GenerationCompleted += HandleTerrainGenerationCompleted;
        terrainGenerator.GenerationFailed += HandleTerrainGenerationFailed;
        terrainEventsSubscribed = true;

        if (terrainGenerator.IsGenerating)
            HandleTerrainGenerationStarted();
    }

    private void UnbindTerrainEvents()
    {
        if (!terrainEventsSubscribed) return;
        if (terrainGenerator != null)
        {
            terrainGenerator.GenerationStarted -= HandleTerrainGenerationStarted;
            terrainGenerator.GenerationCompleted -= HandleTerrainGenerationCompleted;
            terrainGenerator.GenerationFailed -= HandleTerrainGenerationFailed;
        }
        terrainEventsSubscribed = false;
    }

    private void HandleTerrainGenerationStarted()
    {
        if (!hideTerrainLoadingWithFade) return;
        isLoadingTerrain = true;
        loadingShownAtUnscaledTime = Time.unscaledTime;
        StopLoadingHideRoutine();
        RefreshUI(false);
    }

    private void HandleTerrainGenerationCompleted()
    {
        QueueHideLoadingOverlay();
    }

    private void HandleTerrainGenerationFailed(string _)
    {
        QueueHideLoadingOverlay();
    }

    private void QueueHideLoadingOverlay()
    {
        if (!hideTerrainLoadingWithFade) return;
        StopLoadingHideRoutine();
        loadingHideRoutine = StartCoroutine(HideLoadingOverlayAfterMinimum());
    }

    private void StopLoadingHideRoutine()
    {
        if (loadingHideRoutine == null) return;
        StopCoroutine(loadingHideRoutine);
        loadingHideRoutine = null;
    }

    private IEnumerator HideLoadingOverlayAfterMinimum()
    {
        var elapsed = Time.unscaledTime - loadingShownAtUnscaledTime;
        var remaining = Mathf.Max(0f, loadingMinimumVisibleTime - elapsed);
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        isLoadingTerrain = false;
        loadingHideRoutine = null;
        RefreshUI(false);
    }
}

internal static class TransformPathExtensions
{
    public static string GetHierarchyPath(this Transform transform)
    {
        if (transform == null) return string.Empty;
        var path = transform.name;
        var current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
