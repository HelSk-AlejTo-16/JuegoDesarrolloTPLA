using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla el menú de acciones exclusivo del Detective ("DetectiveActionMenu") y su tarjeta de estado
/// de aliado en la interfaz de combate.
/// Cuando es el turno del Detective:
/// - Oculta el menú estándar de Enrique (las 4 voces) y deshabilita/oculta estrictamente los botones/paneles
///   de "Crear Objetos" (Crafting) y "Cambiar/Usar Personalidades".
/// - Muestra las 3 acciones exclusivas del Detective:
///   1. ATACAR -> Submenú con 2 opciones: Cuerpo a Cuerpo (sin costo) y Arma de Fuego (mayor daño/crítico + munición).
///   2. HABILIDADES / EFECTOS -> Habilidades de estado propias del Detective (desvinculadas de Personalidades).
///   3. ESQUIVAR -> Acción defensiva que incrementa temporalmente su Probabilidad_Esquiva hasta su próximo turno.
/// </summary>
public class DetectiveActionMenu : MonoBehaviour
{
    public enum DetectiveSkillType
    {
        AnalizarPuntoDebil = 0,
        DisparoDeSupresion = 1,
        ConcentracionTactica = 2
    }

    [Header("1. Referencias Principales")]
    public BattleManager battleManager;
    public DetectiveStats detectiveStats;

    [Header("2. Tarjeta Visual del Aliado (Slot del Detective en Combate)")]
    public GameObject detectiveAllySlotRoot;
    public Image detectiveAvatarImage;
    public TextMeshProUGUI detectiveNameAndLevelText;
    public TextMeshProUGUI detectiveHPAndAmmoText;
    public Image detectiveHPFillImage;
    public TextMeshProUGUI detectiveStatusBadgesText;

    [Header("3. Menú Principal del Detective (3 Botones Exclusivos)")]
    public GameObject menuRootPanel;
    public GameObject mainButtonsContainer;
    public Button attackMainButton;
    public Button skillsMainButton;
    public Button dodgeMainButton;
    public TextMeshProUGUI turnHeaderLabel;

    [Header("4. Submenú 1: ATACAR (Cuerpo a Cuerpo vs Arma de Fuego)")]
    public GameObject attackSubmenuPanel;
    public Button meleeAttackButton;
    public TextMeshProUGUI meleeAttackLabel;
    public Button firearmAttackButton;
    public TextMeshProUGUI firearmAttackLabel;
    public Button attackBackButton;

    [Header("5. Submenú 2: HABILIDADES / EFECTOS (Exclusivas del Detective)")]
    public GameObject skillsSubmenuPanel;
    public Button skillAnalyzeButton;
    public TextMeshProUGUI skillAnalyzeLabel;
    public Button skillSuppressButton;
    public TextMeshProUGUI skillSuppressLabel;
    public Button skillFocusReloadButton;
    public TextMeshProUGUI skillFocusReloadLabel;
    public Button skillsBackButton;

    private TMP_FontAsset cachedFont;

    /// <summary>
    /// Garantiza compatibilidad retroactiva. El Detective ahora usa directamente el menú principal unificado,
    /// y muestra su tarjeta de estado de aliado en combate (Detective_AllyCombatSlot).
    /// </summary>
    public static DetectiveActionMenu EnsureInBattlePanel(BattleManager bm)
    {
        if (bm == null || bm.battlePanel == null)
        {
            return null;
        }

        DetectiveActionMenu existing = bm.battlePanel.GetComponentInChildren<DetectiveActionMenu>(true);
        if (existing == null)
        {
            existing = bm.battlePanel.AddComponent<DetectiveActionMenu>();
        }

        existing.battleManager = bm;
        existing.detectiveStats = bm.detectiveStats != null ? bm.detectiveStats : DetectiveStats.EnsureInScene();
        if (existing.menuRootPanel != null) existing.menuRootPanel.SetActive(false);
        return existing;
    }

