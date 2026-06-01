using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class Game1QuestionnaireUI : MonoBehaviour
{
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
    [SerializeField] float firstQuestionDelay = 8f;
    [SerializeField] float secondQuestionDelay = 5f;
    [SerializeField] SceneOrbitCamera orbitCamera;

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

    void Awake()
    {
        EnsureAudioSource();
        ResolveOrbitCamera();
        BindButtons();
        HideQuestionnaire();
        StartCoroutine(BeginQuestionnaireSequence());
    }

    void OnDestroy()
    {
        UnbindButtons();
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
        AdvanceQuestionnaire();
    }

    void OnOptionBSelected()
    {
        AdvanceQuestionnaire();
    }

    void OnOptionCSelected()
    {
        AdvanceQuestionnaire();
    }

    void AdvanceQuestionnaire()
    {
        if (isTransitioning)
        {
            return;
        }

        if (currentQuestionIndex < questions.Length - 1)
        {
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
