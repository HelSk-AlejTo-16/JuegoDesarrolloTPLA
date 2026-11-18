using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tipo de pista de música para los diferentes estados del juego.
/// </summary>
public enum MusicTrackType
{
    None,
    Menu,        // Chill Menu Loopeable (Escena inicial / Menú / Diapositivas)
    Exploration, // Crashing Out Loopeable (Escena normal / Fuera de combate)
    Battle,      // Critical Theme Loopeable (Combate por turnos)
    VictoryMenu, // VictoryLap_Loopable (Menú de Subida de Nivel / Menú Fin de Combate)
    GameOver     // 80s Zombies Movie (Pantalla de Game Over)
}

/// <summary>
/// Gestor centralizado de música de fondo (BGM) y efectos de sonido (SFX) para todo el juego.
/// Mantiene la música sonando sin cortes entre escenas (DontDestroyOnLoad),
/// realiza transiciones suaves (crossfade) y reproduce los efectos de sonido de Assets/Sound/.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public const string PathSfxAtaqueAliadoPropio = "Assets/Sound/EfectodeSonido_AtaqueAliadoPropio/DSGNImpt_EXPLOSION-Mecha Damage_HY_PC-001.wav";
    public const string PathSfxAtaqueEnemigo = "Assets/Sound/EfectodeSonido_AtaqueEnemigo/DSGNImpt_EXPLOSION-Forced Shutdown_HY_PC-006.wav";
    public const string FolderSfxAtaqueJefe = "Assets/Sound/EfectodeSonido_AtaqueJefer";
    public const string PathSfxBotonEnCombate = "Assets/Sound/EfectodeSonido_BotonEnCombate/DSGNMisc_HIT-Bitcrusher_HY_PC-002.wav";
    public const string PathSfxBotonConfirmacionDialogo = "Assets/Sound/EfectodeSonido_BotóndeConfirmaciónDialogo/JDSherbert - Pixel UI SFX Pack - Select 1 (Sine).wav";
    public const string PathSfxCurarse = "Assets/Sound/EfectodeSonido_Curarse/DSGNImpt_EXPLOSION-Magically_HY_PC-001.wav";
    public const string PathSfxEfecto = "Assets/Sound/EfectodeSonido_Efecto/DSGNMisc_HIT-Zap Laser_HY_PC-005.wav";
    public const string PathSfxEsquive = "Assets/Sound/EfectodeSonido_Esquive/JDSherbert - Pixel UI SFX Pack - Cursor 2 (Sine).wav";
    public const string PathSfxFuego = "Assets/Sound/EfectodeSonido_Fuego/DSGNImpt_EXPLOSION-Sand Impact_HY_PC-001.wav";

    public const string PathMusicVictoryMenu = "Assets/Sound/MusicaDelMenuDesubidadeNivelMenuFindeCombate/VictoryLap_Loopable.wav";
    public const string PathMusicGameOver = "Assets/Sound/MusicaGameOver/Clement Panchout _ 80s Zombies Movie _ 2018.wav";
    public const string PathSfxDanoFinalMuerte = "Assets/Sound/SonidoDeDañoFinalMuerte/death_3_alex.wav";
    public const string PathSfxEncuentro = "Assets/Sound/SonidoDeEncuentro/DSGNImpt_EXPLOSION-Forced Interruption_HY_PC-006.wav";
    public const string PathSfxPasos = "Assets/Sound/SonidoDePasos/FEETMisc_STEP-Boots on Concrete_HY_PC-001.wav";
    public const string PathSfxSubidaDeNivel = "Assets/Sound/SonidoDeSubidaDeNivel/MAGSpel_CAST-Skill Ready_HY_PC-001.wav";
    public const string PathSfxMuerteEnemigo = "Assets/Sound/SonidoMuerteEnemigo/grunting_2_ian.wav";

    private static MusicManager instance;

    public static MusicManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<MusicManager>();
                if (instance == null)
                {
                    GameObject prefab = Resources.Load<GameObject>("MusicManager");
                    if (prefab != null)
                    {
                        GameObject go = Instantiate(prefab);
                        go.name = "MusicManager";
                        instance = go.GetComponent<MusicManager>();
                    }
                    else
                    {
                        GameObject go = new GameObject("MusicManager");
                        instance = go.AddComponent<MusicManager>();
                    }
                }
            }
            return instance;
        }
    }

    [Header("1. Pistas de Música (Assets en Music/)")]
    [Tooltip("Música de la escena inicial / Menú (Chill Menu Loopeable).")]
    public AudioClip menuMusic;

    [Tooltip("Música de la escena normal / exploración fuera de combate (Crashing Out Loopeable).")]
    public AudioClip explorationMusic;

    [Tooltip("Música de la escena de combate (Critical Theme Loopeable).")]
    public AudioClip battleMusic;

    [Header("2. Configuración de Volumen y Fundido")]
    [Range(0f, 1f)]
    [Tooltip("Volumen maestro de la música.")]
    public float masterVolume = 0.8f;

    [Tooltip("Duración en segundos del fundido cruzado (crossfade) entre pistas.")]
    public float crossfadeDuration = 0.8f;

    [Header("3. Detección Automática de Escenas")]
    [Tooltip("Si está activo, detecta automáticamente la escena al cargar para reproducir Menú o Exploración.")]
    public bool autoHandleScenes = true;

    [Tooltip("Nombres o fragmentos de escenas que usan la música de Menú/Intro.")]
    public string[] menuSceneKeywords = new string[] { "00_MenuIntro", "Menu", "Intro" };

    [Header("4. Efectos de Sonido (Assets en Sound/)")]
    [Tooltip("EfectodeSonido_AtaqueAliadoPropio: Golpe físico de Enrique.")]
    public AudioClip sfxAtaqueAliadoPropio;

    [Tooltip("EfectodeSonido_AtaqueEnemigo: Ataque de enemigo común contra Enrique.")]
    public AudioClip sfxAtaqueEnemigo;

    [Tooltip("EfectodeSonido_AtaqueJefer: Ataque de jefe contra Enrique.")]
    public AudioClip sfxAtaqueJefe;

    [Tooltip("EfectodeSonido_BotonEnCombate: Clic en botones de combate.")]
    public AudioClip sfxBotonEnCombate;

    [Tooltip("EfectodeSonido_BotóndeConfirmaciónDialogo: Confirmar o avanzar diálogo.")]
    public AudioClip sfxBotonConfirmacionDialogo;

    [Tooltip("EfectodeSonido_Curarse: Uso de objeto o receta curativa.")]
    public AudioClip sfxCurarse;

    [Tooltip("EfectodeSonido_Efecto: Habilidades de Mundo Interno (Insultar, Animarse, Delirio).")]
    public AudioClip sfxEfecto;

    [Tooltip("EfectodeSonido_Esquive: Guardia y esquive con Motricidad.")]
    public AudioClip sfxEsquive;

    [Tooltip("EfectodeSonido_Fuego: Explosión de fuego al lanzar una Molotov.")]
    public AudioClip sfxFuego;

    [Tooltip("MusicaDelMenuDesubidadeNivelMenuFindeCombate: Música del menú de fin de combate y subida de nivel.")]
    public AudioClip victoryMenuMusic;

    [Tooltip("MusicaGameOver: Música de la pantalla de Game Over.")]
    public AudioClip gameOverMusic;

    [Tooltip("SonidoDeDañoFinalMuerte: Sonido cuando Enrique recibe el golpe final y cae derrotado.")]
    public AudioClip sfxDanoFinalMuerte;

    [Tooltip("SonidoDeEncuentro: Sonido al iniciar un encuentro de combate con enemigos.")]
    public AudioClip sfxEncuentro;

    [Tooltip("SonidoDePasos: Sonido de pasos de Enrique al caminar por el mapa.")]
    public AudioClip sfxPasos;

    [Tooltip("SonidoDeSubidaDeNivel: Sonido especial al subir de nivel o mejorar una habilidad.")]
    public AudioClip sfxSubidaDeNivel;

    [Tooltip("SonidoMuerteEnemigo: Sonido cuando un enemigo es derrotado en combate.")]
    public AudioClip sfxMuerteEnemigo;

    [Range(0f, 1f)]
    [Tooltip("Volumen maestro de los efectos de sonido (SFX).")]
    public float sfxVolume = 0.9f;

    // Dos AudioSources para permitir fundido cruzado (crossfade) sin cortes y uno dedicado a SFX
    private AudioSource audioSourceA;
    private AudioSource audioSourceB;
    private AudioSource sfxAudioSource;
    private AudioSource activeAudioSource;
    private AudioSource fadingAudioSource;

    private Coroutine crossfadeCoroutine;
    private MusicTrackType currentTrack = MusicTrackType.None;

    public MusicTrackType CurrentTrack => currentTrack;
    public bool IsPlaying => activeAudioSource != null && activeAudioSource.isPlaying;
    public float Volume => masterVolume;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (PlayerPrefs.HasKey("MusicVolume"))
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", masterVolume));
        }
        if (PlayerPrefs.HasKey("SFXVolume"))
        {
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", sfxVolume));
        }

        SetupAudioSources();
        LoadClipsIfMissing();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnValidate()
    {
        LoadClipsIfMissing();
    }

    private void Start()
    {
        // Al arrancar, evaluar la escena activa y comenzar a reproducir inmediatamente
        if (autoHandleScenes)
        {
            EvaluateSceneAndPlayMusic(SceneManager.GetActiveScene().name, instant: true);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void SetupAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 3)
        {
            audioSourceA = sources[0];
            audioSourceB = sources[1];
            sfxAudioSource = sources[2];
        }
        else if (sources.Length == 2)
        {
            audioSourceA = sources[0];
            audioSourceB = sources[1];
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }
        else if (sources.Length == 1)
        {
            audioSourceA = sources[0];
            audioSourceB = gameObject.AddComponent<AudioSource>();
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            audioSourceA = gameObject.AddComponent<AudioSource>();
            audioSourceB = gameObject.AddComponent<AudioSource>();
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(audioSourceA);
        ConfigureAudioSource(audioSourceB);
        ConfigureSfxAudioSource(sfxAudioSource);

        activeAudioSource = audioSourceA;
        fadingAudioSource = audioSourceB;
    }

    private void ConfigureAudioSource(AudioSource source)
    {
        if (source == null) return;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D Stereo
        source.priority = 0;      // Máxima prioridad
    }

    private void ConfigureSfxAudioSource(AudioSource source)
    {
        if (source == null) return;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D Stereo
        source.priority = 16;
        source.volume = sfxVolume;
    }

    public void LoadClipsIfMissing()
    {
        if (menuMusic == null)
            menuMusic = RuntimeAssetCatalog.GetAudioClip("Assets/Music/ChillMenu_Loopable.wav");
        if (explorationMusic == null)
            explorationMusic = RuntimeAssetCatalog.GetAudioClip("Assets/Music/CrashingOut_Loopable.wav");
        if (battleMusic == null)
            battleMusic = RuntimeAssetCatalog.GetAudioClip("Assets/Music/CriticalTheme_Loopable.wav");

        if (sfxAtaqueAliadoPropio == null)
            sfxAtaqueAliadoPropio = RuntimeAssetCatalog.GetAudioClip(PathSfxAtaqueAliadoPropio);
        if (sfxAtaqueEnemigo == null)
            sfxAtaqueEnemigo = RuntimeAssetCatalog.GetAudioClip(PathSfxAtaqueEnemigo);
#if UNITY_EDITOR
        if (sfxAtaqueJefe == null)
            sfxAtaqueJefe = LoadFirstAudioClipInFolderEditor(FolderSfxAtaqueJefe);
#endif
        if (sfxBotonEnCombate == null)
            sfxBotonEnCombate = RuntimeAssetCatalog.GetAudioClip(PathSfxBotonEnCombate);
        if (sfxBotonConfirmacionDialogo == null)
            sfxBotonConfirmacionDialogo = RuntimeAssetCatalog.GetAudioClip(PathSfxBotonConfirmacionDialogo);
        if (sfxCurarse == null)
            sfxCurarse = RuntimeAssetCatalog.GetAudioClip(PathSfxCurarse);
        if (sfxEfecto == null)
            sfxEfecto = RuntimeAssetCatalog.GetAudioClip(PathSfxEfecto);
        if (sfxEsquive == null)
            sfxEsquive = RuntimeAssetCatalog.GetAudioClip(PathSfxEsquive);
        if (sfxFuego == null)
            sfxFuego = RuntimeAssetCatalog.GetAudioClip(PathSfxFuego);

        if (victoryMenuMusic == null)
            victoryMenuMusic = RuntimeAssetCatalog.GetAudioClip(PathMusicVictoryMenu);
        if (gameOverMusic == null)
            gameOverMusic = RuntimeAssetCatalog.GetAudioClip(PathMusicGameOver);
        if (sfxDanoFinalMuerte == null)
            sfxDanoFinalMuerte = RuntimeAssetCatalog.GetAudioClip(PathSfxDanoFinalMuerte);
        if (sfxEncuentro == null)
            sfxEncuentro = RuntimeAssetCatalog.GetAudioClip(PathSfxEncuentro);
        if (sfxPasos == null)
            sfxPasos = RuntimeAssetCatalog.GetAudioClip(PathSfxPasos);
        if (sfxSubidaDeNivel == null)
            sfxSubidaDeNivel = RuntimeAssetCatalog.GetAudioClip(PathSfxSubidaDeNivel);
        if (sfxMuerteEnemigo == null)
            sfxMuerteEnemigo = RuntimeAssetCatalog.GetAudioClip(PathSfxMuerteEnemigo);

        // Copiar referencias faltantes desde el prefab en Resources/MusicManager si una instancia de escena no las tenía serializadas
        GameObject resPrefab = Resources.Load<GameObject>("MusicManager");
        if (resPrefab != null)
        {
            MusicManager prefabMM = resPrefab.GetComponent<MusicManager>();
            if (prefabMM != null && prefabMM != this)
            {
                if (menuMusic == null) menuMusic = prefabMM.menuMusic;
                if (explorationMusic == null) explorationMusic = prefabMM.explorationMusic;
                if (battleMusic == null) battleMusic = prefabMM.battleMusic;
                if (victoryMenuMusic == null) victoryMenuMusic = prefabMM.victoryMenuMusic;
                if (gameOverMusic == null) gameOverMusic = prefabMM.gameOverMusic;

                if (sfxAtaqueAliadoPropio == null) sfxAtaqueAliadoPropio = prefabMM.sfxAtaqueAliadoPropio;
                if (sfxAtaqueEnemigo == null) sfxAtaqueEnemigo = prefabMM.sfxAtaqueEnemigo;
                if (sfxAtaqueJefe == null) sfxAtaqueJefe = prefabMM.sfxAtaqueJefe;
                if (sfxBotonEnCombate == null) sfxBotonEnCombate = prefabMM.sfxBotonEnCombate;
                if (sfxBotonConfirmacionDialogo == null) sfxBotonConfirmacionDialogo = prefabMM.sfxBotonConfirmacionDialogo;
                if (sfxCurarse == null) sfxCurarse = prefabMM.sfxCurarse;
                if (sfxEfecto == null) sfxEfecto = prefabMM.sfxEfecto;
                if (sfxEsquive == null) sfxEsquive = prefabMM.sfxEsquive;
                if (sfxFuego == null) sfxFuego = prefabMM.sfxFuego;
                if (sfxDanoFinalMuerte == null) sfxDanoFinalMuerte = prefabMM.sfxDanoFinalMuerte;
                if (sfxEncuentro == null) sfxEncuentro = prefabMM.sfxEncuentro;
                if (sfxPasos == null) sfxPasos = prefabMM.sfxPasos;
                if (sfxSubidaDeNivel == null) sfxSubidaDeNivel = prefabMM.sfxSubidaDeNivel;
                if (sfxMuerteEnemigo == null) sfxMuerteEnemigo = prefabMM.sfxMuerteEnemigo;
            }
        }

        if (menuMusic == null)
            menuMusic = Resources.Load<AudioClip>("ChillMenu_Loopable");
        if (explorationMusic == null)
            explorationMusic = Resources.Load<AudioClip>("CrashingOut_Loopable");
        if (battleMusic == null)
            battleMusic = Resources.Load<AudioClip>("CriticalTheme_Loopable");

        if (sfxAtaqueAliadoPropio == null)
            sfxAtaqueAliadoPropio = Resources.Load<AudioClip>("SFX/EfectodeSonido_AtaqueAliadoPropio");
        if (sfxAtaqueEnemigo == null)
            sfxAtaqueEnemigo = Resources.Load<AudioClip>("SFX/EfectodeSonido_AtaqueEnemigo");
        if (sfxAtaqueJefe == null)
            sfxAtaqueJefe = Resources.Load<AudioClip>("SFX/EfectodeSonido_AtaqueJefer");
        if (sfxBotonEnCombate == null)
            sfxBotonEnCombate = Resources.Load<AudioClip>("SFX/EfectodeSonido_BotonEnCombate");
        if (sfxBotonConfirmacionDialogo == null)
            sfxBotonConfirmacionDialogo = Resources.Load<AudioClip>("SFX/EfectodeSonido_BotonConfirmacionDialogo");
        if (sfxCurarse == null)
            sfxCurarse = Resources.Load<AudioClip>("SFX/EfectodeSonido_Curarse");
        if (sfxEfecto == null)
            sfxEfecto = Resources.Load<AudioClip>("SFX/EfectodeSonido_Efecto");
        if (sfxEsquive == null)
            sfxEsquive = Resources.Load<AudioClip>("SFX/EfectodeSonido_Esquive");
        if (sfxFuego == null)
            sfxFuego = Resources.Load<AudioClip>("SFX/EfectodeSonido_Fuego");
    }

#if UNITY_EDITOR
    public static AudioClip LoadFirstAudioClipInFolderEditor(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !UnityEditor.AssetDatabase.IsValidFolder(folderPath))
            return null;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip != null)
                return clip;
        }
        return null;
    }
