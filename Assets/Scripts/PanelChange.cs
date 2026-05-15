using UnityEngine;
using UnityEngine.SceneManagement;

public class PanelChange : MonoBehaviour
{

    public void SceneChange()
    {
        SceneManager.LoadSceneAsync("Game 1");
    }    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
