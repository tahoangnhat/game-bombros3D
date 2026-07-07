using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(AudioListener))]
public class AudioControlsOverlay : MonoBehaviour
{
    private const string VolumePrefKey = "BoomBros.MasterVolume";
    private const string MutedPrefKey = "BoomBros.MasterMuted";
    private const string RootName = "AudioControlsOverlay";

    private static AudioControlsOverlay instance;

    [SerializeField] private float volumeStep = 0.1f;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-24f, -24f);
    [SerializeField] private Vector2 buttonSize = new Vector2(64f, 42f);
    [SerializeField] private float muteButtonWidth = 110f;
    [SerializeField] private float volumeLabelWidth = 120f;

    private float volume = 1f;
    private bool muted;
    private TMP_Text volumeText;
    private TMP_Text muteButtonText;
    private AudioListener fallbackAudioListener;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        AudioControlsOverlay[] existing =
            FindObjectsByType<AudioControlsOverlay>(FindObjectsInactive.Include);
        if (existing.Length > 0)
        {
            instance = existing[0];
            return;
        }

        GameObject managerObject = new GameObject(nameof(AudioControlsOverlay));
        instance = managerObject.AddComponent<AudioControlsOverlay>();
        DontDestroyOnLoad(managerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            AudioListener duplicateListener = GetComponent<AudioListener>();
            if (duplicateListener != null)
            {
                duplicateListener.enabled = false;
            }
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        fallbackAudioListener = GetComponent<AudioListener>();
        fallbackAudioListener.enabled = true;
        LoadSettings();
        ApplySettings();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        OnlineScenePresentation.EnforceSingleEventSystem();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceSingleAudioListener();
        OnlineScenePresentation.EnforceSingleEventSystem();
        ApplySettings();

        if (!ShouldShowInScene(scene.name))
        {
            return;
        }

        Canvas canvas = FindBestCanvas(scene);
        if (canvas == null || CanvasAlreadyHasControls(canvas))
        {
            RefreshLabels();
            return;
        }

        CreateControls(canvas);
        RefreshLabels();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (fallbackAudioListener != null)
        {
            fallbackAudioListener.enabled = true;
        }
    }

    private void Update()
    {
        if (instance != this)
        {
            if (fallbackAudioListener != null)
            {
                fallbackAudioListener.enabled = false;
            }
            Destroy(gameObject);
            return;
        }

        EnforceSingleAudioListener();
        OnlineScenePresentation.EnforceSingleEventSystem();

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            transform.SetPositionAndRotation(
                mainCamera.transform.position,
                mainCamera.transform.rotation);
        }
    }

    private void EnforceSingleAudioListener()
    {
        if (fallbackAudioListener == null)
        {
            return;
        }

        fallbackAudioListener.enabled = true;
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null
                && listener != fallbackAudioListener
                && listener.enabled)
            {
                listener.enabled = false;
            }
        }
    }

    private static bool ShouldShowInScene(string sceneName)
    {
        return sceneName == "MainMenuScene" || sceneName == "LobbyScene" || sceneName == "GameScene";
    }

    private static Canvas FindBestCanvas(Scene scene)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        Canvas fallback = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != scene)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = canvas;
            }

            string name = canvas.name;
            if (name == "MainMenuCanvas" || name == "LobbyCanvas" || name == "PlayerStatusCanvas")
            {
                return canvas;
            }
        }

        return fallback;
    }

    private static bool CanvasAlreadyHasControls(Canvas canvas)
    {
        Transform existing = canvas.transform.Find(RootName);
        return existing != null;
    }

    private void CreateControls(Canvas canvas)
    {
        GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rootObject.transform.SetParent(canvas.transform, false);

        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = anchoredPosition;
        const float spacing = 8f;
        float controlsWidth =
            (buttonSize.x * 2f) +
            muteButtonWidth +
            volumeLabelWidth +
            (spacing * 3f);
        root.sizeDelta = new Vector2(controlsWidth, buttonSize.y);

        HorizontalLayoutGroup layout = rootObject.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = spacing;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        CreateButton(root, "-", DecreaseVolume);
        muteButtonText = CreateButton(root, "Mute", ToggleMute);
        CreateButton(root, "+", IncreaseVolume);
        volumeText = CreateLabel(root);
    }

    private TMP_Text CreateButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = label == "Mute" ? muteButtonWidth : buttonSize.x;
        layout.preferredHeight = buttonSize.y;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.fontSize = 22f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        return text;
    }

    private TMP_Text CreateLabel(RectTransform parent)
    {
        GameObject labelObject = new GameObject("VolumeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);

        LayoutElement layout = labelObject.GetComponent<LayoutElement>();
        layout.preferredWidth = volumeLabelWidth;
        layout.preferredHeight = buttonSize.y;

        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        return text;
    }

    private void IncreaseVolume()
    {
        muted = false;
        volume = Mathf.Clamp01(volume + volumeStep);
        SaveAndApply();
    }

    private void DecreaseVolume()
    {
        volume = Mathf.Clamp01(volume - volumeStep);
        muted = volume <= 0f;
        SaveAndApply();
    }

    private void ToggleMute()
    {
        muted = !muted;
        SaveAndApply();
    }

    private void LoadSettings()
    {
        volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        muted = PlayerPrefs.GetInt(MutedPrefKey, 0) == 1;
    }

    private void SaveAndApply()
    {
        PlayerPrefs.SetFloat(VolumePrefKey, volume);
        PlayerPrefs.SetInt(MutedPrefKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplySettings();
        RefreshLabels();
    }

    private void ApplySettings()
    {
        AudioListener.volume = muted ? 0f : volume;
    }

    private void RefreshLabels()
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (text.name == "VolumeText")
            {
                text.text = "Vol " + Mathf.RoundToInt(volume * 100f) + "%";
            }
            else if (text.transform.parent != null && text.transform.parent.name == "MuteButton")
            {
                text.text = muted ? "Unmute" : "Mute";
            }
        }

        if (volumeText != null)
        {
            volumeText.text = "Vol " + Mathf.RoundToInt(volume * 100f) + "%";
        }

        if (muteButtonText != null)
        {
            muteButtonText.text = muted ? "Unmute" : "Mute";
        }
    }
}
