using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Menú subsecuente de fin de combate y subida de nivel para el Detective Héctor Vargas.
/// Muestra su expediente policial, retrato noir, estadísticas actualizadas con aumentos automáticos,
/// y barra de experiencia. Todo utiliza la fuente pixel art oficial (Pixelzone_TMP) con una
/// presentación minimalista, clara y directa.
/// </summary>
public class DetectiveLevelUpAndStatsUI : MonoBehaviour
{
    private static DetectiveLevelUpAndStatsUI _instance;

    [Header("Referencias a Datos")]
    public DetectiveStats detectiveStats;

    [Header("Configuración Visual")]
    public TMP_FontAsset gamePixelFont;
    public Color headerGold = new Color(1f, 0.85f, 0.35f, 1f);
    public Color textCyan = new Color(0.55f, 0.85f, 1f, 1f);
    public Color panelBorderColor = new Color(0.24f, 0.55f, 0.82f, 1f);
    public Color darkBackground = new Color(0.06f, 0.07f, 0.09f, 0.96f);
    public Color cardBorderColor = new Color(0.20f, 0.35f, 0.50f, 0.85f);
    public Color cardInnerColor = new Color(0.08f, 0.09f, 0.12f, 0.95f);

    public GameObject modalPanelRoot;
    public Image portraitImage;
    public TextMeshProUGUI titleHeaderTMP;
    public TextMeshProUGUI subHeaderTMP;
    public RectTransform expFillRect;
    public TextMeshProUGUI expTextTMP;
    public TextMeshProUGUI weaponStatusTMP;
    public TextMeshProUGUI statsReportTMP;

    public Button continueButton;
    public TextMeshProUGUI continueButtonTMP;

    private readonly List<TextMeshProUGUI> allTextComponents = new List<TextMeshProUGUI>();
    private Action onCompleteCallback;
    private bool isOpen = false;
    private int openedFrame = 0;
    private Sprite loadedPortraitSprite;

    public bool IsOpen => isOpen && modalPanelRoot != null && modalPanelRoot.activeInHierarchy;

    public static DetectiveLevelUpAndStatsUI Instance => _instance;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        InitializeIfNeeded();
    }

    public static DetectiveLevelUpAndStatsUI EnsureInScene()
    {
        if (_instance != null)
            return _instance;

        DetectiveLevelUpAndStatsUI existing = FindAnyObjectByType<DetectiveLevelUpAndStatsUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            existing.InitializeIfNeeded();
            return existing;
        }

        GameObject go = new GameObject("DetectiveLevelUpAndStatsUI_Canvas");
        DetectiveLevelUpAndStatsUI ui = go.AddComponent<DetectiveLevelUpAndStatsUI>();
        _instance = ui;
        ui.InitializeIfNeeded();
        return ui;
    }

    public void InitializeIfNeeded()
    {
        if (detectiveStats == null)
        {
            detectiveStats = DetectiveStats.EnsureInScene();
        }

        FetchFontAndPortrait();

        if (modalPanelRoot == null || statsReportTMP == null)
        {
            if (modalPanelRoot != null)
            {
                DestroyImmediate(modalPanelRoot);
                modalPanelRoot = null;
            }
            BuildUIHierarchy();
        }
    }

    private void FetchFontAndPortrait()
    {
        if (gamePixelFont == null)
        {
            BattleManager bm = FindAnyObjectByType<BattleManager>();
            if (bm != null && bm.playerHPText != null && bm.playerHPText.font != null)
            {
                gamePixelFont = bm.playerHPText.font;
            }

            if (gamePixelFont == null)
            {
                PlayerLevelUpAndStatsUI plUI = FindAnyObjectByType<PlayerLevelUpAndStatsUI>();
                if (plUI != null && plUI.customFont != null)
                {
                    gamePixelFont = plUI.customFont;
                }
            }

            if (gamePixelFont == null)
            {
                gamePixelFont = RuntimeAssetCatalog.GetTMPFont("Assets/Fonts/Pixelzone_TMP.asset");
            }
            if (gamePixelFont == null)
            {
                gamePixelFont = Resources.Load<TMP_FontAsset>("Pixelzone_TMP");
            }
            if (gamePixelFont == null)
            {
                gamePixelFont = PlayerHealthBarUI.GetCleanPixelFont();
            }
        }

        if (loadedPortraitSprite == null)
        {
            loadedPortraitSprite = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Detective.png");
            if (loadedPortraitSprite == null)
            {
                loadedPortraitSprite = RuntimeAssetCatalog.GetSprite("Assets/Sprites/Detective/detective_stand_down.png");
            }
        }
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount <= openedFrame) return;

        bool pressContinue = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                pressContinue = true;
            }
        }
