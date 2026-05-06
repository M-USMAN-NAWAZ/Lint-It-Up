using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    public void GoToOffenceField()
    {
        SceneManager.LoadSceneAsync("Game");
    }
}
