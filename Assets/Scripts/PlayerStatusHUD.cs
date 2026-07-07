using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusHUD : MonoBehaviour
{
    [System.Serializable]
    public class PlayerCardUI
    {
        [Header("Root Card GameObject")]
        public GameObject cardParent;

        [Header("UI Fields")]
        public TextMeshProUGUI playerNameText;
        public Image shieldLight;
        public Image bombLight;
        public TextMeshProUGUI rangeText;
        public TextMeshProUGUI speedCountdownText;
        public GameObject eliminatedOverlay;

        [HideInInspector]
        public bool isAssigned;
    }

    [Header("Personal Player Card")]
    public PlayerCardUI[] playerCards = new PlayerCardUI[4];

    [Header("Player Roster")]
    [SerializeField] private Vector2 rosterSize = new Vector2(330f, 190f);
    [SerializeField] private Vector2 rosterPosition = new Vector2(-24f, -86f);
    [SerializeField, Min(0.05f)] private float rosterRefreshInterval = 0.2f;

    private PlayerController localPlayer;
    private OnlinePlayerController localOnlinePlayer;
    private TextMeshProUGUI rosterText;
    private TextMeshProUGUI healthValueText;
    private TextMeshProUGUI bombValueText;
    private RectTransform rosterPanelRect;
    private bool healthIconConverted;
    private bool personalCardStyled;
    private float rosterRefreshTimer;

    private void Start()
    {
        for (int i = 0; i < playerCards.Length; i++)
        {
            PlayerCardUI card = playerCards[i];
            if (card == null || card.cardParent == null)
            {
                continue;
            }

            card.cardParent.SetActive(false);
            card.isAssigned = false;
        }

        CreateRosterPanel();
    }

    private void Update()
    {
        ResolvePersonalPlayer();
        UpdatePersonalCard();

        rosterRefreshTimer += Time.unscaledDeltaTime;
        if (rosterRefreshTimer >= rosterRefreshInterval)
        {
            rosterRefreshTimer = 0f;
            UpdateRoster();
        }
    }

    private void ResolvePersonalPlayer()
    {
        if (OnlineSessionState.IsOnlineSession)
        {
            localPlayer = null;
            if (localOnlinePlayer != null
                && localOnlinePlayer.Object != null
                && localOnlinePlayer.Object.IsValid
                && localOnlinePlayer.Object.HasInputAuthority)
            {
                return;
            }

            localOnlinePlayer = null;
            OnlinePlayerController[] onlinePlayers =
                FindObjectsByType<OnlinePlayerController>(FindObjectsInactive.Include);
            for (int i = 0; i < onlinePlayers.Length; i++)
            {
                OnlinePlayerController player = onlinePlayers[i];
                if (player != null
                    && player.Object != null
                    && player.Object.IsValid
                    && player.Object.HasInputAuthority)
                {
                    localOnlinePlayer = player;
                    return;
                }
            }

            return;
        }

        localOnlinePlayer = null;
        if (localPlayer == null)
        {
            localPlayer = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        }
    }

    private void UpdatePersonalCard()
    {
        for (int i = 1; i < playerCards.Length; i++)
        {
            PlayerCardUI extraCard = playerCards[i];
            if (extraCard != null && extraCard.cardParent != null)
            {
                extraCard.cardParent.SetActive(false);
                extraCard.isAssigned = false;
            }
        }

        if (playerCards.Length == 0 || playerCards[0] == null)
        {
            return;
        }

        PlayerCardUI card = playerCards[0];
        StylePersonalCard(card);
        bool hasPersonalPlayer = localOnlinePlayer != null || localPlayer != null;
        card.isAssigned = hasPersonalPlayer;
        if (card.cardParent != null)
        {
            card.cardParent.SetActive(hasPersonalPlayer);
        }

        if (!hasPersonalPlayer)
        {
            return;
        }

        bool isDead;
        int currentHealth;
        int maxHealth;
        int maxBombs;
        int currentRange;
        float speedMultiplier;
        string displayName;

        if (localOnlinePlayer != null)
        {
            OnlinePlayerHealth health = localOnlinePlayer.GetComponent<OnlinePlayerHealth>();
            isDead = health != null && health.IsEliminated;
            currentHealth = health != null ? health.CurrentHealth : 0;
            maxHealth = health != null ? health.MaxHealth : 0;
            maxBombs = localOnlinePlayer.MaxActiveBombs;
            currentRange = localOnlinePlayer.CurrentBombRange;
            speedMultiplier = Mathf.Max(1f, localOnlinePlayer.SpeedMultiplier);
            displayName = localOnlinePlayer.Nickname.ToString();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = localOnlinePlayer.name;
            }
        }
        else
        {
            PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
            isDead = health == null || !health.IsAlive;
            currentHealth = health != null ? health.CurrentHealth : 0;
            maxHealth = health != null ? health.MaxHealth : 0;
            maxBombs = localPlayer.MaxActiveBombs;
            currentRange = localPlayer.CurrentBombRange;
            speedMultiplier = localPlayer.SpeedMultiplier;
            displayName = localPlayer.name;
        }

        if (card.playerNameText != null)
        {
            card.playerNameText.text = "YOU  |  " + displayName.ToUpperInvariant();
        }
        if (card.eliminatedOverlay != null)
        {
            card.eliminatedOverlay.SetActive(isDead);
        }

        Color activeGreen = new Color(0.2f, 0.8f, 0.2f, 1f);
        Color inactiveGrey = new Color(0.3f, 0.32f, 0.35f, 1f);
        if (card.shieldLight != null)
        {
            card.shieldLight.gameObject.name = "HealthStatus";
            float healthRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            card.shieldLight.color = Color.Lerp(
                new Color(0.85f, 0.15f, 0.2f, 1f),
                activeGreen,
                healthRatio);
        }
        if (card.bombLight != null)
        {
            card.bombLight.color = maxBombs > 1 ? activeGreen : inactiveGrey;
        }

        EnsureStatValueTexts(card);
        if (healthValueText != null)
        {
            healthValueText.text =
                Mathf.Max(0, currentHealth) + "/" + Mathf.Max(1, maxHealth);
        }
        if (bombValueText != null)
        {
            bombValueText.text = "x" + Mathf.Max(1, maxBombs);
        }
        if (card.rangeText != null)
        {
            card.rangeText.text = "R " + Mathf.Max(1, currentRange);
        }
        if (card.speedCountdownText != null)
        {
            card.speedCountdownText.gameObject.name = "SpeedMultiplierText";
            card.speedCountdownText.text = "SPD x" + speedMultiplier.ToString("0.##");
            card.speedCountdownText.color =
                speedMultiplier > 1f ? activeGreen : inactiveGrey;
        }
    }

    private void StylePersonalCard(PlayerCardUI card)
    {
        if (personalCardStyled || card == null || card.cardParent == null)
        {
            return;
        }

        card.cardParent.name = "LocalPlayerHUD";
        Image background = card.cardParent.GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.025f, 0.035f, 0.055f, 0.92f);
            background.raycastTarget = false;
        }

        Outline outline = card.cardParent.GetComponent<Outline>();
        if (outline == null)
        {
            outline = card.cardParent.AddComponent<Outline>();
        }
        outline.effectColor = new Color(0.95f, 0.58f, 0.12f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        Shadow shadow = card.cardParent.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(5f, -5f);
        shadow.useGraphicAlpha = true;

        if (card.playerNameText != null)
        {
            card.playerNameText.color = new Color(1f, 0.72f, 0.27f, 1f);
            card.playerNameText.fontStyle = FontStyles.Bold;
            card.playerNameText.fontSize = 22f;
        }
        if (card.rangeText != null)
        {
            card.rangeText.color = new Color(0.45f, 0.82f, 1f, 1f);
            card.rangeText.fontStyle = FontStyles.Bold;
        }
        if (card.speedCountdownText != null)
        {
            card.speedCountdownText.fontStyle = FontStyles.Bold;
        }

        personalCardStyled = true;
    }

    private void EnsureStatValueTexts(PlayerCardUI card)
    {
        ConvertShieldIconToHealth(card);

        if (healthValueText == null && card.shieldLight != null)
        {
            healthValueText = CreateStatValueText(
                card.shieldLight.transform,
                "HealthValueText",
                card);
        }

        if (bombValueText == null && card.bombLight != null)
        {
            bombValueText = CreateStatValueText(
                card.bombLight.transform,
                "BombValueText",
                card);
        }
    }

    private void ConvertShieldIconToHealth(PlayerCardUI card)
    {
        if (healthIconConverted || card.shieldLight == null)
        {
            return;
        }

        Transform iconTransform = card.shieldLight.transform.parent.Find("ShieldIcon");
        if (iconTransform != null)
        {
            iconTransform.name = "HealthIcon";
            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = new Color(0.85f, 0.15f, 0.35f, 1f);
            }

            TextMeshProUGUI label = CreateStatValueText(
                iconTransform,
                "HealthLabel",
                card);
            label.text = "HP";
            label.fontSize = 14f;
        }

        healthIconConverted = true;
    }

    private static TextMeshProUGUI CreateStatValueText(
        Transform parent,
        string objectName,
        PlayerCardUI card)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 18f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        if (card.playerNameText != null)
        {
            text.font = card.playerNameText.font;
        }

        return text;
    }

    private void CreateRosterPanel()
    {
        if (rosterText != null)
        {
            return;
        }

        Canvas canvas = null;
        if (playerCards.Length > 0
            && playerCards[0] != null
            && playerCards[0].cardParent != null)
        {
            canvas = playerCards[0].cardParent.GetComponentInParent<Canvas>(true);
        }
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }
        if (canvas == null)
        {
            return;
        }

        GameObject panelObject = new GameObject(
            "PlayerRosterPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);

        rosterPanelRect = panelObject.GetComponent<RectTransform>();
        rosterPanelRect.anchorMin = Vector2.one;
        rosterPanelRect.anchorMax = Vector2.one;
        rosterPanelRect.pivot = Vector2.one;
        rosterPanelRect.anchoredPosition = rosterPosition;
        rosterPanelRect.sizeDelta = rosterSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.035f, 0.055f, 0.88f);
        panelImage.raycastTarget = false;

        Outline panelOutline = panelObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.95f, 0.58f, 0.12f, 0.85f);
        panelOutline.effectDistance = new Vector2(2f, -2f);
        panelOutline.useGraphicAlpha = true;

        Shadow panelShadow = panelObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        panelShadow.effectDistance = new Vector2(5f, -5f);
        panelShadow.useGraphicAlpha = true;

        GameObject textObject = new GameObject(
            "RosterText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 12f);
        textRect.offsetMax = new Vector2(-18f, -12f);

        rosterText = textObject.GetComponent<TextMeshProUGUI>();
        rosterText.fontSize = 20f;
        rosterText.color = Color.white;
        rosterText.alignment = TextAlignmentOptions.TopLeft;
        rosterText.textWrappingMode = TextWrappingModes.NoWrap;
        rosterText.overflowMode = TextOverflowModes.Ellipsis;
        rosterText.raycastTarget = false;

        if (playerCards.Length > 0
            && playerCards[0] != null
            && playerCards[0].playerNameText != null)
        {
            rosterText.font = playerCards[0].playerNameText.font;
        }
    }

    private void UpdateRoster()
    {
        if (rosterText == null)
        {
            CreateRosterPanel();
        }
        if (rosterText == null)
        {
            return;
        }

        StringBuilder text = new StringBuilder(
            "<color=#F2A23A><b>MATCH STATUS</b></color>\n"
            + "<size=15><color=#9AA7B8>GREEN = ALIVE   RED = DEAD</color></size>\n");
        if (OnlineSessionState.IsOnlineSession)
        {
            List<OnlinePlayerController> players = new List<OnlinePlayerController>(
                FindObjectsByType<OnlinePlayerController>(FindObjectsInactive.Include));
            players.RemoveAll(player =>
                player == null || player.Object == null || !player.Object.IsValid);
            players.Sort((left, right) =>
                left.Object.InputAuthority.PlayerId.CompareTo(right.Object.InputAuthority.PlayerId));

            for (int i = 0; i < players.Count; i++)
            {
                OnlinePlayerController player = players[i];
                OnlinePlayerHealth health = player.GetComponent<OnlinePlayerHealth>();
                bool alive = health != null && health.IsAlive;
                string displayName = player.Nickname.ToString();
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = $"Player {player.Object.InputAuthority.PlayerId}";
                }

                string you = player.Object.HasInputAuthority ? " (YOU)" : string.Empty;
                AppendRosterLine(text, displayName + you, alive);
            }
        }
        else
        {
            PlayerController[] players =
                FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController player = players[i];
                if (player == null)
                {
                    continue;
                }

                PlayerHealth health = player.GetComponent<PlayerHealth>();
                AppendRosterLine(text, player.name, health != null && health.IsAlive);
            }
        }

        rosterText.text = text.ToString();
    }

    private static void AppendRosterLine(StringBuilder text, string playerName, bool alive)
    {
        string color = alive ? "#56D47A" : "#E86464";
        // string status = alive ? "ALIVE" : "DEAD";
        text.Append("<color=")
            .Append(color)
            .Append(">● ")
            .Append(playerName)
            // .Append(" — ")
            // .Append(status)
            .Append("</color>\n");
    }
}
