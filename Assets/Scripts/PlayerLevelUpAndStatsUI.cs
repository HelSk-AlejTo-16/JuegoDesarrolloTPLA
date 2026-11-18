using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sistema de Interfaz para:
/// 1. El Menú de Estadísticas de las 4 Habilidades/Voces (accesible en cualquier momento con TAB / C o el botón HUD).
/// 2. La Pantalla de Subida de Nivel al terminar un combate, donde el jugador elige qué habilidad subir para hacerla más poderosa.
/// </summary>
public class PlayerLevelUpAndStatsUI : MonoBehaviour
{
    private static PlayerLevelUpAndStatsUI _instance;
    public static PlayerLevelUpAndStatsUI Instance => _instance;

    [Header("Referencias de Datos")]
    public PlayerStats playerStats;
    public BattleManager battleManager;
    public PlayerController2D playerController;
    public TMP_FontAsset customFont;

    [Header("Retratos de las 4 Voces")]
    public Sprite corporeoPortrait;
    public Sprite intelectoPortrait;
    public Sprite motricidadPortrait;
    public Sprite mundoInternoPortrait;

    [Header("Colores de Identidad")]
    public Color corporeoColor = new Color(0.82f, 0.22f, 0.22f, 1f);
    public Color intelectoColor = new Color(0.20f, 0.56f, 0.90f, 1f);
    public Color motricidadColor = new Color(0.90f, 0.68f, 0.16f, 1f);
    public Color mundoInternoColor = new Color(0.64f, 0.28f, 0.86f, 1f);
    public Color goldHighlightColor = new Color(0.98f, 0.80f, 0.24f, 1f);

    [Header("Elementos UI Generados")]
    public GameObject hudButtonRoot;
    public Button hudOpenStatsButton;
    public TextMeshProUGUI hudButtonLabel;
    public Image hudButtonBorder;

    public GameObject modalPanelRoot;
    public TextMeshProUGUI headerTitleText;
    public TextMeshProUGUI subHeaderSummaryText;
    public TextMeshProUGUI feedbackBannerText;
    public RectTransform xpBarFillRect;
    public TextMeshProUGUI xpBarText;
    public Button closeOrContinueButton;
    public TextMeshProUGUI closeOrContinueButtonText;
    public Image closeOrContinueBorderImage;

    [Serializable]
    public class SkillCardUI
    {
        public SkillVoiceType voiceType;
        public Image borderImage;
        public Image portraitImage;
        public TextMeshProUGUI portraitFallbackText;
        public TextMeshProUGUI titleAndStatText;
        public TextMeshProUGUI rankBadgeText;
        public TextMeshProUGUI currentEffectsText;
        public TextMeshProUGUI nextUpgradePreviewText;
        public Button upgradeButton;
        public Image upgradeButtonBorder;
        public TextMeshProUGUI upgradeButtonLabel;
    }

    public SkillCardUI[] skillCards = new SkillCardUI[4];

    public bool IsOpen => modalPanelRoot != null && modalPanelRoot.activeSelf;

