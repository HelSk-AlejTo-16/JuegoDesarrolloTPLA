using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Menú de Pausa completo para el juego (Escena 01 y Escena 02).
/// Incluye:
/// - Congelamiento de tiempo y detención de movimiento del jugador.
/// - Configuración de Audio en tiempo real (Volumen Música y Efectos de Sonido con persistencia en PlayerPrefs).
/// - Resumen en vivo del Estado de Enrique (Salud, Habilidades y Arma equipada).
/// - Guía rápida de controles y supervivencia.
/// - Reiniciar Nivel y Salir al Menú Principal (Escena 00).
/// - Salir al Escritorio.
/// - Retroalimentación sonora en hover y clic usando los audios registrados en MusicManager.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    private static PauseMenuManager _instance;

    public static PauseMenuManager Instance => _instance;

    public enum PauseTab
    {
        AudioSettings,
        PlayerStatus,
        ControlsGuide
    }

    [Header("Referencias Principales")]
    public GameObject pauseModalRoot;
    public Canvas pauseCanvas;

    [Header("Botones de Navegación")]
    public Button resumeButton;
    public Button audioTabButton;
    public Button statusTabButton;
    public Button controlsTabButton;
    public Button restartButton;
    public Button mainMenuButton;
    public Button quitGameButton;

    [Header("Pestañas de Contenido")]
    public GameObject audioTabPanel;
    public GameObject statusTabPanel;
    public GameObject controlsTabPanel;

    [Header("Controles de Configuración de Audio")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public TextMeshProUGUI musicVolumePercentText;
    public TextMeshProUGUI sfxVolumePercentText;
    public Button resetAudioDefaultsButton;

    [Header("Textos del Estado de Enrique")]
    public TextMeshProUGUI statusLevelText;
    public TextMeshProUGUI statusHealthText;
    public RectTransform statusHealthBarFill;
    public TextMeshProUGUI statusSkillsText;
    public TextMeshProUGUI statusWeaponText;

    private bool isPaused = false;
    private int lastOtherMenuOpenFrame = -100;
    private PauseTab currentTab = PauseTab.AudioSettings;

    private PlayerController2D playerController;
    private PlayerStats playerStats;
    private InventoryManager inventoryManager;

    private static readonly Color PureBlack = new Color(0.02f, 0.02f, 0.025f, 1f);
    private static readonly Color MatteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
    private static readonly Color SubtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
    private static readonly Color BoneWhite = new Color(0.92f, 0.90f, 0.86f, 1f);
    private static readonly Color MutedGray = new Color(0.65f, 0.66f, 0.70f, 1f);

    private static readonly Color AccentAmberGold = new Color(0.88f, 0.60f, 0.20f, 1f);
    private static readonly Color AccentIntelectoBlue = new Color(0.20f, 0.56f, 0.88f, 1f);
    private static readonly Color AccentMundoViolet = new Color(0.58f, 0.26f, 0.78f, 1f);
    private static readonly Color AccentCorporeoRed = new Color(0.72f, 0.18f, 0.18f, 1f);
    private static readonly Color AccentTeal = new Color(0.22f, 0.65f, 0.58f, 1f);

    public bool IsPaused => isPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeOnSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedRuntime;
        SceneManager.sceneLoaded += OnSceneLoadedRuntime;
        CheckAndEnsureInActiveScene();
    }

    private static void OnSceneLoadedRuntime(Scene scene, LoadSceneMode mode)
    {
        CheckAndEnsureInActiveScene();
    }

    public static void CheckAndEnsureInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid()) return;

        if (activeScene.name.Contains("00_MenuIntro") ||
            activeScene.name.Contains("Intro") ||
            activeScene.name.Contains("Menu"))
        {
            if (_instance != null)
            {
                if (_instance.isPaused)
                {
                    Time.timeScale = 1f;
                }
                Destroy(_instance.gameObject);
                _instance = null;
            }
            return;
        }

        EnsureInScene();
    }

    public static PauseMenuManager EnsureInScene()
    {
        if (_instance != null)
        {
            _instance.InitializeIfNeeded();
            return _instance;
        }

        PauseMenuManager existing = FindAnyObjectByType<PauseMenuManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            existing.InitializeIfNeeded();
            return existing;
        }

        GameObject go = new GameObject("PauseMenu_Canvas");
        PauseMenuManager manager = go.AddComponent<PauseMenuManager>();
        _instance = manager;
        manager.InitializeIfNeeded();
        return manager;
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
        if (isPaused)
        {
            Time.timeScale = 1f;
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void InitializeIfNeeded()
    {
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController2D>();
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;

        if (pauseModalRoot == null)
        {
            BuildCompleteUI();
        }

        SetupListeners();

        if (pauseModalRoot != null)
        {
            pauseModalRoot.SetActive(false);
        }
        isPaused = false;
    }

    private void SetupListeners()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (audioTabButton != null)
        {
            audioTabButton.onClick.RemoveListener(SelectAudioTab);
            audioTabButton.onClick.AddListener(SelectAudioTab);
        }

        if (statusTabButton != null)
        {
            statusTabButton.onClick.RemoveListener(SelectStatusTab);
            statusTabButton.onClick.AddListener(SelectStatusTab);
        }

        if (controlsTabButton != null)
        {
            controlsTabButton.onClick.RemoveListener(SelectControlsTab);
            controlsTabButton.onClick.AddListener(SelectControlsTab);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartLevel);
            restartButton.onClick.AddListener(RestartLevel);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ExitToMainMenu);
            mainMenuButton.onClick.AddListener(ExitToMainMenu);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveListener(QuitToDesktop);
            quitGameButton.onClick.AddListener(QuitToDesktop);
        }

        if (resetAudioDefaultsButton != null)
        {
            resetAudioDefaultsButton.onClick.RemoveListener(ResetAudioDefaults);
            resetAudioDefaultsButton.onClick.AddListener(ResetAudioDefaults);
        }

        if (musicVolumeSlider != null)
        {
            float musicVol = (MusicManager.Instance != null) ? MusicManager.Instance.masterVolume : 0.8f;
            musicVolumeSlider.SetValueWithoutNotify(musicVol);
            UpdateMusicVolumeDisplay(musicVol);
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            float sfxVol = (MusicManager.Instance != null) ? MusicManager.Instance.sfxVolume : 0.9f;
            sfxVolumeSlider.SetValueWithoutNotify(sfxVol);
            UpdateSfxVolumeDisplay(sfxVol);
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
    }

    private void Update()
    {
        // Si hay otro menú abierto (Inventario o Subida de nivel), no abrir la pausa en ese frame
        if (IsOtherMenuOpen())
        {
            lastOtherMenuOpenFrame = Time.frameCount;
            return;
        }

        // Si se acaba de cerrar otro menú con ESC hace 1 frame, no reaccionar inmediatamente
        if (Time.frameCount <= lastOtherMenuOpenFrame + 1)
        {
            return;
        }

        if (WasPauseKeyPressed())
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    private bool IsOtherMenuOpen()
    {
        if (PlayerInventoryAndWeaponsUI.Instance != null && PlayerInventoryAndWeaponsUI.Instance.IsOpen)
            return true;
        if (PlayerLevelUpAndStatsUI.Instance != null && PlayerLevelUpAndStatsUI.Instance.IsOpen)
            return true;
        return false;
    }

    private bool WasPauseKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
                return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            return true;
#endif
        return false;
    }

    // =========================================================================
    // ACCIONES DE PAUSA
    // =========================================================================

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController2D>();
        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        PlayConfirmSfx();

        if (pauseModalRoot != null)
        {
            pauseModalRoot.SetActive(true);
        }

        // Sincronizar sliders y refrescar estado de Enrique
        SyncAudioSliders();
        RefreshPlayerStatus();
        SwitchTab(currentTab);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController2D>();
        if (playerController != null)
        {
            // Solo restaurar movimiento si no está en combate
            BattleManager bm = FindAnyObjectByType<BattleManager>();
            bool inCombat = (bm != null && bm.state != BattleState.INACTIVE);
            if (!inCombat)
            {
                playerController.SetCanMove(true);
            }
        }

        PlayConfirmSfx();

        if (pauseModalRoot != null)
        {
            pauseModalRoot.SetActive(false);
        }
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        PlayConfirmSfx();
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        PlayConfirmSfx();

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayMenuMusic(instant: true);
        }

        SceneManager.LoadScene("00_MenuIntro");
    }

    public void QuitToDesktop()
    {
        Time.timeScale = 1f;
        PlayConfirmSfx();
        Debug.Log("[PauseMenuManager] Saliendo de la aplicación...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // =========================================================================
    // GESTIÓN DE PESTAÑAS (AUDIO, ESTADO, CONTROLES)
    // =========================================================================

    public void SelectAudioTab()
    {
        PlayConfirmSfx();
        SwitchTab(PauseTab.AudioSettings);
    }

    public void SelectStatusTab()
    {
        PlayConfirmSfx();
        RefreshPlayerStatus();
        SwitchTab(PauseTab.PlayerStatus);
    }

    public void SelectControlsTab()
    {
        PlayConfirmSfx();
        SwitchTab(PauseTab.ControlsGuide);
    }

    private void SwitchTab(PauseTab tab)
    {
        currentTab = tab;

        if (audioTabPanel != null) audioTabPanel.SetActive(tab == PauseTab.AudioSettings);
        if (statusTabPanel != null) statusTabPanel.SetActive(tab == PauseTab.PlayerStatus);
        if (controlsTabPanel != null) controlsTabPanel.SetActive(tab == PauseTab.ControlsGuide);

        UpdateButtonBorderColor(audioTabButton, tab == PauseTab.AudioSettings ? AccentIntelectoBlue : SubtleBorder);
        UpdateButtonBorderColor(statusTabButton, tab == PauseTab.PlayerStatus ? AccentMundoViolet : SubtleBorder);
        UpdateButtonBorderColor(controlsTabButton, tab == PauseTab.ControlsGuide ? AccentTeal : SubtleBorder);
    }

    private void UpdateButtonBorderColor(Button btn, Color color)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    // =========================================================================
    // CONFIGURACIÓN DE AUDIO
    // =========================================================================

    public void OnMusicVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetMusicVolume(value);
        }
        UpdateMusicVolumeDisplay(value);
    }

    public void OnSfxVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetSfxVolume(value);
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }
        UpdateSfxVolumeDisplay(value);
    }

    public void ResetAudioDefaults()
    {
        PlayConfirmSfx();
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = 0.8f;
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = 0.9f;
        }
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetMusicVolume(0.8f);
            MusicManager.Instance.SetSfxVolume(0.9f);
        }
        UpdateMusicVolumeDisplay(0.8f);
        UpdateSfxVolumeDisplay(0.9f);
    }

    private void SyncAudioSliders()
    {
        if (MusicManager.Instance != null)
        {
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.SetValueWithoutNotify(MusicManager.Instance.masterVolume);
                UpdateMusicVolumeDisplay(MusicManager.Instance.masterVolume);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(MusicManager.Instance.sfxVolume);
                UpdateSfxVolumeDisplay(MusicManager.Instance.sfxVolume);
            }
        }
    }

    private void UpdateMusicVolumeDisplay(float value)
    {
        if (musicVolumePercentText != null)
        {
            musicVolumePercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }

    private void UpdateSfxVolumeDisplay(float value)
    {
        if (sfxVolumePercentText != null)
        {
            sfxVolumePercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }

    // =========================================================================
    // ESTADO DE ENRIQUE
    // =========================================================================

    public void RefreshPlayerStatus()
    {
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;

        if (playerStats != null)
        {
            if (statusLevelText != null)
            {
                statusLevelText.text = $"NIVEL: {playerStats.level}  |  EXP: {playerStats.currentXP} / {playerStats.xpToNextLevel}  |  PUNTOS DISPONIBLES: {playerStats.availableSkillPoints}";
            }

            if (statusHealthText != null)
            {
                float pct = (playerStats.maxHP > 0) ? (float)playerStats.currentHP / playerStats.maxHP : 1f;
                statusHealthText.text = $"SALUD BIOLÓGICA: {playerStats.currentHP} / {playerStats.maxHP} HP ({Mathf.RoundToInt(pct * 100f)}%)";
            }

            if (statusHealthBarFill != null)
            {
                float pct = (playerStats.maxHP > 0) ? Mathf.Clamp01((float)playerStats.currentHP / playerStats.maxHP) : 1f;
                statusHealthBarFill.anchorMax = new Vector2(pct, 1f);
            }

            if (statusSkillsText != null)
            {
                statusSkillsText.text =
                    $"<color=#D83A3A><b>CORPÓREO: {playerStats.corporeo}</b></color> (+{playerStats.corporeoUpgrades} mejoras) - Fuerza bruta, salud y potencia física\n" +
                    $"<color=#3399E8><b>INTELECTO: {playerStats.intelecto}</b></color> (+{playerStats.intelectoUpgrades} mejoras) - Curación, recetas e improvisación con objetos\n" +
                    $"<color=#E5AC24><b>MOTRICIDAD: {playerStats.motricidad}</b></color> (+{playerStats.motricidadUpgrades} mejoras) - Iniciativa, velocidad de turno y reflejos de esquive\n" +
                    $"<color=#A045E6><b>MUNDO INTERNO: {playerStats.mundoInterno}</b></color> (+{playerStats.mundoInternoUpgrades} mejoras) - Sugestión, insultos y delirio psicológico";
            }
        }

        if (statusWeaponText != null)
        {
            ItemData weapon = (inventoryManager != null) ? inventoryManager.equippedWeapon : null;
            if (weapon != null)
            {
                statusWeaponText.text = $"<color=#E0B329><b>ARMA EQUIPADA:</b></color> {weapon.itemName.ToUpper()} (+{weapon.weaponAttackBonus} ATQ)\n<color=#A0A3AB>\"{weapon.description}\"</color>";
            }
            else
            {
                statusWeaponText.text = "<color=#E0B329><b>ARMA EQUIPADA:</b></color> PUÑOS DESNUDOS (+0 ATQ)\n<color=#A0A3AB>\"Los nudillos de Enrique listos para defenderse.\"</color>";
            }
        }
    }

    private void PlayConfirmSfx()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }
    }

    // =========================================================================
    // CONSTRUCCIÓN COMPLETA DE LA INTERFAZ (1920x1080)
    // =========================================================================

    public void BuildCompleteUI()
    {
        TMP_FontAsset font = LoadPixelFont();

        // 1. Canvas y Scaler
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        pauseCanvas = canvas;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        // Limpiar hijos existentes si había
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // 2. Fondo Completo
        GameObject modalGO = CreatePanel("PauseModalRoot", transform, new Color(0.01f, 0.01f, 0.015f, 0.90f));
        SetAnchors(modalGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        pauseModalRoot = modalGO;

        // 3. Ventana Central
        GameObject windowBorder = CreatePanel("PauseWindowBorder", modalGO.transform, AccentIntelectoBlue);
        SetAnchors(windowBorder.GetComponent<RectTransform>(), new Vector2(0.10f, 0.05f), new Vector2(0.90f, 0.95f));

        GameObject windowInner = CreatePanel("PauseWindowInner", windowBorder.transform, new Color(0.04f, 0.04f, 0.05f, 0.99f));
        SetAnchors(windowInner.GetComponent<RectTransform>(), new Vector2(0.002f, 0.004f), new Vector2(0.998f, 0.996f));

        // 4. Encabezado de Pausa
        TextMeshProUGUI headerTMP = CreateTMPText("PauseHeader", windowInner.transform, "PAUSA - SANTA FELIPINA", 46, TextAlignmentOptions.Center, font);
        headerTMP.color = BoneWhite;
        SetAnchors(headerTMP.rectTransform, new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f));

        TextMeshProUGUI subHeaderTMP = CreateTMPText("PauseSubHeader", windowInner.transform, "La mente de Enrique se detiene por un instante...", 22, TextAlignmentOptions.Center, font);
        subHeaderTMP.color = AccentAmberGold;
        SetAnchors(subHeaderTMP.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.90f));

        // 5. Columna Izquierda: Botones de Acción (X: 0.03 a 0.36)
        GameObject leftCol = new GameObject("LeftNavigationColumn", typeof(RectTransform));
        leftCol.transform.SetParent(windowInner.transform, false);
        SetAnchors(leftCol.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.36f, 0.83f));

        TextMeshProUGUI navTitle = CreateTMPText("NavTitle", leftCol.transform, "MENU DE SISTEMA", 22, TextAlignmentOptions.Center, font);
        navTitle.color = MutedGray;
        SetAnchors(navTitle.rectTransform, new Vector2(0.05f, 0.93f), new Vector2(0.95f, 0.99f));

        resumeButton = CreateNoirButton("ResumeButton", leftCol.transform, "[ REANUDAR AVENTURA ]", MatteBlack, AccentAmberGold, 32, font);
        SetAnchors(resumeButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.91f));

        audioTabButton = CreateNoirButton("AudioTabButton", leftCol.transform, "[ CONFIGURACION AUDIO ]", MatteBlack, AccentIntelectoBlue, 28, font);
        SetAnchors(audioTabButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.67f), new Vector2(0.95f, 0.77f));

        statusTabButton = CreateNoirButton("StatusTabButton", leftCol.transform, "[ ESTADO DE ENRIQUE ]", MatteBlack, AccentMundoViolet, 28, font);
        SetAnchors(statusTabButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.64f));

        controlsTabButton = CreateNoirButton("ControlsTabButton", leftCol.transform, "[ GUIA DE CONTROLES ]", MatteBlack, AccentTeal, 28, font);
        SetAnchors(controlsTabButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.41f), new Vector2(0.95f, 0.51f));

        restartButton = CreateNoirButton("RestartButton", leftCol.transform, "[ REINICIAR NIVEL ]", MatteBlack, SubtleBorder, 26, font);
        SetAnchors(restartButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.27f), new Vector2(0.95f, 0.37f));

        mainMenuButton = CreateNoirButton("MainMenuButton", leftCol.transform, "[ MENU PRINCIPAL ]", MatteBlack, AccentCorporeoRed, 26, font);
        SetAnchors(mainMenuButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.24f));

        quitGameButton = CreateNoirButton("QuitGameButton", leftCol.transform, "[ SALIR DEL JUEGO ]", MatteBlack, new Color(0.50f, 0.15f, 0.15f, 1f), 24, font);
        SetAnchors(quitGameButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.11f));

        // 6. Columna Derecha: Contenido de Pestañas (X: 0.38 a 0.97)
        GameObject rightColBorder = CreatePanel("RightContentBorder", windowInner.transform, SubtleBorder);
        SetAnchors(rightColBorder.GetComponent<RectTransform>(), new Vector2(0.38f, 0.03f), new Vector2(0.97f, 0.83f));

        GameObject rightColInner = CreatePanel("RightContentInner", rightColBorder.transform, new Color(0.03f, 0.03f, 0.04f, 0.99f));
        SetAnchors(rightColInner.GetComponent<RectTransform>(), new Vector2(0.003f, 0.004f), new Vector2(0.997f, 0.996f));

        // --- PESTAÑA 1: AUDIO SETTINGS ---
        GameObject audioTab = new GameObject("AudioTabPanel", typeof(RectTransform));
        audioTab.transform.SetParent(rightColInner.transform, false);
        SetAnchors(audioTab.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        audioTabPanel = audioTab;

        TextMeshProUGUI audioTitle = CreateTMPText("AudioTitle", audioTab.transform, "PARAMETROS ACUSTICOS DEL SISTEMA", 30, TextAlignmentOptions.Center, font);
        audioTitle.color = AccentAmberGold;
        SetAnchors(audioTitle.rectTransform, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f));

        TextMeshProUGUI audioDesc = CreateTMPText("AudioDesc", audioTab.transform, "Ajuste en tiempo real de volumen con persistencia automatica", 20, TextAlignmentOptions.Center, font);
        audioDesc.color = MutedGray;
        SetAnchors(audioDesc.rectTransform, new Vector2(0.05f, 0.81f), new Vector2(0.95f, 0.88f));

        // Fila Música
        TextMeshProUGUI musicLbl = CreateTMPText("MusicLabel", audioTab.transform, "VOLUMEN DE MUSICA (BGM)", 26, TextAlignmentOptions.Left, font);
        musicLbl.color = BoneWhite;
        SetAnchors(musicLbl.rectTransform, new Vector2(0.08f, 0.67f), new Vector2(0.70f, 0.75f));

        musicVolumePercentText = CreateTMPText("MusicPercent", audioTab.transform, "80%", 26, TextAlignmentOptions.Right, font);
        musicVolumePercentText.color = AccentAmberGold;
        SetAnchors(musicVolumePercentText.rectTransform, new Vector2(0.70f, 0.67f), new Vector2(0.92f, 0.75f));

        musicVolumeSlider = CreateNoirSlider("MusicSlider", audioTab.transform, AccentIntelectoBlue);
        SetAnchors(musicVolumeSlider.GetComponent<RectTransform>(), new Vector2(0.08f, 0.57f), new Vector2(0.92f, 0.65f));

        // Fila SFX
        TextMeshProUGUI sfxLbl = CreateTMPText("SfxLabel", audioTab.transform, "VOLUMEN DE EFECTOS (SFX)", 26, TextAlignmentOptions.Left, font);
        sfxLbl.color = BoneWhite;
        SetAnchors(sfxLbl.rectTransform, new Vector2(0.08f, 0.42f), new Vector2(0.70f, 0.50f));

        sfxVolumePercentText = CreateTMPText("SfxPercent", audioTab.transform, "90%", 26, TextAlignmentOptions.Right, font);
        sfxVolumePercentText.color = AccentAmberGold;
        SetAnchors(sfxVolumePercentText.rectTransform, new Vector2(0.70f, 0.42f), new Vector2(0.92f, 0.50f));

        sfxVolumeSlider = CreateNoirSlider("SfxSlider", audioTab.transform, AccentAmberGold);
        SetAnchors(sfxVolumeSlider.GetComponent<RectTransform>(), new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.40f));

        resetAudioDefaultsButton = CreateNoirButton("ResetAudioButton", audioTab.transform, "[ RESTABLECER VALORES POR DEFECTO (80% / 90%) ]", MatteBlack, SubtleBorder, 22, font);
        SetAnchors(resetAudioDefaultsButton.GetComponent<RectTransform>(), new Vector2(0.12f, 0.14f), new Vector2(0.88f, 0.23f));

        TextMeshProUGUI audioHint = CreateTMPText("AudioHint", audioTab.transform, "Los cambios se aplican de inmediato en exploracion y en combate.", 18, TextAlignmentOptions.Center, font);
        audioHint.color = new Color(0.45f, 0.48f, 0.52f, 1f);
        SetAnchors(audioHint.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.11f));

        // --- PESTAÑA 2: ESTADO DE ENRIQUE ---
        GameObject statusTab = new GameObject("StatusTabPanel", typeof(RectTransform));
        statusTab.transform.SetParent(rightColInner.transform, false);
        SetAnchors(statusTab.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        statusTabPanel = statusTab;

        TextMeshProUGUI statusTitle = CreateTMPText("StatusTitle", statusTab.transform, "EXPEDIENTE: ENRIQUE BOSQUES", 30, TextAlignmentOptions.Center, font);
        statusTitle.color = AccentMundoViolet;
        SetAnchors(statusTitle.rectTransform, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f));

        statusLevelText = CreateTMPText("StatusLevel", statusTab.transform, "NIVEL: 1 | EXP: 0 / 100", 22, TextAlignmentOptions.Center, font);
        statusLevelText.color = BoneWhite;
        SetAnchors(statusLevelText.rectTransform, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.87f));

        statusHealthText = CreateTMPText("StatusHealth", statusTab.transform, "SALUD BIOLOGICA: 100 / 100 HP (100%)", 22, TextAlignmentOptions.Center, font);
        statusHealthText.color = BoneWhite;
        SetAnchors(statusHealthText.rectTransform, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.77f));

        // Barra de Vida Visual
        GameObject hpBg = CreatePanel("HealthBarBG", statusTab.transform, new Color(0.15f, 0.05f, 0.05f, 1f));
        SetAnchors(hpBg.GetComponent<RectTransform>(), new Vector2(0.12f, 0.65f), new Vector2(0.88f, 0.69f));

        GameObject hpFill = CreatePanel("HealthBarFill", hpBg.transform, new Color(0.20f, 0.75f, 0.30f, 1f));
        RectTransform fillRT = hpFill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        fillRT.anchoredPosition = Vector2.zero;
        statusHealthBarFill = fillRT;

        statusSkillsText = CreateTMPText("StatusSkills", statusTab.transform, "CORPOREO: 12\nINTELECTO: 14\nMOTRICIDAD: 12\nMUNDO INTERNO: 12", 21, TextAlignmentOptions.Center, font);
        statusSkillsText.lineSpacing = 8f;
        SetAnchors(statusSkillsText.rectTransform, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.62f));

        statusWeaponText = CreateTMPText("StatusWeapon", statusTab.transform, "ARMA EQUIPADA: NINGUNA", 22, TextAlignmentOptions.Center, font);
        statusWeaponText.color = AccentAmberGold;
        SetAnchors(statusWeaponText.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.18f));

        // --- PESTAÑA 3: CONTROLES ---
        GameObject controlsTab = new GameObject("ControlsTabPanel", typeof(RectTransform));
        controlsTab.transform.SetParent(rightColInner.transform, false);
        SetAnchors(controlsTab.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        controlsTabPanel = controlsTab;

        TextMeshProUGUI controlsTitle = CreateTMPText("ControlsTitle", controlsTab.transform, "GUIA DE TECLAS Y SUPERVIVENCIA", 30, TextAlignmentOptions.Center, font);
        controlsTitle.color = AccentTeal;
        SetAnchors(controlsTitle.rectTransform, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f));

        TextMeshProUGUI controlsDesc = CreateTMPText("ControlsDesc", controlsTab.transform, "Acciones de exploracion urbana y toma de decisiones", 20, TextAlignmentOptions.Center, font);
        controlsDesc.color = MutedGray;
        SetAnchors(controlsDesc.rectTransform, new Vector2(0.05f, 0.81f), new Vector2(0.95f, 0.88f));

        string controlsText =
            "<color=#E5AC24><b>W, A, S, D / Flechas</b></color> : Caminar por las calles de Santa Felipina\n\n" +
            "<color=#E5AC24><b>Shift</b></color> : Correr a toda velocidad por el pavimento\n\n" +
            "<color=#E5AC24><b>E / Espacio</b></color> : Interactuar con puertas, personajes y recoger objetos\n\n" +
            "<color=#E5AC24><b>I / B</b></color> : Abrir la Mochila (Inventario de Objetos y Armas)\n\n" +
            "<color=#E5AC24><b>Tab / C</b></color> : Consultar y mejorar las 4 Habilidades de Enrique\n\n" +
            "<color=#E5AC24><b>Esc / P</b></color> : Abrir o cerrar este Menú de Pausa\n\n" +
            "<color=#E5AC24><b>Clic Izquierdo</b></color> : Seleccionar enemigo en combate y botones de UI";

        TextMeshProUGUI controlsBody = CreateTMPText("ControlsBody", controlsTab.transform, controlsText, 22, TextAlignmentOptions.Center, font);
        controlsBody.lineSpacing = 6f;
        SetAnchors(controlsBody.rectTransform, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.79f));

        TextMeshProUGUI controlsHint = CreateTMPText("ControlsHint", controlsTab.transform, "En combate por turnos, Motricidad determina quien actua primero.", 18, TextAlignmentOptions.Center, font);
        controlsHint.color = new Color(0.45f, 0.48f, 0.52f, 1f);
        SetAnchors(controlsHint.rectTransform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.10f));

        // Inicializar pestañas visibles
        SwitchTab(PauseTab.AudioSettings);
    }

    private static TMP_FontAsset LoadPixelFont()
    {
#if UNITY_EDITOR
        TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Pixelzone_TMP.asset");
        if (font != null) return font;
#endif
        return Resources.Load<TMP_FontAsset>("Pixelzone_TMP");
    }

    private static Button CreateNoirButton(string name, Transform parent, string label, Color innerBlackColor, Color accentBorderColor, float fontSize, TMP_FontAsset font)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image borderImg = btnGO.GetComponent<Image>();
        borderImg.color = accentBorderColor;

        GameObject innerGO = CreatePanel("InnerBlack", btnGO.transform, innerBlackColor);
        SetAnchors(innerGO.GetComponent<RectTransform>(), new Vector2(0.015f, 0.04f), new Vector2(0.985f, 0.96f));
        Image innerImg = innerGO.GetComponent<Image>();

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = innerImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        btn.colors = cb;

        TextMeshProUGUI txt = CreateTMPText("Label", innerGO.transform, label, fontSize, TextAlignmentOptions.Center, font);
        txt.color = BoneWhite;
        SetAnchors(txt.rectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));

        UIButtonSoundFeedback feedback = btnGO.AddComponent<UIButtonSoundFeedback>();
        feedback.hoverVolumeScale = 0.55f;

        return btn;
    }

    private static Slider CreateNoirSlider(string name, Transform parent, Color fillColor)
    {
        GameObject sliderGO = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(parent, false);
        Slider slider = sliderGO.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        GameObject bgGO = CreatePanel("Background", sliderGO.transform, new Color(0.12f, 0.12f, 0.15f, 1f));
        SetAnchors(bgGO.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));

        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        SetAnchors(fillAreaGO.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));

        GameObject fillGO = CreatePanel("Fill", fillAreaGO.transform, fillColor);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        fillRT.anchoredPosition = Vector2.zero;

        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        SetAnchors(handleAreaGO.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f));

        GameObject handleGO = CreatePanel("Handle", handleAreaGO.transform, BoneWhite);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(24f, 0f);
        handleRT.anchorMin = new Vector2(0f, 0.08f);
        handleRT.anchorMax = new Vector2(0f, 0.92f);

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleGO.GetComponent<Image>();

        return slider;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color bgColor)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image img = panel.GetComponent<Image>();
        img.color = bgColor;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return panel;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string content, float fontSize, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.characterSpacing = 2f;
        tmp.wordSpacing = 6f;
        return tmp;
    }

    private static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
