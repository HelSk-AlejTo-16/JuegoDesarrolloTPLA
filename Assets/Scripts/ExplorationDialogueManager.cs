using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla el panel de diálogo de exploración en el mapa (al recoger objetos, examinar el entorno o hablar con NPCs).
/// </summary>
public class ExplorationDialogueManager : MonoBehaviour
{
    private static ExplorationDialogueManager _instance;

    public static ExplorationDialogueManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ExplorationDialogueManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public static ExplorationDialogueManager EnsureInScene()
    {
        if (_instance != null && _instance.explorationDialoguePanel != null)
        {
            return _instance;
        }

        ExplorationDialogueManager existing = FindAnyObjectByType<ExplorationDialogueManager>(FindObjectsInactive.Include);
        if (existing != null && existing.explorationDialoguePanel != null)
        {
            _instance = existing;
            return existing;
        }

        GameObject canvasGO = (existing != null) ? existing.gameObject : new GameObject("ExplorationDialogueCanvas");
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            canvasGO.AddComponent<GraphicRaycaster>();

        ExplorationDialogueManager manager = (existing != null) ? existing : canvasGO.AddComponent<ExplorationDialogueManager>();
        _instance = manager;

        if (manager.explorationDialoguePanel == null)
        {
            GameObject panelGO = new GameObject("ExplorationDialoguePanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            RectTransform panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.08f, 0.04f);
            panelRT.anchorMax = new Vector2(0.92f, 0.30f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            Image panelImg = panelGO.GetComponent<Image>();
            panelImg.color = new Color(0.04f, 0.04f, 0.06f, 0.96f);

            GameObject textGO = new GameObject("SpeakerAndText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(panelGO.transform, false);
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.205f, 0.20f);
            textRT.anchorMax = new Vector2(0.95f, 0.70f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 26f;
            tmp.color = new Color(0.92f, 0.90f, 0.86f, 1f);
            PlayerHealthBarUI.EnsureCleanPixelFont(tmp);

            GameObject contGO = new GameObject("ContinueIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
            contGO.transform.SetParent(panelGO.transform, false);
            RectTransform contRT = contGO.GetComponent<RectTransform>();
            contRT.anchorMin = new Vector2(0.205f, 0.05f);
            contRT.anchorMax = new Vector2(0.96f, 0.19f);
            contRT.offsetMin = Vector2.zero;
            contRT.offsetMax = Vector2.zero;
            TextMeshProUGUI contTMP = contGO.GetComponent<TextMeshProUGUI>();
            contTMP.fontSize = 20f;
            contTMP.alignment = TextAlignmentOptions.BottomRight;
            contTMP.color = new Color(0.92f, 0.68f, 0.22f, 0.9f);
            contTMP.text = "[ ESPACIO / E / CLIC PARA CONTINUAR ]";
            PlayerHealthBarUI.EnsureCleanPixelFont(contTMP);

            manager.explorationDialoguePanel = panelGO;
            manager.speakerAndText = tmp;
            manager.continueIndicatorText = contTMP;
            manager.playerController = FindAnyObjectByType<PlayerController2D>();

            Capitulo2IntroNarrative.UpgradeDialoguePanelToNoirStyle(manager);
            panelGO.SetActive(false);
        }

        return manager;
    }

    [Header("1. Referencias Principales de UI")]
    [Tooltip("Panel raíz del diálogo de exploración dentro del Canvas.")]
    public GameObject explorationDialoguePanel;

    [Tooltip("Texto principal que muestra el diálogo (y el hablante).")]
    public TextMeshProUGUI speakerAndText;

    [Tooltip("Indicador visual de 'Presiona Espacio / E para continuar'.")]
    public TextMeshProUGUI continueIndicatorText;

    [Tooltip("Botón opcional en pantalla para avanzar con clic.")]
    public Button nextLineButton;

    [Header("2. Soporte de Retrato (Opcional - Mismo estilo que Conversaciones/Combate)")]
    public GameObject portraitContainer;
    public Image portraitImage;
    public TextMeshProUGUI portraitLabelText;
    public TextMeshProUGUI speakerNameText;

    [Header("3. Retratos y Colores por Tipo de Hablante")]
    public Sprite enriquePortrait;
    public Sprite investigadorPortrait;
    public Sprite corporeoPortrait;
    public Sprite intelectoPortrait;
    public Sprite motricidadPortrait;
    public Sprite mundoInternoPortrait;
    public Color characterGrayColor = new Color(0.38f, 0.39f, 0.42f, 1f);
    public Color corporeoColor = new Color(0.78f, 0.20f, 0.20f, 1f);
    public Color intelectoColor = new Color(0.20f, 0.48f, 0.82f, 1f);
    public Color motricidadColor = new Color(0.85f, 0.62f, 0.15f, 1f);
    public Color mundoInternoColor = new Color(0.58f, 0.26f, 0.78f, 1f);

    [Header("4. Referencia al Jugador")]
    public PlayerController2D playerController;

    [Header("5. Efecto de Sonido de Confirmación de Diálogo")]
    [Tooltip("EfectodeSonido_BotóndeConfirmaciónDialogo: Sonido al avanzar o confirmar líneas de diálogo.")]
    public AudioClip dialogueConfirmSfx;

    public bool IsDialogueActive { get; private set; } = false;

    private string[] currentLines;
    private int currentLineIndex = 0;
    private int dialogueOpenedFrame = -1;
    private Action onDialogueCompleteCallback;

    private void OnValidate()
    {
        LoadSfxClipsIfMissing();
    }

    public void LoadSfxClipsIfMissing()
    {
        if (dialogueConfirmSfx == null)
        {
            dialogueConfirmSfx = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxBotonConfirmacionDialogo);
        }
        if (investigadorPortrait == null || investigadorPortrait.name.Contains("Cardo") || investigadorPortrait.name == "1")
        {
            Sprite detPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Detective.png");
            if (detPortrait != null)
            {
                investigadorPortrait = detPortrait;
            }
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        LoadSfxClipsIfMissing();
    }

    private void Start()
    {
        if (nextLineButton != null)
        {
            nextLineButton.onClick.RemoveListener(AdvanceDialogue);
            nextLineButton.onClick.AddListener(AdvanceDialogue);
        }

        if (!IsDialogueActive && explorationDialoguePanel != null)
        {
            explorationDialoguePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsDialogueActive)
            return;

        // Evitar que la misma pulsación de tecla 'E' que abrió el diálogo salte la primera línea en el mismo frame
        if (Time.frameCount == dialogueOpenedFrame)
            return;

        if (WasAdvancePressedThisFrame())
        {
            AdvanceDialogue();
        }
    }

    /// <summary>
    /// Inicia una secuencia de diálogo en el mapa, bloqueando el movimiento de Enrique hasta terminar.
    /// </summary>
    public void StartDialogue(string[] lines, Action onComplete = null)
    {
        if (lines == null || lines.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController2D>();
        }

        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        currentLines = lines;
        currentLineIndex = 0;
        dialogueOpenedFrame = Time.frameCount;
        onDialogueCompleteCallback = onComplete;
        IsDialogueActive = true;

        if (explorationDialoguePanel != null)
        {
            explorationDialoguePanel.SetActive(true);
        }

        if (continueIndicatorText != null)
        {
            continueIndicatorText.text = "Presiona ESPACIO / E / CLIC para continuar";
        }

        RenderCurrentLine();
    }

    /// <summary>
    /// Avanza a la siguiente línea del diálogo o cierra el panel si era la última.
    /// </summary>
    public void AdvanceDialogue()
    {
        if (!IsDialogueActive)
            return;

        PlayDialogueConfirmSfx();

        currentLineIndex++;

        if (currentLines == null || currentLineIndex >= currentLines.Length)
        {
            CloseDialogue();
        }
        else
        {
            RenderCurrentLine();
        }
    }

    private void PlayDialogueConfirmSfx()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx(dialogueConfirmSfx);
        }
        else if (dialogueConfirmSfx != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(dialogueConfirmSfx, Camera.main.transform.position, 0.85f);
        }
    }

    private void CloseDialogue()
    {
        IsDialogueActive = false;
        currentLines = null;
        currentLineIndex = 0;

        if (explorationDialoguePanel != null)
        {
            explorationDialoguePanel.SetActive(false);
        }

        // Devolver el movimiento a Enrique solo si no estamos en combate
        BattleManager bm = FindAnyObjectByType<BattleManager>();
        bool inCombat = (bm != null && bm.state != BattleState.INACTIVE);

        if (!inCombat)
        {
            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController2D>();
            }
            if (playerController != null)
            {
                playerController.SetCanMove(true);
            }
        }

        Action callback = onDialogueCompleteCallback;
        onDialogueCompleteCallback = null;
        callback?.Invoke();
    }

    private void RenderCurrentLine()
    {
        if (currentLines == null || currentLineIndex < 0 || currentLineIndex >= currentLines.Length)
            return;

        PlayerHealthBarUI.EnsureCleanPixelFont(speakerAndText);
        PlayerHealthBarUI.EnsureCleanPixelFont(speakerNameText);
        PlayerHealthBarUI.EnsureCleanPixelFont(continueIndicatorText);
        PlayerHealthBarUI.EnsureCleanPixelFont(portraitLabelText);

        string rawLine = currentLines[currentLineIndex] ?? "";

        DialogueSpeakerType speakerType = DialogueSpeakerType.Narrator;
        SkillVoiceType skillVoice = SkillVoiceType.Corporeo;
        string speakerName = "NARRADOR";
        string bodyText = rawLine;

        int colonIndex = rawLine.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 45)
        {
            string prefix = rawLine.Substring(0, colonIndex).Trim();
            bodyText = rawLine.Substring(colonIndex + 1).Trim();
            string upperPrefix = prefix.ToUpperInvariant();

            if (upperPrefix.Contains("NARRADOR"))
            {
                speakerType = DialogueSpeakerType.Narrator;
                speakerName = "NARRADOR";
            }
            else if (upperPrefix.Contains("CORPOREO") || upperPrefix.Contains("CORPÓREO"))
            {
                speakerType = DialogueSpeakerType.SkillVoice;
                skillVoice = SkillVoiceType.Corporeo;
                speakerName = "VOZ: CORPOREO";
            }
            else if (upperPrefix.Contains("INTELECTO"))
            {
                speakerType = DialogueSpeakerType.SkillVoice;
                skillVoice = SkillVoiceType.Intelecto;
                speakerName = "VOZ: INTELECTO";
            }
            else if (upperPrefix.Contains("MOTRICIDAD"))
            {
                speakerType = DialogueSpeakerType.SkillVoice;
                skillVoice = SkillVoiceType.Motricidad;
                speakerName = "VOZ: MOTRICIDAD";
            }
            else if (upperPrefix.Contains("MUNDO INTERNO") || upperPrefix.Contains("MUNDOINTERNO"))
            {
                speakerType = DialogueSpeakerType.SkillVoice;
                skillVoice = SkillVoiceType.MundoInterno;
                speakerName = "VOZ: MUNDO INTERNO";
            }
            else
            {
                speakerType = DialogueSpeakerType.Character;
                speakerName = prefix.ToUpperInvariant();
            }
        }

        bool hasPortrait = (speakerType != DialogueSpeakerType.Narrator);

        if (portraitContainer != null)
        {
            portraitContainer.SetActive(hasPortrait);
        }

        float leftAnchorX = (portraitContainer != null && hasPortrait) ? 0.205f : 0.03f;

        if (speakerNameText != null)
        {
            RectTransform rt = speakerNameText.rectTransform;
            rt.anchorMin = new Vector2(leftAnchorX, rt.anchorMin.y);
        }

        if (speakerAndText != null)
        {
            RectTransform rt = speakerAndText.rectTransform;
            rt.anchorMin = new Vector2(leftAnchorX, rt.anchorMin.y);

            // Si hay un speakerNameText dedicado, mostrar solo el cuerpo; si no, mostrar "HABLANTE: Texto"
            if (speakerNameText != null)
            {
                speakerAndText.text = bodyText;
            }
            else
            {
                speakerAndText.text = (colonIndex > 0) ? $"{speakerName}: {bodyText}" : bodyText;
            }
        }

        switch (speakerType)
        {
            case DialogueSpeakerType.Narrator:
                if (speakerNameText != null)
                {
                    speakerNameText.text = "NARRADOR";
                    speakerNameText.color = new Color(0.68f, 0.70f, 0.75f, 1f);
                }
                break;

            case DialogueSpeakerType.Character:
                bool isEnrique = speakerName.Contains("ENRIQUE");
                bool isInvestigador = speakerName.Contains("INVESTIGADOR") || speakerName.Contains("DETECTIVE") || speakerName.Contains("VARGAS");
                Sprite charSprite = isEnrique ? enriquePortrait : (isInvestigador ? investigadorPortrait : null);

                if (portraitImage != null)
                {
                    if (charSprite != null)
                    {
                        portraitImage.sprite = charSprite;
                        portraitImage.color = Color.white;
                        portraitImage.preserveAspect = true;
                    }
                    else
                    {
                        portraitImage.sprite = null;
                        portraitImage.color = isInvestigador ? new Color(0.22f, 0.25f, 0.32f, 1f) : characterGrayColor;
                    }
                }
                if (portraitLabelText != null)
                {
                    bool showLabel = (charSprite == null);
                    portraitLabelText.gameObject.SetActive(showLabel);
                    portraitLabelText.text = showLabel ? (isInvestigador ? "DETECTIVE\nVARGAS" : $"RETRATO\n[GRIS]\n{speakerName}") : string.Empty;
                }
                if (speakerNameText != null)
                {
                    speakerNameText.text = speakerName;
                    speakerNameText.color = isInvestigador
                        ? new Color(0.95f, 0.78f, 0.36f, 1f)
                        : new Color(0.88f, 0.88f, 0.90f, 1f);
                }
                break;

            case DialogueSpeakerType.SkillVoice:
                Color voiceColor = GetSkillVoiceColor(skillVoice);
                string voiceLabel = IntroSlideshowManager.GetSkillVoiceDisplayName(skillVoice);
                Sprite voiceSprite = GetSkillVoicePortrait(skillVoice);

                if (portraitImage != null)
                {
                    if (voiceSprite != null)
                    {
                        portraitImage.sprite = voiceSprite;
                        portraitImage.color = Color.white;
                        portraitImage.preserveAspect = false;
                    }
                    else
                    {
                        portraitImage.sprite = null;
                        portraitImage.color = voiceColor;
                    }
                }
                if (portraitLabelText != null)
                {
                    bool showLabel = (voiceSprite == null);
                    portraitLabelText.gameObject.SetActive(showLabel);
                    portraitLabelText.text = showLabel ? $"VOZ\n[{voiceLabel}]" : string.Empty;
                }
                if (speakerNameText != null)
                {
                    speakerNameText.text = speakerName;
                    speakerNameText.color = voiceColor;
                }
                break;
        }
    }

    public Sprite GetSkillVoicePortrait(SkillVoiceType voice)
    {
        switch (voice)
        {
            case SkillVoiceType.Corporeo:
                return corporeoPortrait;
            case SkillVoiceType.Intelecto:
                return intelectoPortrait;
            case SkillVoiceType.Motricidad:
                return motricidadPortrait;
            case SkillVoiceType.MundoInterno:
                return mundoInternoPortrait;
            default:
                return null;
        }
    }

    private Color GetSkillVoiceColor(SkillVoiceType voice)
    {
        switch (voice)
        {
            case SkillVoiceType.Corporeo:
                return corporeoColor;
            case SkillVoiceType.Intelecto:
                return intelectoColor;
            case SkillVoiceType.Motricidad:
                return motricidadColor;
            case SkillVoiceType.MundoInterno:
                return mundoInternoColor;
            default:
                return Color.white;
        }
    }

    private bool WasAdvancePressedThisFrame()
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
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            return true;
        }
#endif

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            GameObject hovered = EventSystem.current.currentSelectedGameObject;
            if (hovered != null && hovered.GetComponent<Button>() != null)
                return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }
#endif
        return false;
    }
}
