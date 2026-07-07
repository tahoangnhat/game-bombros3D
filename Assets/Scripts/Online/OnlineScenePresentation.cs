using UnityEngine;
using UnityEngine.EventSystems;
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
        EnforceSingleEventSystem();

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

    public static bool IsLobbySceneLoaded(string lobbySceneName)
    {
        if (string.IsNullOrWhiteSpace(lobbySceneName))
        {
            return false;
        }

        Scene lobbyScene = SceneManager.GetSceneByName(lobbySceneName);
        return (lobbyScene.IsValid() && lobbyScene.isLoaded)
            || GameObject.Find($"[{lobbySceneName}]") != null;
    }

    public static void FinalizeLobbySceneTransition(string lobbySceneName, string gameSceneName)
    {
        OnlineLobbyCanvasUI[] lobbyCanvases =
            Object.FindObjectsByType<OnlineLobbyCanvasUI>(FindObjectsInactive.Include);
        for (int i = 0; i < lobbyCanvases.Length; i++)
        {
            if (lobbyCanvases[i] != null)
            {
                lobbyCanvases[i].ShowForLobby();
            }
        }

        SetSceneCamerasEnabled(lobbySceneName, true);
        SetSceneCamerasEnabled(gameSceneName, false);
        SetSceneListenersEnabled(lobbySceneName, false);
        SetSceneListenersEnabled(gameSceneName, false);
        SetSceneEventSystemsEnabled(gameSceneName, false);
        SetSceneEventSystemsEnabled(lobbySceneName, true);
        EnforceSingleEventSystem();

        Scene lobbyScene = SceneManager.GetSceneByName(lobbySceneName);
        if (lobbyScene.IsValid() && lobbyScene.isLoaded)
        {
            SceneManager.SetActiveScene(lobbyScene);
        }
    }

    public static void EnforceSingleEventSystem()
    {
        EventSystem[] eventSystems =
            Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        EventSystem keeper = null;

        if (EventSystem.current != null
            && EventSystem.current.isActiveAndEnabled)
        {
            keeper = EventSystem.current;
        }

        for (int i = 0; i < eventSystems.Length && keeper == null; i++)
        {
            EventSystem candidate = eventSystems[i];
            if (candidate != null
                && candidate.gameObject.activeInHierarchy
                && candidate.enabled)
            {
                keeper = candidate;
            }
        }

        for (int i = 0; i < eventSystems.Length && keeper == null; i++)
        {
            EventSystem candidate = eventSystems[i];
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                keeper = candidate;
            }
        }

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem != null)
            {
                eventSystem.enabled = eventSystem == keeper;
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
                // AudioControlsOverlay owns the single persistent listener.
                gameListeners[i].enabled = false;
            }
        }

        SetSceneEventSystemsEnabled(gameSceneName, true);
    }

    private static void SetSceneCamerasEnabled(string sceneName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null
                && string.Equals(camera.gameObject.scene.name, sceneName, System.StringComparison.Ordinal))
            {
                camera.enabled = enabled;
            }
        }

        GameObject fusionRoot = GameObject.Find($"[{sceneName}]");
        if (fusionRoot != null)
        {
            Camera[] wrappedCameras = fusionRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < wrappedCameras.Length; i++)
            {
                if (wrappedCameras[i] != null)
                {
                    wrappedCameras[i].enabled = enabled;
                }
            }
        }
    }

    private static void SetSceneListenersEnabled(string sceneName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        AudioListener[] listeners =
            Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null
                && string.Equals(listener.gameObject.scene.name, sceneName, System.StringComparison.Ordinal))
            {
                listener.enabled = enabled;
            }
        }

        GameObject fusionRoot = GameObject.Find($"[{sceneName}]");
        if (fusionRoot != null)
        {
            AudioListener[] wrappedListeners =
                fusionRoot.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < wrappedListeners.Length; i++)
            {
                if (wrappedListeners[i] != null)
                {
                    wrappedListeners[i].enabled = enabled;
                }
            }
        }
    }

    private static void SetSceneEventSystemsEnabled(string sceneName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        EventSystem[] eventSystems =
            Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem != null
                && string.Equals(
                    eventSystem.gameObject.scene.name,
                    sceneName,
                    System.StringComparison.Ordinal))
            {
                eventSystem.enabled = enabled;
            }
        }

        GameObject fusionRoot = GameObject.Find($"[{sceneName}]");
        if (fusionRoot != null)
        {
            EventSystem[] wrappedEventSystems =
                fusionRoot.GetComponentsInChildren<EventSystem>(true);
            for (int i = 0; i < wrappedEventSystems.Length; i++)
            {
                if (wrappedEventSystems[i] != null)
                {
                    wrappedEventSystems[i].enabled = enabled;
                }
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

        SetSceneEventSystemsEnabled(lobbySceneToHide, false);

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
