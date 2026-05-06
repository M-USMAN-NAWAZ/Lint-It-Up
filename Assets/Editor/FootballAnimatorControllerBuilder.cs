using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class FootballAnimatorControllerBuilder
{
    const string ControllerPath = "Assets/Animations/Football/FootballPlayerBlendTree.controller";
    const string PreSnapBallParam = "PreSnapBall";
    const string MoveXParam = "MoveX";
    const string MoveZParam = "MoveZ";
    const string MoveSpeedParam = "MoveSpeed";

    const string RunningClipPath = "Assets/Animations/Football/Running.anim";
    const string SideWalkClipPath = "Assets/Animations/Football/SideWalk.anim";
    const string SideRunClipPath = "Assets/Animations/Football/SideRun.anim";
    const string BallCatchClipPath = "Assets/Animations/Football/ball Catch.anim";
    const string PushClipPath = "Assets/Animations/Football/Push.anim";
    const string HugPushClipPath = "Assets/Animations/Football/Hug Push.anim";
    const string FallDownClipPath = "Assets/Animations/Football/Fall Down.anim";
    const string BallHutHutClipPath = "Assets/Animations/Football/Ball hut hut.anim";
    const string CrouchIdleClipPath = "Assets/Animations/Football/Crouch Idle.anim";
    const string CrouchBallHoldClipPath = "Assets/Animations/Football/Crouch Idle With Ball.anim";

    static readonly string[] s_RequiredClips =
    {
        RunningClipPath,
        SideWalkClipPath,
        SideRunClipPath,
        BallCatchClipPath,
        PushClipPath,
        HugPushClipPath,
        FallDownClipPath,
        BallHutHutClipPath,
        CrouchIdleClipPath,
        CrouchBallHoldClipPath
    };

    [InitializeOnLoadMethod]
    static void AutoEnsureController()
    {
        EditorApplication.delayCall += DelayedEnsureControllerExists;
    }

    [MenuItem("Tools/Football/Rebuild Smooth Animator Controller")]
    static void RebuildFromMenu()
    {
        EnsureControllerExists(forceRebuild: true);
    }

    static void DelayedEnsureControllerExists()
    {
        EnsureControllerExists();
    }

    static void EnsureControllerExists(bool forceRebuild = false)
    {
        if (!AllClipsExist())
        {
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            forceRebuild = true;
        }

        if (!forceRebuild && ControllerLooksConfigured(controller))
        {
            return;
        }

        BuildController(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static bool AllClipsExist()
    {
        for (var i = 0; i < s_RequiredClips.Length; i++)
        {
            if (LoadClip(s_RequiredClips[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    static bool ControllerLooksConfigured(AnimatorController controller)
    {
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
        {
            return false;
        }

        return HasParameter(controller, FootballAnimationStateDriver.IdleParam, AnimatorControllerParameterType.Bool) &&
               HasParameter(controller, PreSnapBallParam, AnimatorControllerParameterType.Float) &&
               HasParameter(controller, MoveXParam, AnimatorControllerParameterType.Float) &&
               HasParameter(controller, MoveZParam, AnimatorControllerParameterType.Float) &&
               HasParameter(controller, MoveSpeedParam, AnimatorControllerParameterType.Float) &&
               controller.layers[0].stateMachine.states.Length >= 8;
    }

    static bool HasParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (var parameter in controller.parameters)
        {
            if (parameter.name == parameterName && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }

    static void BuildController(AnimatorController controller)
    {
        controller.parameters = new AnimatorControllerParameter[0];
        AddParameters(controller);

        var layer = controller.layers[0];
        var stateMachine = layer.stateMachine;
        stateMachine.anyStateTransitions = new AnimatorStateTransition[0];
        stateMachine.entryTransitions = new AnimatorTransition[0];

        RemoveAllStates(stateMachine);
        RemoveOldSubAssets(controller);

        var preSnapTree = CreatePreSnapTree(controller);
        var locomotionTree = CreateLocomotionTree(controller);

        var preSnapState = AddState(stateMachine, "PreSnapCrouch", preSnapTree, new Vector3(260f, 60f, 0f));
        var locomotionState = AddState(stateMachine, "Locomotion", locomotionTree, new Vector3(620f, 60f, 0f));
        var ballHutHutState = AddState(stateMachine, "BallHutHut", LoadClip(BallHutHutClipPath), new Vector3(620f, 240f, 0f));
        var pushState = AddState(stateMachine, "Push", LoadClip(PushClipPath), new Vector3(260f, 420f, 0f));
        var hugPushState = AddState(stateMachine, "HugPush", LoadClip(HugPushClipPath), new Vector3(620f, 420f, 0f));
        var ballCatchState = AddState(stateMachine, "BallCatch", LoadClip(BallCatchClipPath), new Vector3(980f, 420f, 0f));
        var fallDownState = AddState(stateMachine, "FallDown", LoadClip(FallDownClipPath), new Vector3(1340f, 420f, 0f));

        stateMachine.defaultState = preSnapState;

        AddAnyStateTransition(stateMachine, preSnapState, FootballAnimationStateDriver.CrouchIdleParam, 0.08f, TransitionInterruptionSource.None);
        AddAnyStateTransition(stateMachine, preSnapState, FootballAnimationStateDriver.CrouchBallHoldParam, 0.08f, TransitionInterruptionSource.None);
        AddAnyStateTransition(stateMachine, ballHutHutState, FootballAnimationStateDriver.BallHutHutParam, 0.12f, TransitionInterruptionSource.DestinationThenSource);
        AddAnyStateTransition(stateMachine, pushState, FootballAnimationStateDriver.PushParam, 0.16f, TransitionInterruptionSource.DestinationThenSource);
        AddAnyStateTransition(stateMachine, hugPushState, FootballAnimationStateDriver.HugPushParam, 0.18f, TransitionInterruptionSource.DestinationThenSource);
        AddAnyStateTransition(stateMachine, ballCatchState, FootballAnimationStateDriver.BallCatchParam, 0.14f, TransitionInterruptionSource.DestinationThenSource);
        AddAnyStateTransition(stateMachine, fallDownState, FootballAnimationStateDriver.FallDownParam, 0.2f, TransitionInterruptionSource.None);

        AddPreSnapExit(preSnapState, locomotionState);

        AddActionReturnTransitions(ballHutHutState, preSnapState, locomotionState, FootballAnimationStateDriver.BallHutHutParam, 0.18f);
        AddActionReturnTransitions(pushState, preSnapState, locomotionState, FootballAnimationStateDriver.PushParam, 0.2f);
        AddActionReturnTransitions(hugPushState, preSnapState, locomotionState, FootballAnimationStateDriver.HugPushParam, 0.22f);
        AddActionReturnTransitions(ballCatchState, preSnapState, locomotionState, FootballAnimationStateDriver.BallCatchParam, 0.18f);
        AddActionReturnTransitions(fallDownState, preSnapState, locomotionState, FootballAnimationStateDriver.FallDownParam, 0.24f);

        layer.stateMachine = stateMachine;
        controller.layers[0] = layer;
        EditorUtility.SetDirty(controller);
    }

    static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter(FootballAnimationStateDriver.IdleParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.RunningParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.BallGrabRunningParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.BallCatchParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.PushParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.HugPushParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.FallDownParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.BallHutHutParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.CrouchIdleParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.CrouchBallHoldParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.SideWalkParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FootballAnimationStateDriver.HasBallParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(PreSnapBallParam, AnimatorControllerParameterType.Float);
        controller.AddParameter(MoveXParam, AnimatorControllerParameterType.Float);
        controller.AddParameter(MoveZParam, AnimatorControllerParameterType.Float);
        controller.AddParameter(MoveSpeedParam, AnimatorControllerParameterType.Float);
    }

    static void RemoveAllStates(AnimatorStateMachine stateMachine)
    {
        foreach (var childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }
    }

    static void RemoveOldSubAssets(AnimatorController controller)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
        foreach (var asset in assets)
        {
            if (asset is BlendTree)
            {
                Object.DestroyImmediate(asset, true);
            }
        }
    }

    static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, Vector3 position)
    {
        var state = stateMachine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    static void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string parameterName, float duration, TransitionInterruptionSource interruptionSource)
    {
        var transition = stateMachine.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = interruptionSource;
        transition.orderedInterruption = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
    }

    static void AddPreSnapExit(AnimatorState preSnapState, AnimatorState targetState)
    {
        var transition = preSnapState.AddTransition(targetState);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.2f;
        transition.interruptionSource = TransitionInterruptionSource.None;
        transition.orderedInterruption = false;
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, FootballAnimationStateDriver.CrouchIdleParam);
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, FootballAnimationStateDriver.CrouchBallHoldParam);
        transition.AddCondition(AnimatorConditionMode.Greater, 0.05f, MoveSpeedParam);
    }

    static void AddActionReturnTransitions(AnimatorState from, AnimatorState preSnapState, AnimatorState locomotionState, string actionParam, float duration)
    {
        var toPreSnap = from.AddTransition(preSnapState);
        toPreSnap.hasExitTime = false;
        toPreSnap.hasFixedDuration = true;
        toPreSnap.duration = duration;
        toPreSnap.interruptionSource = TransitionInterruptionSource.DestinationThenSource;
        toPreSnap.orderedInterruption = false;
        toPreSnap.AddCondition(AnimatorConditionMode.IfNot, 0f, actionParam);
        toPreSnap.AddCondition(AnimatorConditionMode.If, 0f, FootballAnimationStateDriver.CrouchIdleParam);

        var toPreSnapBall = from.AddTransition(preSnapState);
        toPreSnapBall.hasExitTime = false;
        toPreSnapBall.hasFixedDuration = true;
        toPreSnapBall.duration = duration;
        toPreSnapBall.interruptionSource = TransitionInterruptionSource.DestinationThenSource;
        toPreSnapBall.orderedInterruption = false;
        toPreSnapBall.AddCondition(AnimatorConditionMode.IfNot, 0f, actionParam);
        toPreSnapBall.AddCondition(AnimatorConditionMode.If, 0f, FootballAnimationStateDriver.CrouchBallHoldParam);

        var toLocomotion = from.AddTransition(locomotionState);
        toLocomotion.hasExitTime = false;
        toLocomotion.hasFixedDuration = true;
        toLocomotion.duration = duration;
        toLocomotion.interruptionSource = TransitionInterruptionSource.DestinationThenSource;
        toLocomotion.orderedInterruption = false;
        toLocomotion.AddCondition(AnimatorConditionMode.IfNot, 0f, actionParam);
        toLocomotion.AddCondition(AnimatorConditionMode.IfNot, 0f, FootballAnimationStateDriver.CrouchIdleParam);
        toLocomotion.AddCondition(AnimatorConditionMode.IfNot, 0f, FootballAnimationStateDriver.CrouchBallHoldParam);
    }

    static BlendTree CreatePreSnapTree(AnimatorController controller)
    {
        var tree = CreateBlendTreeAsset(controller, "PreSnapCrouchTree", BlendTreeType.Simple1D, PreSnapBallParam);
        Add1DChild(tree, LoadClip(CrouchIdleClipPath), 0f);
        Add1DChild(tree, LoadClip(CrouchBallHoldClipPath), 1f);
        return tree;
    }

    static BlendTree CreateLocomotionTree(AnimatorController controller)
    {
        var tree = CreateDirectionalTree(controller, "Locomotion2D");
        Add2DChild(tree, LoadClip(RunningClipPath), new Vector2(0f, 1f));
        Add2DChild(tree, LoadClip(RunningClipPath), new Vector2(0f, 0f));
        Add2DChild(tree, LoadClip(SideWalkClipPath), new Vector2(-0.65f, 0.12f), mirror: true);
        Add2DChild(tree, LoadClip(SideWalkClipPath), new Vector2(0.65f, 0.12f));
        Add2DChild(tree, LoadClip(SideRunClipPath), new Vector2(-1f, 0.75f), mirror: true);
        Add2DChild(tree, LoadClip(SideRunClipPath), new Vector2(1f, 0.75f));
        return tree;
    }

    static BlendTree CreateDirectionalTree(AnimatorController controller, string name)
    {
        var tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = MoveXParam,
            blendParameterY = MoveZParam,
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.hideFlags = HideFlags.HideInHierarchy;
        return tree;
    }

    static BlendTree CreateBlendTreeAsset(AnimatorController controller, string name, BlendTreeType type, string parameter)
    {
        var tree = new BlendTree
        {
            name = name,
            blendType = type,
            blendParameter = parameter,
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.hideFlags = HideFlags.HideInHierarchy;
        return tree;
    }

    static void Add1DChild(BlendTree tree, Motion motion, float threshold, bool mirror = false)
    {
        if (tree == null || motion == null)
        {
            return;
        }

        tree.AddChild(motion, threshold);
        var children = tree.children;
        var index = children.Length - 1;
        var child = children[index];
        child.mirror = mirror;
        children[index] = child;
        tree.children = children;
    }

    static void Add2DChild(BlendTree tree, Motion motion, Vector2 position, bool mirror = false)
    {
        if (tree == null || motion == null)
        {
            return;
        }

        tree.AddChild(motion, position);
        var children = tree.children;
        var index = children.Length - 1;
        var child = children[index];
        child.mirror = mirror;
        children[index] = child;
        tree.children = children;
    }

    static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }
}
