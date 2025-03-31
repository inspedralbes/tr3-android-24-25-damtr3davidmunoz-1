using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.Collections;

public class MenuController : MonoBehaviour
{
    private UIDocument menuDocument;
    private VisualElement rootElement;
    private bool isChangingScene = false;

    private void Awake()
    {
        menuDocument = GetComponent<UIDocument>();
        if (menuDocument != null)
        {
            rootElement = menuDocument.rootVisualElement;
            rootElement.visible = false;
        }
    }

    private void Start()
    {
        if (menuDocument != null)
        {
            rootElement.visible = true;
            SetupButtons();
        }
    }

    private void SetupButtons()
    {
        var createButton = rootElement.Q<Button>("create-game-button");
        var joinButton = rootElement.Q<Button>("join-game-button");

        if (createButton != null)
        {
            createButton.clickable = new Clickable(() =>
            {
                if (!isChangingScene) StartCoroutine(ChangeSceneSafely());
            });
        }

        if (joinButton != null)
        {
            joinButton.clickable = new Clickable(() => 
            {
                Debug.Log("Unirse a partida - Funcionalidad no implementada aún");
            });
        }
    }

    private IEnumerator ChangeSceneSafely()
    {
        isChangingScene = true;
        
        if (menuDocument != null)
        {
            menuDocument.enabled = false;
            rootElement.visible = false;
            rootElement.RemoveFromHierarchy();
        }

        for (int i = 0; i < 3; i++)
        {
            yield return null;
        }

        System.GC.Collect();
        Resources.UnloadUnusedAssets();
        yield return null;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Game");
        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (rootElement != null)
        {
            rootElement.RemoveFromHierarchy();
        }
    }
}