using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Player Cards (Assign up to 4 cards in order)")]
    public PlayerCardUI[] playerCards = new PlayerCardUI[4];

    private void Start()
    {
        // Deactivate all cards initially
        foreach (var card in playerCards)
        {
            if (card != null && card.cardParent != null)
            {
                card.cardParent.SetActive(false);
                card.isAssigned = false;
            }
        }
    }

    private void Update()
    {
        // Find players and bind them to slots
        BindPlayersToCards();

        // Update card visual elements
        UpdateHUDVisuals();
    }

    private void BindPlayersToCards()
    {
        // 1. Gather all active players
        List<PlayerController> localPlayers = new List<PlayerController>(FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        List<OnlinePlayerController> onlinePlayers = new List<OnlinePlayerController>(FindObjectsByType<OnlinePlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        int totalPlayers = localPlayers.Count + onlinePlayers.Count;

        // Reset assignment status
        for (int i = 0; i < playerCards.Length; i++)
        {
            if (playerCards[i] != null)
            {
                playerCards[i].isAssigned = false;
            }
        }

        // Bind offline players
        for (int i = 0; i < localPlayers.Count; i++)
        {
            if (i < playerCards.Length && playerCards[i] != null)
            {
                var card = playerCards[i];
                if (card.cardParent != null)
                {
                    card.cardParent.SetActive(true);
                    card.isAssigned = true;
                    if (card.playerNameText != null) card.playerNameText.text = localPlayers[i].name.ToUpper();
                }
            }
        }

        // Bind online players (offset by local player count)
        int localCount = localPlayers.Count;
        for (int i = 0; i < onlinePlayers.Count; i++)
        {
            int slotIndex = localCount + i;
            if (slotIndex < playerCards.Length && playerCards[slotIndex] != null)
            {
                var card = playerCards[slotIndex];
                if (card.cardParent != null)
                {
                    card.cardParent.SetActive(true);
                    card.isAssigned = true;
                    if (card.playerNameText != null)
                    {
                        string displayName = "";
                        if (onlinePlayers[i].Object != null && onlinePlayers[i].Object.IsValid)
                        {
                            displayName = onlinePlayers[i].Nickname.ToString();
                        }
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = onlinePlayers[i].name;
                        }
                        card.playerNameText.text = displayName.ToUpper();
                    }
                }
            }
        }

        // Turn off unused card slots
        for (int i = totalPlayers; i < playerCards.Length; i++)
        {
            if (i >= 0 && i < playerCards.Length && playerCards[i] != null)
            {
                var card = playerCards[i];
                if (card.cardParent != null)
                {
                    card.cardParent.SetActive(false);
                }
            }
        }
    }

    private void UpdateHUDVisuals()
    {
        Color activeGreen = new Color(0.2f, 0.8f, 0.2f, 1f);
        Color inactiveGrey = new Color(0.3f, 0.32f, 0.35f, 1f);

        // Gather lists again for lookup
        List<PlayerController> localPlayers = new List<PlayerController>(FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        List<OnlinePlayerController> onlinePlayers = new List<OnlinePlayerController>(FindObjectsByType<OnlinePlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        int localCount = localPlayers.Count;

        for (int i = 0; i < playerCards.Length; i++)
        {
            var card = playerCards[i];
            if (card == null || !card.isAssigned || card.cardParent == null) continue;

            bool isDead = false;
            bool hasShield = false;
            bool hasBombBuff = false;
            int currentRange = 1;
            float speedProgress = 0f;

            // Retrieve stats depending on whether the slot holds a local or online player
            if (i < localCount)
            {
                PlayerController lp = localPlayers[i];
                if (lp == null)
                {
                    isDead = true;
                }
                else
                {
                    PlayerHealth health = lp.GetComponent<PlayerHealth>();
                    isDead = health == null;
                    hasShield = health != null && health.hasShield;
                    hasBombBuff = lp.MaxActiveBombs > 2;
                    currentRange = lp.CurrentBombRange;
                    speedProgress = lp.SpeedBuffProgress;
                }
            }
            else
            {
                int onlineIdx = i - localCount;
                if (onlineIdx >= 0 && onlineIdx < onlinePlayers.Count)
                {
                    OnlinePlayerController op = onlinePlayers[onlineIdx];
                    if (op == null || op.Object == null || !op.Object.IsValid)
                    {
                        isDead = true;
                    }
                    else
                    {
                        OnlinePlayerHealth health = op.GetComponent<OnlinePlayerHealth>();
                        isDead = health != null && health.IsEliminated;
                        hasShield = health != null && health.HasShield;
                        hasBombBuff = op.MaxActiveBombs > 2;
                        currentRange = op.CurrentBombRange;
                        speedProgress = op.SpeedBuffProgress;
                    }
                }
                else
                {
                    isDead = true;
                }
            }

            // Update UI Card Visuals
            if (isDead)
            {
                if (card.eliminatedOverlay != null) card.eliminatedOverlay.SetActive(true);
            }
            else
            {
                if (card.eliminatedOverlay != null) card.eliminatedOverlay.SetActive(false);

                if (card.shieldLight != null) card.shieldLight.color = hasShield ? activeGreen : inactiveGrey;
                if (card.bombLight != null) card.bombLight.color = hasBombBuff ? activeGreen : inactiveGrey;
                if (card.rangeText != null) card.rangeText.text = "x" + Mathf.Clamp(currentRange, 1, 5);
                
                if (card.speedCountdownText != null)
                {
                    int remainingSeconds = Mathf.CeilToInt(speedProgress * 15f);
                    if (remainingSeconds > 0)
                    {
                        card.speedCountdownText.text = remainingSeconds + "s";
                        card.speedCountdownText.color = activeGreen;
                    }
                    else
                    {
                        card.speedCountdownText.text = "0s";
                        card.speedCountdownText.color = inactiveGrey;
                    }
                }
            }
        }
    }
}
