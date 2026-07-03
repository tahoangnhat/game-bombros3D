using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class OnlineLobbyCanvasUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OnlineLobbyManager lobbyManager;
    [SerializeField] private GameObject root;

    [Header("Texts")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text playerSlot1Text;
    [SerializeField] private TMP_Text playerSlot2Text;
    [SerializeField] private TMP_Text playerSlot3Text;
    [SerializeField] private TMP_Text playerSlot4Text;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private TMP_Text startButtonText;

    [Header("Buttons")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

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
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }
    }

    private void UnbindEvents()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyClicked);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackClicked);
        }
    }

    public void HideForMatch()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void RefreshUI()
    {
        bool hasManager = lobbyManager != null;
        if (hasManager && lobbyManager.IsMatchActive)
        {
            HideForMatch();
            return;
        }

        bool inLobby = hasManager && lobbyManager.CurrentLobby != null;
        bool isHost = hasManager && lobbyManager.IsHostLobby;

        if (root != null)
        {
            root.SetActive(true);
        }

        SetText(lobbyCodeText, inLobby ? "Code phòng: " + lobbyManager.CurrentLobbyCode : "Code phòng: -");
        SetText(playerSlot1Text, GetSlotText(0));
        SetText(playerSlot2Text, GetSlotText(1));
        SetText(playerSlot3Text, GetSlotText(2));
        SetText(playerSlot4Text, GetSlotText(3));

        bool canReady = hasManager && inLobby && !isHost && lobbyManager.CanToggleReady;
        bool canStart = hasManager && inLobby && isHost && lobbyManager.CanStartGame;

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(inLobby && !isHost);
            readyButton.interactable = canReady;
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(inLobby && isHost);
            startButton.interactable = canStart;
        }

        if (backButton != null)
        {
            backButton.interactable = true;
        }

        if (readyButtonText != null)
        {
            readyButtonText.text = hasManager && lobbyManager.IsLocalPlayerReady() ? "Cancel" : "Ready";
        }

        if (startButtonText != null)
        {
            startButtonText.text = "Start";
        }
    }

    private string GetSlotText(int slotIndex)
    {
        if (lobbyManager == null)
        {
            return "Slot " + (slotIndex + 1) + ": -";
        }

        return lobbyManager.GetLobbyPlayerSlotText(slotIndex);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private void OnReadyClicked()
    {
        lobbyManager?.ToggleReady();
    }

    private void OnStartClicked()
    {
        lobbyManager?.StartGame();
    }

    private void OnBackClicked()
    {
        if (lobbyManager != null)
        {
            lobbyManager.LeaveSessionAndGoToMainMenu(mainMenuSceneName);
            return;
        }

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
