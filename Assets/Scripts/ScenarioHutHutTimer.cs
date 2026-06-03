using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[ExecuteAlways]
public class ScenarioHutHutTimer : MonoBehaviour
{
    static ScenarioHutHutTimer instance;

    float timeoutSeconds = 40f;
    [SerializeField] float gameSceneRetryTimeoutSeconds = 20f;
    string game1SceneName = "Game 1";
    string gameSceneName = "Game";
    float timerStartedAt;
    bool timerRunning;
    bool gameTimeoutSequenceRunning;
    Coroutine gameTimeoutCoroutine;
    Canvas timerCanvas;
    RectTransform timerCanvasRect;
    TMP_Text timerText;
    GameObject gameTimeoutPanel;
    Image gameTimeoutImage;
    AudioSource gameTimeoutAudioSource;
    bool gameTimeoutParentForcedActive;

    [Header("Game Timeout UI")]
    [SerializeField] AudioClip gameTimeoutAudioClip;
    const string FootballScenarioCanvasName = "Football Scenario Canvas";
    const string RootPanelName = "Root Panel";
    const string GameTimeoutUIName = "Game Timeout UI";
    const string TimeoutImageName = "Timeout Image";

    public static void StartTimer(float seconds, string game1Scene, string gameScene)
    {
        var timer = GetOrCreate();
        timer.timeoutSeconds = Mathf.Max(0.1f, seconds);
        timer.game1SceneName = string.IsNullOrWhiteSpace(game1Scene) ? "Game 1" : game1Scene;
        timer.gameSceneName = string.IsNullOrWhiteSpace(gameScene) ? "Game" : gameScene;
        timer.timerStartedAt = Time.realtimeSinceStartup;
        timer.timerRunning = true;
        timer.EnsureDisplay();
        timer.EnsureGameTimeoutUI();
        timer.CancelGameTimeoutSequence();
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
        instance.CancelGameTimeoutSequence();
    }

    public static void StartGameSceneRetryTimer()
    {
        var timer = GetOrCreate();
        timer.EnsureDisplay();
        timer.EnsureGameTimeoutUI();
        timer.CancelGameTimeoutSequence();
        timer.RestartTimer(timer.gameSceneRetryTimeoutSeconds);
    }

    static ScenarioHutHutTimer GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<ScenarioHutHutTimer>();
        if (instance != null)
        {
            return instance;
        }

