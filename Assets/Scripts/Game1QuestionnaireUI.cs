using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Scene")]
    [SerializeField] string nextSceneName = "Game";

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

    void Awake()
    {
        BindButtons();
        ShowQuestionnaireShell();
        ShowQuestion(0);
    }

    void OnDestroy()
    {
        UnbindButtons();
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
        if (currentQuestionIndex < questions.Length - 1)
        {
            ShowQuestion(currentQuestionIndex + 1);
            return;
        }

        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadSceneAsync(nextSceneName);
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