    public void BuildUIIfNeeded()
    {
        if (battleManager == null)
        {
            battleManager = FindAnyObjectByType<BattleManager>();
        }
        if (detectiveStats == null)
        {
            detectiveStats = DetectiveStats.EnsureInScene();
        }

        ResolveFont();

        if (detectiveAllySlotRoot != null)
        {
            detectiveAllySlotRoot.SetActive(false);
        }

        if (menuRootPanel == null)
        {
            BuildDetectiveExclusiveActionMenu();
        }
    }

    private void ResolveFont()
    {
        if (cachedFont != null) return;

        if (battleManager != null && battleManager.battleLogText != null && battleManager.battleLogText.font != null)
        {
            cachedFont = battleManager.battleLogText.font;
            return;
        }
        cachedFont = RuntimeAssetCatalog.GetTMPFont("Assets/Fonts/Pixelzone_TMP.asset");
        if (cachedFont == null)
        {
            cachedFont = PlayerHealthBarUI.GetCleanPixelFont();
        }
    }

    /// <summary>
    /// Construye la tarjeta visual de aliado del Detective en la esquina superior izquierda / lateral del combate.
    /// </summary>
    private void BuildDetectiveAllyStatusCard()
    {
        if (detectiveAllySlotRoot != null)
        {
            detectiveAllySlotRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Construye el menú de acciones exclusivo del Detective sobre la franja inferior de acciones,
    /// ocultando las 4 voces de Enrique, el Crafting (Crear Objetos) y las Personalidades.
    /// </summary>
    private void BuildDetectiveExclusiveActionMenu()
    {
        menuRootPanel = new GameObject("DetectiveActionMenu_Panel", typeof(RectTransform), typeof(Image));
        menuRootPanel.transform.SetParent(transform, false);

        RectTransform rootRT = menuRootPanel.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.188f, 0.015f);
        rootRT.anchorMax = new Vector2(0.975f, 0.192f);
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image borderImg = menuRootPanel.GetComponent<Image>();
        borderImg.color = new Color(0.32f, 0.68f, 0.95f, 1f);

        GameObject innerGO = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
        innerGO.transform.SetParent(menuRootPanel.transform, false);
        RectTransform innerRT = innerGO.GetComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.004f, 0.03f);
        innerRT.anchorMax = new Vector2(0.996f, 0.97f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        innerGO.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.98f);

        // Cinta superior indicando el turno exclusivo del Detective y las restricciones activas
        turnHeaderLabel = CreateText(innerGO.transform, "DetectiveTurnHeader",
            new Vector2(0.015f, 0.76f), new Vector2(0.985f, 0.98f),
            "TURNO DEL DETECTIVE VARGAS — [MENÚ TÁCTICO POLICIAL | SIN PERSONALIDADES NI CRAFTING]",
            16f, new Color(0.55f, 0.86f, 1f, 1f), TextAlignmentOptions.Left);

        // 1. CONTENEDOR DE LOS 3 BOTONES PRINCIPALES (Atacar, Habilidades/Efectos, Esquivar)
        mainButtonsContainer = new GameObject("MainButtonsRow", typeof(RectTransform));
        mainButtonsContainer.transform.SetParent(innerGO.transform, false);
        RectTransform mainRowRT = mainButtonsContainer.GetComponent<RectTransform>();
        mainRowRT.anchorMin = new Vector2(0.015f, 0.06f);
        mainRowRT.anchorMax = new Vector2(0.985f, 0.74f);
        mainRowRT.offsetMin = Vector2.zero;
        mainRowRT.offsetMax = Vector2.zero;

        attackMainButton = CreateStyledButton(mainButtonsContainer.transform, "Btn_Atacar",
            new Vector2(0.0f, 0.0f), new Vector2(0.32f, 1.0f),
            new Color(0.85f, 0.30f, 0.25f, 1f),
            "1. ATACAR\n<size=14>Cuerpo a Cuerpo / Arma .38</size>",
            OpenAttackSubmenu, out _);

        skillsMainButton = CreateStyledButton(mainButtonsContainer.transform, "Btn_Habilidades",
            new Vector2(0.34f, 0.0f), new Vector2(0.66f, 1.0f),
            new Color(0.25f, 0.62f, 0.92f, 1f),
            "2. HABILIDADES / EFECTOS\n<size=14>Tácticas Policiales de Estado</size>",
            OpenSkillsSubmenu, out _);

        dodgeMainButton = CreateStyledButton(mainButtonsContainer.transform, "Btn_Esquivar",
            new Vector2(0.68f, 0.0f), new Vector2(1.0f, 1.0f),
            new Color(0.25f, 0.82f, 0.55f, 1f),
            "3. ESQUIVAR\n<size=14>+Probabilidad_Esquiva (1 Turno)</size>",
            OnDodgeButtonClicked, out _);

        // 2. SUBMENÚ ATACAR (2 Opciones: Cuerpo a Cuerpo / Arma de Fuego + Volver)
        attackSubmenuPanel = new GameObject("AttackSubmenuRow", typeof(RectTransform));
        attackSubmenuPanel.transform.SetParent(innerGO.transform, false);
        RectTransform atkRowRT = attackSubmenuPanel.GetComponent<RectTransform>();
        atkRowRT.anchorMin = new Vector2(0.015f, 0.06f);
        atkRowRT.anchorMax = new Vector2(0.985f, 0.74f);
        atkRowRT.offsetMin = Vector2.zero;
        atkRowRT.offsetMax = Vector2.zero;

        meleeAttackButton = CreateStyledButton(attackSubmenuPanel.transform, "Btn_MeleeAttack",
            new Vector2(0.0f, 0.0f), new Vector2(0.40f, 1.0f),
            new Color(0.82f, 0.48f, 0.22f, 1f),
            "CUERPO A CUERPO\n<size=14>Daño físico estándar (Sin costo)</size>",
            OnMeleeAttackClicked, out meleeAttackLabel);

        firearmAttackButton = CreateStyledButton(attackSubmenuPanel.transform, "Btn_FirearmAttack",
            new Vector2(0.415f, 0.0f), new Vector2(0.815f, 1.0f),
            new Color(0.92f, 0.26f, 0.26f, 1f),
            "ARMA DE FUEGO (.38)\n<size=14>Alto daño + Crítico (1 Bala)</size>",
            OnFirearmAttackClicked, out firearmAttackLabel);

        attackBackButton = CreateStyledButton(attackSubmenuPanel.transform, "Btn_AttackBack",
            new Vector2(0.83f, 0.0f), new Vector2(1.0f, 1.0f),
            new Color(0.45f, 0.48f, 0.54f, 1f),
            "VOLVER",
            ShowMainDetectiveButtons, out _);

        // 3. SUBMENÚ HABILIDADES / EFECTOS (Exclusivas del Detective + Volver)
        skillsSubmenuPanel = new GameObject("SkillsSubmenuRow", typeof(RectTransform));
        skillsSubmenuPanel.transform.SetParent(innerGO.transform, false);
        RectTransform sklRowRT = skillsSubmenuPanel.GetComponent<RectTransform>();
        sklRowRT.anchorMin = new Vector2(0.015f, 0.06f);
        sklRowRT.anchorMax = new Vector2(0.985f, 0.74f);
        sklRowRT.offsetMin = Vector2.zero;
        sklRowRT.offsetMax = Vector2.zero;

        skillAnalyzeButton = CreateStyledButton(skillsSubmenuPanel.transform, "Btn_SkillAnalyze",
            new Vector2(0.0f, 0.0f), new Vector2(0.27f, 1.0f),
            new Color(0.25f, 0.68f, 0.95f, 1f),
            "ANALIZAR PUNTO DÉBIL\n<size=13>Rompe Escudo / -DEF y +30% Daño</size>",
            () => OnSkillSelected(DetectiveSkillType.AnalizarPuntoDebil), out skillAnalyzeLabel);

        skillSuppressButton = CreateStyledButton(skillsSubmenuPanel.transform, "Btn_SkillSuppress",
            new Vector2(0.28f, 0.0f), new Vector2(0.55f, 1.0f),
            new Color(0.88f, 0.62f, 0.18f, 1f),
            "DISPARO DE SUPRESIÓN\n<size=13>Control: -ATQ / -VEL (1 Bala)</size>",
            () => OnSkillSelected(DetectiveSkillType.DisparoDeSupresion), out skillSuppressLabel);

        skillFocusReloadButton = CreateStyledButton(skillsSubmenuPanel.transform, "Btn_SkillFocus",
            new Vector2(0.56f, 0.0f), new Vector2(0.83f, 1.0f),
            new Color(0.32f, 0.82f, 0.62f, 1f),
            "CONCENTRACIÓN TÁCTICA\n<size=13>Buff: +ATQ, +ESQ y Recarga +3 Balas</size>",
            () => OnSkillSelected(DetectiveSkillType.ConcentracionTactica), out skillFocusReloadLabel);

        skillsBackButton = CreateStyledButton(skillsSubmenuPanel.transform, "Btn_SkillsBack",
            new Vector2(0.84f, 0.0f), new Vector2(1.0f, 1.0f),
            new Color(0.45f, 0.48f, 0.54f, 1f),
            "VOLVER",
            ShowMainDetectiveButtons, out _);

        attackSubmenuPanel.SetActive(false);
        skillsSubmenuPanel.SetActive(false);
        menuRootPanel.SetActive(false);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string content, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (cachedFont != null) tmp.font = cachedFont;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
        return tmp;
    }

