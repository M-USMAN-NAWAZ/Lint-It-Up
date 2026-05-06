using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class VRFootballScenarioSceneBuilder
{
    const string CanvasName = "Football Scenario Canvas";
    const string ControllerName = "Football Scenario Controller";

    [MenuItem("Tools/Football/Build VR Scenario UI")]
    static void BuildScenarioUi()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        var existingController = Object.FindFirstObjectByType<VRFootballScenarioController>();
        if (existingController != null)
        {
            Selection.activeObject = existingController.gameObject;
            return;
        }

        var formationController = Object.FindFirstObjectByType<FormationRunController>();
        var xrRig = GameObject.Find("XR Origin Hands (XR Rig)");
        var head = FindHeadTransform(xrRig);
        var ball = FindBall();

        var controllerObject = new GameObject(ControllerName);
        Undo.RegisterCreatedObjectUndo(controllerObject, "Create football scenario controller");
        var scenarioController = controllerObject.AddComponent<VRFootballScenarioController>();

        var targetsRoot = new GameObject("Scenario Targets");
        Undo.RegisterCreatedObjectUndo(targetsRoot, "Create football scenario targets");
        targetsRoot.transform.SetParent(controllerObject.transform, false);

        var passOriginTarget = CreateTarget("Pass Origin", targetsRoot.transform, new Vector3(0f, 1.15f, 11.1f));
        var catchBallTarget = CreateTarget("Catch Ball Target", targetsRoot.transform, new Vector3(0.15f, 1.2f, 10.1f));
        var fakerRunTarget = CreateTarget("Faker Run Target", targetsRoot.transform, new Vector3(-1.5f, 1f, 9f));
        var fakeReachTarget = CreateTarget("Fake Reach Target", targetsRoot.transform, new Vector3(-0.8f, 1.1f, 8.4f));
        var safeRunTarget = CreateTarget("Safe Run Target", targetsRoot.transform, new Vector3(1.5f, 1f, 5.5f));
        var goalThrowTarget = CreateTarget("Goal Throw Target", targetsRoot.transform, new Vector3(2.2f, 1.2f, 2.8f));

        var canvas = CreateCanvas(head != null ? head : (xrRig != null ? xrRig.transform : null));
        var ui = canvas.AddComponent<VRFootballScenarioUI>();
        var follower = canvas.AddComponent<VRFootballHudFollower>();
        if (head != null)
        {
            follower.SetTargetHead(head);
        }

        var rootPanel = CreatePanel("Root Panel", canvas.transform, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var countdownPanel = CreatePanel("Countdown Panel", rootPanel.transform, new Color(0f, 0f, 0f, 0.32f), new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.75f), Vector2.zero, Vector2.zero);
        var countdownText = CreateText("Countdown Text", countdownPanel.transform, "3", 140, TextAlignmentOptions.Center);

        var taskPanel = CreatePanel("Task Panel", rootPanel.transform, new Color(0f, 0f, 0f, 0.45f), new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.28f), Vector2.zero, Vector2.zero);
        var taskTitle = CreateText("Task Title", taskPanel.transform, "Catch The Ball", 52, TextAlignmentOptions.Center);
        SetRect(taskTitle.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.98f), Vector2.zero, Vector2.zero);
        var taskDescription = CreateText("Task Description", taskPanel.transform, "Catch the ball from the passer after hut hut.", 28, TextAlignmentOptions.Center);
        SetRect(taskDescription.rectTransform, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.7f), Vector2.zero, Vector2.zero);
        var hintText = CreateText("Hint Text", taskPanel.transform, "Pinch or grab with either hand to catch the ball.", 24, TextAlignmentOptions.Center);
        hintText.color = new Color(0.82f, 0.93f, 1f, 1f);
        SetRect(hintText.rectTransform, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.31f), Vector2.zero, Vector2.zero);
        var timerText = CreateText("Timer Text", taskPanel.transform, "3.0s", 34, TextAlignmentOptions.Center);
        SetRect(timerText.rectTransform, new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.14f), Vector2.zero, Vector2.zero);

        var failPanel = CreatePanel("Fail Panel", rootPanel.transform, new Color(0.24f, 0f, 0f, 0.72f), new Vector2(0.27f, 0.2f), new Vector2(0.73f, 0.62f), Vector2.zero, Vector2.zero);
        var failTitle = CreateText("Fail Title", failPanel.transform, "You Failed!", 72, TextAlignmentOptions.Center);
        SetRect(failTitle.rectTransform, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        var failDescription = CreateText("Fail Description", failPanel.transform, "Scenario restarting...", 28, TextAlignmentOptions.Center);
        SetRect(failDescription.rectTransform, new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.45f), Vector2.zero, Vector2.zero);

        var uiSerialized = new SerializedObject(ui);
        uiSerialized.FindProperty("rootPanel").objectReferenceValue = rootPanel;
        uiSerialized.FindProperty("countdownPanel").objectReferenceValue = countdownPanel;
        uiSerialized.FindProperty("taskPanel").objectReferenceValue = taskPanel;
        uiSerialized.FindProperty("failPanel").objectReferenceValue = failPanel;
        uiSerialized.FindProperty("countdownText").objectReferenceValue = countdownText;
        uiSerialized.FindProperty("taskTitleText").objectReferenceValue = taskTitle;
        uiSerialized.FindProperty("taskDescriptionText").objectReferenceValue = taskDescription;
        uiSerialized.FindProperty("taskHintText").objectReferenceValue = hintText;
        uiSerialized.FindProperty("taskTimerText").objectReferenceValue = timerText;
        uiSerialized.FindProperty("failTitleText").objectReferenceValue = failTitle;
        uiSerialized.FindProperty("failDescriptionText").objectReferenceValue = failDescription;
        uiSerialized.ApplyModifiedPropertiesWithoutUndo();

        ConfigureScenarioController(scenarioController, formationController, ui, ball, xrRig, head, passOriginTarget, catchBallTarget, fakerRunTarget, fakeReachTarget, safeRunTarget, goalThrowTarget);
        DisableFormationAutoplay(formationController);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = controllerObject;
    }

    static void ConfigureScenarioController(
        VRFootballScenarioController scenarioController,
        FormationRunController formationController,
        VRFootballScenarioUI ui,
        XRGrabInteractable ball,
        GameObject xrRig,
        Transform head,
        Transform passOriginTarget,
        Transform catchBallTarget,
        Transform fakerRunTarget,
        Transform fakeReachTarget,
        Transform safeRunTarget,
        Transform goalThrowTarget)
    {
        var serialized = new SerializedObject(scenarioController);
        serialized.FindProperty("formationController").objectReferenceValue = formationController;
        serialized.FindProperty("scenarioUI").objectReferenceValue = ui;
        serialized.FindProperty("football").objectReferenceValue = ball;
        serialized.FindProperty("userRoot").objectReferenceValue = xrRig != null ? xrRig.transform : null;
        serialized.FindProperty("leftHand").objectReferenceValue = FindChildByNameContains(xrRig != null ? xrRig.transform : null, "Left");
        serialized.FindProperty("rightHand").objectReferenceValue = FindChildByNameContains(xrRig != null ? xrRig.transform : null, "Right");
        serialized.FindProperty("ballCatchTarget").objectReferenceValue = catchBallTarget;
        serialized.FindProperty("passOrigin").objectReferenceValue = passOriginTarget;

        var tasksProperty = serialized.FindProperty("tasks");
        tasksProperty.arraySize = 5;

        SetTask(tasksProperty.GetArrayElementAtIndex(0), "Catch The Ball", "Catch the ball from the passer after hut hut.", "Pinch or grab with either hand to catch the ball.", VRFootballScenarioController.ScenarioTaskType.CatchBall, null, 1f, 4f, 0.22f, false);
        SetTask(tasksProperty.GetArrayElementAtIndex(1), "Run To Faker", "Run toward the faker next.", "Move your VR body to the faker target while holding the ball.", VRFootballScenarioController.ScenarioTaskType.ReachZone, fakerRunTarget, 1.1f, 3f, 0.25f, true);
        SetTask(tasksProperty.GetArrayElementAtIndex(2), "Fake To Faker", "Stretch your arm toward the faker.", "Extend the hand holding the ball toward the fake target.", VRFootballScenarioController.ScenarioTaskType.ReachWithHand, fakeReachTarget, 0.55f, 2.5f, 0.2f, true);
        SetTask(tasksProperty.GetArrayElementAtIndex(3), "Run To Safe Spot", "Run to a safer place before throwing.", "Keep the ball in hand and move to the safe target.", VRFootballScenarioController.ScenarioTaskType.ReachZone, safeRunTarget, 1.1f, 3f, 0.25f, true);
        SetTask(tasksProperty.GetArrayElementAtIndex(4), "Throw To Goal", "Throw the ball to the goal player.", "Release the ball toward the goal target.", VRFootballScenarioController.ScenarioTaskType.ThrowBallToTarget, goalThrowTarget, 1f, 4f, 0.2f, true);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetTask(SerializedProperty taskProperty, string title, string instruction, string controlHint, VRFootballScenarioController.ScenarioTaskType taskType, Transform target, float radius, float duration, float slowMotionScale, bool requireBallInHand)
    {
        taskProperty.FindPropertyRelative("title").stringValue = title;
        taskProperty.FindPropertyRelative("instruction").stringValue = instruction;
        taskProperty.FindPropertyRelative("controlHint").stringValue = controlHint;
        taskProperty.FindPropertyRelative("taskType").enumValueIndex = (int)taskType;
        taskProperty.FindPropertyRelative("target").objectReferenceValue = target;
        taskProperty.FindPropertyRelative("completionRadius").floatValue = radius;
        taskProperty.FindPropertyRelative("taskDuration").floatValue = duration;
        taskProperty.FindPropertyRelative("slowMotionScale").floatValue = slowMotionScale;
        taskProperty.FindPropertyRelative("requireBallInHand").boolValue = requireBallInHand;
    }

    static void DisableFormationAutoplay(FormationRunController formationController)
    {
        if (formationController == null)
        {
            return;
        }

        var serialized = new SerializedObject(formationController);
        serialized.FindProperty("playPlayerTeamOnStart").boolValue = false;
        serialized.FindProperty("playOpponentTeamOnStart").boolValue = false;
        serialized.FindProperty("useStartupCountdown").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static XRGrabInteractable FindBall()
    {
        var grabInteractables = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var interactable in grabInteractables)
        {
            if (interactable != null && interactable.name.Contains("Sphere"))
            {
                return interactable;
            }
        }

        return Object.FindFirstObjectByType<XRGrabInteractable>();
    }

    static Transform FindHeadTransform(GameObject xrRig)
    {
        if (xrRig == null)
        {
            return Camera.main != null ? Camera.main.transform : null;
        }

        var camera = xrRig.GetComponentInChildren<Camera>();
        return camera != null ? camera.transform : xrRig.transform;
    }

    static Transform FindChildByNameContains(Transform root, string nameContains)
    {
        if (root == null)
        {
            return null;
        }

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains(nameContains))
            {
                return child;
            }
        }

        return null;
    }

    static GameObject CreateCanvas(Transform parent)
    {
        var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create football scenario canvas");
        if (parent != null)
        {
            canvasObject.transform.SetParent(parent, false);
        }

        canvasObject.transform.localPosition = new Vector3(0f, -0.12f, 1.25f);
        canvasObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.0015f;

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.dynamicPixelsPerUnit = 10f;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1400f, 900f);

        return canvasObject;
    }

    static GameObject CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create panel");
        panel.transform.SetParent(parent, false);

        var image = panel.GetComponent<Image>();
        image.color = color;

        var rect = panel.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        return panel;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textObject, "Create text");
        textObject.transform.SetParent(parent, false);

        var textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.enableWordWrapping = true;

        var rect = textObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        return textComponent;
    }

    static Transform CreateTarget(string name, Transform parent, Vector3 localPosition)
    {
        var target = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(target, "Create scenario target");
        target.transform.SetParent(parent, false);
        target.transform.localPosition = localPosition;
        return target.transform;
    }

    static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }
}
