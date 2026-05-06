using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonClickSound : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clickClip;
    [SerializeField] float volume = 1f;

    Button button;

    void Reset()
    {
        button = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        button = GetComponent<Button>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(PlayClickSound);
        }
    }

    void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClickSound);
        }
    }

    void PlayClickSound()
    {
        if (clickClip == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clickClip, volume);
            return;
        }

        AudioSource.PlayClipAtPoint(clickClip, transform.position, volume);
    }
}