    private Button CreateStyledButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color borderColor, string text, UnityEngine.Events.UnityAction onClickAction, out TextMeshProUGUI labelOut)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image borderImg = btnGO.GetComponent<Image>();
        borderImg.color = borderColor;

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(btnGO.transform, false);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0.02f, 0.06f);
        fillRT.anchorMax = new Vector2(0.98f, 0.94f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.color = new Color(0.07f, 0.09f, 0.14f, 0.98f);

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = fillImg;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClickAction);

        labelOut = CreateText(fillGO.transform, "Label",
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
            text, 16f, Color.white, TextAlignmentOptions.Center);

        return btn;
    }

    /// <summary>
    /// Actualiza la visibilidad y los datos del slot de aliado del Detective en la pantalla de combate.
    /// </summary>
    public void RefreshAllySlotUI(bool showSlot = true)
    {
        if (detectiveAllySlotRoot != null)
        {
            detectiveAllySlotRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Muestra el menú de acciones exclusivo del Detective y aplica las restricciones estrictas:
    /// - Oculta los 4 botones de voces de Enrique.
    /// - Cierra y oculta por completo "Crear Objetos" (Crafting / Intelecto) y "Cambiar/Usar Personalidades" (Mundo Interno).
    /// - Oculta el botón de Inventario/Armas del protagonista durante el turno del Detective.
    /// </summary>
    public void ShowDetectiveTurnMenu()
    {
        BuildUIIfNeeded();

        if (battleManager != null)
        {
            // Ocultar el menú estándar de las 4 voces del protagonista
            if (battleManager.corporeoButton != null) battleManager.corporeoButton.gameObject.SetActive(false);
            if (battleManager.intelectoButton != null) battleManager.intelectoButton.gameObject.SetActive(false);
            if (battleManager.motricidadButton != null) battleManager.motricidadButton.gameObject.SetActive(false);
            if (battleManager.mundoInternoButton != null) battleManager.mundoInternoButton.gameObject.SetActive(false);

            // Restricción estricta: asegurar que Crafting (Intelecto) y Personalidades (Mundo Interno) estén cerrados y ocultos
            battleManager.ForceHideProtagonistExclusiveSubmenusForDetective();
        }

        RefreshButtonLabels();
        ShowMainDetectiveButtons();

        if (menuRootPanel != null)
        {
            menuRootPanel.SetActive(true);
        }

        RefreshAllySlotUI(true);
    }

    /// <summary>
    /// Oculta el menú exclusivo del Detective y restaura la visibilidad de los botones del protagonista.
    /// </summary>
    public void HideDetectiveTurnMenu(bool restoreProtagonistButtons)
    {
        if (menuRootPanel != null)
        {
            menuRootPanel.SetActive(false);
        }
        if (attackSubmenuPanel != null)
        {
            attackSubmenuPanel.SetActive(false);
        }
        if (skillsSubmenuPanel != null)
        {
            skillsSubmenuPanel.SetActive(false);
        }

        if (restoreProtagonistButtons && battleManager != null)
        {
            if (battleManager.corporeoButton != null) battleManager.corporeoButton.gameObject.SetActive(true);
            if (battleManager.intelectoButton != null) battleManager.intelectoButton.gameObject.SetActive(true);
            if (battleManager.motricidadButton != null) battleManager.motricidadButton.gameObject.SetActive(true);
            if (battleManager.mundoInternoButton != null) battleManager.mundoInternoButton.gameObject.SetActive(true);
            battleManager.SetProtagonistInventoryButtonVisible(true);
        }
    }

    public void RefreshButtonLabels()
    {
        if (detectiveStats == null)
        {
            detectiveStats = DetectiveStats.EnsureInScene();
        }
        if (detectiveStats == null) return;

        int meleeDmg = detectiveStats.GetTotalMeleeAttack();
        int gunDmg = detectiveStats.GetTotalFirearmAttack();

        if (meleeAttackLabel != null)
        {
            meleeAttackLabel.text = $"CUERPO A CUERPO\n<size=13>Culatazo ({meleeDmg} ATQ | Sin costo)</size>";
        }

        if (firearmAttackLabel != null)
        {
            bool canFire = detectiveStats.CanFireWeapon(out _);
            string ammoColor = canFire ? "#FFD84D" : "#FF6666";
            firearmAttackLabel.text = $"ARMA DE FUEGO (.38)\n<size=13>{gunDmg} ATQ + Crítico | <color={ammoColor}>Balas: {detectiveStats.municionActual}/{detectiveStats.municionMaxima}</color></size>";
        }

        if (turnHeaderLabel != null)
        {
            turnHeaderLabel.text = $"TURNO DE {detectiveStats.detectiveName} (NV. {detectiveStats.Nivel}) — BALAS: {detectiveStats.municionActual}/{detectiveStats.municionMaxima} | ESQUIVA: {detectiveStats.GetEffectiveDodgeChance():0}%";
        }
    }

    public void ShowMainDetectiveButtons()
    {
        if (battleManager != null) battleManager.PlayBotonEnCombateSfx();
        RefreshButtonLabels();
        if (mainButtonsContainer != null) mainButtonsContainer.SetActive(true);
        if (attackSubmenuPanel != null) attackSubmenuPanel.SetActive(false);
        if (skillsSubmenuPanel != null) skillsSubmenuPanel.SetActive(false);
    }

    private void OpenAttackSubmenu()
    {
        if (battleManager != null) battleManager.PlayBotonEnCombateSfx();
        RefreshButtonLabels();
        if (mainButtonsContainer != null) mainButtonsContainer.SetActive(false);
        if (attackSubmenuPanel != null) attackSubmenuPanel.SetActive(true);
        if (skillsSubmenuPanel != null) skillsSubmenuPanel.SetActive(false);
    }

    private void OpenSkillsSubmenu()
    {
        if (battleManager != null) battleManager.PlayBotonEnCombateSfx();
        RefreshButtonLabels();
        if (mainButtonsContainer != null) mainButtonsContainer.SetActive(false);
        if (attackSubmenuPanel != null) attackSubmenuPanel.SetActive(false);
        if (skillsSubmenuPanel != null) skillsSubmenuPanel.SetActive(true);
    }

    private void OnMeleeAttackClicked()
    {
        if (battleManager == null) return;
        battleManager.ExecuteDetectiveMeleeAttack();
    }

    private void OnFirearmAttackClicked()
    {
        if (battleManager == null) return;
        battleManager.ExecuteDetectiveFirearmAttack();
    }

    private void OnSkillSelected(DetectiveSkillType skillType)
    {
        if (battleManager == null) return;
        battleManager.ExecuteDetectiveSkill(skillType);
    }

    private void OnDodgeButtonClicked()
    {
        if (battleManager == null) return;
        battleManager.ExecuteDetectiveDodgeAction();
    }
}
