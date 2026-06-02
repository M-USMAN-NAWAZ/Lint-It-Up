using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ScenarioHutHutTimer : MonoBehaviour
{
    static ScenarioHutHutTimer instance;

    float timeoutSeconds = 40f;
    string game1SceneName = "Game 1";
    string gameSceneName = "Game";
    float timerStartedAt;
    bool timerRunning;
    Canvas timerCanvas;
    RectTransform timerCanvasRect;
    TMP_Text timerText;
    Camera cachedCamera;
    SceneOrbitCamera cachedOrbitCamera;
    Transform cachedGameFacingTarget;
    string lastSceneName;
    const float OrbitTargetVerticalOffset = 3.5f;
    const float GameFacingTargetHeightOffset = 1.6f;
    const float DisplayRotationLerpSpeed = 18f;
    const float DefaultDisplayScale = 0.0015f;
    const float Game1DisplayScale = 0.0085f;
    const string GameFacingTargetName = "XR Origin Hands (XR Rig)";
    const string GameFacingTargetFallbackName = "XR Origin (XR Rig)";

    public static void StartTimer(float seconds, string game1Scene, string gameScene)
    {
        var timer = GetOrCreate();
        timer.timeoutSeconds = Mathf.Max(0.1f, seconds);
        timer.game1SceneName = string.IsNullOrWhiteSpace(game1Scene) ? "Game 1" : game1Scene;
        timer.gameSceneName = string.IsNullOrWhiteSpace(gameScene) ? "Game" : gameScene;
        timer.timerStartedAt = Time.realtimeSinceStartup;
        timer.timerRunning = true;
        timer.EnsureDisplay();
        timer.SetDisplayVisible(true);
        timer.UpdateDisplay();
    }

    public static void StopForHutHut()
    {
        if (instance == null)
        {
            return;
        }

        instance.StopTimer();
    }

    static ScenarioHutHutTimer GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        var timerObject = new GameObject(nameof(ScenarioHutHutTimer));
        instance = timerObject.AddComponent<ScenarioHutHutTimer>();
        DontDestroyOnLoad(timerObject);
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!timerRunning)
        {
            return;
        }

        if (Time.realtimeSinceStartup - timerStartedAt < timeoutSeconds)
        {
            UpdateDisplay();
            return;
        }

        HandleTimeout();
    }

    void LateUpdate()
    {
        if (!timerRunning || timerCanvasRect == null)
        {
            return;
        }

        AnchorToOrbitTargetAndFaceCamera();
    }

    void HandleTimeout()
    {
        var activeSceneName = SceneManager.GetActiveScene().name;
        StopTimer();

        if (activeSceneName == game1SceneName)
        {
            SceneManager.LoadScene(game1SceneName);
            return;
        }

        if (activeSceneName == gameSceneName)
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    void StopTimer()
    {
        timerRunning = false;
        SetDisplayVisible(false);
    }

    void EnsureDisplay()
    {
        if (timerCanvas != null && timerText != null)
        {
            return;
        }

        var canvasObject = new GameObject("Hut Hut Timeout Display", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        timerCanvas = canvasObject.GetComponent<Canvas>();
        timerCanvas.renderMode = RenderMode.WorldSpace;
        timerCanvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;

        timerCanvasRect = canvasObject.GetComponent<RectTransform>();
        timerCanvasRect.sizeDelta = new Vector2(360f, 90f);
        ApplySceneDisplayScale();

        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(timerCanvasRect, false);

        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);

        var textObject = new GameObject("Timer Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(timerCanvasRect, false);

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        timerText = textObject.GetComponent<TextMeshProUGUI>();
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.fontSize = 34f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = Color.white;
        timerText.raycastTarget = false;
    }

    void UpdateDisplay()
    {
        EnsureDisplay();

        if (timerText == null)
        {
            return;
        }

        var remaining = Mathf.Max(0f, timeoutSeconds - (Time.realtimeSinceStartup - timerStartedAt));
        timerText.text = "Hut Hut Timer: " + Mathf.CeilToInt(remaining).ToString("00") + "s";
    }

    void AnchorToOrbitTargetAndFaceCamera()
    {
        var activeSceneName = SceneManager.GetActiveScene().name;
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        }

        if (cachedOrbitCamera == null || !cachedOrbitCamera.isActiveAndEnabled)
        {
            cachedOrbitCamera = FindObjectOfType<SceneOrbitCamera>();
        }

        if (cachedCamera == null)
        {
            return;
        }

        ApplySceneDisplayScale();
        if (cachedOrbitCamera != null)
        {
            timerCanvasRect.position = cachedOrbitCamera.GetOrbitTargetTopPosition(OrbitTargetVerticalOffset);
        }

        var facingTargetPosition = activeSceneName == gameSceneName
            ? GetGameFacingTargetPosition()
            : cachedCamera.transform.position;
        var faceDirection = timerCanvasRect.position - facingTargetPosition;
        if (faceDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
        if (lastSceneName != activeSceneName)
        {
            timerCanvasRect.rotation = targetRotation;
            lastSceneName = activeSceneName;
            return;
        }

        timerCanvasRect.rotation = Quaternion.Slerp(
            timerCanvasRect.rotation,
            targetRotation,
            1f - Mathf.Exp(-DisplayRotationLerpSpeed * Time.unscaledDeltaTime));
    }

    Vector3 GetGameFacingTargetPosition()
    {
        if (cachedGameFacingTarget == null)
        {
            cachedGameFacingTarget = FindTransformByName(GameFacingTargetName);
            if (cachedGameFacingTarget == null)
            {
                cachedGameFacingTarget = FindTransformByName(GameFacingTargetFallbackName);
            }
        }

        if (cachedGameFacingTarget != null)
        {
            return cachedGameFacingTarget.position + Vector3.up * GameFacingTargetHeightOffset;
        }

        return cachedCamera != null ? cachedCamera.transform.position : timerCanvasRect.position + Vector3.forward;
    }

    static Transform FindTransformByName(string objectName)
    {
        var foundObject = GameObject.Find(objectName);
        return foundObject != null ? foundObject.transform : null;
    }

    void SetDisplayVisible(bool visible)
    {
        if (timerCanvas != null)
        {
            timerCanvas.gameObject.SetActive(visible);
        }
    }

    void ApplySceneDisplayScale()
    {
        if (timerCanvasRect == null)
        {
            return;
        }

        var activeSceneName = SceneManager.GetActiveScene().name;
        var scale = activeSceneName == game1SceneName ? Game1DisplayScale : DefaultDisplayScale;
        timerCanvasRect.localScale = Vector3.one * scale;
    }
}
