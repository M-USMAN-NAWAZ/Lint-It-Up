using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System;

[ExecuteAlways]
public class Game1QuestionnaireUI : MonoBehaviour
{
    public const string FirstQuestionAnswerKey = "Game1.Question1.AnswerText";
    public const string SecondQuestionAnswerKey = "Game1.Question2.AnswerText";

    [System.Serializable]
    struct QuestionStep
    {
        [TextArea(2, 4)] public string prompt;
        [TextArea(1, 3)] public string context;
        public string optionA;
        public string optionB;
        public string optionC;
        public AudioClip voiceClip;
    }

    [Serializable]
    class GroundPlayerHighlighter
    {
        public GameObject root;
        public Transform targetPlayer;
        public string playerNumber = "88";
        public string playerPosition = "BARNER";
        public Vector3 groundOffset = new Vector3(0f, 0.04f, -1.25f);
        public float scale = 1f;

        [NonSerialized] public bool visualBuilt;
        [NonSerialized] public TextMeshPro numberText;
        [NonSerialized] public TextMeshPro positionText;
    }

    [Header("Panels")]
    [SerializeField] GameObject rootPanel;
    [SerializeField] GameObject countdownPanel;
    [SerializeField] GameObject taskPanel;
    [SerializeField] GameObject failPanel;

    [Header("Text")]
    [SerializeField] TMP_Text questionText;
    [SerializeField] TMP_Text contextText;
    [SerializeField] TMP_Text timerText;

    [Header("Answer Buttons")]
    [SerializeField] Button optionAButton;
    [SerializeField] Button optionBButton;
    [SerializeField] Button optionCButton;
    [SerializeField] TMP_Text optionALabel;
    [SerializeField] TMP_Text optionBLabel;
    [SerializeField] TMP_Text optionCLabel;

    [Header("Audio")]
    [SerializeField] AudioSource questionAudioSource;
    [SerializeField] AudioClip postQuestionnaireVoiceClip;

    [Header("Scene")]
    [SerializeField] string nextSceneName = "Game";
    [SerializeField] float scenarioTimeoutSeconds = 40f;
    [SerializeField] string game1SceneName = "Game 1";
    [SerializeField] float firstQuestionDelay = 8f;
    [SerializeField] float secondQuestionDelay = 5f;
    [SerializeField] SceneOrbitCamera orbitCamera;

