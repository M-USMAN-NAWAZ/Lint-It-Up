using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RightControllerSceneExit : MonoBehaviour
{
    [SerializeField] string exitSceneName = "Theater";
    [SerializeField] bool useAsyncLoad = true;

    InputAction exitAction;

    void Awake()
    {
        exitAction = new InputAction(
            name: "Right Controller Exit",
            type: InputActionType.Button,
            binding: "<XRController>{RightHand}/{SecondaryButton}");
        exitAction.performed += OnExitPerformed;
    }

    void OnEnable()
    {
        exitAction?.Enable();
    }

    void OnDisable()
    {
        exitAction?.Disable();
    }

    void OnDestroy()
    {
        if (exitAction == null)
        {
            return;
        }

        exitAction.performed -= OnExitPerformed;
        exitAction.Dispose();
        exitAction = null;
    }

    void OnExitPerformed(InputAction.CallbackContext context)
    {
        ExitScene();
    }

    public void ExitScene()
    {
        if (string.IsNullOrWhiteSpace(exitSceneName))
        {
            return;
        }

        if (useAsyncLoad)
        {
            SceneManager.LoadSceneAsync(exitSceneName);
            return;
        }

        SceneManager.LoadScene(exitSceneName);
    }
}