#endif

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
        {
            pressContinue = true;
        }

        if (pressContinue)
        {
            OnContinueClicked();
        }
    }

    /// <summary>
    /// Abre la pantalla subsecuente con el expediente del Detective al ganar un combate.
    /// </summary>
    public void OpenForCombatReport(DetectiveStats stats, int levelsGained, int xpEarned, List<string> levelUpLogs, Action onComplete)
    {
        if (stats != null) detectiveStats = stats;
        InitializeIfNeeded();

        onCompleteCallback = onComplete;
        openedFrame = Time.frameCount;
        isOpen = true;

        if (modalPanelRoot != null)
        {
            modalPanelRoot.SetActive(true);
        }

        RefreshUI(levelsGained, xpEarned, levelUpLogs);
    }

    public void RefreshUI(int levelsGained = 0, int xpEarned = 0, List<string> levelUpLogs = null)
    {
        if (detectiveStats == null)
        {
            detectiveStats = DetectiveStats.EnsureInScene();
            if (detectiveStats == null) return;
        }

        FetchFontAndPortrait();

        // 1. Título principal
        if (titleHeaderTMP != null)
        {
            titleHeaderTMP.font = gamePixelFont;
            titleHeaderTMP.text = "DETECTIVE HÉCTOR VARGAS";
            titleHeaderTMP.fontSize = 36;
            titleHeaderTMP.color = headerGold;
            PlayerHealthBarUI.EnsureCleanPixelFont(titleHeaderTMP);
        }

        // 2. Subcabecera / Nivel y EXP ganada
        if (subHeaderTMP != null)
        {
            subHeaderTMP.font = gamePixelFont;
            if (levelsGained > 0)
            {
                subHeaderTMP.text = $"¡SUBIDA DE NIVEL! (NIVEL {detectiveStats.Nivel})   |   +{xpEarned} EXP";
                subHeaderTMP.color = headerGold;
            }
            else
            {
                subHeaderTMP.text = $"NIVEL {detectiveStats.Nivel}   |   +{xpEarned} EXP";
                subHeaderTMP.color = textCyan;
            }
            subHeaderTMP.fontSize = 24;
            PlayerHealthBarUI.EnsureCleanPixelFont(subHeaderTMP);
        }

        // 3. Barra de Experiencia
        if (expFillRect != null)
        {
            float ratio = Mathf.Clamp01((float)detectiveStats.EXP_Actual / Mathf.Max(1, detectiveStats.EXP_Requerida));
            expFillRect.anchorMax = new Vector2(ratio, 1f);
        }
        if (expTextTMP != null)
        {
            expTextTMP.font = gamePixelFont;
            expTextTMP.text = $"EXP: {detectiveStats.EXP_Actual} / {detectiveStats.EXP_Requerida}";
            expTextTMP.fontSize = 20;
            PlayerHealthBarUI.EnsureCleanPixelFont(expTextTMP);
        }

        // 4. Retrato
        if (portraitImage != null && loadedPortraitSprite != null)
        {
            portraitImage.sprite = loadedPortraitSprite;
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
        }

        // 5. Estado de Munición bajo el retrato
        if (weaponStatusTMP != null)
        {
            weaponStatusTMP.font = gamePixelFont;
            weaponStatusTMP.text = $"Tambor .38: {detectiveStats.municionActual} / {detectiveStats.municionMaxima} Balas";
            weaponStatusTMP.fontSize = 22;
            weaponStatusTMP.color = headerGold;
            PlayerHealthBarUI.EnsureCleanPixelFont(weaponStatusTMP);
        }

        // 6. Estadísticas Actualizadas con Aumentos (Directo, grande y sin relleno)
        string hpUp = "";
        string atkUp = "";
        string defUp = "";
        string spdUp = "";

        if (levelUpLogs != null && levelUpLogs.Count > 0)
        {
            foreach (string log in levelUpLogs)
            {
                if (log.Contains("HP_Max") || log.Contains("HP")) hpUp = "  <color=#52E06B>(+5)</color>";
                if (log.Contains("Ataque_Arma") || log.Contains("Arma") || log.Contains("Ataque")) atkUp = "  <color=#FFD84D>(+2)</color>";
                if (log.Contains("Defensa")) defUp = "  <color=#64B5F6>(+1)</color>";
                if (log.Contains("Velocidad")) spdUp = "  <color=#B388FF>(+1)</color>";
            }
        }

        if (statsReportTMP != null)
        {
            statsReportTMP.font = gamePixelFont;
            statsReportTMP.text =
                $"- Salud Máxima: <color=#FFFFFF>{detectiveStats.HP_Max} HP</color>{hpUp}\n\n" +
                $"- Ataque .38: <color=#FFFFFF>{detectiveStats.Ataque_Arma} ATQ</color>{atkUp}\n\n" +
                $"- Defensa: <color=#FFFFFF>{detectiveStats.Defensa} DEF</color>{defUp}\n\n" +
                $"- Velocidad: <color=#FFFFFF>{detectiveStats.Velocidad} VEL</color>{spdUp}\n\n" +
                $"- Evasión: <color=#FFFFFF>{detectiveStats.Probabilidad_Esquiva:0}%</color>";
            statsReportTMP.fontSize = 30;
            statsReportTMP.lineSpacing = 16f;
            PlayerHealthBarUI.EnsureCleanPixelFont(statsReportTMP);
        }

        // 7. Botón Continuar
        if (continueButtonTMP != null)
        {
            continueButtonTMP.font = gamePixelFont;
            continueButtonTMP.text = "CONTINUAR [ESPACIO]";
            continueButtonTMP.fontSize = 26;
            PlayerHealthBarUI.EnsureCleanPixelFont(continueButtonTMP);
        }

        // Asegurar que absolutamente todos los textos tengan la fuente asignada
        EnsureAllFontsAssigned();
    }

    public void EnsureAllFontsAssigned()
    {
        if (gamePixelFont == null) FetchFontAndPortrait();

        TextMeshProUGUI[] tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (tmps != null)
        {
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] != null)
                {
                    tmps[i].font = gamePixelFont;
                    tmps[i].fontStyle = FontStyles.Normal;
                    tmps[i].fontWeight = FontWeight.Regular;
                    if (gamePixelFont != null && gamePixelFont.material != null)
                    {
                        tmps[i].fontSharedMaterial = gamePixelFont.material;
                    }
                    PlayerHealthBarUI.EnsureCleanPixelFont(tmps[i]);
                }
            }
        }
    }

    public void OnContinueClicked()
    {
        isOpen = false;
        if (modalPanelRoot != null)
        {
            modalPanelRoot.SetActive(false);
        }

        onCompleteCallback?.Invoke();
        onCompleteCallback = null;
    }

    // =========================================================================
    // CONSTRUCCIÓN DINÁMICA DE LA JERARQUÍA UI MINIMALISTA
    // =========================================================================
    private void BuildUIHierarchy()
    {
        allTextComponents.Clear();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
        }
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // 1. Overlay sombrío noir
        modalPanelRoot = CreatePanel("DetectiveModalRoot", transform, Vector2.zero, Vector2.one, new Color(0.03f, 0.03f, 0.04f, 0.94f));

        // 2. Marco exterior del modal (Centrado y elegante)
        GameObject outerFrame = CreatePanel("OuterFrame", modalPanelRoot.transform, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f), panelBorderColor);

        // 3. Fondo interior oscuro
        GameObject innerBg = CreatePanel("InnerBackground", outerFrame.transform, new Vector2(0.003f, 0.005f), new Vector2(0.997f, 0.995f), darkBackground);

        // 4. Cabecera principal
        titleHeaderTMP = CreateText("TitleHeader", innerBg.transform, "DETECTIVE HÉCTOR VARGAS", 36, TextAlignmentOptions.Center, headerGold);
        SetAnchors(titleHeaderTMP.rectTransform, new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.97f));

        // 5. Subcabecera / Nivel
        subHeaderTMP = CreateText("SubHeader", innerBg.transform, "", 24, TextAlignmentOptions.Center, textCyan);
        SetAnchors(subHeaderTMP.rectTransform, new Vector2(0.04f, 0.81f), new Vector2(0.96f, 0.88f));

        // 6. Barra de Experiencia Enmarcada
        GameObject expBarBorder = CreatePanel("ExpBarBorder", innerBg.transform, new Vector2(0.10f, 0.73f), new Vector2(0.90f, 0.79f), new Color(0.20f, 0.35f, 0.50f, 1f));
        GameObject expBarInner = CreatePanel("ExpBarInner", expBarBorder.transform, new Vector2(0.003f, 0.08f), new Vector2(0.997f, 0.92f), new Color(0.08f, 0.09f, 0.12f, 1f));
        GameObject expBarFill = CreatePanel("ExpBarFill", expBarInner.transform, Vector2.zero, new Vector2(0.5f, 1f), new Color(0.25f, 0.65f, 0.95f, 1f));
        expFillRect = expBarFill.GetComponent<RectTransform>();

        expTextTMP = CreateText("ExpText", expBarInner.transform, "EXP", 20, TextAlignmentOptions.Center, Color.white);
        SetAnchors(expTextTMP.rectTransform, Vector2.zero, Vector2.one);

        // 7. Columna Izquierda: Retrato y Munición
        GameObject leftColBorder = CreatePanel("LeftColBorder", innerBg.transform, new Vector2(0.10f, 0.13f), new Vector2(0.46f, 0.71f), cardBorderColor);
        GameObject leftColInner = CreatePanel("LeftColInner", leftColBorder.transform, new Vector2(0.008f, 0.01f), new Vector2(0.992f, 0.99f), darkBackground);

        // Imagen de retrato (grande y nítida)
        GameObject portraitGO = CreatePanel("PortraitImage", leftColInner.transform, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.96f), Color.white);
        portraitImage = portraitGO.GetComponent<Image>();
        if (loadedPortraitSprite != null) portraitImage.sprite = loadedPortraitSprite;

        // Badge de Munición
        GameObject weaponBg = CreatePanel("WeaponBadge", leftColInner.transform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.15f), cardInnerColor);
        weaponStatusTMP = CreateText("WeaponText", weaponBg.transform, "", 22, TextAlignmentOptions.Center, headerGold);
        SetAnchors(weaponStatusTMP.rectTransform, Vector2.zero, Vector2.one);

        // 8. Columna Derecha: Estadísticas Grandes y Claras (Minimalista, sin títulos redundantes)
        GameObject statsBorder = CreatePanel("StatsBorder", innerBg.transform, new Vector2(0.50f, 0.13f), new Vector2(0.90f, 0.71f), cardBorderColor);
        GameObject statsInner = CreatePanel("StatsInner", statsBorder.transform, new Vector2(0.008f, 0.01f), new Vector2(0.992f, 0.99f), darkBackground);

        statsReportTMP = CreateText("StatsReport", statsInner.transform, "", 30, TextAlignmentOptions.MidlineLeft, Color.white);
        SetAnchors(statsReportTMP.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f));

        // 9. Botón Continuar Inferior
        GameObject btnBorder = CreatePanel("ContinueButtonBorder", innerBg.transform, new Vector2(0.30f, 0.03f), new Vector2(0.70f, 0.10f), new Color(0.24f, 0.86f, 0.42f, 1f));
        GameObject btnInner = CreatePanel("Inner", btnBorder.transform, new Vector2(0.005f, 0.06f), new Vector2(0.995f, 0.94f), new Color(0.09f, 0.14f, 0.11f, 1f));

        continueButton = btnBorder.AddComponent<Button>();
        continueButton.targetGraphic = btnInner.GetComponent<Image>();
        continueButton.onClick.AddListener(OnContinueClicked);

        continueButtonTMP = CreateText("ContinueLabel", btnInner.transform, "CONTINUAR [ESPACIO]", 26, TextAlignmentOptions.Center, new Color(0.55f, 0.95f, 0.65f, 1f));
        SetAnchors(continueButtonTMP.rectTransform, Vector2.zero, Vector2.one);

        modalPanelRoot.SetActive(false);
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        SetAnchors(rt, anchorMin, anchorMax);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return go;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        
        if (gamePixelFont == null)
        {
            FetchFontAndPortrait();
        }
        tmp.font = gamePixelFont;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Normal;
        tmp.fontWeight = FontWeight.Regular;
        if (gamePixelFont != null && gamePixelFont.material != null)
        {
            tmp.fontSharedMaterial = gamePixelFont.material;
        }
        tmp.characterSpacing = 0f;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);

        allTextComponents.Add(tmp);
        return tmp;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        if (rt == null) return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
