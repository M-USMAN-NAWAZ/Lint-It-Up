using TMPro;
using UnityEngine;

public class VRFootballScenarioUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject rootPanel;
    [SerializeField] GameObject countdownPanel;
    [SerializeField] GameObject taskPanel;
    [SerializeField] GameObject failPanel;
    [SerializeField] GameObject successPanel;

    [Header("Countdown")]
    [SerializeField] TMP_Text countdownText;

    [Header("Task")]
    [SerializeField] TMP_Text taskTitleText;
    [SerializeField] TMP_Text taskDescriptionText;
    [SerializeField] TMP_Text taskHintText;
    [SerializeField] TMP_Text taskTimerText;

    [Header("Failure")]
    [SerializeField] TMP_Text failTitleText;
    [SerializeField] TMP_Text failDescriptionText;

    [Header("Success")]
    [SerializeField] TMP_Text successTitleText;
    [SerializeField] TMP_Text successDescriptionText;

    void Awake()
    {
        HideAll();
    }

    public void HideAll()
    {
        SetActive(rootPanel, false);
        SetActive(countdownPanel, false);
        SetActive(taskPanel, false);
        SetActive(failPanel, false);
        SetActive(successPanel, false);
    }

    public void ShowCountdown(int count)
    {
        SetActive(rootPanel, true);
        SetActive(countdownPanel, true);
        SetActive(taskPanel, false);
        SetActive(failPanel, false);
        SetActive(successPanel, false);

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

    public void ShowFailure(string title, string description)
    {
        SetActive(rootPanel, true);
        SetActive(countdownPanel, false);
        SetActive(taskPanel, false);
        SetActive(failPanel, true);
        SetActive(successPanel, false);

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
    }

    static void SetActive(GameObject target, bool state)
    {
        if (target != null)
        {
            target.SetActive(state);
        }
    }
}
