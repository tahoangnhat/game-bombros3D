using UnityEngine;
using TMPro;

public class PlayerNameTag : MonoBehaviour
{
    [Header("Assign the TMP Text component")]
    public TMP_Text nameText;

    private OnlinePlayerController onlinePlayer;
    private PlayerController localPlayer;

    private void Start()
    {
        if (nameText == null)
        {
            nameText = GetComponentInChildren<TMP_Text>();
        }

        // Cache controllers on parent or current object
        onlinePlayer = GetComponentInParent<OnlinePlayerController>();
        localPlayer = GetComponentInParent<PlayerController>();

        UpdateNameText();
    }

    private void Update()
    {
        UpdateNameText();
    }

    private void UpdateNameText()
    {
        if (nameText == null)
        {
            return;
        }

        string displayName = "";

        if (onlinePlayer != null)
        {
            if (onlinePlayer.Object != null && onlinePlayer.Object.IsValid)
            {
                displayName = onlinePlayer.Nickname.ToString();
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = onlinePlayer.gameObject.name;
            }
        }
        else if (localPlayer != null)
        {
            displayName = localPlayer.gameObject.name;
        }
        else
        {
            displayName = gameObject.name;
        }

        if (nameText.text != displayName)
        {
            nameText.text = displayName;
        }
    }

    private void LateUpdate()
    {
        // Face the main camera perfectly
        if (Camera.main != null && nameText != null)
        {
            nameText.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