    private bool isPostCombatMode = false;
    private int lastCombatLevelsGained = 0;
    private int lastCombatXpEarned = 0;
    private Action onLevelUpCompleteCallback;
    private bool wasPlayerMovementAllowedBeforeOpen = true;
    private int openedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeOnSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckAndEnsureInActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndEnsureInActiveScene();
    }

    public static void CheckAndEnsureInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid()) return;
        if (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu"))
            return;
        if (FindAnyObjectByType<PlayerStats>() == null)
            return;

        EnsureInScene();
    }

    public static PlayerLevelUpAndStatsUI EnsureInScene()
    {
        if (_instance != null)
            return _instance;

        PlayerLevelUpAndStatsUI existing = FindAnyObjectByType<PlayerLevelUpAndStatsUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            existing.InitializeIfNeeded();
            return existing;
        }

        GameObject go = new GameObject("PlayerLevelUpAndStatsUI_Canvas");
        PlayerLevelUpAndStatsUI ui = go.AddComponent<PlayerLevelUpAndStatsUI>();
        _instance = ui;
        ui.InitializeIfNeeded();
        return ui;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        InitializeIfNeeded();
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnStatsChanged -= RefreshUI;
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void InitializeIfNeeded()
    {
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include);
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController2D>();

        FetchPortraitsAndFontIfMissing();

        if (modalPanelRoot == null)
        {
            BuildCompleteUI();
        }

        SetupButtonListeners();

        if (playerStats != null)
        {
            playerStats.OnStatsChanged -= RefreshUI;
            playerStats.OnStatsChanged += RefreshUI;
        }

        RefreshUI();
    }

    /// <summary>
    /// Re-vincula los listeners de botones estáticos en InitializeIfNeeded() para que sigan funcionando
    /// incluso cuando el Canvas y modalPanelRoot ya vienen serializados en la escena.
    /// </summary>
    private void SetupButtonListeners()
    {
        if (closeOrContinueButton != null)
        {
            closeOrContinueButton.onClick.RemoveAllListeners();
            closeOrContinueButton.onClick.AddListener(OnCloseOrContinueClicked);
        }

        if (skillCards == null) return;

        for (int i = 0; i < skillCards.Length; i++)
        {
            SkillCardUI card = skillCards[i];
            if (card == null || card.upgradeButton == null) continue;

            SkillVoiceType capturedVoice = card.voiceType;
            card.upgradeButton.onClick.RemoveAllListeners();
            card.upgradeButton.onClick.AddListener(() => OnUpgradeSkillClicked(capturedVoice));
        }
    }

    private void FetchPortraitsAndFontIfMissing()
    {
        if (customFont == null)
        {
            customFont = PlayerHealthBarUI.GetCleanPixelFont();
        }

        if (battleManager != null)
        {
            if (corporeoPortrait == null) corporeoPortrait = battleManager.corporeoPortrait;
            if (intelectoPortrait == null) intelectoPortrait = battleManager.intelectoPortrait;
            if (motricidadPortrait == null) motricidadPortrait = battleManager.motricidadPortrait;
            if (mundoInternoPortrait == null) mundoInternoPortrait = battleManager.mundoInternoPortrait;
        }

        ExplorationDialogueManager exp = FindAnyObjectByType<ExplorationDialogueManager>(FindObjectsInactive.Include);
        if (exp != null)
        {
            if (corporeoPortrait == null) corporeoPortrait = exp.corporeoPortrait;
            if (intelectoPortrait == null) intelectoPortrait = exp.intelectoPortrait;
            if (motricidadPortrait == null) motricidadPortrait = exp.motricidadPortrait;
            if (mundoInternoPortrait == null) mundoInternoPortrait = exp.mundoInternoPortrait;
        }
    }

    private void Update()
    {
        UpdateHudButtonVisibility();

        if (Time.frameCount == openedFrame)
            return;

        if (IsOpen)
        {
            if (!isPostCombatMode || (playerStats != null && playerStats.availableSkillPoints <= 0))
            {
                if (WasToggleMenuKeyPressed() || WasEscapeKeyPressed() || (isPostCombatMode && WasConfirmKeyPressed()))
                {
                    OnCloseOrContinueClicked();
                }
            }
            return;
        }

        // Permitir abrir el menú de estadísticas con TAB o C cuando estamos explorando (o durante el turno del jugador en combate)
        if (WasToggleMenuKeyPressed())
        {
            bool inDialogue = (ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive);
            bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);

            if (!inDialogue && (!inCombat || battleManager.state == BattleState.PLAYERTURN))
            {
                if (PlayerInventoryAndWeaponsUI.Instance != null && PlayerInventoryAndWeaponsUI.Instance.IsOpen)
                {
                    PlayerInventoryAndWeaponsUI.Instance.CloseInventoryMenu();
                }
                OpenStatsMenu();
            }
        }
    }

    private void UpdateHudButtonVisibility()
    {
        // El botón de nivel/estadísticas en pantalla ha sido retirado a petición del usuario.
        if (hudButtonRoot != null && hudButtonRoot.activeSelf)
        {
            hudButtonRoot.SetActive(false);
        }
    }

    // =========================================================================
    // APERTURA DEL MENÚ DE ESTADÍSTICAS Y DE LA PANTALLA POST-COMBATE
    // =========================================================================

    /// <summary>
    /// Abre el menú de estadísticas para consultar (y mejorar si hay puntos) las 4 habilidades.
    /// </summary>
    public void OpenStatsMenu()
    {
        InitializeIfNeeded();
        if (IsOpen) return;

        isPostCombatMode = false;
        lastCombatLevelsGained = 0;
        lastCombatXpEarned = 0;
        onLevelUpCompleteCallback = null;
        openedFrame = Time.frameCount;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController2D>();

        bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);
        if (!inCombat && playerController != null)
        {
            wasPlayerMovementAllowedBeforeOpen = playerController.canMove;
            playerController.SetCanMove(false);
        }

        if (feedbackBannerText != null)
        {
            if (playerStats != null && playerStats.availableSkillPoints > 0)
            {
                feedbackBannerText.text = $"TIENES {playerStats.availableSkillPoints} PUNTO(S) DE HABILIDAD DISPONIBLE(S). ¡Elige una voz para mejorarla!";
                feedbackBannerText.color = goldHighlightColor;
            }
            else
            {
                feedbackBannerText.text = "Consulta el poder actual de tus 4 voces. Solo puedes subir una habilidad cuando subes de nivel en combate.";
                feedbackBannerText.color = new Color(0.75f, 0.78f, 0.84f, 1f);
            }
        }

        modalPanelRoot.SetActive(true);
        RefreshUI();
    }

    /// <summary>
    /// Abre automáticamente el menú al finalizar cada combate ganado.
    /// - Si subió de nivel (levelsGained > 0): exige elegir qué habilidad sube antes de continuar.
    /// - Si no subió de nivel (levelsGained == 0): muestra que acabó el combate, la EXP ganada y las estadísticas de las 4 habilidades.
    /// </summary>
    public void OpenForCombatLevelUp(PlayerStats stats, int levelsGained, int xpEarned, Action onComplete)
    {
        if (stats != null) playerStats = stats;
        InitializeIfNeeded();

        isPostCombatMode = true;
        lastCombatLevelsGained = levelsGained;
        lastCombatXpEarned = xpEarned;
        onLevelUpCompleteCallback = onComplete;
        openedFrame = Time.frameCount;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayVictoryMenuMusic();
            if (levelsGained > 0)
                MusicManager.Instance.PlaySubidaDeNivelSfx();
            else
                MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }

        if (feedbackBannerText != null && playerStats != null)
        {
            int pts = playerStats.availableSkillPoints;
            DetectiveStats detStats = DetectiveStats.EnsureInScene();
            string partyTag = "";
            if (detStats != null && detStats.hasDetectiveInParty)
            {
                partyTag = detStats.IsAlive 
                    ? $" | <color=#90CAF9>DET. VARGAS:</color> Nivel {detStats.Nivel} ({detStats.HP_Actual}/{detStats.HP_Max} HP, {detStats.municionActual}/{detStats.municionMaxima} Balas)"
                    : $" | <color=#FF7070>DET. VARGAS:</color> Caído (Revivirá con 1 HP en próximo combate)";
            }

            if (levelsGained > 0 || pts > 0)
            {
                feedbackBannerText.text = $"¡VICTORIA! (+{xpEarned} EXP)";
                feedbackBannerText.color = goldHighlightColor;
            }
            else
            {
                feedbackBannerText.text = $"+{xpEarned} EXP";
                feedbackBannerText.color = new Color(0.55f, 0.92f, 0.68f, 1f);
            }
        }

        modalPanelRoot.SetActive(true);
        RefreshUI();
    }

    public void ToggleStatsMenu()
    {
        if (IsOpen)
        {
            if (!isPostCombatMode || (playerStats != null && playerStats.availableSkillPoints <= 0))
            {
                OnCloseOrContinueClicked();
            }
        }
        else
        {
            OpenStatsMenu();
        }
    }

    private void OnUpgradeSkillClicked(SkillVoiceType voice)
    {
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();

        if (playerStats == null || playerStats.availableSkillPoints <= 0)
            return;

        bool upgraded = playerStats.UpgradeSkill(voice);
        if (!upgraded)
            return;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlaySubidaDeNivelSfx();
        }

        if (feedbackBannerText != null)
        {
            switch (voice)
            {
                case SkillVoiceType.Corporeo:
                    feedbackBannerText.text = $"¡CORPÓREO SUBIÓ A {playerStats.corporeo}! Ahora infliges {playerStats.corporeo} de daño físico base y tu Vida Máxima subió a {playerStats.maxHP} HP.";
                    feedbackBannerText.color = new Color(1f, 0.45f, 0.45f, 1f);
                    break;
                case SkillVoiceType.Intelecto:
                    feedbackBannerText.text = $"¡INTELECTO SUBIÓ A {playerStats.intelecto}! Tus Molotovs hacen +{playerStats.GetIntelectoDamageBonus()} de daño y tus objetos curan +{playerStats.GetIntelectoHealBonus()} HP extra.";
                    feedbackBannerText.color = new Color(0.45f, 0.80f, 1f, 1f);
                    break;
                case SkillVoiceType.Motricidad:
                    feedbackBannerText.text = $"¡MOTRICIDAD SUBIÓ A {playerStats.motricidad}! Actúas antes en turno, tienes {playerStats.GetDisplayDodgePercentage()}% de esquive base y -{playerStats.GetDisplayGuardReductionPercentage()}% daño en guardia.";
                    feedbackBannerText.color = new Color(1f, 0.85f, 0.35f, 1f);
                    break;
                case SkillVoiceType.MundoInterno:
                    feedbackBannerText.text = $"¡MUNDO INTERNO SUBIÓ A {playerStats.mundoInterno}! Insultar baja -{playerStats.GetInsultarDebuffAmount()} ATQ, Animarse da +{playerStats.GetAnimarseBuffAmount()} ATQ y Delirio hace {playerStats.GetDelirioPsychicDamage()} daño psíquico.";
                    feedbackBannerText.color = new Color(0.82f, 0.55f, 1f, 1f);
                    break;
            }
        }

        if (battleManager != null)
        {
            battleManager.UpdateHealthUI();
        }

        RefreshUI();
    }

    public void OnCloseOrContinueClicked()
    {
        if (isPostCombatMode && playerStats != null && playerStats.availableSkillPoints > 0)
        {
            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"¡Aún tienes {playerStats.availableSkillPoints} punto(s) de habilidad! Haz clic en [SUBIR HABILIDAD] en una de las 4 voces antes de continuar.";
                feedbackBannerText.color = goldHighlightColor;
            }
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayBotonEnCombateSfx();
            }
            return;
        }

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }

        if (modalPanelRoot != null)
        {
            modalPanelRoot.SetActive(false);
        }

        bool wasPostCombat = isPostCombatMode;
        isPostCombatMode = false;

        Action callback = onLevelUpCompleteCallback;
        onLevelUpCompleteCallback = null;

        if (wasPostCombat && callback != null)
        {
            callback.Invoke();
            return;
        }

        bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);
        if (!inCombat && playerController != null && wasPlayerMovementAllowedBeforeOpen)
        {
            playerController.SetCanMove(true);
        }
    }

    // =========================================================================
    // ACTUALIZACIÓN DE TEXTOS Y TARJETAS DE LAS 4 HABILIDADES
    // =========================================================================

    public void RefreshUI()
    {
        if (playerStats == null)
        {
            playerStats = FindAnyObjectByType<PlayerStats>();
            if (playerStats == null) return;
        }

        FetchPortraitsAndFontIfMissing();

        int pts = playerStats.availableSkillPoints;

        // 1. Actualizar botón HUD superior derecho
        if (hudButtonLabel != null)
        {
            if (pts > 0)
            {
                hudButtonLabel.text = $"[TAB] HABILIDADES  |  NV. {playerStats.level}  <color=#FFD84D>(+{pts} PTO)</color>";
                if (hudButtonBorder != null) hudButtonBorder.color = goldHighlightColor;
            }
            else
            {
                hudButtonLabel.text = $"[TAB] ESTADÍSTICAS  |  NV. {playerStats.level} ({playerStats.currentXP}/{playerStats.xpToNextLevel} EXP)";
                if (hudButtonBorder != null) hudButtonBorder.color = new Color(0.38f, 0.42f, 0.50f, 1f);
            }
        }

        if (!IsOpen) return;

        // 2. Actualizar Cabecera del Modal
        if (headerTitleText != null)
        {
            if (isPostCombatMode)
            {
                if (lastCombatLevelsGained > 0 || pts > 0)
                {
                    headerTitleText.text = "¡SUBIDA DE NIVEL! — ¡VIDA RESTAURADA AL 100%!";
                    headerTitleText.color = goldHighlightColor;
                }
                else
                {
                    headerTitleText.text = "¡VICTORIA!";
                    headerTitleText.color = new Color(0.45f, 0.95f, 0.62f, 1f);
                }
            }
            else
            {
                headerTitleText.text = "ESTADÍSTICAS Y HABILIDADES";
                headerTitleText.color = new Color(0.94f, 0.92f, 0.88f, 1f);
            }
        }

        if (subHeaderSummaryText != null)
        {
            string ptsTag = pts > 0
                ? $"<color=#FFD84D>PUNTOS: {pts}</color>"
                : "<color=#A0A6B2>0 PUNTOS</color>";

            DetectiveStats detStats = DetectiveStats.EnsureInScene();
            string partyStatus = "";
            if (detStats != null && detStats.hasDetectiveInParty)
            {
                partyStatus = detStats.IsAlive
                    ? $"   |   VARGAS: <color=#52E06B>{detStats.HP_Actual}/{detStats.HP_Max} HP</color>"
                    : $"   |   VARGAS: <color=#FF5555>0/{detStats.HP_Max} HP</color>";
            }

            subHeaderSummaryText.text = $"ENRIQUE: <color=#52E06B>{playerStats.currentHP}/{playerStats.maxHP} HP</color>   |   {ptsTag}{partyStatus}";
        }

        if (xpBarFillRect != null)
        {
            float xpRatio = Mathf.Clamp01((float)playerStats.currentXP / Mathf.Max(1, playerStats.xpToNextLevel));
            xpBarFillRect.anchorMax = new Vector2(xpRatio, 1f);
        }
        if (xpBarText != null)
        {
            xpBarText.text = $"EXP: {playerStats.currentXP} / {playerStats.xpToNextLevel}";
        }

        // 3. Actualizar las 4 Tarjetas de Habilidades
        for (int i = 0; i < skillCards.Length; i++)
        {
            SkillCardUI card = skillCards[i];
            if (card == null) continue;

            UpdateSingleSkillCard(card, pts > 0);
        }

        // 4. Actualizar botón inferior de Continuar / Cerrar
        if (closeOrContinueButton != null && closeOrContinueButtonText != null)
        {
            if (isPostCombatMode)
            {
                bool canContinue = (pts <= 0);
                closeOrContinueButton.interactable = canContinue;
                closeOrContinueButtonText.text = canContinue
                    ? "CONTINUAR [ESPACIO]"
                    : $"ASIGNA EL PUNTO DE HABILIDAD ({pts} PENDIENTE)";

                if (closeOrContinueBorderImage != null)
                {
                    closeOrContinueBorderImage.color = canContinue ? new Color(0.24f, 0.86f, 0.42f, 1f) : new Color(0.32f, 0.32f, 0.36f, 1f);
                }
            }
            else
            {
                closeOrContinueButton.interactable = true;
                closeOrContinueButtonText.text = "CERRAR [TAB / ESC]";
                if (closeOrContinueBorderImage != null)
                {
                    closeOrContinueBorderImage.color = new Color(0.55f, 0.58f, 0.65f, 1f);
                }
            }
        }

        EnsureAllTextsFontAndClean();
    }

    private void UpdateSingleSkillCard(SkillCardUI card, bool hasSkillPoints)
    {
        Sprite portrait = null;
        Color themeColor = Color.white;
        string voiceTitle = "";
        int statVal = 0;
        int rank = 0;
        string currentEffects = "";
        string nextPreview = "";

        switch (card.voiceType)
        {
            case SkillVoiceType.Corporeo:
                portrait = corporeoPortrait;
                themeColor = corporeoColor;
                voiceTitle = "CORPÓREO";
                statVal = playerStats.corporeo;
                rank = playerStats.corporeoUpgrades;
                int wAtk = playerStats.GetEquippedWeaponAttackBonus();
                string wAtkNote = wAtk > 0 ? $" <color=#FFD84D>(+{wAtk} por {playerStats.GetEquippedWeaponName()} = {playerStats.GetTotalPhysicalAttack()} total)</color>" : "";
                currentEffects =
                    $"- <b>Daño Físico Directo:</b> {playerStats.corporeo} pts base{wAtkNote}\n" +
                    $"- <b>Salud Máxima:</b> {playerStats.maxHP} HP totales\n" +
                    $"- <b>Rol:</b> Fuerza bruta y resistencia física";
                nextPreview = "Al subir (+1): <color=#66FF88>+4 Daño Físico y +15 HP Máx (y cura +15 HP)</color>";
                break;

            case SkillVoiceType.Intelecto:
                portrait = intelectoPortrait;
                themeColor = intelectoColor;
                voiceTitle = "INTELECTO";
                statVal = playerStats.intelecto;
                rank = playerStats.intelectoUpgrades;
                int molotovDmg = 30 + playerStats.GetIntelectoDamageBonus();
                int vendajeHeal = 50 + playerStats.GetIntelectoHealBonus();
                currentEffects =
                    $"- <b>Daño Molotov Improvisada:</b> {molotovDmg} (ignora defensa)\n" +
                    $"- <b>Curación Vendaje Rudo:</b> +{vendajeHeal} HP\n" +
                    $"- <b>Bono Ítems Curativos:</b> +{playerStats.GetIntelectoHealBonus()} HP extra";
                nextPreview = "Al subir (+1): <color=#66FF88>+4 Intelecto (+12 daño Molotov y +12 curación)</color>";
                break;

            case SkillVoiceType.Motricidad:
                portrait = motricidadPortrait;
                themeColor = motricidadColor;
                voiceTitle = "MOTRICIDAD";
                statVal = playerStats.motricidad;
                rank = playerStats.motricidadUpgrades;
                int wMot = playerStats.GetEquippedWeaponMotricidadBonus();
                string wMotNote = wMot != 0 ? $" <color=#FFD84D>({(wMot > 0 ? "+" : "")}{wMot} arma = {playerStats.GetEffectiveMotricidad()})</color>" : "";
                currentEffects =
                    $"- <b>Velocidad de Turno:</b> {playerStats.motricidad}{wMotNote} (actúa antes en la ronda)\n" +
                    $"- <b>Probabilidad de Esquive:</b> {playerStats.GetDisplayDodgePercentage()}% base\n" +
                    $"- <b>Reducción en Guardia:</b> -{playerStats.GetDisplayGuardReductionPercentage()}% daño recibido";
                nextPreview = "Al subir (+1): <color=#66FF88>+4 Velocidad, +24% Esquive y +5% Guardia</color>";
                break;

            case SkillVoiceType.MundoInterno:
                portrait = mundoInternoPortrait;
                themeColor = mundoInternoColor;
                voiceTitle = "MUNDO INTERNO";
                statVal = playerStats.mundoInterno;
                rank = playerStats.mundoInternoUpgrades;
                int psyDmg = playerStats.GetDelirioPsychicDamage();
                string delirioDesc = psyDmg > 0 ? $"Confunde 1 turno + {psyDmg} daño psíquico" : "Confunde al enemigo (pierde 1 turno)";
                currentEffects =
                    $"- <b>Insultar:</b> Reduce -{playerStats.GetInsultarDebuffAmount()} ATQ al enemigo\n" +
                    $"- <b>Animarse:</b> Otorga +{playerStats.GetAnimarseBuffAmount()} ATQ a tu próximo golpe\n" +
                    $"- <b>Delirio Psíquico:</b> {delirioDesc}";
                nextPreview = "Al subir (+1): <color=#66FF88>-3 ATQ en Insultar, +4 ATQ en Animarse y +8 daño en Delirio</color>";
                break;
        }

        if (card.borderImage != null)
        {
            card.borderImage.color = hasSkillPoints ? Color.Lerp(themeColor, goldHighlightColor, 0.35f) : themeColor;
        }

        if (card.portraitImage != null)
        {
            if (portrait != null)
            {
                card.portraitImage.sprite = portrait;
                card.portraitImage.color = Color.white;
                card.portraitImage.preserveAspect = true;
                if (card.portraitFallbackText != null) card.portraitFallbackText.gameObject.SetActive(false);
            }
            else
            {
                card.portraitImage.sprite = null;
                card.portraitImage.color = themeColor;
                if (card.portraitFallbackText != null)
                {
                    card.portraitFallbackText.gameObject.SetActive(true);
                    card.portraitFallbackText.text = voiceTitle;
                }
            }
        }

        if (card.titleAndStatText != null)
        {
            card.titleAndStatText.font = customFont;
            card.titleAndStatText.text = $"{voiceTitle}: <color=#FFFFFF>{statVal}</color>";
            card.titleAndStatText.color = themeColor;
            PlayerHealthBarUI.EnsureCleanPixelFont(card.titleAndStatText);
        }

        if (card.rankBadgeText != null)
        {
            card.rankBadgeText.font = customFont;
            card.rankBadgeText.text = rank > 0 ? $"MEJORAS: +{rank}" : "NIVEL BASE";
            card.rankBadgeText.color = rank > 0 ? goldHighlightColor : new Color(0.65f, 0.68f, 0.72f, 1f);
            PlayerHealthBarUI.EnsureCleanPixelFont(card.rankBadgeText);
        }

        if (card.currentEffectsText != null)
        {
            card.currentEffectsText.font = customFont;
            card.currentEffectsText.text = currentEffects;
            PlayerHealthBarUI.EnsureCleanPixelFont(card.currentEffectsText);
        }

        if (card.nextUpgradePreviewText != null)
        {
            card.nextUpgradePreviewText.font = customFont;
            card.nextUpgradePreviewText.text = nextPreview;
            PlayerHealthBarUI.EnsureCleanPixelFont(card.nextUpgradePreviewText);
        }

        if (card.upgradeButton != null)
        {
            card.upgradeButton.interactable = hasSkillPoints;
        }

        if (card.upgradeButtonBorder != null)
        {
            card.upgradeButtonBorder.color = hasSkillPoints ? goldHighlightColor : new Color(0.24f, 0.25f, 0.28f, 1f);
        }

        if (card.upgradeButtonLabel != null)
        {
            card.upgradeButtonLabel.font = customFont;
            card.upgradeButtonLabel.text = hasSkillPoints
                ? $"▲ SUBIR {voiceTitle} (+1 PTO)"
                : "DISPONIBLE AL SUBIR DE NIVEL";
            card.upgradeButtonLabel.color = hasSkillPoints ? goldHighlightColor : new Color(0.50f, 0.52f, 0.56f, 1f);
            PlayerHealthBarUI.EnsureCleanPixelFont(card.upgradeButtonLabel);
        }
    }

    // =========================================================================
    // CONSTRUCCIÓN PROGRAMÁTICA DEL CANVAS Y LAS 4 TARJETAS DE HABILIDADES
    // =========================================================================

    private void BuildCompleteUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // El botón HUD flotante de nivel/estadísticas se ha retirado a petición del usuario.
        if (hudButtonRoot != null)
        {
            Destroy(hudButtonRoot);
            hudButtonRoot = null;
        }

        // 2. Panel Modal de Pantalla Completa para Estadísticas y Subida de Nivel
        modalPanelRoot = CreatePanel("LevelUpAndStatsModal", transform, Vector2.zero, Vector2.one, new Color(0.01f, 0.01f, 0.02f, 0.92f));

        GameObject outerFrame = CreatePanel("OuterFrame", modalPanelRoot.transform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), new Color(0.45f, 0.46f, 0.52f, 1f));
        GameObject mainBox = CreatePanel("MainBlackBox", outerFrame.transform, new Vector2(0.004f, 0.006f), new Vector2(0.996f, 0.994f), new Color(0.05f, 0.05f, 0.07f, 1f));

        // Cabecera
        headerTitleText = CreateText("HeaderTitleText", mainBox.transform, "ESTADÍSTICAS Y HABILIDADES DE ENRIQUE", 36, TextAlignmentOptions.Center, goldHighlightColor);
        headerTitleText.fontStyle = FontStyles.Bold;
        SetAnchors(headerTitleText.rectTransform, new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.98f));

        subHeaderSummaryText = CreateText("SubHeaderSummaryText", mainBox.transform, "SALUD: 100 / 100 HP  |  PUNTOS DISPONIBLES: 0", 24, TextAlignmentOptions.Center, Color.white);
        SetAnchors(subHeaderSummaryText.rectTransform, new Vector2(0.03f, 0.84f), new Vector2(0.97f, 0.90f));

        // Barra de Experiencia
        GameObject xpBarBg = CreatePanel("XPBarBg", mainBox.transform, new Vector2(0.15f, 0.79f), new Vector2(0.85f, 0.835f), new Color(0.16f, 0.17f, 0.22f, 1f));
        GameObject xpBarFill = CreatePanel("XPBarFill", xpBarBg.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Color(0.28f, 0.65f, 0.95f, 1f));
        xpBarFillRect = xpBarFill.GetComponent<RectTransform>();
        xpBarText = CreateText("XPBarText", xpBarBg.transform, "EXPERIENCIA: 0 / 100 EXP", 20, TextAlignmentOptions.Center, Color.white);
        SetAnchors(xpBarText.rectTransform, Vector2.zero, Vector2.one);

        // Banner de retroalimentación
        GameObject feedbackBg = CreatePanel("FeedbackBannerBg", mainBox.transform, new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.775f), new Color(0.10f, 0.11f, 0.15f, 1f));
        feedbackBannerText = CreateText("FeedbackBannerText", feedbackBg.transform, "Selecciona una habilidad para mejorar su poder en combate.", 22, TextAlignmentOptions.Center, goldHighlightColor);
        SetAnchors(feedbackBannerText.rectTransform, new Vector2(0.01f, 0.05f), new Vector2(0.99f, 0.95f));

        // Cuadrícula 2x2 para las 4 Voces/Habilidades
        SkillVoiceType[] voices = new SkillVoiceType[]
        {
            SkillVoiceType.Corporeo,
            SkillVoiceType.Intelecto,
            SkillVoiceType.Motricidad,
            SkillVoiceType.MundoInterno
        };

        Vector2[] mins = new Vector2[]
        {
            new Vector2(0.03f, 0.42f), // Arriba Izquierda: Corpóreo
            new Vector2(0.51f, 0.42f), // Arriba Derecha: Intelecto
            new Vector2(0.03f, 0.12f), // Abajo Izquierda: Motricidad
            new Vector2(0.51f, 0.12f)  // Abajo Derecha: Mundo Interno
        };

        Vector2[] maxs = new Vector2[]
        {
            new Vector2(0.49f, 0.705f),
            new Vector2(0.97f, 0.705f),
            new Vector2(0.49f, 0.405f),
            new Vector2(0.97f, 0.405f)
        };

        for (int i = 0; i < 4; i++)
        {
            skillCards[i] = BuildSkillCard(mainBox.transform, voices[i], mins[i], maxs[i]);
        }

        // Botón inferior de Cerrar / Continuar
        GameObject closeBorder = CreatePanel("CloseOrContinueButton", mainBox.transform, new Vector2(0.26f, 0.02f), new Vector2(0.74f, 0.095f), new Color(0.55f, 0.58f, 0.65f, 1f));
        closeOrContinueBorderImage = closeBorder.GetComponent<Image>();

        GameObject closeInner = CreatePanel("InnerBlack", closeBorder.transform, new Vector2(0.006f, 0.08f), new Vector2(0.994f, 0.92f), new Color(0.07f, 0.08f, 0.10f, 1f));
        closeOrContinueButton = closeBorder.AddComponent<Button>();
        closeOrContinueButton.targetGraphic = closeInner.GetComponent<Image>();

        closeOrContinueButtonText = CreateText("CloseButtonText", closeInner.transform, "CERRAR MENÚ [TAB / ESC]", 24, TextAlignmentOptions.Center, Color.white);
        closeOrContinueButtonText.fontStyle = FontStyles.Bold;
        SetAnchors(closeOrContinueButtonText.rectTransform, Vector2.zero, Vector2.one);

        modalPanelRoot.SetActive(false);
    }

    private SkillCardUI BuildSkillCard(Transform parent, SkillVoiceType voice, Vector2 anchorMin, Vector2 anchorMax)
    {
        SkillCardUI card = new SkillCardUI { voiceType = voice };

        GameObject cardBorder = CreatePanel($"SkillCard_{voice}", parent, anchorMin, anchorMax, Color.white);
        card.borderImage = cardBorder.GetComponent<Image>();

        GameObject cardInner = CreatePanel("CardInner", cardBorder.transform, new Vector2(0.006f, 0.015f), new Vector2(0.994f, 0.985f), new Color(0.07f, 0.07f, 0.09f, 1f));

        // Retrato a la izquierda
        GameObject portraitFrame = CreatePanel("PortraitFrame", cardInner.transform, new Vector2(0.02f, 0.24f), new Vector2(0.24f, 0.94f), new Color(0.18f, 0.19f, 0.23f, 1f));
        GameObject portraitGO = CreatePanel("PortraitImage", portraitFrame.transform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Color.white);
        card.portraitImage = portraitGO.GetComponent<Image>();

        card.portraitFallbackText = CreateText("PortraitFallback", portraitGO.transform, voice.ToString().ToUpper(), 16, TextAlignmentOptions.Center, Color.black);
        SetAnchors(card.portraitFallbackText.rectTransform, Vector2.zero, Vector2.one);

        // Título + Estadística Actual
        card.titleAndStatText = CreateText("TitleAndStatText", cardInner.transform, $"{voice}: 12", 28, TextAlignmentOptions.Left, Color.white);
        card.titleAndStatText.fontStyle = FontStyles.Bold;
        SetAnchors(card.titleAndStatText.rectTransform, new Vector2(0.26f, 0.78f), new Vector2(0.72f, 0.96f));

        // Insignia de Rango / Mejoras
        card.rankBadgeText = CreateText("RankBadgeText", cardInner.transform, "NIVEL BASE", 20, TextAlignmentOptions.Right, goldHighlightColor);
        SetAnchors(card.rankBadgeText.rectTransform, new Vector2(0.72f, 0.78f), new Vector2(0.97f, 0.96f));

        // Descripción de Estadísticas Actuales
        card.currentEffectsText = CreateText("CurrentEffectsText", cardInner.transform, "", 19, TextAlignmentOptions.TopLeft, new Color(0.88f, 0.88f, 0.92f, 1f));
        SetAnchors(card.currentEffectsText.rectTransform, new Vector2(0.26f, 0.36f), new Vector2(0.98f, 0.77f));

        // Vista previa de la siguiente mejora
        card.nextUpgradePreviewText = CreateText("NextUpgradePreviewText", cardInner.transform, "", 17, TextAlignmentOptions.Left, new Color(0.60f, 0.95f, 0.68f, 1f));
        SetAnchors(card.nextUpgradePreviewText.rectTransform, new Vector2(0.26f, 0.22f), new Vector2(0.98f, 0.36f));

        // Botón para Subir esta Habilidad
        GameObject upgBorder = CreatePanel("UpgradeButton", cardInner.transform, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.20f), goldHighlightColor);
        card.upgradeButtonBorder = upgBorder.GetComponent<Image>();

        GameObject upgInner = CreatePanel("Inner", upgBorder.transform, new Vector2(0.005f, 0.08f), new Vector2(0.995f, 0.92f), new Color(0.09f, 0.10f, 0.13f, 1f));
        card.upgradeButton = upgBorder.AddComponent<Button>();
        card.upgradeButton.targetGraphic = upgInner.GetComponent<Image>();

        card.upgradeButtonLabel = CreateText("UpgradeLabel", upgInner.transform, "▲ SUBIR HABILIDAD (+1 PTO)", 20, TextAlignmentOptions.Center, goldHighlightColor);
        card.upgradeButtonLabel.fontStyle = FontStyles.Bold;
        SetAnchors(card.upgradeButtonLabel.rectTransform, Vector2.zero, Vector2.one);

        return card;
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
        if (customFont == null)
        {
            customFont = PlayerHealthBarUI.GetCleanPixelFont();
        }
        tmp.font = customFont;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Normal;
        tmp.fontWeight = FontWeight.Regular;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
        return tmp;
    }

    public void EnsureAllTextsFontAndClean()
    {
        if (customFont == null) customFont = PlayerHealthBarUI.GetCleanPixelFont();
        TextMeshProUGUI[] tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (tmps != null)
        {
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] != null)
                {
                    tmps[i].font = customFont;
                    tmps[i].fontStyle = FontStyles.Normal;
                    tmps[i].fontWeight = FontWeight.Regular;
                    PlayerHealthBarUI.EnsureCleanPixelFont(tmps[i]);
                }
            }
        }
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private bool WasToggleMenuKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame)
                return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.C))
            return true;
#endif
        return false;
    }

    private bool WasEscapeKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif
        return false;
    }

    private bool WasConfirmKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            return true;
        }
#endif
        return false;
    }
}
