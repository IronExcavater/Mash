using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[AddComponentMenu("Gameplay/Game Flow Controller")]
public class GameFlowController : MonoBehaviour
{
    public enum SessionState
    {
        MainMenu = 0,
        Playing = 1,
        Paused = 2,
        MissionComplete = 3
    }

    public enum MissionPhase
    {
        WaitingForBoarding = 0,
        Boarding = 1,
        ReadyToExtract = 2,
        Complete = 3
    }

    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterFlightController helicopterFlight;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private SoldierSpawnManager soldierSpawner;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelipadZone helipadZone;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterCapacity helicopterCapacity;

    [Header("Input")]
    [SerializeField] private bool autoAssignInputAsset = true;
    [ConditionalField("autoAssignInputAsset", false)]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string startActionPath = "Player/Attack";
    [SerializeField] private string pauseActionPath = "Player/Pause";
    [SerializeField] private string resetActionPath = "Player/Reset";
    [SerializeField] private bool autoEnableActions = true;

    [Header("Rules")]
    [SerializeField] private bool startInMainMenu = true;
    [SerializeField] private bool autoSpawnSoldiersOnPlay = true;
    [SerializeField, Min(1)] private int requiredSoldierCount = 6;
    [SerializeField, Min(10f)] private float extractionDistanceFromHelipad = 70f;

    [Header("Helicopter Ground Hold")]
    [SerializeField] private bool enforceLowGroundHold = true;
    [SerializeField, Min(0.5f)] private float lowGroundClearance = 4.5f;
    [SerializeField, Min(0.1f)] private float lowGroundHoldResponse = 3.4f;
    [SerializeField, Min(0.1f)] private float lowGroundHoldDamping = 1.8f;
    [SerializeField, Min(0.1f)] private float lowGroundMaxCorrectionSpeed = 10f;

    [Header("Events")]
    [SerializeField] private UnityEvent<SessionState> onSessionStateChanged;
    [SerializeField] private UnityEvent<MissionPhase> onMissionPhaseChanged;

    public SessionState State { get; private set; } = SessionState.MainMenu;
    public MissionPhase Phase { get; private set; } = MissionPhase.WaitingForBoarding;

    private float baseFixedDeltaTime;
    private bool soldiersSpawned;
    private Vector3 helipadCenter;
    private InputAction startAction;
    private InputAction pauseAction;
    private InputAction resetAction;

