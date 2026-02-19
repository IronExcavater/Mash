using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

[AddComponentMenu("Gameplay/Game Flow Controller")]
public class GameFlowController : MonoBehaviour
{
    private static bool forceStartPlayingOnNextLoad;

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
    [SerializeField] private bool useActionReferences = true;
    [ConditionalField("useActionReferences", true)]
    [SerializeField] private InputActionReference startActionReference;
    [ConditionalField("useActionReferences", true)]
    [SerializeField] private InputActionReference pauseActionReference;
    [ConditionalField("useActionReferences", true)]
    [SerializeField] private InputActionReference resetActionReference;
    [ConditionalField("useActionReferences", false)]
    [SerializeField] private bool autoAssignInputAsset = true;
    [ConditionalField("useActionReferences", false)]
    [SerializeField] private InputActionAsset inputActions;
    [ConditionalField("useActionReferences", false)]
    [SerializeField] private string startActionPath = "Player/Attack";
    [ConditionalField("useActionReferences", false)]
    [SerializeField] private string pauseActionPath = "Player/Pause";
    [ConditionalField("useActionReferences", false)]
    [SerializeField] private string resetActionPath = "Player/Reset";
    [SerializeField] private bool autoEnableActions = true;

    [Header("Rules")]
    [SerializeField] private bool startInMainMenu = true;
    [SerializeField] private bool autoSpawnSoldiersOnPlay = true;
    [SerializeField, Min(1)] private int requiredSoldierCount = 10;
    [SerializeField, Min(0f)] private float missionCompleteDelaySeconds = 1f;
    [SerializeField, Min(0f)] private float pauseFadeDuration = 0.22f;
    [SerializeField, Min(0f)] private float resumeFadeDuration = 0.14f;

    [Header("Events")]
    [SerializeField] private UnityEvent<SessionState> onSessionStateChanged;
    [SerializeField] private UnityEvent<MissionPhase> onMissionPhaseChanged;

    public SessionState State { get; private set; } = SessionState.MainMenu;
    public MissionPhase Phase { get; private set; } = MissionPhase.WaitingForBoarding;
    public int RequiredSoldierCount => Mathf.Max(1, requiredSoldierCount);
    public event Action<SessionState, SessionState> SessionStateChanged;
    public event Action<MissionPhase, MissionPhase> MissionPhaseChanged;

    private float baseFixedDeltaTime;
    private bool soldiersSpawned;
    private InputAction startAction;
    private InputAction pauseAction;
    private InputAction resetAction;
    private Coroutine timeScaleRoutine;
    private Coroutine missionCompleteRoutine;
    private bool missionCompletePending;

