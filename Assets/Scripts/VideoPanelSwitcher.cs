using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoPanelSwitcher : MonoBehaviour
{
    [Header("Video Player")]
    public VideoPlayer videoPlayer;

    [Header("Panels")]
    public GameObject panelToTurnOff;
    public GameObject panelToTurnOn;

    private void Start()
    {
        // Subscribe to the event when video finishes
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        // Turn off current panel
        if (panelToTurnOff != null)
            panelToTurnOff.SetActive(false);

        // Turn on next panel
        if (panelToTurnOn != null)
            panelToTurnOn.SetActive(true);
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}