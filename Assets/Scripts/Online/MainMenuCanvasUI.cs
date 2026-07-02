using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class MainMenuCanvasUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OnlineLobbyManager lobbyManager;
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_InputField joinCodeField;

    [Header("Buttons")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    [Header("Auto Find")]
    [SerializeField] private bool autoFindButtonsByName = true;
    [SerializeField] private bool autoFindJoinCodeFieldByName = true;
    [SerializeField] private string createButtonNameKeyword = "create";
    [SerializeField] private string joinButtonNameKeyword = "join";
    [SerializeField] private string joinCodeFieldNameKeyword = "code";

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
    }

    private void Start()
    {
        ResolveReferences();

        if (root != null)
        {
            root.SetActive(true);
        }

        SyncJoinCodeField();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void ResolveReferences()
    {
        if (lobbyManager == null)
        {
            lobbyManager = FindAnyObjectByType<OnlineLobbyManager>(FindObjectsInactive.Include);
        }

        if (autoFindButtonsByName)
        {
            if (createButton == null)
            {
                createButton = FindButtonByKeyword(createButtonNameKeyword);
            }

            if (joinButton == null)
            {
                joinButton = FindButtonByKeyword(joinButtonNameKeyword);
            }
        }

        if (autoFindJoinCodeFieldByName && joinCodeField == null)
        {
            joinCodeField = FindInputFieldByKeyword(joinCodeFieldNameKeyword);
        }
    }

    private void BindEvents()
    {
        if (createButton != null)
        {
            createButton.onClick.AddListener(OnCreateClicked);
        }

        if (joinButton != null)
        {
            joinButton.onClick.AddListener(OnJoinClicked);
        }
    }

    private void UnbindEvents()
    {
        if (createButton != null)
        {
            createButton.onClick.RemoveListener(OnCreateClicked);
        }

        if (joinButton != null)
        {
            joinButton.onClick.RemoveListener(OnJoinClicked);
        }
    }

    private void SyncJoinCodeField()
    {
        if (joinCodeField == null || lobbyManager == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(lobbyManager.joinLobbyCode) && joinCodeField.text != lobbyManager.joinLobbyCode)
        {
            joinCodeField.SetTextWithoutNotify(lobbyManager.joinLobbyCode);
        }
    }

    private void OnCreateClicked()
    {
        ResolveReferences();

        lobbyManager?.CreateLobbyAndHost();
        StartCoroutine(LoadLobbySceneWhenReady());
    }

    private void OnJoinClicked()
    {
        ResolveReferences();

        string lobbyCode = ReadJoinCode();
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            if (lobbyManager != null)
            {
                lobbyManager.statusMessage = "Enter a lobby code first";
            }

            return;
        }

        if (lobbyManager != null)
        {
            lobbyManager.SetJoinCodeInput(lobbyCode);
            lobbyManager.JoinLobbyByInputCode();
        }

        StartCoroutine(LoadLobbySceneWhenReady());
    }

    private IEnumerator LoadLobbySceneWhenReady()
    {
        const float timeoutSeconds = 15f;
        float startTime = Time.unscaledTime;

        while (lobbyManager != null && lobbyManager.CurrentLobby == null && Time.unscaledTime - startTime <= timeoutSeconds)
        {
            string message = lobbyManager.statusMessage != null ? lobbyManager.statusMessage.ToLowerInvariant() : string.Empty;
            if (message.Contains("failed") || message.Contains("please login first") || message.Contains("enter a lobby code first"))
            {
                yield break;
            }

            yield return null;
        }

        if (lobbyManager != null && lobbyManager.CurrentLobby != null)
        {
            LoadLobbyScene();
        }
    }

    private string ReadJoinCode()
    {
        if (joinCodeField != null)
        {
            return joinCodeField.text != null ? joinCodeField.text.Trim() : string.Empty;
        }

        if (lobbyManager != null)
        {
            return lobbyManager.joinLobbyCode != null ? lobbyManager.joinLobbyCode.Trim() : string.Empty;
        }

        return string.Empty;
    }

    private void LoadLobbyScene()
    {
        if (!string.IsNullOrWhiteSpace(lobbySceneName))
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }

    private static Button FindButtonByKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        string normalizedKeyword = keyword.Trim().ToLowerInvariant();

        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null)
            {
                continue;
            }

            string candidateName = candidate.name == null ? string.Empty : candidate.name.ToLowerInvariant();
            if (candidateName.Contains(normalizedKeyword))
            {
                return candidate;
            }
        }

        return null;
    }

    private static TMP_InputField FindInputFieldByKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        TMP_InputField[] inputFields = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include);
        string normalizedKeyword = keyword.Trim().ToLowerInvariant();

        for (int i = 0; i < inputFields.Length; i++)
        {
            TMP_InputField candidate = inputFields[i];
            if (candidate == null)
            {
                continue;
            }

            string candidateName = candidate.name == null ? string.Empty : candidate.name.ToLowerInvariant();
            if (candidateName.Contains(normalizedKeyword))
            {
                return candidate;
            }
        }

        return null;
    }
}