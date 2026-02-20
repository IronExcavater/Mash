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

    [Header("Roots")]
    [ConditionalField("autoAssignUiBindings", false)]
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

    [Header("Buttons")]
    [ConditionalField("autoAssignUiBindings", false)]
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

    [Header("HUD Text")]
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text inHelicopterText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text rescuedText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text missionPhaseText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private TMP_Text missionStateText;
    [ConditionalField("autoAssignUiBindings", false)]
    [SerializeField] private HelipadOffscreenArrow helipadIndicator;
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
    [SerializeField] private bool loadingShowsHelicopterAndSkyOnly = true;
    [ConditionalField("loadingShowsHelicopterAndSkyOnly", true)]
    [SerializeField, Min(5f)] private float loadingShowcaseHeight = 12f;
    [SerializeField, Min(0f)] private float crashMenuDelaySeconds = 3.4f;

    private bool listenersBound;
    private bool settingsOpen;
    private bool isLoadingTerrain;
    private float loadingShownAtUnscaledTime;
    private Coroutine loadingHideRoutine;
    private bool terrainEventsSubscribed;
    private readonly List<Renderer> hiddenWorldRenderers = new List<Renderer>();
    private readonly List<TerrainVisibilityState> hiddenTerrains = new List<TerrainVisibilityState>();
    private bool waitingCrashMenuDelay;
    private float crashDetectedAtUnscaledTime;
    private readonly Dictionary<CanvasGroup, Coroutine> fadeRoutines = new Dictionary<CanvasGroup, Coroutine>();
    private readonly Dictionary<CanvasGroup, bool> panelVisibilityTargets = new Dictionary<CanvasGroup, bool>();
    private bool loadingShowcaseApplied;
    private Vector3 cachedHelicopterPosition;
    private Quaternion cachedHelicopterRotation;
    private Vector3 cachedHelicopterLinearVelocity;
    private Vector3 cachedHelicopterAngularVelocity;
    private bool cachedHelicopterKinematic;
    private bool cachedHelicopterDetectCollisions;
    private RigidbodyConstraints cachedHelicopterConstraints;
    private Rigidbody cachedHelicopterBody;
    private Transform cachedHelicopterRoot;

    private void Awake()
    {
        if (crashMenuDelaySeconds < 3f) crashMenuDelaySeconds = 3.4f;
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

    private void Update()
    {
        RefreshUI(false);
        HandleMenuHotkeys();
    }

    private void ResolveReferences()
    {
        if (!autoAssignReferences) return;
        if (gameFlow == null) gameFlow = FindFirstObjectByType<GameFlowController>();
        if (helicopterCapacity == null) helicopterCapacity = FindFirstObjectByType<HelicopterCapacity>();
        if (helipadZone == null) helipadZone = FindFirstObjectByType<HelipadZone>();
        if (crashHandler == null) crashHandler = FindFirstObjectByType<HelicopterCollisionHandler>();
        if (soldierSpawner == null) soldierSpawner = FindFirstObjectByType<SoldierSpawnManager>();
        if (terrainGenerator == null) terrainGenerator = FindFirstObjectByType<TerrainGenerator>();
        if (helicopterFlight == null) helicopterFlight = FindFirstObjectByType<HelicopterFlightController>();
        if (!autoAssignUiBindings) return;

        if (mainMenuRoot == null) mainMenuRoot = FindCanvasGroupByName("MainMenuPanel");
        if (mainMenuContentRoot == null) mainMenuContentRoot = FindCanvasGroupByName("MainMenuContent");
        if (pauseMenuRoot == null) pauseMenuRoot = FindCanvasGroupByName("PauseMenuPanel");
        if (hudRoot == null) hudRoot = FindCanvasGroupByName("HUDPanel");
        if (crashRoot == null) crashRoot = FindCanvasGroupByName("CrashOverlayPanel");
        if (settingsRoot == null) settingsRoot = FindCanvasGroupByName("SettingsPanel");
        if (loadingRoot == null) loadingRoot = FindCanvasGroupByName("LoadingOverlayPanel");

        if (startButton == null) startButton = FindEvoButtonByName("StartMissionButton");
        if (resumeButton == null) resumeButton = FindEvoButtonByName("ResumeGameButton");
        if (restartButton == null) restartButton = FindEvoButtonByName("RestartGameButton");
        if (settingsButton == null) settingsButton = FindEvoButtonByName("SettingsButton");
        if (settingsBackButton == null) settingsBackButton = FindEvoButtonByName("SettingsBackButton");
        if (quitButton == null) quitButton = FindEvoButtonByName("MainMenuQuitButton");
        if (pauseQuitButton == null) pauseQuitButton = FindEvoButtonByName("PauseMenuQuitButton");
        if (crashRestartButton == null) crashRestartButton = FindEvoButtonByName("CrashRestartButton");
        if (crashQuitButton == null) crashQuitButton = FindEvoButtonByName("CrashQuitButton");

        if (inHelicopterText == null) inHelicopterText = FindTextByName("HelicopterCapacityRow/ValueLabel");
        if (rescuedText == null) rescuedText = FindTextByName("RescuedSoldiersRow/ValueLabel");
        if (missionPhaseText == null) missionPhaseText = FindTextByName("WaitingSoldiersRow/ValueLabel");
        if (missionStateText == null) missionStateText = FindTextByName("MissionStateLabel");
        if (helipadIndicator == null) helipadIndicator = FindOffscreenArrowByName("HelipadOffscreenIndicator");
        if (mainMenuTitleText == null) mainMenuTitleText = FindTextByName("MainMenuTitleLabel");
        if (mainMenuSubtitleText == null) mainMenuSubtitleText = FindTextByName("MainMenuSubtitleLabel");
        if (displayFullscreenToggle == null) displayFullscreenToggle = FindToggleByName("DisplayFullscreenToggle");
        if (displayModeDropdown == null) displayModeDropdown = FindDropdownByName("DisplayModeDropdown");
        if (soundMasterVolumeSlider == null) soundMasterVolumeSlider = FindSliderByName("SoundMasterVolumeSlider");
        if (gameplayControlModeDropdown == null) gameplayControlModeDropdown = FindDropdownByName("GameplayControlModeDropdown");
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
        var hasGameFlow = gameFlow != null;
        var state = hasGameFlow ? gameFlow.State : GameFlowController.SessionState.Playing;
        var showCrashRaw = crashHandler != null && (crashHandler.IsCrashing || crashHandler.IsCrashComplete);
        if (showCrashRaw)
        {
            if (!waitingCrashMenuDelay)
            {
                waitingCrashMenuDelay = true;
                crashDetectedAtUnscaledTime = Time.unscaledTime;
            }
        }
        else
        {
            waitingCrashMenuDelay = false;
            crashDetectedAtUnscaledTime = 0f;
        }

        var crashDelayElapsed = !showCrashRaw || (Time.unscaledTime - crashDetectedAtUnscaledTime) >= Mathf.Max(0f, crashMenuDelaySeconds);
        var showCrash = showCrashRaw && crashDelayElapsed;
        var showMenu = state == GameFlowController.SessionState.MainMenu ||
                       state == GameFlowController.SessionState.Paused ||
                       state == GameFlowController.SessionState.MissionComplete ||
                       showCrash;
        var showPause = state == GameFlowController.SessionState.Paused;
        var showHUD = state == GameFlowController.SessionState.Playing;
        var showLoading = hideTerrainLoadingWithFade && isLoadingTerrain;
        var lockHelicopter = showMenu || showLoading;

        if (!hasGameFlow)
        {
            if (showCrash) SetCanvasGroup(crashRoot, true);
        }
        else if (force)
        {
            SetCanvasGroup(mainMenuRoot, showMenu);
            SetCanvasGroup(pauseMenuRoot, false);
            SetCanvasGroup(hudRoot, showHUD && !showMenu);
            SetCanvasGroup(crashRoot, false);
            SetCanvasGroup(settingsRoot, showMenu && settingsOpen);
            SetCanvasGroup(mainMenuContentRoot, showMenu && !settingsOpen);
            SetCanvasGroup(loadingRoot, showLoading);
        }
        else
        {
            SetCanvasGroupFaded(mainMenuRoot, showMenu);
            SetCanvasGroupFaded(pauseMenuRoot, false);
            SetCanvasGroupFaded(hudRoot, showHUD && !showMenu);
            SetCanvasGroupFaded(crashRoot, false);
            SetCanvasGroupFaded(settingsRoot, showMenu && settingsOpen);
            SetCanvasGroupFaded(mainMenuContentRoot, showMenu && !settingsOpen);
            SetCanvasGroupFaded(loadingRoot, showLoading);
        }

        var boarded = helicopterCapacity != null ? helicopterCapacity.BoardedCount : 0;
        var maxSeats = helicopterCapacity != null ? helicopterCapacity.MaxSeats : 0;
        var rescued = helicopterCapacity != null ? helicopterCapacity.TotalRescuedCount : 0;
        var required = gameFlow != null ? gameFlow.RequiredSoldierCount : Mathf.Max(1, rescued);
        var notRescued = Mathf.Max(0, required - rescued);
        var phase = gameFlow != null ? gameFlow.Phase.ToString() : "Unknown";
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
            ConfigureHelipadIndicator(showHUD && !showMenu);

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
        if (loadingShowsHelicopterAndSkyOnly)
            ApplyLoadingWorldVisibility(false);
        foreach (var kv in fadeRoutines)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        if (loadingHideRoutine != null)
        {
            StopCoroutine(loadingHideRoutine);
            loadingHideRoutine = null;
        }
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

    private CanvasGroup FindCanvasGroupByName(string objectName)
    {
        var t = transform.Find(objectName);
        if (t == null) t = FindDeepChildByName(transform, objectName);
        return t != null ? t.GetComponent<CanvasGroup>() : null;
    }

    private Button FindEvoButtonByName(string objectName)
    {
        var t = FindDeepChildByName(transform, objectName);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private UnityEngine.UI.Toggle FindToggleByName(string objectName)
    {
        var t = FindDeepChildByName(transform, objectName);
        return t != null ? t.GetComponent<UnityEngine.UI.Toggle>() : null;
    }

    private UnityEngine.UI.Slider FindSliderByName(string objectName)
    {
        var t = FindDeepChildByName(transform, objectName);
        return t != null ? t.GetComponent<UnityEngine.UI.Slider>() : null;
    }

    private TMP_Dropdown FindDropdownByName(string objectName)
    {
        var t = FindDeepChildByName(transform, objectName);
        return t != null ? t.GetComponent<TMP_Dropdown>() : null;
    }

    private HelipadOffscreenArrow FindOffscreenArrowByName(string objectName)
    {
        var t = FindDeepChildByName(transform, objectName);
        return t != null ? t.GetComponent<HelipadOffscreenArrow>() : null;
    }

    private TMP_Text FindTextByName(string pathOrName)
    {
        var t = transform.Find(pathOrName);
        if (t == null) t = FindDeepChildByName(transform, pathOrName);
        if (t == null) return null;

        var direct = t.GetComponent<TMP_Text>();
        if (direct != null) return direct;

        var mainChild = t.Find("MainText");
        if (mainChild != null)
        {
            var mainText = mainChild.GetComponent<TMP_Text>();
            if (mainText != null) return mainText;
        }

        var texts = t.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null) continue;
            if (text.name == "ShadowText" || text.name == "ShadowCopy") continue;
            return text;
        }

        return null;
    }

    private void ConfigureHelipadIndicator(bool shouldBeVisible)
    {
        if (helipadIndicator == null) return;

        var cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        var canvas = GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;

        if (helipadZone == null || cam == null || canvasRect == null)
        {
            if (helipadIndicator.gameObject.activeSelf)
                helipadIndicator.gameObject.SetActive(false);
            return;
        }

        helipadIndicator.SetTarget(helipadZone.transform, cam, canvasRect);

        if (!helipadIndicator.gameObject.activeSelf && shouldBeVisible)
            helipadIndicator.gameObject.SetActive(true);
        else if (helipadIndicator.gameObject.activeSelf != shouldBeVisible)
            helipadIndicator.gameObject.SetActive(shouldBeVisible);
    }

    private void HandleMenuHotkeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        var mainShown = IsMenuVisible(mainMenuRoot);
        var pauseShown = gameFlow != null && gameFlow.State == GameFlowController.SessionState.Paused && mainShown;
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

        if (gameFlow != null && gameFlow.State == GameFlowController.SessionState.MainMenu &&
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

        var selected = 0;
        if (helicopterFlight != null && helicopterFlight.CurrentControlScheme == HelicopterFlightController.ControlScheme.Complex)
            selected = 1;
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
        if (terrainGenerator == null) terrainGenerator = FindFirstObjectByType<TerrainGenerator>();
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
        if (loadingShowsHelicopterAndSkyOnly)
            ApplyLoadingWorldVisibility(true);
        if (loadingHideRoutine != null)
        {
            StopCoroutine(loadingHideRoutine);
            loadingHideRoutine = null;
        }
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
        if (loadingHideRoutine != null) StopCoroutine(loadingHideRoutine);
        loadingHideRoutine = StartCoroutine(HideLoadingOverlayAfterMinimum());
    }

    private IEnumerator HideLoadingOverlayAfterMinimum()
    {
        var elapsed = Time.unscaledTime - loadingShownAtUnscaledTime;
        var remaining = Mathf.Max(0f, loadingMinimumVisibleTime - elapsed);
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        isLoadingTerrain = false;
        if (loadingShowsHelicopterAndSkyOnly)
            ApplyLoadingWorldVisibility(false);
        if (helicopterFlight != null)
            helicopterFlight.ReapplyStartupPlacementNow();
        loadingHideRoutine = null;
        RefreshUI(false);
    }

    private void ApplyLoadingWorldVisibility(bool hideWorld)
    {
        if (hideWorld)
        {
            hiddenWorldRenderers.Clear();
            hiddenTerrains.Clear();
            var helicopterRoot = ResolveHelicopterRoot();
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (helicopterRoot != null && r.transform.IsChildOf(helicopterRoot)) continue;
                r.enabled = false;
                hiddenWorldRenderers.Add(r);
            }

            var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null) continue;
                hiddenTerrains.Add(new TerrainVisibilityState
                {
                    terrain = terrain,
                    drawHeightmap = terrain.drawHeightmap,
                    drawTreesAndFoliage = terrain.drawTreesAndFoliage
                });
                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
            }

            ApplyHelicopterLoadingShowcase(true);
        }
        else
        {
            for (var i = 0; i < hiddenWorldRenderers.Count; i++)
            {
                var r = hiddenWorldRenderers[i];
                if (r != null) r.enabled = true;
            }
            hiddenWorldRenderers.Clear();

            for (var i = 0; i < hiddenTerrains.Count; i++)
            {
                var state = hiddenTerrains[i];
                if (state.terrain == null) continue;
                state.terrain.drawHeightmap = state.drawHeightmap;
                state.terrain.drawTreesAndFoliage = state.drawTreesAndFoliage;
            }
            hiddenTerrains.Clear();

            ApplyHelicopterLoadingShowcase(false);
        }
    }

    private void HideRenderersUnder(GameObject root)
    {
        if (root == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled) continue;
            r.enabled = false;
            hiddenWorldRenderers.Add(r);
        }
    }

    private Transform ResolveHelicopterRoot()
    {
        if (helicopterFlight != null) return helicopterFlight.transform;
        if (helicopterCapacity != null) return helicopterCapacity.transform;
        if (crashHandler != null) return crashHandler.transform;
        return null;
    }

    private void ApplyHelicopterLoadingShowcase(bool enabled)
    {
        if (!loadingShowsHelicopterAndSkyOnly) return;
        var root = ResolveHelicopterRoot();
        if (root == null) return;

        if (enabled)
        {
            if (loadingShowcaseApplied) return;
            cachedHelicopterRoot = root;
            cachedHelicopterPosition = root.position;
            cachedHelicopterRotation = root.rotation;
            cachedHelicopterBody = root.GetComponent<Rigidbody>();
            if (cachedHelicopterBody != null)
            {
                cachedHelicopterLinearVelocity = cachedHelicopterBody.linearVelocity;
                cachedHelicopterAngularVelocity = cachedHelicopterBody.angularVelocity;
                cachedHelicopterKinematic = cachedHelicopterBody.isKinematic;
                cachedHelicopterDetectCollisions = cachedHelicopterBody.detectCollisions;
                cachedHelicopterConstraints = cachedHelicopterBody.constraints;
                cachedHelicopterBody.linearVelocity = Vector3.zero;
                cachedHelicopterBody.angularVelocity = Vector3.zero;
                cachedHelicopterBody.isKinematic = true;
                cachedHelicopterBody.detectCollisions = false;
                cachedHelicopterBody.constraints = RigidbodyConstraints.FreezeAll;
            }

            var euler = root.rotation.eulerAngles;
            root.rotation = Quaternion.Euler(0f, euler.y, 0f);
            root.position = new Vector3(root.position.x, loadingShowcaseHeight, root.position.z);
            loadingShowcaseApplied = true;
            return;
        }

        if (!loadingShowcaseApplied) return;
        if (cachedHelicopterRoot != null)
        {
            cachedHelicopterRoot.position = cachedHelicopterPosition;
            cachedHelicopterRoot.rotation = cachedHelicopterRotation;
        }

        if (cachedHelicopterBody != null)
        {
            cachedHelicopterBody.isKinematic = cachedHelicopterKinematic;
            cachedHelicopterBody.detectCollisions = cachedHelicopterDetectCollisions;
            cachedHelicopterBody.constraints = cachedHelicopterConstraints;
            cachedHelicopterBody.linearVelocity = cachedHelicopterLinearVelocity;
            cachedHelicopterBody.angularVelocity = cachedHelicopterAngularVelocity;
        }

        cachedHelicopterRoot = null;
        cachedHelicopterBody = null;
        loadingShowcaseApplied = false;
    }

    private struct TerrainVisibilityState
    {
        public Terrain terrain;
        public bool drawHeightmap;
        public bool drawTreesAndFoliage;
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
