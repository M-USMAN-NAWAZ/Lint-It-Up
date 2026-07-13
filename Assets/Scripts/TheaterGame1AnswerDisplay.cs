using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TheaterGame1AnswerDisplay : MonoBehaviour
{
    public const string OpenScorecardOnTheaterLoadKey = "Theater.OpenGame1Scorecard";

    [Header("Answer Text References")]
    public TMP_Text firstQuestionAnswerText;
    public TMP_Text secondQuestionAnswerText;

    [Header("Fallback")]
    public string missingAnswerText = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, "Theater", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (PlayerPrefs.GetInt(OpenScorecardOnTheaterLoadKey, 0) != 1)
        {
            return;
        }

        PlayerPrefs.DeleteKey(OpenScorecardOnTheaterLoadKey);
        PlayerPrefs.Save();

        var display = FindScorecardDisplay(scene);
        if (display == null)
        {
            return;
        }

        SetSceneObjectActive(scene, "IntroVideoPanel", false);
        SetSceneObjectActive(scene, "Panel 2", true);
        ActivateHierarchy(display.transform);
        display.LoadSavedGame1Answers();
    }

    static void SetSceneObjectActive(Scene scene, string objectName, bool active)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var target = FindChildByName(root.transform, objectName);
            if (target != null)
            {
                target.gameObject.SetActive(active);
                return;
            }
        }
    }

    static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var match = FindChildByName(root.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
    static TheaterGame1AnswerDisplay FindScorecardDisplay(Scene scene)
    {
        var displays = Resources.FindObjectsOfTypeAll<TheaterGame1AnswerDisplay>();
        TheaterGame1AnswerDisplay fallback = null;

        foreach (var display in displays)
        {
            if (display == null || display.gameObject.scene != scene)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = display;
            }

            if (display.gameObject.name.Contains("Panel 2 (3)"))
            {
                return display;
            }
        }

        return fallback;
    }

    static void ActivateHierarchy(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (target.parent != null)
        {
            ActivateHierarchy(target.parent);
        }

        target.gameObject.SetActive(true);
    }

    public void LoadSavedGame1Answers()
    {
        SetText(
            firstQuestionAnswerText,
            PlayerPrefs.GetString(Game1QuestionnaireUI.FirstQuestionAnswerKey, missingAnswerText));

        SetText(
            secondQuestionAnswerText,
            PlayerPrefs.GetString(Game1QuestionnaireUI.SecondQuestionAnswerKey, missingAnswerText));
    }

    public void ClearAnswerTexts()
    {
        SetText(firstQuestionAnswerText, string.Empty);
        SetText(secondQuestionAnswerText, string.Empty);
    }

    static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}