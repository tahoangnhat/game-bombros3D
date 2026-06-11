using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnlineMatchResultPopup : MonoBehaviour
{
    [Header("Popup References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Refresh")]
    [SerializeField] private float checkInterval = 0.2f;

    private OnlineLobbyManager lobbyManager;
    private float checkTimer;
    private int observedPlayerCount;
    private bool localLoseShown;
    private bool localWinShown;
    private bool drawShown;

    private void Awake()
    {
        BindButtons();
        HidePopup();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    private void Update()
    {
        ResolveManager();

        checkTimer += Time.unscaledDeltaTime;
        if (checkTimer < checkInterval)
        {
            return;
        }

        checkTimer = 0f;
        CheckMatchState();
    }

    public void HidePopup()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    public void QuitMatch()
    {
        ResolveManager();

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

    private void ResolveManager()
    {
        if (lobbyManager == null)
        {
            lobbyManager = OnlineLobbyManager.Instance != null
                ? OnlineLobbyManager.Instance
                : FindAnyObjectByType<OnlineLobbyManager>();
        }
    }

    private void CheckMatchState()
    {
        OnlinePlayerHealth[] players = FindObjectsByType<OnlinePlayerHealth>(FindObjectsInactive.Include);
        if (players == null || players.Length == 0)
        {
            return;
        }

        observedPlayerCount = Mathf.Max(observedPlayerCount, players.Length);
        int expectedPlayers = GetExpectedPlayerCount();
        if (expectedPlayers > 0)
        {
            observedPlayerCount = Mathf.Max(observedPlayerCount, expectedPlayers);
        }

        OnlinePlayerHealth localPlayer = null;
        OnlinePlayerHealth alivePlayer = null;
        int aliveCount = 0;

        for (int i = 0; i < players.Length; i++)
        {
            OnlinePlayerHealth player = players[i];
            if (player == null || player.Object == null || !player.Object.IsValid)
            {
                continue;
            }

            if (player.Object.HasInputAuthority)
            {
                localPlayer = player;
            }

            if (player.IsAlive)
            {
                aliveCount++;
                alivePlayer = player;
            }
        }

        if (localPlayer != null && localPlayer.IsEliminated && !localLoseShown)
        {
            localLoseShown = true;
            ShowPopup(
                "Bạn đã thua",
                "Bạn có thể xem tiếp trận đấu hoặc thoát về menu.",
                showContinue: true);
            return;
        }

        if (observedPlayerCount < 2)
        {
            return;
        }

        if (aliveCount == 1 && alivePlayer != null && alivePlayer.Object.HasInputAuthority && !localWinShown)
        {
            localWinShown = true;
            ShowPopup(
                "Bạn thắng",
                "Bạn là người sống cuối cùng.",
                showContinue: false);
            return;
        }

        if (aliveCount == 0 && !drawShown)
        {
            drawShown = true;
            ShowPopup(
                "Hòa",
                "Không còn người chơi nào sống sót.",
                showContinue: false);
        }
    }

    private int GetExpectedPlayerCount()
    {
        Lobby lobby = lobbyManager != null ? lobbyManager.CurrentLobby : null;
        return lobby != null && lobby.Players != null ? lobby.Players.Count : 0;
    }

    private void ShowPopup(string title, string message, bool showContinue)
    {
        if (popupPanel == null)
        {
            Debug.LogError("[Match Result] Popup Panel is not assigned.");
            return;
        }

        if (titleText != null)
        {
            titleText.text = title;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(showContinue);
        }

        popupPanel.SetActive(true);
    }

    private void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(HidePopup);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitMatch);
        }
    }

    private void UnbindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(HidePopup);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitMatch);
        }
    }
}