    private void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
        AutoResolveReferences();
    }

    private void Start()
    {
        ResolveInputActions();
        if (autoEnableActions) SetActionsEnabled(true);
        ApplyHelicopterGroundHoldIfNeeded();

        if (helipadZone != null) helipadCenter = helipadZone.transform.position;
        SetPhase(MissionPhase.WaitingForBoarding);
        SetSessionState(startInMainMenu ? SessionState.MainMenu : SessionState.Playing);
    }

    private void OnEnable()
    {
        ResolveInputActions();
        if (autoEnableActions) SetActionsEnabled(true);
    }

    private void OnDisable()
    {
        if (autoEnableActions) SetActionsEnabled(false);
    }

    private void Update()
    {
        HandleInput();
        UpdateMissionProgress();
    }

    public void StartGame()
    {
        SetSessionState(SessionState.Playing);
    }

    public void TogglePause()
    {
        if (State == SessionState.Playing) SetSessionState(SessionState.Paused);
        else if (State == SessionState.Paused) SetSessionState(SessionState.Playing);
    }

    public void ResetGame()
    {
        SetSimulationScale(1f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleInput()
    {
        if (resetAction != null && resetAction.WasPressedThisFrame())
        {
            ResetGame();
            return;
        }

        if (State == SessionState.MainMenu)
        {
            if (startAction != null && startAction.WasPressedThisFrame()) StartGame();
            return;
        }

        if (pauseAction != null && pauseAction.WasPressedThisFrame() && State != SessionState.MissionComplete)
            TogglePause();
    }

    private void UpdateMissionProgress()
    {
        if (helicopterCapacity == null || helipadZone == null) return;
        if (State == SessionState.MissionComplete) return;

        var boarded = helicopterCapacity.BoardedCount;
        var waiting = helipadZone.SoldiersWaitingCount;
        var effectiveRequired = Mathf.Max(1, requiredSoldierCount);

        if (boarded >= effectiveRequired)
        {
            SetPhase(MissionPhase.ReadyToExtract);
            var dist = Vector3.Distance(helicopterCapacity.transform.position, helipadCenter);
            if (dist >= extractionDistanceFromHelipad)
            {
                SetPhase(MissionPhase.Complete);
                SetSessionState(SessionState.MissionComplete);
            }

            return;
        }

        if (waiting > 0 || boarded > 0) SetPhase(MissionPhase.Boarding);
        else SetPhase(MissionPhase.WaitingForBoarding);
    }

    private void SetSessionState(SessionState next)
    {
        if (State == next) return;
        State = next;

        switch (State)
        {
            case SessionState.MainMenu:
                SetSimulationScale(0f);
                SetGameplayInputEnabled(false);
                break;
            case SessionState.Playing:
                SetSimulationScale(1f);
                SetGameplayInputEnabled(true);
                if (autoSpawnSoldiersOnPlay && !soldiersSpawned && soldierSpawner != null)
                {
                    soldierSpawner.SpawnSoldiers();
                    soldiersSpawned = true;
                }
                break;
            case SessionState.Paused:
                SetSimulationScale(0f);
                SetGameplayInputEnabled(false);
                break;
            case SessionState.MissionComplete:
                SetSimulationScale(0f);
                SetGameplayInputEnabled(false);
                break;
        }

        onSessionStateChanged?.Invoke(State);
    }

    private void SetPhase(MissionPhase next)
    {
        if (Phase == next) return;
        Phase = next;
        onMissionPhaseChanged?.Invoke(Phase);
    }

    private void SetSimulationScale(float scale)
    {
        Time.timeScale = Mathf.Clamp(scale, 0f, 1f);
        Time.fixedDeltaTime = baseFixedDeltaTime * Mathf.Max(0f, Time.timeScale);
    }

    private void SetGameplayInputEnabled(bool enabled)
    {
        if (helicopterFlight != null) helicopterFlight.SetInputEnabled(enabled);
    }

    private void AutoResolveReferences()
    {
        if (!autoAssignReferences) return;
        if (helicopterFlight == null) helicopterFlight = FindFirstObjectByType<HelicopterFlightController>();
        if (soldierSpawner == null) soldierSpawner = FindFirstObjectByType<SoldierSpawnManager>();
        if (helipadZone == null) helipadZone = FindFirstObjectByType<HelipadZone>();
        if (helicopterCapacity == null) helicopterCapacity = FindFirstObjectByType<HelicopterCapacity>();
    }

    private void ApplyHelicopterGroundHoldIfNeeded()
    {
        if (!enforceLowGroundHold || helicopterFlight == null) return;
        helicopterFlight.ApplyGroundHoldProfile(
            lowGroundClearance,
            lowGroundHoldResponse,
            lowGroundHoldDamping,
            lowGroundMaxCorrectionSpeed);
    }

    private void ResolveInputActions()
    {
        if (autoAssignInputAsset && inputActions == null)
        {
            var playerInput = FindFirstObjectByType<PlayerInput>();
            if (playerInput != null) inputActions = playerInput.actions;
        }

        startAction = FindAction(startActionPath);
        pauseAction = FindAction(pauseActionPath);
        resetAction = FindAction(resetActionPath);
    }

    private InputAction FindAction(string path)
    {
        if (inputActions == null || string.IsNullOrWhiteSpace(path)) return null;
        return inputActions.FindAction(path, false);
    }

    private void SetActionsEnabled(bool enabled)
    {
        SetActionEnabled(startAction, enabled);
        SetActionEnabled(pauseAction, enabled);
        SetActionEnabled(resetAction, enabled);
    }

    private static void SetActionEnabled(InputAction action, bool enabled)
    {
        if (action == null) return;
        if (enabled)
        {
            if (!action.enabled) action.Enable();
            return;
        }

        if (action.enabled) action.Disable();
    }
}
