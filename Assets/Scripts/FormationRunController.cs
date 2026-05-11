using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FormationRunController : MonoBehaviour
{
    public enum SpaceMode
    {
        World,
        PlayerRelative
    }

    public enum BumpDirectionMode
    {
        ActorForward,
        TowardPartner,
        CustomDirection
    }

    public enum PointAnimationType
    {
        None,
        Push,
        HugPush,
        BallCatch,
        FallDown
    }

    [System.Serializable]
    public class MotionPoint
    {
        public string pointName = "Point";
        public Transform point;
        public SpaceMode spaceMode = SpaceMode.World;
        public float segmentDuration = 0.6f;
        public float waitAtPoint;
        public string triggerOnReach;
        public PointAnimationType pointAnimationType;
        public float animationHoldDuration = 0.35f;
        public bool giveRunnerBallAfterAction;
        public bool stopPathAfterAction;

        [Header("Bump Effect")]
        public bool triggerBump;
        public Transform bumpPartner;
        public float bumpDuration = 0.6f;
        public float bumpDistance = 0.08f;
        public int bumpFrequency = 1;
        public float settleTime = 0.08f;
        public float impactHoldRatio = 0.18f;
        public float bodyDipDistance = 0.035f;
        public float bodyLeanAngle = 9f;
        public float partnerReactionMultiplier = 0.85f;
        public bool stabilizeFacingDuringBump = true;
        public BumpDirectionMode bumpDirectionMode = BumpDirectionMode.ActorForward;
        public Vector3 customBumpDirection = Vector3.forward;
    }

    [System.Serializable]
    public class TeamRunner
    {
        public string playerName = "Runner";
        public Transform actor;
        public Animator animator;
        public FootballAnimationStateDriver animationDriver;
        public Transform assignedPlayer;
        public List<MotionPoint> path = new List<MotionPoint>();

        [Header("Movement")]
        public float positionSmoothness = 8f;
        public float rotationSmoothness = 10f;
        public float maxMoveSpeed = 4f;
        [Range(0f, 0.45f)] public float pathCurveStrength = 0.18f;
        [Range(0.05f, 0.45f)] public float rotationLookAhead = 0.2f;
        [Range(0f, 20f)] public float turnTiltAngle = 8f;
        public bool keepYFromActor = true;
        public bool seamlessZeroWaitTransitions = true;
        public bool useLinearTimingForSeamlessTransitions = true;
        public int openingNoRotatePointCount = 2;
        public bool moveBeforeHutHut;
        public bool startsWithBall;
        public bool useBallGrabRunningWhenCarryingBall = true;
        public int openingSideWalkSegmentCount;
        public int openingCrouchSegmentCount;
        public bool openingCrouchUsesBallHold;

        [Header("Countdown Animation")]
        public bool startInCrouchIdle;
        public bool startInCrouchBallHold;
        public bool playBallHutHutOnStart;
        public float ballHutHutDuration = 0.8f;

        [Header("Legacy Animation")]
        public string speedFloatName = "Speed";
        public string movingBoolName = "IsMoving";
        public string startMoveTriggerName = "StartMove";
        public string stopMoveTriggerName = "StopMove";

        [HideInInspector] public bool runtimeHasBall;
        [HideInInspector] public bool runtimeAwaitingHutHutRelease;
        [HideInInspector] public bool runtimeDelayedStartPending;
    }

    [Header("Optional Shared Player")]
    [SerializeField] Transform sharedPlayerReference;

    [Header("Player Team")]
    [SerializeField] List<TeamRunner> playerTeam = new List<TeamRunner>();

    [Header("Opponent Team")]
    [SerializeField] List<TeamRunner> opponentTeam = new List<TeamRunner>();

    [Header("Autoplay")]
    [SerializeField] bool playPlayerTeamOnStart;
    [SerializeField] bool playOpponentTeamOnStart;

    [Header("Countdown")]
    [SerializeField] bool useStartupCountdown = true;
    [SerializeField] float countdownStepDuration = 1f;
    [SerializeField] float postHutHutDelay = 0.1f;

    readonly Dictionary<Transform, Coroutine> activeRoutines = new Dictionary<Transform, Coroutine>();
    readonly HashSet<Transform> bumpLockedActors = new HashSet<Transform>();

    void Start()
    {
        var hasAutoplay = playPlayerTeamOnStart || playOpponentTeamOnStart;
        if (useStartupCountdown && hasAutoplay)
        {
            StartCoroutine(StartupSequence());
            return;
        }

        PrepareAllRunnerStates();

        if (playPlayerTeamOnStart)
        {
            PlayPlayerTeam();
        }

        if (playOpponentTeamOnStart)
        {
            PlayOpponentTeam();
        }
    }

    public void PrepareOpeningPose()
    {
        PrepareAllRunnerStates();
        ApplyCountdownPose(playerTeam);
        ApplyCountdownPose(opponentTeam);
    }

    public void BeginAfterCountdown()
    {
        StartCoroutine(BeginAfterCountdownRoutine(true, true));
    }

    public void BeginAfterCountdown(bool startPlayerTeam, bool startOpponentTeam)
    {
        StartCoroutine(BeginAfterCountdownRoutine(startPlayerTeam, startOpponentTeam));
    }

    public void BeginAfterCountdownWithoutHutHut(bool startPlayerTeam, bool startOpponentTeam)
    {
        StartCoroutine(BeginAfterCountdownWithoutHutHutRoutine(startPlayerTeam, startOpponentTeam));
    }

    public void TriggerDeferredHutHut()
    {
        StartCoroutine(PlayDeferredHutHutRoutine());
    }

    public void PlayDelayedRunners()
    {
        SetDelayedStartPending(playerTeam, false, onlyDelayedRunners: true);
        SetDelayedStartPending(opponentTeam, false, onlyDelayedRunners: true);
        ClearDelayedRunnerCrouch(playerTeam);
        ClearDelayedRunnerCrouch(opponentTeam);
        PlayTeam(playerTeam, false);
        PlayTeam(opponentTeam, false);
    }

    public void SetSharedPlayerReference(Transform player)
    {
        sharedPlayerReference = player;
    }

    public void PlayBothTeams()
    {
        PlayTeam(playerTeam);
        PlayTeam(opponentTeam);
    }

    public void PlayPlayerTeam()
    {
        PlayTeam(playerTeam);
    }

    public void PlayOpponentTeam()
    {
        PlayTeam(opponentTeam);
    }

    public void StopBothTeams()
    {
        StopTeam(playerTeam);
        StopTeam(opponentTeam);
    }

    public void StopPlayerTeam()
    {
        StopTeam(playerTeam);
    }

    public void StopOpponentTeam()
    {
        StopTeam(opponentTeam);
    }

    public Transform FindPlayerRunnerActor(string runnerName)
    {
        var runner = FindRunnerByName(playerTeam, runnerName);
        return runner != null ? runner.actor : null;
    }

    public Transform GetPlayerRunnerLastPoint(string runnerName)
    {
        var runner = FindRunnerByName(playerTeam, runnerName);
        return GetRunnerLastPoint(runner);
    }

    public bool IsPlayerRunnerNearLastPoint(string runnerName, float radius)
    {
        var runner = FindRunnerByName(playerTeam, runnerName);
        var lastPoint = GetRunnerLastPoint(runner);
        if (runner == null || runner.actor == null)
        {
            return false;
        }

        if (lastPoint == null)
        {
            return true;
        }

        return Vector3.Distance(runner.actor.position, lastPoint.position) <= Mathf.Max(0.01f, radius);
    }

    TeamRunner FindRunnerByName(List<TeamRunner> team, string runnerName)
    {
        if (team == null || string.IsNullOrWhiteSpace(runnerName))
        {
            return null;
        }

        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner != null && string.Equals(runner.playerName, runnerName, System.StringComparison.OrdinalIgnoreCase))
            {
                return runner;
            }
        }

        return null;
    }

    Transform GetRunnerLastPoint(TeamRunner runner)
    {
        if (runner == null || runner.path == null)
        {
            return null;
        }

        for (var i = runner.path.Count - 1; i >= 0; i--)
        {
            var point = runner.path[i];
            if (point != null && point.point != null)
            {
                return point.point;
            }
        }

        return null;
    }

    void PlayTeam(List<TeamRunner> team)
    {
        PlayTeam(team, null);
    }

    void PlayTeam(List<TeamRunner> team, bool? moveBeforeHutHutFilter)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null || runner.actor == null)
            {
                continue;
            }

            if (moveBeforeHutHutFilter.HasValue && runner.moveBeforeHutHut != moveBeforeHutHutFilter.Value)
            {
                continue;
            }

            runner.runtimeHasBall = runner.startsWithBall;
            PrepareRunnerForMovement(runner);
            StopRunner(runner);
            activeRoutines[runner.actor] = StartCoroutine(PlayRunnerPath(runner));
        }
    }

    void StopTeam(List<TeamRunner> team)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null)
            {
                continue;
            }

            StopRunner(runner);
        }
    }

    void StopRunner(TeamRunner runner)
    {
        if (runner.actor != null && activeRoutines.TryGetValue(runner.actor, out var routine))
        {
            StopCoroutine(routine);
            activeRoutines.Remove(runner.actor);
        }

        SetMovingState(runner, false);
        SetAnimatorSpeed(runner, 0f);
        ApplyLocomotionAnimation(runner, false, -1);
        SetAnimatorPlayback(runner, false);
    }

    IEnumerator PlayRunnerPath(TeamRunner runner)
    {
        if (runner.path == null || runner.path.Count == 0)
        {
            yield break;
        }

        var actor = runner.actor;
        SetAnimatorPlayback(runner, true);
        SetMovingState(runner, true);

        if (runner.path[0] != null && runner.path[0].point != null)
        {
            var firstPosition = GetResolvedPosition(runner, 0);
            if (runner.keepYFromActor)
            {
                firstPosition.y = actor.position.y;
            }

            actor.position = firstPosition;
        }

        if (runner.path.Count == 1)
        {
            SetMovingState(runner, false);
            SetAnimatorSpeed(runner, 0f);
            activeRoutines.Remove(actor);
            ApplyRouteEndAnimation(runner, true);
            yield break;
        }

        PrimeStartLocomotion(runner);
        DesyncCentreAnimatorPhase(runner);

        var previousPointTriggeredBump = false;
        var stoppedByPointAction = false;
        var stoppedByFallDown = false;

        for (var i = 0; i < runner.path.Count - 1; i++)
        {
            var destinationPoint = runner.path[i + 1];
            if (destinationPoint == null || destinationPoint.point == null)
            {
                continue;
            }

            var useSegmentActionAnimation =
                destinationPoint.pointAnimationType == PointAnimationType.Push ||
                destinationPoint.pointAnimationType == PointAnimationType.HugPush ||
                destinationPoint.pointAnimationType == PointAnimationType.FallDown;

            if (useSegmentActionAnimation)
            {
                ApplySegmentActionAnimation(runner, destinationPoint, true);
            }

            var p0 = GetResolvedPosition(runner, i - 1);
            var p1 = GetResolvedPosition(runner, i);
            var p2 = GetResolvedPosition(runner, i + 1);
            var p3 = GetResolvedPosition(runner, i + 2);
            var usePostBumpRecoverySegment = previousPointTriggeredBump;
            var recoveryStartPosition = actor.position;

            if (runner.keepYFromActor)
            {
                p0.y = actor.position.y;
                p1.y = actor.position.y;
                p2.y = actor.position.y;
                p3.y = actor.position.y;
                recoveryStartPosition.y = actor.position.y;
            }

            var duration = Mathf.Max(0.01f, destinationPoint.segmentDuration);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                yield return WaitWhileScenarioPaused(runner);

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var nextWaitIsZero = destinationPoint.waitAtPoint <= 0f;
                var useSeamlessMotion = runner.seamlessZeroWaitTransitions && nextWaitIsZero;
                var interpolationT = useSeamlessMotion && runner.useLinearTimingForSeamlessTransitions
                    ? t
                    : Mathf.SmoothStep(0f, 1f, t);

                Vector3 targetPosition;
                if (usePostBumpRecoverySegment)
                {
                    targetPosition = Vector3.Lerp(recoveryStartPosition, p2, interpolationT);
                }
                else if (runner.path.Count >= 3)
                {
                    targetPosition = GetCurvedSegmentPosition(runner, p0, p1, p2, p3, interpolationT);
                }
                else
                {
                    targetPosition = Vector3.Lerp(p1, p2, interpolationT);
                }

                if (runner.keepYFromActor)
                {
                    targetPosition.y = actor.position.y;
                }

                var smoothedPosition = useSeamlessMotion
                    ? targetPosition
                    : Vector3.Lerp(actor.position, targetPosition, 1f - Mathf.Exp(-runner.positionSmoothness * Time.deltaTime));

                var movement = smoothedPosition - actor.position;
                var desiredVelocity = movement / Mathf.Max(Time.deltaTime, 0.0001f);
                var speedRatio = Mathf.Clamp01(desiredVelocity.magnitude / Mathf.Max(0.01f, runner.maxMoveSpeed));

                actor.position = smoothedPosition;
                ApplyLocomotionAnimation(
                    runner,
                    desiredVelocity.magnitude > 0.001f,
                    i,
                    desiredVelocity,
                    speedRatio,
                    useSegmentActionAnimation
                        ? destinationPoint.pointAnimationType == PointAnimationType.FallDown
                            ? PointAnimationType.Push
                            : destinationPoint.pointAnimationType
                        : PointAnimationType.None);

                var destinationPointIndex = i + 1;
                var shouldAlwaysFaceNextPoint = ShouldAlwaysFaceNextPoint(runner);
                var useDirectPointFacing =
                    shouldAlwaysFaceNextPoint &&
                    destinationPoint.pointAnimationType != PointAnimationType.Push &&
                    destinationPoint.pointAnimationType != PointAnimationType.HugPush;
                var shouldRotateThisSegment = shouldAlwaysFaceNextPoint || destinationPointIndex > runner.openingNoRotatePointCount;
                if (shouldRotateThisSegment)
                {
                    var waypointLookDirection = useDirectPointFacing
                        ? GetDirectPointFacingDirection(runner, actor.position, p2)
                        : GetSegmentFacingDirection(
                            runner,
                            destinationPoint,
                            actor.position,
                            p0,
                            p1,
                            p2,
                            p3,
                            interpolationT,
                            usePostBumpRecoverySegment,
                            recoveryStartPosition);
                    if (waypointLookDirection.sqrMagnitude > 0.0001f)
                    {
                        var targetRotation = useDirectPointFacing
                            ? Quaternion.LookRotation(waypointLookDirection.normalized, Vector3.up)
                            : GetBankedRotation(actor.rotation, waypointLookDirection.normalized, runner, speedRatio);
                        var rotationSmoothness = shouldAlwaysFaceNextPoint
                            ? Mathf.Max(runner.rotationSmoothness, 28f)
                            : runner.rotationSmoothness;
                        actor.rotation = Quaternion.Slerp(actor.rotation, targetRotation, 1f - Mathf.Exp(-rotationSmoothness * Time.deltaTime));
                    }
                }

                SetAnimatorSpeed(runner, speedRatio);
                yield return null;
            }

            if (!runner.seamlessZeroWaitTransitions || destinationPoint.waitAtPoint > 0f)
            {
                actor.position = p2;
            }

            if (!string.IsNullOrWhiteSpace(destinationPoint.triggerOnReach))
            {
                TriggerAnimator(runner, destinationPoint.triggerOnReach);
            }

            if (destinationPoint.triggerBump)
            {
                var usesPushOnlyContact =
                    destinationPoint.pointAnimationType == PointAnimationType.Push ||
                    destinationPoint.pointAnimationType == PointAnimationType.HugPush;

                if (!usesPushOnlyContact)
                {
                    yield return AlignActorsForBump(runner, destinationPoint);
                    var isTerminalPoint = i + 1 >= runner.path.Count - 1;
                    yield return PlayBumpEffect(runner, destinationPoint, isTerminalPoint);
                }
            }

            if (destinationPoint.pointAnimationType != PointAnimationType.None)
            {
                var shouldStopPath = false;
                yield return PlayPointAnimation(runner, destinationPoint, result => shouldStopPath = result);
                if (shouldStopPath)
                {
                    stoppedByPointAction = true;
                    stoppedByFallDown = destinationPoint.pointAnimationType == PointAnimationType.FallDown;
                    break;
                }
            }

            previousPointTriggeredBump = destinationPoint.triggerBump;

            if (destinationPoint.waitAtPoint > 0f)
            {
                SetAnimatorSpeed(runner, 0f);
                yield return new WaitForSeconds(destinationPoint.waitAtPoint);
            }

            if (useSegmentActionAnimation)
            {
                ApplySegmentActionAnimation(runner, destinationPoint, false);
                ApplyLocomotionAnimation(runner, false, i);
            }
        }

        activeRoutines.Remove(actor);
        SetMovingState(runner, false);
        SetAnimatorSpeed(runner, 0f);

        if (stoppedByFallDown)
        {
            SetAnimatorPlayback(runner, true);
            yield break;
        }

        ApplyRouteEndAnimation(runner, !stoppedByPointAction);
    }

    IEnumerator StartupSequence()
    {
        PrepareOpeningPose();

        for (var count = 3; count >= 1; count--)
        {
            yield return new WaitForSeconds(countdownStepDuration);
        }

        yield return BeginAfterCountdownRoutine(playPlayerTeamOnStart, playOpponentTeamOnStart);
    }

    IEnumerator BeginAfterCountdownRoutine(bool startPlayerTeam, bool startOpponentTeam)
    {
        yield return PlayHutHut(playerTeam);
        yield return PlayHutHut(opponentTeam);

        ClearCountdownPose(playerTeam);
        ClearCountdownPose(opponentTeam);

        if (postHutHutDelay > 0f)
        {
            yield return new WaitForSeconds(postHutHutDelay);
        }

        if (startPlayerTeam)
        {
            PlayPlayerTeam();
        }

        if (startOpponentTeam)
        {
            PlayOpponentTeam();
        }
    }

    IEnumerator BeginAfterCountdownWithoutHutHutRoutine(bool startPlayerTeam, bool startOpponentTeam)
    {
        SetDelayedStartPending(playerTeam, startPlayerTeam);
        SetDelayedStartPending(opponentTeam, startOpponentTeam);
        ClearCountdownPose(playerTeam, preserveDeferredHutHutRunners: true);
        ClearCountdownPose(opponentTeam, preserveDeferredHutHutRunners: true);

        if (postHutHutDelay > 0f)
        {
            yield return new WaitForSeconds(postHutHutDelay);
        }

        if (startPlayerTeam)
        {
            PlayTeam(playerTeam, true);
        }

        if (startOpponentTeam)
        {
            PlayTeam(opponentTeam, true);
        }
    }

    IEnumerator PlayDeferredHutHutRoutine()
    {
        StartCoroutine(PlayHutHut(playerTeam));
        StartCoroutine(PlayHutHut(opponentTeam));
        yield break;
    }

    void PrepareAllRunnerStates()
    {
        PrepareRunnerStates(playerTeam);
        PrepareRunnerStates(opponentTeam);
    }

    void PrepareRunnerStates(List<TeamRunner> team)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null)
            {
                continue;
            }

            runner.runtimeHasBall = runner.startsWithBall;
            runner.runtimeAwaitingHutHutRelease = false;
            runner.runtimeDelayedStartPending = false;
            PrepareRunnerForMovement(runner);
        }
    }

    void SetDelayedStartPending(List<TeamRunner> team, bool enabled, bool onlyDelayedRunners = false)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null)
            {
                continue;
            }

            if (onlyDelayedRunners)
            {
                if (!runner.moveBeforeHutHut)
                {
                    runner.runtimeDelayedStartPending = enabled;
                }

                continue;
            }

            runner.runtimeDelayedStartPending = enabled && !runner.moveBeforeHutHut;
        }
    }

    void ClearDelayedRunnerCrouch(List<TeamRunner> team)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null || runner.moveBeforeHutHut)
            {
                continue;
            }

            var driver = GetAnimationDriver(runner);
            if (driver == null)
            {
                continue;
            }

            driver.ClearCrouch();
            driver.ClearActionStates();
            driver.ApplyStates();
            driver.RefreshAnimatorImmediate();
        }
    }

    void PrepareRunnerForMovement(TeamRunner runner)
    {
        SetAnimatorPlayback(runner, true);

        var driver = GetAnimationDriver(runner);
        if (driver == null)
        {
            return;
        }

        driver.ClearAllStates();
        if (ShouldHoldCrouchUntilRun(runner))
        {
            driver.SetCrouchPose(false);
        }

        driver.ApplyStates();
        driver.RefreshAnimatorImmediate();
    }

    void ApplyCountdownPose(List<TeamRunner> team)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            var driver = GetAnimationDriver(runner);
            if (driver == null)
            {
                continue;
            }

            driver.ClearMovement();
            driver.ClearActionStates();
            driver.SetBallHutHut(false);
            driver.SetCrouchPose(false);

            driver.ApplyStates();
            driver.RefreshAnimatorImmediate();
        }
    }

    void ClearCountdownPose(List<TeamRunner> team, bool preserveDeferredHutHutRunners = false)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            var driver = GetAnimationDriver(runner);
            if (driver == null)
            {
                continue;
            }

            if (ShouldHoldCrouchUntilRun(runner) ||
                (preserveDeferredHutHutRunners &&
                 runner != null &&
                 (runner.runtimeAwaitingHutHutRelease || runner.runtimeDelayedStartPending)))
            {
                ApplyHeldCrouchPose(runner, driver);

                driver.SetBallHutHut(false);
                driver.ApplyStates();
                driver.RefreshAnimatorImmediate();
                continue;
            }

            driver.ClearCrouch();
            driver.SetBallHutHut(false);
            driver.ApplyStates();
            driver.RefreshAnimatorImmediate();
        }
    }

    bool ShouldHoldCrouchUntilRun(TeamRunner runner)
    {
        return runner != null &&
               (ShouldUseDelayedCrouch(runner) ||
                ShouldUseDelayedCrouchWithBall(runner) ||
                runner.startInCrouchIdle ||
                runner.startInCrouchBallHold);
    }

    void ApplyHeldCrouchPose(TeamRunner runner, FootballAnimationStateDriver driver)
    {
        if (driver == null)
        {
            return;
        }

        driver.ClearMovement();
        driver.ClearActionStates();
        driver.SetCrouchPose(false);
    }

    IEnumerator PlayHutHut(List<TeamRunner> team, bool onlyDeferredRunners = false)
    {
        var maxDuration = 0f;

        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null || !runner.playBallHutHutOnStart)
            {
                continue;
            }

            if (onlyDeferredRunners && !runner.runtimeAwaitingHutHutRelease)
            {
                continue;
            }

            var driver = GetAnimationDriver(runner);
            if (driver == null)
            {
                continue;
            }

            runner.runtimeAwaitingHutHutRelease = false;
            driver.ClearCrouch();
            driver.SetBallHutHut(true);
            driver.ApplyStates();
            driver.RefreshAnimatorImmediate();
            maxDuration = Mathf.Max(maxDuration, runner.ballHutHutDuration);
        }

        if (maxDuration > 0f)
        {
            yield return new WaitForSeconds(maxDuration);
        }

        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null || !runner.playBallHutHutOnStart)
            {
                continue;
            }

            var driver = GetAnimationDriver(runner);
            if (driver == null)
            {
                continue;
            }

            if (ShouldHoldCrouchUntilRun(runner))
            {
                ApplyHeldCrouchPose(runner, driver);
            }

            driver.SetBallHutHut(false);
            driver.ApplyStates();
            driver.RefreshAnimatorImmediate();
        }
    }

    IEnumerator PlayPointAnimation(TeamRunner runner, MotionPoint point, System.Action<bool> onComplete)
    {
        var shouldStopPath = point.stopPathAfterAction;
        var driver = GetAnimationDriver(runner);
        var duration = Mathf.Max(0f, point.animationHoldDuration);

        switch (point.pointAnimationType)
        {
            case PointAnimationType.Push:
                break;

            case PointAnimationType.HugPush:
                break;

            case PointAnimationType.BallCatch:
                if (driver != null)
                {
                    driver.SetBallCatch(true);
                    driver.ApplyStates();
                }
                if (duration > 0f)
                {
                    yield return new WaitForSeconds(duration);
                }
                if (driver != null)
                {
                    driver.SetBallCatch(false);
                    driver.ApplyStates();
                }
                if (point.giveRunnerBallAfterAction)
                {
                    runner.runtimeHasBall = true;
                }
                break;

            case PointAnimationType.FallDown:
                ApplyLocomotionAnimation(runner, false, -1);
                if (driver != null)
                {
                    driver.ClearAllStates();
                    driver.SetFallDown(true);
                    driver.SetHasBall(runner.runtimeHasBall);
                    driver.ApplyStates();
                    driver.RefreshAnimatorImmediate();
                    ForceAnimatorState(runner, "FallDown", 0f);
                }
                shouldStopPath = true;
                if (duration > 0f)
                {
                    yield return new WaitForSeconds(duration);
                }
                break;
        }

        onComplete?.Invoke(shouldStopPath);
    }

    void ApplySegmentActionAnimation(TeamRunner runner, MotionPoint point, bool enabled)
    {
        var driver = GetAnimationDriver(runner);
        if (driver == null || point == null)
        {
            return;
        }

        driver.SetPush(false);
        driver.SetHugPush(false);

        if (enabled)
        {
            if (point.pointAnimationType == PointAnimationType.Push)
            {
                driver.SetPush(true);
            }
            else if (point.pointAnimationType == PointAnimationType.HugPush)
            {
                driver.SetHugPush(true);
            }
            else if (point.pointAnimationType == PointAnimationType.FallDown)
            {
                driver.SetPush(true);
            }
        }

        driver.ApplyStates();
    }

    void PrimeStartLocomotion(TeamRunner runner)
    {
        if (runner == null || runner.actor == null || runner.path == null || runner.path.Count < 2)
        {
            return;
        }

        var start = GetResolvedPosition(runner, 0);
        var next = GetResolvedPosition(runner, 1);
        if (runner.keepYFromActor)
        {
            start.y = runner.actor.position.y;
            next.y = runner.actor.position.y;
        }

        var direction = next - start;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = runner.actor.forward;
        }

        var velocity = direction.normalized * Mathf.Max(0.01f, runner.maxMoveSpeed);
        ApplyLocomotionAnimation(runner, true, 0, velocity, 1f);
        ForceDriverLocomotion(runner, velocity, 1f);
        SetAnimatorSpeed(runner, 1f);
        ForceAnimatorState(runner, "Locomotion", 0.05f);
    }

    IEnumerator AlignActorsForBump(TeamRunner runner, MotionPoint motionPoint)
    {
        if (runner == null || runner.actor == null || motionPoint == null)
        {
            yield break;
        }

        var actor = runner.actor;
        var partner = motionPoint.bumpPartner;
        if (partner == null)
        {
            yield break;
        }

        var actorToPartner = partner.position - actor.position;
        actorToPartner.y = 0f;
        if (actorToPartner.sqrMagnitude < 0.0001f)
        {
            yield break;
        }

        var actorStartRotation = actor.rotation;
        var actorTargetRotation = Quaternion.LookRotation(actorToPartner.normalized, Vector3.up);

        Quaternion partnerStartRotation = Quaternion.identity;
        Quaternion partnerTargetRotation = Quaternion.identity;
        var rotatePartner = motionPoint.stabilizeFacingDuringBump;
        if (rotatePartner)
        {
            var partnerToActor = -actorToPartner;
            partnerStartRotation = partner.rotation;
            partnerTargetRotation = Quaternion.LookRotation(partnerToActor.normalized, Vector3.up);
        }

        const float alignDuration = 0.08f;
        var elapsed = 0f;
        while (elapsed < alignDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / alignDuration);
            var eased = Mathf.SmoothStep(0f, 1f, t);
            actor.rotation = Quaternion.Slerp(actorStartRotation, actorTargetRotation, eased);

            if (rotatePartner)
            {
                partner.rotation = Quaternion.Slerp(partnerStartRotation, partnerTargetRotation, eased);
            }

            yield return null;
        }

        actor.rotation = actorTargetRotation;
        if (rotatePartner)
        {
            partner.rotation = partnerTargetRotation;
        }
    }

    IEnumerator PlayBumpEffect(TeamRunner runner, MotionPoint motionPoint, bool isTerminalPoint)
    {
        var actor = runner.actor;
        if (actor == null)
        {
            yield break;
        }

        var driver = GetAnimationDriver(runner);
        ApplyLocomotionAnimation(runner, false, -1);
        if (driver != null)
        {
            if (motionPoint.pointAnimationType == PointAnimationType.Push)
            {
                driver.ClearActionStates();
                driver.SetPush(true);
                driver.ApplyStates();
            }
            else if (motionPoint.pointAnimationType == PointAnimationType.HugPush)
            {
                driver.ClearActionStates();
                driver.SetHugPush(true);
                driver.ApplyStates();
            }
        }

        if (bumpLockedActors.Contains(actor))
        {
            yield break;
        }

        var originalPosition = actor.position;
        var originalRotation = actor.rotation;
        var partner = motionPoint.bumpPartner;

        if (partner != null && bumpLockedActors.Contains(partner))
        {
            yield break;
        }

        bumpLockedActors.Add(actor);
        if (partner != null)
        {
            bumpLockedActors.Add(partner);
        }

        var originalPartnerPosition = partner != null ? partner.position : Vector3.zero;
        var originalPartnerRotation = partner != null ? partner.rotation : Quaternion.identity;
        var cycleCount = Mathf.Max(1, motionPoint.bumpFrequency);
        var totalDuration = Mathf.Max(0.01f, motionPoint.bumpDuration);
        var cycleDuration = totalDuration / cycleCount;
        var holdDuration = cycleDuration * Mathf.Clamp01(motionPoint.impactHoldRatio);
        var activeDuration = Mathf.Max(0.01f, cycleDuration - holdDuration);
        var pushDuration = activeDuration * 0.45f;
        var recoilDuration = activeDuration * 0.55f;
        var direction = ResolveBumpDirection(actor, motionPoint);
        var dip = Vector3.down * motionPoint.bodyDipDistance;
        var leanRotation = Quaternion.AngleAxis(motionPoint.bodyLeanAngle, actor.right);
        var actorContactRotation = originalRotation;
        var partnerContactRotation = originalPartnerRotation;

        if (partner != null && motionPoint.stabilizeFacingDuringBump)
        {
            var towardPartner = partner.position - actor.position;
            towardPartner.y = 0f;
            if (towardPartner.sqrMagnitude > 0.0001f)
            {
                actorContactRotation = Quaternion.LookRotation(towardPartner.normalized, Vector3.up);
                partnerContactRotation = Quaternion.LookRotation((-towardPartner).normalized, Vector3.up);
                leanRotation = Quaternion.AngleAxis(motionPoint.bodyLeanAngle, actorContactRotation * Vector3.right);
            }
        }

        SetAnimatorSpeed(runner, 0f);

        for (var cycle = 0; cycle < cycleCount; cycle++)
        {
            var pushElapsed = 0f;
            while (pushElapsed < pushDuration)
            {
                yield return WaitWhileScenarioPaused(runner);
                pushElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(pushElapsed / Mathf.Max(0.01f, pushDuration));
                var eased = EaseOutCubic(t);
                var offset = direction * (motionPoint.bumpDistance * eased);
                var verticalOffset = dip * Mathf.Sin(eased * Mathf.PI);
                actor.position = originalPosition + offset + verticalOffset;
                actor.rotation = Quaternion.Slerp(originalRotation, actorContactRotation * leanRotation, eased);

                if (partner != null)
                {
                    var partnerOffset = direction * (motionPoint.bumpDistance * motionPoint.partnerReactionMultiplier * eased);
                    partner.position = originalPartnerPosition - partnerOffset + verticalOffset * 0.5f;
                    partner.rotation = Quaternion.Slerp(originalPartnerRotation, partnerContactRotation * Quaternion.Inverse(leanRotation), eased);
                }

                yield return null;
            }

            var actorPeak = originalPosition + direction * motionPoint.bumpDistance;
            var partnerPeak = partner != null
                ? originalPartnerPosition - direction * (motionPoint.bumpDistance * motionPoint.partnerReactionMultiplier)
                : Vector3.zero;

            var holdElapsed = 0f;
            while (holdElapsed < holdDuration)
            {
                yield return WaitWhileScenarioPaused(runner);
                holdElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(holdElapsed / Mathf.Max(0.01f, holdDuration));
                var compress = 1f - 0.15f * Mathf.Sin(t * Mathf.PI);
                actor.position = originalPosition + direction * (motionPoint.bumpDistance * compress) + dip;
                actor.rotation = Quaternion.Slerp(actor.rotation, actorContactRotation * leanRotation, 1f - Mathf.Exp(-10f * Time.deltaTime));

                if (partner != null)
                {
                    partner.position = originalPartnerPosition - direction * (motionPoint.bumpDistance * motionPoint.partnerReactionMultiplier * compress) + dip * 0.5f;
                    partner.rotation = Quaternion.Slerp(partner.rotation, partnerContactRotation * Quaternion.Inverse(leanRotation), 1f - Mathf.Exp(-10f * Time.deltaTime));
                }

                yield return null;
            }

            var recoilElapsed = 0f;
            while (recoilElapsed < recoilDuration)
            {
                yield return WaitWhileScenarioPaused(runner);
                recoilElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(recoilElapsed / Mathf.Max(0.01f, recoilDuration));
                var eased = isTerminalPoint ? Mathf.SmoothStep(0f, 1f, t) : EaseOutBack(t);
                actor.position = Vector3.Lerp(actorPeak, originalPosition, eased);
                actor.rotation = Quaternion.Slerp(actorContactRotation * leanRotation, originalRotation, eased);

                if (partner != null)
                {
                    partner.position = Vector3.Lerp(partnerPeak, originalPartnerPosition, eased);
                    partner.rotation = Quaternion.Slerp(partnerContactRotation * Quaternion.Inverse(leanRotation), originalPartnerRotation, eased);
                }
                yield return null;
            }
        }

        var settleElapsed = 0f;
        var settleDuration = Mathf.Max(0.01f, motionPoint.settleTime);
        var actorStart = actor.position;
        var partnerStart = partner != null ? partner.position : Vector3.zero;

        while (settleElapsed < settleDuration)
        {
            yield return WaitWhileScenarioPaused(runner);
            settleElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(settleElapsed / settleDuration);
            actor.position = Vector3.Lerp(actorStart, originalPosition, t);

            if (partner != null)
            {
                partner.position = Vector3.Lerp(partnerStart, originalPartnerPosition, t);
            }

            actor.rotation = Quaternion.Slerp(actor.rotation, originalRotation, t);
            yield return null;
        }

        actor.position = originalPosition;
        actor.rotation = originalRotation;

        if (partner != null)
        {
            partner.position = originalPartnerPosition;
            partner.rotation = originalPartnerRotation;
        }

        if (driver != null)
        {
            if (motionPoint.pointAnimationType == PointAnimationType.Push)
            {
                driver.SetPush(false);
                driver.ApplyStates();
            }
            else if (motionPoint.pointAnimationType == PointAnimationType.HugPush)
            {
                driver.SetHugPush(false);
                driver.ApplyStates();
            }
        }

        ApplyLocomotionAnimation(runner, true, -1);

        bumpLockedActors.Remove(actor);
        if (partner != null)
        {
            bumpLockedActors.Remove(partner);
        }
    }

    Vector3 ResolveBumpDirection(Transform actor, MotionPoint motionPoint)
    {
        Vector3 direction;

        switch (motionPoint.bumpDirectionMode)
        {
            case BumpDirectionMode.TowardPartner:
                if (motionPoint.bumpPartner != null)
                {
                    direction = motionPoint.bumpPartner.position - actor.position;
                    break;
                }
                direction = actor.forward;
                break;

            case BumpDirectionMode.CustomDirection:
                direction = motionPoint.customBumpDirection;
                break;

            default:
                direction = actor.forward;
                if (motionPoint.bumpPartner != null)
                {
                    var towardPartner = motionPoint.bumpPartner.position - actor.position;
                    towardPartner.y = 0f;
                    if (towardPartner.sqrMagnitude > 0.0001f)
                    {
                        towardPartner.Normalize();
                        var actorForward = actor.forward;
                        actorForward.y = 0f;
                        actorForward = actorForward.sqrMagnitude > 0.0001f ? actorForward.normalized : towardPartner;

                        if (Vector3.Dot(actorForward, towardPartner) < 0.35f)
                        {
                            direction = towardPartner;
                        }
                    }
                }
                break;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        return direction.normalized;
    }

    Vector3 GetResolvedPosition(TeamRunner runner, int index)
    {
        if (runner.path == null || runner.path.Count == 0)
        {
            return runner.actor != null ? runner.actor.position : Vector3.zero;
        }

        index = Mathf.Clamp(index, 0, runner.path.Count - 1);
        var point = runner.path[index];
        if (point == null || point.point == null)
        {
            return runner.actor != null ? runner.actor.position : Vector3.zero;
        }

        var basePosition = point.point.position;
        if (point.spaceMode != SpaceMode.PlayerRelative)
        {
            return basePosition;
        }

        var playerReference = runner.assignedPlayer != null ? runner.assignedPlayer : sharedPlayerReference;
        if (playerReference == null)
        {
            return basePosition;
        }

        return playerReference.position + point.point.localPosition;
    }

    static Vector3 GetLookTarget(Vector3 currentPoint, Vector3 nextPoint, Vector3 actorPosition, bool flattenY)
    {
        var forward = nextPoint - actorPosition;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = currentPoint - actorPosition;
        }

        if (flattenY)
        {
            forward.y = 0f;
        }

        return forward;
    }

    Vector3 GetSegmentFacingDirection(
        TeamRunner runner,
        MotionPoint destinationPoint,
        Vector3 actorPosition,
        Vector3 previousPoint,
        Vector3 currentPoint,
        Vector3 nextPoint,
        Vector3 followingPoint,
        float segmentT,
        bool usePostBumpRecoverySegment,
        Vector3 recoveryStartPosition)
    {
        if (destinationPoint != null &&
            destinationPoint.bumpPartner != null &&
            (destinationPoint.pointAnimationType == PointAnimationType.Push ||
             destinationPoint.pointAnimationType == PointAnimationType.HugPush))
        {
            var partnerDirection = destinationPoint.bumpPartner.position - actorPosition;
            if (runner != null && runner.keepYFromActor)
            {
                partnerDirection.y = 0f;
            }

            if (partnerDirection.sqrMagnitude > 0.0001f)
            {
                return partnerDirection.normalized;
            }
        }

        return GetLookAheadDirection(
            runner,
            actorPosition,
            previousPoint,
            currentPoint,
            nextPoint,
            followingPoint,
            segmentT,
            usePostBumpRecoverySegment,
            recoveryStartPosition);
    }

    Vector3 GetDirectPointFacingDirection(TeamRunner runner, Vector3 actorPosition, Vector3 nextPoint)
    {
        var direction = nextPoint - actorPosition;
        if (runner != null && runner.keepYFromActor)
        {
            direction.y = 0f;
        }

        return direction;
    }

    Vector3 GetLookAheadDirection(
        TeamRunner runner,
        Vector3 actorPosition,
        Vector3 previousPoint,
        Vector3 currentPoint,
        Vector3 nextPoint,
        Vector3 followingPoint,
        float segmentT,
        bool usePostBumpRecoverySegment,
        Vector3 recoveryStartPosition)
    {
        var lookAheadT = Mathf.Clamp01(segmentT + runner.rotationLookAhead);
        Vector3 currentSample;
        Vector3 futureSample;

        if (usePostBumpRecoverySegment)
        {
            currentSample = Vector3.Lerp(recoveryStartPosition, nextPoint, segmentT);
            futureSample = Vector3.Lerp(recoveryStartPosition, nextPoint, lookAheadT);
        }
        else
        {
            currentSample = GetCurvedSegmentPosition(runner, previousPoint, currentPoint, nextPoint, followingPoint, segmentT);
            futureSample = GetCurvedSegmentPosition(runner, previousPoint, currentPoint, nextPoint, followingPoint, lookAheadT);
        }

        var currentDirection = futureSample - actorPosition;
        var nextDirection = followingPoint - nextPoint;

        if (runner.keepYFromActor)
        {
            currentDirection.y = 0f;
            nextDirection.y = 0f;
            currentSample.y = actorPosition.y;
            futureSample.y = actorPosition.y;
        }

        if (currentDirection.sqrMagnitude < 0.0001f)
        {
            currentDirection = futureSample - currentSample;
            if (runner.keepYFromActor)
            {
                currentDirection.y = 0f;
            }
        }

        if (currentDirection.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        if (nextDirection.sqrMagnitude < 0.0001f)
        {
            return currentDirection.normalized;
        }

        var normalizedCurrent = currentDirection.normalized;
        var normalizedNext = nextDirection.normalized;
        var turnBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((segmentT - 0.2f) / 0.8f));
        return Vector3.Slerp(normalizedCurrent, normalizedNext, turnBlend).normalized;
    }

    Vector3 GetCurvedSegmentPosition(TeamRunner runner, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        if (runner == null)
        {
            return Vector3.Lerp(p1, p2, t);
        }

        var segmentDirection = p2 - p1;
        var segmentLength = segmentDirection.magnitude;
        if (segmentLength < 0.0001f)
        {
            return p1;
        }

        var tangentStart = p2 - p0;
        var tangentEnd = p3 - p1;
        if (runner.keepYFromActor)
        {
            tangentStart.y = 0f;
            tangentEnd.y = 0f;
        }

        if (tangentStart.sqrMagnitude < 0.0001f)
        {
            tangentStart = segmentDirection;
        }

        if (tangentEnd.sqrMagnitude < 0.0001f)
        {
            tangentEnd = segmentDirection;
        }

        var handleLength = segmentLength * Mathf.Clamp(runner.pathCurveStrength, 0f, 0.45f);
        if (handleLength <= 0.0001f)
        {
            return CatmullRom(p0, p1, p2, p3, t);
        }

        var control1 = p1 + tangentStart.normalized * handleLength;
        var control2 = p2 - tangentEnd.normalized * handleLength;
        return CubicBezier(p1, control1, control2, p2, t);
    }

    Quaternion GetBankedRotation(Quaternion currentRotation, Vector3 lookDirection, TeamRunner runner, float speedRatio)
    {
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return currentRotation;
        }

        var flatCurrentForward = currentRotation * Vector3.forward;
        flatCurrentForward.y = 0f;
        if (flatCurrentForward.sqrMagnitude < 0.0001f)
        {
            flatCurrentForward = lookDirection;
        }

        var flatLookDirection = lookDirection;
        flatLookDirection.y = 0f;
        if (flatLookDirection.sqrMagnitude < 0.0001f)
        {
            flatLookDirection = lookDirection;
        }

        flatCurrentForward.Normalize();
        flatLookDirection.Normalize();

        var targetRotation = Quaternion.LookRotation(flatLookDirection, Vector3.up);
        var signedTurn = Vector3.SignedAngle(flatCurrentForward, flatLookDirection, Vector3.up);
        var bankAmount = Mathf.Clamp(-signedTurn / 45f, -1f, 1f) * runner.turnTiltAngle * speedRatio;
        return targetRotation * Quaternion.AngleAxis(bankAmount, Vector3.forward);
    }

    Vector3 GetArrivalLookDirection(TeamRunner runner, int currentIndex)
    {
        if (runner == null || runner.path == null || runner.path.Count == 0)
        {
            return Vector3.zero;
        }

        var currentPosition = GetResolvedPosition(runner, currentIndex);
        Vector3 lookDirection;

        if (currentIndex < runner.path.Count - 1)
        {
            var nextPosition = GetResolvedPosition(runner, currentIndex + 1);
            lookDirection = nextPosition - currentPosition;
        }
        else if (currentIndex > 0)
        {
            var previousPosition = GetResolvedPosition(runner, currentIndex - 1);
            lookDirection = currentPosition - previousPosition;
        }
        else
        {
            lookDirection = Vector3.zero;
        }

        if (runner.keepYFromActor)
        {
            lookDirection.y = 0f;
        }

        return lookDirection;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        var invT = 1f - t;
        var invT2 = invT * invT;
        var invT3 = invT2 * invT;
        var t2 = t * t;
        var t3 = t2 * t;

        return invT3 * p0 +
               3f * invT2 * t * p1 +
               3f * invT * t2 * p2 +
               t3 * p3;
    }

    static float EaseOutCubic(float t)
    {
        var inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        var inv = t - 1f;
        return 1f + c3 * inv * inv * inv + c1 * inv * inv;
    }

    void SetMovingState(TeamRunner runner, bool isMoving)
    {
        if (runner.animator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(runner.movingBoolName) && HasParameter(runner.animator, runner.movingBoolName, AnimatorControllerParameterType.Bool))
        {
            runner.animator.SetBool(runner.movingBoolName, isMoving);
        }

        var triggerName = isMoving ? runner.startMoveTriggerName : runner.stopMoveTriggerName;
        if (!string.IsNullOrWhiteSpace(triggerName))
        {
            TriggerAnimator(runner, triggerName);
        }
    }

    FootballAnimationStateDriver GetAnimationDriver(TeamRunner runner)
    {
        if (runner == null)
        {
            return null;
        }

        if (runner.animationDriver != null)
        {
            return runner.animationDriver;
        }

        if (runner.actor == null)
        {
            return null;
        }

        if (runner.animator == null)
        {
            runner.animator = runner.actor.GetComponent<Animator>();
        }

        runner.animationDriver = runner.actor.GetComponent<FootballAnimationStateDriver>();
        if (runner.animationDriver == null)
        {
            if (runner.animator != null)
            {
                runner.animationDriver = runner.actor.gameObject.AddComponent<FootballAnimationStateDriver>();
            }
        }

        return runner.animationDriver;
    }

    void ApplyLocomotionAnimation(
        TeamRunner runner,
        bool isMoving,
        int segmentIndex = -1,
        Vector3 worldVelocity = default,
        float normalizedSpeed = 0f,
        PointAnimationType activeSegmentAction = PointAnimationType.None)
    {
        var driver = GetAnimationDriver(runner);
        if (driver == null)
        {
            return;
        }

        if (runner.runtimeDelayedStartPending && !ShouldForceLocomotionDuringDelayedStart(runner))
        {
            driver.ClearMovement();
            driver.SetSideWalk(false);
            driver.SetHasBall(false);
            driver.ClearActionStates();
            driver.SetCrouchPose(false);

            driver.ApplyStates();
            return;
        }

        var destinationPointIndex = segmentIndex + 1;
        var useSideWalk = isMoving && destinationPointIndex > 0 && destinationPointIndex < runner.openingSideWalkSegmentCount;
        var useOpeningCrouch = isMoving && destinationPointIndex > 0 && destinationPointIndex < runner.openingCrouchSegmentCount;

        if (!isMoving)
        {
            driver.ClearMovement();
            if (runner.runtimeDelayedStartPending && ShouldUseDelayedCrouch(runner))
            {
                driver.SetCrouchPose(false);
            }
            else
            {
                driver.ClearCrouch();
            }
            driver.SetHasBall(runner.runtimeHasBall);
            driver.ApplyStates();
            return;
        }

        ApplySegmentActionState(driver, activeSegmentAction);

        if (useOpeningCrouch)
        {
            driver.ClearMovement();
            driver.SetSideWalk(false);
            driver.SetHasBall(ShouldUseOpeningCrouchWithBall(runner) || runner.runtimeHasBall);

            if (ShouldUseOpeningCrouchWithBall(runner))
            {
                driver.SetCrouchPose(true);
            }
            else
            {
                driver.SetCrouchPose(false);
            }

            driver.ApplyStates();
            return;
        }

        driver.ClearMovement();
        driver.ClearCrouch();

        if (useSideWalk)
        {
            var sideDirection = 1f;
            var flattenedVelocity = worldVelocity;
            if (runner.keepYFromActor)
            {
                flattenedVelocity.y = 0f;
            }

            if (flattenedVelocity.sqrMagnitude > 0.0001f && runner.actor != null)
            {
                var localVelocity = runner.actor.InverseTransformDirection(flattenedVelocity.normalized);
                if (Mathf.Abs(localVelocity.x) > 0.05f)
                {
                    sideDirection = Mathf.Sign(localVelocity.x);
                }
            }

            driver.SetDirectionalLocomotion(sideDirection, 0f, Mathf.Max(normalizedSpeed, 0.55f), false);
            driver.SetSideWalk(true);
            driver.ApplyStates();
            return;
        }

        var carryingBall = runner.runtimeHasBall && runner.useBallGrabRunningWhenCarryingBall;
        var velocityForBlend = worldVelocity;
        if (runner.keepYFromActor)
        {
            velocityForBlend.y = 0f;
        }

        var localBlend = runner.actor != null
            ? runner.actor.InverseTransformDirection(velocityForBlend)
            : velocityForBlend;

        var maxSpeed = Mathf.Max(0.01f, runner.maxMoveSpeed);
        var lateral = Mathf.Clamp(localBlend.x / maxSpeed, -1f, 1f);
        var forward = Mathf.Clamp(localBlend.z / maxSpeed, -1f, 1f);
        var forwardBlend = Mathf.Max(0f, forward);

        if (forwardBlend < 0.05f && normalizedSpeed > 0.05f && Mathf.Abs(lateral) < 0.15f)
        {
            forwardBlend = normalizedSpeed;
        }

        driver.SetSideWalk(false);
        driver.SetDirectionalLocomotion(lateral, forwardBlend, normalizedSpeed, carryingBall);
        driver.ApplyStates();
    }

    void ApplyRouteEndAnimation(TeamRunner runner, bool completedRoute)
    {
        var driver = GetAnimationDriver(runner);
        if (driver == null)
        {
            SetAnimatorPlayback(runner, false);
            return;
        }

        driver.ClearMovement();
        driver.ClearCrouch();
        driver.ClearActionStates();
        driver.SetHasBall(runner.runtimeHasBall);
        var playIdle = completedRoute && ShouldPlayIdleAtRouteEnd(runner);
        driver.SetIdle(playIdle);
        driver.ApplyStates();

        if (playIdle)
        {
            driver.RefreshAnimatorImmediate();
            ForceAnimatorState(runner, "Idle", 0f);
        }

        SetAnimatorPlayback(runner, playIdle);
    }

    bool ShouldPlayIdleAtRouteEnd(TeamRunner runner)
    {
        if (runner == null || runner.path == null || runner.path.Count == 0)
        {
            return false;
        }

        var lastPoint = runner.path[runner.path.Count - 1];
        return lastPoint == null || lastPoint.pointAnimationType == PointAnimationType.None;
    }

    void ApplySegmentActionState(FootballAnimationStateDriver driver, PointAnimationType activeSegmentAction)
    {
        if (driver == null)
        {
            return;
        }

        driver.ClearActionStates();

        if (activeSegmentAction == PointAnimationType.Push)
        {
            driver.SetPush(true);
        }
        else if (activeSegmentAction == PointAnimationType.HugPush)
        {
            driver.SetHugPush(true);
        }
        else if (activeSegmentAction == PointAnimationType.FallDown)
        {
            driver.SetPush(true);
        }
    }

    IEnumerator WaitWhileScenarioPaused(TeamRunner runner = null)
    {
        if (!VRFootballScenarioController.IsWorldPaused)
        {
            yield break;
        }

        if (runner != null)
        {
            SetAnimatorPlayback(runner, false);
            SetAnimatorSpeed(runner, 0f);
        }

        while (VRFootballScenarioController.IsWorldPaused)
        {
            yield return null;
        }

        if (runner != null)
        {
            SetAnimatorPlayback(runner, true);
        }
    }

    void SetAnimatorSpeed(TeamRunner runner, float normalizedSpeed)
    {
        if (runner.animator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(runner.speedFloatName) && HasParameter(runner.animator, runner.speedFloatName, AnimatorControllerParameterType.Float))
        {
            runner.animator.SetFloat(runner.speedFloatName, normalizedSpeed);
        }
    }

    void SetAnimatorPlayback(TeamRunner runner, bool isPlaying)
    {
        if (runner == null || runner.animator == null)
        {
            return;
        }

        runner.animator.speed = isPlaying && !VRFootballScenarioController.IsWorldPaused ? 1f : 0f;
    }

    void ForceDriverLocomotion(TeamRunner runner, Vector3 worldVelocity, float normalizedSpeed)
    {
        var driver = GetAnimationDriver(runner);
        if (driver == null)
        {
            return;
        }

        var velocityForBlend = worldVelocity;
        if (runner.keepYFromActor)
        {
            velocityForBlend.y = 0f;
        }

        var localBlend = runner.actor != null
            ? runner.actor.InverseTransformDirection(velocityForBlend)
            : velocityForBlend;

        var maxSpeed = Mathf.Max(0.01f, runner.maxMoveSpeed);
        var lateral = Mathf.Clamp(localBlend.x / maxSpeed, -1f, 1f);
        var forward = Mathf.Clamp(localBlend.z / maxSpeed, -1f, 1f);
        var forwardBlend = Mathf.Max(0f, forward);

        if (forwardBlend < 0.05f && normalizedSpeed > 0.05f && Mathf.Abs(lateral) < 0.15f)
        {
            forwardBlend = normalizedSpeed;
        }

        driver.ForceLocomotion(lateral, forwardBlend, normalizedSpeed, runner.runtimeHasBall && runner.useBallGrabRunningWhenCarryingBall);
    }

    void ForceAnimatorState(TeamRunner runner, string stateName, float transitionDuration)
    {
        if (runner == null || runner.animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        var stateHash = Animator.StringToHash(stateName);
        if (!runner.animator.HasState(0, stateHash))
        {
            return;
        }

        runner.animator.CrossFadeInFixedTime(stateName, Mathf.Max(0f, transitionDuration));
        runner.animator.Update(0f);
    }

    void DesyncCentreAnimatorPhase(TeamRunner runner)
    {
        if (!ShouldAlwaysFaceNextPoint(runner) || runner.animator == null)
        {
            return;
        }

        runner.animator.Update(GetDeterministicAnimatorPhaseOffset(runner.playerName));
    }

    static float GetDeterministicAnimatorPhaseOffset(string runnerName)
    {
        if (string.IsNullOrWhiteSpace(runnerName))
        {
            return 0f;
        }

        unchecked
        {
            var hash = 17;
            for (var i = 0; i < runnerName.Length; i++)
            {
                hash = hash * 31 + runnerName[i];
            }

            var normalized = Mathf.Abs(hash % 1000) / 1000f;
            return Mathf.Lerp(0.05f, 0.22f, normalized);
        }
    }

    static bool ShouldAlwaysFaceNextPoint(TeamRunner runner)
    {
        if (runner == null || string.IsNullOrWhiteSpace(runner.playerName))
        {
            return false;
        }

        var name = runner.playerName.Trim();
        return name.Equals("Centre 1", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Centre 2", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Centre 3", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Center 1", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Center 2", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Center 3", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Left Defend", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Fake", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Faker", System.StringComparison.OrdinalIgnoreCase);
    }

    bool ShouldUseOpeningCrouch(TeamRunner runner)
    {
        if (runner == null)
        {
            return false;
        }

        if (runner.openingCrouchSegmentCount <= 0)
        {
            return false;
        }

        if (runner.startInCrouchIdle || runner.startInCrouchBallHold)
        {
            return true;
        }

        var name = runner.playerName ?? string.Empty;
        if (name.Equals("Gole", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Goal", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Centre 1", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Centre 2", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Centre 3", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    bool ShouldUseOpeningCrouchWithBall(TeamRunner runner)
    {
        return runner != null &&
               runner.openingCrouchSegmentCount > 0 &&
               (runner.startInCrouchBallHold || runner.openingCrouchUsesBallHold);
    }

    bool ShouldUseDelayedCrouch(TeamRunner runner)
    {
        return runner != null &&
               !ShouldForceLocomotionDuringDelayedStart(runner) &&
               (!runner.moveBeforeHutHut ||
                runner.runtimeDelayedStartPending ||
                ShouldUseOpeningCrouch(runner));
    }

    bool ShouldUseDelayedCrouchWithBall(TeamRunner runner)
    {
        return runner != null &&
               !ShouldForceLocomotionDuringDelayedStart(runner) &&
               (runner.startInCrouchBallHold ||
                runner.openingCrouchUsesBallHold ||
                runner.startsWithBall ||
                runner.playBallHutHutOnStart);
    }

    bool ShouldForceLocomotionDuringDelayedStart(TeamRunner runner)
    {
        if (runner == null || string.IsNullOrWhiteSpace(runner.playerName))
        {
            return false;
        }

        var name = runner.playerName.Trim();
        return name.Equals("Fake", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Passer Right Facing", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Passer Left second", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Passer Left Second", System.StringComparison.OrdinalIgnoreCase);
    }

    void TriggerAnimator(TeamRunner runner, string triggerName)
    {
        if (runner.animator == null)
        {
            return;
        }

        if (HasParameter(runner.animator, triggerName, AnimatorControllerParameterType.Trigger))
        {
            runner.animator.SetTrigger(triggerName);
        }
    }

    static bool HasParameter(Animator animator, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == type && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    void OnDrawGizmos()
    {
        DrawTeamGizmos(playerTeam, new Color(0.2f, 0.8f, 1f, 0.9f));
        DrawTeamGizmos(opponentTeam, new Color(1f, 0.35f, 0.35f, 0.9f));
    }

    void DrawTeamGizmos(List<TeamRunner> team, Color color)
    {
        if (team == null)
        {
            return;
        }

        Gizmos.color = color;

        for (var i = 0; i < team.Count; i++)
        {
            var runner = team[i];
            if (runner == null || runner.path == null)
            {
                continue;
            }

            Vector3? previous = null;
            for (var j = 0; j < runner.path.Count; j++)
            {
                var motionPoint = runner.path[j];
                if (motionPoint == null || motionPoint.point == null)
                {
                    continue;
                }

                var current = motionPoint.point.position;
                Gizmos.DrawSphere(current, 0.12f);

                if (previous.HasValue)
                {
                    DrawCurvedGizmoSegment(runner, j - 1, previous.Value, current);
                }

                previous = current;

#if UNITY_EDITOR
                Handles.color = color;
                Handles.Label(current + Vector3.up * 0.2f, $"{runner.playerName} - {motionPoint.pointName}");
#endif
            }
        }
    }

    void DrawCurvedGizmoSegment(TeamRunner runner, int segmentStartIndex, Vector3 from, Vector3 to)
    {
        if (runner == null || runner.path == null || runner.path.Count < 3 || segmentStartIndex < 0)
        {
            Gizmos.DrawLine(from, to);
            return;
        }

        var p0 = GetResolvedPosition(runner, segmentStartIndex - 1);
        var p1 = GetResolvedPosition(runner, segmentStartIndex);
        var p2 = GetResolvedPosition(runner, segmentStartIndex + 1);
        var p3 = GetResolvedPosition(runner, segmentStartIndex + 2);

        var previousPoint = p1;
        const int steps = 10;
        for (var step = 1; step <= steps; step++)
        {
            var t = step / (float)steps;
            var currentPoint = GetCurvedSegmentPosition(runner, p0, p1, p2, p3, t);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}
