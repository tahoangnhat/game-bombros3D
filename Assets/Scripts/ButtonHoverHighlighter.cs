using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Automatically adds a consistent hover highlight to every UI Button.
/// Buttons created at runtime are picked up by the periodic scan as well.
/// </summary>
public sealed class ButtonHoverHighlighter : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const float HoverScale = 1.05f;
    private const float AnimationDuration = 0.12f;

    private static readonly Color HoverTint = new Color(1f, 0.92f, 0.72f, 1f);
    private static readonly Color GlowColor = new Color(1f, 0.48f, 0.08f, 0.75f);

    private Button button;
    private Graphic targetGraphic;
    private Shadow hoverGlow;
    private Vector3 normalScale;
    private Color normalColor;
    private Coroutine animationRoutine;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        button = GetComponent<Button>();
        if (button == null)
        {
            enabled = false;
            return;
        }

        targetGraphic = button.targetGraphic;
        normalScale = transform.localScale;
        normalColor = targetGraphic != null ? targetGraphic.color : Color.white;

        if (targetGraphic != null)
        {
            hoverGlow = targetGraphic.gameObject.AddComponent<Shadow>();
            hoverGlow.effectColor = GlowColor;
            hoverGlow.effectDistance = new Vector2(3f, -3f);
            hoverGlow.useGraphicAlpha = true;
            hoverGlow.enabled = false;
        }

        initialized = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.IsInteractable())
        {
            return;
        }

        AnimateTo(normalScale * HoverScale, HoverTint, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(normalScale, normalColor, false);
    }

    private void AnimateTo(Vector3 targetScale, Color targetColor, bool showGlow)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(
            AnimateRoutine(targetScale, targetColor, showGlow)
        );
    }

    private IEnumerator AnimateRoutine(
        Vector3 targetScale,
        Color targetColor,
        bool showGlow)
    {
        Vector3 startScale = transform.localScale;
        Color startColor = targetGraphic != null ? targetGraphic.color : normalColor;

        if (hoverGlow != null && showGlow)
        {
            hoverGlow.enabled = true;
        }

        float elapsed = 0f;
        while (elapsed < AnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / AnimationDuration);
            t = t * t * (3f - 2f * t);

            transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            if (targetGraphic != null)
            {
                targetGraphic.color = Color.LerpUnclamped(startColor, targetColor, t);
            }

            yield return null;
        }

        transform.localScale = targetScale;
        if (targetGraphic != null)
        {
            targetGraphic.color = targetColor;
        }

        if (hoverGlow != null && !showGlow)
        {
            hoverGlow.enabled = false;
        }

        animationRoutine = null;
    }

    private void OnDisable()
    {
        if (!initialized)
        {
            return;
        }

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        transform.localScale = normalScale;
        if (targetGraphic != null)
        {
            targetGraphic.color = normalColor;
        }

        if (hoverGlow != null)
        {
            hoverGlow.enabled = false;
        }
    }
}

/// <summary>
/// Persistent bootstrap that installs ButtonHoverHighlighter globally.
/// </summary>
public sealed class ButtonHoverInstaller : MonoBehaviour
{
    private const float ScanInterval = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<ButtonHoverInstaller>() != null)
        {
            return;
        }

        GameObject installerObject = new GameObject("[Button Hover Installer]");
        DontDestroyOnLoad(installerObject);
        installerObject.AddComponent<ButtonHoverInstaller>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        InstallOnAllButtons();
        StartCoroutine(ScanForNewButtons());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAllCoroutines();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallOnAllButtons();
    }

    private IEnumerator ScanForNewButtons()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(ScanInterval);

        while (true)
        {
            yield return wait;
            InstallOnAllButtons();
        }
    }

    private static void InstallOnAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Button currentButton in buttons)
        {
            if (currentButton.GetComponent<ButtonHoverHighlighter>() == null)
            {
                currentButton.gameObject.AddComponent<ButtonHoverHighlighter>();
            }
        }
    }
}
