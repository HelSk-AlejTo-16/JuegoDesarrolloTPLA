using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Los 3 tipos de diálogo del juego:
/// 1. Narrator: Sin portrait.
/// 2. Character: Portrait color gris (sirve para Enrique, enemigos y NPCs).
/// 3. SkillVoice: Portrait que cambia de color según cuál de las 4 habilidades habla.
/// </summary>
public enum DialogueSpeakerType
{
    Narrator,
    Character,
    SkillVoice
}

/// <summary>
/// Las 4 voces/estadísticas en la cabeza de Enrique.
/// </summary>
public enum SkillVoiceType
{
    Corporeo,
    Intelecto,
    Motricidad,
    MundoInterno
}

[System.Serializable]
public class SlideData
{
    [Header("Imagen de la Diapositiva")]
    [Tooltip("Opcional: Sprite definitivo para cuando tengas el dibujo final de la escena.")]
    public Sprite slidePhoto;

    [Tooltip("Color sólido del recuadro de la foto mientras no haya sprite asignado.")]
    public Color placeholderColor = new Color(0.2f, 0.25f, 0.35f, 1f);

    [Tooltip("Texto corto encima del cuadro de la foto (ej. FOTO 1: Calle de Santa Felipina).")]
    public string placeholderPhotoLabel;

    [Header("Tipo de Diálogo y Portrait")]
    [Tooltip("1. Narrator (sin portrait) | 2. Character (portrait gris) | 3. SkillVoice (portrait de color por habilidad).")]
    public DialogueSpeakerType speakerType = DialogueSpeakerType.Narrator;

    [Tooltip("Nombre del personaje o enemigo cuando Speaker Type es 'Character' (ej. Enrique, Lacayo, Don Aurelio).")]
    public string characterName = "Enrique Bosques";

    [Tooltip("Habilidad que habla cuando Speaker Type es 'SkillVoice'.")]
    public SkillVoiceType skillVoice = SkillVoiceType.Corporeo;

    [Tooltip("Opcional: Sprite del portrait si en el futuro tienes el retrato dibujado.")]
    public Sprite customPortraitSprite;

    [Header("Texto del Diálogo")]
    [TextArea(3, 5)]
    public string storyText;
}

public class IntroSlideshowManager : MonoBehaviour
{
    [Header("0. Música de Fondo")]
    [Tooltip("Música de fondo del menú inicial y diapositivas (Chill Menu Loopeable).")]
    public AudioClip menuMusicClip;

    [Tooltip("EfectodeSonido_BotóndeConfirmaciónDialogo: Sonido al avanzar diapositivas o confirmar.")]
    public AudioClip dialogueConfirmSfx;

    [Header("0.1. Fotos de Introduccion (Santa Felipina)")]
    [Tooltip("Foto 1: Calle de Santa Felipina / Enrique repartiendo en bicicleta (Assets/Fondos/Bicicleta1.png)")]
    public Sprite photo1;

    [Tooltip("Foto 2: El Accidente / Caida en bicicleta (Assets/Fondos/Bicicleta2.png)")]
    public Sprite photo2;

    [Tooltip("Foto 3: El Despertar y las 4 Voces / Paquete extraviado (Assets/Fondos/Bicicleta3.png)")]
    public Sprite photo3;
    [Header("1. Pantalla de Inicio (Title Screen)")]
    public GameObject startScreenPanel;
    public Button startGameButton;
    [Tooltip("Botón para comenzar la aventura")]
    public Button adventureButton;
    [Tooltip("Botón para abrir la configuración del sistema (Volumen/SFX)")]
    public Button settingsButton;
    [Tooltip("Botón para abrir los créditos del juego")]
    public Button creditsButton;
    [Tooltip("Botón para salir de la aplicación")]
    public Button quitButton;

    [Header("1.1. Configuración del Sistema (Audio)")]
    public GameObject settingsPanel;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public TextMeshProUGUI musicVolumePercentText;
    public TextMeshProUGUI sfxVolumePercentText;
    public Button closeSettingsButton;

    [Header("1.2. Créditos del Juego")]
    public GameObject creditsPanel;
    public Button closeCreditsButton;