        var timerObject = new GameObject(nameof(ScenarioHutHutTimer));
        instance = timerObject.AddComponent<ScenarioHutHutTimer>();
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(timerObject);
        }

        return instance;
    }

    void Awake()
    {
        if (Application.isPlaying && instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureDisplay();
        EnsureGameTimeoutUI();

        if (Application.isPlaying)
        {
            SetGameTimeoutUIVisible(false);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            SetDisplayVisible(false);
        }
    }

    void OnValidate()
    {
        EnsureDisplay();
        EnsureGameTimeoutUI();
        if (!Application.isPlaying)
        {
            SetDisplayVisible(false);
            SetGameTimeoutUIVisible(false);
        }
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

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

    void HandleTimeout()
    {
        var activeSceneName = SceneManager.GetActiveScene().name;

        if (activeSceneName == game1SceneName)
        {
            StopTimer();
            SceneManager.LoadScene(game1SceneName);
            return;
        }

        if (activeSceneName == gameSceneName)
        {
            BeginGameTimeoutSequence();
            return;
        }

        StopTimer();
    }

    void StopTimer()
    {
        timerRunning = false;
        SetDisplayVisible(false);
    }

    void BeginGameTimeoutSequence()
    {
        if (gameTimeoutSequenceRunning)
        {
            return;
        }

        StopTimer();
        gameTimeoutCoroutine = StartCoroutine(GameTimeoutSequence());
    }

    IEnumerator GameTimeoutSequence()
    {
        gameTimeoutSequenceRunning = true;
        EnsureGameTimeoutUI();
        SetGameTimeoutUIVisible(true);

        if (gameTimeoutAudioSource != null)
        {
            gameTimeoutAudioSource.Stop();
            var clipToPlay = gameTimeoutAudioClip != null ? gameTimeoutAudioClip : gameTimeoutAudioSource.clip;
            gameTimeoutAudioSource.clip = clipToPlay;

            if (clipToPlay != null)
            {
                gameTimeoutAudioSource.Play();
                yield return new WaitWhile(() => gameTimeoutAudioSource != null && gameTimeoutAudioSource.isPlaying);
            }
            else
            {
                yield return null;
            }
        }

        SetGameTimeoutUIVisible(false);
        gameTimeoutSequenceRunning = false;
        gameTimeoutCoroutine = null;
        SceneManager.LoadScene(gameSceneName);
        RestartTimer(gameSceneRetryTimeoutSeconds);
    }

    void CancelGameTimeoutSequence()
    {
        if (gameTimeoutCoroutine != null)
        {
            StopCoroutine(gameTimeoutCoroutine);
            gameTimeoutCoroutine = null;
        }

        gameTimeoutSequenceRunning = false;

        if (gameTimeoutAudioSource != null)
        {
            gameTimeoutAudioSource.Stop();
        }

        SetGameTimeoutUIVisible(false);
    }

    void RestartTimer(float seconds)
    {
        timeoutSeconds = Mathf.Max(0.1f, seconds);
        timerStartedAt = Time.realtimeSinceStartup;
        timerRunning = true;
        EnsureDisplay();
        SetDisplayVisible(true);
        UpdateDisplay();
    }

    void EnsureDisplay()
    {
        if (timerCanvas != null && timerText != null)
        {
            return;
        }

        var existingCanvas = transform.Find("Hut Hut Timeout Display");
        var createdCanvas = existingCanvas == null;
        var canvasObject = existingCanvas != null
            ? existingCanvas.gameObject
            : new GameObject("Hut Hut Timeout Display", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        if (existingCanvas == null)
        {
            canvasObject.transform.SetParent(transform, false);
        }

        timerCanvas = canvasObject.GetComponent<Canvas>();
        if (timerCanvas == null)
        {
            timerCanvas = canvasObject.AddComponent<Canvas>();
        }

        timerCanvas.renderMode = RenderMode.WorldSpace;
        timerCanvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.dynamicPixelsPerUnit = 12f;

        timerCanvasRect = canvasObject.GetComponent<RectTransform>();
        if (createdCanvas)
        {
            timerCanvasRect.sizeDelta = new Vector2(360f, 90f);
        }

        var existingBackground = canvasObject.transform.Find("Background");
        var createdBackground = existingBackground == null;
        var backgroundObject = existingBackground != null
            ? existingBackground.gameObject
            : new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (existingBackground == null)
        {
            backgroundObject.transform.SetParent(timerCanvasRect, false);
        }

        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        if (createdBackground)
        {
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
        }

        var background = backgroundObject.GetComponent<Image>();
        if (background == null)
        {
            background = backgroundObject.AddComponent<Image>();
            createdBackground = true;
        }

        if (createdBackground)
        {
            background.color = new Color(0f, 0f, 0f, 0.65f);
        }

        var existingText = canvasObject.transform.Find("Timer Text");
        var createdText = existingText == null;
        var textObject = existingText != null
            ? existingText.gameObject
            : new GameObject("Timer Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

        if (existingText == null)
        {
            textObject.transform.SetParent(timerCanvasRect, false);
        }

        var textRect = textObject.GetComponent<RectTransform>();
        if (createdText)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
        }

        timerText = textObject.GetComponent<TextMeshProUGUI>();
        if (timerText == null)
        {
            timerText = textObject.AddComponent<TextMeshProUGUI>();
            createdText = true;
        }

        if (createdText)
        {
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.fontSize = 34f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.color = Color.white;
        }

        timerText.raycastTarget = false;
    }

    void EnsureGameTimeoutUI()
    {
        var parent = FindGameTimeoutUIParent();
        if (gameTimeoutPanel != null && gameTimeoutImage != null && gameTimeoutAudioSource != null)
        {
            if (gameTimeoutPanel.transform.parent != parent)
            {
                gameTimeoutPanel.transform.SetParent(parent, false);
            }

            return;
        }

        var existingPanel = parent.Find(GameTimeoutUIName);
        if (existingPanel == null && gameTimeoutPanel != null)
        {
            existingPanel = gameTimeoutPanel.transform;
        }

        if (existingPanel == null)
        {
            existingPanel = FindSceneTransformByName(GameTimeoutUIName);
        }

        var createdPanel = existingPanel == null;
        var panelObject = existingPanel != null
            ? existingPanel.gameObject
            : new GameObject(GameTimeoutUIName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AudioSource));

        if (panelObject.transform.parent != parent)
        {
            panelObject.transform.SetParent(parent, false);
        }

        gameTimeoutPanel = panelObject;

        var panelRect = panelObject.GetComponent<RectTransform>();
        if (createdPanel)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.localScale = Vector3.one;
        }

        gameTimeoutImage = panelObject.GetComponent<Image>();
        if (gameTimeoutImage == null)
        {
            gameTimeoutImage = panelObject.AddComponent<Image>();
        }

        var existingImage = panelObject.transform.Find(TimeoutImageName);
        var createdImage = existingImage == null;
        var imageObject = existingImage != null
            ? existingImage.gameObject
            : new GameObject(TimeoutImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (createdImage)
        {
            imageObject.transform.SetParent(panelRect, false);
        }

        var imageRect = imageObject.GetComponent<RectTransform>();
        if (createdImage)
        {
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
        }

        var childImage = imageObject.GetComponent<Image>();
        if (childImage == null)
        {
            childImage = imageObject.AddComponent<Image>();
        }

        if (createdPanel)
        {
            gameTimeoutImage.color = Color.white;
        }

        if (createdImage)
        {
            childImage.color = Color.white;
        }

        gameTimeoutAudioSource = panelObject.GetComponent<AudioSource>();
        if (gameTimeoutAudioSource == null)
        {
            gameTimeoutAudioSource = panelObject.AddComponent<AudioSource>();
        }

        gameTimeoutAudioSource.playOnAwake = false;
    }

    Transform FindGameTimeoutUIParent()
    {
        var footballCanvas = FindSceneTransformByName(FootballScenarioCanvasName);
        if (footballCanvas != null)
        {
            var rootPanel = footballCanvas.Find(RootPanelName);
            if (rootPanel != null)
            {
                return rootPanel;
            }
        }

        var fallbackRootPanel = FindSceneTransformByName(RootPanelName);
        return fallbackRootPanel != null ? fallbackRootPanel : transform;
    }

    static Transform FindSceneTransformByName(string objectName)
    {
        var transforms = FindObjectsOfType<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            var candidate = transforms[i];
            if (candidate.name == objectName && candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    void UpdateDisplay()
    {
        EnsureDisplay();

        if (timerText == null)
        {
            return;
        }

        var remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, timeoutSeconds - (Time.realtimeSinceStartup - timerStartedAt)));
        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    void SetDisplayVisible(bool visible)
    {
        if (timerCanvas != null)
        {
            timerCanvas.gameObject.SetActive(visible);
        }
    }

    void SetGameTimeoutUIVisible(bool visible)
    {
        if (gameTimeoutPanel != null)
        {
            var parentObject = gameTimeoutPanel.transform.parent != null
                ? gameTimeoutPanel.transform.parent.gameObject
                : null;

            if (visible && parentObject != null)
            {
                gameTimeoutParentForcedActive = !parentObject.activeSelf;
                parentObject.SetActive(true);
            }

            gameTimeoutPanel.SetActive(visible);

            if (!visible && parentObject != null && gameTimeoutParentForcedActive)
            {
                parentObject.SetActive(false);
                gameTimeoutParentForcedActive = false;
            }
        }
    }

}
