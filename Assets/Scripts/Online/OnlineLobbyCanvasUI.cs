using UnityEngine;
using UnityEngine.UI;

public class OnlineLobbyCanvasUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OnlineLobbyManager lobbyManager;
    [SerializeField] private GameObject root;

    [Header("Texts")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text signedInText;
    [SerializeField] private Text rankText;
    [SerializeField] private Text lobbyInfoText;
    [SerializeField] private Text playersText;
    [SerializeField] private Text readyButtonText;

    [Header("Inputs")]
    [SerializeField] private InputField joinCodeField;

    [Header("Buttons")]
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.2f;

    private float refreshTimer;

    private void Awake()
    {
        BindEvents();
    }

    private void Start()
    {
        ResolveManager();
        RefreshUI();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void Update()
    {
        ResolveManager();

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            RefreshUI();
        }
    }

    private void ResolveManager()
    {
        if (lobbyManager == null)
        {
            lobbyManager = FindAnyObjectByType<OnlineLobbyManager>();
        }
    }

    private void BindEvents()
    {
        if (joinCodeField != null)
        {
            joinCodeField.onValueChanged.AddListener(OnJoinCodeChanged);
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(OnLogoutClicked);
        }

        if (createButton != null)
        {
            createButton.onClick.AddListener(OnCreateHostClicked);
        }

        if (joinButton != null)
        {
            joinButton.onClick.AddListener(OnJoinByCodeClicked);
        }

        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(OnQuickJoinClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }
    }

    private void UnbindEvents()
    {
        if (joinCodeField != null)
        {
            joinCodeField.onValueChanged.RemoveListener(OnJoinCodeChanged);
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveListener(OnLogoutClicked);
        }

        if (createButton != null)
        {
            createButton.onClick.RemoveListener(OnCreateHostClicked);
        }

        if (joinButton != null)
        {
            joinButton.onClick.RemoveListener(OnJoinByCodeClicked);
        }

        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.RemoveListener(OnQuickJoinClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyClicked);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
        }
    }

    private void RefreshUI()
    {
        bool signedIn = lobbyManager != null && lobbyManager.IsSignedIn;
        if (root != null)
        {
            root.SetActive(signedIn);
        }

        if (lobbyManager == null)
        {
            SetText(statusText, "Status: Waiting for OnlineLobbyManager...");
            SetText(signedInText, "Signed in: No");
            SetText(rankText, "Rank: -");
            SetText(lobbyInfoText, "Lobby: -");
            SetText(playersText, "Players\nNo lobby yet.");
            SetButtonsInteractable(false, false, false, false, false, false, false);
            return;
        }

        SetText(statusText, "Status: " + lobbyManager.statusMessage);
        SetText(signedInText, "Signed in: " + (lobbyManager.IsSignedIn ? "Yes" : "No"));
        SetText(rankText, "Rank: " + lobbyManager.CurrentRankTier + " (" + lobbyManager.CurrentMmr + ")");

        if (lobbyManager.CurrentLobby != null)
        {
            int count = lobbyManager.CurrentLobby.Players != null ? lobbyManager.CurrentLobby.Players.Count : 0;
            SetText(lobbyInfoText, "Lobby: " + lobbyManager.CurrentLobby.Name +
                                  " | Code: " + lobbyManager.CurrentLobby.LobbyCode +
                                  " | Players: " + count + "/" + lobbyManager.maxPlayers);
            SetText(playersText, "Players\n" + lobbyManager.GetLobbyPlayersSummary());
        }
        else
        {
            SetText(lobbyInfoText, "Lobby: -");
            SetText(playersText, "Players\nNo lobby yet.");
        }

        bool canLogout = lobbyManager.ServicesReady && lobbyManager.IsSignedIn;
        bool canSessionControl = lobbyManager.CanUseLobbyActions && lobbyManager.CurrentLobby == null;
        bool canReady = lobbyManager.CanToggleReady;
        bool canStart = lobbyManager.CanStartGame;
        bool canLeave = lobbyManager.CurrentLobby != null || lobbyManager.IsHostLobby;

        SetButtonsInteractable(canLogout, canSessionControl, canSessionControl, canSessionControl, canReady, canStart, canLeave);

        if (readyButtonText != null)
        {
            readyButtonText.text = lobbyManager.IsLocalPlayerReady() ? "Set Not Ready" : "Set Ready";
        }
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private void SetButtonsInteractable(bool logout, bool create, bool join, bool quickJoin, bool ready, bool start, bool leave)
    {
        if (logoutButton != null) logoutButton.interactable = logout;
        if (createButton != null) createButton.interactable = create;
        if (joinButton != null) joinButton.interactable = join;
        if (quickJoinButton != null) quickJoinButton.interactable = quickJoin;
        if (readyButton != null) readyButton.interactable = ready;
        if (startButton != null) startButton.interactable = start;
        if (leaveButton != null) leaveButton.interactable = leave;
    }

    private void OnJoinCodeChanged(string value)
    {
        lobbyManager?.SetJoinCodeInput(value);
    }

    private void OnLogoutClicked()
    {
        lobbyManager?.Logout();
    }

    private void OnCreateHostClicked()
    {
        lobbyManager?.CreateLobbyAndHost();
    }

    private void OnJoinByCodeClicked()
    {
        lobbyManager?.JoinLobbyByInputCode();
    }

    private void OnQuickJoinClicked()
    {
        lobbyManager?.QuickJoin();
    }

    private void OnReadyClicked()
    {
        lobbyManager?.ToggleReady();
    }

    private void OnStartClicked()
    {
        lobbyManager?.StartGame();
    }

    private void OnLeaveClicked()
    {
        lobbyManager?.LeaveSession();
    }
}
