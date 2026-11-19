using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Gestiona la transición cinemática con fundido a negro y cuadros de texto narrativos noir
/// que se muestra al pasar de la Escena 1 a la Escena 2 (tras derrotar al jefe o entrar al callejón).
/// </summary>
public class BossVictoryTransitionUI : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("CanvasGroup del fondo oscuro que hace el fade in/out.")]
    public CanvasGroup blackoutCanvasGroup;

    [Tooltip("Texto para mostrar la narración de transición.")]
    public TextMeshProUGUI narrativeText;

    [Tooltip("Texto de indicador para continuar (ej. '[ ESPACIO / E / CLIC PARA CONTINUAR ]').")]
    public TextMeshProUGUI continuePromptText;

    private string[] narrativeLines;
    private string targetSceneName;
    private int currentLineIndex = 0;
    private bool isTransitioning = false;
    private bool canAdvanceLine = false;

    private static BossVictoryTransitionUI instance;

    public static BossVictoryTransitionUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<BossVictoryTransitionUI>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        PlayerHealthBarUI.EnsureCleanPixelFont(narrativeText);
        PlayerHealthBarUI.EnsureCleanPixelFont(continuePromptText);
    }

    /// <summary>
    /// Inicia la secuencia completa de transición: fundido a negro, cuadros de texto narrativos y carga de escena.
    /// </summary>
    public void StartTransition(string[] lines, string sceneToLoad)
    {
        if (isTransitioning) return;
        isTransitioning = true;
        narrativeLines = lines;
        targetSceneName = sceneToLoad;
        currentLineIndex = 0;

        PlayerHealthBarUI.EnsureCleanPixelFont(narrativeText);
        PlayerHealthBarUI.EnsureCleanPixelFont(continuePromptText);

        gameObject.SetActive(true);
        StartCoroutine(TransitionSequenceRoutine());
    }

    private IEnumerator TransitionSequenceRoutine()
    {
        if (blackoutCanvasGroup != null)
        {
            blackoutCanvasGroup.alpha = 0f;
            blackoutCanvasGroup.blocksRaycasts = true;
            blackoutCanvasGroup.interactable = true;
        }

        if (narrativeText != null) narrativeText.text = "";
        if (continuePromptText != null) continuePromptText.gameObject.SetActive(false);

        // Fundido suave a negro (1.1 segundos)
        float fadeDuration = 1.1f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (blackoutCanvasGroup != null)
            {
                blackoutCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            }
            yield return null;
        }

        if (blackoutCanvasGroup != null) blackoutCanvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(0.35f);

        if (narrativeLines != null && narrativeLines.Length > 0)
        {
            for (int i = 0; i < narrativeLines.Length; i++)
            {
                currentLineIndex = i;
                yield return StartCoroutine(DisplayNarrativeLine(narrativeLines[i]));
            }
        }
        else
        {
            yield return StartCoroutine(DisplayNarrativeLine("[ CAPÍTULO 2: EL CALLEJÓN DE LAS SOMBRAS ]\n\nAdentrándote a pie entre los edificios hacia la guarida de El Calacas..."));
        }

        if (continuePromptText != null) continuePromptText.gameObject.SetActive(false);
        if (narrativeText != null) narrativeText.text = "";

        yield return new WaitForSecondsRealtime(0.5f);

        if (!string.IsNullOrEmpty(targetSceneName))
        {
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null) stats.SaveProgressionSnapshot();
            if (InventoryManager.Instance != null) InventoryManager.Instance.SaveSnapshot();
            if (DetectiveStats.Instance != null) DetectiveStats.Instance.SaveProgressionSnapshot();

            Time.timeScale = 1f;
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning("[BossVictoryTransitionUI] No se especificó targetSceneName para la transición.");
        }
    }

    private IEnumerator DisplayNarrativeLine(string line)
    {
        if (narrativeText == null) yield break;

        PlayerHealthBarUI.EnsureCleanPixelFont(narrativeText);
        narrativeText.text = line;
        narrativeText.color = new Color(narrativeText.color.r, narrativeText.color.g, narrativeText.color.b, 0f);

        if (continuePromptText != null)
        {
            PlayerHealthBarUI.EnsureCleanPixelFont(continuePromptText);
            continuePromptText.gameObject.SetActive(false);
        }

        // Fade in del cuadro de texto (0.45s)
        float fadeInTime = 0.45f;
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(t / fadeInTime);
            narrativeText.color = new Color(narrativeText.color.r, narrativeText.color.g, narrativeText.color.b, alpha);
            yield return null;
        }
        narrativeText.color = new Color(narrativeText.color.r, narrativeText.color.g, narrativeText.color.b, 1f);

        yield return new WaitForSecondsRealtime(0.2f);

        if (continuePromptText != null)
        {
            continuePromptText.gameObject.SetActive(true);
        }

        canAdvanceLine = false;
        yield return new WaitForSecondsRealtime(0.15f);
        canAdvanceLine = true;

        float autoAdvanceTimer = 6.5f;
        bool advanced = false;

        while (!advanced && autoAdvanceTimer > 0f)
        {
            autoAdvanceTimer -= Time.unscaledDeltaTime;

            if (canAdvanceLine && WasAdvancePressedThisFrame())
            {
                if (MusicManager.Instance != null)
                {
                    MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
                }
                advanced = true;
                break;
            }
            yield return null;
        }

        // Fade out breve antes de la siguiente línea (0.28s)
        float fadeOutTime = 0.28f;
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(t / fadeOutTime);
            narrativeText.color = new Color(narrativeText.color.r, narrativeText.color.g, narrativeText.color.b, alpha);
            yield return null;
        }
        narrativeText.color = new Color(narrativeText.color.r, narrativeText.color.g, narrativeText.color.b, 0f);
        if (continuePromptText != null) continuePromptText.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(0.18f);
    }

    private static bool WasAdvancePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                return true;
            }
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetMouseButtonDown(0))
        {
            return true;
        }
