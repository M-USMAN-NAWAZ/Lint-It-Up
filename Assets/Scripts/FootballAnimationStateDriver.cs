using System.Collections.Generic;
using UnityEngine;

public class FootballAnimationStateDriver : MonoBehaviour
{
    public const string IdleParam = "Idle";
    public const string RunningParam = "Running";
    public const string BallGrabRunningParam = "BallGrabRunning";
    public const string BallCatchParam = "BallCatch";
    public const string PushParam = "Push";
    public const string HugPushParam = "HugPush";
    public const string FallDownParam = "FallDown";
    public const string BallHutHutParam = "BallHutHut";
    public const string CrouchIdleParam = "CrouchIdle";
    public const string CrouchBallHoldParam = "CrouchBallHold";
    public const string SideWalkParam = "SideWalk";
    public const string HasBallParam = "HasBall";

    static readonly string[] s_BoolParameters =
    {
        IdleParam,
        RunningParam,
        BallGrabRunningParam,
        BallCatchParam,
        PushParam,
        HugPushParam,
        FallDownParam,
        BallHutHutParam,
        CrouchIdleParam,
        CrouchBallHoldParam,
        SideWalkParam,
        HasBallParam
    };

    [SerializeField] Animator animator;
    [SerializeField] bool idle;
    [SerializeField] bool running;
    [SerializeField] bool ballGrabRunning;
    [SerializeField] bool ballCatch;
    [SerializeField] bool push;
    [SerializeField] bool hugPush;
    [SerializeField] bool fallDown;
    [SerializeField] bool ballHutHut;
    [SerializeField] bool crouchIdle;
    [SerializeField] bool crouchBallHold;
    [SerializeField] bool sideWalk;
    [SerializeField] bool hasBall;
    [SerializeField] bool applyStatesEveryFrame = true;
    [SerializeField] bool logBoolChanges = true;
    [SerializeField] bool drivePushLayerWeight = true;
    [SerializeField] string pushLayerName = "Push";
    [SerializeField] float pushLayerActiveWeight = 0.4f;
    [SerializeField] float pushLayerWeightChangeSpeed = 6f;

    readonly Dictionary<string, bool> boolParameterCache = new Dictionary<string, bool>();
    readonly Dictionary<string, bool> lastAppliedBoolStates = new Dictionary<string, bool>();
    int cachedPushLayerIndex = -2;