#endif

    // =========================================================================
    // REPRODUCCIÓN DE EFECTOS DE SONIDO (SFX)
    // =========================================================================

    public void PlaySFX(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        if (sfxAudioSource == null)
        {
            SetupAudioSources();
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        if (sfxAudioSource != null)
        {
            sfxAudioSource.pitch = pitch;
            sfxAudioSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeScale));
        }
    }

    public void PlayAtaqueAliadoPropioSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxAtaqueAliadoPropio, 1.0f, 1.0f);
    }

    public void PlayAtaqueEnemigoSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxAtaqueEnemigo, 1.0f, 1.0f);
    }

    public void PlayAtaqueJefeSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        AudioClip clip = overrideClip != null ? overrideClip : sfxAtaqueJefe;
        if (clip != null)
        {
            PlaySFX(clip, 1.0f, 1.0f);
        }
        else if (sfxAtaqueEnemigo != null)
        {
            // Respaldo contundente si la carpeta EfectodeSonido_AtaqueJefer aún no tiene un archivo propio
            PlaySFX(sfxAtaqueEnemigo, 1.0f, 0.80f);
        }
    }

    public void PlayBotonEnCombateSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxBotonEnCombate, 0.85f, 1.0f);
    }

    public void PlayBotonConfirmacionDialogoSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxBotonConfirmacionDialogo, 0.90f, 1.0f);
    }

    public void PlayCurarseSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxCurarse, 1.0f, 1.0f);
    }

    public void PlayEfectoSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxEfecto, 1.0f, 1.0f);
    }

    public void PlayRecogerObjetoSfx(AudioClip overrideClip = null)
    {
        PlayEfectoSfx(overrideClip);
    }

    public void PlayEsquiveSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxEsquive, 1.0f, 1.0f);
    }

    public void PlayFuegoSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxFuego, 1.0f, 1.0f);
    }

    public void PlayDanoFinalMuerteSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxDanoFinalMuerte, 1.0f, 1.0f);
    }

    public void PlayEncuentroSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxEncuentro, 1.0f, 1.0f);
    }

    public void PlayPasosSfx(AudioClip overrideClip = null, float pitch = 1.0f)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxPasos, 0.55f, pitch);
    }

    public void PlaySubidaDeNivelSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxSubidaDeNivel, 1.0f, 1.0f);
    }

    public void PlayMuerteEnemigoSfx(AudioClip overrideClip = null)
    {
        LoadClipsIfMissing();
        PlaySFX(overrideClip != null ? overrideClip : sfxMuerteEnemigo, 1.0f, 1.0f);
    }

    // =========================================================================
    // REPRODUCCIÓN DE MÚSICA DE FONDO (BGM)
    // =========================================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;

        if (autoHandleScenes)
        {
            EvaluateSceneAndPlayMusic(scene.name, instant: false);
        }
    }

    public void EvaluateSceneAndPlayMusic(string sceneName, bool instant)
    {
        bool isMenu = false;
        if (!string.IsNullOrEmpty(sceneName) && menuSceneKeywords != null)
        {
            for (int i = 0; i < menuSceneKeywords.Length; i++)
            {
                if (!string.IsNullOrEmpty(menuSceneKeywords[i]) &&
                    sceneName.IndexOf(menuSceneKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isMenu = true;
                    break;
                }
            }
        }

        if (isMenu)
        {
            PlayMenuMusic(instant);
        }
        else
        {
            PlayExplorationMusic(instant);
        }
    }

    /// <summary>
    /// Reproduce la música de la escena inicial / menú (Chill Menu Loopeable).
    /// </summary>
    public void PlayMenuMusic(bool instant = false)
    {
        PlayTrack(MusicTrackType.Menu, instant);
    }

    /// <summary>
    /// Reproduce la música de la escena normal / exploración fuera de combate (Crashing Out Loopeable).
    /// </summary>
    public void PlayExplorationMusic(bool instant = false)
    {
        PlayTrack(MusicTrackType.Exploration, instant);
    }

    /// <summary>
    /// Reproduce la música de la escena de combate (Critical Theme Loopeable).
    /// </summary>
    public void PlayBattleMusic(bool instant = false)
    {
        PlayTrack(MusicTrackType.Battle, instant);
    }

    /// <summary>
    /// Reproduce la música del menú de subida de nivel / fin de combate (VictoryLap_Loopable).
    /// </summary>
    public void PlayVictoryMenuMusic(bool instant = false)
    {
        LoadClipsIfMissing();
        PlayTrack(MusicTrackType.VictoryMenu, instant);
    }

    /// <summary>
    /// Reproduce la música de Game Over (80s Zombies Movie).
    /// </summary>
    public void PlayGameOverMusic(bool instant = false)
    {
        LoadClipsIfMissing();
        PlayTrack(MusicTrackType.GameOver, instant);
    }

    /// <summary>
    /// Cambia a la pista indicada. Si ya está sonando la misma pista, no la reinicia.
    /// Si no hay nada sonando o se solicita instantáneo, inicia inmediatamente al volumen objetivo.
    /// </summary>
    public void PlayTrack(MusicTrackType trackType, bool instant = false)
    {
        AudioClip targetClip = GetClipForTrack(trackType);

        if (targetClip == null)
        {
            Debug.LogWarning($"[MusicManager] No se encontró AudioClip asignado para el track '{trackType}'.");
            return;
        }

        // Si ya está sonando activamente esta pista
        if (currentTrack == trackType && activeAudioSource != null && activeAudioSource.clip == targetClip && activeAudioSource.isPlaying)
        {
            return;
        }

        currentTrack = trackType;

        // Si no hay música activa o es inicio instantáneo, iniciar directo sin fundido
        if (instant || crossfadeDuration <= 0f || activeAudioSource == null || !activeAudioSource.isPlaying || activeAudioSource.clip == null)
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            if (fadingAudioSource != null)
            {
                fadingAudioSource.Stop();
                fadingAudioSource.volume = 0f;
                fadingAudioSource.clip = null;
            }

            if (activeAudioSource != null)
            {
                activeAudioSource.clip = targetClip;
                activeAudioSource.volume = masterVolume;
                activeAudioSource.loop = true;
                activeAudioSource.Play();
                Debug.Log($"[MusicManager] Reproduciendo '{targetClip.name}'. isPlaying={activeAudioSource.isPlaying}, loadState={targetClip.loadState}, length={targetClip.length}");
            }
        }
        else
        {
            if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(targetClip));
        }
    }

    private AudioClip GetClipForTrack(MusicTrackType trackType)
    {
        LoadClipsIfMissing();
        switch (trackType)
        {
            case MusicTrackType.Menu:
                return menuMusic;
            case MusicTrackType.Exploration:
                return explorationMusic;
            case MusicTrackType.Battle:
                return battleMusic;
            case MusicTrackType.VictoryMenu:
                return victoryMenuMusic != null ? victoryMenuMusic : explorationMusic;
            case MusicTrackType.GameOver:
                return gameOverMusic != null ? gameOverMusic : menuMusic;
            default:
                return null;
        }
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip)
    {
        AudioSource outgoing = activeAudioSource;
        AudioSource incoming = (activeAudioSource == audioSourceA) ? audioSourceB : audioSourceA;

        incoming.clip = newClip;
        incoming.volume = 0f;
        incoming.loop = true;
        incoming.Play();
        Debug.Log($"[MusicManager] Crossfade hacia '{newClip.name}'. isPlaying={incoming.isPlaying}, loadState={newClip.loadState}, length={newClip.length}");

        float startOutgoingVolume = (outgoing != null && outgoing.isPlaying) ? outgoing.volume : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, crossfadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (incoming != null) incoming.volume = Mathf.Lerp(0f, masterVolume, t);
            if (outgoing != null && outgoing.isPlaying && outgoing != incoming) outgoing.volume = Mathf.Lerp(startOutgoingVolume, 0f, t);

            yield return null;
        }

        if (incoming != null) incoming.volume = masterVolume;
        if (outgoing != null && outgoing != incoming)
        {
            outgoing.volume = 0f;
            outgoing.Stop();
            outgoing.clip = null;
        }

        activeAudioSource = incoming;
        fadingAudioSource = outgoing;
        crossfadeCoroutine = null;
    }

    /// <summary>
    /// Detiene la música actual con un fundido a silencio.
    /// </summary>
    public void StopMusic(float customFade = -1f)
    {
        currentTrack = MusicTrackType.None;
        float fade = (customFade >= 0f) ? customFade : crossfadeDuration;
        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(FadeOutRoutine(fade));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        AudioSource s1 = activeAudioSource;
        AudioSource s2 = fadingAudioSource;
        float startVol1 = (s1 != null && s1.isPlaying) ? s1.volume : 0f;
        float startVol2 = (s2 != null && s2.isPlaying) ? s2.volume : 0f;

        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (s1 != null && s1.isPlaying) s1.volume = Mathf.Lerp(startVol1, 0f, t);
            if (s2 != null && s2.isPlaying) s2.volume = Mathf.Lerp(startVol2, 0f, t);
            yield return null;
        }

        if (s1 != null) { s1.volume = 0f; s1.Stop(); s1.clip = null; }
        if (s2 != null) { s2.volume = 0f; s2.Stop(); s2.clip = null; }
        crossfadeCoroutine = null;
    }

    /// <summary>
    /// Ajusta el volumen maestro de la música (BGM) en tiempo real y persiste la preferencia.
    /// </summary>
    public void SetMusicVolume(float newVolume)
    {
        masterVolume = Mathf.Clamp01(newVolume);
        PlayerPrefs.SetFloat("MusicVolume", masterVolume);
        PlayerPrefs.Save();

        if (crossfadeCoroutine == null && activeAudioSource != null && activeAudioSource.isPlaying)
        {
            activeAudioSource.volume = masterVolume;
        }
    }

    /// <summary>
    /// Ajusta el volumen de los efectos de sonido (SFX) en tiempo real y persiste la preferencia.
    /// </summary>
    public void SetSfxVolume(float newVolume)
    {
        sfxVolume = Mathf.Clamp01(newVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.Save();

        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = sfxVolume;
        }
    }

    /// <summary>
    /// Ajusta el volumen maestro de la música en tiempo real (mantenido por compatibilidad).
    /// </summary>
    public void SetVolume(float newVolume)
    {
        SetMusicVolume(newVolume);
    }
}
