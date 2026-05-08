using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VRFootballScenarioUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject rootPanel;
    [SerializeField] GameObject countdownPanel;
    [SerializeField] GameObject taskPanel;
    [SerializeField] GameObject failPanel;
    [SerializeField] GameObject successPanel;
    [SerializeField] GameObject resumePanel;

    [Header("Countdown")]
    [SerializeField] TMP_Text countdownText;

    [Header("Task")]
    [SerializeField] TMP_Text taskTitleText;
    [SerializeField] TMP_Text taskDescriptionText;
    [SerializeField] TMP_Text taskHintText;
    [SerializeField] TMP_Text taskTimerText;

    [Header("Resume")]
    [SerializeField] Button resumeButton;
    [SerializeField] bool autoCreateResumeButtonIfMissing = true;
    [SerializeField] string resumeButtonText = "Resume";
    [SerializeField] TMP_Text resumeTitleText;
    [SerializeField] TMP_Text resumeDescriptionText;
    [SerializeField] TMP_Text resumeHintText;

    [Header("Failure")]
    [SerializeField] TMP_Text failTitleText;
    [SerializeField] TMP_Text failDescriptionText;

    [Header("Success")]
    [SerializeField] TMP_Text successTitleText;
    [SerializeField] TMP_Text successDescriptionText;

    public event Action ResumeRequested;

    void Awake()
    {
        EnsureResumeButtonExists();
        HideAll();
    }

    void OnEnable()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(RequestResume);
        }
    }

    void OnDisable()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(RequestResume);
        }
    }

    void EnsureResumeButtonExists()
    {
        if (!autoCreateResumeButtonIfMissing || resumeButton != null)
        {
            return;
        }

        var parent = resumePanel != null
            ? resumePanel.transform
            : taskPanel != null
                ? taskPanel.transform
                : rootPanel != null
                    ? rootPanel.transform
                    : transform;

        var buttonObject = new GameObject("Resume Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, 60f);
        rectTransform.sizeDelta = new Vector2(260f, 80f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.1f, 0.45f, 1f, 0.95f);

        resumeButton = buttonObject.GetComponent<Button>();
        resumeButton.targetGraphic = image;

        var labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = resumeButtonText;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 30f;
        label.raycastTarget = false;
    }

    public void HideAll()
    {
        SetActive(rootPanel, false);
        SetActive(countdownPanel, false);
        SetActive(taskPanel, false);
        SetActive(failPanel, false);
        SetActive(successPanel, false);
        SetActive(resumePanel, false);
    }

    public void ShowCountdown(int count)
    {
        SetActive(rootPanel, true);
        SetActive(countdownPanel, true);
        SetActive(taskPanel, false);
        SetActive(failPanel, false);
        SetActive(successPanel, false);
        SetActive(resumePanel, false);

        if (countdownText != null)
        {
            countdownText.text = count.ToString();
        }
    }

    public void ShowTask(string title, string description, float remainingSeconds)
    {
        ShowTask(title, description, string.Empty, remainingSeconds);
    }

    public void ShowTask(string title, string description, string hint, float remainingSeconds)
    {
        SetActive(rootPanel, true);
        SetActive(countdownPanel, false);
        SetActive(taskPanel, true);
        SetActive(failPanel, false);
        SetActive(successPanel, false);
        SetActive(resumePanel, false);

        if (taskTitleText != null)
        {
            taskTitleText.text = title;
        }

        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = description;
        }

        if (taskHintText != null)
        {
            taskHintText.text = hint;
            taskHintText.gameObject.SetActive(!string.IsNullOrWhiteSpace(hint));
        }

        UpdateTaskTimer(remainingSeconds);
    }

    public void UpdateTaskTimer(float remainingSeconds)
    {
        if (taskTimerText == null)
        {
            return;
        }

        taskTimerText.text = remainingSeconds.ToString("0.0") + "s";
    }

    public void ShowResume(string title, string description, string hint)
    {
        SetActive(rootPanel, true);
        SetActive(countdownPanel, false);
        SetActive(taskPanel, resumePanel == null);
        SetActive(failPanel, false);
        SetActive(successPanel, false);
        SetActive(resumePanel, resumePanel != null);

        if (resumePanel != null)
        {
            if (resumeTitleText != null)
            {
                resumeTitleText.text = title;
            }

            if (resumeDescriptionText != null)
            {
                resumeDescriptionText.text = description;
            }

            if (resumeHintText != null)
            {
                resumeHintText.text = hint;
                resumeHintText.gameObject.SetActive(!string.IsNullOrWhiteSpace(hint));
            }

            return;
        }

        if (taskTitleText != null)
        {
            taskTitleText.text = title;
        }

        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = description;
        }

        if (taskHintText != null)
        {
            taskHintText.text = hint;
            taskHintText.gameObject.SetActive(!string.IsNullOrWhiteSpace(hint));
        }

        if (taskTimerText != null)
        {
            taskTimerText.text = "Paused";
        }
    }

    public void RequestResume()
    {
        ResumeRequested?.Invoke();
    }

    public void ShowFailure(string title, string description)
    {
        SetActive(rootPanel, true);
        SetActive(countdownPanel, false);
        SetActive(taskPanel, false);
        SetActive(failPanel, true);
        SetActive(successPanel, false);
        SetActive(resumePanel, false);

        if (failTitleText != null)
        {
            failTitleText.text = title;
        }

        if (failDescriptionText != null)
        {
            failDescriptionText.text = description;
        }
    }

    public void ShowSuccess(string title, string description)
    {
        SetActive(rootPanel, true);
        SetActive(countdownPanel, false);
        SetActive(failPanel, false);
        SetActive(taskPanel, successPanel == null);
        SetActive(successPanel, successPanel != null);
        SetActive(resumePanel, false);

        if (successPanel != null)
        {
            if (successTitleText != null)
            {
                successTitleText.text = title;
            }

            if (successDescriptionText != null)
            {
                successDescriptionText.text = description;
            }
            return;
        }

        if (taskTitleText != null)
        {
            taskTitleText.text = title;
        }

        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = description;
        }

        if (taskHintText != null)
        {
            taskHintText.text = string.Empty;
            taskHintText.gameObject.SetActive(false);
        }

        if (taskTimerText != null)
        {
            taskTimerText.text = string.Empty;
        }
    }

    public void HideCountdown()
    {
        SetActive(countdownPanel, false);
    }

    public void HideTask()
    {
        SetActive(taskPanel, false);
        SetActive(resumePanel, false);
    }

    static void SetActive(GameObject target, bool state)
    {
        if (target != null)
        {
            target.SetActive(state);
        }
    }
}