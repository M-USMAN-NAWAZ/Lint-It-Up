using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRFootballScenarioController : MonoBehaviour
{
    public enum ScenarioTaskType
    {
        CatchBall,
        ReachZone,
        ReachWithHand,
        ThrowBallToTarget
    }

    [System.Serializable]
    public class ScenarioTask
    {
        public string title = "Task";
        [TextArea(2, 4)] public string instruction = "Complete the objective.";
        [TextArea(1, 3)] public string controlHint = "Use your hands to complete the task.";
        public ScenarioTaskType taskType;
        public Transform target;
        public float completionRadius = 1f;
        public float taskDuration = 3f;
        [Range(0.05f, 1f)] public float slowMotionScale = 0.25f;
        public float slowMotionDuration = 1.25f;
        public bool requireBallInHand;
    }

    [Header("Core References")]
    [SerializeField] FormationRunController formationController;
    [SerializeField] VRFootballScenarioUI scenarioUI;
    [SerializeField] XRGrabInteractable football;
    [SerializeField] Transform userRoot;
    [SerializeField] Transform leftHand;
    [SerializeField] Transform rightHand;
    [SerializeField] Transform ballCatchTarget;
    [SerializeField] Transform objectiveIndicator;
    [SerializeField] Transform handObjectiveIndicator;
    [SerializeField] LineRenderer throwTrajectoryLine;

    [Header("Pass Setup")]
    [SerializeField] Transform passOrigin;
    [SerializeField] float passStartDelay = 0.4f;
    [SerializeField] float delayedRunnerStartDelay = 1.5f;
    [SerializeField] float passTravelTime = 0.7f;
    [SerializeField] AnimationCurve passArc = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
    [SerializeField] float passArcHeight = 0.45f;
    [SerializeField] float passSpinStrength = 14f;
    [SerializeField] bool keepCountdownVisibleUntilBallToss = true;

    [Header("Goal Throw")]
    [SerializeField] float throwDirectionAcceptanceDot = 0.55f;
    [SerializeField] float minimumThrowSpeed = 0.35f;
    [SerializeField] float goalThrowTravelTime = 0.75f;
    [SerializeField] AnimationCurve goalThrowArc = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
    [SerializeField] float goalThrowArcHeight = 0.9f;
    [SerializeField] int throwTrajectoryResolution = 20;

    [Header("Goal Receiver Sync")]
    [SerializeField] bool waitForGoalReceiverOnFinalThrow = true;
    [SerializeField] string goalReceiverName = "Goal";
    [SerializeField] Transform goalReceiver;
    [SerializeField] Transform goalBallHoldAnchor;
    [SerializeField] float goalReceiverReadyRadius = 1.5f;
    [SerializeField] float goalCatchDistance = 2f;
    [SerializeField] float goalCatchHoldDuration = 1.2f;
    [SerializeField] Vector3 goalCaughtBallLocalOffset = new Vector3(0f, 1.1f, 0.35f);

    [Header("Flow")]
    [SerializeField] bool autoStartOnPlay = true;
    [SerializeField] bool disablePlayerControllerTasks;
    [SerializeField] bool startFormationFromScenario = true;
    [SerializeField] bool startPlayerTeam = true;
    [SerializeField] bool startOpponentTeam = true;
    [SerializeField] int countdownStart = 3;
    [SerializeField] float countdownSecondsPerStep = 1f;
    [SerializeField] float interTaskDelay = 0.15f;
    [SerializeField] float failureScreenDuration = 1.5f;

    [Header("Win Flow")]
    [SerializeField] string winSceneName = "Theater";
    [SerializeField] float winScreenDuration = 3f;
    [SerializeField] string winTitle = "You Win!";
    [SerializeField] string winDescription = "Play complete. Moving to the theater...";

    [Header("Scenario Tasks")]
    [SerializeField] List<ScenarioTask> tasks = new List<ScenarioTask>();

    bool ballHeldByUser;
    bool ballReleasedByUserThisTask;
    bool scenarioRunning;
    bool isPassingBall;
    bool isGuidingGoalThrow;
    bool goalThrowLaunched;
    bool goalThrowCompleted;
    bool goalThrowDirectionAccepted;
    bool goalCatchTriggered;
    bool queuedGoalThrow;
    bool taskFailedEarly;
    bool lockBallAtPassOrigin;
    bool ballTossStarted;
    int currentTaskIndex = -1;
    float defaultFixedDeltaTime;
    float closestGoalThrowDistance = float.MaxValue;
    float goalThrowWatchUntilTime;
    Rigidbody footballBody;
    Transform selectedBallHand;
    Transform caughtBallHolder;
    Transform queuedGoalThrowHoldTarget;
    string earlyFailureMessage = string.Empty;
    Coroutine goalThrowReleaseRoutine;
    Coroutine goalCatchRoutine;

    public List<ScenarioTask> Tasks => tasks;
    public Transform ObjectiveIndicator
    {
        get => objectiveIndicator;
        set => objectiveIndicator = value;
    }

    void Reset()
    {
        if (tasks.Count > 0)
        {
            return;
        }

        tasks = new List<ScenarioTask>
        {
            new ScenarioTask
            {
                title = "Catch The Ball",
                instruction = "Catch the ball from the passer after hut hut.",
                controlHint = "Pinch or grab with either hand to catch the ball.",
                taskType = ScenarioTaskType.CatchBall,
                taskDuration = 4f,
                slowMotionScale = 0.22f,
                slowMotionDuration = 1.25f,
            },
            new ScenarioTask
            {
                title = "Run To Faker",
                instruction = "Run toward the faker to sell the play.",
                controlHint = "Move your VR body toward the highlighted faker spot while holding the ball.",
                taskType = ScenarioTaskType.ReachZone,
                taskDuration = 3f,
                slowMotionScale = 0.25f,
                slowMotionDuration = 1.1f,
                completionRadius = 1.1f,
                requireBallInHand = true,
            },
            new ScenarioTask
            {
                title = "Fake The Hand Off",
                instruction = "Stretch your arm toward the faker.",
                controlHint = "Extend the hand holding the ball toward the fake target.",
                taskType = ScenarioTaskType.ReachWithHand,
                taskDuration = 2.5f,
                slowMotionScale = 0.2f,
                slowMotionDuration = 1.1f,
                completionRadius = 0.55f,
                requireBallInHand = true,
            },
            new ScenarioTask
            {
                title = "Run To Safety",
                instruction = "Move to the safe spot before the defense closes in.",
                controlHint = "Keep the ball in hand and move your body to the safe zone.",
                taskType = ScenarioTaskType.ReachZone,
                taskDuration = 3f,
                slowMotionScale = 0.25f,
                slowMotionDuration = 1.1f,
                completionRadius = 1.1f,
                requireBallInHand = true,
            },
            new ScenarioTask
            {
                title = "Throw To Goal",
                instruction = "Throw the ball to the goal player.",
                controlHint = "Release the ball toward the goal target with a throwing motion.",
                taskType = ScenarioTaskType.ThrowBallToTarget,
                taskDuration = 4f,
                slowMotionScale = 0.2f,
                slowMotionDuration = 1.25f,
                completionRadius = 1f,
                requireBallInHand = true,
            }
        };
    }

    void OnEnable()
    {
        if (football != null)
        {
            football.selectEntered.AddListener(OnBallSelectEntered);
            football.selectExited.AddListener(OnBallSelectExited);
        }

        UpdateThrowTrajectoryLine();
    }

    void OnDisable()
    {
        RestoreNormalTime();

        if (football != null)
        {
            football.selectEntered.RemoveListener(OnBallSelectEntered);
            football.selectExited.RemoveListener(OnBallSelectExited);
        }

        SetThrowTrajectoryVisible(false);
    }

    void Start()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        footballBody = football != null ? football.GetComponent<Rigidbody>() : null;
        if (football != null)
        {
            football.useDynamicAttach = false;
            football.matchAttachPosition = true;
            football.matchAttachRotation = true;
        }

        ResolveGoalReceiver();

        SetThrowTrajectoryVisible(false);
        lockBallAtPassOrigin = true;
        SnapBallToPassOrigin();

        if (autoStartOnPlay)
        {
            StartScenario();
        }
    }

    void Update()
    {
        MaintainBallAtPassOrigin();
        UpdateGoalThrowState();
        UpdateThrowTrajectoryLine();
    }

    void LateUpdate()
    {
        if (ballHeldByUser && football != null && football.isSelected && selectedBallHand != null)
        {
            SnapFootballToTransform(selectedBallHand, false);
        }

        if (caughtBallHolder != null)
        {
            HoldFootballAtGoalReceiver();
        }

        if (queuedGoalThrow)
        {
            MaintainQueuedGoalThrow();
        }
    }

    public void StartScenario()
    {
        if (scenarioRunning)
        {
            return;
        }

        StartCoroutine(disablePlayerControllerTasks ? RunFormationAnimationTest() : RunScenario());
    }

    IEnumerator RunFormationAnimationTest()
    {
        scenarioRunning = true;
        RestoreNormalTime();
        lockBallAtPassOrigin = true;
        caughtBallHolder = null;
        SnapBallToPassOrigin();

        if (scenarioUI != null)
        {
            scenarioUI.HideAll();
        }

        UpdateObjectiveIndicator(null, false);
        UpdateHandObjectiveIndicator(null, false);
        SetThrowTrajectoryVisible(false);

        if (formationController != null)
        {
            formationController.PrepareOpeningPose();
        }

        for (var count = countdownStart; count >= 1; count--)
        {
            if (scenarioUI != null)
            {
                scenarioUI.ShowCountdown(count);
            }

            yield return new WaitForSecondsRealtime(countdownSecondsPerStep);
        }

        if (scenarioUI != null)
        {
            scenarioUI.HideCountdown();
        }

        if (startFormationFromScenario && formationController != null)
        {
            formationController.BeginAfterCountdownWithoutHutHut(startPlayerTeam, startOpponentTeam);
            StartCoroutine(StartDelayedRunnersAfterDelay());
        }

        if (passStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(passStartDelay);
        }

        if (formationController != null)
        {
            formationController.TriggerDeferredHutHut();
        }

        var catchTarget = ballCatchTarget != null ? ballCatchTarget : GetPreferredCatchTarget();
        if (football != null && passOrigin != null && catchTarget != null)
        {
            lockBallAtPassOrigin = false;
            ballTossStarted = true;
            LaunchBallPass(catchTarget);
        }

        scenarioRunning = false;
    }

    IEnumerator RunScenario()
    {
        scenarioRunning = true;
        RestoreNormalTime();
        lockBallAtPassOrigin = true;
        caughtBallHolder = null;
        SnapBallToPassOrigin();

        if (scenarioUI != null)
        {
            scenarioUI.HideAll();
        }

        UpdateObjectiveIndicator(null, false);
        UpdateHandObjectiveIndicator(null, false);

        if (formationController != null)
        {
            formationController.PrepareOpeningPose();
        }

        for (var count = countdownStart; count >= 1; count--)
        {
            if (scenarioUI != null)
            {
                scenarioUI.ShowCountdown(count);
            }

            yield return new WaitForSecondsRealtime(countdownSecondsPerStep);
        }

        if (scenarioUI != null)
        {
            if (!keepCountdownVisibleUntilBallToss || tasks.Count == 0 || tasks[0].taskType != ScenarioTaskType.CatchBall)
            {
                scenarioUI.HideCountdown();
            }
        }

        if (startFormationFromScenario && formationController != null)
        {
            formationController.BeginAfterCountdownWithoutHutHut(startPlayerTeam, startOpponentTeam);
            StartCoroutine(StartDelayedRunnersAfterDelay());
        }

        for (var i = 0; i < tasks.Count; i++)
        {
            currentTaskIndex = i;
            ballReleasedByUserThisTask = false;
            goalThrowLaunched = false;
            goalThrowCompleted = false;
            goalThrowDirectionAccepted = false;
            goalCatchTriggered = false;
            queuedGoalThrow = false;
            queuedGoalThrowHoldTarget = null;
            closestGoalThrowDistance = float.MaxValue;
            goalThrowWatchUntilTime = 0f;
            taskFailedEarly = false;
            earlyFailureMessage = string.Empty;

            var task = tasks[i];
            var completed = false;
            yield return RunTask(task, result => completed = result);
            if (!completed)
            {
                yield return FailAndRestart();
                yield break;
            }

            if (interTaskDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(interTaskDelay);
            }
        }

        currentTaskIndex = -1;
        scenarioRunning = false;
        RestoreNormalTime();
        UpdateObjectiveIndicator(null, false);
        UpdateHandObjectiveIndicator(null, false);
        SetThrowTrajectoryVisible(false);

        if (scenarioUI != null)
        {
            scenarioUI.ShowSuccess(winTitle, winDescription);
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, winScreenDuration));

        if (!string.IsNullOrWhiteSpace(winSceneName))
        {
            SceneManager.LoadScene(winSceneName);
        }
    }

    IEnumerator RunTask(ScenarioTask task, System.Action<bool> onComplete)
    {
        if (task == null)
        {
            onComplete?.Invoke(true);
            yield break;
        }

        UpdateObjectiveIndicator(GetTaskIndicatorTarget(task), true);
        UpdateHandObjectiveIndicator(GetHandIndicatorTarget(task), true);
        UpdateThrowTrajectoryLine();

        ApplySlowMotion(task.slowMotionScale);

        if (task.taskType == ScenarioTaskType.CatchBall)
        {
            yield return StartCatchBallSequence(task);
        }

        var elapsed = 0f;
        var duration = Mathf.Max(0.1f, task.taskDuration);
        var slowMotionDuration = Mathf.Clamp(task.slowMotionDuration, 0f, duration);
        var slowMotionActive = slowMotionDuration > 0f;

        while (elapsed < duration || ShouldKeepWatchingGoalThrow(task) || ShouldWaitForGoalReceiver(task))
        {
            var waitingForGoalReceiver = ShouldWaitForGoalReceiver(task);
            if (waitingForGoalReceiver && slowMotionActive)
            {
                RestoreNormalTime();
                slowMotionActive = false;
            }

            var remaining = Mathf.Max(duration - elapsed, GetGoalThrowWatchRemaining(task));
            var shouldShowTaskUi = task.taskType != ScenarioTaskType.CatchBall || !keepCountdownVisibleUntilBallToss || ballTossStarted;
            if (scenarioUI != null && shouldShowTaskUi)
            {
                scenarioUI.ShowTask(task.title, task.instruction, task.controlHint, remaining);
            }

            if (EvaluateTask(task))
            {
                RestoreNormalTime();
                if (scenarioUI != null)
                {
                    scenarioUI.HideTask();
                }

                UpdateObjectiveIndicator(GetNextObjectiveTarget(), true);
                UpdateHandObjectiveIndicator(GetNextHandIndicatorTarget(), true);
                UpdateThrowTrajectoryLine();

                onComplete?.Invoke(true);
                yield break;
            }

            if (taskFailedEarly)
            {
                break;
            }

            if (!waitingForGoalReceiver)
            {
                elapsed += Time.unscaledDeltaTime;
            }

            if (slowMotionActive && elapsed >= slowMotionDuration)
            {
                RestoreNormalTime();
                slowMotionActive = false;
            }

            yield return null;
        }

        RestoreNormalTime();
        if (scenarioUI != null)
        {
            scenarioUI.ShowFailure("You Failed!", GetFailureMessage(task));
        }
        UpdateObjectiveIndicator(GetTaskIndicatorTarget(task), true);
        UpdateHandObjectiveIndicator(GetHandIndicatorTarget(task), true);
        UpdateThrowTrajectoryLine();
        onComplete?.Invoke(false);
    }

    bool EvaluateTask(ScenarioTask task)
    {
        if (task.requireBallInHand &&
            task.taskType != ScenarioTaskType.ThrowBallToTarget &&
            !ballHeldByUser)
        {
            return false;
        }

        switch (task.taskType)
        {
            case ScenarioTaskType.CatchBall:
                return ballHeldByUser;

            case ScenarioTaskType.ReachZone:
                return DistanceToTarget(userRoot, task.target) <= task.completionRadius;

            case ScenarioTaskType.ReachWithHand:
                if (DistanceToTarget(leftHand, task.target) <= task.completionRadius)
                {
                    return true;
                }

                if (DistanceToTarget(rightHand, task.target) <= task.completionRadius)
                {
                    return true;
                }

                return false;

            case ScenarioTaskType.ThrowBallToTarget:
                return goalThrowCompleted;
        }

        return false;
    }

    IEnumerator FailAndRestart()
    {
        currentTaskIndex = -1;
        scenarioRunning = false;
        isPassingBall = false;
        RestoreNormalTime();
        UpdateObjectiveIndicator(null, false);
        UpdateHandObjectiveIndicator(null, false);
        SetThrowTrajectoryVisible(false);

        yield return new WaitForSecondsRealtime(failureScreenDuration);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ApplySlowMotion(float timeScale)
    {
        var clampedScale = Mathf.Clamp(timeScale, 0.05f, 1f);
        Time.timeScale = clampedScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * clampedScale;
    }

    void RestoreNormalTime()
    {
        Time.timeScale = 1f;
        if (defaultFixedDeltaTime > 0f)
        {
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }
    }

    void OnBallSelectEntered(SelectEnterEventArgs args)
    {
        isPassingBall = false;
        ballHeldByUser = true;
        caughtBallHolder = null;
        selectedBallHand = ResolveHandForInteractor(args.interactorObject);
        SnapFootballToSelectingHand(args.interactorObject);
    }

    void SnapFootballToSelectingHand(IXRSelectInteractor interactor)
    {
        if (football == null)
        {
            return;
        }

        var snapTarget = selectedBallHand != null
            ? selectedBallHand
            : interactor != null
                ? interactor.GetAttachTransform(football)
                : null;

        SnapFootballToTransform(snapTarget, true);
    }

    void SnapFootballToTransform(Transform snapTarget, bool resetVelocity)
    {
        if (football == null || snapTarget == null)
        {
            return;
        }

        football.transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);
        if (footballBody != null)
        {
            footballBody.position = snapTarget.position;
            footballBody.rotation = quaternion.Euler(0,0,90);

            if (resetVelocity)
            {
                footballBody.linearVelocity = Vector3.zero;
                footballBody.angularVelocity = Vector3.zero;
            }
        }
    }

    Transform ResolveHandForInteractor(IXRSelectInteractor interactor)
    {
        var interactorTransform = interactor is Component component ? component.transform : null;
        if (interactorTransform == null)
        {
            return rightHand != null ? rightHand : leftHand;
        }

        if (leftHand == null)
        {
            return rightHand;
        }

        if (rightHand == null)
        {
            return leftHand;
        }

        var leftDistance = (leftHand.position - interactorTransform.position).sqrMagnitude;
        var rightDistance = (rightHand.position - interactorTransform.position).sqrMagnitude;
        return leftDistance <= rightDistance ? leftHand : rightHand;
    }

    void OnBallSelectExited(SelectExitEventArgs args)
    {
        var releasedFromHand = selectedBallHand;
        ballHeldByUser = false;
        selectedBallHand = null;

        if (currentTaskIndex >= 0 && currentTaskIndex < tasks.Count && tasks[currentTaskIndex].taskType == ScenarioTaskType.ThrowBallToTarget)
        {
            ballReleasedByUserThisTask = true;

            if (!IsGoalReceiverReady(tasks[currentTaskIndex]))
            {
                QueueGoalThrowUntilReceiverReady(tasks[currentTaskIndex], releasedFromHand);
                return;
            }

            BeginGoalThrowWatch(tasks[currentTaskIndex]);

            if (goalThrowReleaseRoutine != null)
            {
                StopCoroutine(goalThrowReleaseRoutine);
            }

            goalThrowReleaseRoutine = StartCoroutine(HandleGoalThrowReleaseAfterPhysics(tasks[currentTaskIndex]));
        }
    }

    IEnumerator HandleGoalThrowReleaseAfterPhysics(ScenarioTask task)
    {
        // XRI applies throw velocity during release processing, so wait before reading Rigidbody velocity.
        yield return null;
        yield return new WaitForFixedUpdate();
        HandleGoalThrowRelease(task);
        goalThrowReleaseRoutine = null;
    }

    IEnumerator StartCatchBallSequence(ScenarioTask task)
    {
        if (football == null)
        {
            yield break;
        }

        ballTossStarted = false;

        if (passOrigin != null)
        {
            SnapBallToPassOrigin();
        }

        lockBallAtPassOrigin = false;

        if (passStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(passStartDelay);
        }

        var catchTarget = ballCatchTarget != null ? ballCatchTarget : GetPreferredCatchTarget();
        if (passOrigin == null || catchTarget == null)
        {
            yield break;
        }

        ballTossStarted = true;
        if (formationController != null)
        {
            formationController.TriggerDeferredHutHut();
        }
        if (keepCountdownVisibleUntilBallToss && scenarioUI != null)
        {
            scenarioUI.HideCountdown();
        }

        LaunchBallPass(catchTarget);
        yield return null;
    }

    IEnumerator StartDelayedRunnersAfterDelay()
    {
        if (formationController == null)
        {
            yield break;
        }

        if (delayedRunnerStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(delayedRunnerStartDelay);
        }

        formationController.PlayDelayedRunners();
    }

    void SnapBallToPassOrigin()
    {
        if (football == null || passOrigin == null)
        {
            return;
        }

        if (football.isSelected)
        {
            return;
        }

        football.transform.position = passOrigin.position;
        football.transform.rotation = passOrigin.rotation;

        if (footballBody != null)
        {
            footballBody.isKinematic = true;
            footballBody.linearVelocity = Vector3.zero;
            footballBody.angularVelocity = Vector3.zero;
            footballBody.position = passOrigin.position;
            footballBody.rotation = quaternion.Euler(0,0,90);
        }
    }

    void MaintainBallAtPassOrigin()
    {
        if (!lockBallAtPassOrigin || football == null || passOrigin == null)
        {
            return;
        }

        if (football.isSelected)
        {
            return;
        }

        football.transform.position = passOrigin.position;
        football.transform.rotation = passOrigin.rotation;

        if (footballBody != null)
        {
            footballBody.isKinematic = true;
            footballBody.linearVelocity = Vector3.zero;
            footballBody.angularVelocity = Vector3.zero;
            footballBody.position = passOrigin.position;
            footballBody.rotation = quaternion.Euler(0,0,90);
        }
    }

    void LaunchBallPass(Transform catchTarget)
    {
        if (football == null || footballBody == null || passOrigin == null || catchTarget == null)
        {
            return;
        }

        isPassingBall = true;
        ballReleasedByUserThisTask = false;

        var start = passOrigin.position;
        var end = catchTarget.position;
        var launchGravity = BuildLaunchGravityForFlightTime(start, end, Mathf.Max(0.15f, passTravelTime), passArcHeight);

        if (!TryCalculateBallisticVelocity(start, end, passArcHeight, out var launchVelocity, launchGravity))
        {
            var fallbackDirection = (end - start).normalized;
            var fallbackSpeed = Vector3.Distance(start, end) / Mathf.Max(0.15f, passTravelTime);
            launchVelocity = fallbackDirection * fallbackSpeed;
        }

        football.transform.position = start;
        football.transform.rotation = Quaternion.LookRotation((end - start).normalized, Vector3.up);

        footballBody.isKinematic = false;
        footballBody.linearVelocity = launchVelocity;
        footballBody.angularVelocity = football.transform.right * passSpinStrength;
    }

    Transform GetPreferredCatchTarget()
    {
        if (rightHand != null)
        {
            return rightHand;
        }

        if (leftHand != null)
        {
            return leftHand;
        }

        return userRoot;
    }

    Transform GetTaskIndicatorTarget(ScenarioTask task)
    {
        if (task == null)
        {
            return null;
        }

        switch (task.taskType)
        {
            case ScenarioTaskType.CatchBall:
                return null;

            case ScenarioTaskType.ReachZone:
            case ScenarioTaskType.ReachWithHand:
            case ScenarioTaskType.ThrowBallToTarget:
                return task.target;
        }

        return task.target;
    }

    Transform GetNextObjectiveTarget()
    {
        var nextIndex = currentTaskIndex + 1;
        if (nextIndex < 0 || nextIndex >= tasks.Count)
        {
            return null;
        }

        return GetTaskIndicatorTarget(tasks[nextIndex]);
    }

    Transform GetHandIndicatorTarget(ScenarioTask task)
    {
        if (task == null || task.taskType != ScenarioTaskType.ReachWithHand)
        {
            return null;
        }

        return task.target;
    }

    Transform GetNextHandIndicatorTarget()
    {
        var nextIndex = currentTaskIndex + 1;
        if (nextIndex < 0 || nextIndex >= tasks.Count)
        {
            return null;
        }

        return GetHandIndicatorTarget(tasks[nextIndex]);
    }

    void UpdateObjectiveIndicator(Transform target, bool visible)
    {
        if (objectiveIndicator == null)
        {
            return;
        }

        objectiveIndicator.gameObject.SetActive(visible && target != null);
        if (!visible || target == null)
        {
            return;
        }

        objectiveIndicator.position = target.position;
        objectiveIndicator.rotation = target.rotation;
    }

    void UpdateHandObjectiveIndicator(Transform target, bool visible)
    {
        if (handObjectiveIndicator == null)
        {
            return;
        }

        handObjectiveIndicator.gameObject.SetActive(visible && target != null);
        if (!visible || target == null)
        {
            return;
        }

        handObjectiveIndicator.position = target.position;
        handObjectiveIndicator.rotation = target.rotation;
    }

    string GetFailureMessage(ScenarioTask task)
    {
        if (!string.IsNullOrWhiteSpace(earlyFailureMessage))
        {
            return earlyFailureMessage;
        }

        if (task == null)
        {
            return "Scenario restarting...";
        }

        switch (task.taskType)
        {
            case ScenarioTaskType.CatchBall:
                return "You did not catch the ball in time. Pinch or grab it with either hand.";

            case ScenarioTaskType.ReachZone:
                return "You did not reach " + task.title + " in time.";

            case ScenarioTaskType.ReachWithHand:
                return "You did not stretch your hand to the fake target in time.";

            case ScenarioTaskType.ThrowBallToTarget:
                return "You did not throw the ball to the goal target in time.";
        }

        return "Scenario restarting...";
    }

    void HandleGoalThrowRelease(ScenarioTask task)
    {
        if (task == null || football == null || task.target == null || isGuidingGoalThrow)
        {
            return;
        }

        var throwTarget = GetGoalThrowTarget(task);
        if (throwTarget == null)
        {
            return;
        }

        if (IsFootballNearGoalReceiver())
        {
            TriggerGoalCatch();
            return;
        }

        var throwVelocity = footballBody != null ? footballBody.linearVelocity : Vector3.zero;
        var throwSpeed = throwVelocity.magnitude;
        var targetDirection = throwTarget.position - football.transform.position;

        if (targetDirection.sqrMagnitude < 0.0001f)
        {
            TriggerGoalCatch();
            return;
        }

        var planarTargetDirection = Vector3.ProjectOnPlane(targetDirection, Vector3.up);
        if (planarTargetDirection.sqrMagnitude < 0.0001f)
        {
            planarTargetDirection = targetDirection;
        }

        var normalizedTargetDirection = planarTargetDirection.normalized;
        Vector3 normalizedThrowDirection;

        if (throwSpeed >= minimumThrowSpeed)
        {
            normalizedThrowDirection = Vector3.ProjectOnPlane(throwVelocity, Vector3.up);
            if (normalizedThrowDirection.sqrMagnitude < 0.0001f)
            {
                normalizedThrowDirection = throwVelocity;
            }
        }
        else
        {
            var handReference = rightHand != null ? rightHand : leftHand;
            if (handReference == null)
            {
                BeginGoalThrowWatch(task);
                return;
            }

            normalizedThrowDirection = Vector3.ProjectOnPlane(handReference.forward, Vector3.up);
            if (normalizedThrowDirection.sqrMagnitude < 0.0001f)
            {
                normalizedThrowDirection = handReference.forward;
            }
        }

        normalizedThrowDirection.Normalize();
        var directionDot = Vector3.Dot(normalizedThrowDirection, normalizedTargetDirection);
        if (directionDot < throwDirectionAcceptanceDot)
        {
            BeginGoalThrowWatch(task);
            return;
        }

        SetThrowTrajectoryVisible(false);
        goalThrowDirectionAccepted = true;
        RestoreNormalTime();
        LaunchGoalThrow(throwTarget);
        if (!taskFailedEarly)
        {
            BeginGoalThrowWatch(task);
        }
    }

    void FailGoalThrow(string message)
    {
        earlyFailureMessage = message;
        taskFailedEarly = true;
    }

    void LaunchGoalThrow(Transform target)
    {
        if (football == null || target == null || footballBody == null)
        {
            FailGoalThrow("The goal throw could not be launched.");
            return;
        }

        isGuidingGoalThrow = true;
        goalThrowLaunched = false;
        goalThrowCompleted = false;
        SetThrowTrajectoryVisible(false);

        if (!TryCalculateBallisticVelocity(football.transform.position, target.position, goalThrowArcHeight, out var launchVelocity))
        {
            FailGoalThrow("The throw arc to the goal target could not be calculated.");
            isGuidingGoalThrow = false;
            return;
        }

        footballBody.isKinematic = false;
        footballBody.linearVelocity = launchVelocity;
        footballBody.angularVelocity = Vector3.zero;
        closestGoalThrowDistance = DistanceToTarget(football.transform, target);
        goalThrowLaunched = true;
        isGuidingGoalThrow = false;
    }

    void BeginGoalThrowWatch(ScenarioTask task)
    {
        if (football == null || task == null || task.target == null)
        {
            return;
        }

        var throwTarget = GetGoalThrowTarget(task);
        if (throwTarget == null)
        {
            return;
        }

        closestGoalThrowDistance = Mathf.Min(closestGoalThrowDistance, DistanceToTarget(football.transform, throwTarget));
        var calculatedFlightTime = CalculateBallisticFlightTime(football.transform.position, throwTarget.position, goalThrowArcHeight);
        var watchDuration = Mathf.Max(task.taskDuration, goalThrowTravelTime, calculatedFlightTime) + 0.75f;
        goalThrowWatchUntilTime = Mathf.Max(goalThrowWatchUntilTime, Time.unscaledTime + watchDuration);
    }

    bool ShouldKeepWatchingGoalThrow(ScenarioTask task)
    {
        return task != null &&
               task.taskType == ScenarioTaskType.ThrowBallToTarget &&
               ballReleasedByUserThisTask &&
               !goalThrowCompleted &&
               (goalThrowDirectionAccepted || Time.unscaledTime < goalThrowWatchUntilTime);
    }

    bool ShouldWaitForGoalReceiver(ScenarioTask task)
    {
        return task != null &&
               task.taskType == ScenarioTaskType.ThrowBallToTarget &&
               waitForGoalReceiverOnFinalThrow &&
               !goalThrowDirectionAccepted &&
               !IsGoalReceiverReady(task);
    }

    void QueueGoalThrowUntilReceiverReady(ScenarioTask task, Transform holdTarget)
    {
        queuedGoalThrow = true;
        goalThrowDirectionAccepted = true;
        queuedGoalThrowHoldTarget = holdTarget != null ? holdTarget : GetPreferredCatchTarget();
        SetThrowTrajectoryVisible(false);
        RestoreNormalTime();
        HoldQueuedGoalThrowBall();
        BeginGoalThrowWatch(task);
    }

    void MaintainQueuedGoalThrow()
    {
        if (currentTaskIndex < 0 || currentTaskIndex >= tasks.Count)
        {
            queuedGoalThrow = false;
            return;
        }

        var task = tasks[currentTaskIndex];
        if (task == null || task.taskType != ScenarioTaskType.ThrowBallToTarget)
        {
            queuedGoalThrow = false;
            return;
        }

        if (!IsGoalReceiverReady(task))
        {
            HoldQueuedGoalThrowBall();
            return;
        }

        queuedGoalThrow = false;
        var throwTarget = GetGoalThrowTarget(task);
        if (throwTarget == null)
        {
            return;
        }

        LaunchGoalThrow(throwTarget);
        if (!taskFailedEarly)
        {
            BeginGoalThrowWatch(task);
        }
    }

    void HoldQueuedGoalThrowBall()
    {
        if (football == null)
        {
            return;
        }

        var holdTarget = queuedGoalThrowHoldTarget != null ? queuedGoalThrowHoldTarget : GetPreferredCatchTarget();
        if (holdTarget == null)
        {
            return;
        }

        football.transform.SetPositionAndRotation(holdTarget.position, holdTarget.rotation);
        if (footballBody != null)
        {
            footballBody.isKinematic = true;
            footballBody.position = holdTarget.position;
            footballBody.rotation = quaternion.Euler(0,0,90);
            footballBody.linearVelocity = Vector3.zero;
            footballBody.angularVelocity = Vector3.zero;
        }
    }

    float GetGoalThrowWatchRemaining(ScenarioTask task)
    {
        return ShouldKeepWatchingGoalThrow(task)
            ? Mathf.Max(0f, goalThrowWatchUntilTime - Time.unscaledTime)
            : 0f;
    }

    void UpdateThrowTrajectoryLine()
    {
        if (throwTrajectoryLine == null)
        {
            return;
        }

        var shouldShow = ShouldShowThrowTrajectory();
        if (!shouldShow)
        {
            SetThrowTrajectoryVisible(false);
            return;
        }

        var currentTask = tasks[currentTaskIndex];
        var start = football != null ? football.transform.position : GetPreferredCatchTarget() != null ? GetPreferredCatchTarget().position : Vector3.zero;
        var endTarget = GetGoalThrowTarget(currentTask);
        var end = endTarget != null ? endTarget.position : currentTask.target.position;
        DrawBallisticArc(throwTrajectoryLine, start, end, goalThrowArcHeight, Mathf.Max(4, throwTrajectoryResolution));
        SetThrowTrajectoryVisible(true);
    }

    bool ShouldShowThrowTrajectory()
    {
        if (throwTrajectoryLine == null)
        {
            return false;
        }

        if (!scenarioRunning || isGuidingGoalThrow)
        {
            return false;
        }

        if (currentTaskIndex < 0 || currentTaskIndex >= tasks.Count)
        {
            return false;
        }

        var task = tasks[currentTaskIndex];
        return task != null &&
               task.taskType == ScenarioTaskType.ThrowBallToTarget &&
               task.target != null &&
               football != null &&
               ballHeldByUser;
    }

    void SetThrowTrajectoryVisible(bool visible)
    {
        if (throwTrajectoryLine == null)
        {
            return;
        }

        throwTrajectoryLine.enabled = visible;
    }

    void UpdateGoalThrowState()
    {
        if (isPassingBall && football != null && football.isSelected)
        {
            isPassingBall = false;
        }

        if (queuedGoalThrow ||
            (!goalThrowLaunched && !ballReleasedByUserThisTask) ||
            goalThrowCompleted ||
            football == null ||
            currentTaskIndex < 0 ||
            currentTaskIndex >= tasks.Count)
        {
            return;
        }

        var task = tasks[currentTaskIndex];
        if (task == null || task.taskType != ScenarioTaskType.ThrowBallToTarget || task.target == null)
        {
            return;
        }

        if (IsFootballNearGoalReceiver())
        {
            TriggerGoalCatch();
            return;
        }

        var throwTarget = GetGoalThrowTarget(task);
        if (throwTarget == null)
        {
            return;
        }

        var receiverReady = IsGoalReceiverReady(task);
        var currentDistance = DistanceToTarget(football.transform, throwTarget);
        closestGoalThrowDistance = Mathf.Min(closestGoalThrowDistance, currentDistance);

        var catchDistance = Mathf.Max(0.1f, goalCatchDistance);
        if ((receiverReady || goalReceiver == null) &&
            (currentDistance <= catchDistance ||
             (currentDistance > closestGoalThrowDistance + 0.01f && closestGoalThrowDistance <= catchDistance * 1.5f)))
        {
            TriggerGoalCatch();
        }
    }

    void ResolveGoalReceiver()
    {
        if (goalReceiver != null || formationController == null)
        {
            return;
        }

        goalReceiver = formationController.FindPlayerRunnerActor(goalReceiverName);
        if (goalReceiver == null && string.Equals(goalReceiverName, "Goal", System.StringComparison.OrdinalIgnoreCase))
        {
            goalReceiver = formationController.FindPlayerRunnerActor("Gole");
        }
    }

    bool IsGoalReceiverReady(ScenarioTask task)
    {
        ResolveGoalReceiver();
        if (!waitForGoalReceiverOnFinalThrow || goalReceiver == null)
        {
            return true;
        }

        if (formationController != null && !string.IsNullOrWhiteSpace(goalReceiverName))
        {
            if (formationController.IsPlayerRunnerNearLastPoint(goalReceiverName, goalReceiverReadyRadius))
            {
                return true;
            }

            if (string.Equals(goalReceiverName, "Goal", System.StringComparison.OrdinalIgnoreCase) &&
                formationController.IsPlayerRunnerNearLastPoint("Gole", goalReceiverReadyRadius))
            {
                return true;
            }
        }

        var fallbackTarget = task != null ? task.target : null;
        return fallbackTarget == null || DistanceToTarget(goalReceiver, fallbackTarget) <= Mathf.Max(0.01f, goalReceiverReadyRadius);
    }

    Transform GetGoalThrowTarget(ScenarioTask task)
    {
        ResolveGoalReceiver();
        if (goalReceiver != null && IsGoalReceiverReady(task))
        {
            return goalBallHoldAnchor != null ? goalBallHoldAnchor : goalReceiver;
        }

        return task != null ? task.target : null;
    }

    bool IsFootballNearGoalReceiver()
    {
        ResolveGoalReceiver();
        if (football == null)
        {
            return false;
        }

        var catchTarget = goalBallHoldAnchor != null ? goalBallHoldAnchor : goalReceiver;
        return catchTarget != null && Vector3.Distance(football.transform.position, catchTarget.position) <= Mathf.Max(0.1f, goalCatchDistance);
    }

    void TriggerGoalCatch()
    {
        if (goalCatchTriggered)
        {
            return;
        }

        ResolveGoalReceiver();
        goalCatchTriggered = true;
        caughtBallHolder = goalBallHoldAnchor != null ? goalBallHoldAnchor : goalReceiver;
        HoldFootballAtGoalReceiver();

        if (goalCatchRoutine != null)
        {
            StopCoroutine(goalCatchRoutine);
        }

        goalCatchRoutine = StartCoroutine(PlayGoalCatchRoutine());
        CompleteGoalThrow(false);
    }

    IEnumerator PlayGoalCatchRoutine()
    {
        var driver = goalReceiver != null ? goalReceiver.GetComponent<FootballAnimationStateDriver>() : null;
        if (driver != null)
        {
            if (driver.Animator != null)
            {
                driver.Animator.speed = 1f;
            }

            driver.ClearActionStates();
            driver.ClearMovement();
            driver.SetHasBall(true);
            driver.SetBallCatch(true);
            driver.ApplyStates();
            driver.RefreshAnimatorImmediate();
        }

        var duration = Mathf.Max(0f, goalCatchHoldDuration);
        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
        }

        if (driver != null)
        {
            driver.SetBallCatch(false);
            driver.SetHasBall(true);
            driver.ApplyStates();
            driver.RefreshAnimatorImmediate();
        }

        goalCatchRoutine = null;
    }

    void HoldFootballAtGoalReceiver()
    {
        if (football == null || caughtBallHolder == null)
        {
            return;
        }

        var targetPosition = goalBallHoldAnchor != null
            ? goalBallHoldAnchor.position
            : caughtBallHolder.TransformPoint(goalCaughtBallLocalOffset);
        var targetRotation = goalBallHoldAnchor != null
            ? goalBallHoldAnchor.rotation
            : caughtBallHolder.rotation;

        football.transform.SetPositionAndRotation(targetPosition, targetRotation);
        if (footballBody != null)
        {
            footballBody.isKinematic = true;
            footballBody.position = targetPosition;
            footballBody.rotation = quaternion.Euler(0,0,90);
            footballBody.linearVelocity = Vector3.zero;
            footballBody.angularVelocity = Vector3.zero;
        }
    }

    void CompleteGoalThrow(bool stopBall)
    {
        goalThrowCompleted = true;
        goalThrowLaunched = false;
        goalThrowWatchUntilTime = 0f;

        if (stopBall && footballBody != null)
        {
            footballBody.linearVelocity = Vector3.zero;
            footballBody.angularVelocity = Vector3.zero;
        }
    }

    bool TryCalculateBallisticVelocity(Vector3 start, Vector3 end, float apexHeightOffset, out Vector3 launchVelocity)
    {
        return TryCalculateBallisticVelocity(start, end, apexHeightOffset, out launchVelocity, 0f);
    }

    bool TryCalculateBallisticVelocity(Vector3 start, Vector3 end, float apexHeightOffset, out Vector3 launchVelocity, float gravityOverride)
    {
        var gravity = gravityOverride < -0.001f ? gravityOverride : Physics.gravity.y;
        if (gravity >= -0.001f)
        {
            launchVelocity = Vector3.zero;
            return false;
        }

        var apexHeight = Mathf.Max(start.y, end.y) + Mathf.Max(0.1f, apexHeightOffset);
        var rise = apexHeight - start.y;
        var fall = apexHeight - end.y;
        if (rise <= 0f || fall < 0f)
        {
            launchVelocity = Vector3.zero;
            return false;
        }

        var gravityAbs = -gravity;
        var verticalVelocity = Mathf.Sqrt(2f * gravityAbs * rise);
        var timeUp = verticalVelocity / gravityAbs;
        var timeDown = Mathf.Sqrt(2f * fall / gravityAbs);
        var totalTime = timeUp + timeDown;
        if (totalTime <= 0.001f)
        {
            launchVelocity = Vector3.zero;
            return false;
        }

        var planarDelta = new Vector3(end.x - start.x, 0f, end.z - start.z);
        var horizontalVelocity = planarDelta / totalTime;
        launchVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        return true;
    }

    float BuildLaunchGravityForFlightTime(Vector3 start, Vector3 end, float desiredFlightTime, float apexHeightOffset)
    {
        var time = Mathf.Max(0.15f, desiredFlightTime);
        var apexHeight = Mathf.Max(start.y, end.y) + Mathf.Max(0.1f, apexHeightOffset);
        var rise = apexHeight - start.y;
        if (rise <= 0.001f)
        {
            return 0f;
        }

        var gravityMagnitude = (8f * rise) / Mathf.Max(0.001f, time * time);
        return -gravityMagnitude;
    }

    float CalculateBallisticFlightTime(Vector3 start, Vector3 end, float apexHeightOffset)
    {
        if (!TryCalculateBallisticVelocity(start, end, apexHeightOffset, out var launchVelocity))
        {
            return 0f;
        }

        var gravityAbs = -Physics.gravity.y;
        return launchVelocity.y / gravityAbs + Mathf.Sqrt(Mathf.Max(0f, 2f * ((Mathf.Max(start.y, end.y) + Mathf.Max(0.1f, apexHeightOffset)) - end.y) / gravityAbs));
    }

    void DrawBallisticArc(LineRenderer lineRenderer, Vector3 start, Vector3 end, float apexHeightOffset, int resolution)
    {
        if (!TryCalculateBallisticVelocity(start, end, apexHeightOffset, out var launchVelocity))
        {
            lineRenderer.positionCount = 0;
            return;
        }

        var totalTime = CalculateBallisticFlightTime(start, end, apexHeightOffset);
        if (totalTime <= 0.001f)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = resolution;
        for (var i = 0; i < resolution; i++)
        {
            var t = resolution == 1 ? totalTime : (i / (float)(resolution - 1)) * totalTime;
            var position = start + launchVelocity * t + 0.5f * Physics.gravity * t * t;
            lineRenderer.SetPosition(i, position);
        }
    }

    float DistanceToTarget(Transform source, Transform target)
    {
        if (source == null || target == null)
        {
            return float.MaxValue;
        }

        return Vector3.Distance(source.position, target.position);
    }

    void OnDrawGizmosSelected()
    {
        if (tasks == null)
        {
            return;
        }

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            if (task == null || task.target == null)
            {
                continue;
            }

            Gizmos.color = task.taskType == ScenarioTaskType.ThrowBallToTarget
                ? new Color(1f, 0.8f, 0.2f, 0.9f)
                : new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(task.target.position, task.completionRadius);
        }
    }
}
