using UnityEngine;
using UnityEngine.SceneManagement;

public static class OnlineScenePresentation
{
    public static bool IsGameSceneLoaded(string gameSceneName)
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            return false;
        }

        Scene gameScene = SceneManager.GetSceneByName(gameSceneName);
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            return true;
        }

        return FindGameSceneRootTransform(gameSceneName) != null || ThemeManager.Instance != null;
    }

    public static void FinalizeGameSceneTransition(string gameSceneName, string lobbySceneName)
    {
        HideLobbyPresentation(lobbySceneName);
        EnableGameScenePresentation(gameSceneName);

        Scene gameScene = SceneManager.GetSceneByName(gameSceneName);
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.SetActiveScene(gameScene);
        }

        if (!string.IsNullOrWhiteSpace(lobbySceneName)
            && !string.Equals(lobbySceneName, gameSceneName, System.StringComparison.Ordinal))
        {
            Scene lobbyScene = SceneManager.GetSceneByName(lobbySceneName);
            if (lobbyScene.IsValid() && lobbyScene.isLoaded)
            {
                _ = SceneManager.UnloadSceneAsync(lobbyScene);
            }
        }
    }

    private static Transform FindGameSceneRootTransform(string gameSceneName)
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            return null;
        }

        GameObject fusionRoot = GameObject.Find($"[{gameSceneName}]");
        if (fusionRoot != null)
        {
            return fusionRoot.transform;
        }

        return ThemeManager.Instance != null ? ThemeManager.Instance.transform.root : null;
    }

    private static void EnableGameScenePresentation(string gameSceneName)
    {
        Transform gameRoot = FindGameSceneRootTransform(gameSceneName);
        if (gameRoot == null)
        {
            return;
        }

        Camera[] gameCameras = gameRoot.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < gameCameras.Length; i++)
        {
            if (gameCameras[i] != null)
            {
                gameCameras[i].enabled = true;
            }
        }

        AudioListener[] gameListeners = gameRoot.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < gameListeners.Length; i++)
        {
            if (gameListeners[i] != null)
            {
                gameListeners[i].enabled = true;
            }
        }
    }

    private static void HideLobbyPresentation(string lobbySceneToHide)
    {
        OnlineLobbyCanvasUI[] lobbyCanvases = Object.FindObjectsByType<OnlineLobbyCanvasUI>(FindObjectsInactive.Include);
        for (int i = 0; i < lobbyCanvases.Length; i++)
        {
            OnlineLobbyCanvasUI lobbyCanvas = lobbyCanvases[i];
            if (lobbyCanvas != null)
            {
                lobbyCanvas.HideForMatch();
            }
        }

        GameObject lobbyCanvasObject = GameObject.Find("LobbyCanvas");
        if (lobbyCanvasObject != null)
        {
            lobbyCanvasObject.SetActive(false);
        }

        if (string.IsNullOrWhiteSpace(lobbySceneToHide))
        {
            return;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            if (string.Equals(camera.gameObject.scene.name, lobbySceneToHide, System.StringComparison.Ordinal))
            {
                camera.enabled = false;
            }
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
            {
                continue;
            }

            if (string.Equals(listener.gameObject.scene.name, lobbySceneToHide, System.StringComparison.Ordinal))
            {
                listener.enabled = false;
            }
        }
    }
}