    public Animator Animator => animator;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            CacheParameters();
            ClearAllStates();
            ApplyStates();
        }
    }

    void LateUpdate()
    {
        if (applyStatesEveryFrame)
        {
            ApplyStates();
        }
    }

    public void SetIdle(bool value)
    {
        idle = value;
        if (value)
        {
            ClearMovement();
            ClearCrouch();
            ClearActionStates();
        }
    }

    public void SetRunning(bool value)
    {
        running = value;
        if (value)
        {
            idle = false;
            sideWalk = false;
            ClearCrouch();
        }
    }

    public void SetBallGrabRunning(bool value)
    {
        ballGrabRunning = false;
        hasBall = value || hasBall;
    }

    public void SetBallCatch(bool value)
    {
        ballCatch = value;
        if (value)
        {
            idle = false;
        }
    }

    public void SetPush(bool value)
    {
        push = value;
        if (value)
        {
            idle = false;
        }
    }

    public void SetHugPush(bool value)
    {
        hugPush = value;
        if (value)
        {
            idle = false;
        }
    }

    public void SetFallDown(bool value)
    {
        fallDown = value;
        if (value)
        {
            idle = false;
        }
    }

    public void SetBallHutHut(bool value)
    {
        ballHutHut = value;
        if (value)
        {
            idle = false;
        }
    }

    public void SetCrouchIdle(bool value)
    {
        crouchIdle = value;
        if (value)
        {
            idle = false;
        }
    }

    public void SetCrouchBallHold(bool value)
    {
        crouchBallHold = value;
        hasBall = value || hasBall;
        if (value)
        {
            idle = false;
        }
    }

    public void SetSideWalk(bool value)
    {
        sideWalk = value;
        if (value)
        {
            idle = false;
            running = false;
            ClearCrouch();
        }
    }

    public void SetHasBall(bool value)
    {
        hasBall = value;
    }

    public void SetLocomotion(bool isMoving, bool carryingBall)
    {
        SetDirectionalLocomotion(0f, isMoving ? 1f : 0f, isMoving ? 1f : 0f, carryingBall);
    }

    public void SetDirectionalLocomotion(float lateral, float forward, float normalizedSpeed, bool carryingBall)
    {
        var isMoving = normalizedSpeed > 0.01f;
        var useSideWalk = isMoving && Mathf.Abs(lateral) > Mathf.Abs(forward);

        idle = false;
        running = isMoving && !useSideWalk;
        
        

        sideWalk = useSideWalk;
        ballGrabRunning = false;
        hasBall = carryingBall;

        if (isMoving)
        {
            ClearCrouch();
        }
    }

    public void ForceLocomotion(float lateral, float forward, float normalizedSpeed, bool carryingBall)
    {
        ClearAllStates();
        SetDirectionalLocomotion(lateral, forward, Mathf.Max(normalizedSpeed, 0.05f), carryingBall);
        ApplyStates();
        RefreshAnimatorImmediate();
    }

    public void SetCrouchPose(bool withBall)
    {
        idle = false;
        running = false;
        sideWalk = false;
        crouchIdle = !withBall;
        crouchBallHold = withBall;
        hasBall = withBall;
    }

    public void ClearCrouch()
    {
        crouchIdle = false;
        crouchBallHold = false;
    }

    public void ClearMovement()
    {
        running = false;
        ballGrabRunning = false;
        sideWalk = false;
        hasBall = false;
    }

    public void ClearActionStates()
    {
        ballCatch = false;
        push = false;
        hugPush = false;
        fallDown = false;
        ballHutHut = false;
    }

    public void ClearAllStates()
    {
        idle = false;
        running = false;
        ballGrabRunning = false;
        ballCatch = false;
        push = false;
        hugPush = false;
        fallDown = false;
        ballHutHut = false;
        crouchIdle = false;
        crouchBallHold = false;
        sideWalk = false;
        hasBall = false;
    }

    public void ApplyStates()
    {
        if (animator == null)
        {
            return;
        }

        var resolvedIdle = idle;
        var resolvedRunning = running;
        var resolvedBallGrabRunning = ballGrabRunning;
        var resolvedBallCatch = ballCatch;
        var resolvedPush = push;
        var resolvedHugPush = hugPush;
        var resolvedFallDown = fallDown;
        var resolvedBallHutHut = ballHutHut;
        var resolvedCrouchIdle = crouchIdle;
        var resolvedCrouchBallHold = crouchBallHold;
        var resolvedSideWalk = sideWalk;
        var resolvedHasBall = hasBall || resolvedCrouchBallHold;

        if (resolvedFallDown)
        {
            resolvedIdle = false;
            resolvedRunning = false;
            resolvedBallCatch = false;
            resolvedPush = false;
            resolvedHugPush = false;
            resolvedBallHutHut = false;
            resolvedCrouchIdle = false;
            resolvedCrouchBallHold = false;
            resolvedSideWalk = false;
        }
        else if (resolvedBallCatch)
        {
            resolvedIdle = false;
            resolvedRunning = false;
            resolvedPush = false;
            resolvedHugPush = false;
            resolvedBallHutHut = false;
            resolvedCrouchIdle = false;
            resolvedCrouchBallHold = false;
            resolvedSideWalk = false;
        }
        else if (resolvedPush || resolvedHugPush)
        {
            resolvedIdle = false;
            resolvedRunning = false;
            resolvedBallHutHut = false;
            resolvedCrouchIdle = false;
            resolvedCrouchBallHold = false;
            resolvedSideWalk = false;
        }
        else if (resolvedBallHutHut)
        {
            resolvedIdle = false;
            resolvedRunning = false;
            resolvedCrouchIdle = false;
            resolvedCrouchBallHold = false;
            resolvedSideWalk = false;
        }
        else if (resolvedCrouchIdle || resolvedCrouchBallHold)
        {
            resolvedIdle = false;
            resolvedRunning = false;
            resolvedSideWalk = false;
            resolvedHasBall = resolvedCrouchBallHold;
            //Debug.Log("running false");
        }
        else if (resolvedSideWalk)
        {
            resolvedIdle = false;
            resolvedRunning = false;
            resolvedBallGrabRunning = false;
        }
        else if (resolvedRunning)
        {
            resolvedIdle = false;
            resolvedBallGrabRunning = false;
        }

        SetAnimatorBool(IdleParam, resolvedIdle);
        SetAnimatorBool(RunningParam, resolvedRunning);
        SetAnimatorBool(BallGrabRunningParam, resolvedBallGrabRunning);
        SetAnimatorBool(BallCatchParam, resolvedBallCatch);
        SetAnimatorBool(PushParam, resolvedPush);
        SetAnimatorBool(HugPushParam, resolvedHugPush);
        SetAnimatorBool(FallDownParam, resolvedFallDown);
        SetAnimatorBool(BallHutHutParam, resolvedBallHutHut);
        SetAnimatorBool(CrouchIdleParam, resolvedCrouchIdle);
        SetAnimatorBool(CrouchBallHoldParam, resolvedCrouchBallHold);
        SetAnimatorBool(SideWalkParam, resolvedSideWalk);
        SetAnimatorBool(HasBallParam, resolvedHasBall);
        ApplyPushLayerWeight(resolvedPush || resolvedHugPush);
    }

    public void RefreshAnimatorImmediate()
    {
        if (animator == null)
        {
            return;
        }

        animator.Update(0f);
    }

    void CacheParameters()
    {
        boolParameterCache.Clear();
        lastAppliedBoolStates.Clear();

        foreach (var parameterName in s_BoolParameters)
        {
            boolParameterCache[parameterName] = false;
            lastAppliedBoolStates[parameterName] = false;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                boolParameterCache[parameter.name] = true;
            }
        }
    }

    void SetAnimatorBool(string parameterName, bool value)
    {
        if (!boolParameterCache.TryGetValue(parameterName, out var exists) || !exists)
        {
            return;
        }

        if (!lastAppliedBoolStates.TryGetValue(parameterName, out var previousValue))
        {
            previousValue = animator.GetBool(parameterName);
            lastAppliedBoolStates[parameterName] = previousValue;
        }

        if (previousValue != value)
        {
            if (logBoolChanges)
            {
                Debug.Log($"[{name}] Animator bool '{parameterName}' -> {value}", this);
            }

            lastAppliedBoolStates[parameterName] = value;
        }

        animator.SetBool(parameterName, value);
    }

    void ApplyPushLayerWeight(bool pushIsActive)
    {
        if (!drivePushLayerWeight)
        {
            return;
        }

        var pushLayerIndex = ResolvePushLayerIndex();
        if (pushLayerIndex < 0)
        {
            return;
        }

        var targetWeight = pushIsActive ? pushLayerActiveWeight : 0f;
        var currentWeight = animator.GetLayerWeight(pushLayerIndex);
        var nextWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            pushLayerWeightChangeSpeed * Time.deltaTime);

        animator.SetLayerWeight(pushLayerIndex, nextWeight);
    }

    int ResolvePushLayerIndex()
    {
        if (animator == null)
        {
            return -1;
        }

        if (cachedPushLayerIndex >= -1)
        {
            return cachedPushLayerIndex;
        }

        cachedPushLayerIndex = -1;

        if (animator.layerCount > 1 && animator.GetLayerName(1) == pushLayerName)
        {
            cachedPushLayerIndex = 1;
            return cachedPushLayerIndex;
        }

        for (var layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            if (animator.GetLayerName(layerIndex) == pushLayerName)
            {
                cachedPushLayerIndex = layerIndex;
                break;
            }
        }

        return cachedPushLayerIndex;
    }
}