    private void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
        AutoResolveReferences();
    }

    private void Start()
    {
        ResolveInputActions();
        if (autoEnableActions) SetActionsEnabled(true);

        SetPhase(MissionPhase.WaitingForBoarding);
        var forceStartPlaying = forceStartPlayingOnNextLoad;
        forceStartPlayingOnNextLoad = false;
        SetSessionState(forceStartPlaying || !startInMainMenu ? SessionState.Playing : SessionState.MainMenu);
    }

    private void OnEnable()
    {
        ResolveInputActions();
        if (autoEnableActions) SetActionsEnabled(true);
    }

    private void OnDisable()
    {
        if (autoEnableActions) SetActionsEnabled(false);
        if (missionCompleteRoutine != null)
        {
            StopCoroutine(missionCompleteRoutine);
            missionCompleteRoutine = null;
        }
        missionCompletePending = false;
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
        ResetGame(false);
    }

    public void ResetGame(bool startImmediately)
    {
        forceStartPlayingOnNextLoad = startImmediately;
        SetSimulationScale(1f, true);
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
        if (helicopterCapacity == null) return;
        if (State == SessionState.MissionComplete) return;

        var boarded = helicopterCapacity.BoardedCount;
        var rescued = helicopterCapacity.TotalRescuedCount;
        var effectiveRequired = Mathf.Max(1, requiredSoldierCount);

        if (rescued >= effectiveRequired)
        {
            SetPhase(MissionPhase.Complete);
            if (!missionCompletePending && State != SessionState.MissionComplete)
            {
                missionCompletePending = true;
                missionCompleteRoutine = StartCoroutine(DelayedMissionComplete());
            }
            return;
        }

        if (boarded > 0) SetPhase(MissionPhase.ReadyToExtract);
        else if (rescued > 0) SetPhase(MissionPhase.Boarding);
        else SetPhase(MissionPhase.WaitingForBoarding);
    }

    private IEnumerator DelayedMissionComplete()
    {
        var delay = Mathf.Max(0f, missionCompleteDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        missionCompletePending = false;
        missionCompleteRoutine = null;
        if (State != SessionState.MissionComplete)
            SetSessionState(SessionState.MissionComplete);
    }

    private void SetSessionState(SessionState next)
    {
        if (State == next) return;
        var previous = State;
        State = next;

        switch (State)
        {
            case SessionState.MainMenu:
                SetSimulationScale(0f, true);
                SetGameplayInputEnabled(false);
                break;
            case SessionState.Playing:
                SetSimulationScale(1f, previous == SessionState.Paused ? false : true);
                SetGameplayInputEnabled(true);
                if (autoSpawnSoldiersOnPlay && !soldiersSpawned && soldierSpawner != null)
                {
                    soldierSpawner.SpawnSoldiers();
                    soldiersSpawned = true;
                }
                break;
            case SessionState.Paused:
                SetSimulationScale(0f, false);
                SetGameplayInputEnabled(false);
                break;
            case SessionState.MissionComplete:
                SetSimulationScale(0f, true);
                SetGameplayInputEnabled(false);
                break;
        }

        onSessionStateChanged?.Invoke(State);
        SessionStateChanged?.Invoke(previous, State);
    }

    private void SetPhase(MissionPhase next)
    {
        if (Phase == next) return;
        var previous = Phase;
        Phase = next;
        onMissionPhaseChanged?.Invoke(Phase);
        MissionPhaseChanged?.Invoke(previous, Phase);
    }

    private void SetSimulationScale(float scale, bool instant)
    {
        var target = Mathf.Clamp(scale, 0f, 1f);
        if (instant)
        {
            if (timeScaleRoutine != null)
            {
                StopCoroutine(timeScaleRoutine);
                timeScaleRoutine = null;
            }
            Time.timeScale = target;
            Time.fixedDeltaTime = baseFixedDeltaTime * Mathf.Max(0f, Time.timeScale);
            return;
        }

        var duration = target < Time.timeScale ? pauseFadeDuration : resumeFadeDuration;
        if (duration <= 0.001f)
        {
            Time.timeScale = target;
            Time.fixedDeltaTime = baseFixedDeltaTime * Mathf.Max(0f, Time.timeScale);
            return;
        }

        if (timeScaleRoutine != null) StopCoroutine(timeScaleRoutine);
        timeScaleRoutine = StartCoroutine(AnimateTimeScale(Time.timeScale, target, duration));
    }

    private IEnumerator AnimateTimeScale(float start, float target, float duration)
    {
        var t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k);
            Time.timeScale = Mathf.Lerp(start, target, k);
            Time.fixedDeltaTime = baseFixedDeltaTime * Mathf.Max(0f, Time.timeScale);
            yield return null;
        }

        Time.timeScale = target;
        Time.fixedDeltaTime = baseFixedDeltaTime * Mathf.Max(0f, Time.timeScale);
        timeScaleRoutine = null;
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

    private void ResolveInputActions()
    {
        if (useActionReferences)
        {
            startAction = startActionReference != null ? startActionReference.action : null;
            pauseAction = pauseActionReference != null ? pauseActionReference.action : null;
            resetAction = resetActionReference != null ? resetActionReference.action : null;
            return;
        }

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