    [Header("2. Referencias UI de Diapositivas")]
    public GameObject slideshowPanel;
    public Image photoPlaceholderImage;
    public TextMeshProUGUI photoLabelText;
    public TextMeshProUGUI narrativeText;
    public Button nextSlideButton;

    [Header("3. Referencias UI de Portrait y Hablante")]
    [Tooltip("Contenedor del Portrait (se oculta automáticamente cuando habla el Narrador).")]
    public GameObject portraitContainer;

    [Tooltip("Imagen del Portrait (se pinta de gris para personajes o del color de la habilidad).")]
    public Image portraitImage;

    [Tooltip("Texto placeholder dentro del Portrait para indicar quién se muestra.")]
    public TextMeshProUGUI portraitLabelText;

    [Tooltip("Texto superior en la caja de diálogo con el nombre de quien habla.")]
    public TextMeshProUGUI speakerNameText;

    [Header("4. Retratos de Enrique y de las 4 Personalidades")]
    [Tooltip("Retrato de Enrique Bosques (Assets/Portraits/Enrique.png).")]
    public Sprite enriquePortrait;

    [Tooltip("Retrato de CORPÓREO / Musculatura (Assets/Portraits/Musculatura.png).")]
    public Sprite corporeoPortrait;

    [Tooltip("Retrato de INTELECTO (Assets/Portraits/Intelecto.png).")]
    public Sprite intelectoPortrait;

    [Tooltip("Retrato de MOTRICIDAD (Assets/Portraits/Motricidad.png).")]
    public Sprite motricidadPortrait;

    [Tooltip("Retrato de MUNDO INTERNO (Assets/Portraits/MundoInterno.png).")]
    public Sprite mundoInternoPortrait;

    [Tooltip("Color gris para el protagonista, enemigos y personajes principales.")]
    public Color characterGrayColor = new Color(0.50f, 0.52f, 0.55f, 1f);

    [Tooltip("Color del portrait cuando habla CORPÓREO.")]
    public Color corporeoColor = new Color(0.82f, 0.20f, 0.20f, 1f);

    [Tooltip("Color del portrait cuando habla INTELECTO.")]
    public Color intelectoColor = new Color(0.20f, 0.56f, 0.88f, 1f);

    [Tooltip("Color del portrait cuando habla MOTRICIDAD.")]
    public Color motricidadColor = new Color(0.88f, 0.70f, 0.16f, 1f);

    [Tooltip("Color del portrait cuando habla MUNDO INTERNO.")]
    public Color mundoInternoColor = new Color(0.60f, 0.26f, 0.84f, 1f);

    [Header("5. Cambio de Escena / Control del Jugador al Terminar")]
    [Tooltip("Si está activo, carga la escena del mapa al terminar las diapositivas de introducción.")]
    public bool loadSceneOnFinish = true;
    [Tooltip("Nombre de la escena del mapa a cargar al finalizar la introducción.")]
    public string nextSceneName = "01_Nivel1_SantaFelipina";
    public PlayerController2D playerController;
    public MonoBehaviour playerMovementScript;
    public UnityEvent onIntroFinished;