    [Header("Play Clock Display")]
    [SerializeField] bool showPlayClock = true;
    [SerializeField] string playClockLabel = "Play Clock";
    [SerializeField] Vector3 playClockOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] int playClockSortingOrder = 120;
    [SerializeField] Camera playClockCamera;
    [SerializeField] Color playClockBackgroundColor = new Color(0f, 0f, 0f, 0.68f);
    [SerializeField] Color playClockTextColor = Color.white;
    [SerializeField] RectTransform playClockRoot;
    [SerializeField] TMP_Text playClockText;

    [Header("Ground Highlighters")]
    [SerializeField] GroundPlayerHighlighter[] playerHighlighters = new GroundPlayerHighlighter[2];
    [SerializeField] Color highlighterBlue = new Color(0.02f, 0.14f, 0.65f, 0.88f);
    [SerializeField] Color highlighterGreen = new Color(0.12f, 0.82f, 0.35f, 0.95f);
    [SerializeField] Color highlighterTextColor = Color.white;

    [SerializeField] QuestionStep[] questions =
    {
        new QuestionStep
        {
            prompt = "What do you see?",
            context = "",
            optionA = "Option A: SS in the box - Showing Cover 1",
            optionB = "Option B: 5 Down & Possible Blitzer",
            optionC = "Option C: Ben to provide"
        },
        new QuestionStep
        {
            prompt = "What might they be giving us?",
            context = "As the camera finishes rotating, a voice says:",
            optionA = "Option A: Run",
            optionB = "Option B: TE Release",
            optionC = "Option C: Ben to provide"
        }
    };

    int currentQuestionIndex;
    bool isTransitioning;
    float playClockStartedAt;

    void Awake()
    {
        EnsureAllHighlighterVisuals();
        if (!Application.isPlaying)
        {
            return;
        }

        ScenarioHutHutTimer.StartTimer(scenarioTimeoutSeconds, game1SceneName, nextSceneName);
        ClearSavedAnswers();
        EnsureAudioSource();
        ResolveOrbitCamera();
        StartPlayClock();
        BindButtons();
        HideQuestionnaire();
        SetPlayerHighlightersVisible(false);
        StartCoroutine(BeginQuestionnaireSequence());
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdatePlayerHighlighters();
        UpdatePlayClock();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UnbindButtons();
    }

    void OnValidate()
    {
        EnsureAllHighlighterVisuals();
        EnsurePlayClockDisplay();
        if (!Application.isPlaying)
        {
            SetPlayClockSampleText();
            SetActive(playClockRoot != null ? playClockRoot.gameObject : null, showPlayClock);
        }
    }

    void EnsureAudioSource()
    {
        if (questionAudioSource == null)
        {
            questionAudioSource = GetComponent<AudioSource>();
        }

        if (questionAudioSource == null)
        {
            questionAudioSource = gameObject.AddComponent<AudioSource>();
        }

        questionAudioSource.playOnAwake = false;
        questionAudioSource.loop = false;
        questionAudioSource.spatialBlend = 0f;
    }

    void ResolveOrbitCamera()
    {
        if (orbitCamera == null)
        {
            orbitCamera = FindObjectOfType<SceneOrbitCamera>();
        }
    }

    void StartPlayClock()
    {
        playClockStartedAt = Time.realtimeSinceStartup;
        EnsurePlayClockDisplay();
        SetActive(playClockRoot != null ? playClockRoot.gameObject : null, showPlayClock);
        UpdatePlayClock();
    }

    void EnsurePlayClockDisplay()
    {
        if (playClockRoot == null)
        {
            var existing = transform.Find("Game 1 Play Clock");
            if (existing != null)
            {
                playClockRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                var clockObject = new GameObject("Game 1 Play Clock", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                clockObject.transform.SetParent(transform, false);
                playClockRoot = clockObject.GetComponent<RectTransform>();
            }

            if (playClockRoot == null)
            {
                return;
            }
        }

        var canvas = playClockRoot.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = playClockRoot.gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = playClockSortingOrder;

        var scaler = playClockRoot.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = playClockRoot.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.dynamicPixelsPerUnit = 12f;

        var background = EnsurePlayClockChild<Image>("Background");
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.color = playClockBackgroundColor;
        background.raycastTarget = false;

        playClockText = EnsurePlayClockChild<TextMeshProUGUI>("Text");
        var textRect = playClockText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-16f, -10f);
        playClockText.color = playClockTextColor;
        playClockText.raycastTarget = false;
    }

    T EnsurePlayClockChild<T>(string childName) where T : Component
    {
        var child = playClockRoot.transform.Find(childName);
        var childObject = child != null
            ? child.gameObject
            : new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));

        if (child == null)
        {
            childObject.transform.SetParent(playClockRoot, false);
        }

        var component = childObject.GetComponent<T>();
        if (component == null)
        {
            component = childObject.AddComponent<T>();
        }

        return component;
    }

    void UpdatePlayClock()
    {
        if (!showPlayClock)
        {
            SetActive(playClockRoot != null ? playClockRoot.gameObject : null, false);
            return;
        }

        EnsurePlayClockDisplay();
        SetActive(playClockRoot != null ? playClockRoot.gameObject : null, true);
        PositionPlayClock();

        if (playClockText == null)
        {
            return;
        }

        var elapsed = Time.realtimeSinceStartup - playClockStartedAt;
        var remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, scenarioTimeoutSeconds - elapsed));
        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        playClockText.text = playClockLabel + "\n" + minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    void SetPlayClockSampleText()
    {
        if (playClockText == null)
        {
            return;
        }

        var previewSeconds = Mathf.CeilToInt(Mathf.Max(0f, scenarioTimeoutSeconds));
        var minutes = previewSeconds / 60;
        var seconds = previewSeconds % 60;
        playClockText.text = playClockLabel + "\n" + minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    void PositionPlayClock()
    {
        if (playClockRoot == null)
        {
            return;
        }

        var targetPosition = transform.position + playClockOffset;
        if (orbitCamera != null)
        {
            targetPosition = orbitCamera.GetOrbitTargetTopPosition(playClockOffset.y);
            targetPosition += new Vector3(playClockOffset.x, 0f, playClockOffset.z);
        }

        playClockRoot.position = targetPosition;

        var cameraTransform = ResolvePlayClockCameraTransform();
        if (cameraTransform == null)
        {
            return;
        }

        var toClock = playClockRoot.position - cameraTransform.position;
        if (toClock.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        playClockRoot.rotation = Quaternion.LookRotation(toClock.normalized, Vector3.up);
    }

    Transform ResolvePlayClockCameraTransform()
    {
        if (playClockCamera != null)
        {
            return playClockCamera.transform;
        }

        if (orbitCamera != null)
        {
            var orbitCameraComponent = orbitCamera.GetComponent<Camera>();
            if (orbitCameraComponent != null)
            {
                playClockCamera = orbitCameraComponent;
                return playClockCamera.transform;
            }

            return orbitCamera.transform;
        }

        if (Camera.main != null)
        {
            playClockCamera = Camera.main;
            return playClockCamera.transform;
        }

        return null;
    }

    void BindButtons()
    {
        if (optionAButton != null)
        {
            optionAButton.onClick.AddListener(OnOptionASelected);
        }

        if (optionBButton != null)
        {
            optionBButton.onClick.AddListener(OnOptionBSelected);
        }

        if (optionCButton != null)
        {
            optionCButton.onClick.AddListener(OnOptionCSelected);
        }
    }

    void UnbindButtons()
    {
        if (optionAButton != null)
        {
            optionAButton.onClick.RemoveListener(OnOptionASelected);
        }

        if (optionBButton != null)
        {
            optionBButton.onClick.RemoveListener(OnOptionBSelected);
        }

        if (optionCButton != null)
        {
            optionCButton.onClick.RemoveListener(OnOptionCSelected);
        }
    }

    IEnumerator BeginQuestionnaireSequence()
    {
        yield return new WaitForSeconds(firstQuestionDelay);
        SetOrbitPaused(true);
        ShowQuestionnaireShell();
        ShowQuestion(0);
    }

    void ShowQuestionnaireShell()
    {
        SetActive(rootPanel, true);
        SetActive(taskPanel, true);
        SetActive(countdownPanel, false);
        SetActive(failPanel, false);

        if (timerText != null)
        {
            timerText.text = string.Empty;
            timerText.gameObject.SetActive(false);
        }
    }

    void HideQuestionnaire()
    {
        SetActive(taskPanel, false);
        SetActive(countdownPanel, false);
        SetActive(failPanel, false);
        SetActive(rootPanel, false);
    }

    void ShowQuestion(int questionIndex)
    {
        if (questions == null || questions.Length == 0)
        {
            return;
        }

        currentQuestionIndex = Mathf.Clamp(questionIndex, 0, questions.Length - 1);
        var question = questions[currentQuestionIndex];

        if (questionText != null)
        {
            questionText.text = question.prompt;
        }

        if (contextText != null)
        {
            var hasContext = !string.IsNullOrWhiteSpace(question.context);
            contextText.text = hasContext ? question.context : string.Empty;
            contextText.gameObject.SetActive(hasContext);
        }

        SetButton(optionAButton, optionALabel, question.optionA);
        SetButton(optionBButton, optionBLabel, question.optionB);
        SetButton(optionCButton, optionCLabel, question.optionC);
        PlayQuestionVoice(question.voiceClip);
    }

    void OnOptionASelected()
    {
        SaveSelectedAnswer(GetCurrentOptionText(optionALabel, questions[currentQuestionIndex].optionA));
        AdvanceQuestionnaire();
    }

    void OnOptionBSelected()
    {
        SaveSelectedAnswer(GetCurrentOptionText(optionBLabel, questions[currentQuestionIndex].optionB));
        AdvanceQuestionnaire();
    }

    void OnOptionCSelected()
    {
        SaveSelectedAnswer(GetCurrentOptionText(optionCLabel, questions[currentQuestionIndex].optionC));
        AdvanceQuestionnaire();
    }

    string GetCurrentOptionText(TMP_Text label, string fallback)
    {
        if (label != null && !string.IsNullOrWhiteSpace(label.text))
        {
            return label.text;
        }

        return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback;
    }

    void SaveSelectedAnswer(string answerText)
    {
        var key = currentQuestionIndex == 0
            ? FirstQuestionAnswerKey
            : SecondQuestionAnswerKey;

        PlayerPrefs.SetString(key, answerText);
        PlayerPrefs.Save();
    }

    void ClearSavedAnswers()
    {
        PlayerPrefs.DeleteKey(FirstQuestionAnswerKey);
        PlayerPrefs.DeleteKey(SecondQuestionAnswerKey);
        PlayerPrefs.Save();
    }

    void AdvanceQuestionnaire()
    {
        if (isTransitioning)
        {
            return;
        }

        if (currentQuestionIndex < questions.Length - 1)
        {
            SetPlayerHighlightersVisible(true);
            StartCoroutine(TransitionToNextQuestion(currentQuestionIndex + 1));
            return;
        }

        StartCoroutine(FinishQuestionnaireAndLoadScene());
    }

    IEnumerator TransitionToNextQuestion(int nextQuestionIndex)
    {
        isTransitioning = true;
        HideQuestionnaire();
        StopQuestionVoice();
        SetOrbitPaused(false);
        yield return new WaitForSeconds(secondQuestionDelay);
        SetOrbitPaused(true);
        ShowQuestionnaireShell();
        ShowQuestion(nextQuestionIndex);
        isTransitioning = false;
    }

    IEnumerator FinishQuestionnaireAndLoadScene()
    {
        isTransitioning = true;
        SetPlayerHighlightersVisible(false);
        HideQuestionnaire();
        StopQuestionVoice();
        SetOrbitPaused(true);

        if (questionAudioSource != null && postQuestionnaireVoiceClip != null)
        {
            questionAudioSource.clip = postQuestionnaireVoiceClip;
            questionAudioSource.Play();
            yield return new WaitWhile(() => questionAudioSource != null && questionAudioSource.isPlaying);
        }

        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadSceneAsync(nextSceneName);
        }
    }

    void PlayQuestionVoice(AudioClip clip)
    {
        if (questionAudioSource == null)
        {
            return;
        }

        questionAudioSource.Stop();
        questionAudioSource.clip = clip;
        if (clip != null)
        {
            questionAudioSource.Play();
        }
    }

    void StopQuestionVoice()
    {
        if (questionAudioSource == null)
        {
            return;
        }

        questionAudioSource.Stop();
        questionAudioSource.clip = null;
    }

    void SetOrbitPaused(bool paused)
    {
        if (orbitCamera != null)
        {
            orbitCamera.SetOrbitPaused(paused);
        }
    }

    void SetPlayerHighlightersVisible(bool visible)
    {
        if (playerHighlighters == null)
        {
            return;
        }

        for (var i = 0; i < playerHighlighters.Length; i++)
        {
            var highlighter = playerHighlighters[i];
            if (highlighter == null || highlighter.root == null)
            {
                continue;
            }

            EnsureHighlighterVisual(highlighter);
            var shouldShow = visible && highlighter.targetPlayer != null;
            if (shouldShow)
            {
                PositionHighlighter(highlighter);
                UpdateHighlighterText(highlighter);
            }

            highlighter.root.SetActive(shouldShow);
        }
    }

    void EnsureAllHighlighterVisuals()
    {
        if (playerHighlighters == null)
        {
            return;
        }

        for (var i = 0; i < playerHighlighters.Length; i++)
        {
            var highlighter = playerHighlighters[i];
            if (highlighter == null || highlighter.root == null)
            {
                continue;
            }

            EnsureHighlighterVisual(highlighter);
        }
    }

    void UpdatePlayerHighlighters()
    {
        if (playerHighlighters == null)
        {
            return;
        }

        for (var i = 0; i < playerHighlighters.Length; i++)
        {
            var highlighter = playerHighlighters[i];
            if (highlighter == null || highlighter.root == null || !highlighter.root.activeSelf)
            {
                continue;
            }

            if (highlighter.targetPlayer == null)
            {
                highlighter.root.SetActive(false);
                continue;
            }

            EnsureHighlighterVisual(highlighter);
            PositionHighlighter(highlighter);
            UpdateHighlighterText(highlighter);
        }
    }

    void EnsureHighlighterVisual(GroundPlayerHighlighter highlighter)
    {
        if (highlighter.visualBuilt || highlighter.root == null)
        {
            return;
        }

        if (TryUseExistingHighlighterVisual(highlighter))
        {
            return;
        }

        var shadow = CreateQuad("Soft Shadow", highlighter.root.transform, new Color(0f, 0f, 0f, 0.45f));
        shadow.transform.localPosition = new Vector3(0.08f, -0.06f, 0.002f);
        shadow.transform.localScale = new Vector3(3.2f, 0.86f, 1f);

        var plate = CreateQuad("Plate", highlighter.root.transform, highlighterBlue);
        plate.transform.localPosition = new Vector3(0f, 0f, 0.006f);
        plate.transform.localScale = new Vector3(3.05f, 0.74f, 1f);

        var highlight = CreateQuad("Top Highlight", highlighter.root.transform, new Color(1f, 1f, 1f, 0.16f));
        highlight.transform.localPosition = new Vector3(0f, 0.24f, 0.01f);
        highlight.transform.localScale = new Vector3(2.82f, 0.12f, 1f);

        var accent = CreateQuad("Accent", highlighter.root.transform, highlighterGreen);
        accent.transform.localPosition = new Vector3(-1.08f, -0.23f, 0.012f);
        accent.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
        accent.transform.localScale = new Vector3(0.95f, 0.13f, 1f);

        var numberBlock = CreateQuad("Number Block", highlighter.root.transform, new Color(0.01f, 0.06f, 0.34f, 0.94f));
        numberBlock.transform.localPosition = new Vector3(1.08f, 0f, 0.014f);
        numberBlock.transform.localScale = new Vector3(0.74f, 0.56f, 1f);

        var pointer = CreateQuad("Ground Pointer", highlighter.root.transform, highlighterGreen);
        pointer.transform.localPosition = new Vector3(1.72f, 0.48f, 0.009f);
        pointer.transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
        pointer.transform.localScale = new Vector3(0.16f, 1.25f, 1f);

        highlighter.positionText = CreateHighlighterText(
            "Position Text",
            highlighter.root.transform,
            new Vector3(-0.35f, -0.015f, -0.05f),
            new Vector2(2.35f, 0.8f),
            5.2f,
            TextAlignmentOptions.Center);

        highlighter.numberText = CreateHighlighterText(
            "Number Text",
            highlighter.root.transform,
            new Vector3(1.08f, -0.015f, -0.055f),
            new Vector2(0.95f, 0.8f),
            5.6f,
            TextAlignmentOptions.Center);

        highlighter.visualBuilt = true;
        UpdateHighlighterText(highlighter);
    }

    bool TryUseExistingHighlighterVisual(GroundPlayerHighlighter highlighter)
    {
        var existingNumber = highlighter.root.transform.Find("Number Text");
        var existingPosition = highlighter.root.transform.Find("Position Text");
        if (existingNumber == null || existingPosition == null)
        {
            return false;
        }

        highlighter.numberText = existingNumber.GetComponent<TextMeshPro>();
        highlighter.positionText = existingPosition.GetComponent<TextMeshPro>();
        existingNumber.localRotation = Quaternion.identity;
        existingPosition.localRotation = Quaternion.identity;
        highlighter.visualBuilt = true;
        UpdateHighlighterText(highlighter);
        return true;
    }

    TextMeshPro CreateHighlighterText(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector2 size,
        float maxFontSize,
        TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(MeshRenderer), typeof(TextMeshPro));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        textObject.transform.localScale = Vector3.one * 0.78f;

        var text = textObject.GetComponent<TextMeshPro>();
        text.alignment = alignment;
        text.fontSize = maxFontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = highlighterTextColor;
        text.rectTransform.sizeDelta = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 2.2f;
        text.fontSizeMax = maxFontSize;
        return text;
    }

    GameObject CreateQuad(string objectName, Transform parent, Color color)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objectName;
        quad.transform.SetParent(parent, false);

        var collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyObject(collider);
        }

        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateHighlighterMaterial(color);
        return quad;
    }

    Material CreateHighlighterMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        var material = new Material(shader);
        material.color = color;
        return material;
    }

    void PositionHighlighter(GroundPlayerHighlighter highlighter)
    {
        var target = highlighter.targetPlayer;
        var rootTransform = highlighter.root.transform;
        rootTransform.position = target.position + highlighter.groundOffset;
        rootTransform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
        rootTransform.localScale = Vector3.one * Mathf.Max(0.01f, highlighter.scale);
    }

    void UpdateHighlighterText(GroundPlayerHighlighter highlighter)
    {
        if (highlighter.numberText == null || highlighter.positionText == null)
        {
            return;
        }

        var number = string.IsNullOrWhiteSpace(highlighter.playerNumber) ? "--" : highlighter.playerNumber;
        var position = string.IsNullOrWhiteSpace(highlighter.playerPosition) ? "PLAYER" : highlighter.playerPosition;
        highlighter.positionText.text = position.ToUpperInvariant();
        highlighter.numberText.text = number;
    }

    void DestroyObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    static void SetButton(Button button, TMP_Text label, string text)
    {
        var hasValue = !string.IsNullOrWhiteSpace(text);

        if (button != null)
        {
            button.gameObject.SetActive(hasValue);
        }

        if (label != null)
        {
            label.text = hasValue ? text : string.Empty;
        }
    }

    static void SetActive(GameObject target, bool state)
    {
        if (target != null)
        {
            target.SetActive(state);
        }
    }
}
