using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuLogoutButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button logoutButton;
    [SerializeField] private SpringAuthClient authClient;
    [SerializeField] private OnlineLobbyManager lobbyManager;

    [Header("Navigation")]
    [SerializeField] private string loginSceneName = "LoginScene";

    [Header("Auto Resolve")]
    [SerializeField] private bool autoFindLogoutButtonByName = true;
    [SerializeField] private string logoutButtonNameKeyword = "logout";

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void ResolveReferences()
    {
        if (logoutButton == null)
        {
            logoutButton = GetComponent<Button>();
        }

        if (logoutButton == null && autoFindLogoutButtonByName)
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            string keyword = string.IsNullOrWhiteSpace(logoutButtonNameKeyword) ? "logout" : logoutButtonNameKeyword.Trim().ToLowerInvariant();

            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate == null)
                {
                    continue;
                }

                string candidateName = candidate.name == null ? string.Empty : candidate.name.ToLowerInvariant();
                if (candidateName.Contains(keyword))
                {
                    logoutButton = candidate;
                    break;
                }
            }
        }

        if (authClient == null)
        {
            authClient = FindAnyObjectByType<SpringAuthClient>();
        }

        if (lobbyManager == null)
        {
            lobbyManager = FindAnyObjectByType<OnlineLobbyManager>();
        }
    }

    private void BindEvents()
    {
        if (logoutButton == null)
        {
            return;
        }

        logoutButton.onClick.RemoveListener(OnLogoutClicked);
        logoutButton.onClick.AddListener(OnLogoutClicked);
    }

    private void UnbindEvents()
    {
        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveListener(OnLogoutClicked);
        }
    }

    private void OnLogoutClicked()
    {
        if (authClient != null)
        {
            authClient.Logout();
        }

        if (lobbyManager != null)
        {
            lobbyManager.Logout();
        }

        SpringAuthSession.Clear();

        if (!string.IsNullOrWhiteSpace(loginSceneName))
        {
            SceneManager.LoadScene(loginSceneName);
        }
    }
}