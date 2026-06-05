using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoPlayerTimelineControls : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public Slider timelineSlider;
    public Button playPauseButton;
    public Image playPauseButtonImage;
    public Sprite playSprite;
    public Sprite pauseSprite;
    public TMP_Text currentTimeText;
    public TMP_Text durationText;

    [Header("Startup")]
    public bool findReferencesInChildren = true;
    public bool prepareVideoOnStart = true;
    public bool pauseOnStart;

    [Header("Auto Hide")]
    public bool autoHideControls = true;
    public float visibleSeconds = 2f;
    public float fadeDuration = 0.25f;
    public CanvasGroup sliderCanvasGroup;
    public CanvasGroup playPauseCanvasGroup;

    bool isUpdatingSlider;
    bool hasPendingSeek;
    float pendingNormalizedTime;
    float lastControlsShownTime;
    float controlsAlpha = 1f;
    float targetControlsAlpha = 1f;

    void Reset()
    {
        ResolveMissingReferences();
    }

    void Awake()
    {
        ResolveMissingReferences();
        ConfigureSlider();
        EnsureCanvasGroups();
        ShowControls();
    }

    void OnEnable()
    {
        Subscribe();
        ConfigureSlider();
        EnsureCanvasGroups();
        PrepareIfNeeded();
        ShowControls();
        UpdateControls();
    }

    void Start()
    {
        if (pauseOnStart && videoPlayer != null)
        {
            videoPlayer.Pause();
        }

        UpdateControls();
    }

    void Update()
    {
        UpdateAutoHide();
        UpdateControls();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    public void TogglePlayPause()
    {
        if (videoPlayer == null)
        {
            return;
        }

        if (!videoPlayer.isPrepared && prepareVideoOnStart)
        {
            videoPlayer.Prepare();
            ShowControls();
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }
        else
        {
            videoPlayer.Play();
        }

        ShowControls();
        UpdateControls();
    }

    public void Play()
    {
        if (videoPlayer == null)
        {
            return;
        }

        if (!videoPlayer.isPrepared && prepareVideoOnStart)
        {
            videoPlayer.Prepare();
        }

        videoPlayer.Play();
        ShowControls();
        UpdateControls();
    }

    public void Pause()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.Pause();
        ShowControls();
        UpdateControls();
    }

    public void SetNormalizedTime(float normalizedTime)
    {
        if (videoPlayer == null)
        {
            return;
        }

        var duration = GetDuration();
        if (duration <= 0.0001d)
        {
            pendingNormalizedTime = Mathf.Clamp01(normalizedTime);
            hasPendingSeek = true;
            PrepareIfNeeded();
            return;
        }

        videoPlayer.time = Mathf.Clamp01(normalizedTime) * duration;
        ShowControls();
        UpdateControls();
    }

    public void ShowControls()
    {
        lastControlsShownTime = Time.unscaledTime;
        targetControlsAlpha = 1f;
    }

    public void HideControls()
    {
        targetControlsAlpha = 0f;
    }

    void ResolveMissingReferences()
    {
        if (!findReferencesInChildren)
        {
            return;
        }

        if (videoPlayer == null)
        {
            videoPlayer = GetComponentInChildren<VideoPlayer>(true);
        }

        if (timelineSlider == null)
        {
            timelineSlider = GetComponentInChildren<Slider>(true);
        }

        if (playPauseButton == null)
        {
            playPauseButton = GetComponentInChildren<Button>(true);
        }

        if (playPauseButtonImage == null && playPauseButton != null)
        {
            playPauseButtonImage = playPauseButton.GetComponent<Image>();
        }
    }

    void Subscribe()
    {
        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        if (playPauseButton != null)
        {
            playPauseButton.onClick.RemoveListener(TogglePlayPause);
            playPauseButton.onClick.AddListener(TogglePlayPause);
        }

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.loopPointReached += OnVideoFinished;
        }
    }

    void Unsubscribe()
    {
        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        if (playPauseButton != null)
        {
            playPauseButton.onClick.RemoveListener(TogglePlayPause);
        }

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }

    void ConfigureSlider()
    {
        if (timelineSlider == null)
        {
            return;
        }

        timelineSlider.minValue = 0f;
        timelineSlider.maxValue = 1f;
        timelineSlider.wholeNumbers = false;
    }

    void EnsureCanvasGroups()
    {
        if (timelineSlider != null && sliderCanvasGroup == null)
        {
            sliderCanvasGroup = timelineSlider.GetComponent<CanvasGroup>();
            if (sliderCanvasGroup == null)
            {
                sliderCanvasGroup = timelineSlider.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (playPauseButton != null && playPauseCanvasGroup == null)
        {
            playPauseCanvasGroup = playPauseButton.GetComponent<CanvasGroup>();
            if (playPauseCanvasGroup == null)
            {
                playPauseCanvasGroup = playPauseButton.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    void PrepareIfNeeded()
    {
        if (!prepareVideoOnStart || videoPlayer == null || videoPlayer.isPrepared)
        {
            return;
        }

        videoPlayer.Prepare();
    }

    void OnVideoPrepared(VideoPlayer preparedVideoPlayer)
    {
        if (hasPendingSeek)
        {
            hasPendingSeek = false;
            SetNormalizedTime(pendingNormalizedTime);
        }

        if (pauseOnStart)
        {
            preparedVideoPlayer.Pause();
        }

        UpdateControls();
    }

    void OnVideoFinished(VideoPlayer finishedVideoPlayer)
    {
        UpdateControls();
    }

    void OnSliderValueChanged(float normalizedTime)
    {
        if (isUpdatingSlider)
        {
            return;
        }

        ShowControls();
        SetNormalizedTime(normalizedTime);
    }

    void UpdateAutoHide()
    {
        if (!autoHideControls)
        {
            targetControlsAlpha = 1f;
        }
        else if (Time.unscaledTime - lastControlsShownTime >= Mathf.Max(0f, visibleSeconds))
        {
            targetControlsAlpha = 0f;
        }

        var duration = Mathf.Max(0.0001f, fadeDuration);
        var nextAlpha = Mathf.MoveTowards(controlsAlpha, targetControlsAlpha, Time.unscaledDeltaTime / duration);
        ApplyControlsAlpha(nextAlpha);
    }

    void ApplyControlsAlpha(float alpha)
    {
        controlsAlpha = Mathf.Clamp01(alpha);
        ApplyCanvasGroup(sliderCanvasGroup, controlsAlpha);
        ApplyCanvasGroup(playPauseCanvasGroup, controlsAlpha);
    }

    static void ApplyCanvasGroup(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = alpha;
        canvasGroup.interactable = alpha > 0.05f;
        canvasGroup.blocksRaycasts = alpha > 0.05f;
    }

    void UpdateControls()
    {
        if (videoPlayer == null)
        {
            return;
        }

        var duration = GetDuration();
        var currentTime = Mathf.Clamp((float)videoPlayer.time, 0f, duration > 0d ? (float)duration : 0f);
        var normalizedTime = duration > 0.0001d
            ? Mathf.Clamp01((float)(videoPlayer.time / duration))
            : 0f;

        if (timelineSlider != null)
        {
            isUpdatingSlider = true;
            timelineSlider.SetValueWithoutNotify(normalizedTime);
            isUpdatingSlider = false;
        }

        if (playPauseButtonImage != null)
        {
            var targetSprite = videoPlayer.isPlaying ? pauseSprite : playSprite;
            if (targetSprite != null)
            {
                playPauseButtonImage.sprite = targetSprite;
            }
        }

        if (currentTimeText != null)
        {
            currentTimeText.text = FormatTime(currentTime);
        }

        if (durationText != null)
        {
            durationText.text = duration > 0.0001d ? FormatTime((float)duration) : "00:00";
        }
    }

    double GetDuration()
    {
        if (videoPlayer == null)
        {
            return 0d;
        }

        if (videoPlayer.length > 0.0001d)
        {
            return videoPlayer.length;
        }

        if (videoPlayer.frameCount > 0 && videoPlayer.frameRate > 0.0001d)
        {
            return videoPlayer.frameCount / videoPlayer.frameRate;
        }

        return 0d;
    }

    static string FormatTime(float seconds)
    {
        var totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        var minutes = totalSeconds / 60;
        var secondsPart = totalSeconds % 60;
        return minutes.ToString("00") + ":" + secondsPart.ToString("00");
    }
}