    [Header("6. Secuencia de Diapositivas y Diálogos (Santa Felipina)")]
    public SlideData[] slides = new SlideData[]
    {
        // --- FOTO 1: La Ciudad de Santa Felipina (Narrador + Enrique) ---
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.82f, 0.50f, 0.18f, 1f),
            placeholderPhotoLabel = "FOTO 1: Calle de Santa Felipina\nEnrique repartiendo en bicicleta",
            speakerType = DialogueSpeakerType.Narrator,
            storyText = "Santa Felipina hierve bajo el sol y el esmog. Entre callejones empedrados y cables colgantes, Enrique Bosques pedalea a toda velocidad para entregar su ultimo paquete del dia."
        },
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.82f, 0.50f, 0.18f, 1f),
            placeholderPhotoLabel = "FOTO 1: Calle de Santa Felipina\nEnrique repartiendo en bicicleta",
            speakerType = DialogueSpeakerType.Character,
            characterName = "Enrique Bosques",
            storyText = "\"Solo una entrega mas en la avenida principal y me voy a dormir... Si esta cadena oxidada aguanta dos cuadras mas.\""
        },

        // --- FOTO 2: El Accidente (Narrador) ---
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.72f, 0.18f, 0.18f, 1f),
            placeholderPhotoLabel = "FOTO 2: El Accidente\nCaida en bicicleta y golpe en la cabeza",
            speakerType = DialogueSpeakerType.Narrator,
            storyText = "CRACK! La rueda delantera se hunde en una alcantarilla abierta. Enrique sale disparado por encima del manubrio; su craneo rebota contra el pavimento caliente y todo se funde a negro."
        },

        // --- FOTO 3: El Despertar y las 4 Voces (Habilidades con cambio de color) ---
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.28f, 0.20f, 0.42f, 1f),
            placeholderPhotoLabel = "FOTO 3: El Despertar\nLas 4 voces retumbando en la mente de Enrique",
            speakerType = DialogueSpeakerType.SkillVoice,
            skillVoice = SkillVoiceType.Corporeo,
            storyText = "Arriba, pedazo de carne! La sangre aun bombea y los nudillos nos pican. Si alguien se acerca a quitarnos la bici, lo partimos en dos."
        },
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.28f, 0.20f, 0.42f, 1f),
            placeholderPhotoLabel = "FOTO 3: El Despertar\nLas 4 voces retumbando en la mente de Enrique",
            speakerType = DialogueSpeakerType.SkillVoice,
            skillVoice = SkillVoiceType.Intelecto,
            storyText = "Fascinante. El golpe dividio nuestra mente en cuatro. Observa el suelo: tuercas, botellas rotas, cables... podemos improvisar objetos utiles con todo esto."
        },
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.28f, 0.20f, 0.42f, 1f),
            placeholderPhotoLabel = "FOTO 3: El Despertar\nLas 4 voces retumbando en la mente de Enrique",
            speakerType = DialogueSpeakerType.SkillVoice,
            skillVoice = SkillVoiceType.Motricidad,
            storyText = "Callense y miren los pies! El equilibrio vuelve. Somos rapidos: nosotros decidimos quien actua primero y cuando esquivar o defender."
        },
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.28f, 0.20f, 0.42f, 1f),
            placeholderPhotoLabel = "FOTO 3: El Despertar\nLas 4 voces retumbando en la mente de Enrique",
            speakerType = DialogueSpeakerType.SkillVoice,
            skillVoice = SkillVoiceType.MundoInterno,
            storyText = "Las farolas de Santa Felipina nos observan en silencio... Si proyectamos este delirio hacia nuestros enemigos, confundiremos sus mentes y alteraremos su animo."
        },

        // --- FOTO 4: La Entrega Equivocada (Enrique + Lacayo con Portrait Gris) ---
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.18f, 0.42f, 0.38f, 1f),
            placeholderPhotoLabel = "FOTO 4: La Entrega Equivocada\nEl paquete del mafioso perdido",
            speakerType = DialogueSpeakerType.Character,
            characterName = "Enrique Bosques",
            storyText = "\"Quienes estan gritando en mi cabeza?... Espera. Mi mochila esta abierta. Entregue el paquete equivocado... El encargo privado del mafioso no esta!\""
        },
        new SlideData
        {
            slidePhoto = null,
            placeholderColor = new Color(0.18f, 0.42f, 0.38f, 1f),
            placeholderPhotoLabel = "FOTO 4: La Entrega Equivocada\nEl paquete del mafioso perdido",
            speakerType = DialogueSpeakerType.Character,
            characterName = "Lacayo de la Mafia",
            storyText = "\"Ahi esta el repartidor de la bicicleta! No dejen que se escape sin devolver el paquete del Jefe!\""
        }
    };

    private int currentSlideIndex = 0;
    private bool isInStartScreen = false;
    private bool isSlideshowActive = false;
    private int stateChangeFrame = -1;

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
    }

    private void Awake()
    {
        // Al estar en el menú/intro, asegurar que la progresión, inventario y recompensas arranquen desde cero
        PlayerStats.ResetSavedProgression();
        InventoryManager.ResetSavedInventory();
        BattleManager.ResetStarterWeaponAwarded();

        LoadSfxClipsIfMissing();
        if (photoPlaceholderImage != null)
        {
            photoPlaceholderImage.preserveAspect = true;
        }
        ApplyPhotosToSlides();

        // Asegurar que en la escena de la intro no existan botones ni menús de inventario ni de nivel
        PlayerInventoryAndWeaponsUI invUI = FindAnyObjectByType<PlayerInventoryAndWeaponsUI>(FindObjectsInactive.Include);
        if (invUI != null)
        {
            Destroy(invUI.gameObject);
        }
        PlayerLevelUpAndStatsUI statsUI = FindAnyObjectByType<PlayerLevelUpAndStatsUI>(FindObjectsInactive.Include);
        if (statsUI != null)
        {
            Destroy(statsUI.gameObject);
        }
    }

    public void ApplyPhotosToSlides()
    {
        if (slides == null) return;

        // Foto 1: Diapositiva 0 y 1 (Santa Felipina y Enrique pedaleando)
        if (photo1 != null)
        {
            if (slides.Length > 0 && slides[0].slidePhoto == null) slides[0].slidePhoto = photo1;
            if (slides.Length > 1 && slides[1].slidePhoto == null) slides[1].slidePhoto = photo1;
        }

        // Foto 2: Diapositiva 2 (El Accidente en alcantarilla)
        if (photo2 != null)
        {
            if (slides.Length > 2 && slides[2].slidePhoto == null) slides[2].slidePhoto = photo2;
        }

        // Foto 3: Diapositivas 3 en adelante (Despertar, 4 voces y paquete extraviado)
        if (photo3 != null)
        {
            for (int i = 3; i < slides.Length; i++)
            {
                if (slides[i].slidePhoto == null) slides[i].slidePhoto = photo3;
            }
        }
    }

    private void Start()
    {
        if (MusicManager.Instance != null)
        {
            if (menuMusicClip != null && MusicManager.Instance.menuMusic == null)
            {
                MusicManager.Instance.menuMusic = menuMusicClip;
            }
            MusicManager.Instance.PlayMenuMusic(instant: true);
        }

        SetupMenuButtonsAndModals();

        if (nextSlideButton != null)
        {
            nextSlideButton.onClick.RemoveListener(NextSlide);
            nextSlideButton.onClick.AddListener(NextSlide);
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController2D>();
        }
        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        if (startScreenPanel != null)
        {
            ShowStartScreen();
        }
        else
        {
            StartGame();
        }
    }

    public void SetupMenuButtonsAndModals()
    {
        // 1. Botón Comenzar la Aventura
        if (adventureButton != null)
        {
            adventureButton.onClick.RemoveListener(StartGame);
            adventureButton.onClick.AddListener(StartGame);
            if (startGameButton == null) startGameButton = adventureButton;
        }
        else if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(StartGame);
            startGameButton.onClick.AddListener(StartGame);
            adventureButton = startGameButton;
        }

        // 2. Botón Configuración
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
            settingsButton.onClick.AddListener(OpenSettings);
        }

        // 3. Botón Créditos
        if (creditsButton != null)
        {
            creditsButton.onClick.RemoveListener(OpenCredits);
            creditsButton.onClick.AddListener(OpenCredits);
        }

        // 4. Botón Salir
        if (quitButton != null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            quitButton.gameObject.SetActive(false);
#else
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
#endif
        }

        // 5. Controles dentro de Configuración
        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveListener(CloseSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }

        if (musicVolumeSlider != null)
        {
            float musicVol = (MusicManager.Instance != null) ? MusicManager.Instance.masterVolume : 0.8f;
            musicVolumeSlider.value = musicVol;
            UpdateMusicVolumeDisplay(musicVol);
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            float sfxVol = (MusicManager.Instance != null) ? MusicManager.Instance.sfxVolume : 0.9f;
            sfxVolumeSlider.value = sfxVol;
            UpdateSfxVolumeDisplay(sfxVol);
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        // 6. Controles dentro de Créditos
        if (closeCreditsButton != null)
        {
            closeCreditsButton.onClick.RemoveListener(CloseCredits);
            closeCreditsButton.onClick.AddListener(CloseCredits);
        }
    }

    private void Update()
    {
        if (Time.frameCount == stateChangeFrame)
            return;

        if (isInStartScreen)
        {
            // Cerrar panel abierto con ESC
            if (WasCancelPressed())
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    CloseSettings();
                    return;
                }
                if (creditsPanel != null && creditsPanel.activeSelf)
                {
                    CloseCredits();
                    return;
                }
            }

            // Si hay un modal abierto (Configuración o Créditos), no iniciar aventura con teclado
            bool isModalOpen = (settingsPanel != null && settingsPanel.activeSelf) ||
                               (creditsPanel != null && creditsPanel.activeSelf);

            if (!isModalOpen && WasKeyboardAdvancePressed())
            {
                StartGame();
            }
            return;
        }

        if (isSlideshowActive)
        {
            if (WasAdvanceInputPressed())
            {
                NextSlide();
            }
        }
    }

    public void ShowStartScreen()
    {
        isInStartScreen = true;
        isSlideshowActive = false;
        stateChangeFrame = Time.frameCount;

        if (startScreenPanel != null)
            startScreenPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (slideshowPanel != null)
            slideshowPanel.SetActive(false);
    }

    public void StartGame()
    {
        // Si hay un modal abierto, no arrancar juego accidentalmente
        if (settingsPanel != null && settingsPanel.activeSelf) return;
        if (creditsPanel != null && creditsPanel.activeSelf) return;

        PlayerStats.ResetSavedProgression();
        InventoryManager.ResetSavedInventory();
        BattleManager.ResetStarterWeaponAwarded();

        if (isInStartScreen)
        {
            PlayDialogueConfirmSfx();
        }
        isInStartScreen = false;
        isSlideshowActive = true;
        currentSlideIndex = 0;
        stateChangeFrame = Time.frameCount;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (startScreenPanel != null)
            startScreenPanel.SetActive(false);

        if (slideshowPanel != null)
            slideshowPanel.SetActive(true);

        if (slides != null && slides.Length > 0)
        {
            ShowSlide(currentSlideIndex);
        }
        else
        {
            FinishIntro();
        }
    }

    // =========================================================================
    // ACCIONES DE CONFIGURACIÓN Y CRÉDITOS
    // =========================================================================

    public void OpenSettings()
    {
        PlayDialogueConfirmSfx();
        if (creditsPanel != null) creditsPanel.SetActive(false);

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

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        PlayDialogueConfirmSfx();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

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
            // Sonido de confirmación breve para evaluar el volumen
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }
        UpdateSfxVolumeDisplay(value);
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

    public void OpenCredits()
    {
        PlayDialogueConfirmSfx();
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
        }
    }

    public void CloseCredits()
    {
        PlayDialogueConfirmSfx();
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }

    public void QuitGame()
    {
        PlayDialogueConfirmSfx();
        Debug.Log("[IntroSlideshowManager] Saliendo de la aventura...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void NextSlide()
    {
        if (!isSlideshowActive)
            return;

        PlayDialogueConfirmSfx();

        stateChangeFrame = Time.frameCount;
        currentSlideIndex++;

        if (slides != null && currentSlideIndex < slides.Length)
        {
            ShowSlide(currentSlideIndex);
        }
        else
        {
            FinishIntro();
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

    private void ShowSlide(int index)
    {
        SlideData slide = slides[index];

        // 1. Actualizar Foto Principal con ratio preservado y nitidez perfecta
        if (photoPlaceholderImage != null)
        {
            photoPlaceholderImage.preserveAspect = true;

            Sprite photoToUse = slide.slidePhoto;
            if (photoToUse == null)
            {
                if (index == 0 || index == 1) photoToUse = photo1;
                else if (index == 2) photoToUse = photo2;
                else photoToUse = photo3;
            }

            if (photoToUse != null)
            {
                photoPlaceholderImage.sprite = photoToUse;
                photoPlaceholderImage.color = Color.white;
            }
            else
            {
                photoPlaceholderImage.sprite = null;
                photoPlaceholderImage.color = slide.placeholderColor;
            }
        }

        if (photoLabelText != null)
        {
            bool showPhotoPlaceholder = (photoPlaceholderImage == null || photoPlaceholderImage.sprite == null);
            photoLabelText.gameObject.SetActive(showPhotoPlaceholder);
            photoLabelText.text = showPhotoPlaceholder ? slide.placeholderPhotoLabel : string.Empty;
        }

        // 2. Actualizar Portrait y Hablante según los 3 tipos de diálogo
        ApplyDialogueStyle(slide);

        // 3. Actualizar Texto Narrativo
        if (narrativeText != null)
        {
            narrativeText.text = slide.storyText;
        }
    }

    private void ApplyDialogueStyle(SlideData slide)
    {
        bool hasPortrait = (slide.speakerType != DialogueSpeakerType.Narrator);

        if (portraitContainer != null)
            portraitContainer.SetActive(hasPortrait);

        // Ajustar el margen izquierdo del texto dinámicamente según si hay portrait o no
        float leftAnchorX = hasPortrait ? 0.21f : 0.03f;
        if (speakerNameText != null)
        {
            RectTransform rt = speakerNameText.rectTransform;
            rt.anchorMin = new Vector2(leftAnchorX, rt.anchorMin.y);
        }
        if (narrativeText != null)
        {
            RectTransform rt = narrativeText.rectTransform;
            rt.anchorMin = new Vector2(leftAnchorX, rt.anchorMin.y);
        }

        switch (slide.speakerType)
        {
            case DialogueSpeakerType.Narrator:
                if (speakerNameText != null)
                {
                    speakerNameText.text = "NARRADOR";
                    speakerNameText.color = new Color(0.75f, 0.78f, 0.85f, 1f);
                }
                break;

            case DialogueSpeakerType.Character:
                string charName = string.IsNullOrWhiteSpace(slide.characterName) ? "Personaje" : slide.characterName;
                bool isEnrique = charName.IndexOf("Enrique", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Sprite charSprite = slide.customPortraitSprite != null
                    ? slide.customPortraitSprite
                    : (isEnrique ? enriquePortrait : null);

                if (portraitImage != null)
                {
                    if (charSprite != null)
                    {
                        portraitImage.sprite = charSprite;
                        portraitImage.color = Color.white;
                        portraitImage.preserveAspect = false;
                    }
                    else
                    {
                        portraitImage.sprite = null;
                        portraitImage.color = characterGrayColor;
                    }
                }

                if (portraitLabelText != null)
                {
                    bool showLabel = (charSprite == null);
                    portraitLabelText.gameObject.SetActive(showLabel);
                    portraitLabelText.text = showLabel ? $"RETRATO\n[GRIS]\n{charName}" : string.Empty;
                }

                if (speakerNameText != null)
                {
                    speakerNameText.text = charName.ToUpper();
                    speakerNameText.color = new Color(0.90f, 0.92f, 0.95f, 1f);
                }
                break;

            case DialogueSpeakerType.SkillVoice:
                Color voiceColor = GetSkillVoiceColor(slide.skillVoice);
                string voiceDisplayName = GetSkillVoiceDisplayName(slide.skillVoice);
                Sprite voiceSprite = slide.customPortraitSprite != null
                    ? slide.customPortraitSprite
                    : GetSkillVoicePortrait(slide.skillVoice);

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
                    portraitLabelText.text = showLabel ? $"VOZ\n[{voiceDisplayName}]" : string.Empty;
                }

                if (speakerNameText != null)
                {
                    speakerNameText.text = $"VOZ: {voiceDisplayName}";
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

    public Color GetSkillVoiceColor(SkillVoiceType voice)
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

    public static string GetSkillVoiceDisplayName(SkillVoiceType voice)
    {
        switch (voice)
        {
            case SkillVoiceType.Corporeo:
                return "CORPOREO";
            case SkillVoiceType.Intelecto:
                return "INTELECTO";
            case SkillVoiceType.Motricidad:
                return "MOTRICIDAD";
            case SkillVoiceType.MundoInterno:
                return "MUNDO INTERNO";
            default:
                return voice.ToString().ToUpper();
        }
    }

    private void FinishIntro()
    {
        isSlideshowActive = false;
        stateChangeFrame = Time.frameCount;

        if (slideshowPanel != null)
        {
            slideshowPanel.SetActive(false);
        }

        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        onIntroFinished?.Invoke();

        if (loadSceneOnFinish && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            PlayerStats.ResetSavedProgression();
            InventoryManager.ResetSavedInventory();
            DetectiveStats.ResetSavedProgression();
            BattleManager.ResetStarterWeaponAwarded();

            SceneManager.LoadScene(nextSceneName);
        }
    }

    private bool WasKeyboardAdvancePressed()
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

    private bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            return true;
        }
#endif
        return false;
    }

    private bool WasAdvanceInputPressed()
    {
        if (WasKeyboardAdvancePressed())
            return true;

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