#endif
        return false;
    }

    /// <summary>
    /// Crea dinámicamente el UI de transición en la escena activa si no existe previamente.
    /// </summary>
    public static BossVictoryTransitionUI EnsureInScene(TMP_FontAsset font = null)
    {
        BossVictoryTransitionUI existing = FindAnyObjectByType<BossVictoryTransitionUI>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject canvasGO = new GameObject("BossVictoryTransitionCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel de pantalla negra completa
        GameObject blackoutGO = new GameObject("BlackoutPanel");
        blackoutGO.transform.SetParent(canvasGO.transform, false);

        RectTransform blackoutRect = blackoutGO.AddComponent<RectTransform>();
        blackoutRect.anchorMin = Vector2.zero;
        blackoutRect.anchorMax = Vector2.one;
        blackoutRect.offsetMin = Vector2.zero;
        blackoutRect.offsetMax = Vector2.zero;

        Image blackoutImg = blackoutGO.AddComponent<Image>();
        blackoutImg.color = new Color(0.03f, 0.03f, 0.04f, 1f);

        CanvasGroup cg = blackoutGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Marco noir central para el cuadro de texto de transición
        GameObject frameBorderGO = new GameObject("NoirNarrativeBorder");
        frameBorderGO.transform.SetParent(blackoutGO.transform, false);
        RectTransform borderRect = frameBorderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.12f, 0.22f);
        borderRect.anchorMax = new Vector2(0.88f, 0.78f);
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImg = frameBorderGO.AddComponent<Image>();
        borderImg.color = new Color(0.92f, 0.68f, 0.22f, 0.45f);

        GameObject frameInnerGO = new GameObject("NoirNarrativeInner");
        frameInnerGO.transform.SetParent(frameBorderGO.transform, false);
        RectTransform innerRect = frameInnerGO.AddComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.004f, 0.008f);
        innerRect.anchorMax = new Vector2(0.996f, 0.992f);
        innerRect.offsetMin = Vector2.zero;
        innerRect.offsetMax = Vector2.zero;
        Image innerImg = frameInnerGO.AddComponent<Image>();
        innerImg.color = new Color(0.05f, 0.05f, 0.07f, 0.98f);

        // Texto narrativo central
        GameObject textGO = new GameObject("NarrativeText");
        textGO.transform.SetParent(frameInnerGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0.22f);
        textRect.anchorMax = new Vector2(0.92f, 0.85f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI narrativeTMP = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) narrativeTMP.font = font;
        narrativeTMP.fontSize = 32;
        narrativeTMP.alignment = TextAlignmentOptions.Center;
        narrativeTMP.color = new Color(0.92f, 0.90f, 0.86f, 1f);
        narrativeTMP.textWrappingMode = TextWrappingModes.Normal;
        narrativeTMP.text = "";
        PlayerHealthBarUI.EnsureCleanPixelFont(narrativeTMP);

        // Texto inferior de continuar
        GameObject promptGO = new GameObject("ContinuePromptText");
        promptGO.transform.SetParent(frameInnerGO.transform, false);

        RectTransform promptRect = promptGO.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.15f, 0.06f);
        promptRect.anchorMax = new Vector2(0.85f, 0.18f);
        promptRect.offsetMin = Vector2.zero;
        promptRect.offsetMax = Vector2.zero;

        TextMeshProUGUI promptTMP = promptGO.AddComponent<TextMeshProUGUI>();
        if (font != null) promptTMP.font = font;
        promptTMP.fontSize = 22;
        promptTMP.alignment = TextAlignmentOptions.Center;
        promptTMP.color = new Color(0.92f, 0.68f, 0.22f, 0.95f);
        promptTMP.text = "[ ESPACIO / E / CLIC PARA CONTINUAR ]";
        PlayerHealthBarUI.EnsureCleanPixelFont(promptTMP);
        promptGO.SetActive(false);

        BossVictoryTransitionUI transitionUI = canvasGO.AddComponent<BossVictoryTransitionUI>();
        transitionUI.blackoutCanvasGroup = cg;
        transitionUI.narrativeText = narrativeTMP;
        transitionUI.continuePromptText = promptTMP;

        canvasGO.SetActive(true);
        return transitionUI;
    }
}
