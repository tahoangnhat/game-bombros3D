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

    private static readonly Color CardBackground = new Color(0.38f, 0.24f, 0.13f, 1f);
    private static readonly Color CardBorderGold = new Color(0.95f, 0.78f, 0.22f, 1f);
    private static readonly Color NameGold = new Color(1f, 0.88f, 0.35f, 1f);
    private static readonly Color IconFrameBrown = new Color(0.18f, 0.11f, 0.06f, 1f);
    private static readonly Color HpIconPink = new Color(0.93f, 0.18f, 0.42f, 1f);
    private static readonly Color PortraitFrameBrown = new Color(0.15f, 0.09f, 0.05f, 1f);
    private const float StatValueFontSize = 22f;
    private const float PlayerNameFontSize = 20f;
    private static readonly float[] StatRowCenterY = { 0.62f, 0.46f, 0.30f, 0.14f };
    private const float StatIconLeft = 0.40f;
    private const float StatIconRight = 0.52f;
    private const float StatValueLeft = 0.54f;
    private const float StatValueRight = 0.96f;
    private const float StatRowHalfHeight = 0.07f;

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
        int maxBombs;
        int currentRange;
        float speedMultiplier;
        string displayName;

        if (localOnlinePlayer != null)
        {
            OnlinePlayerHealth health = localOnlinePlayer.GetComponent<OnlinePlayerHealth>();
            isDead = health != null && health.IsEliminated;
            currentHealth = health != null ? health.CurrentHealth : 0;
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
            maxBombs = localPlayer.MaxActiveBombs;
            currentRange = localPlayer.CurrentBombRange;
            speedMultiplier = localPlayer.SpeedMultiplier;
            displayName = localPlayer.name;
        }

        if (card.playerNameText != null)
        {
            card.playerNameText.text = displayName.ToUpperInvariant();
        }
        if (card.eliminatedOverlay != null)
        {
            card.eliminatedOverlay.SetActive(isDead);
        }

        EnsureStatValueTexts(card);
        if (healthValueText != null)
        {
            healthValueText.text = Mathf.Max(0, currentHealth).ToString();
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
        }
    }

    private void StylePersonalCard(PlayerCardUI card)
    {
        if (personalCardStyled || card == null || card.cardParent == null)
        {
            return;
        }

        Transform cardRoot = card.cardParent.transform;
        card.cardParent.name = "LocalPlayerHUD";

        Image background = card.cardParent.GetComponent<Image>();
        if (background != null)
        {
            background.color = CardBackground;
            background.raycastTarget = false;
        }

        Outline outline = card.cardParent.GetComponent<Outline>();
        if (outline == null)
        {
            outline = card.cardParent.AddComponent<Outline>();
        }
        outline.effectColor = CardBorderGold;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;

        Shadow shadow = card.cardParent.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = card.cardParent.AddComponent<Shadow>();
        }
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(4f, -4f);
        shadow.useGraphicAlpha = true;

        if (card.playerNameText != null)
        {
            RectTransform nameRect = card.playerNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.04f, 0.74f);
            nameRect.anchorMax = new Vector2(0.96f, 0.92f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            card.playerNameText.color = NameGold;
            card.playerNameText.fontStyle = FontStyles.Bold;
            card.playerNameText.fontSize = PlayerNameFontSize;
            card.playerNameText.enableAutoSizing = true;
            card.playerNameText.fontSizeMin = 14f;
            card.playerNameText.fontSizeMax = PlayerNameFontSize;
            card.playerNameText.alignment = TextAlignmentOptions.Center;
            card.playerNameText.overflowMode = TextOverflowModes.Ellipsis;
        }

        Transform characterImage = cardRoot.Find("CharacterImage");
        if (characterImage != null)
        {
            RectTransform portraitRect = characterImage.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.04f, 0.08f);
            portraitRect.anchorMax = new Vector2(0.36f, 0.70f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            Image portraitImage = characterImage.GetComponent<Image>();
            if (portraitImage != null)
            {
                portraitImage.color = Color.white;
                portraitImage.preserveAspect = true;
            }

            EnsurePortraitFrame(cardRoot, characterImage);
            characterImage.SetSiblingIndex(1);
        }

        LayoutStatRowIcon(cardRoot, "ShieldIcon", 0);
        LayoutStatRowIcon(cardRoot, "HealthIcon", 0);
        LayoutStatRowIcon(cardRoot, "BombIcon", 1);
        LayoutStatRowIcon(cardRoot, "RangeIcon", 2);
        LayoutStatRowIcon(cardRoot, "SpeedIcon", 3);

        LayoutStatRowValue(card.shieldLight, 0);
        LayoutStatRowValue(card.bombLight, 1);
        LayoutStatRowValue(card.rangeText, 2);
        LayoutStatRowValue(card.speedCountdownText, 3);

        ApplyStatValueStyle(card.rangeText, card);
        ApplyStatValueStyle(card.speedCountdownText, card);

        if (card.eliminatedOverlay != null)
        {
            RectTransform overlayRect = card.eliminatedOverlay.GetComponent<RectTransform>();
            if (overlayRect != null)
            {
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
            }
            card.eliminatedOverlay.transform.SetAsLastSibling();
        }

        personalCardStyled = true;
    }

    private void LayoutStatRowIcon(Transform cardRoot, string iconName, int rowIndex)
    {
        Transform iconTransform = cardRoot.Find(iconName);
        if (iconTransform == null)
        {
            return;
        }

        RectTransform iconRect = iconTransform.GetComponent<RectTransform>();
        float rowCenterY = StatRowCenterY[rowIndex];
        iconRect.anchorMin = new Vector2(StatIconLeft, rowCenterY - StatRowHalfHeight);
        iconRect.anchorMax = new Vector2(StatIconRight, rowCenterY + StatRowHalfHeight);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconRect.pivot = new Vector2(0.5f, 0.5f);

        if (iconName != "HealthIcon" && iconName != "ShieldIcon")
        {
            EnsureIconFrame(iconTransform, IconFrameBrown);
        }

        Transform graphicTransform = iconTransform.Find("IconGraphic");
        if (graphicTransform != null)
        {
            Image graphicImage = graphicTransform.GetComponent<Image>();
            if (graphicImage != null)
            {
                graphicImage.preserveAspect = true;
            }
        }
    }

    private static void LayoutStatRowValue(Component valueComponent, int rowIndex)
    {
        if (valueComponent == null)
        {
            return;
        }

        RectTransform valueRect = valueComponent.GetComponent<RectTransform>();
        float rowCenterY = StatRowCenterY[rowIndex];
        valueRect.anchorMin = new Vector2(StatValueLeft, rowCenterY - StatRowHalfHeight);
        valueRect.anchorMax = new Vector2(StatValueRight, rowCenterY + StatRowHalfHeight);
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;
        valueRect.pivot = new Vector2(0f, 0.5f);

        Image valueImage = valueComponent.GetComponent<Image>();
        if (valueImage != null)
        {
            valueImage.enabled = false;
        }
    }

    private static void EnsurePortraitFrame(Transform cardRoot, Transform portraitTransform)
    {
        Transform frameTransform = cardRoot.Find("PortraitFrame");
        if (frameTransform == null)
        {
            GameObject frameObject = new GameObject(
                "PortraitFrame",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            frameObject.transform.SetParent(cardRoot, false);
            frameObject.transform.SetSiblingIndex(portraitTransform.GetSiblingIndex());
            frameTransform = frameObject.transform;
        }

        RectTransform portraitRect = portraitTransform.GetComponent<RectTransform>();
        RectTransform frameRect = frameTransform.GetComponent<RectTransform>();
        frameRect.anchorMin = portraitRect.anchorMin;
        frameRect.anchorMax = portraitRect.anchorMax;
        frameRect.offsetMin = portraitRect.offsetMin;
        frameRect.offsetMax = portraitRect.offsetMax;
        frameRect.pivot = portraitRect.pivot;
        frameRect.anchoredPosition = portraitRect.anchoredPosition;
        frameRect.sizeDelta = portraitRect.sizeDelta;

        Image frameImage = frameTransform.GetComponent<Image>();
        frameImage.color = PortraitFrameBrown;
        frameImage.raycastTarget = false;
    }

    private static void EnsureIconFrame(Transform iconTransform, Color frameColor)
    {
        Image parentImage = iconTransform.GetComponent<Image>();
        if (parentImage == null)
        {
            return;
        }

        Transform legacyFrame = iconTransform.Find("IconFrame");
        if (legacyFrame != null)
        {
            Object.Destroy(legacyFrame.gameObject);
        }

        Transform graphicTransform = iconTransform.Find("IconGraphic");
        if (graphicTransform == null)
        {
            Sprite iconSprite = parentImage.sprite;

            GameObject graphicObject = new GameObject(
                "IconGraphic",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            graphicObject.transform.SetParent(iconTransform, false);

            RectTransform graphicRect = graphicObject.GetComponent<RectTransform>();
            graphicRect.anchorMin = Vector2.zero;
            graphicRect.anchorMax = Vector2.one;
            graphicRect.offsetMin = new Vector2(2f, 2f);
            graphicRect.offsetMax = new Vector2(-2f, -2f);

            Image graphicImage = graphicObject.GetComponent<Image>();
            graphicImage.sprite = iconSprite;
            graphicImage.color = Color.white;
            graphicImage.preserveAspect = true;
            graphicImage.raycastTarget = false;
        }

        parentImage.sprite = null;
        parentImage.color = frameColor;
        parentImage.raycastTarget = false;
    }

    private static void ApplyStatValueStyle(TextMeshProUGUI text, PlayerCardUI card)
    {
        if (text == null)
        {
            return;
        }

        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = StatValueFontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = StatValueFontSize;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        if (card.playerNameText != null)
        {
            text.font = card.playerNameText.font;
        }
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
            ApplyStatValueStyle(healthValueText, card);
        }

        if (bombValueText == null && card.bombLight != null)
        {
            bombValueText = CreateStatValueText(
                card.bombLight.transform,
                "BombValueText",
                card);
            ApplyStatValueStyle(bombValueText, card);
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
                iconImage.color = HpIconPink;
            }

            TextMeshProUGUI label = CreateStatValueText(
                iconTransform,
                "HealthLabel",
                card);
            label.text = "HP";
            label.fontSize = 13f;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 13f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;

            LayoutStatRowIcon(card.cardParent.transform, "HealthIcon", 0);
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
        text.fontSize = StatValueFontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = StatValueFontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
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
