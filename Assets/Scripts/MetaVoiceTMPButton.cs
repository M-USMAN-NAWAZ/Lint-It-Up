using Meta.WitAi;
using Meta.WitAi.Configuration;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaVoiceTMPButton : MonoBehaviour
{
    const string DefaultRecognizedQuestion =
        "How might New England adjust to our 2 Tight Ends Trips set, and how should we counter?";
    const string DefaultRecognizedAnswer =
        "Based on analysis if this year's games, New England will most certainly expect run, especially if you execute run early. If they are suspecting pass, they may show cover 1, and then settle back into cover 2. If they do this, look for the running back on a flare, or release him on a wheel route.";
    const string DefaultUnrecognizedQuestionText = "This AI feature is coming soon";

    [Header("Voice")]
    [SerializeField] VoiceService voiceService;
    [SerializeField] bool activateImmediately = true;

    [Header("UI")]
    [SerializeField] Button activationButton;
    [SerializeField] TMP_Text transcriptionText;
    [SerializeField] TMP_Text buttonLabel;

    [Header("Text")]
    [SerializeField] string idleText = "Press the button and speak";
    [SerializeField] string listeningText = "Listening...";
    [SerializeField] string activateButtonText = "Speak";
    [SerializeField] string deactivateButtonText = "Stop";
    [SerializeField] bool clearTextOnActivate = true;
    [SerializeField] bool showPartialTranscription = true;
    [Min(0f)] [SerializeField] float hideDelayAfterRecognitionSeconds = 5f;
    [Min(0.01f)] [SerializeField] float fadeDurationSeconds = 0.45f;

    [Header("Question Response")]
    [TextArea(2, 4)] [SerializeField] string recognizedQuestion =
        DefaultRecognizedQuestion;
    [TextArea(4, 8)] [SerializeField] string recognizedAnswer =
        DefaultRecognizedAnswer;
    [SerializeField] string unrecognizedQuestionText = DefaultUnrecognizedQuestionText;
    [SerializeField] bool answerFromPartialTranscription = true;
    [SerializeField] bool logVoiceTranscriptions;

    VoiceServiceRequest activeRequest;
    bool isListening;
    bool answerShownForCurrentRequest;
    string latestTranscription = string.Empty;
    Color transcriptionBaseColor = Color.white;
    Coroutine hideRoutine;

    void Reset()
    {
        activationButton = GetComponent<Button>();
        buttonLabel = GetComponentInChildren<TMP_Text>(true);
        transcriptionText = GetComponentInChildren<TMP_Text>(true);
    }

    void Awake()
    {
        ResolveReferences();
        CacheTranscriptionColor();
        SetTranscriptionAlpha(0f);
        SetOutputText(idleText, false);
        RefreshButtonState();
    }

    void OnEnable()
    {
        ResolveReferences();
        CacheTranscriptionColor();

        if (activationButton != null)
        {
            activationButton.onClick.AddListener(ToggleListening);
        }

        if (voiceService != null)
        {
            voiceService.VoiceEvents.OnStartListening.AddListener(OnStartListening);
            voiceService.VoiceEvents.OnStoppedListening.AddListener(OnStoppedListening);
            voiceService.VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
            voiceService.VoiceEvents.OnFullTranscription.AddListener(OnFullTranscription);
            voiceService.VoiceEvents.OnResponse.AddListener(OnVoiceResponse);
            voiceService.VoiceEvents.OnError.AddListener(OnVoiceError);
            voiceService.VoiceEvents.OnComplete.AddListener(OnVoiceComplete);
        }

        RefreshButtonState();
    }

    void OnDisable()
    {
        if (activationButton != null)
        {
            activationButton.onClick.RemoveListener(ToggleListening);
        }

        if (voiceService != null)
        {
            voiceService.VoiceEvents.OnStartListening.RemoveListener(OnStartListening);
            voiceService.VoiceEvents.OnStoppedListening.RemoveListener(OnStoppedListening);
            voiceService.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
            voiceService.VoiceEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
            voiceService.VoiceEvents.OnResponse.RemoveListener(OnVoiceResponse);
            voiceService.VoiceEvents.OnError.RemoveListener(OnVoiceError);
            voiceService.VoiceEvents.OnComplete.RemoveListener(OnVoiceComplete);
        }

        isListening = false;
        activeRequest = null;
    }

    public void ToggleListening()
    {
        ResolveReferences();

        if (voiceService == null)
        {
            SetOutputText("Voice service missing");
            return;
        }

        if (isListening || voiceService.MicActive)
        {
            StopListening();
            return;
        }

        StartListening();
    }

    public void StartListening()
    {
        if (voiceService == null)
        {
            return;
        }

        if (clearTextOnActivate)
        {
            SetOutputText(listeningText);
        }

        ShowTranscriptionText();

        var requestOptions = new WitRequestOptions();
        var requestEvents = new VoiceServiceRequestEvents();
        activeRequest = activateImmediately
            ? voiceService.ActivateImmediately(requestOptions, requestEvents)
            : voiceService.Activate(requestOptions, requestEvents);

        isListening = activeRequest != null || voiceService.MicActive;
        RefreshButtonState();
    }

    public void StopListening()
    {
        if (voiceService != null)
        {
            voiceService.Deactivate();
        }

        activeRequest = null;
        isListening = false;
        RefreshButtonState();
    }

    void ResolveReferences()
    {
        if (activationButton == null)
        {
            activationButton = GetComponent<Button>();
        }

        if (buttonLabel == null && activationButton != null)
        {
            buttonLabel = activationButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (transcriptionText == null && activationButton != null)
        {
            var textComponents = activationButton.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < textComponents.Length; i++)
            {
                if (textComponents[i] != null && textComponents[i] != buttonLabel)
                {
                    transcriptionText = textComponents[i];
                    break;
                }
            }
        }

        if (voiceService == null)
        {
            voiceService = FindAnyObjectByType<VoiceService>();
        }
    }

    void OnStartListening()
    {
        isListening = true;
        answerShownForCurrentRequest = false;
        latestTranscription = string.Empty;
        ShowTranscriptionText();
        SetOutputText(listeningText);
        RefreshButtonState();
    }

    void OnStoppedListening()
    {
        isListening = false;
        activeRequest = null;
        StartDelayedHide();
        RefreshButtonState();
    }

    void OnPartialTranscription(string transcription)
    {
        latestTranscription = transcription;
        LogTranscription("Partial", transcription);
        if (answerFromPartialTranscription && TryShowRecognizedAnswer(transcription))
        {
            return;
        }

        if (showPartialTranscription)
        {
            SetOutputText(transcription);
        }
    }

    void OnFullTranscription(string transcription)
    {
        latestTranscription = transcription;
        LogTranscription("Full", transcription);
        if (TryShowRecognizedAnswer(transcription))
        {
            return;
        }

        ShowUnrecognizedQuestionText();
    }

    void OnVoiceResponse(WitResponseNode response)
    {
        var transcription = response?.GetTranscription();
        if (string.IsNullOrWhiteSpace(transcription))
        {
            return;
        }

        latestTranscription = transcription;
        LogTranscription("Response", transcription);
        TryShowRecognizedAnswer(transcription);
    }

    void OnVoiceError(string status, string error)
    {
        ShowTranscriptionText();
        SetOutputText(string.IsNullOrWhiteSpace(status) ? error : status + ": " + error);
        isListening = false;
        activeRequest = null;
        StartDelayedHide();
        RefreshButtonState();
    }

    void OnVoiceComplete(VoiceServiceRequest request)
    {
        if (!TryShowRecognizedAnswer(latestTranscription) && !string.IsNullOrWhiteSpace(latestTranscription))
        {
            ShowUnrecognizedQuestionText();
        }

        if (transcriptionText != null && transcriptionText.text == listeningText)
        {
            SetOutputText(idleText);
        }

        if (!isListening && (voiceService == null || !voiceService.MicActive))
        {
            StartDelayedHide();
        }
    }

    void SetOutputText(string value, bool reveal = true)
    {
        if (transcriptionText != null)
        {
            if (reveal)
            {
                ShowTranscriptionText();
            }

            transcriptionText.text = value;
        }
    }

    void RefreshButtonState()
    {
        if (buttonLabel != null)
        {
            buttonLabel.text = isListening ? deactivateButtonText : activateButtonText;
        }
    }

    void CacheTranscriptionColor()
    {
        if (transcriptionText == null)
        {
            return;
        }

        transcriptionBaseColor = transcriptionText.color;
        transcriptionBaseColor.a = Mathf.Approximately(transcriptionBaseColor.a, 0f) ? 1f : transcriptionBaseColor.a;
    }

    void ShowTranscriptionText()
    {
        if (transcriptionText == null)
        {
            return;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        SetTranscriptionAlpha(1f);
    }

    void StartDelayedHide()
    {
        if (!isActiveAndEnabled || transcriptionText == null)
        {
            return;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(HideAfterRecognitionDelay());
    }

    IEnumerator HideAfterRecognitionDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hideDelayAfterRecognitionSeconds));

        var fadeDuration = Mathf.Max(0.01f, fadeDurationSeconds);
        var startAlpha = transcriptionText != null ? transcriptionText.color.a : 0f;
        var elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetTranscriptionAlpha(Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / fadeDuration)));
            yield return null;
        }

        SetTranscriptionAlpha(0f);
        hideRoutine = null;
    }

    void SetTranscriptionAlpha(float alpha)
    {
        if (transcriptionText == null)
        {
            return;
        }

        var color = transcriptionBaseColor;
        color.a = Mathf.Clamp01(alpha);
        transcriptionText.color = color;
    }

    bool TryShowRecognizedAnswer(string transcription)
    {
        if (answerShownForCurrentRequest)
        {
            return true;
        }

        if (!IsRecognizedQuestion(transcription))
        {
            return false;
        }

        answerShownForCurrentRequest = true;
        SetOutputText(GetRecognizedAnswer());
        return true;
    }

    bool IsRecognizedQuestion(string transcription)
    {
        var normalizedTranscription = NormalizeRecognitionText(transcription);
        var normalizedQuestion = NormalizeRecognitionText(GetRecognizedQuestion());

        if (string.IsNullOrWhiteSpace(normalizedTranscription) || string.IsNullOrWhiteSpace(normalizedQuestion))
        {
            return false;
        }

        if (normalizedTranscription == normalizedQuestion || normalizedTranscription.Contains(normalizedQuestion))
        {
            return true;
        }

        var mentionsNewEngland = normalizedTranscription.Contains("new england");
        var mentionsTrips = normalizedTranscription.Contains("trips") || normalizedTranscription.Contains("trip set");
        var mentionsTightEnds =
            normalizedTranscription.Contains("tight end") ||
            normalizedTranscription.Contains("2 tight") ||
            normalizedTranscription.Contains("two tight");
        var asksForAdjustmentOrCounter =
            normalizedTranscription.Contains("adjust") ||
            normalizedTranscription.Contains("counter") ||
            normalizedTranscription.Contains("how should we");

        return mentionsNewEngland && mentionsTrips && mentionsTightEnds && asksForAdjustmentOrCounter;
    }

    string GetRecognizedQuestion()
    {
        return string.IsNullOrWhiteSpace(recognizedQuestion)
            ? DefaultRecognizedQuestion
            : recognizedQuestion;
    }

    string GetRecognizedAnswer()
    {
        return string.IsNullOrWhiteSpace(recognizedAnswer)
            ? DefaultRecognizedAnswer
            : recognizedAnswer;
    }

    void ShowUnrecognizedQuestionText()
    {
        SetOutputText(string.IsNullOrWhiteSpace(unrecognizedQuestionText)
            ? DefaultUnrecognizedQuestionText
            : unrecognizedQuestionText);
    }

    void LogTranscription(string phase, string transcription)
    {
        if (!logVoiceTranscriptions)
        {
            return;
        }

        Debug.Log($"[MetaVoiceTMPButton] {phase} transcription: {transcription}", this);
    }

    static string NormalizeRecognitionText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = true;
        for (var i = 0; i < value.Length; i++)
        {
            var character = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        var normalized = builder.ToString().Trim();
        return (" " + normalized + " ").Replace(" two ", " 2 ").Trim();
    }
}
