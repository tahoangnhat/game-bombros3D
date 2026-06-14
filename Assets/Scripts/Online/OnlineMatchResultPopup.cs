using System.Collections;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnlineMatchResultPopup : MonoBehaviour
{
    public enum MatchResultType { Win, Lose, Draw }

    [Header("Popup References (Original)")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Refresh")]
    [SerializeField] private float checkInterval = 0.2f;

    [Header("Visual Customizations (Optional)")]
    [SerializeField] private Image panelBgImage;         
    [SerializeField] private Image statusIconImage;       
    [SerializeField] private Sprite victoryIcon;          
    [SerializeField] private Sprite defeatIcon;           
    [SerializeField] private Sprite drawIcon;             

    [Header("Style Colors")]
    [SerializeField] private Color victoryPanelColor = new Color(0.12f, 0.22f, 0.14f, 0.95f);
    [SerializeField] private Color defeatPanelColor = new Color(0.25f, 0.12f, 0.12f, 0.95f);
    [SerializeField] private Color drawPanelColor = new Color(0.16f, 0.16f, 0.20f, 0.95f);

    [Header("Text Gradients")]
    [SerializeField] private TMP_ColorGradient victoryTextGradient;
    [SerializeField] private TMP_ColorGradient defeatTextGradient;
    [SerializeField] private TMP_ColorGradient drawTextGradient;

    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.35f;

    private OnlineLobbyManager lobbyManager;
    private float checkTimer;
    private int observedPlayerCount;
    private bool localLoseShown;
    private bool localWinShown;
    private bool drawShown;
    private bool spectatorEndShown;
    private Coroutine activeAnimationRoutine;

    private void Awake()
    {
        BindButtons();
        HidePopupImmediately();
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

    private void HidePopupImmediately()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
            popupPanel.transform.localScale = Vector3.zero;
            CanvasGroup cg = popupPanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }
        spectatorEndShown = false;
        localLoseShown = false;
        localWinShown = false;
        drawShown = false;
    }

    public void HidePopup()
    {
        if (activeAnimationRoutine != null) StopCoroutine(activeAnimationRoutine);
        activeAnimationRoutine = StartCoroutine(AnimateClose());
    }

    public void QuitMatch()
    {
        ResolveManager();

        if (lobbyManager != null)
        {
            if (lobbyManager.IsHostLobby)
            {
                // Host đưa tất cả mọi người quay lại sảnh chờ cùng nhau thông qua NetworkRunner
                lobbyManager.ReturnToLobby();
                return;
            }
            else
            {
                // Client thoát khỏi session phòng hiện tại và quay về màn hình chính
                lobbyManager.LeaveSessionAndGoToMainMenu(mainMenuSceneName);
                return;
            }
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

        // 1. Người chơi chết (You Lose)
        if (localPlayer != null && localPlayer.IsEliminated && !localLoseShown)
        {
            localLoseShown = true;
            ShowPopup(
                "You Lose",
                "Continute Watching or Exit",
                showContinue: true,
                MatchResultType.Lose);
            return;
        }

        if (observedPlayerCount < 2)
        {
            return;
        }

        // 2. Người chơi thắng (You Win)
        if (aliveCount == 1 && alivePlayer != null && alivePlayer.Object.HasInputAuthority && !localWinShown)
        {
            localWinShown = true;
            ShowPopup(
                "You Win",
                "You are the Winner",
                showContinue: false,
                MatchResultType.Win);
            return;
        }

        // 3. Trận đấu hòa (Tie)
        if (aliveCount == 0 && !drawShown)
        {
            drawShown = true;
            spectatorEndShown = true; // Trận đấu kết thúc hoàn toàn
            ShowPopup(
                "Tie",
                "No survivors left",
                showContinue: false,
                MatchResultType.Draw);
            return;
        }

        // 4. Nếu người chơi đang theo dõi (Spectating) và có người chiến thắng cuối cùng
        if (localLoseShown && aliveCount == 1 && !spectatorEndShown)
        {
            spectatorEndShown = true;
            ShowPopup(
                "Match Finished",
                "Winner has been decided!",
                showContinue: false, // Trận đấu kết thúc, không xem tiếp nữa
                MatchResultType.Draw); // Trực quan trung tính
        }
    }

    private int GetExpectedPlayerCount()
    {
        Lobby lobby = lobbyManager != null ? lobbyManager.CurrentLobby : null;
        return lobby != null && lobby.Players != null ? lobby.Players.Count : 0;
    }

    private void ShowPopup(string title, string message, bool showContinue, MatchResultType resultType)
    {
        if (popupPanel == null)
        {
            Debug.LogError("[Match Result] Popup Panel is not assigned.");
            return;
        }

        if (titleText != null)
        {
            titleText.text = title;
            titleText.enableVertexGradient = true;
            if (resultType == MatchResultType.Win && victoryTextGradient != null)
                titleText.colorGradientPreset = victoryTextGradient;
            else if (resultType == MatchResultType.Lose && defeatTextGradient != null)
                titleText.colorGradientPreset = defeatTextGradient;
            else if (resultType == MatchResultType.Draw && drawTextGradient != null)
                titleText.colorGradientPreset = drawTextGradient;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (panelBgImage != null)
        {
            if (resultType == MatchResultType.Win) panelBgImage.color = victoryPanelColor;
            else if (resultType == MatchResultType.Lose) panelBgImage.color = defeatPanelColor;
            else panelBgImage.color = drawPanelColor;
        }

        if (statusIconImage != null)
        {
            Sprite targetIcon = null;
            if (resultType == MatchResultType.Win) targetIcon = victoryIcon;
            else if (resultType == MatchResultType.Lose) targetIcon = defeatIcon;
            else targetIcon = drawIcon;

            if (targetIcon != null)
            {
                statusIconImage.sprite = targetIcon;
                statusIconImage.gameObject.SetActive(true);
            }
            else
            {
                statusIconImage.gameObject.SetActive(false);
            }
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(showContinue);
            TMP_Text cButtonText = continueButton.GetComponentInChildren<TMP_Text>();
            if (cButtonText != null)
            {
                cButtonText.text = "Watch";
            }
            else
            {
                Text cLegacyText = continueButton.GetComponentInChildren<Text>();
                if (cLegacyText != null)
                {
                    cLegacyText.text = "Watch";
                }
            }
        }

        if (quitButton != null)
        {
            TMP_Text buttonText = quitButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = (lobbyManager != null && lobbyManager.IsHostLobby) ? "Return" : "Exit";
            }
            else
            {
                Text legacyText = quitButton.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = (lobbyManager != null && lobbyManager.IsHostLobby) ? "Return" : "Exit";
                }
            }
        }

        popupPanel.SetActive(true);
        if (activeAnimationRoutine != null) StopCoroutine(activeAnimationRoutine);
        activeAnimationRoutine = StartCoroutine(AnimateOpen());
    }

    private IEnumerator AnimateOpen()
    {
        float elapsed = 0f;
        CanvasGroup cg = popupPanel.GetComponent<CanvasGroup>();
        
        popupPanel.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        if (cg != null) cg.alpha = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = Mathf.Clamp01(elapsed / animationDuration);
            
            float t = percent;
            float scaleVal = 1f + Mathf.Sin(t * Mathf.PI * 1.5f) * 0.15f * (1f - t);
            if (percent >= 0.99f) scaleVal = 1f;

            popupPanel.transform.localScale = new Vector3(scaleVal, scaleVal, 1f);
            if (cg != null) cg.alpha = percent;

            yield return null;
        }

        popupPanel.transform.localScale = Vector3.one;
        if (cg != null) cg.alpha = 1f;
    }

    private IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        float duration = animationDuration * 0.6f;
        CanvasGroup cg = popupPanel.GetComponent<CanvasGroup>();
        Vector3 startScale = popupPanel.transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);
            float inversePercent = 1f - percent;

            popupPanel.transform.localScale = Vector3.Lerp(startScale, new Vector3(0.5f, 0.5f, 1f), percent);
            if (cg != null) cg.alpha = inversePercent;

            yield return null;
        }

        popupPanel.SetActive(false);
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
