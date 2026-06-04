using TMPro;
using UnityEngine;

public class TheaterGame1AnswerDisplay : MonoBehaviour
{
    [Header("Answer Text References")]
    public TMP_Text firstQuestionAnswerText;
    public TMP_Text secondQuestionAnswerText;

    [Header("Fallback")]
    public string missingAnswerText = "";

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
