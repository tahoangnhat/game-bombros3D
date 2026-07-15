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
    [SerializeField] private Button singlePlayerButton;

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    [Header("Single Player")]
    [SerializeField] private string singlePlayerLabelKeyword = "single player";
    [SerializeField] private string comingSoonTitle = "Coming Soon";
    [SerializeField] private string comingSoonMessage = "Single Player mode is coming soon.";

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.2f;

    [Header("Auto Find")]
    [SerializeField] private bool autoFindButtonsByName = true;
    [SerializeField] private bool autoFindSinglePlayerButtonByLabel = true;
    [SerializeField] private bool autoFindJoinCodeFieldByName = true;
    [SerializeField] private bool autoCreateLobbyManager = true;
    [SerializeField] private string createButtonNameKeyword = "create";
    [SerializeField] private string joinButtonNameKeyword = "join";
    [SerializeField] private string joinCodeFieldNameKeyword = "code";

    private float refreshTimer;
    private Coroutine loadLobbySceneCoroutine;
    private GameObject comingSoonPopup;

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
        RefreshUI();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void Update()
    {
        ResolveReferences();

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            RefreshUI();
        }
    }

    private void ResolveReferences()
    {
        if (lobbyManager == null)
        {
            lobbyManager = FindAnyObjectByType<OnlineLobbyManager>(FindObjectsInactive.Include);
        }

        if (lobbyManager == null && autoCreateLobbyManager && SpringAuthSession.IsSignedIn)
        {
            GameObject managerObject = new GameObject("OnlineLobbyManager");
            lobbyManager = managerObject.AddComponent<OnlineLobbyManager>();
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

        if (autoFindSinglePlayerButtonByLabel && singlePlayerButton == null)
        {
            singlePlayerButton = FindButtonByLabelKeyword(singlePlayerLabelKeyword);
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

        if (singlePlayerButton != null)
        {
            singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
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

        if (singlePlayerButton != null)
        {
            singlePlayerButton.onClick.RemoveListener(OnSinglePlayerClicked);
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

    private void RefreshUI()
    {
        bool canUseLobbyActions = lobbyManager != null && lobbyManager.CanUseLobbyActions;

        SetLobbyButtonsInteractable(canUseLobbyActions);
    }

    private void SetLobbyButtonsInteractable(bool canUseLobbyActions)
    {
        if (createButton != null)
        {
            createButton.interactable = canUseLobbyActions;
        }

        if (joinButton != null)
        {
            joinButton.interactable = canUseLobbyActions && !string.IsNullOrWhiteSpace(ReadJoinCode());
        }
    }

    private void OnCreateClicked()
    {
        ResolveReferences();

        if (lobbyManager == null || !lobbyManager.CanUseLobbyActions)
        {
            return;
        }

        RefreshUI();
        lobbyManager.CreateLobbyAndHost();
        SetLobbyButtonsInteractable(false);
        StartLoadLobbySceneWhenReady();
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

        if (lobbyManager == null || !lobbyManager.CanUseLobbyActions)
        {
            return;
        }

        if (lobbyManager != null)
        {
            lobbyManager.SetJoinCodeInput(lobbyCode);
            lobbyManager.JoinLobbyByInputCode();
        }

        RefreshUI();
        StartLoadLobbySceneWhenReady();
        SetLobbyButtonsInteractable(false);
    }

    private void OnSinglePlayerClicked()
    {
        ShowComingSoonPopup();
    }

    private void ShowComingSoonPopup()
    {
        if (comingSoonPopup == null)
        {
            comingSoonPopup = CreateComingSoonPopup();
        }

        if (comingSoonPopup != null)
        {
            comingSoonPopup.SetActive(true);
        }
    }

    private GameObject CreateComingSoonPopup()
    {
        Canvas targetCanvas = root != null
            ? root.GetComponentInParent<Canvas>()
            : FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

        if (targetCanvas == null)
        {
            Debug.LogWarning("[Main Menu] Cannot show Coming Soon popup because no Canvas was found.");
            return null;
        }

        GameObject overlay = new GameObject("ComingSoonPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(targetCanvas.transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(520f, 260f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);

        TMP_Text titleText = CreatePopupText("Title", panel.transform, comingSoonTitle, 42f, FontStyles.Bold);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -34f);
        titleRect.sizeDelta = new Vector2(-60f, 64f);

        TMP_Text messageText = CreatePopupText("Message", panel.transform, comingSoonMessage, 24f, FontStyles.Normal);
        RectTransform messageRect = messageText.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0f, 0.5f);
        messageRect.anchorMax = new Vector2(1f, 0.5f);
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.anchoredPosition = new Vector2(0f, -12f);
        messageRect.sizeDelta = new Vector2(-70f, 80f);

        Button okButton = CreatePopupButton(panel.transform);
        okButton.onClick.AddListener(() => overlay.SetActive(false));

        return overlay;
    }

    private static TMP_Text CreatePopupText(string name, Transform parent, string text, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = fontStyle;
        tmpText.color = Color.white;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.enableWordWrapping = true;

        return tmpText;
    }

    private static Button CreatePopupButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("OkButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 28f);
        buttonRect.sizeDelta = new Vector2(180f, 56f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.95f, 0.76f, 0.22f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        TMP_Text buttonText = CreatePopupText("Text", buttonObject.transform, "OK", 24f, FontStyles.Bold);
        buttonText.color = new Color(0.08f, 0.10f, 0.14f, 1f);
        RectTransform textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private void StartLoadLobbySceneWhenReady()
    {
        if (loadLobbySceneCoroutine != null)
        {
            return;
        }

        loadLobbySceneCoroutine = StartCoroutine(LoadLobbySceneWhenReady());
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
                loadLobbySceneCoroutine = null;
                yield break;
            }

            yield return null;
        }

        if (lobbyManager != null && lobbyManager.CurrentLobby != null)
        {
            LoadLobbyScene();
        }

        loadLobbySceneCoroutine = null;
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

    private static Button FindButtonByLabelKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        TMP_Text[] labels = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        string normalizedKeyword = keyword.Trim().ToLowerInvariant();

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || label.text == null)
            {
                continue;
            }

            if (!label.text.Trim().ToLowerInvariant().Contains(normalizedKeyword))
            {
                continue;
            }

            Button button = label.GetComponentInParent<Button>(true);
            if (button != null)
            {
                return button;
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
