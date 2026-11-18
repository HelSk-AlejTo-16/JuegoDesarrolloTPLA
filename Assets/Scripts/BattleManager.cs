using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif


public enum BattleState
{
	INACTIVE,
	START,
	DIALOGUE,
	PLAYERTURN,
	DETECTIVETURN,
	PLAYING_GIF,
	ENEMYTURN,
	WON,
	LOST
}


[Serializable]
public class ActionGifData
{
	[Tooltip("Texto descriptivo mientras no existan los frames PNG extraídos con ffmpeg.")]
	public string actionLabel = "[GIF PLACEHOLDER: Accion actuada]";

	[Tooltip("Color sólido del recuadro central mientras no haya frames asignados.")]
	public Color placeholderColor = new Color(0.14f, 0.08f, 0.08f, 1f);

	[Tooltip("Opcional: Secuencia de sprites (frames extraídos con ffmpeg) para reproducir el GIF.")]
	public Sprite[] gifFrames;

	[Tooltip("Duración en segundos de la animación antes de aplicar el daño o efecto.")]
	public float duration = 1.5f;

	public ActionGifData()
	{
	}
}


[Serializable]
public class EnemyUISlot
{
	public GameObject slotRoot;

	public Image borderImage;

	public Image enemyPlaceholderImage;

	public TextMeshProUGUI enemyAvatarLabelText;

	public TextMeshProUGUI enemyNameText;

	public TextMeshProUGUI enemyHPText;

	public Button selectTargetButton;

	[Header("Indicador Dinámico de Selección")]
	public GameObject targetIndicator;

	public RectTransform targetIndicatorRect;

	public Image targetIndicatorImage;

	public TextMeshProUGUI targetIndicatorText;

	public Image groundIndicatorRing;
}

[Serializable]
public class CombatEnemyInstance
{
	public EnemyData data;

	public int currentHP;

	public bool isConfused;

	public int attackDebuff;

	public int defenseDebuff;

	public int motricidadDebuff;

	public int vulnerabilityTurns;

	public bool hasTriggeredHalfHealthDialogue;

	[NonSerialized]
	public Sprite[] overrideFrames;

	[NonSerialized]
	public int overrideFrameIndex;

	[NonSerialized]
	public float overrideFrameTimer;

	[NonSerialized]
	public float overrideFrameDuration = 0.12f;

	[NonSerialized]
	public bool holdLastOverrideFrame;

	[NonSerialized]
	public int idleFrameIndex;

	[NonSerialized]
	public float idleFrameTimer;

	[NonSerialized]
	public int idleLoopsCompleted;

	[NonSerialized]
	public bool playingIdleAlt;

	[NonSerialized]
	public int attackComboIndex;

	public bool IsAlive => currentHP > 0;
}

public class BattleManager : MonoBehaviour
{
	private struct TurnActor
	{
		public bool isPlayer;

		public bool isDetective;

		public int enemyIndex;

		public int motricidad;
	}

	[Header("1. Estado Actual del Combate")]
	public BattleState state;

	[Header("1b. Integración del Compañero Detective y Jefe Nivel 2")]
	[Tooltip("Estado de validación de equipo: si es true, el Detective entra al combate y a la cola de turnos.")]
	public bool hasDetectiveInParty;

	public DetectiveStats detectiveStats;

	public DetectiveActionMenu detectiveActionMenu;

	public Level2BossController activeLevel2BossController;

	private List<string> lastDetectiveAutoLevelUpLog = new List<string>();

	private int lastDetectiveExpGained;
	private int lastDetectiveLevelsGained;

	[Header("2. Referencias del Jugador y Enemigos Activos")]
	public PlayerStats playerStats;

	public PlayerController2D playerController;

	public MonoBehaviour playerMovementScript;

	public List<CombatEnemyInstance> activeEnemies = new List<CombatEnemyInstance>();

	public int selectedTargetIndex;

	public EnemyData currentEnemy;

	public int currentEnemyHP;

	[Header("3. Referencias UI Generales de Combate")]
	[Tooltip("Panel raíz de toda la interfaz de combate.")]
	public GameObject battlePanel;

	[Tooltip("Fondo visual del combate (calle escena1Combate).")]
	public Image combatBackgroundImage;

	[Tooltip("Slots visuales de los enemigos frente a la cámara (soporta 1, 2 o más enemigos en pantalla).")]
	public EnemyUISlot[] enemySlots;

	public Image enemyPlaceholderImage;

	public TextMeshProUGUI enemyAvatarLabelText;

	public TextMeshProUGUI enemyNameText;

	public TextMeshProUGUI enemyHPText;

	public Slider enemyHPSlider;

	[Tooltip("Texto de vida de Enrique (ej. ENRIQUE HP: 100 / 100).")]
	public TextMeshProUGUI playerHPText;

	public Slider playerHPSlider;

	[Tooltip("Componente avanzado de la barra de vida visual de Enrique.")]
	public PlayerHealthBarUI playerHealthBarUI;

	[Tooltip("Texto de registro/estado del combate y cola de turnos.")]
	public TextMeshProUGUI battleLogText;

	[Header("4. Botones de las 4 Voces / Acciones")]
	public Button corporeoButton;

	public Button intelectoButton;

	public Button motricidadButton;

	public Button mundoInternoButton;

	[Header("5. Panel de GIFs de Acción (Debajo de los enemigos, arriba de opciones)")]
	public GameObject actionGifPanel;

	public Image actionGifImage;

	public TextMeshProUGUI actionGifLabelText;

	[Header("6. Configuración de GIFs de las 4 Acciones y Enemigo")]
	public ActionGifData corporeoGif = new ActionGifData
	{
		actionLabel = "[GIF: CORPOREO - Golpe fisico directo]",
		placeholderColor = new Color(0.28f, 0.08f, 0.08f, 1f),
		duration = 1.5f
	};

	public ActionGifData intelectoGif = new ActionGifData
	{
		actionLabel = "[GIF: INTELECTO - Improvisando artefacto con chatarra]",
		placeholderColor = new Color(0.08f, 0.18f, 0.3f, 1f),
		duration = 1.5f
	};

	public ActionGifData motricidadGif = new ActionGifData
	{
		actionLabel = "[GIF: MOTRICIDAD - Guardia y reflejos de esquive]",
		placeholderColor = new Color(0.28f, 0.22f, 0.06f, 1f),
		duration = 1.5f
	};

	public ActionGifData mundoInternoGif = new ActionGifData
	{
		actionLabel = "[GIF: MUNDO INTERNO - Proyectando delirio psiquico]",
		placeholderColor = new Color(0.2f, 0.08f, 0.28f, 1f),
		duration = 1.5f
	};

	public ActionGifData enemyAttackGif = new ActionGifData
	{
		actionLabel = "[GIF: ENEMIGO - Lanzando ataque contra Enrique]",
		placeholderColor = new Color(0.18f, 0.18f, 0.2f, 1f),
		duration = 1.5f
	};

	public ActionGifData explosionGif = new ActionGifData
	{
		actionLabel = "[GIF: EXPLOSION - Fuego y metralla]",
		placeholderColor = new Color(0.88f, 0.35f, 0.08f, 1f),
		duration = 1.2f
	};

	[Header("7. Sub-Panel de Diálogos en Combate (Mismo diseño con Portrait)")]
	public GameObject combatDialoguePanel;

	public GameObject combatPortraitContainer;

	public Image combatPortraitImage;

	public TextMeshProUGUI combatPortraitLabelText;

	public TextMeshProUGUI combatSpeakerNameText;

	public TextMeshProUGUI combatDialogueText;

	public Button combatNextDialogueButton;

	[Header("7b. Sub-Panel de Crafteo y Objetos de Intelecto (intelectoAndItemsPanel)")]
	public GameObject intelectoAndItemsPanel;

	public GameObject intelectoCraftingPanel;

	public CombatIntelectoUI intelectoCraftingUI;

	[Header("7c. Sub-Panel de Mundo Interno (Habilidades Psicológicas)")]
	public GameObject mundoInternoPanel;

	public CombatMundoInternoUI mundoInternoUI;

	[Header("Configuración de GIFs de Mundo Interno")]
	public ActionGifData insultarGif = new ActionGifData
	{
		actionLabel = "[GIF: INSULTAR - Enrique lanza un insulto mordaz al enemigo]",
		placeholderColor = new Color(0.38f, 0.1f, 0.28f, 1f),
		duration = 1.5f
	};

	public ActionGifData animarseGif = new ActionGifData
	{
		actionLabel = "[GIF: ANIMARSE - Enrique respira hondo y se convence de su fuerza]",
		placeholderColor = new Color(0.35f, 0.2f, 0.45f, 1f),
		duration = 1.5f
	};

	[NonSerialized]
	public int playerAttackBuff;

	[Header("8. Retratos y Colores de Portraits y Selección")]
	public Image playerCombatPortraitImage;

	public Sprite enriquePortrait;

	public Sprite detectiveCombatPortrait;

	private bool lastEnemyTargetWasDetective = false;

	public Sprite corporeoPortrait;

	public Sprite intelectoPortrait;

	public Sprite motricidadPortrait;

	public Sprite mundoInternoPortrait;

	public Color characterGrayColor = new Color(0.38f, 0.4f, 0.43f, 1f);

	public Color corporeoColor = new Color(0.75f, 0.2f, 0.2f, 1f);

	public Color intelectoColor = new Color(0.22f, 0.52f, 0.82f, 1f);

	public Color motricidadColor = new Color(0.82f, 0.65f, 0.18f, 1f);

	public Color mundoInternoColor = new Color(0.58f, 0.26f, 0.78f, 1f);

	public Color selectedTargetBorderColor = new Color(0.92f, 0.68f, 0.22f, 1f);

	public Color normalEnemyBorderColor = new Color(0.26f, 0.26f, 0.3f, 1f);

	[Header("8b. Visualización de Arma en Retrato de Combate de Enrique")]
	public GameObject combatWeaponBadgeRoot;

	public TextMeshProUGUI combatWeaponBadgeText;

	public Image combatWeaponBadgeIcon;

	public Image combatPortraitFrameBorder;

	public Button combatPortraitClickButton;

	public GameObject combatInventoryButtonRoot;

	public Button combatInventoryButton;

	public TextMeshProUGUI combatInventoryButtonLabel;

	public Image combatInventoryButtonBorder;

	[Header("9. Pantalla de Game Over y Eventos de Fin de Combate")]
	[Tooltip("Panel raíz de la pantalla de Game Over cuando Enrique pierde toda su vida.")]
	public GameObject gameOverPanel;

	[Tooltip("Botón para reintentar en el mapa actual.")]
	public Button retryBattleButton;

	[Tooltip("Botón para volver al menú principal (00_MenuIntro).")]
	public Button returnToMenuButton;

	[Tooltip("Nombre de la escena del menú principal.")]
	public string menuSceneName = "00_MenuIntro";

	public UnityEvent onBattleWon;

	public UnityEvent onBattleLost;

	[Header("10. Transición de Escenario al Vencer al Jefe (Boss Victory)")]
	[Tooltip("UI para el fundido a negro y textos narrativos tras derrotar a un Boss.")]
	public BossVictoryTransitionUI bossVictoryTransitionUI;

	[Header("11. Efectos de Sonido de Combate (Assets/Sound)")]
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

	[Tooltip("SonidoDeDañoFinalMuerte: Efecto de sonido cuando Enrique recibe el golpe final y muere.")]
	public AudioClip sfxDanoFinalMuerte;

	[Tooltip("SonidoDeEncuentro: Efecto de sonido de interrupción al iniciar un encuentro de combate.")]
	public AudioClip sfxEncuentro;

	[Tooltip("SonidoDeSubidaDeNivel: Efecto de sonido al subir de nivel o mejorar una habilidad.")]
	public AudioClip sfxSubidaDeNivel;

	[Tooltip("SonidoMuerteEnemigo: Efecto de sonido cuando un enemigo es derrotado.")]
	public AudioClip sfxMuerteEnemigo;

	[Tooltip("Efecto de sonido de disparo con el Revólver .38 del Detective Vargas.")]
	public AudioClip sfxDisparoRevolver38;

	[Tooltip("Efecto de sonido de recarga de tambor del Revólver .38 del Detective Vargas.")]
	public AudioClip sfxRecargaRevolver;

	[HideInInspector]
	public bool isCurrentEncounterBoss;

	[HideInInspector]
	public string currentBossNextScene;

	[HideInInspector]
	public string[] currentBossVictoryNarrative;

	private List<TurnActor> roundTurnOrder = new List<TurnActor>();

	private int currentTurnCursor;

	private bool isMotricidadDefending;

	private string[] currentDialogueLines;

	private int currentDialogueIndex;

	private Action onDialogueFinishedCallback;

	private int dialogueStateChangeFrame = -1;

	private GameObject[] currentEncounterObjectsToDisable;

	[Header("12. Recompensas de Combate (Armas y Objetos)")]
	[Tooltip("Arma otorgada por defecto al vencer al primer enemigo (Cadena de Bicicleta).")]
	public ItemData starterRewardWeapon;

	private ItemData currentRewardItem;

	private List<InventorySlot> currentRewardBundle;

	private string[] currentRewardDialogueLines;

	private static bool hasAwardedStarterWeapon;
	private static Sprite[] s_cachedAttackGifFrames;
	private static Sprite[] s_cachedExplosionGifFrames;
	private static Sprite s_cachedCombatBgSprite;

	[RuntimeInitializeOnLoadMethod(/*Could not decode attribute arguments.*/)]
	private static void OnSubsystemRegistration()
	{
		hasAwardedStarterWeapon = false;
	}

	[RuntimeInitializeOnLoadMethod(/*Could not decode attribute arguments.*/)]
	private static void OnBeforeSceneLoad()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu") || activeScene.name.Contains("01_Nivel1")))
		{
			ResetStarterWeaponAwarded();
		}
	}

	public static void ResetStarterWeaponAwarded()
	{
		hasAwardedStarterWeapon = false;
	}

	private void Awake()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu") || activeScene.name.Contains("01_Nivel1")))
		{
			ResetStarterWeaponAwarded();
		}
	}

	private void OnValidate()
	{
		LoadSfxClipsIfMissing();
	}

	public void LoadSfxClipsIfMissing()
	{
		if ((Object)(object)sfxAtaqueAliadoPropio == (Object)null)
		{
			sfxAtaqueAliadoPropio = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxAtaqueAliadoPropio);
		}
		if ((Object)(object)sfxAtaqueEnemigo == (Object)null)
		{
			sfxAtaqueEnemigo = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxAtaqueEnemigo);
		}
#if UNITY_EDITOR
		if ((Object)(object)sfxAtaqueJefe == (Object)null)
		{
			sfxAtaqueJefe = MusicManager.LoadFirstAudioClipInFolderEditor(MusicManager.FolderSfxAtaqueJefe);
		}
#endif
		if ((Object)(object)sfxBotonEnCombate == (Object)null)
		{
			sfxBotonEnCombate = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxBotonEnCombate);
		}
		if ((Object)(object)sfxBotonConfirmacionDialogo == (Object)null)
		{
			sfxBotonConfirmacionDialogo = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxBotonConfirmacionDialogo);
		}
		if ((Object)(object)sfxCurarse == (Object)null)
		{
			sfxCurarse = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxCurarse);
		}
		if ((Object)(object)sfxEfecto == (Object)null)
		{
			sfxEfecto = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxEfecto);
		}
		if ((Object)(object)sfxEsquive == (Object)null)
		{
			sfxEsquive = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxEsquive);
		}
		if ((Object)(object)sfxFuego == (Object)null)
		{
			sfxFuego = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxFuego);
		}
		if ((Object)(object)victoryMenuMusic == (Object)null)
		{
			victoryMenuMusic = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathMusicVictoryMenu);
		}
		if ((Object)(object)gameOverMusic == (Object)null)
		{
			gameOverMusic = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathMusicGameOver);
		}
		if ((Object)(object)starterRewardWeapon == (Object)null)
		{
			starterRewardWeapon = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Arma_CadenaBicicleta.asset");
		}
		if ((Object)(object)sfxDanoFinalMuerte == (Object)null)
		{
			sfxDanoFinalMuerte = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxDanoFinalMuerte);
		}
		if ((Object)(object)sfxEncuentro == (Object)null)
		{
			sfxEncuentro = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxEncuentro);
		}
		if ((Object)(object)sfxSubidaDeNivel == (Object)null)
		{
			sfxSubidaDeNivel = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxSubidaDeNivel);
		}
		if ((Object)(object)sfxMuerteEnemigo == (Object)null)
		{
			sfxMuerteEnemigo = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxMuerteEnemigo);
		}
		if ((Object)(object)sfxDisparoRevolver38 == (Object)null)
		{
			sfxDisparoRevolver38 = RuntimeAssetCatalog.GetAudioClip("Assets/Sound/EfectodeSonido_DisparoRevolver38/Disparo_Revolver38.wav");
		}
		if ((Object)(object)sfxRecargaRevolver == (Object)null)
		{
			sfxRecargaRevolver = RuntimeAssetCatalog.GetAudioClip("Assets/Sound/EfectodeSonido_RecargaRevolver/Recarga_Revolver.wav");
		}
		LoadPortraitsIfMissing();
	}

	private void EnsureDetectiveCombatPortraitResolved()
	{
		if ((Object)(object)detectiveCombatPortrait == (Object)null || detectiveCombatPortrait.name.Contains("Cardo") || detectiveCombatPortrait.name == "1")
		{
			Sprite customDet = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Detective.png");
			if ((Object)(object)customDet != (Object)null)
			{
				detectiveCombatPortrait = customDet;
			}
			else if ((Object)(object)detectiveCombatPortrait == (Object)null)
			{
				detectiveCombatPortrait = RuntimeAssetCatalog.GetSprite("Assets/Sprites/Detective/detective_stand_down.png");
			}
		}
	}

	public void LoadPortraitsIfMissing()
	{
		if ((Object)(object)enriquePortrait == (Object)null && PlayerInventoryAndWeaponsUI.Instance != null)
		{
			enriquePortrait = PlayerInventoryAndWeaponsUI.Instance.enriquePortrait;
		}
		if ((Object)(object)enriquePortrait == (Object)null)
		{
			enriquePortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Enrique.png");
			if ((Object)(object)enriquePortrait == (Object)null)
			{
				enriquePortrait = RuntimeAssetCatalog.GetSprite("Assets/Sprites/Personaje/stand_down.png");
			}
		}
		EnsureDetectiveCombatPortraitResolved();
		if ((Object)(object)corporeoPortrait == (Object)null)
		{
			corporeoPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Musculatura.png");
		}
		if ((Object)(object)intelectoPortrait == (Object)null)
		{
			intelectoPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Intelecto.png");
		}
		if ((Object)(object)motricidadPortrait == (Object)null)
		{
			motricidadPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Motricidad.png");
		}
		if ((Object)(object)mundoInternoPortrait == (Object)null)
		{
			mundoInternoPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/MundoInterno.png");
		}
	}

	private void Start()
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected Obj, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected Obj, but got Unknown
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected Obj, but got Unknown
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Expected Obj, but got Unknown
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected Obj, but got Unknown
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Expected Obj, but got Unknown
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Expected Obj, but got Unknown
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected Obj, but got Unknown
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Expected Obj, but got Unknown
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Expected Obj, but got Unknown
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Expected Obj, but got Unknown
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Expected Obj, but got Unknown
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Expected Obj, but got Unknown
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Expected Obj, but got Unknown
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Expected Obj, but got Unknown
		LoadSfxClipsIfMissing();
		PlayerInventoryAndWeaponsUI.EnsureInScene();
		PlayerLevelUpAndStatsUI.EnsureInScene();
		EnforceUnifiedCombatUILayout();
		SetupCombatPortraitAndWeaponUI();
		EnsureCombatGreenHealthBar();
		if ((Object)(object)corporeoButton != (Object)null)
		{
			((UnityEvent)corporeoButton.onClick).RemoveListener((UnityAction)OnCorporeoButton);
			((UnityEvent)corporeoButton.onClick).AddListener((UnityAction)OnCorporeoButton);
		}
		if ((Object)(object)intelectoButton != (Object)null)
		{
			((UnityEvent)intelectoButton.onClick).RemoveListener((UnityAction)OnIntelectoButton);
			((UnityEvent)intelectoButton.onClick).AddListener((UnityAction)OnIntelectoButton);
		}
		if ((Object)(object)motricidadButton != (Object)null)
		{
			((UnityEvent)motricidadButton.onClick).RemoveListener((UnityAction)OnMotricidadButton);
			((UnityEvent)motricidadButton.onClick).AddListener((UnityAction)OnMotricidadButton);
		}
		if ((Object)(object)mundoInternoButton != (Object)null)
		{
			((UnityEvent)mundoInternoButton.onClick).RemoveListener((UnityAction)OnMundoInternoButton);
			((UnityEvent)mundoInternoButton.onClick).AddListener((UnityAction)OnMundoInternoButton);
		}
		if ((Object)(object)combatNextDialogueButton != (Object)null)
		{
			((UnityEvent)combatNextDialogueButton.onClick).RemoveListener((UnityAction)AdvanceCombatDialogue);
			((UnityEvent)combatNextDialogueButton.onClick).AddListener((UnityAction)AdvanceCombatDialogue);
		}
		if ((Object)(object)retryBattleButton != (Object)null)
		{
			((UnityEvent)retryBattleButton.onClick).RemoveListener((UnityAction)RetryFromGameOver);
			((UnityEvent)retryBattleButton.onClick).AddListener((UnityAction)RetryFromGameOver);
		}
		if ((Object)(object)returnToMenuButton != (Object)null)
		{
			((UnityEvent)returnToMenuButton.onClick).RemoveListener((UnityAction)ReturnToMainMenu);
			((UnityEvent)returnToMenuButton.onClick).AddListener((UnityAction)ReturnToMainMenu);
		}
		if (enemySlots != null)
		{
			for (int i = 0; i < enemySlots.Length; i++)
			{
				int capturedIndex = i;
				if (enemySlots[i] != null && (Object)(object)enemySlots[i].selectTargetButton != (Object)null)
				{
					((UnityEventBase)enemySlots[i].selectTargetButton.onClick).RemoveAllListeners();
					((UnityEvent)enemySlots[i].selectTargetButton.onClick).AddListener((UnityAction)(() =>
					{
						SelectTargetEnemy(capturedIndex);
					}));
				}
			}
		}
		if (state == BattleState.INACTIVE)
		{
			if ((Object)(object)battlePanel != (Object)null)
			{
				battlePanel.SetActive(false);
			}
			if ((Object)(object)actionGifPanel != (Object)null)
			{
				actionGifPanel.SetActive(false);
			}
			if ((Object)(object)combatDialoguePanel != (Object)null)
			{
				combatDialoguePanel.SetActive(false);
			}
			if ((Object)(object)intelectoCraftingPanel != (Object)null)
			{
				intelectoCraftingPanel.SetActive(false);
			}
			if ((Object)(object)gameOverPanel != (Object)null)
			{
				gameOverPanel.SetActive(false);
			}
		}
	}

	private void Update()
	{
		if (state != BattleState.INACTIVE && (Object)(object)battlePanel != (Object)null && battlePanel.activeSelf)
		{
			UpdateEnemySlotAnimations(Time.deltaTime);
			UpdateDynamicTargetIndicators(Time.deltaTime);
		}
		if ((state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN) && WasToggleInventoryKeyPressed())
		{
			PlayerInventoryAndWeaponsUI playerInventoryAndWeaponsUI = PlayerInventoryAndWeaponsUI.EnsureInScene();
			if ((Object)(object)playerInventoryAndWeaponsUI != (Object)null && !playerInventoryAndWeaponsUI.IsOpen)
			{
				playerInventoryAndWeaponsUI.OpenInventoryMenu();
			}
		}
		if (state == BattleState.DIALOGUE)
		{
			if (Time.frameCount != dialogueStateChangeFrame && WasDialogueAdvancePressed())
			{
				AdvanceCombatDialogue();
			}
		}
		else if (state == BattleState.LOST && (Object)(object)gameOverPanel != (Object)null && gameOverPanel.activeSelf && Keyboard.current != null)
		{
			if (((ButtonControl)Keyboard.current.rKey).wasPressedThisFrame || ((ButtonControl)Keyboard.current.spaceKey).wasPressedThisFrame || ((ButtonControl)Keyboard.current.enterKey).wasPressedThisFrame)
			{
				RetryFromGameOver();
			}
			else if (((ButtonControl)Keyboard.current.mKey).wasPressedThisFrame || ((ButtonControl)Keyboard.current.escapeKey).wasPressedThisFrame)
			{
				ReturnToMainMenu();
			}
		}
	}

	public void StartBattle(EnemyData enemy, string[] combatIntroDialogue = null, GameObject mapEnemyObject = null, bool isBoss = false, string bossNextScene = null, string[] bossVictoryNarrative = null, ItemData rewardItem = null, string[] rewardDialogue = null, List<InventorySlot> rewardItemsBundle = null)
	{
		EnemyData[] enemies = ((!((Object)(object)enemy != (Object)null)) ? Array.Empty<EnemyData>() : new EnemyData[1] { enemy });
		GameObject[] mapEnemyObjects = ((!((Object)(object)mapEnemyObject != (Object)null)) ? null : new GameObject[1] { mapEnemyObject });
		StartBattle(enemies, combatIntroDialogue, mapEnemyObjects, isBoss, bossNextScene, bossVictoryNarrative, rewardItem, rewardDialogue, rewardItemsBundle);
	}

	public void StartBattle(EnemyData[] enemies, string[] combatIntroDialogue = null, GameObject[] mapEnemyObjects = null, bool isBoss = false, string bossNextScene = null, string[] bossVictoryNarrative = null, ItemData rewardItem = null, string[] rewardDialogue = null, List<InventorySlot> rewardItemsBundle = null)
	{
		if (enemies == null || enemies.Length == 0)
		{
			Debug.LogError((object)"[BattleManager] No se puede iniciar combate: la lista de enemigos está vacía.");
			return;
		}
		isCurrentEncounterBoss = isBoss;
		currentBossNextScene = bossNextScene;
		currentBossVictoryNarrative = bossVictoryNarrative;
		currentRewardItem = rewardItem;
		currentRewardBundle = rewardItemsBundle;
		currentRewardDialogueLines = rewardDialogue;
		if ((Object)(object)playerStats == (Object)null)
		{
			playerStats = Object.FindAnyObjectByType<PlayerStats>();
		}
		activeEnemies.Clear();
		for (int i = 0; i < enemies.Length; i++)
		{
			if ((Object)(object)enemies[i] != (Object)null)
			{
				activeEnemies.Add(new CombatEnemyInstance
				{
					data = enemies[i],
					currentHP = enemies[i].maxHP,
					isConfused = false,
					attackDebuff = 0,
					hasTriggeredHalfHealthDialogue = false
				});
			}
		}
		if (activeEnemies.Count != 0)
		{
			currentEnemy = activeEnemies[0].data;
			currentEnemyHP = activeEnemies[0].currentHP;
			selectedTargetIndex = 0;
			currentEncounterObjectsToDisable = mapEnemyObjects;
			isMotricidadDefending = false;
			playerAttackBuff = 0;
			state = BattleState.START;
			if ((Object)(object)playerController == (Object)null)
			{
				playerController = Object.FindAnyObjectByType<PlayerController2D>();
			}
			if ((Object)(object)playerController != (Object)null)
			{
				playerController.SetCanMove(value: false);
			}
			if ((Object)(object)playerMovementScript != (Object)null)
			{
				((Behaviour)playerMovementScript).enabled = false;
			}
			if ((Object)(object)battlePanel != (Object)null)
			{
				battlePanel.SetActive(true);
			}
			if ((Object)(object)actionGifPanel != (Object)null)
			{
				actionGifPanel.SetActive(false);
			}
			if ((Object)(object)combatDialoguePanel != (Object)null)
			{
				combatDialoguePanel.SetActive(false);
			}
			HideIntelectoPanelSilent();
			HideMundoInternoPanelSilent();
			ValidateAndSetupDetectivePartyState();
			InitializeLevel2BossIfPresent(mapEnemyObjects);
			PlayEncuentroSfx();
			if ((Object)(object)MusicManager.Instance != (Object)null)
			{
				MusicManager.Instance.PlayBattleMusic();
			}
			BuildNewRoundTurnOrder();
			EnforceUnifiedCombatUILayout();
			SetupCombatPortraitAndWeaponUI();
			SetupEnemyVisuals();
			EnsureCombatGreenHealthBar();
			if ((Object)(object)playerHealthBarUI != (Object)null && (Object)(object)playerStats != (Object)null)
			{
				playerHealthBarUI.SetHealthInstant(playerStats.currentHP, playerStats.maxHP, isMotricidadDefending, playerAttackBuff);
			}
			UpdateHealthUI();
			string turnOrderSummaryText = GetTurnOrderSummaryText();
			SetBattleLog("Orden por Motricidad: " + turnOrderSummaryText);
			if (combatIntroDialogue != null && combatIntroDialogue.Length != 0)
			{
				StartCombatDialogue(combatIntroDialogue, ExecuteCurrentTurnInQueue);
			}
			else
			{
				ExecuteCurrentTurnInQueue();
			}
		}
	}

	private void ValidateAndSetupDetectivePartyState()
	{
		Scene activeScene = SceneManager.GetActiveScene();
		bool isLevel2OrLater = activeScene.IsValid() && (activeScene.name.Contains("Nivel2") || activeScene.name.Contains("Nivel_2") || activeScene.name.Contains("Guarida") || activeScene.name.Contains("Nivel3") || activeScene.name.Contains("Nivel_3"));
		CompanionFollowAI companion = Object.FindAnyObjectByType<CompanionFollowAI>();
		if (isLevel2OrLater || ((Object)(object)companion != (Object)null && companion.isFollowingPlayer))
		{
			hasDetectiveInParty = true;
			if ((Object)(object)companion != (Object)null && !companion.isFollowingPlayer)
			{
				companion.ActivateCompanionFollow();
			}
		}
		else if (DetectiveStats.IsDetectiveInPartyGlobal)
		{
			hasDetectiveInParty = true;
		}
		if (activeScene.IsValid() && activeScene.name.Contains("01_Nivel1"))
		{
			hasDetectiveInParty = false;
		}
		if ((Object)(object)detectiveActionMenu == (Object)null)
		{
			detectiveActionMenu = DetectiveActionMenu.EnsureInBattlePanel(this);
		}
		if (hasDetectiveInParty)
		{
			if ((Object)(object)detectiveStats == (Object)null)
			{
				detectiveStats = DetectiveStats.EnsureInScene();
			}
			if ((Object)(object)detectiveStats != (Object)null)
			{
				detectiveStats.hasDetectiveInParty = true;
				detectiveStats.SyncPartyStateWithCompanion();
				if (detectiveStats.HP_Actual <= 0)
				{
					detectiveStats.HP_Actual = 1;
				}
				detectiveStats.ResetCombatTemporaryStates();
			}
			EnsureDetectiveCombatPortraitResolved();
			if ((Object)(object)detectiveActionMenu != (Object)null)
			{
				detectiveActionMenu.battleManager = this;
				detectiveActionMenu.detectiveStats = detectiveStats;
				detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: true);
				if ((Object)(object)detectiveActionMenu.menuRootPanel != (Object)null)
				{
					detectiveActionMenu.menuRootPanel.SetActive(false);
				}
				detectiveActionMenu.RefreshAllySlotUI(showSlot: true);
			}
		}
		else if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			detectiveActionMenu.RefreshAllySlotUI(showSlot: false);
			detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: true);
		}
	}

	private void InitializeLevel2BossIfPresent(GameObject[] mapEnemyObjects)
	{
		if ((Object)(object)activeLevel2BossController == (Object)null && mapEnemyObjects != null)
		{
			for (int i = 0; i < mapEnemyObjects.Length; i++)
			{
				if ((Object)(object)mapEnemyObjects[i] != (Object)null)
				{
					activeLevel2BossController = mapEnemyObjects[i].GetComponent<Level2BossController>();
					if ((Object)(object)activeLevel2BossController != (Object)null)
					{
						break;
					}
				}
			}
		}
		bool hasBossLevel2Archetype = false;
		bool hasCalacasArchetype = false;
		for (int j = 0; j < activeEnemies.Count; j++)
		{
			if (activeEnemies[j] != null && (Object)(object)activeEnemies[j].data != (Object)null)
			{
				if (activeEnemies[j].data.enemyType == EnemyType.JefeNivel2Capataz)
				{
					hasBossLevel2Archetype = true;
					break;
				}
				if (activeEnemies[j].data.enemyType == EnemyType.JefeElCalacas)
				{
					hasCalacasArchetype = true;
					break;
				}
			}
		}
		if (hasCalacasArchetype && (Object)(object)activeLevel2BossController == (Object)null)
		{
			activeLevel2BossController = ((Component)this).GetComponent<CalacasBossController>();
			if ((Object)(object)activeLevel2BossController == (Object)null)
			{
				activeLevel2BossController = ((Component)this).gameObject.AddComponent<CalacasBossController>();
			}
		}
		else if (hasBossLevel2Archetype && (Object)(object)activeLevel2BossController == (Object)null)
		{
			activeLevel2BossController = ((Component)this).GetComponent<Level2BossController>();
			if ((Object)(object)activeLevel2BossController == (Object)null)
			{
				activeLevel2BossController = ((Component)this).gameObject.AddComponent<Level2BossController>();
			}
		}
		if ((Object)(object)activeLevel2BossController != (Object)null)
		{
			activeLevel2BossController.InitializeBossCombat();
		}
	}

	public void SelectTargetEnemy(int index)
	{
		if (index >= 0 && index < activeEnemies.Count && activeEnemies[index].IsAlive)
		{
			PlayBotonEnCombateSfx();
			selectedTargetIndex = index;
			currentEnemy = activeEnemies[selectedTargetIndex].data;
			currentEnemyHP = activeEnemies[selectedTargetIndex].currentHP;
			UpdateHealthUI();
			if (state == BattleState.PLAYERTURN)
			{
				SetBattleLog("Objetivo seleccionado: " + activeEnemies[selectedTargetIndex].data.enemyName + ". Elige que voz actuara.");
			}
			else if (state == BattleState.DETECTIVETURN)
			{
				SetBattleLog("Objetivo del Detective: " + activeEnemies[selectedTargetIndex].data.enemyName + ". Elige una accion tactica.");
			}
		}
	}

	private void EnsureValidLivingTarget()
	{
		if (selectedTargetIndex >= 0 && selectedTargetIndex < activeEnemies.Count && activeEnemies[selectedTargetIndex].IsAlive)
		{
			return;
		}
		for (int i = 0; i < activeEnemies.Count; i++)
		{
			if (activeEnemies[i].IsAlive)
			{
				selectedTargetIndex = i;
				currentEnemy = activeEnemies[i].data;
				currentEnemyHP = activeEnemies[i].currentHP;
				break;
			}
		}
	}

	private void BuildNewRoundTurnOrder()
	{
		roundTurnOrder.Clear();
		currentTurnCursor = 0;
		int motricidad = (((Object)(object)playerStats != (Object)null) ? playerStats.GetEffectiveMotricidad() : 12);

		// 1. Enrique (protagonista) actúa siempre primero
		roundTurnOrder.Add(new TurnActor
		{
			isPlayer = true,
			isDetective = false,
			enemyIndex = -1,
			motricidad = motricidad
		});

		// 2. Detective Vargas (aliado) actúa siempre segundo si está en el equipo y con vida
		if (hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
		{
			roundTurnOrder.Add(new TurnActor
			{
				isPlayer = false,
				isDetective = true,
				enemyIndex = -2,
				motricidad = detectiveStats.Velocidad
			});
		}

		// 3. Los enemigos actúan después de la patrulla aliada (ordenados entre ellos por su velocidad/motricidad)
		List<TurnActor> enemyTurns = new List<TurnActor>();
		for (int i = 0; i < activeEnemies.Count; i++)
		{
			if (activeEnemies[i].IsAlive)
			{
				int effectiveEnemyMot = Mathf.Max(1, activeEnemies[i].data.motricidad - activeEnemies[i].motricidadDebuff);
				enemyTurns.Add(new TurnActor
				{
					isPlayer = false,
					isDetective = false,
					enemyIndex = i,
					motricidad = effectiveEnemyMot
				});
			}
		}
		enemyTurns.Sort((TurnActor a, TurnActor b) =>
		{
			int num = b.motricidad.CompareTo(a.motricidad);
			if (num != 0)
			{
				return num;
			}
			return a.enemyIndex.CompareTo(b.enemyIndex);
		});
		roundTurnOrder.AddRange(enemyTurns);
	}

	private string GetTurnOrderSummaryText()
	{
		List<string> list = new List<string>();
		foreach (TurnActor item in roundTurnOrder)
		{
			if (item.isPlayer)
			{
				list.Add($"Enrique({item.motricidad})");
			}
			else if (item.isDetective && hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
			{
				list.Add($"Detective({item.motricidad})");
			}
			else if (item.enemyIndex >= 0 && item.enemyIndex < activeEnemies.Count && activeEnemies[item.enemyIndex].IsAlive)
			{
				list.Add($"{activeEnemies[item.enemyIndex].data.enemyName}({item.motricidad})");
			}
		}
		return string.Join(" > ", list);
	}

	private void ExecuteCurrentTurnInQueue()
	{
		lastEnemyTargetWasDetective = false;
		if (AreAllEnemiesDead())
		{
			EndBattle(won: true);
			return;
		}
		if ((Object)(object)playerStats != (Object)null && !playerStats.IsAlive)
		{
			EndBattle(won: false);
			return;
		}
		while (currentTurnCursor < roundTurnOrder.Count)
		{
			TurnActor turnActor = roundTurnOrder[currentTurnCursor];
			if (turnActor.isPlayer)
			{
				break;
			}
			if (turnActor.isDetective && hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
			{
				break;
			}
			if (!turnActor.isPlayer && !turnActor.isDetective && turnActor.enemyIndex >= 0 && turnActor.enemyIndex < activeEnemies.Count && activeEnemies[turnActor.enemyIndex].IsAlive)
			{
				break;
			}
			currentTurnCursor++;
		}
		if (currentTurnCursor >= roundTurnOrder.Count)
		{
			BuildNewRoundTurnOrder();
		}
		TurnActor turnActor2 = roundTurnOrder[currentTurnCursor];
		if (turnActor2.isPlayer)
		{
			isMotricidadDefending = false;
			EnsureValidLivingTarget();
			state = BattleState.PLAYERTURN;
			if ((Object)(object)detectiveActionMenu != (Object)null)
			{
				detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: true);
				detectiveActionMenu.RefreshAllySlotUI(showSlot: hasDetectiveInParty);
			}
			SetActionButtonsInteractable(interactable: true);
			UpdateHealthUI();
			string enemyName = activeEnemies[selectedTargetIndex].data.enemyName;
			SetBattleLog("TU TURNO — ENRIQUE (Objetivo: " + enemyName + ") | Cola: " + GetTurnOrderSummaryText());
		}
		else if (turnActor2.isDetective)
		{
			EnsureValidLivingTarget();
			state = BattleState.DETECTIVETURN;
			if ((Object)(object)detectiveStats != (Object)null)
			{
				detectiveStats.OnStartDetectiveTurn();
			}
			HideIntelectoPanelSilent();
			HideMundoInternoPanelSilent();
			SetProtagonistInventoryButtonVisible(visible: true);
			if ((Object)(object)detectiveActionMenu != (Object)null)
			{
				detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: true);
				if ((Object)(object)detectiveActionMenu.menuRootPanel != (Object)null)
				{
					detectiveActionMenu.menuRootPanel.SetActive(false);
				}
				detectiveActionMenu.RefreshAllySlotUI(showSlot: hasDetectiveInParty);
			}
			SetActionButtonsInteractable(interactable: true);
			UpdateHealthUI();
			string enemyName = activeEnemies[selectedTargetIndex].data.enemyName;
			SetBattleLog("TU TURNO — DETECTIVE VARGAS (Objetivo: " + enemyName + ") | Cola: " + GetTurnOrderSummaryText());
		}
		else
		{
			state = BattleState.ENEMYTURN;
			if ((Object)(object)detectiveActionMenu != (Object)null)
			{
				detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: true);
			}
			SetActionButtonsInteractable(interactable: false);
			((MonoBehaviour)this).StartCoroutine(EnemyTurnCoroutine(turnActor2.enemyIndex));
		}
	}

	private void AdvanceTurnQueueAndContinue()
	{
		currentTurnCursor++;
		ExecuteCurrentTurnInQueue();
	}

	private IEnumerator DetectiveCompanionTurnCoroutine()
	{
		if ((Object)(object)detectiveStats == (Object)null || !detectiveStats.IsAlive)
		{
			AdvanceTurnQueueAndContinue();
			yield break;
		}

		EnsureValidLivingTarget();
		if (activeEnemies == null || activeEnemies.Count == 0 || selectedTargetIndex < 0 || selectedTargetIndex >= activeEnemies.Count)
		{
			AdvanceTurnQueueAndContinue();
			yield break;
		}

		CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
		string enemyName = target.data.enemyName;

		SetBattleLog($"TURNO DEL DETECTIVE VARGAS — Apunta con su revólver .38 Especial hacia {enemyName}...");
		yield return new WaitForSeconds(0.65f);

		if (state != BattleState.DETECTIVETURN)
		{
			yield break;
		}

		EnsureValidLivingTarget();
		if (activeEnemies == null || activeEnemies.Count == 0 || selectedTargetIndex < 0 || selectedTargetIndex >= activeEnemies.Count)
		{
			AdvanceTurnQueueAndContinue();
			yield break;
		}
		target = activeEnemies[selectedTargetIndex];

		// Inteligencia de combate del detective:
		// Dispara su revólver .38 (perfora blindaje de jefe y causa daño balístico crítico).
		// Si está sin munición, aplica culatazo táctico cuerpo a cuerpo.
		if (detectiveStats.CanFireWeapon(out _))
		{
			ExecuteDetectiveFirearmAttack();
		}
		else
		{
			ExecuteDetectiveMeleeAttack();
		}
	}

	public void ForceHideProtagonistExclusiveSubmenusForDetective()
	{
		HideIntelectoPanelSilent();
		HideMundoInternoPanelSilent();
		SetProtagonistInventoryButtonVisible(visible: false);
		PlayerInventoryAndWeaponsUI invUI = PlayerInventoryAndWeaponsUI.Instance;
		if ((Object)(object)invUI != (Object)null && invUI.IsOpen)
		{
			invUI.CloseInventoryMenu();
		}
	}

	public void SetProtagonistInventoryButtonVisible(bool visible)
	{
		if ((Object)(object)combatInventoryButtonRoot != (Object)null)
		{
			combatInventoryButtonRoot.SetActive(false);
		}
	}

	private int ApplyDamageToEnemyWithBossMechanics(CombatEnemyInstance target, int rawDamageAfterDefense, bool isDirectMelee, bool canBreakBossShield, string attackerName, bool attackerIsDetective, out string bossFeedbackLog)
	{
		bossFeedbackLog = string.Empty;
		if (target == null)
		{
			return 0;
		}
		int adjustedDamage = Mathf.Max(1, rawDamageAfterDefense);
		if (target.vulnerabilityTurns > 0)
		{
			adjustedDamage = Mathf.Max(1, Mathf.RoundToInt((float)adjustedDamage * 1.3f));
		}
		if ((Object)(object)activeLevel2BossController != (Object)null && IsBossEnemy(target))
		{
			adjustedDamage = activeLevel2BossController.ProcessIncomingDamage(adjustedDamage, isDirectMelee, canBreakBossShield, attackerName, out var triggeredCounterAttack, out var counterAttackDamage, out bossFeedbackLog);
			if (triggeredCounterAttack && counterAttackDamage > 0)
			{
				if (attackerIsDetective && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
				{
					detectiveStats.TakeDamage(counterAttackDamage, out var _);
				}
				else if ((Object)(object)playerStats != (Object)null)
				{
					playerStats.TakeDamage(counterAttackDamage);
				}
			}
		}
		target.currentHP = Mathf.Max(0, target.currentHP - adjustedDamage);
		currentEnemy = target.data;
		currentEnemyHP = target.currentHP;
		if (target.currentHP <= 0 && (Object)(object)activeLevel2BossController != (Object)null && IsBossEnemy(target))
		{
			activeLevel2BossController.NotifyBossDefeated(this);
		}
		return adjustedDamage;
	}

	public void ExecuteDetectiveMeleeAttack()
	{
		if (state != BattleState.DETECTIVETURN || (Object)(object)detectiveStats == (Object)null || !detectiveStats.IsAlive)
		{
			return;
		}
		PlayBotonEnCombateSfx();
		if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: false);
		}
		EnsureValidLivingTarget();
		CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
		ActionGifData meleeGif = new ActionGifData
		{
			actionLabel = $"[GIF: DETECTIVE VARGAS - Golpe Cuerpo a Cuerpo (Culatazo) contra {target.data.enemyName}]",
			placeholderColor = new Color(0.16f, 0.28f, 0.42f, 1f),
			gifFrames = ((corporeoGif != null) ? corporeoGif.gifFrames : null),
			duration = 1.25f
		};
		PlayAtaqueAliadoPropioSfx();
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(meleeGif, () =>
		{
			int effectiveDef = Mathf.Max(0, target.data.defense - target.defenseDebuff);
			int rawDmg = Mathf.Max(1, detectiveStats.GetTotalMeleeAttack() - effectiveDef);
			int finalDmg = ApplyDamageToEnemyWithBossMechanics(target, rawDmg, isDirectMelee: true, canBreakBossShield: false, detectiveStats.detectiveName, attackerIsDetective: true, out var bossLog);
			string extraLog = ((!string.IsNullOrEmpty(bossLog)) ? (" | " + bossLog) : "");
			SetBattleLog($"DETECTIVE [CUERPO A CUERPO]: Vargas asesta un culatazo tactico a {target.data.enemyName} e inflige {finalDmg} de dano fisico (sin costo){extraLog}.");
			EnsureValidLivingTarget();
			UpdateHealthUI();
			CheckPostPlayerActionFlow(target);
		}));
	}

	public void ExecuteDetectiveFirearmAttack()
	{
		if (state != BattleState.DETECTIVETURN || (Object)(object)detectiveStats == (Object)null || !detectiveStats.IsAlive)
		{
			return;
		}
		if (!detectiveStats.CanFireWeapon(out var reason))
		{
			PlayBotonEnCombateSfx();
			SetBattleLog("DETECTIVE [ARMA DE FUEGO]: " + reason);
			if ((Object)(object)detectiveActionMenu != (Object)null)
			{
				detectiveActionMenu.RefreshButtonLabels();
			}
			return;
		}
		PlayBotonEnCombateSfx();
		detectiveStats.ConsumeWeaponShot();
		if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: false);
		}
		EnsureValidLivingTarget();
		CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
		ActionGifData gunGif = new ActionGifData
		{
			actionLabel = $"[GIF: DETECTIVE VARGAS - Disparo de Revolver .38 Especial contra {target.data.enemyName}]",
			placeholderColor = new Color(0.45f, 0.22f, 0.1f, 1f),
			gifFrames = ((explosionGif != null && explosionGif.gifFrames != null && explosionGif.gifFrames.Length != 0) ? explosionGif.gifFrames : ((corporeoGif != null) ? corporeoGif.gifFrames : null)),
			duration = 1.25f
		};
		PlayDisparoRevolverSfx();
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(gunGif, () =>
		{
			bool isCrit = Random.value < 0.35f;
			int baseGunAtk = detectiveStats.GetTotalFirearmAttack();
			if (isCrit)
			{
				baseGunAtk = Mathf.RoundToInt((float)baseGunAtk * 1.5f);
			}
			int effectiveDef = Mathf.Max(0, (target.data.defense - target.defenseDebuff) / 2);
			int rawDmg = Mathf.Max(1, baseGunAtk - effectiveDef);
			int finalDmg = ApplyDamageToEnemyWithBossMechanics(target, rawDmg, isDirectMelee: false, canBreakBossShield: true, detectiveStats.detectiveName, attackerIsDetective: true, out var bossLog);
			string critTag = (isCrit ? " ¡DISPARO CRITICO (x1.5)!" : "");
			string extraLog = ((!string.IsNullOrEmpty(bossLog)) ? (" | " + bossLog) : "");
			SetBattleLog($"DETECTIVE [ARMA DE FUEGO .38]:{critTag} Inflige {finalDmg} de dano balistico a {target.data.enemyName} (Balas: {detectiveStats.municionActual}/{detectiveStats.municionMaxima}){extraLog}.");
			EnsureValidLivingTarget();
			UpdateHealthUI();
			CheckPostPlayerActionFlow(target);
		}));
	}

	public void ExecuteDetectiveSkill(DetectiveActionMenu.DetectiveSkillType skillType)
	{
		if (state != BattleState.DETECTIVETURN || (Object)(object)detectiveStats == (Object)null || !detectiveStats.IsAlive)
		{
			return;
		}
		if (skillType == DetectiveActionMenu.DetectiveSkillType.DisparoDeSupresion && !detectiveStats.CanFireWeapon(out var reason))
		{
			PlayBotonEnCombateSfx();
			SetBattleLog("DETECTIVE [DISPARO DE SUPRESIÓN]: " + reason);
			return;
		}
		PlayBotonEnCombateSfx();
		if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: false);
		}
		EnsureValidLivingTarget();
		CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
		PlayEfectoSfx();
		ActionGifData skillGif = new ActionGifData
		{
			actionLabel = $"[GIF: HABILIDAD DEL DETECTIVE - {skillType} en ejecucion]",
			placeholderColor = new Color(0.12f, 0.32f, 0.48f, 1f),
			gifFrames = ((intelectoGif != null) ? intelectoGif.gifFrames : null),
			duration = 1.25f
		};
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(skillGif, () =>
		{
			switch (skillType)
			{
			case DetectiveActionMenu.DetectiveSkillType.AnalizarPuntoDebil:
			{
				target.defenseDebuff += 4;
				target.vulnerabilityTurns = Mathf.Max(target.vulnerabilityTurns, 2);
				string shieldMsg = string.Empty;
				if ((Object)(object)activeLevel2BossController != (Object)null && IsBossEnemy(target))
				{
					activeLevel2BossController.TryBreakShieldWithSkill("Analizar Punto Débil", out shieldMsg);
				}
				string extra = ((!string.IsNullOrEmpty(shieldMsg)) ? (" " + shieldMsg) : "");
				SetBattleLog($"DETECTIVE [ANALIZAR PUNTO DÉBIL]: Expone la guardia de {target.data.enemyName} (-4 DEF y +30% dano recibido por 2 turnos).{extra}");
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
				break;
			}
			case DetectiveActionMenu.DetectiveSkillType.DisparoDeSupresion:
			{
				PlayDisparoRevolverSfx();
				detectiveStats.ConsumeWeaponShot();
				target.attackDebuff += 5;
				target.motricidadDebuff += 4;
				int chipDmg = ApplyDamageToEnemyWithBossMechanics(target, Mathf.Max(4, detectiveStats.Ataque_Arma / 2), isDirectMelee: false, canBreakBossShield: true, detectiveStats.detectiveName, attackerIsDetective: true, out var bossLog);
				string extra2 = ((!string.IsNullOrEmpty(bossLog)) ? (" | " + bossLog) : "");
				SetBattleLog($"DETECTIVE [DISPARO DE SUPRESIÓN]: Impacta por {chipDmg} dano y suprime a {target.data.enemyName} (-5 ATQ, -4 VEL){extra2}.");
				EnsureValidLivingTarget();
				UpdateHealthUI();
				CheckPostPlayerActionFlow(target);
				break;
			}
			case DetectiveActionMenu.DetectiveSkillType.ConcentracionTactica:
			{
				PlayRecargaRevolverSfx();
				int bulletsAdded = detectiveStats.ReloadAmmo(3);
				detectiveStats.tacticalAttackBonus = 5;
				detectiveStats.skillDodgeBonusPercent = 15f;
				detectiveStats.tacticalBuffTurnsRemaining = 2;
				SetBattleLog($"DETECTIVE [CONCENTRACIÓN TÁCTICA]: Recarga +{bulletsAdded} balas (Total {detectiveStats.municionActual}/{detectiveStats.municionMaxima}) y obtiene +5 ATQ y +15% Esquiva por 2 turnos.");
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
				break;
			}
			}
		}));
	}

	public void ExecuteDetectiveDodgeAction()
	{
		if (state != BattleState.DETECTIVETURN || (Object)(object)detectiveStats == (Object)null || !detectiveStats.IsAlive)
		{
			return;
		}
		PlayBotonEnCombateSfx();
		if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: false);
		}
		PlayEsquiveSfx();
		ActionGifData dodgeGif = new ActionGifData
		{
			actionLabel = "[GIF: DETECTIVE VARGAS - Posicion defensiva y evasion tactica]",
			placeholderColor = new Color(0.14f, 0.38f, 0.32f, 1f),
			gifFrames = ((motricidadGif != null) ? motricidadGif.gifFrames : null),
			duration = 1.15f
		};
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(dodgeGif, () =>
		{
			float totalDodge = detectiveStats.ActivateDodgeStance();
			SetBattleLog($"DETECTIVE [ESQUIVAR]: Vargas adopta postura evasiva. Su Probabilidad_Esquiva sube a {totalDodge:0}% hasta su proximo turno.");
			UpdateHealthUI();
			AdvanceTurnQueueAndContinue();
		}));
	}

	public void OnCorporeoButton()
	{
		if (state == BattleState.PLAYERTURN)
		{
			PlayBotonEnCombateSfx();
			HideIntelectoPanelSilent();
			HideMundoInternoPanelSilent();
			EnsureValidLivingTarget();
			CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
			ItemData equippedWeapon = (((Object)(object)InventoryManager.Instance != (Object)null) ? InventoryManager.Instance.equippedWeapon : null);
			string weaponTag = (((Object)(object)equippedWeapon != (Object)null) ? (" [" + equippedWeapon.itemName + "]") : "");
			corporeoGif.actionLabel = "[GIF: CORPOREO" + weaponTag + " - Golpe fisico contra " + target.data.enemyName + "]";
			PlayAtaqueAliadoPropioSfx();
			((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(corporeoGif, () =>
			{
				int num = (((Object)(object)playerStats != (Object)null) ? playerStats.GetTotalPhysicalAttack() : 12);
				int num2 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetEquippedWeaponAttackBonus() : 0);
				int num3 = playerAttackBuff;
				int num4 = num + num3;
				int effectiveDef = Mathf.Max(0, target.data.defense - target.defenseDebuff);
				int rawDmg = Mathf.Max(1, num4 - effectiveDef);
				int num5 = ApplyDamageToEnemyWithBossMechanics(target, rawDmg, isDirectMelee: true, canBreakBossShield: false, "Enrique", attackerIsDetective: false, out var bossLog);
				string text = ((num2 > 0 && (Object)(object)equippedWeapon != (Object)null) ? $" (+{num2} por {equippedWeapon.itemName})" : "");
				string text2 = ((num3 > 0) ? $" (+{num3} bonus por estar ANIMADO)" : "");
				string textBoss = ((!string.IsNullOrEmpty(bossLog)) ? (" | " + bossLog) : "");
				playerAttackBuff = 0;
				string text3 = (((Object)(object)equippedWeapon != (Object)null) ? ("¡Golpe contundente con " + equippedWeapon.itemName + "!") : "¡Golpe contundente a puño limpio!");
				SetBattleLog($"CORPOREO{weaponTag}: {text3} Infliges {num5} de dano fisico a {target.data.enemyName}{text}{text2}{textBoss}.");
				EnsureValidLivingTarget();
				UpdateHealthUI();
				if ((Object)(object)playerStats != (Object)null && !playerStats.IsAlive)
				{
					EndBattle(won: false);
					return;
				}
				CheckPostPlayerActionFlow(target);
			}));
		}
		else if (state == BattleState.DETECTIVETURN)
		{
			if ((Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
			{
				PlayBotonEnCombateSfx();
				SetActionButtonsInteractable(interactable: false);
				if (detectiveStats.CanFireWeapon(out _))
				{
					ExecuteDetectiveFirearmAttack();
				}
				else
				{
					ExecuteDetectiveMeleeAttack();
				}
			}
		}
	}

	public void OnIntelectoButton()
	{
		if (state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN)
		{
			PlayBotonEnCombateSfx();
			HideMundoInternoPanelSilent();
			EnsureValidLivingTarget();
			GameObject val = (((Object)(object)intelectoAndItemsPanel != (Object)null) ? intelectoAndItemsPanel : intelectoCraftingPanel);
			if ((Object)(object)intelectoCraftingUI == (Object)null && (Object)(object)val != (Object)null)
			{
				intelectoCraftingUI = val.GetComponent<CombatIntelectoUI>();
			}
			if ((Object)(object)intelectoCraftingUI != (Object)null)
			{
				intelectoCraftingUI.ShowAndRefresh(this);
				SetBattleLog("INTELECTO / INVENTARIO: Usa un objeto curativo o combina materiales con Intelecto (o Cancela para volver).");
			}
			else if ((Object)(object)val != (Object)null)
			{
				val.SetActive(true);
				SetBattleLog("INTELECTO / INVENTARIO: Usa un objeto curativo o combina materiales con Intelecto (o Cancela para volver).");
			}
		}
	}

	public void CloseIntelectoPanel()
	{
		PlayBotonEnCombateSfx();
		HideIntelectoPanelSilent();
		if ((state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN) && activeEnemies.Count > 0)
		{
			EnsureValidLivingTarget();
			string enemyName = activeEnemies[selectedTargetIndex].data.enemyName;
			string actorLabel = (state == BattleState.DETECTIVETURN) ? "DETECTIVE VARGAS" : "ENRIQUE";
			SetBattleLog($"TU TURNO — {actorLabel} (Objetivo: {enemyName}) | Cola: {GetTurnOrderSummaryText()}");
		}
	}

	private void HideIntelectoPanelSilent()
	{
		if ((Object)(object)intelectoCraftingUI != (Object)null)
		{
			intelectoCraftingUI.Hide();
		}
		if ((Object)(object)intelectoAndItemsPanel != (Object)null)
		{
			intelectoAndItemsPanel.SetActive(false);
		}
		if ((Object)(object)intelectoCraftingPanel != (Object)null)
		{
			intelectoCraftingPanel.SetActive(false);
		}
	}

	public void UseConsumableInCombat(ItemData item, bool targetDetective = false)
	{
		UseItemInCombat(item, targetDetective);
	}

	public void UseItemInCombat(ItemData item, bool targetDetective = false)
	{
		if ((state != BattleState.PLAYERTURN && state != BattleState.DETECTIVETURN) || (Object)(object)item == (Object)null || ((Object)(object)InventoryManager.Instance != (Object)null && !InventoryManager.Instance.HasItem(item)))
		{
			return;
		}
		PlayBotonEnCombateSfx();
		HideIntelectoPanelSilent();
		EnsureValidLivingTarget();
		CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
		if ((Object)(object)InventoryManager.Instance != (Object)null)
		{
			InventoryManager.Instance.RemoveItem(item);
		}
		bool isHealingItem = item.category == ItemCategory.ConsumibleCuracion || item.healAmount > 0;
		if (isHealingItem)
		{
			PlayCurarseSfx();
		}
		else if (item.confusesTarget || InventoryManager.GetCanonicalItemKey(item.itemName) == "OFENS_MOLOTOV")
		{
			PlayFuegoSfx();
		}
		else
		{
			PlayAtaqueAliadoPropioSfx();
		}
		ActionGifData gifData = new ActionGifData
		{
			actionLabel = isHealingItem 
				? (targetDetective 
					? $"[GIF: INTELECTO - Atendiendo medicamente al Detective Vargas con {item.itemName}]" 
					: $"[GIF: INTELECTO - Enrique usando objeto curativo: {item.itemName}]")
				: $"[GIF: Arrojando artefacto ofensivo: {item.itemName}]",
			placeholderColor = isHealingItem ? new Color(0.12f, 0.72f, 0.28f, 1f) : new Color(0.88f, 0.35f, 0.08f, 1f),
			gifFrames = (!isHealingItem && explosionGif != null && explosionGif.gifFrames != null && explosionGif.gifFrames.Length != 0) ? explosionGif.gifFrames : ((intelectoGif != null) ? intelectoGif.gifFrames : null),
			duration = (!isHealingItem && explosionGif != null) ? explosionGif.duration : 1.5f
		};
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(gifData, () =>
		{
			if (isHealingItem)
			{
				int num = (((Object)(object)playerStats != (Object)null) ? playerStats.GetIntelectoHealBonus() : 0);
				int num2 = item.healAmount + num;
				if (targetDetective && hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null)
				{
					bool wasUnconscious = (detectiveStats.HP_Actual <= 0);
					if (wasUnconscious)
					{
						detectiveStats.HP_Actual = Mathf.Clamp(num2, 1, detectiveStats.HP_Max);
						SetBattleLog($"¡REANIMACIÓN CON INTELECTO! Atienden las heridas de Vargas con {item.itemName}. ¡El Detective recupera el conocimiento con {detectiveStats.HP_Actual} HP!");
					}
					else
					{
						int prev = detectiveStats.HP_Actual;
						detectiveStats.Heal(num2);
						int healed = detectiveStats.HP_Actual - prev;
						SetBattleLog($"INTELECTO: Asistencia al Detective Vargas con {item.itemName} (+{healed} HP) [Total: {detectiveStats.HP_Actual}/{detectiveStats.HP_Max} HP].");
					}
				}
				else
				{
					if ((Object)(object)playerStats != (Object)null)
					{
						playerStats.Heal(num2);
					}
					string arg = ((num > 0) ? $" (+{num} por Intelecto)" : "");
					string arg2 = ((!string.IsNullOrWhiteSpace(item.combatUseMessage)) ? item.combatUseMessage : $"Enrique usa {item.itemName} y recupera +{num2} HP!");
					SetBattleLog($"{arg2} (+{num2} HP{arg})");
				}
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
			}
			else
			{
				int num3 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetIntelectoDamageBonus() : 0);
				int rawDmg = Mathf.Max(1, item.damageAmount + num3);
				int num4 = ApplyDamageToEnemyWithBossMechanics(target, rawDmg, isDirectMelee: false, canBreakBossShield: true, "Enrique (Intelecto)", attackerIsDetective: false, out var bossLog);
				if ((item.confusesTarget || InventoryManager.GetCanonicalItemKey(item.itemName) == "OFENS_MOLOTOV") && target.IsAlive)
				{
					target.isConfused = true;
				}
				string confSuffix = ((item.confusesTarget || InventoryManager.GetCanonicalItemKey(item.itemName) == "OFENS_MOLOTOV") && target.IsAlive) ? $" | {target.data.enemyName} queda CONFUNDIDO (1 turno)" : "";
				string extraBoss = ((!string.IsNullOrEmpty(bossLog)) ? (" | " + bossLog) : "");
				string battleLog = ((!string.IsNullOrWhiteSpace(item.combatUseMessage)) ? item.combatUseMessage : $"Enrique lanza {item.itemName} contra {target.data.enemyName} ({num4} dano).");
				SetBattleLog(battleLog + confSuffix + extraBoss);
				EnsureValidLivingTarget();
				UpdateHealthUI();
				CheckPostPlayerActionFlow(target);
			}
		}));
	}

	public void CraftAndUseRecipeInCombat(CraftingRecipeData recipe, bool targetDetective = false)
	{
		if ((state != BattleState.PLAYERTURN && state != BattleState.DETECTIVETURN) || (Object)(object)recipe == (Object)null || ((Object)(object)InventoryManager.Instance != (Object)null && !InventoryManager.Instance.CanCraft(recipe)))
		{
			return;
		}
		PlayBotonEnCombateSfx();
		HideIntelectoPanelSilent();
		EnsureValidLivingTarget();
		CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
		if ((Object)(object)InventoryManager.Instance != (Object)null)
		{
			InventoryManager.Instance.ConsumeRecipeMaterials(recipe);
		}
		string text = ((!string.IsNullOrWhiteSpace(recipe.intelectoComment)) ? recipe.intelectoComment : ("VOZ [INTELECTO]: \"Fabricando " + recipe.recipeName + "!\""));
		Debug.Log((object)("[BattleManager] " + text));
		SetBattleLog(text);
		bool flag = recipe.damageIgnoringDefense > 0;
		if (flag)
		{
			PlayFuegoSfx();
		}
		else if (recipe.healAmount > 0)
		{
			PlayCurarseSfx();
		}
		ActionGifData gifData = new ActionGifData
		{
			actionLabel = flag 
				? ("[GIF: INTELECTO - Explosion de " + recipe.recipeName + "]") 
				: (targetDetective 
					? $"[GIF: INTELECTO - Fabricando y usando {recipe.recipeName} para curar al Detective Vargas]" 
					: $"[GIF: INTELECTO - Fabricando y usando {recipe.recipeName} para curar a Enrique]"),
			placeholderColor = (flag ? new Color(0.88f, 0.32f, 0.08f, 1f) : new Color(0.08f, 0.58f, 0.78f, 1f)),
			gifFrames = (flag && explosionGif != null && explosionGif.gifFrames != null && explosionGif.gifFrames.Length != 0) ? explosionGif.gifFrames : ((intelectoGif != null) ? intelectoGif.gifFrames : null),
			duration = (flag && explosionGif != null) ? explosionGif.duration : 1.5f
		};
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(gifData, () =>
		{
			List<string> list = new List<string>();
			List<CombatEnemyInstance> affectedTargets = new List<CombatEnemyInstance>();
			if (recipe.isAreaOfEffect)
			{
				for (int i = 0; i < activeEnemies.Count; i++)
				{
					if (activeEnemies[i] != null && activeEnemies[i].IsAlive)
					{
						affectedTargets.Add(activeEnemies[i]);
					}
				}
			}
			else if (target != null && target.IsAlive)
			{
				affectedTargets.Add(target);
			}

			if (recipe.damageIgnoringDefense > 0 && affectedTargets.Count > 0)
			{
				int num = (((Object)(object)playerStats != (Object)null) ? playerStats.GetIntelectoDamageBonus() : 0);
				int rawDmg = recipe.damageIgnoringDefense + num;
				if (recipe.isAreaOfEffect && affectedTargets.Count > 1)
				{
					int totalHit = 0;
					int lastDmg = rawDmg;
					foreach (CombatEnemyInstance enemyTarget in affectedTargets)
					{
						lastDmg = ApplyDamageToEnemyWithBossMechanics(enemyTarget, rawDmg, isDirectMelee: false, canBreakBossShield: true, "Enrique (Crafteo Intelecto)", attackerIsDetective: false, out var bossLog);
						totalHit++;
						if (!string.IsNullOrEmpty(bossLog))
						{
							list.Add(bossLog);
						}
					}
					list.Add($"{lastDmg} dano explosivo en area a {totalHit} enemigos");
				}
				else
				{
					CombatEnemyInstance singleTarget = affectedTargets[0];
					int num2 = ApplyDamageToEnemyWithBossMechanics(singleTarget, rawDmg, isDirectMelee: false, canBreakBossShield: true, "Enrique (Crafteo Intelecto)", attackerIsDetective: false, out var bossLog);
					list.Add($"{num2} dano ignorando defensa a {singleTarget.data.enemyName}");
					if (!string.IsNullOrEmpty(bossLog))
					{
						list.Add(bossLog);
					}
				}
			}
			if (recipe.healAmount > 0)
			{
				int intelectoHealBonus = (((Object)(object)playerStats != (Object)null) ? playerStats.GetIntelectoHealBonus() : 0);
				int num3 = recipe.healAmount + intelectoHealBonus;
				if (targetDetective && hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null)
				{
					bool wasUnconscious = (detectiveStats.HP_Actual <= 0);
					if (wasUnconscious)
					{
						detectiveStats.HP_Actual = Mathf.Clamp(num3, 1, detectiveStats.HP_Max);
						list.Add($"¡Reanimación de Vargas con {detectiveStats.HP_Actual} HP!");
					}
					else
					{
						int prev = detectiveStats.HP_Actual;
						detectiveStats.Heal(num3);
						list.Add($"+{detectiveStats.HP_Actual - prev} HP para Detective Vargas");
					}
				}
				else
				{
					if ((Object)(object)playerStats != (Object)null)
					{
						playerStats.Heal(num3);
					}
					list.Add($"+{num3} HP para Enrique");
				}
			}
			if (recipe.appliesConfusion)
			{
				int confusedCount = 0;
				string lastConfusedName = "";
				foreach (CombatEnemyInstance enemyTarget in affectedTargets)
				{
					if (enemyTarget != null && enemyTarget.IsAlive)
					{
						enemyTarget.isConfused = true;
						confusedCount++;
						lastConfusedName = enemyTarget.data.enemyName;
					}
				}
				if (confusedCount > 1)
				{
					list.Add($"TODOS los enemigos ({confusedCount}) quedan CONFUNDIDOS (1 turno)");
				}
				else if (confusedCount == 1)
				{
					list.Add(lastConfusedName + " queda CONFUNDIDO (1 turno)");
				}
			}
			if (recipe.enemyDefenseDebuff > 0 || recipe.enemyAttackDebuff > 0 || recipe.enemyMotricidadDebuff > 0)
			{
				foreach (CombatEnemyInstance enemyTarget in affectedTargets)
				{
					if (enemyTarget != null && enemyTarget.IsAlive)
					{
						enemyTarget.defenseDebuff += recipe.enemyDefenseDebuff;
						enemyTarget.attackDebuff += recipe.enemyAttackDebuff;
						enemyTarget.motricidadDebuff += recipe.enemyMotricidadDebuff;
					}
				}
				List<string> debuffParts = new List<string>();
				if (recipe.enemyDefenseDebuff > 0) debuffParts.Add($"-{recipe.enemyDefenseDebuff} DEF");
				if (recipe.enemyAttackDebuff > 0) debuffParts.Add($"-{recipe.enemyAttackDebuff} ATQ");
				if (recipe.enemyMotricidadDebuff > 0) debuffParts.Add($"-{recipe.enemyMotricidadDebuff} VEL");
				list.Add("Enemigo(s) sufren " + string.Join(", ", debuffParts));
			}
			if (recipe.playerAttackBuff > 0)
			{
				playerAttackBuff += recipe.playerAttackBuff;
				list.Add($"+{recipe.playerAttackBuff} ATQ al proximo golpe de Enrique");
			}
			if (recipe.grantsPlayerGuard)
			{
				isMotricidadDefending = true;
				list.Add("Guardia Defensiva activa en esta ronda");
			}
			if (recipe.detectiveAmmoRestored > 0 && hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null)
			{
				int addedAmmo = detectiveStats.ReloadAmmo(recipe.detectiveAmmoRestored);
				list.Add($"+{addedAmmo} Balas .38 para Detective Vargas ({detectiveStats.municionActual}/{detectiveStats.municionMaxima})");
			}
			string text2 = ((list.Count > 0) ? string.Join(" | ", list) : "Efecto aplicado");
			SetBattleLog("INTELECTO [" + recipe.recipeName + "]: " + text2 + ".");
			EnsureValidLivingTarget();
			UpdateHealthUI();
			CheckPostPlayerActionFlow(target);
		}));
	}

	public void OnMotricidadButton()
	{
		if (state == BattleState.PLAYERTURN)
		{
			PlayBotonEnCombateSfx();
			HideIntelectoPanelSilent();
			HideMundoInternoPanelSilent();
			PlayEsquiveSfx();
			((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(motricidadGif, () =>
			{
				isMotricidadDefending = true;
				int num = (((Object)(object)playerStats != (Object)null) ? playerStats.motricidad : 12);
				int num2 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetDisplayGuardReductionPercentage() : 70);
				int num3 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetDisplayDodgePercentage() : 35);
				SetBattleLog($"MOTRICIDAD ({num}): Guardia activa contra todos los ataques de esta ronda (-{num2}% dano o {num3}% evasion).");
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
			}));
		}
		else if (state == BattleState.DETECTIVETURN)
		{
			if ((Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
			{
				PlayBotonEnCombateSfx();
				HideIntelectoPanelSilent();
				HideMundoInternoPanelSilent();
				SetActionButtonsInteractable(interactable: false);
				ExecuteDetectiveDodgeAction();
			}
		}
	}

	public void OnMundoInternoButton()
	{
		if (state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN)
		{
			PlayBotonEnCombateSfx();
			HideIntelectoPanelSilent();
			EnsureValidLivingTarget();
			if ((Object)(object)mundoInternoUI == (Object)null && (Object)(object)mundoInternoPanel != (Object)null)
			{
				mundoInternoUI = mundoInternoPanel.GetComponent<CombatMundoInternoUI>();
			}
			if ((Object)(object)mundoInternoUI != (Object)null)
			{
				mundoInternoUI.ShowAndRefresh(this);
				SetBattleLog(state == BattleState.DETECTIVETURN
					? "TÁCTICA DEL DETECTIVE: Elige una habilidad táctica (Analizar Punto Débil, Concentración o Supresión)."
					: "MUNDO INTERNO: Elige una habilidad psicologica (Insultar, Animarse o Delirio Psiquico).");
			}
			else if ((Object)(object)mundoInternoPanel != (Object)null)
			{
				mundoInternoPanel.SetActive(true);
				SetBattleLog("MUNDO INTERNO: Elige una habilidad.");
			}
			else if (state == BattleState.DETECTIVETURN)
			{
				ExecuteDetectiveSkill(DetectiveActionMenu.DetectiveSkillType.ConcentracionTactica);
			}
			else
			{
				ExecuteMundoInternoDelirio();
			}
		}
	}

	public void CloseMundoInternoPanel()
	{
		PlayBotonEnCombateSfx();
		HideMundoInternoPanelSilent();
		if ((state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN) && activeEnemies.Count > 0)
		{
			EnsureValidLivingTarget();
			string enemyName = activeEnemies[selectedTargetIndex].data.enemyName;
			string actorLabel = (state == BattleState.DETECTIVETURN) ? "DETECTIVE VARGAS" : "ENRIQUE";
			SetBattleLog($"TU TURNO — {actorLabel} (Objetivo: {enemyName}) | Cola: {GetTurnOrderSummaryText()}");
		}
	}

	public void HideMundoInternoPanelSilent()
	{
		if ((Object)(object)mundoInternoUI != (Object)null)
		{
			mundoInternoUI.Hide();
		}
		if ((Object)(object)mundoInternoPanel != (Object)null)
		{
			mundoInternoPanel.SetActive(false);
		}
	}

	public CombatEnemyInstance GetSelectedLivingTarget()
	{
		EnsureValidLivingTarget();
		if (selectedTargetIndex >= 0 && selectedTargetIndex < activeEnemies.Count)
		{
			return activeEnemies[selectedTargetIndex];
		}
		return null;
	}

	public void ExecuteMundoInternoInsultar()
	{
		if (state == BattleState.PLAYERTURN)
		{
			PlayBotonEnCombateSfx();
			HideIntelectoPanelSilent();
			HideMundoInternoPanelSilent();
			EnsureValidLivingTarget();
			CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
			int debuffAmount = (((Object)(object)playerStats != (Object)null) ? playerStats.GetInsultarDebuffAmount() : 4);
			insultarGif.actionLabel = $"[GIF: INSULTAR - Provocando a {target.data.enemyName} (-{debuffAmount} ATQ)]";
			PlayEfectoSfx();
			((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(insultarGif, () =>
			{
				target.attackDebuff += debuffAmount;
				int num = Mathf.Max(1, target.data.attack - target.attackDebuff);
				SetBattleLog($"MUNDO INTERNO [INSULTO]: Enrique lanza un insulto mordaz a {target.data.enemyName}. Su ataque cae en -{debuffAmount} (Ataque actual: {num}).");
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
			}));
		}
	}

	public void ExecuteMundoInternoAnimarse()
	{
		if (state == BattleState.PLAYERTURN)
		{
			PlayBotonEnCombateSfx();
			HideIntelectoPanelSilent();
			HideMundoInternoPanelSilent();
			EnsureValidLivingTarget();
			int buffAmount = (((Object)(object)playerStats != (Object)null) ? playerStats.GetAnimarseBuffAmount() : 5);
			animarseGif.actionLabel = $"[GIF: ANIMARSE - Enrique respira hondo (+{buffAmount} ATQ)]";
			PlayEfectoSfx();
			((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(animarseGif, () =>
			{
				playerAttackBuff += buffAmount;
				SetBattleLog($"MUNDO INTERNO [ANIMARSE]: Enrique se llena de coraje (+{buffAmount} al dano de su proximo golpe fisico Corporeo).");
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
			}));
		}
	}

	public void ExecuteMundoInternoDelirio()
	{
		if (state != BattleState.PLAYERTURN)
		{
			return;
		}
		PlayBotonEnCombateSfx();
		HideIntelectoPanelSilent();
		HideMundoInternoPanelSilent();
		EnsureValidLivingTarget();
		CombatEnemyInstance target = activeEnemies[selectedTargetIndex];
		mundoInternoGif.actionLabel = "[GIF: MUNDO INTERNO - Delirio psiquico sobre " + target.data.enemyName + "]";
		PlayEfectoSfx();
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(mundoInternoGif, () =>
		{
			target.isConfused = true;
			int num = (((Object)(object)playerStats != (Object)null) ? playerStats.GetDelirioPsychicDamage() : 0);
			if (num > 0)
			{
				target.currentHP = Mathf.Max(0, target.currentHP - num);
				currentEnemy = target.data;
				currentEnemyHP = target.currentHP;
				SetBattleLog($"MUNDO INTERNO [DELIRIO]: {target.data.enemyName} sufre {num} de dano psiquico y queda CONFUNDIDO (pierde 1 turno).");
				EnsureValidLivingTarget();
				UpdateHealthUI();
				CheckPostPlayerActionFlow(target);
			}
			else
			{
				SetBattleLog("MUNDO INTERNO [DELIRIO]: " + target.data.enemyName + " queda CONFUNDIDO y perdera su proximo turno.");
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
			}
		}));
	}

	public IEnumerator PlayActionGifCoroutine(ActionGifData gifData)
	{
		state = BattleState.PLAYING_GIF;
		SetActionButtonsInteractable(interactable: false);
		float num = ((gifData != null && gifData.duration > 0f) ? Mathf.Clamp(gifData.duration, 0.4f, 1.5f) : 1.5f);
		if ((Object)(object)actionGifPanel != (Object)null)
		{
			actionGifPanel.SetActive(true);
		}
		if (gifData != null && gifData.gifFrames != null && gifData.gifFrames.Length != 0)
		{
			if ((Object)(object)actionGifLabelText != (Object)null)
			{
				((Component)actionGifLabelText).gameObject.SetActive(false);
			}
			if ((Object)(object)actionGifImage != (Object)null)
			{
				((Graphic)actionGifImage).color = Color.white;
				actionGifImage.preserveAspect = true;
			}
			int frameCount = gifData.gifFrames.Length;
			float frameTime = num / (float)frameCount;
			for (int i = 0; i < frameCount; i++)
			{
				if ((Object)(object)actionGifImage != (Object)null)
				{
					actionGifImage.sprite = gifData.gifFrames[i];
				}
				yield return (object)new WaitForSeconds(frameTime);
			}
		}
		else
		{
			if ((Object)(object)actionGifImage != (Object)null)
			{
				actionGifImage.sprite = null;
				actionGifImage.preserveAspect = false;
				((Graphic)actionGifImage).color = gifData?.placeholderColor ?? new Color(0.12f, 0.12f, 0.14f, 1f);
			}
			if ((Object)(object)actionGifLabelText != (Object)null)
			{
				((Component)actionGifLabelText).gameObject.SetActive(true);
				((TMP_Text)actionGifLabelText).text = ((gifData != null && !string.IsNullOrEmpty(gifData.actionLabel)) ? gifData.actionLabel : "[GIF PLACEHOLDER]");
			}
			yield return (object)new WaitForSeconds(num);
		}
		if ((Object)(object)actionGifPanel != (Object)null)
		{
			actionGifPanel.SetActive(false);
		}
	}

	private IEnumerator ExecuteActionWithGifCoroutine(ActionGifData gifData, Action applyEffectAfterGif)
	{
		yield return ((MonoBehaviour)this).StartCoroutine(PlayActionGifCoroutine(gifData));
		applyEffectAfterGif?.Invoke();
	}

	private void CheckPostPlayerActionFlow(CombatEnemyInstance affectedEnemy)
	{
		if (affectedEnemy != null)
		{
			if (!affectedEnemy.IsAlive)
			{
				PlayMuerteEnemigoSfx();
				if ((Object)(object)affectedEnemy.data != (Object)null && affectedEnemy.data.deadFrames != null && affectedEnemy.data.deadFrames.Length != 0)
				{
					PlayEnemySlotOneShot(affectedEnemy, affectedEnemy.data.deadFrames, 0.16f, holdLastFrame: true);
				}
			}
			else if (!affectedEnemy.hasTriggeredHalfHealthDialogue && affectedEnemy.currentHP <= affectedEnemy.data.maxHP / 2 && (Object)(object)affectedEnemy.data != (Object)null && affectedEnemy.data.specialFrames != null && affectedEnemy.data.specialFrames.Length != 0)
			{
				PlayEnemySlotOneShot(affectedEnemy, affectedEnemy.data.specialFrames, 0.11f, holdLastFrame: false);
			}
			else if ((Object)(object)affectedEnemy.data != (Object)null && affectedEnemy.data.hurtFrames != null && affectedEnemy.data.hurtFrames.Length != 0)
			{
				PlayEnemySlotOneShot(affectedEnemy, affectedEnemy.data.hurtFrames, 0.14f, holdLastFrame: false);
			}
		}
		if (AreAllEnemiesDead())
		{
			EndBattle(won: true);
			return;
		}
		if (affectedEnemy != null && affectedEnemy.IsAlive && !affectedEnemy.hasTriggeredHalfHealthDialogue && affectedEnemy.currentHP <= affectedEnemy.data.maxHP / 2)
		{
			affectedEnemy.hasTriggeredHalfHealthDialogue = true;
			if (affectedEnemy.data.halfHealthDialogueLines != null && affectedEnemy.data.halfHealthDialogueLines.Length != 0)
			{
				StartCombatDialogue(affectedEnemy.data.halfHealthDialogueLines, AdvanceTurnQueueAndContinue);
				return;
			}
		}
		AdvanceTurnQueueAndContinue();
	}

	private IEnumerator EnemyTurnCoroutine(int enemyIndex)
	{
		if (enemyIndex < 0 || enemyIndex >= activeEnemies.Count || !activeEnemies[enemyIndex].IsAlive)
		{
			AdvanceTurnQueueAndContinue();
			yield break;
		}
		CombatEnemyInstance attacker = activeEnemies[enemyIndex];
		if (attacker.vulnerabilityTurns > 0)
		{
			attacker.vulnerabilityTurns--;
		}
		yield return (object)new WaitForSeconds(0.5f);
		if (attacker.isConfused)
		{
			attacker.isConfused = false;
			SetBattleLog("TURNO DE " + attacker.data.enemyName.ToUpper() + ": Esta CONFUNDIDO por Mundo Interno y pierde su ataque!");
			UpdateHealthUI();
			yield return (object)new WaitForSeconds(1.2f);
			AdvanceTurnQueueAndContinue();
			yield break;
		}
		bool targetDetective = false;
		if (hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
		{
			targetDetective = ((enemyIndex % 2 == 1) ? (Random.value < 0.65f) : (Random.value < 0.42f));
		}
		lastEnemyTargetWasDetective = targetDetective;
		string targetActorName = (targetDetective ? detectiveStats.detectiveName : "Enrique");
		float bossMultiplier = 1f;
		string bossPhaseLog = string.Empty;
		int bossFieldDmg = 0;
		if ((Object)(object)activeLevel2BossController != (Object)null && IsBossEnemy(attacker))
		{
			activeLevel2BossController.ExecuteBossTurnStateTransition(attacker.currentHP, attacker.data.maxHP, out bossMultiplier, out bossPhaseLog, out bossFieldDmg);
		}
		Sprite[] nextEnemyAttackFrames = GetNextEnemyAttackFrames(attacker);
		if (nextEnemyAttackFrames != null && nextEnemyAttackFrames.Length != 0)
		{
			float frameDuration = Mathf.Clamp(enemyAttackGif.duration / (float)nextEnemyAttackFrames.Length, 0.08f, 0.22f);
			PlayEnemySlotOneShot(attacker, nextEnemyAttackFrames, frameDuration, holdLastFrame: false);
			enemyAttackGif.gifFrames = nextEnemyAttackFrames;
		}
		else if (enemyAttackGif.gifFrames == null || enemyAttackGif.gifFrames.Length == 0)
		{
			if (corporeoGif != null && corporeoGif.gifFrames != null && corporeoGif.gifFrames.Length != 0)
			{
				enemyAttackGif.gifFrames = corporeoGif.gifFrames;
			}
		}
		string customAttackName = ((!string.IsNullOrEmpty(attacker.data.primaryAttackName)) ? attacker.data.primaryAttackName : "Ataque Callejero");
		enemyAttackGif.actionLabel = $"[GIF: {attacker.data.enemyName.ToUpper()} ({customAttackName}) atacando a {targetActorName}]";
		enemyAttackGif.placeholderColor = new Color(0.18f, 0.12f, 0.12f, 1f);
		if (IsBossEnemy(attacker))
		{
			PlayAtaqueJefeSfx();
		}
		else
		{
			PlayAtaqueEnemigoSfx();
		}
		yield return ((MonoBehaviour)this).StartCoroutine(PlayActionGifCoroutine(enemyAttackGif));
		int baseAtk = Mathf.Max(1, attacker.data.attack - attacker.attackDebuff);
		int num = Mathf.Max(1, Mathf.RoundToInt((float)baseAtk * bossMultiplier));
		string text = ((attacker.attackDebuff > 0) ? $" (-{attacker.attackDebuff} ATQ reduc.)" : "");
		if (bossFieldDmg > 0)
		{
			if ((Object)(object)playerStats != (Object)null && playerStats.IsAlive)
			{
				playerStats.TakeDamage(bossFieldDmg);
			}
			if (hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
			{
				detectiveStats.TakeDamage(bossFieldDmg, out var _);
			}
		}
		string bossPrefix = ((!string.IsNullOrEmpty(bossPhaseLog)) ? (bossPhaseLog + " | ") : "");
		if (targetDetective && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive)
		{
			if (detectiveStats.TryRollDodge(out var effDodgeChance))
			{
				PlayEsquiveSfx();
				SetBattleLog($"{bossPrefix}¡ESQUIVA DEL DETECTIVE ({effDodgeChance:0}%)! Vargas evade por completo '{customAttackName}' de {attacker.data.enemyName} (0 dano).");
			}
			else
			{
				int detDmg = detectiveStats.TakeDamage(num, out var mitigated);
				SetBattleLog($"{bossPrefix}TURNO DE {attacker.data.enemyName.ToUpper()}: Ejecuta '{customAttackName}' contra {detectiveStats.detectiveName} (-{detDmg} HP, mitigado -{mitigated} por Defensa){text}.");
				if (!detectiveStats.IsAlive)
				{
					SetBattleLog($"{bossPrefix}¡EL DETECTIVE VARGAS HA CAÍDO INCONSCIENTE! Enrique continúa el combate en solitario.");
				}
			}
		}
		else if (isMotricidadDefending)
		{
			float num2 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetDodgeChanceAgainst(attacker.data.motricidad) : Mathf.Clamp(0.25f + (float)(12 - attacker.data.motricidad) * 0.05f, 0.15f, 0.75f));
			bool flag = Random.value <= num2;
			PlayEsquiveSfx();
			if (flag)
			{
				SetBattleLog($"{bossPrefix}MOTRICIDAD: Enrique ESQUIVA por completo '{customAttackName}' de {attacker.data.enemyName} (0 dano).");
			}
			else
			{
				float num3 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetGuardDamageMultiplier() : 0.3f);
				int num4 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetDisplayGuardReductionPercentage() : 70);
				int num5 = Mathf.Max(1, Mathf.RoundToInt((float)num * num3));
				if ((Object)(object)playerStats != (Object)null)
				{
					playerStats.TakeDamage(num5);
				}
				SetBattleLog($"{bossPrefix}MOTRICIDAD: Guardia defensiva reduce un {num4}% el golpe de {attacker.data.enemyName} ({num5} dano{text}).");
			}
		}
		else
		{
			if ((Object)(object)playerStats != (Object)null)
			{
				playerStats.TakeDamage(num);
			}
			SetBattleLog($"{bossPrefix}TURNO DE {attacker.data.enemyName.ToUpper()}: Ejecuta '{customAttackName}' contra Enrique e inflige {num} de dano{text}.");
		}
		UpdateHealthUI();
		if ((Object)(object)playerStats != (Object)null && !playerStats.IsAlive)
		{
			EndBattle(won: false);
			yield break;
		}
		yield return (object)new WaitForSeconds(0.9f);
		AdvanceTurnQueueAndContinue();
	}

	private bool AreAllEnemiesDead()
	{
		for (int i = 0; i < activeEnemies.Count; i++)
		{
			if (activeEnemies[i].IsAlive)
			{
				return false;
			}
		}
		return true;
	}

	public void StartCombatDialogue(string[] lines)
	{
		StartCombatDialogue(lines, ExecuteCurrentTurnInQueue);
	}

	public void StartCombatDialogue(string[] lines, Action onFinished)
	{
		if (lines == null || lines.Length == 0)
		{
			onFinished?.Invoke();
			return;
		}
		onDialogueFinishedCallback = onFinished;
		state = BattleState.DIALOGUE;
		dialogueStateChangeFrame = Time.frameCount;
		SetActionButtonsInteractable(interactable: false);
		currentDialogueLines = lines;
		currentDialogueIndex = 0;
		if ((Object)(object)combatDialoguePanel != (Object)null)
		{
			combatDialoguePanel.SetActive(true);
		}
		ShowCombatDialogueLine(currentDialogueLines[currentDialogueIndex]);
	}

	public void AdvanceCombatDialogue()
	{
		if (state != BattleState.DIALOGUE)
		{
			return;
		}
		PlayBotonConfirmacionDialogoSfx();
		dialogueStateChangeFrame = Time.frameCount;
		currentDialogueIndex++;
		if (currentDialogueLines != null && currentDialogueIndex < currentDialogueLines.Length)
		{
			ShowCombatDialogueLine(currentDialogueLines[currentDialogueIndex]);
			return;
		}
		if ((Object)(object)combatDialoguePanel != (Object)null)
		{
			combatDialoguePanel.SetActive(false);
		}
		Action action = onDialogueFinishedCallback;
		onDialogueFinishedCallback = null;
		if (action != null)
		{
			action();
		}
		else
		{
			ExecuteCurrentTurnInQueue();
		}
	}

	private void ShowCombatDialogueLine(string rawLine)
	{
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_037e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0354: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0304: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(rawLine))
		{
			rawLine = "...";
		}
		DialogueSpeakerType dialogueSpeakerType = DialogueSpeakerType.Narrator;
		SkillVoiceType voice = SkillVoiceType.Corporeo;
		string text = "NARRADOR";
		string text2 = rawLine;
		int num = rawLine.IndexOf(':');
		if (num > 0 && num < 45)
		{
			string text3 = rawLine.Substring(0, num).Trim();
			text2 = rawLine.Substring(num + 1).Trim();
			string text4 = text3.ToUpperInvariant();
			if (text4.Contains("NARRADOR"))
			{
				dialogueSpeakerType = DialogueSpeakerType.Narrator;
				text = "NARRADOR";
			}
			else if (text4.Contains("CORPOREO") || text4.Contains("CORPÓREO"))
			{
				dialogueSpeakerType = DialogueSpeakerType.SkillVoice;
				voice = SkillVoiceType.Corporeo;
				text = "VOZ: CORPOREO";
			}
			else if (text4.Contains("INTELECTO"))
			{
				dialogueSpeakerType = DialogueSpeakerType.SkillVoice;
				voice = SkillVoiceType.Intelecto;
				text = "VOZ: INTELECTO";
			}
			else if (text4.Contains("MOTRICIDAD"))
			{
				dialogueSpeakerType = DialogueSpeakerType.SkillVoice;
				voice = SkillVoiceType.Motricidad;
				text = "VOZ: MOTRICIDAD";
			}
			else if (text4.Contains("MUNDO INTERNO") || text4.Contains("MUNDOINTERNO"))
			{
				dialogueSpeakerType = DialogueSpeakerType.SkillVoice;
				voice = SkillVoiceType.MundoInterno;
				text = "VOZ: MUNDO INTERNO";
			}
			else
			{
				dialogueSpeakerType = DialogueSpeakerType.Character;
				text = text3.ToUpperInvariant();
			}
		}
		bool flag = dialogueSpeakerType != DialogueSpeakerType.Narrator;
		if ((Object)(object)combatPortraitContainer != (Object)null)
		{
			combatPortraitContainer.SetActive(flag);
		}
		float num2 = (flag ? 0.205f : 0.03f);
		if ((Object)(object)combatSpeakerNameText != (Object)null)
		{
			RectTransform rectTransform = ((TMP_Text)combatSpeakerNameText).rectTransform;
			rectTransform.anchorMin = new Vector2(num2, rectTransform.anchorMin.y);
		}
		if ((Object)(object)combatDialogueText != (Object)null)
		{
			RectTransform rectTransform2 = ((TMP_Text)combatDialogueText).rectTransform;
			rectTransform2.anchorMin = new Vector2(num2, rectTransform2.anchorMin.y);
			((TMP_Text)combatDialogueText).text = text2;
		}
		switch (dialogueSpeakerType)
		{
		case DialogueSpeakerType.Narrator:
			if ((Object)(object)combatSpeakerNameText != (Object)null)
			{
				((TMP_Text)combatSpeakerNameText).text = "NARRADOR";
				((Graphic)combatSpeakerNameText).color = new Color(0.68f, 0.7f, 0.75f, 1f);
			}
			break;
		case DialogueSpeakerType.Character:
		{
			bool isDetective = text.Contains("INVESTIGADOR") || text.Contains("DETECTIVE") || text.Contains("VARGAS");
			Sprite val = (text.Contains("ENRIQUE") ? enriquePortrait : (isDetective ? detectiveCombatPortrait : null));
			if ((Object)(object)combatPortraitImage != (Object)null)
			{
				if ((Object)(object)val != (Object)null)
				{
					combatPortraitImage.sprite = val;
					((Graphic)combatPortraitImage).color = Color.white;
					combatPortraitImage.preserveAspect = isDetective;
				}
				else
				{
					combatPortraitImage.sprite = null;
					((Graphic)combatPortraitImage).color = characterGrayColor;
				}
			}
			if ((Object)(object)combatPortraitLabelText != (Object)null)
			{
				bool flag3 = (Object)(object)val == (Object)null;
				((Component)combatPortraitLabelText).gameObject.SetActive(flag3);
				((TMP_Text)combatPortraitLabelText).text = (flag3 ? ("RETRATO\n[GRIS]\n" + text) : string.Empty);
			}
			if ((Object)(object)combatSpeakerNameText != (Object)null)
			{
				((TMP_Text)combatSpeakerNameText).text = text;
				((Graphic)combatSpeakerNameText).color = new Color(0.88f, 0.88f, 0.9f, 1f);
			}
			break;
		}
		case DialogueSpeakerType.SkillVoice:
		{
			Color skillVoiceColor = GetSkillVoiceColor(voice);
			string skillVoiceDisplayName = IntroSlideshowManager.GetSkillVoiceDisplayName(voice);
			Sprite skillVoicePortrait = GetSkillVoicePortrait(voice);
			if ((Object)(object)combatPortraitImage != (Object)null)
			{
				if ((Object)(object)skillVoicePortrait != (Object)null)
				{
					combatPortraitImage.sprite = skillVoicePortrait;
					((Graphic)combatPortraitImage).color = Color.white;
					combatPortraitImage.preserveAspect = false;
				}
				else
				{
					combatPortraitImage.sprite = null;
					((Graphic)combatPortraitImage).color = skillVoiceColor;
				}
			}
			if ((Object)(object)combatPortraitLabelText != (Object)null)
			{
				bool flag2 = (Object)(object)skillVoicePortrait == (Object)null;
				((Component)combatPortraitLabelText).gameObject.SetActive(flag2);
				((TMP_Text)combatPortraitLabelText).text = (flag2 ? ("VOZ\n[" + skillVoiceDisplayName + "]") : string.Empty);
			}
			if ((Object)(object)combatSpeakerNameText != (Object)null)
			{
				((TMP_Text)combatSpeakerNameText).text = text;
				((Graphic)combatSpeakerNameText).color = skillVoiceColor;
			}
			break;
		}
		}
	}

	public Sprite GetSkillVoicePortrait(SkillVoiceType voice)
	{
		return voice switch
		{
			SkillVoiceType.Corporeo => corporeoPortrait, 
			SkillVoiceType.Intelecto => intelectoPortrait, 
			SkillVoiceType.Motricidad => motricidadPortrait, 
			SkillVoiceType.MundoInterno => mundoInternoPortrait, 
			_ => (Sprite)null, 
		};
	}

	private Color GetSkillVoiceColor(SkillVoiceType voice)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		return voice switch
		{
			SkillVoiceType.Corporeo => corporeoColor, 
			SkillVoiceType.Intelecto => intelectoColor, 
			SkillVoiceType.Motricidad => motricidadColor, 
			SkillVoiceType.MundoInterno => mundoInternoColor, 
			_ => Color.white, 
		};
	}

	private void EndBattle(bool won)
	{
		SetActionButtonsInteractable(interactable: false);
		if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			detectiveActionMenu.HideDetectiveTurnMenu(restoreProtagonistButtons: true);
		}
		if (won)
		{
			state = BattleState.WON;
			int totalEnemyExpReward = 0;
			for (int i = 0; i < activeEnemies.Count; i++)
			{
				if (activeEnemies[i] != null && (Object)(object)activeEnemies[i].data != (Object)null)
				{
					totalEnemyExpReward += Mathf.Max(45, activeEnemies[i].data.EXP_Reward);
				}
			}
			int xpAwarded = 0;
			int num = 0;
			if ((Object)(object)playerStats == (Object)null)
			{
				playerStats = Object.FindAnyObjectByType<PlayerStats>();
			}
			if ((Object)(object)playerStats != (Object)null)
			{
				num = playerStats.GainExperienceFromBattle(activeEnemies.Count, isCurrentEncounterBoss, totalEnemyExpReward, out xpAwarded);
			}
			lastDetectiveAutoLevelUpLog.Clear();
			lastDetectiveExpGained = 0;
			lastDetectiveLevelsGained = 0;
			int detLevelsGained = 0;
			if (hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null)
			{
				detLevelsGained = detectiveStats.GainExperienceFromVictory(xpAwarded, out lastDetectiveExpGained, out lastDetectiveAutoLevelUpLog);
				lastDetectiveLevelsGained = detLevelsGained;
			}
			if ((Object)(object)activeLevel2BossController != (Object)null && isCurrentEncounterBoss)
			{
				activeLevel2BossController.NotifyBossDefeated(this);
			}
			UpdateHealthUI();
			string detLogSuffix = "";
			if (detLevelsGained > 0 && lastDetectiveAutoLevelUpLog.Count > 0)
			{
				detLogSuffix = " | " + string.Join(" | ", lastDetectiveAutoLevelUpLog);
			}
			else if (hasDetectiveInParty && lastDetectiveExpGained > 0)
			{
				detLogSuffix = $" | Detective +{lastDetectiveExpGained} EXP";
			}
			if (num > 0 && (Object)(object)playerStats != (Object)null)
			{
				SetBattleLog($"¡VICTORIA (+{xpAwarded} EXP)! ¡ENRIQUE SUBE AL NIVEL {playerStats.level}!{detLogSuffix}");
			}
			else
			{
				SetBattleLog($"VICTORIA (+{xpAwarded} EXP). Todos los enemigos han caido.{detLogSuffix}");
			}
			if ((Object)(object)MusicManager.Instance != (Object)null)
			{
				if ((Object)(object)victoryMenuMusic != (Object)null && (Object)(object)MusicManager.Instance.victoryMenuMusic == (Object)null)
				{
					MusicManager.Instance.victoryMenuMusic = victoryMenuMusic;
				}
				MusicManager.Instance.PlayVictoryMenuMusic();
			}
			if (currentEncounterObjectsToDisable != null)
			{
				GameObject[] array = currentEncounterObjectsToDisable;
				foreach (GameObject val in array)
				{
					if ((Object)(object)val != (Object)null)
					{
						val.SetActive(false);
					}
				}
				currentEncounterObjectsToDisable = null;
			}
			UnityEvent val2 = onBattleWon;
			if (val2 != null)
			{
				val2.Invoke();
			}
			((MonoBehaviour)this).StartCoroutine(ShowPostCombatFlowCoroutine(num, xpAwarded));
			return;
		}
		state = BattleState.LOST;
		SetBattleLog("DERROTA... Enrique cae inconsciente sobre el asfalto de Santa Felipina.");
		PlayDanoFinalMuerteSfx();
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			if ((Object)(object)gameOverMusic != (Object)null && (Object)(object)MusicManager.Instance.gameOverMusic == (Object)null)
			{
				MusicManager.Instance.gameOverMusic = gameOverMusic;
			}
			MusicManager.Instance.PlayGameOverMusic();
		}
		if (currentEncounterObjectsToDisable != null)
		{
			GameObject[] array = currentEncounterObjectsToDisable;
			foreach (GameObject val3 in array)
			{
				if ((Object)(object)val3 != (Object)null)
				{
					EnemyEncounterTrigger component = val3.GetComponent<EnemyEncounterTrigger>();
					if ((Object)(object)component != (Object)null)
					{
						component.ResetTrigger();
					}
				}
			}
			currentEncounterObjectsToDisable = null;
		}
		UnityEvent val4 = onBattleLost;
		if (val4 != null)
		{
			val4.Invoke();
		}
		if ((Object)(object)gameOverPanel != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(ShowGameOverAfterDelay(1.5f));
		}
		else
		{
			((MonoBehaviour)this).StartCoroutine(CloseBattlePanelAfterDelay(2f, enablePlayerMovement: true));
		}
	}

	private IEnumerator ShowGameOverAfterDelay(float delay)
	{
		yield return (object)new WaitForSeconds(delay);
		if ((Object)(object)battlePanel != (Object)null)
		{
			battlePanel.SetActive(false);
		}
		if ((Object)(object)actionGifPanel != (Object)null)
		{
			actionGifPanel.SetActive(false);
		}
		if ((Object)(object)combatDialoguePanel != (Object)null)
		{
			combatDialoguePanel.SetActive(false);
		}
		HideIntelectoPanelSilent();
		if ((Object)(object)gameOverPanel != (Object)null)
		{
			gameOverPanel.SetActive(true);
		}
	}

	private static ItemData ResolveOrCreateCanonicalRewardItem(string itemName, string description, ItemCategory category, int healAmount)
	{
		if ((Object)(object)InventoryManager.Instance != (Object)null)
		{
			if (InventoryManager.Instance.knownRecipes != null)
			{
				foreach (CraftingRecipeData recipe in InventoryManager.Instance.knownRecipes)
				{
					if ((Object)(object)recipe == (Object)null) continue;
					if ((Object)(object)recipe.materialA != (Object)null && InventoryManager.GetCanonicalItemKey(recipe.materialA.itemName) == InventoryManager.GetCanonicalItemKey(itemName))
						return recipe.materialA;
					if ((Object)(object)recipe.materialB != (Object)null && InventoryManager.GetCanonicalItemKey(recipe.materialB.itemName) == InventoryManager.GetCanonicalItemKey(itemName))
						return recipe.materialB;
				}
			}
			if (InventoryManager.Instance.items != null)
			{
				foreach (InventorySlot s in InventoryManager.Instance.items)
				{
					if (s != null && (Object)(object)s.item != (Object)null && InventoryManager.GetCanonicalItemKey(s.item.itemName) == InventoryManager.GetCanonicalItemKey(itemName))
						return s.item;
				}
			}
		}
		string[] candidatePaths = new string[]
		{
			"Assets/Data/Items/Item_BotellaVacia.asset",
			"Assets/Data/Items/Botella_Vacia.asset",
			"Assets/Data/Items/Item_ChilePolvo.asset",
			"Assets/Data/Items/Chile_Polvo.asset",
			"Assets/Data/Items/Cinta_Canela.asset",
			"Assets/Data/Items/Item_Refresco.asset",
			"Assets/Data/Items/Refresco_Cola.asset",
			"Assets/Data/Items/Item_Torta.asset",
			"Assets/Data/Items/Torta_Jamon.asset"
		};
		foreach (string p in candidatePaths)
		{
			ItemData loaded = RuntimeAssetCatalog.GetItem(p);
			if ((Object)(object)loaded != (Object)null && InventoryManager.GetCanonicalItemKey(loaded.itemName) == InventoryManager.GetCanonicalItemKey(itemName))
			{
				return loaded;
			}
		}
		ItemData runtimeItem = ScriptableObject.CreateInstance<ItemData>();
		runtimeItem.itemName = itemName;
		runtimeItem.description = description;
		runtimeItem.category = category;
		runtimeItem.healAmount = healAmount;
		runtimeItem.placeholderColor = (category == ItemCategory.MaterialDeCrafteo) ? new Color(0.85f, 0.55f, 0.2f, 1f) : new Color(0.25f, 0.78f, 0.42f, 1f);
		return runtimeItem;
	}

	private List<InventorySlot> GenerateLootDropsFromDefeatedEnemies()
	{
		List<InventorySlot> drops = new List<InventorySlot>();
		int enemyCount = (activeEnemies != null && activeEnemies.Count > 0) ? activeEnemies.Count : 1;
		int rollsCount = isCurrentEncounterBoss ? 3 : Mathf.Clamp(enemyCount + 1, 2, 3);

		for (int i = 0; i < rollsCount; i++)
		{
			int roll = Random.Range(0, 8);
			ItemData droppedItem = null;
			switch (roll)
			{
			case 0:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Polvora Callejera", "Pólvora negra extraída de cartuchos viejos. Se combina con Chatarra Afilada (Granada de Metralla) o con Gasolina (Bomba Cegadora de Humo).", ItemCategory.MaterialDeCrafteo, 0);
				break;
			case 1:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Chatarra Afilada", "Clavos oxidados y láminas cortantes. Se combina con Pólvora Callejera (Granada de Metralla) o con Cinta Canela (Parche Blindado).", ItemCategory.MaterialDeCrafteo, 0);
				break;
			case 2:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Alcohol Industrial", "Botella de alcohol etílico de 96°. Se combina con Refresco Frío para crear un Tónico de Adrenalina (+55 HP, +8 ATQ y Guardia).", ItemCategory.MaterialDeCrafteo, 0);
				break;
			case 3:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Botella Vacia", "Envase de vidrio grueso. Combinado con Gasolina permite crear una Bomba Molotov.", ItemCategory.MaterialDeCrafteo, 0);
				break;
			case 4:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Gasolina", "Combustible inflamable. Combinado con Botella Vacia crea una Bomba Molotov o con Pólvora una Bomba Cegadora.", ItemCategory.MaterialDeCrafteo, 0);
				break;
			case 5:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Cinta Canela", "Rollo de cinta adhesiva industrial. Combinada con Botella Vacia (Vendaje Rudo) o con Chatarra Afilada (Parche Blindado).", ItemCategory.MaterialDeCrafteo, 0);
				break;
			case 6:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Refresco Frio", "Refresco bien helado (+35 HP) o ingrediente para Tónico de Adrenalina.", ItemCategory.ConsumibleCuracion, 35);
				break;
			default:
				droppedItem = ResolveOrCreateCanonicalRewardItem("Torta Callejera", "Torta de milanesa envuelta en papel estraza. Restaura +60 HP.", ItemCategory.ConsumibleCuracion, 60);
				break;
			}

			if ((Object)(object)droppedItem != (Object)null)
			{
				InventorySlot existing = drops.Find(s => s != null && (Object)(object)s.item != (Object)null && InventoryManager.AreItemsEquivalent(s.item, droppedItem));
				if (existing != null)
				{
					existing.quantity += 1;
				}
				else
				{
					drops.Add(new InventorySlot { item = droppedItem, quantity = 1 });
				}
			}
		}

		return drops;
	}

	private IEnumerator ShowPostCombatFlowCoroutine(int levelsGained, int xpAwarded)
	{
		yield return (object)new WaitForSeconds(0.85f);
		ItemData itemData = currentRewardItem;
		string[] array = currentRewardDialogueLines;
		if ((Object)(object)itemData == (Object)null && !hasAwardedStarterWeapon && (currentRewardBundle == null || currentRewardBundle.Count == 0) && !isCurrentEncounterBoss)
		{
			if ((Object)(object)starterRewardWeapon != (Object)null)
			{
				itemData = starterRewardWeapon;
			}
			else if ((Object)(object)InventoryManager.Instance != (Object)null && InventoryManager.Instance.preparedWeaponCatalog != null && InventoryManager.Instance.preparedWeaponCatalog.Count > 0)
			{
				itemData = InventoryManager.Instance.preparedWeaponCatalog[0];
			}
		}
		bool shouldAwardSingleItem = (Object)(object)itemData != (Object)null &&
			(itemData.category != ItemCategory.Arma || (Object)(object)InventoryManager.Instance == (Object)null || !InventoryManager.Instance.IsWeaponUnlocked(itemData));
		if (shouldAwardSingleItem)
		{
			if (itemData.category == ItemCategory.Arma)
			{
				hasAwardedStarterWeapon = true;
			}
			if ((Object)(object)InventoryManager.Instance != (Object)null)
			{
				InventoryManager.Instance.AddItem(itemData);
				if (itemData.category == ItemCategory.Arma && (Object)(object)InventoryManager.Instance.equippedWeapon == (Object)null)
				{
					InventoryManager.Instance.EquipWeapon(itemData);
				}
			}
			if ((Object)(object)MusicManager.Instance != (Object)null)
			{
				MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
			}
			if (array == null || array.Length == 0)
			{
				if (itemData.category == ItemCategory.Arma)
				{
					array = new string[3]
					{
						"NARRADOR: \"¡El lacayo cae al suelo inconsciente! De su cinturón se desprende una pesada " + itemData.itemName.ToUpper() + " con eslabones de acero engrasado. Enrique la recoge del asfalto.\"",
						$"VOZ [CORPOREO]: \"¡Nuestra primera arma callejera! Con esta {itemData.itemName} (+{itemData.weaponAttackBonus} ATQ, +{itemData.weaponMotricidadBonus} Motricidad) nuestros golpes seran mas letales y rapidos.\"",
						"VOZ [INTELECTO]: \"¡" + itemData.itemName.ToUpper() + " OBTENIDA Y EQUIPADA! Se ha colocado en tus manos. Puedes abrir el menú [INVENTARIO / TAB] en cualquier momento para ver tu equipamiento, o hacer clic en tu retrato para cambiarla.\""
					};
				}
				else
				{
					array = new string[2]
					{
						$"NARRADOR: \"Entre las pertenencias del enemigo derrotado encuentras: 1x {itemData.itemName.ToUpper()}.\"",
						$"VOZ [INTELECTO]: \"Lo guardamos en el inventario ({itemData.description}).\""
					};
				}
			}
			bool rewardDialogueFinished = false;
			StartCombatDialogue(array, () =>
			{
				rewardDialogueFinished = true;
			});
			while (!rewardDialogueFinished)
			{
				yield return null;
			}
			if ((Object)(object)PlayerInventoryAndWeaponsUI.Instance != (Object)null)
			{
				PlayerInventoryAndWeaponsUI.Instance.RefreshUI();
			}
		}

		// Recompensa de suministros al derrotar enemigos (tanto en el Jefe Final del Nivel 1 como en todos los combates)
		Scene activeSceneForRewards = SceneManager.GetActiveScene();
		bool isLevel1FinalBoss = isCurrentEncounterBoss && activeSceneForRewards.IsValid() &&
			(activeSceneForRewards.name.Contains("01_Nivel1") || (!string.IsNullOrEmpty(currentBossNextScene) && currentBossNextScene.Contains("02_Nivel2")));
		if (isLevel1FinalBoss && (currentRewardBundle == null || currentRewardBundle.Count == 0))
		{
			currentRewardBundle = new List<InventorySlot>
			{
				new InventorySlot { item = ResolveOrCreateCanonicalRewardItem("Botella Vacia", "Envase de vidrio grueso. Combinado con Gasolina permite crear una Bomba Molotov.", ItemCategory.MaterialDeCrafteo, 0), quantity = 2 },
				new InventorySlot { item = ResolveOrCreateCanonicalRewardItem("Gasolina", "Combustible inflamable. Combinado con una Botella Vacia crea una Bomba Molotov explosiva.", ItemCategory.MaterialDeCrafteo, 0), quantity = 2 },
				new InventorySlot { item = ResolveOrCreateCanonicalRewardItem("Polvora Callejera", "Pólvora negra extraída de cartuchos viejos. Se combina con Chatarra Afilada (Granada de Metralla) o con Gasolina (Bomba Cegadora de Humo).", ItemCategory.MaterialDeCrafteo, 0), quantity = 1 },
				new InventorySlot { item = ResolveOrCreateCanonicalRewardItem("Chatarra Afilada", "Clavos oxidados y láminas cortantes. Se combina con Pólvora Callejera (Granada de Metralla) o con Cinta Canela (Parche Blindado).", ItemCategory.MaterialDeCrafteo, 0), quantity = 1 },
				new InventorySlot { item = ResolveOrCreateCanonicalRewardItem("Cinta Canela", "Rollo de cinta adhesiva industrial. Combinada con una Botella Vacia crea un Vendaje Rudo (+45 HP).", ItemCategory.MaterialDeCrafteo, 0), quantity = 1 },
				new InventorySlot { item = ResolveOrCreateCanonicalRewardItem("Refresco Frio", "Refresco bien helado. Restaura +35 HP o sirve para destilar Tónico de Adrenalina con Alcohol Industrial.", ItemCategory.ConsumibleCuracion, 35), quantity = 1 }
			};
		}
		else if (currentRewardBundle == null || currentRewardBundle.Count == 0)
		{
			currentRewardBundle = GenerateLootDropsFromDefeatedEnemies();
		}

		int bonusDetectiveAmmoDropped = 0;
		if (hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null && activeEnemies != null && activeEnemies.Count > 0)
		{
			bonusDetectiveAmmoDropped = detectiveStats.ReloadAmmo(Mathf.Clamp(activeEnemies.Count, 1, 3));
		}

		if (currentRewardBundle != null && currentRewardBundle.Count > 0)
		{
			List<string> grantedSummaryParts = new List<string>();
			foreach (InventorySlot rewardSlot in currentRewardBundle)
			{
				if (rewardSlot == null || (Object)(object)rewardSlot.item == (Object)null || rewardSlot.quantity <= 0)
				{
					continue;
				}
				if ((Object)(object)InventoryManager.Instance != (Object)null)
				{
					InventoryManager.Instance.AddItem(rewardSlot.item, rewardSlot.quantity);
				}
				grantedSummaryParts.Add($"{rewardSlot.quantity}x {rewardSlot.item.itemName}");
			}
			if (bonusDetectiveAmmoDropped > 0)
			{
				grantedSummaryParts.Add($"+{bonusDetectiveAmmoDropped} Balas .38 Especial (Detective)");
			}
			if (grantedSummaryParts.Count > 0)
			{
				if ((Object)(object)MusicManager.Instance != (Object)null)
				{
					MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
					MusicManager.Instance.PlayRecogerObjetoSfx();
				}
				string summaryJoined = string.Join(", ", grantedSummaryParts);
				string[] bundleDialogue;
				if (!shouldAwardSingleItem && array != null && array.Length > 0)
				{
					bundleDialogue = array;
				}
				else if (isLevel1FinalBoss)
				{
					bundleDialogue = new string[3]
					{
						$"NARRADOR: \"Al caer los dos matones de La Sombra, registras sus mochilas antes de que lleguen refuerzos. Obtienes suministros clave para el siguiente nivel: {summaryJoined}.\"",
						"VOZ [INTELECTO]: \"¡Excelente hallazgo! Ahora podemos fabricar en combate BOMBAS MOLOTOV, VENDAJES RUDOS, GRANADAS DE METRALLA (Pólvora + Chatarra), BOMBAS CEGADORAS DE HUMO (Pólvora + Gasolina), TÓNICOS DE ADRENALINA (Alcohol + Refresco) y PARCHES BLINDADOS (Cinta + Chatarra).\"",
						"ENRIQUE BOSQUES: \"Guardo todo en mi abrigo. En el callejón nos esperan más sicarios y estos materiales nos darán ventaja táctica.\""
					};
				}
				else
				{
					string enemyCountText = (activeEnemies != null && activeEnemies.Count > 1) ? "los enemigos derrotados" : "el enemigo derrotado";
					bundleDialogue = new string[2]
					{
						$"NARRADOR: \"Registras a {enemyCountText} y recoges los objetos que soltaron al caer: {summaryJoined}.\"",
						"VOZ [INTELECTO]: \"Materiales añadidos al inventario. Recuerda abrir [INTELECTO] en combate para combinarlos en Molotovs, Granadas de Metralla, Bombas Cegadoras, Tónicos de Adrenalina, Parches Blindados o Vendajes Rudos.\""
					};
				}
				bool bundleDialogueDone = false;
				StartCombatDialogue(bundleDialogue, () =>
				{
					bundleDialogueDone = true;
				});
				while (!bundleDialogueDone)
				{
					yield return null;
				}
				if ((Object)(object)PlayerInventoryAndWeaponsUI.Instance != (Object)null)
				{
					PlayerInventoryAndWeaponsUI.Instance.RefreshUI();
				}
			}
		}

		if (levelsGained > 0 && (Object)(object)playerStats != (Object)null)
		{
			playerStats.RestoreFullHealth();
			UpdateHealthUI();
			PlaySubidaDeNivelSfx();
			string[] lines = new string[2]
			{
				$"NARRADOR: \"¡VICTORIA! Has ganado +{xpAwarded} EXP. ¡ENRIQUE BOSQUES SUBE AL NIVEL {playerStats.level} Y RECUPERA TODA SU VIDA ({playerStats.currentHP}/{playerStats.maxHP} HP)!\"",
				"ENRIQUE BOSQUES: \"Mis heridas se cierran y las cuatro voces cobran más fuerza en mi mente... Debo elegir cuál de mis habilidades se volverá más poderosa.\""
			};
			bool dialogueFinished = false;
			StartCombatDialogue(lines, () =>
			{
				dialogueFinished = true;
			});
			while (!dialogueFinished)
			{
				yield return null;
			}
			state = BattleState.WON;
		}
		if (hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null)
		{
			List<string> detLines = new List<string>();
			if (detectiveStats.IsAlive)
			{
				if (lastDetectiveAutoLevelUpLog != null && lastDetectiveAutoLevelUpLog.Count > 0)
				{
					PlaySubidaDeNivelSfx();
					detLines.Add($"NARRADOR: \"[¡VICTORIA DEL GRUPO! El Detective Vargas gana +{lastDetectiveExpGained} EXP y SUBE AL NIVEL {detectiveStats.Nivel}]:\"");
					foreach (string logEntry in lastDetectiveAutoLevelUpLog)
					{
						detLines.Add("DETECTIVE VARGAS: \"" + logEntry + "\"");
					}
				}
				else
				{
					detLines.Add($"NARRADOR: \"¡VICTORIA DEL GRUPO! Los atacantes han sido neutralizados (+{xpAwarded} EXP repartidos).\"");
					detLines.Add($"DETECTIVE VARGAS: \"Buen trabajo en equipo, Bosques (+{lastDetectiveExpGained} EXP). La zona está despejada; me quedan {detectiveStats.municionActual} balas en el tambor.\"");
				}
			}
			else
			{
				detLines.Add($"NARRADOR: \"¡VICTORIA! Los atacantes cayeron (+{xpAwarded} EXP), pero el Detective Vargas quedó inconsciente. Se reanimará con 1 HP en el siguiente combate.\"");
				detLines.Add("ENRIQUE BOSQUES: \"Tengo que cubrirlo y atender sus heridas antes de avanzar al fondo del callejón.\"");
			}

			bool detDialogueFinished = false;
			StartCombatDialogue(detLines.ToArray(), () =>
			{
				detDialogueFinished = true;
			});
			while (!detDialogueFinished)
			{
				yield return null;
			}
			state = BattleState.WON;
		}
		PlayerLevelUpAndStatsUI playerLevelUpAndStatsUI = PlayerLevelUpAndStatsUI.EnsureInScene();
		if ((Object)(object)playerLevelUpAndStatsUI != (Object)null)
		{
			bool finishedPostCombatMenu = false;
			playerLevelUpAndStatsUI.OpenForCombatLevelUp(playerStats, levelsGained, xpAwarded, () =>
			{
				finishedPostCombatMenu = true;
			});
			while (!finishedPostCombatMenu)
			{
				yield return null;
			}
		}
		if (hasDetectiveInParty && (Object)(object)detectiveStats != (Object)null)
		{
			DetectiveLevelUpAndStatsUI detUI = DetectiveLevelUpAndStatsUI.EnsureInScene();
			if ((Object)(object)detUI != (Object)null)
			{
				bool finishedDetMenu = false;
				detUI.OpenForCombatReport(detectiveStats, lastDetectiveLevelsGained, lastDetectiveExpGained, lastDetectiveAutoLevelUpLog, () =>
				{
					finishedDetMenu = true;
				});
				while (!finishedDetMenu)
				{
					yield return null;
				}
			}
		}
		if (isCurrentEncounterBoss && !string.IsNullOrEmpty(currentBossNextScene))
		{
			yield return ((MonoBehaviour)this).StartCoroutine(ExecuteBossVictoryAndSceneTransition());
		}
		else
		{
			yield return ((MonoBehaviour)this).StartCoroutine(CloseBattlePanelAfterDelay(0.15f, enablePlayerMovement: true));
		}
	}

	private IEnumerator ExecuteBossVictoryAndSceneTransition()
	{
		yield return (object)new WaitForSeconds(1.2f);
		if ((Object)(object)battlePanel != (Object)null)
		{
			battlePanel.SetActive(false);
		}
		if ((Object)(object)actionGifPanel != (Object)null)
		{
			actionGifPanel.SetActive(false);
		}
		if ((Object)(object)combatDialoguePanel != (Object)null)
		{
			combatDialoguePanel.SetActive(false);
		}
		HideIntelectoPanelSilent();
		if ((Object)(object)bossVictoryTransitionUI == (Object)null)
		{
			bossVictoryTransitionUI = BossVictoryTransitionUI.Instance;
			if ((Object)(object)bossVictoryTransitionUI == (Object)null)
			{
				bossVictoryTransitionUI = BossVictoryTransitionUI.EnsureInScene();
			}
		}
		if ((Object)(object)bossVictoryTransitionUI != (Object)null)
		{
			bossVictoryTransitionUI.StartTransition(currentBossVictoryNarrative, currentBossNextScene);
		}
		else
		{
			if ((Object)(object)playerStats != (Object)null) playerStats.SaveProgressionSnapshot();
			if ((Object)(object)detectiveStats != (Object)null) detectiveStats.SaveProgressionSnapshot();
			if ((Object)(object)InventoryManager.Instance != (Object)null) InventoryManager.Instance.SaveSnapshot();
			SceneManager.LoadScene(currentBossNextScene);
		}
	}

	public void RetryFromGameOver()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		PlayBotonEnCombateSfx();
		if ((Object)(object)gameOverPanel != (Object)null)
		{
			gameOverPanel.SetActive(false);
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.IsValid() && (activeScene.name.Contains("01_Nivel1") || activeScene.name.Contains("Intro")))
		{
			PlayerStats.ResetSavedProgression();
			InventoryManager.ResetSavedInventory();
			DetectiveStats.ResetSavedProgression();
			ResetStarterWeaponAwarded();
		}
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayExplorationMusic();
		}
		if (!string.IsNullOrEmpty(activeScene.name))
		{
			SceneManager.LoadScene(activeScene.name);
		}
	}

	public void ReturnToMainMenu()
	{
		PlayBotonEnCombateSfx();
		if ((Object)(object)gameOverPanel != (Object)null)
		{
			gameOverPanel.SetActive(false);
		}
		PlayerStats.ResetSavedProgression();
		InventoryManager.ResetSavedInventory();
		DetectiveStats.ResetSavedProgression();
		ResetStarterWeaponAwarded();
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayMenuMusic();
		}
		if (!string.IsNullOrWhiteSpace(menuSceneName))
		{
			SceneManager.LoadScene(menuSceneName);
		}
	}

	private IEnumerator CloseBattlePanelAfterDelay(float delay, bool enablePlayerMovement)
	{
		yield return (object)new WaitForSeconds(delay);
		if ((Object)(object)battlePanel != (Object)null)
		{
			battlePanel.SetActive(false);
		}
		if ((Object)(object)actionGifPanel != (Object)null)
		{
			actionGifPanel.SetActive(false);
		}
		if ((Object)(object)combatDialoguePanel != (Object)null)
		{
			combatDialoguePanel.SetActive(false);
		}
		HideIntelectoPanelSilent();
		state = BattleState.INACTIVE;
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayExplorationMusic();
		}
		if (enablePlayerMovement)
		{
			if ((Object)(object)playerStats != (Object)null && !playerStats.IsAlive)
			{
				playerStats.RestoreFullHealth();
			}
			if ((Object)(object)playerController != (Object)null)
			{
				playerController.SetCanMove(value: true);
			}
			if ((Object)(object)playerMovementScript != (Object)null)
			{
				((Behaviour)playerMovementScript).enabled = true;
			}
		}
	}

	public void SetupEnemyVisuals()
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		if (enemySlots == null || enemySlots.Length == 0)
		{
			return;
		}
		int count = activeEnemies.Count;
		for (int i = 0; i < enemySlots.Length; i++)
		{
			EnemyUISlot enemyUISlot = enemySlots[i];
			if (enemyUISlot == null || (Object)(object)enemyUISlot.slotRoot == (Object)null)
			{
				continue;
			}
			if (i < count)
			{
				enemyUISlot.slotRoot.SetActive(true);
				RectTransform component = enemyUISlot.slotRoot.GetComponent<RectTransform>();
				if (!((Object)(object)component != (Object)null))
				{
					continue;
				}
				switch (count)
				{
				case 1:
					component.anchorMin = new Vector2(0.29f, 0.44f);
					component.anchorMax = new Vector2(0.71f, 0.94f);
					break;
				case 2:
					if (i == 0)
					{
						component.anchorMin = new Vector2(0.1f, 0.44f);
						component.anchorMax = new Vector2(0.5f, 0.94f);
					}
					else
					{
						component.anchorMin = new Vector2(0.52f, 0.44f);
						component.anchorMax = new Vector2(0.92f, 0.94f);
					}
					break;
				}
			}
			else
			{
				enemyUISlot.slotRoot.SetActive(false);
			}
		}
	}

	public void SetupCombatPortraitAndWeaponUI()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bd: Expected Obj, but got Unknown
		//IL_04ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0504: Unknown result type (might be due to invalid IL or missing references)
		//IL_050f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0519: Unknown result type (might be due to invalid IL or missing references)
		//IL_053b: Unknown result type (might be due to invalid IL or missing references)
		//IL_056a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0571: Expected Obj, but got Unknown
		//IL_059b: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0638: Unknown result type (might be due to invalid IL or missing references)
		//IL_0642: Expected Obj, but got Unknown
		//IL_0667: Unknown result type (might be due to invalid IL or missing references)
		//IL_066e: Expected Obj, but got Unknown
		//IL_068a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0695: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Expected Obj, but got Unknown
		//IL_071f: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Expected Obj, but got Unknown
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Expected Obj, but got Unknown
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_038f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Expected Obj, but got Unknown
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		LoadPortraitsIfMissing();
		if ((Object)(object)playerCombatPortraitImage != (Object)null)
		{
			if ((Object)(object)enriquePortrait != (Object)null && ((Object)(object)playerCombatPortraitImage.sprite == (Object)null || state != BattleState.DETECTIVETURN))
			{
				playerCombatPortraitImage.sprite = enriquePortrait;
				((Graphic)playerCombatPortraitImage).color = Color.white;
				playerCombatPortraitImage.preserveAspect = false;
			}
			if ((Object)(object)combatPortraitFrameBorder == (Object)null && (Object)(object)((Component)playerCombatPortraitImage).transform.parent != (Object)null)
			{
				combatPortraitFrameBorder = ((Component)((Component)playerCombatPortraitImage).transform.parent).GetComponent<Image>();
			}
			combatPortraitClickButton = ((Component)playerCombatPortraitImage).GetComponent<Button>();
			if ((Object)(object)combatPortraitClickButton == (Object)null)
			{
				combatPortraitClickButton = ((Component)playerCombatPortraitImage).gameObject.AddComponent<Button>();
				((Selectable)combatPortraitClickButton).targetGraphic = (Graphic)(object)playerCombatPortraitImage;
			}
			((UnityEventBase)combatPortraitClickButton.onClick).RemoveAllListeners();
			((UnityEvent)combatPortraitClickButton.onClick).AddListener((UnityAction)(() =>
			{
				if (state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN)
				{
					PlayBotonEnCombateSfx();
					PlayerInventoryAndWeaponsUI playerInventoryAndWeaponsUI = PlayerInventoryAndWeaponsUI.EnsureInScene();
					if ((Object)(object)playerInventoryAndWeaponsUI != (Object)null)
					{
						playerInventoryAndWeaponsUI.OpenInventoryMenu();
					}
				}
			}));
			if ((Object)(object)combatWeaponBadgeRoot == (Object)null)
			{
				Transform val = ((Component)playerCombatPortraitImage).transform.Find("CombatWeaponBadgeBanner");
				if ((Object)(object)val != (Object)null)
				{
					combatWeaponBadgeRoot = ((Component)val).gameObject;
					combatWeaponBadgeText = combatWeaponBadgeRoot.GetComponentInChildren<TextMeshProUGUI>();
				}
				else
				{
					combatWeaponBadgeRoot = new GameObject("CombatWeaponBadgeBanner", new Type[2]
					{
						typeof(RectTransform),
						typeof(Image)
					});
					combatWeaponBadgeRoot.transform.SetParent(((Component)playerCombatPortraitImage).transform, false);
					RectTransform component = combatWeaponBadgeRoot.GetComponent<RectTransform>();
					component.anchorMin = new Vector2(0f, 0f);
					component.anchorMax = new Vector2(1f, 0.28f);
					component.offsetMin = Vector2.zero;
					component.offsetMax = Vector2.zero;
					((Graphic)combatWeaponBadgeRoot.GetComponent<Image>()).color = new Color(0.06f, 0.07f, 0.1f, 0.92f);
					GameObject val2 = new GameObject("WeaponBadgeText", new Type[2]
					{
						typeof(RectTransform),
						typeof(TextMeshProUGUI)
					});
					val2.transform.SetParent(combatWeaponBadgeRoot.transform, false);
					RectTransform component2 = val2.GetComponent<RectTransform>();
					component2.anchorMin = Vector2.zero;
					component2.anchorMax = Vector2.one;
					component2.offsetMin = new Vector2(4f, 2f);
					component2.offsetMax = new Vector2(-4f, -2f);
					combatWeaponBadgeText = val2.GetComponent<TextMeshProUGUI>();
					if ((Object)(object)combatDialogueText != (Object)null && (Object)(object)((TMP_Text)combatDialogueText).font != (Object)null)
					{
						((TMP_Text)combatWeaponBadgeText).font = ((TMP_Text)combatDialogueText).font;
					}
					((TMP_Text)combatWeaponBadgeText).fontSize = 13f;
					((TMP_Text)combatWeaponBadgeText).alignment = (TextAlignmentOptions)514;
					((Graphic)combatWeaponBadgeText).color = Color.white;
					((TMP_Text)combatWeaponBadgeText).textWrappingMode = (TextWrappingModes)1;
				}
			}
			if ((Object)(object)combatWeaponBadgeIcon == (Object)null)
			{
				Transform val3 = ((Component)playerCombatPortraitImage).transform.Find("CombatWeaponCornerIcon");
				if ((Object)(object)val3 != (Object)null)
				{
					combatWeaponBadgeIcon = ((Component)val3).GetComponent<Image>();
				}
				else
				{
					GameObject val4 = new GameObject("CombatWeaponCornerIcon", new Type[2]
					{
						typeof(RectTransform),
						typeof(Image)
					});
					val4.transform.SetParent(((Component)playerCombatPortraitImage).transform, false);
					RectTransform component3 = val4.GetComponent<RectTransform>();
					component3.anchorMin = new Vector2(0.68f, 0.68f);
					component3.anchorMax = new Vector2(0.98f, 0.98f);
					component3.offsetMin = Vector2.zero;
					component3.offsetMax = Vector2.zero;
					combatWeaponBadgeIcon = val4.GetComponent<Image>();
					combatWeaponBadgeIcon.preserveAspect = true;
				}
			}
		}
		if (!((Object)(object)battlePanel != (Object)null))
		{
			return;
		}
		if ((Object)(object)combatInventoryButtonRoot == (Object)null)
		{
			Transform val5 = battlePanel.transform.Find("Combat_InventoryAndWeaponsButton");
			if ((Object)(object)val5 != (Object)null)
			{
				combatInventoryButtonRoot = ((Component)val5).gameObject;
				combatInventoryButton = combatInventoryButtonRoot.GetComponent<Button>();
				combatInventoryButtonLabel = combatInventoryButtonRoot.GetComponentInChildren<TextMeshProUGUI>();
				combatInventoryButtonBorder = combatInventoryButtonRoot.GetComponent<Image>();
			}
		}
		if ((Object)(object)combatInventoryButtonRoot != (Object)null)
		{
			if ((Object)(object)combatInventoryButton == (Object)null)
			{
				combatInventoryButton = combatInventoryButtonRoot.GetComponent<Button>();
			}
			if ((Object)(object)combatInventoryButton != (Object)null)
			{
				((UnityEventBase)combatInventoryButton.onClick).RemoveAllListeners();
				((UnityEvent)combatInventoryButton.onClick).AddListener((UnityAction)(() =>
				{
					if (state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN)
					{
						PlayBotonEnCombateSfx();
						PlayerInventoryAndWeaponsUI playerInventoryAndWeaponsUI = PlayerInventoryAndWeaponsUI.EnsureInScene();
						if ((Object)(object)playerInventoryAndWeaponsUI != (Object)null)
						{
							playerInventoryAndWeaponsUI.OpenInventoryMenu();
						}
					}
				}));
			}
			return;
		}
		combatInventoryButtonRoot = new GameObject("Combat_InventoryAndWeaponsButton", new Type[2]
		{
			typeof(RectTransform),
			typeof(Image)
		});
		combatInventoryButtonRoot.transform.SetParent(battlePanel.transform, false);
		RectTransform component4 = combatInventoryButtonRoot.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0.02f, 0.445f);
		component4.anchorMax = new Vector2(0.175f, 0.505f);
		component4.offsetMin = Vector2.zero;
		component4.offsetMax = Vector2.zero;
		combatInventoryButtonBorder = combatInventoryButtonRoot.GetComponent<Image>();
		((Graphic)combatInventoryButtonBorder).color = selectedTargetBorderColor;
		GameObject val6 = new GameObject("Inner", new Type[2]
		{
			typeof(RectTransform),
			typeof(Image)
		});
		val6.transform.SetParent(combatInventoryButtonRoot.transform, false);
		RectTransform component5 = val6.GetComponent<RectTransform>();
		component5.anchorMin = new Vector2(0.02f, 0.08f);
		component5.anchorMax = new Vector2(0.98f, 0.92f);
		component5.offsetMin = Vector2.zero;
		component5.offsetMax = Vector2.zero;
		Image component6 = val6.GetComponent<Image>();
		((Graphic)component6).color = new Color(0.06f, 0.08f, 0.11f, 0.95f);
		combatInventoryButton = combatInventoryButtonRoot.AddComponent<Button>();
		((Selectable)combatInventoryButton).targetGraphic = (Graphic)(object)component6;
		((UnityEventBase)combatInventoryButton.onClick).RemoveAllListeners();
		((UnityEvent)combatInventoryButton.onClick).AddListener((UnityAction)(() =>
		{
			if (state == BattleState.PLAYERTURN || state == BattleState.DETECTIVETURN)
			{
				PlayBotonEnCombateSfx();
				PlayerInventoryAndWeaponsUI playerInventoryAndWeaponsUI = PlayerInventoryAndWeaponsUI.EnsureInScene();
				if ((Object)(object)playerInventoryAndWeaponsUI != (Object)null)
				{
					playerInventoryAndWeaponsUI.OpenInventoryMenu();
				}
			}
		}));
		GameObject val7 = new GameObject("Label", new Type[2]
		{
			typeof(RectTransform),
			typeof(TextMeshProUGUI)
		});
		val7.transform.SetParent(val6.transform, false);
		RectTransform component7 = val7.GetComponent<RectTransform>();
		component7.anchorMin = Vector2.zero;
		component7.anchorMax = Vector2.one;
		component7.offsetMin = Vector2.zero;
		component7.offsetMax = Vector2.zero;
		combatInventoryButtonLabel = val7.GetComponent<TextMeshProUGUI>();
		if ((Object)(object)combatDialogueText != (Object)null && (Object)(object)((TMP_Text)combatDialogueText).font != (Object)null)
		{
			((TMP_Text)combatInventoryButtonLabel).font = ((TMP_Text)combatDialogueText).font;
		}
		((TMP_Text)combatInventoryButtonLabel).fontSize = 16f;
		((TMP_Text)combatInventoryButtonLabel).alignment = (TextAlignmentOptions)514;
		((Graphic)combatInventoryButtonLabel).color = selectedTargetBorderColor;
		((TMP_Text)combatInventoryButtonLabel).text = "INVENTARIO";
		((TMP_Text)combatInventoryButtonLabel).textWrappingMode = (TextWrappingModes)1;
		combatInventoryButtonRoot.SetActive(false);
	}

	private bool WasToggleInventoryKeyPressed()
	{
		if (Keyboard.current != null && (((ButtonControl)Keyboard.current.iKey).wasPressedThisFrame || ((ButtonControl)Keyboard.current.bKey).wasPressedThisFrame))
		{
			return true;
		}
		return false;
	}

	public void UpdateHealthUI()
	{
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0407: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_0468: Unknown result type (might be due to invalid IL or missing references)
		//IL_0460: Unknown result type (might be due to invalid IL or missing references)
		//IL_0503: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_057c: Unknown result type (might be due to invalid IL or missing references)
		//IL_053e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0543: Unknown result type (might be due to invalid IL or missing references)
		//IL_0558: Unknown result type (might be due to invalid IL or missing references)
		//IL_066f: Unknown result type (might be due to invalid IL or missing references)
		//IL_061e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0603: Unknown result type (might be due to invalid IL or missing references)
		//IL_0817: Unknown result type (might be due to invalid IL or missing references)
		//IL_0809: Unknown result type (might be due to invalid IL or missing references)
		//IL_0798: Unknown result type (might be due to invalid IL or missing references)
		//IL_0791: Unknown result type (might be due to invalid IL or missing references)
		//IL_0722: Unknown result type (might be due to invalid IL or missing references)
		EnsureCombatGreenHealthBar();
		ItemData itemData = (((Object)(object)InventoryManager.Instance != (Object)null) ? InventoryManager.Instance.equippedWeapon : null);
		int num = (((Object)(object)playerStats != (Object)null) ? playerStats.GetEquippedWeaponAttackBonus() : 0);
		int num2 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetTotalPhysicalAttack() : 12);
		int num3 = (((Object)(object)playerStats != (Object)null) ? playerStats.corporeo : 12);

		bool showDetective = (state == BattleState.DETECTIVETURN) || 
		                     (state == BattleState.ENEMYTURN && lastEnemyTargetWasDetective && (Object)(object)detectiveStats != (Object)null && detectiveStats.IsAlive);

		if (showDetective && (Object)(object)detectiveStats != (Object)null)
		{
			// Retrato del Detective Vargas
			if ((Object)(object)playerCombatPortraitImage != (Object)null)
			{
				EnsureDetectiveCombatPortraitResolved();
				playerCombatPortraitImage.sprite = (((Object)(object)detectiveCombatPortrait != (Object)null) ? detectiveCombatPortrait : enriquePortrait);
				((Graphic)playerCombatPortraitImage).color = Color.white;
			}

			// Barra de Vida del Detective Vargas
			if ((Object)(object)playerHealthBarUI != (Object)null)
			{
				playerHealthBarUI.healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);
				playerHealthBarUI.percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);
				playerHealthBarUI.SetHealthInstant(detectiveStats.HP_Actual, detectiveStats.HP_Max, detectiveStats.isDodgingUntilNextTurn, detectiveStats.tacticalAttackBonus, "HÉCTOR VARGAS");
			}
			if ((Object)(object)playerHPText != (Object)null)
			{
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)playerHPText);
				if (detectiveStats.HP_Actual <= 0)
				{
					((TMP_Text)playerHPText).text = $"HÉCTOR VARGAS <color=#FF5555>[CAÍDO / 0 HP]</color> | BALAS: {detectiveStats.municionActual}/{detectiveStats.municionMaxima}";
				}
				else
				{
					((TMP_Text)playerHPText).text = $"HÉCTOR VARGAS <color=#9FA6B2>(NV. {detectiveStats.Nivel})</color>   HP: <color=#FFFFFF>{detectiveStats.HP_Actual}</color> / {detectiveStats.HP_Max} | BALAS: {detectiveStats.municionActual}/{detectiveStats.municionMaxima}";
				}
			}
			if ((Object)(object)playerHPSlider != (Object)null)
			{
				playerHPSlider.maxValue = detectiveStats.HP_Max;
				playerHPSlider.value = detectiveStats.HP_Actual;
				if ((Object)(object)playerHPSlider.fillRect != (Object)null)
				{
					Image comp = ((Component)playerHPSlider.fillRect).GetComponent<Image>();
					if ((Object)(object)comp != (Object)null)
					{
						((Graphic)comp).color = new Color(0.22f, 0.85f, 0.38f, 1f);
					}
				}
			}

			// Badge de Arma del Detective
			if ((Object)(object)combatWeaponBadgeText != (Object)null)
			{
				((TMP_Text)combatWeaponBadgeText).text = $"<b>REVÓLVER .38</b>\n<size=12><color=#FFD84D>BALAS: {detectiveStats.balasActuales}/{detectiveStats.balasMaximas}</color> <color=#90CAF9>[INVESTIGADOR]</color></size>";
			}
			if ((Object)(object)combatWeaponBadgeIcon != (Object)null)
			{
				((Component)combatWeaponBadgeIcon).gameObject.SetActive(false);
			}
			if ((Object)(object)combatInventoryButtonRoot != (Object)null)
			{
				combatInventoryButtonRoot.SetActive(false);
			}

			// 4 Botones de Acción Unificados (Mismo diseño Noir y submenús para ambos protagonistas)
			// Botón 1: Corpóreo / Disparo .38
			if ((Object)(object)corporeoButton != (Object)null)
			{
				TextMeshProUGUI label = ((Component)corporeoButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)label != (Object)null)
				{
					if (detectiveStats.CanFireWeapon(out _))
					{
						((TMP_Text)label).text = $"CORPÓREO [REVÓLVER .38]\n<size=20>Disparo ({detectiveStats.balasActuales}/{detectiveStats.balasMaximas} Balas)</size>";
					}
					else
					{
						int meleeAtk = detectiveStats.GetTotalMeleeAttack();
						((TMP_Text)label).text = $"CORPÓREO [CULATAZO]\n<size=20>Atacar ({meleeAtk} ATQ)</size>";
					}
				}
				Image btnImg = ((Component)corporeoButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.78f, 0.22f, 0.22f, 1f);
			}

			// Botón 2: Intelecto (Objetos / Crear)
			if ((Object)(object)intelectoButton != (Object)null)
			{
				TextMeshProUGUI label = ((Component)intelectoButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)label != (Object)null)
				{
					((TMP_Text)label).text = "INTELECTO\n<size=20>Objetos / Crear</size>";
				}
				Image btnImg = ((Component)intelectoButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.22f, 0.52f, 0.85f, 1f);
			}

			// Botón 3: Motricidad (Guardia / Esquive)
			if ((Object)(object)motricidadButton != (Object)null)
			{
				TextMeshProUGUI label = ((Component)motricidadButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)label != (Object)null)
				{
					((TMP_Text)label).text = "MOTRICIDAD\n<size=20>Guardia / Esquive</size>";
				}
				Image btnImg = ((Component)motricidadButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.85f, 0.65f, 0.18f, 1f);
			}

			// Botón 4: Mundo Interno (Tácticas / Personalidades)
			if ((Object)(object)mundoInternoButton != (Object)null)
			{
				TextMeshProUGUI label = ((Component)mundoInternoButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)label != (Object)null)
				{
					((TMP_Text)label).text = "MUNDO INTERNO\n<size=20>Tácticas / Mente</size>";
				}
				Image btnImg = ((Component)mundoInternoButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.58f, 0.26f, 0.78f, 1f);
			}
		}
		else
		{
			// Retrato de ENRIQUE
			if ((Object)(object)playerCombatPortraitImage != (Object)null)
			{
				playerCombatPortraitImage.sprite = enriquePortrait;
				((Graphic)playerCombatPortraitImage).color = Color.white;
			}

			// Barra de Vida de Enrique
			if ((Object)(object)playerStats != (Object)null)
			{
				if ((Object)(object)playerHealthBarUI != (Object)null)
				{
					playerHealthBarUI.healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);
					playerHealthBarUI.percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);
					playerHealthBarUI.SetHealthInstant(playerStats.currentHP, playerStats.maxHP, isMotricidadDefending, playerAttackBuff, "ENRIQUE");
				}
				if ((Object)(object)playerHPText != (Object)null)
				{
					PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)playerHPText);
					((TMP_Text)playerHPText).text = $"ENRIQUE <color=#9FA6B2>(NV. {playerStats.level})</color>   HP: <color=#FFFFFF>{playerStats.currentHP}</color> / {playerStats.maxHP}";
				}
				if ((Object)(object)playerHPSlider != (Object)null)
				{
					playerHPSlider.maxValue = playerStats.maxHP;
					playerHPSlider.value = playerStats.currentHP;
					if ((Object)(object)playerHPSlider.fillRect != (Object)null)
					{
						Image component = ((Component)playerHPSlider.fillRect).GetComponent<Image>();
						if ((Object)(object)component != (Object)null)
						{
							((Graphic)component).color = new Color(0.22f, 0.85f, 0.38f, 1f);
						}
					}
				}
			}

			// Badge de Arma de Enrique
			if ((Object)(object)combatWeaponBadgeText != (Object)null)
			{
				if ((Object)(object)itemData != (Object)null)
				{
					((TMP_Text)combatWeaponBadgeText).text = $"<b>{itemData.itemName.ToUpper()}</b>\n<size=12><color=#66FF88>+{itemData.weaponAttackBonus} ATQ</color> <color=#FFD84D>[CAMBIAR]</color></size>";
				}
				else
				{
					((TMP_Text)combatWeaponBadgeText).text = "<b>PUÑOS DESNUDOS</b>\n<size=12><color=#B0B6C2>12 ATQ</color> <color=#66CCFF>[EQUIPAR]</color></size>";
				}
			}
			if ((Object)(object)combatWeaponBadgeIcon != (Object)null)
			{
				if ((Object)(object)itemData != (Object)null && (Object)(object)itemData.icon != (Object)null)
				{
					((Component)combatWeaponBadgeIcon).gameObject.SetActive(true);
					combatWeaponBadgeIcon.sprite = itemData.icon;
					((Graphic)combatWeaponBadgeIcon).color = Color.white;
				}
				else
				{
					((Component)combatWeaponBadgeIcon).gameObject.SetActive(false);
				}
			}
			if ((Object)(object)combatInventoryButtonRoot != (Object)null)
			{
				combatInventoryButtonRoot.SetActive(false);
			}

			// 4 Botones de Acción para Enrique (Voces)
			// Botón 1: Corpóreo
			if ((Object)(object)corporeoButton != (Object)null)
			{
				TextMeshProUGUI componentInChildren = ((Component)corporeoButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)componentInChildren != (Object)null)
				{
					if ((Object)(object)itemData != (Object)null)
					{
						((TMP_Text)componentInChildren).text = $"CORPÓREO [{itemData.itemName.ToUpper()}]\n<size=20>Atacar ({num3}+{num} = {num2} ATQ)</size>";
					}
					else
					{
						((TMP_Text)componentInChildren).text = $"CORPÓREO\n<size=20>Atacar (Puños: {num2} ATQ)</size>";
					}
				}
				Image btnImg = ((Component)corporeoButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.78f, 0.22f, 0.22f, 1f);
			}

			// Botón 2: Intelecto
			if ((Object)(object)intelectoButton != (Object)null)
			{
				TextMeshProUGUI label = ((Component)intelectoButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)label != (Object)null)
				{
					((TMP_Text)label).text = "INTELECTO\n<size=20>Objetos / Crear</size>";
				}
				Image btnImg = ((Component)intelectoButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.22f, 0.52f, 0.85f, 1f);
			}

			// Botón 3: Motricidad
			if ((Object)(object)motricidadButton != (Object)null)
			{
				TextMeshProUGUI label = ((Component)motricidadButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)label != (Object)null)
				{
					((TMP_Text)label).text = "MOTRICIDAD\n<size=20>Guardia / Esquive</size>";
				}
				Image btnImg = ((Component)motricidadButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.85f, 0.65f, 0.18f, 1f);
			}

			// Botón 4: Mundo Interno
			if ((Object)(object)mundoInternoButton != (Object)null)
			{
				TextMeshProUGUI label = ((Component)mundoInternoButton).GetComponentInChildren<TextMeshProUGUI>();
				if ((Object)(object)label != (Object)null)
				{
					((TMP_Text)label).text = "MUNDO INTERNO\n<size=20>Personalidades</size>";
				}
				Image btnImg = ((Component)mundoInternoButton).GetComponent<Image>();
				if ((Object)(object)btnImg != (Object)null) ((Graphic)btnImg).color = new Color(0.58f, 0.26f, 0.78f, 1f);
			}
		}

		if ((Object)(object)combatPortraitFrameBorder != (Object)null)
		{
			((Graphic)combatPortraitFrameBorder).color = (((Object)(object)itemData != (Object)null) ? selectedTargetBorderColor : normalEnemyBorderColor);
		}
		if ((Object)(object)combatInventoryButtonLabel != (Object)null)
		{
			((TMP_Text)combatInventoryButtonLabel).text = "INVENTARIO";
		}
		if ((Object)(object)combatInventoryButtonBorder != (Object)null)
		{
			((Graphic)combatInventoryButtonBorder).color = (Color)(((Object)(object)itemData != (Object)null) ? selectedTargetBorderColor : new Color(0.32f, 0.62f, 0.78f, 1f));
		}
		if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			if ((Object)(object)detectiveActionMenu.menuRootPanel != (Object)null)
			{
				detectiveActionMenu.menuRootPanel.SetActive(false);
			}
			detectiveActionMenu.RefreshAllySlotUI(showSlot: hasDetectiveInParty);
		}
		if (enemySlots == null)
		{
			return;
		}
		for (int i = 0; i < enemySlots.Length; i++)
		{
			EnemyUISlot enemyUISlot = enemySlots[i];
			if (enemyUISlot == null || (Object)(object)enemyUISlot.slotRoot == (Object)null || i >= activeEnemies.Count)
			{
				continue;
			}
			CombatEnemyInstance combatEnemyInstance = activeEnemies[i];
			bool flag = i == selectedTargetIndex && combatEnemyInstance.IsAlive;
			if ((Object)(object)enemyUISlot.borderImage != (Object)null)
			{
				((Graphic)enemyUISlot.borderImage).color = (flag ? selectedTargetBorderColor : normalEnemyBorderColor);
			}
			if ((Object)(object)enemyUISlot.targetIndicator != (Object)null)
			{
				enemyUISlot.targetIndicator.SetActive(flag);
			}
			if ((Object)(object)enemyUISlot.groundIndicatorRing != (Object)null)
			{
				if (flag)
				{
					Color color = selectedTargetBorderColor;
					color.a = 0.5f;
					((Graphic)enemyUISlot.groundIndicatorRing).color = color;
				}
				else
				{
					((Graphic)enemyUISlot.groundIndicatorRing).color = new Color(0f, 0f, 0f, 0.3f);
				}
			}
			if ((Object)(object)enemyUISlot.enemyNameText != (Object)null)
			{
				string arg3 = (flag ? "> [OBJETIVO] " : "");
				((TMP_Text)enemyUISlot.enemyNameText).text = $"{arg3}{combatEnemyInstance.data.enemyName.ToUpper()} (MOT: {combatEnemyInstance.data.motricidad})";
				((Graphic)enemyUISlot.enemyNameText).color = (flag ? new Color(0.96f, 0.78f, 0.32f, 1f) : new Color(0.88f, 0.86f, 0.82f, 1f));
			}
			if ((Object)(object)enemyUISlot.enemyHPText != (Object)null)
			{
				if (!combatEnemyInstance.IsAlive)
				{
					((TMP_Text)enemyUISlot.enemyHPText).text = "DERROTADO (0 HP)";
					((Graphic)enemyUISlot.enemyHPText).color = new Color(0.45f, 0.45f, 0.48f, 1f);
				}
				else
				{
					string text = (combatEnemyInstance.isConfused ? " [CONFUNDIDO]" : "");
					string text2 = ((combatEnemyInstance.attackDebuff > 0) ? $" [-{combatEnemyInstance.attackDebuff} ATQ]" : "");
					string text3 = ((combatEnemyInstance.defenseDebuff > 0) ? $" [-{combatEnemyInstance.defenseDebuff} DEF]" : "");
					string text4 = ((combatEnemyInstance.vulnerabilityTurns > 0) ? " [VULNERABLE +30%]" : "");
					string bossPhaseTag = (((Object)(object)activeLevel2BossController != (Object)null && IsBossEnemy(combatEnemyInstance)) ? activeLevel2BossController.GetPhaseStatusBadge() : "");
					((TMP_Text)enemyUISlot.enemyHPText).text = $"HP: {combatEnemyInstance.currentHP} / {combatEnemyInstance.data.maxHP}{text}{text2}{text3}{text4}{bossPhaseTag}";
					((Graphic)enemyUISlot.enemyHPText).color = new Color(0.85f, 0.35f, 0.35f, 1f);
				}
			}
			if (!((Object)(object)enemyUISlot.enemyPlaceholderImage != (Object)null))
			{
				continue;
			}
			Sprite currentEnemyDisplaySprite = GetCurrentEnemyDisplaySprite(combatEnemyInstance);
			if ((Object)(object)currentEnemyDisplaySprite != (Object)null)
			{
				enemyUISlot.enemyPlaceholderImage.sprite = currentEnemyDisplaySprite;
				enemyUISlot.enemyPlaceholderImage.preserveAspect = true;
				((Graphic)enemyUISlot.enemyPlaceholderImage).color = (Color)(combatEnemyInstance.IsAlive ? Color.white : new Color(0.55f, 0.55f, 0.58f, 1f));
				if ((Object)(object)enemyUISlot.enemyAvatarLabelText != (Object)null)
				{
					((Component)enemyUISlot.enemyAvatarLabelText).gameObject.SetActive(false);
				}
				continue;
			}
			enemyUISlot.enemyPlaceholderImage.sprite = null;
			enemyUISlot.enemyPlaceholderImage.preserveAspect = false;
			((Graphic)enemyUISlot.enemyPlaceholderImage).color = (Color)(combatEnemyInstance.IsAlive ? combatEnemyInstance.data.placeholderColor : new Color(0.15f, 0.15f, 0.17f, 1f));
			if ((Object)(object)enemyUISlot.enemyAvatarLabelText != (Object)null)
			{
				((Component)enemyUISlot.enemyAvatarLabelText).gameObject.SetActive(true);
				((TMP_Text)enemyUISlot.enemyAvatarLabelText).text = (combatEnemyInstance.IsAlive ? ("[CLIC PARA APUNTAR]\n" + combatEnemyInstance.data.enemyName.ToUpper()) : "[DERROTADO]");
			}
		}
	}

	public void EnsureCombatGreenHealthBar()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0301: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0480: Unknown result type (might be due to invalid IL or missing references)
		//IL_048c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0498: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_045b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0460: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_054b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0557: Unknown result type (might be due to invalid IL or missing references)
		//IL_0563: Unknown result type (might be due to invalid IL or missing references)
		//IL_056f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0526: Unknown result type (might be due to invalid IL or missing references)
		//IL_052b: Unknown result type (might be due to invalid IL or missing references)
		//IL_058e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0605: Unknown result type (might be due to invalid IL or missing references)
		//IL_0606: Unknown result type (might be due to invalid IL or missing references)
		//IL_060d: Unknown result type (might be due to invalid IL or missing references)
		//IL_060e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0629: Unknown result type (might be due to invalid IL or missing references)
		//IL_062e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)battlePanel == (Object)null)
		{
			return;
		}
		Color val = new Color(0.22f, 0.85f, 0.38f, 1f);
		Transform val2 = FindChildRecursive(battlePanel.transform, "PlayerStatusStrip");
		if (!((Object)(object)val2 != (Object)null))
		{
			return;
		}
		RectTransform component = ((Component)val2).GetComponent<RectTransform>();
		if ((Object)(object)component != (Object)null)
		{
			component.anchorMin = new Vector2(0.19f, 0.198f);
			component.anchorMax = new Vector2(0.97f, 0.282f);
			component.offsetMin = Vector2.zero;
			component.offsetMax = Vector2.zero;
		}
		Transform val3 = FindChildRecursive(val2, "PlayerHPText");
		TextMeshProUGUI val4 = null;
		if ((Object)(object)val3 != (Object)null)
		{
			RectTransform component2 = ((Component)val3).GetComponent<RectTransform>();
			if ((Object)(object)component2 != (Object)null && (Object)(object)val3.parent == (Object)(object)val2)
			{
				component2.anchorMin = new Vector2(0.015f, 0.48f);
				component2.anchorMax = new Vector2(0.68f, 0.96f);
				component2.offsetMin = Vector2.zero;
				component2.offsetMax = Vector2.zero;
			}
			val4 = ((Component)val3).GetComponent<TextMeshProUGUI>();
			if ((Object)(object)val4 != (Object)null)
			{
				((TMP_Text)val4).alignment = (TextAlignmentOptions)4097;
				((TMP_Text)val4).fontSize = 22f;
				((TMP_Text)val4).fontStyle = (FontStyles)0;
				((TMP_Text)val4).overflowMode = (TextOverflowModes)1;
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)val4);
			}
		}
		Transform val5 = FindChildRecursive(val2, "PlayerHPPctText");
		TextMeshProUGUI val6 = null;
		if ((Object)(object)val5 == (Object)null)
		{
			Transform headerRow = val2.Find("HeaderRow");
			Transform parentForPct = ((Object)(object)headerRow != (Object)null) ? headerRow : val2;
			GameObject val7 = new GameObject("PlayerHPPctText", new Type[2]
			{
				typeof(RectTransform),
				typeof(TextMeshProUGUI)
			});
			val7.transform.SetParent(parentForPct, false);
			val5 = val7.transform;
		}
		RectTransform component3 = ((Component)val5).GetComponent<RectTransform>();
		if ((Object)(object)component3 != (Object)null)
		{
			if ((Object)(object)val5.parent == (Object)(object)val2)
			{
				component3.anchorMin = new Vector2(0.68f, 0.48f);
				component3.anchorMax = new Vector2(0.985f, 0.96f);
			}
			else
			{
				component3.anchorMin = new Vector2(0.70f, 0f);
				component3.anchorMax = new Vector2(1f, 1f);
			}
			component3.offsetMin = Vector2.zero;
			component3.offsetMax = Vector2.zero;
		}
		val6 = ((Component)val5).GetComponent<TextMeshProUGUI>();
		if ((Object)(object)val6 != (Object)null)
		{
			((TMP_Text)val6).alignment = (TextAlignmentOptions)4100;
			((TMP_Text)val6).fontSize = 24f;
			((TMP_Text)val6).fontStyle = (FontStyles)0;
			((Graphic)val6).color = val;
			if ((Object)(object)((TMP_Text)val6).font == (Object)null)
			{
				((TMP_Text)val6).font = RuntimeAssetCatalog.GetTMPFont("Assets/Fonts/Pixelzone_TMP.asset");
			}
			if ((Object)(object)val4 != (Object)null && (Object)(object)((TMP_Text)val6).font == (Object)null)
			{
				((TMP_Text)val6).font = ((TMP_Text)val4).font;
			}
			PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)val6);
		}
		Transform val8 = val2.Find("HealthBarBorder");
		if ((Object)(object)val8 == (Object)null)
		{
			GameObject val9 = new GameObject("HealthBarBorder", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val9.transform.SetParent(val2, false);
			val8 = val9.transform;
		}
		RectTransform component4 = ((Component)val8).GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0.015f, 0.08f);
		component4.anchorMax = new Vector2(0.985f, 0.38f);
		component4.offsetMin = Vector2.zero;
		component4.offsetMax = Vector2.zero;
		Image component5 = ((Component)val8).GetComponent<Image>();
		if ((Object)(object)component5 != (Object)null)
		{
			((Graphic)component5).color = new Color(0.35f, 0.35f, 0.35f, 1f);
			((Graphic)component5).raycastTarget = false;
		}
		Transform val10 = val8.Find("HealthBarBackground");
		if ((Object)(object)val10 == (Object)null)
		{
			GameObject val11 = new GameObject("HealthBarBackground", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val11.transform.SetParent(val8, false);
			val10 = val11.transform;
		}
		RectTransform component6 = ((Component)val10).GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0.002f, 0.08f);
		component6.anchorMax = new Vector2(0.998f, 0.92f);
		component6.offsetMin = Vector2.zero;
		component6.offsetMax = Vector2.zero;
		Image component7 = ((Component)val10).GetComponent<Image>();
		if ((Object)(object)component7 != (Object)null)
		{
			((Graphic)component7).color = new Color(0.08f, 0.08f, 0.08f, 1f);
			((Graphic)component7).raycastTarget = false;
		}
		Transform val12 = val10.Find("DamageFill");
		bool isNewDamageFill = false;
		if ((Object)(object)val12 == (Object)null)
		{
			GameObject val13 = new GameObject("DamageFill", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val13.transform.SetParent(val10, false);
			val12 = val13.transform;
			isNewDamageFill = true;
		}
		RectTransform component8 = ((Component)val12).GetComponent<RectTransform>();
		if (isNewDamageFill)
		{
			component8.anchorMin = Vector2.zero;
			component8.anchorMax = Vector2.one;
			component8.offsetMin = Vector2.zero;
			component8.offsetMax = Vector2.zero;
		}
		Image component9 = ((Component)val12).GetComponent<Image>();
		if ((Object)(object)component9 != (Object)null)
		{
			((Graphic)component9).color = new Color(0.88f, 0.14f, 0.14f, 0.75f);
			((Graphic)component9).raycastTarget = false;
		}
		Transform val14 = val10.Find("MainFill");
		bool isNewMainFill = false;
		if ((Object)(object)val14 == (Object)null)
		{
			GameObject val15 = new GameObject("MainFill", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val15.transform.SetParent(val10, false);
			val14 = val15.transform;
			isNewMainFill = true;
		}
		RectTransform component10 = ((Component)val14).GetComponent<RectTransform>();
		if (isNewMainFill)
		{
			component10.anchorMin = Vector2.zero;
			component10.anchorMax = Vector2.one;
			component10.offsetMin = Vector2.zero;
			component10.offsetMax = Vector2.zero;
		}
		Image component11 = ((Component)val14).GetComponent<Image>();
		if ((Object)(object)component11 != (Object)null)
		{
			((Graphic)component11).color = val;
			((Graphic)component11).raycastTarget = false;
		}
		PlayerHealthBarUI playerHealthBarUI = ((Component)val2).gameObject.GetComponent<PlayerHealthBarUI>();
		if ((Object)(object)playerHealthBarUI == (Object)null)
		{
			playerHealthBarUI = ((Component)val2).gameObject.AddComponent<PlayerHealthBarUI>();
		}
		playerHealthBarUI.playerStats = playerStats;
		playerHealthBarUI.barBorderImage = component5;
		playerHealthBarUI.barBackgroundImage = component7;
		playerHealthBarUI.damageFillRect = component8;
		playerHealthBarUI.damageFillImage = component9;
		playerHealthBarUI.mainFillRect = component10;
		playerHealthBarUI.mainFillImage = component11;
		playerHealthBarUI.healthyColor = val;
		playerHealthBarUI.percentageColor = val;
		playerHealthBarUI.damageLagColor = new Color(0.88f, 0.14f, 0.14f, 0.75f);
		playerHealthBarUI.hpText = val4;
		playerHealthBarUI.percentageText = val6;
		playerHealthBarUI.autoSyncWithPlayerStats = true;
		playerHealthBarUI.hideWhenCombatActive = false;
		if ((Object)(object)playerStats != (Object)null && !playerHealthBarUI.IsInitialized)
		{
			playerHealthBarUI.SetHealthInstant(playerStats.currentHP, playerStats.maxHP);
		}
		this.playerHealthBarUI = playerHealthBarUI;
		playerHPText = val4;
	}

	private static Transform FindChildRecursive(Transform parent, string childName)
	{
		if ((Object)(object)parent == (Object)null)
		{
			return null;
		}
		Transform val = parent.Find(childName);
		if ((Object)(object)val != (Object)null)
		{
			return val;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform val2 = FindChildRecursive(parent.GetChild(i), childName);
			if ((Object)(object)val2 != (Object)null)
			{
				return val2;
			}
		}
		return null;
	}

	private void PlayEnemySlotOneShot(CombatEnemyInstance inst, Sprite[] frames, float frameDuration, bool holdLastFrame)
	{
		if (inst != null && frames != null && frames.Length != 0)
		{
			inst.overrideFrames = frames;
			inst.overrideFrameIndex = 0;
			inst.overrideFrameTimer = 0f;
			inst.overrideFrameDuration = Mathf.Max(0.05f, frameDuration);
			inst.holdLastOverrideFrame = holdLastFrame;
		}
	}

	private Sprite[] GetNextEnemyAttackFrames(CombatEnemyInstance inst)
	{
		if (inst == null || (Object)(object)inst.data == (Object)null)
		{
			return null;
		}
		List<Sprite[]> list = new List<Sprite[]>();
		if (inst.data.attackFrames != null && inst.data.attackFrames.Length != 0)
		{
			list.Add(inst.data.attackFrames);
		}
		if (inst.data.attack2Frames != null && inst.data.attack2Frames.Length != 0)
		{
			list.Add(inst.data.attack2Frames);
		}
		if (inst.data.attack3Frames != null && inst.data.attack3Frames.Length != 0)
		{
			list.Add(inst.data.attack3Frames);
		}
		if (list.Count == 0)
		{
			return null;
		}
		Sprite[] result = list[inst.attackComboIndex % list.Count];
		inst.attackComboIndex++;
		return result;
	}

	private Sprite GetCurrentEnemyDisplaySprite(CombatEnemyInstance inst)
	{
		if (inst == null || (Object)(object)inst.data == (Object)null)
		{
			return null;
		}
		if (inst.overrideFrames != null && inst.overrideFrames.Length != 0)
		{
			int num = Mathf.Clamp(inst.overrideFrameIndex, 0, inst.overrideFrames.Length - 1);
			if ((Object)(object)inst.overrideFrames[num] != (Object)null)
			{
				return inst.overrideFrames[num];
			}
		}
		if (!inst.IsAlive && inst.data.deadFrames != null && inst.data.deadFrames.Length != 0)
		{
			return inst.data.deadFrames[inst.data.deadFrames.Length - 1];
		}
		if (inst.playingIdleAlt && inst.data.idleAltFrames != null && inst.data.idleAltFrames.Length != 0)
		{
			int num2 = Mathf.Clamp(inst.idleFrameIndex, 0, inst.data.idleAltFrames.Length - 1);
			return inst.data.idleAltFrames[num2];
		}
		if (inst.data.idleFrames != null && inst.data.idleFrames.Length != 0)
		{
			int num3 = Mathf.Clamp(inst.idleFrameIndex, 0, inst.data.idleFrames.Length - 1);
			return inst.data.idleFrames[num3];
		}
		return inst.data.enemySprite;
	}

	private void UpdateEnemySlotAnimations(float deltaTime)
	{
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		if (enemySlots == null || activeEnemies == null)
		{
			return;
		}
		for (int i = 0; i < enemySlots.Length && i < activeEnemies.Count; i++)
		{
			EnemyUISlot enemyUISlot = enemySlots[i];
			CombatEnemyInstance combatEnemyInstance = activeEnemies[i];
			if (enemyUISlot == null || (Object)(object)enemyUISlot.enemyPlaceholderImage == (Object)null || combatEnemyInstance == null || (Object)(object)combatEnemyInstance.data == (Object)null)
			{
				continue;
			}
			if (combatEnemyInstance.overrideFrames != null && combatEnemyInstance.overrideFrames.Length != 0)
			{
				combatEnemyInstance.overrideFrameTimer += deltaTime;
				if (combatEnemyInstance.overrideFrameTimer >= combatEnemyInstance.overrideFrameDuration)
				{
					combatEnemyInstance.overrideFrameTimer -= combatEnemyInstance.overrideFrameDuration;
					combatEnemyInstance.overrideFrameIndex++;
					if (combatEnemyInstance.overrideFrameIndex >= combatEnemyInstance.overrideFrames.Length)
					{
						if (combatEnemyInstance.holdLastOverrideFrame)
						{
							combatEnemyInstance.overrideFrameIndex = combatEnemyInstance.overrideFrames.Length - 1;
						}
						else
						{
							combatEnemyInstance.overrideFrames = null;
							combatEnemyInstance.overrideFrameIndex = 0;
						}
					}
				}
			}
			else if (combatEnemyInstance.IsAlive)
			{
				Sprite[] array = ((combatEnemyInstance.playingIdleAlt && combatEnemyInstance.data.idleAltFrames != null && combatEnemyInstance.data.idleAltFrames.Length != 0) ? combatEnemyInstance.data.idleAltFrames : combatEnemyInstance.data.idleFrames);
				if (array != null && array.Length != 0)
				{
					combatEnemyInstance.idleFrameTimer += deltaTime;
					if (combatEnemyInstance.idleFrameTimer >= 0.13f)
					{
						combatEnemyInstance.idleFrameTimer -= 0.13f;
						combatEnemyInstance.idleFrameIndex++;
						if (combatEnemyInstance.idleFrameIndex >= array.Length)
						{
							combatEnemyInstance.idleFrameIndex = 0;
							if (combatEnemyInstance.playingIdleAlt)
							{
								combatEnemyInstance.playingIdleAlt = false;
								combatEnemyInstance.idleLoopsCompleted = 0;
							}
							else
							{
								combatEnemyInstance.idleLoopsCompleted++;
								if (combatEnemyInstance.idleLoopsCompleted >= 2 && combatEnemyInstance.data.idleAltFrames != null && combatEnemyInstance.data.idleAltFrames.Length != 0)
								{
									combatEnemyInstance.playingIdleAlt = true;
								}
							}
						}
					}
				}
			}
			Sprite currentEnemyDisplaySprite = GetCurrentEnemyDisplaySprite(combatEnemyInstance);
			if ((Object)(object)currentEnemyDisplaySprite != (Object)null)
			{
				enemyUISlot.enemyPlaceholderImage.sprite = currentEnemyDisplaySprite;
				enemyUISlot.enemyPlaceholderImage.preserveAspect = true;
				((Graphic)enemyUISlot.enemyPlaceholderImage).color = (Color)(combatEnemyInstance.IsAlive ? Color.white : new Color(0.55f, 0.55f, 0.58f, 1f));
				if ((Object)(object)enemyUISlot.enemyAvatarLabelText != (Object)null && ((Component)enemyUISlot.enemyAvatarLabelText).gameObject.activeSelf)
				{
					((Component)enemyUISlot.enemyAvatarLabelText).gameObject.SetActive(false);
				}
			}
		}
	}

	public void SetBattleLog(string message)
	{
		if ((Object)(object)battleLogText != (Object)null)
		{
			((TMP_Text)battleLogText).text = message;
		}
	}

	public void SetActionButtonsInteractable(bool interactable)
	{
		if ((Object)(object)corporeoButton != (Object)null)
		{
			((Selectable)corporeoButton).interactable = interactable;
		}
		if ((Object)(object)intelectoButton != (Object)null)
		{
			((Selectable)intelectoButton).interactable = interactable;
		}
		if ((Object)(object)motricidadButton != (Object)null)
		{
			((Selectable)motricidadButton).interactable = interactable;
		}
		if ((Object)(object)mundoInternoButton != (Object)null)
		{
			((Selectable)mundoInternoButton).interactable = interactable;
		}
	}

	private bool WasDialogueAdvancePressed()
	{
		if (Keyboard.current != null && (((ButtonControl)Keyboard.current.spaceKey).wasPressedThisFrame || ((ButtonControl)Keyboard.current.enterKey).wasPressedThisFrame || ((ButtonControl)Keyboard.current.numpadEnterKey).wasPressedThisFrame))
		{
			return true;
		}
		if ((Object)(object)EventSystem.current != (Object)null && EventSystem.current.IsPointerOverGameObject())
		{
			GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
			if ((Object)(object)currentSelectedGameObject != (Object)null && (Object)(object)currentSelectedGameObject.GetComponent<Button>() != (Object)null)
			{
				return false;
			}
		}
		if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
		{
			return true;
		}
		return false;
	}

	private void UpdateDynamicTargetIndicators(float deltaTime)
	{
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		if (enemySlots == null || activeEnemies == null)
		{
			return;
		}
		float time = Time.time;
		float num = Mathf.Sin(time * 6.5f) * 6f;
		float num2 = 1f + Mathf.Sin(time * 5f) * 0.08f;
		float a = 0.45f + Mathf.Sin(time * 4.5f) * 0.2f;
		for (int i = 0; i < enemySlots.Length && i < activeEnemies.Count; i++)
		{
			EnemyUISlot enemyUISlot = enemySlots[i];
			if (enemyUISlot == null)
			{
				continue;
			}
			bool flag = i == selectedTargetIndex && activeEnemies[i].IsAlive;
			if ((Object)(object)enemyUISlot.targetIndicator != (Object)null)
			{
				if (enemyUISlot.targetIndicator.activeSelf != flag)
				{
					enemyUISlot.targetIndicator.SetActive(flag);
				}
				if (flag && (Object)(object)enemyUISlot.targetIndicatorRect != (Object)null)
				{
					enemyUISlot.targetIndicatorRect.anchoredPosition = new Vector2(0f, num);
					((Transform)enemyUISlot.targetIndicatorRect).localScale = new Vector3(num2, num2, 1f);
				}
			}
			if ((Object)(object)enemyUISlot.groundIndicatorRing != (Object)null)
			{
				if (flag)
				{
					Color color = selectedTargetBorderColor;
					color.a = a;
					((Graphic)enemyUISlot.groundIndicatorRing).color = color;
				}
				else
				{
					((Graphic)enemyUISlot.groundIndicatorRing).color = new Color(0f, 0f, 0f, 0.32f);
				}
			}
		}
	}

	private bool IsBossEnemy(CombatEnemyInstance attacker)
	{
		if (attacker == null || (Object)(object)attacker.data == (Object)null)
		{
			return isCurrentEncounterBoss;
		}
		if (attacker.data.enemyType == EnemyType.JefeElCalacas || attacker.data.enemyType == EnemyType.JefeNivel2Capataz)
		{
			return true;
		}
		if (!string.IsNullOrEmpty(attacker.data.enemyName))
		{
			string text = attacker.data.enemyName.ToUpperInvariant();
			if (text.Contains("JEFE") || text.Contains("BOSS") || text.Contains("CALACAS") || text.Contains("COMANDANTE") || text.Contains("CAPATAZ"))
			{
				return true;
			}
		}
		if (isCurrentEncounterBoss && attacker.data.enemyType != EnemyType.LacayoRapidoDebil)
		{
			return true;
		}
		return false;
	}

	/// <summary>
	/// Garantiza que exista un BattleManager y su interfaz de combate completa en la escena activa (ej. en el Nivel 2 o Nivel 3).
	/// </summary>
	public static BattleManager EnsureInScene()
	{
		BattleManager existing = Object.FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include);
		if ((Object)(object)existing != (Object)null && (Object)(object)existing.battlePanel != (Object)null)
		{
			existing.EnforceUnifiedCombatUILayout();
			return existing;
		}
		return BuildRuntimeBattleSystemIfMissing(existing);
	}

	/// <summary>
	/// Unifica todos los paneles de combate (Nivel 1, Nivel 2 y Nivel 3) para que compartan exactamente el mismo
	/// diseño Noir, proporciones, bordes de casillas enemigas, submenús de Intelecto/Mundo Interno y tipografía.
	/// </summary>
	public void EnforceUnifiedCombatUILayout()
	{
		if ((Object)(object)battlePanel == (Object)null)
		{
			return;
		}

		// 1. Unificar EnemySlots (corrigiendo el bug de Nivel 2 donde borderImage apuntaba a la raíz del slot y faltaban anillos/indicadores)
		if (enemySlots != null)
		{
			for (int i = 0; i < enemySlots.Length; i++)
			{
				EnemyUISlot slot = enemySlots[i];
				if (slot == null || (Object)(object)slot.slotRoot == (Object)null)
				{
					continue;
				}
				Image rootImg = slot.slotRoot.GetComponent<Image>();
				if ((Object)(object)rootImg != (Object)null)
				{
					((Graphic)rootImg).color = new Color(0f, 0f, 0f, 0f);
				}
				Transform innerTr = slot.slotRoot.transform.Find("Inner");
				if ((Object)(object)innerTr != (Object)null)
				{
					Image inImg = ((Component)innerTr).GetComponent<Image>();
					if ((Object)(object)inImg != (Object)null)
					{
						((Graphic)inImg).color = new Color(0f, 0f, 0f, 0f);
					}
				}
				Transform nameplateBorderTr = slot.slotRoot.transform.Find("EnemyNameplateBorder");
				if ((Object)(object)nameplateBorderTr != (Object)null)
				{
					Image npBorderImg = ((Component)nameplateBorderTr).GetComponent<Image>();
					if ((Object)(object)npBorderImg != (Object)null)
					{
						slot.borderImage = npBorderImg;
					}
					RectTransform npRT = ((Component)nameplateBorderTr).GetComponent<RectTransform>();
					if ((Object)(object)npRT != (Object)null)
					{
						npRT.anchorMin = new Vector2(0.06f, 0.77f);
						npRT.anchorMax = new Vector2(0.94f, 0.89f);
						npRT.offsetMin = Vector2.zero;
						npRT.offsetMax = Vector2.zero;
					}
				}
				else
				{
					GameObject npBorderGO = new GameObject("EnemyNameplateBorder", typeof(RectTransform), typeof(Image));
					npBorderGO.transform.SetParent(slot.slotRoot.transform, false);
					RectTransform npRT = npBorderGO.GetComponent<RectTransform>();
					npRT.anchorMin = new Vector2(0.06f, 0.77f);
					npRT.anchorMax = new Vector2(0.94f, 0.89f);
					npRT.offsetMin = Vector2.zero;
					npRT.offsetMax = Vector2.zero;
					Image npBorderImg = npBorderGO.GetComponent<Image>();
					((Graphic)npBorderImg).color = normalEnemyBorderColor;
					slot.borderImage = npBorderImg;

					GameObject npBg = new GameObject("EnemyNameplateBg", typeof(RectTransform), typeof(Image));
					npBg.transform.SetParent(npBorderGO.transform, false);
					RectTransform bgRT = npBg.GetComponent<RectTransform>();
					bgRT.anchorMin = new Vector2(0.02f, 0.08f);
					bgRT.anchorMax = new Vector2(0.98f, 0.92f);
					bgRT.offsetMin = Vector2.zero;
					bgRT.offsetMax = Vector2.zero;
					Image bgImg = npBg.GetComponent<Image>();
					((Graphic)bgImg).color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

					if ((Object)(object)slot.enemyNameText != (Object)null)
					{
						((Component)slot.enemyNameText).transform.SetParent(npBg.transform, false);
						RectTransform nmRT = ((Component)slot.enemyNameText).GetComponent<RectTransform>();
						nmRT.anchorMin = new Vector2(0.02f, 0.48f);
						nmRT.anchorMax = new Vector2(0.98f, 0.98f);
						nmRT.offsetMin = Vector2.zero;
						nmRT.offsetMax = Vector2.zero;
					}
					if ((Object)(object)slot.enemyHPText != (Object)null)
					{
						((Component)slot.enemyHPText).transform.SetParent(npBg.transform, false);
						RectTransform hpRT = ((Component)slot.enemyHPText).GetComponent<RectTransform>();
						hpRT.anchorMin = new Vector2(0.02f, 0.04f);
						hpRT.anchorMax = new Vector2(0.98f, 0.48f);
						hpRT.offsetMin = Vector2.zero;
						hpRT.offsetMax = Vector2.zero;
					}
					if ((Object)(object)slot.enemyPlaceholderImage != (Object)null)
					{
						RectTransform spriteRT = ((Component)slot.enemyPlaceholderImage).GetComponent<RectTransform>();
						spriteRT.anchorMin = new Vector2(0.05f, 0.02f);
						spriteRT.anchorMax = new Vector2(0.95f, 0.74f);
						spriteRT.offsetMin = Vector2.zero;
						spriteRT.offsetMax = Vector2.zero;
					}
				}
				if ((Object)(object)slot.groundIndicatorRing == (Object)null)
				{
					Transform ringTr = slot.slotRoot.transform.Find("GroundIndicatorRing")
						?? slot.slotRoot.transform.Find("GroundTargetRing");
					if ((Object)(object)ringTr == (Object)null)
					{
						GameObject ringGO = new GameObject("GroundIndicatorRing", typeof(RectTransform), typeof(Image));
						ringGO.transform.SetParent(slot.slotRoot.transform, false);
						ringGO.transform.SetAsFirstSibling();
						Image ringImg = ringGO.GetComponent<Image>();
						((Graphic)ringImg).color = new Color(0f, 0f, 0f, 0.32f);
						((Graphic)ringImg).raycastTarget = false;
						slot.groundIndicatorRing = ringImg;
					}
					else
					{
						slot.groundIndicatorRing = ((Component)ringTr).GetComponent<Image>();
					}
				}
				if ((Object)(object)slot.groundIndicatorRing != (Object)null)
				{
					RectTransform ringRT = ((Graphic)slot.groundIndicatorRing).rectTransform;
					ringRT.anchorMin = new Vector2(0.15f, 0.00f);
					ringRT.anchorMax = new Vector2(0.85f, 0.10f);
					ringRT.offsetMin = Vector2.zero;
					ringRT.offsetMax = Vector2.zero;
					((Graphic)slot.groundIndicatorRing).color = new Color(0f, 0f, 0f, 0.32f);
				}
				if ((Object)(object)slot.targetIndicator == (Object)null)
				{
					Transform indTr = slot.slotRoot.transform.Find("TargetIndicator")
						?? slot.slotRoot.transform.Find("TargetSelectionIndicator");
					if ((Object)(object)indTr == (Object)null)
					{
						GameObject indGO = new GameObject("TargetSelectionIndicator", typeof(RectTransform), typeof(Image));
						indGO.transform.SetParent(slot.slotRoot.transform, false);
						RectTransform indRT = indGO.GetComponent<RectTransform>();
						indRT.anchorMin = new Vector2(0.20f, 0.90f);
						indRT.anchorMax = new Vector2(0.80f, 0.99f);
						indRT.offsetMin = Vector2.zero;
						indRT.offsetMax = Vector2.zero;
						Image indImg = indGO.GetComponent<Image>();
						((Graphic)indImg).color = new Color(0.95f, 0.75f, 0.2f, 0.95f);
						((Graphic)indImg).raycastTarget = false;

						GameObject lblGO = new GameObject("IndicatorLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
						lblGO.transform.SetParent(indGO.transform, false);
						RectTransform lblRT = lblGO.GetComponent<RectTransform>();
						lblRT.anchorMin = Vector2.zero;
						lblRT.anchorMax = Vector2.one;
						lblRT.offsetMin = Vector2.zero;
						lblRT.offsetMax = Vector2.zero;
						TextMeshProUGUI lblTMP = lblGO.GetComponent<TextMeshProUGUI>();
						((TMP_Text)lblTMP).text = "▼ OBJETIVO SELECCIONADO ▼";
						((TMP_Text)lblTMP).fontSize = 18f;
						((TMP_Text)lblTMP).alignment = TextAlignmentOptions.Center;
						((Graphic)lblTMP).color = new Color(0.05f, 0.05f, 0.07f, 1f);
						((Graphic)lblTMP).raycastTarget = false;
						PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)lblTMP);
						slot.targetIndicator = indGO;
					}
					else
					{
						slot.targetIndicator = ((Component)indTr).gameObject;
					}
				}
				if ((Object)(object)slot.enemyNameText != (Object)null)
				{
					((TMP_Text)slot.enemyNameText).fontSize = 26f;
					PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)slot.enemyNameText);
				}
				if ((Object)(object)slot.enemyHPText != (Object)null)
				{
					((TMP_Text)slot.enemyHPText).fontSize = 24f;
					PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)slot.enemyHPText);
				}
			}
		}

		// 2. Unificar el texto del registro de combate y los 4 botones de comando
		if ((Object)(object)battleLogText != (Object)null)
		{
			((TMP_Text)battleLogText).fontSize = 32f;
			((TMP_Text)battleLogText).alignment = TextAlignmentOptions.Center;
			PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)battleLogText);
		}
		Button[] mainVoiceButtons = new Button[4] { corporeoButton, intelectoButton, motricidadButton, mundoInternoButton };
		foreach (Button b in mainVoiceButtons)
		{
			if ((Object)(object)b == (Object)null) continue;
			TextMeshProUGUI btnTmp = ((Component)b).GetComponentInChildren<TextMeshProUGUI>();
			if ((Object)(object)btnTmp != (Object)null)
			{
				((TMP_Text)btnTmp).fontSize = 28f;
				((TMP_Text)btnTmp).alignment = TextAlignmentOptions.Center;
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)btnTmp);
			}
		}

		// 3. Unificar CombatDialoguePanel y el marco del retrato (CombatPortraitContainer) al estilo alto del Nivel 1
		if ((Object)(object)combatDialoguePanel != (Object)null)
		{
			Transform portraitCont = combatDialoguePanel.transform.Find("CombatPortraitContainer");
			if ((Object)(object)portraitCont != (Object)null)
			{
				RectTransform pcRT = ((Component)portraitCont).GetComponent<RectTransform>();
				if ((Object)(object)pcRT != (Object)null)
				{
					pcRT.anchorMin = new Vector2(0.01f, 0.04f);
					pcRT.anchorMax = new Vector2(0.195f, 1.55f);
					pcRT.offsetMin = Vector2.zero;
					pcRT.offsetMax = Vector2.zero;
				}
			}
			if ((Object)(object)combatSpeakerNameText != (Object)null)
			{
				((TMP_Text)combatSpeakerNameText).fontSize = 40f;
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)combatSpeakerNameText);
			}
			if ((Object)(object)combatDialogueText != (Object)null)
			{
				((TMP_Text)combatDialogueText).fontSize = 34f;
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)combatDialogueText);
			}
		}

		LoadPortraitsIfMissing();

		// 4. Asegurar retratos y GIFs de ataque/explosión estandarizados como en Nivel 1 (solo secuencia Orange)
		EnsureDetectiveCombatPortraitResolved();

		bool needsAttackFrames = (corporeoGif == null || corporeoGif.gifFrames == null || corporeoGif.gifFrames.Length == 0 || corporeoGif.gifFrames.Length > 10 || corporeoGif.duration > 1.5f);
		bool needsEnemyAttackFrames = (enemyAttackGif == null || enemyAttackGif.gifFrames == null || enemyAttackGif.gifFrames.Length == 0 || enemyAttackGif.gifFrames.Length > 10 || enemyAttackGif.duration > 1.5f);
		if (needsAttackFrames || needsEnemyAttackFrames)
		{
			Sprite[] attackSprites = LoadFilteredSpriteSequence("Assets/Scripts/AnimacionAtaque.png", "AnimacionAtaque_Orange_");
			if (attackSprites.Length > 0)
			{
				if (needsAttackFrames)
				{
					if (corporeoGif == null) corporeoGif = new ActionGifData();
					corporeoGif.actionLabel = "[GIF: CORPOREO - Golpe fisico directo]";
					corporeoGif.gifFrames = attackSprites;
					corporeoGif.duration = 1.5f;
				}
				if (needsEnemyAttackFrames)
				{
					if (enemyAttackGif == null) enemyAttackGif = new ActionGifData();
					enemyAttackGif.actionLabel = "[GIF: ENEMIGO - Lanzando ataque contra Enrique]";
					enemyAttackGif.gifFrames = attackSprites;
					enemyAttackGif.duration = 1.5f;
				}
			}
		}

		bool needsExpFrames = (explosionGif == null || explosionGif.gifFrames == null || explosionGif.gifFrames.Length == 0 || explosionGif.gifFrames.Length > 12 || explosionGif.duration > 1.5f);
		if (needsExpFrames)
		{
			Sprite[] expSprites = LoadFilteredSpriteSequence("Assets/Scripts/AtaqueExplosion.png", "AtaqueExplosion_Orange_");
			if (expSprites.Length > 0)
			{
				if (explosionGif == null) explosionGif = new ActionGifData();
				explosionGif.actionLabel = "[GIF: EXPLOSION - Fuego y metralla]";
				explosionGif.gifFrames = expSprites;
				explosionGif.duration = 1.2f;
			}
		}

		// Ocultar botón flotante de inventario y tarjeta flotante del aliado en combate
		if ((Object)(object)combatInventoryButtonRoot != (Object)null)
		{
			combatInventoryButtonRoot.SetActive(false);
		}
		if ((Object)(object)detectiveActionMenu != (Object)null)
		{
			detectiveActionMenu.RefreshAllySlotUI(false);
		}

		// 5. Asegurar subpaneles de Intelecto y Mundo Interno si la escena fue generada en runtime (ej. Nivel 3)
		EnsureUnifiedSubpanelsExist();
	}

	private void EnsureUnifiedSubpanelsExist()
	{
		if ((Object)(object)battlePanel == (Object)null)
		{
			return;
		}

		// Subpanel de Intelecto y Objetos (mismas anclas Noir que Nivel 1 y Nivel 2: mitad inferior)
		if ((Object)(object)intelectoAndItemsPanel == (Object)null)
		{
			Transform existingInt = battlePanel.transform.Find("IntelectoAndItemsPanel");
			if ((Object)(object)existingInt != (Object)null)
			{
				intelectoAndItemsPanel = ((Component)existingInt).gameObject;
				intelectoCraftingPanel = intelectoAndItemsPanel;
				intelectoCraftingUI = intelectoAndItemsPanel.GetComponent<CombatIntelectoUI>();
			}
			else
			{
				GameObject intPanel = new GameObject("IntelectoAndItemsPanel", typeof(RectTransform), typeof(Image), typeof(CombatIntelectoUI));
				intPanel.transform.SetParent(battlePanel.transform, false);
				RectTransform ipRT = intPanel.GetComponent<RectTransform>();
				ipRT.anchorMin = new Vector2(0.04f, 0.02f);
				ipRT.anchorMax = new Vector2(0.96f, 0.53f);
				ipRT.offsetMin = Vector2.zero;
				ipRT.offsetMax = Vector2.zero;
				((Graphic)intPanel.GetComponent<Image>()).color = new Color(0.20f, 0.48f, 0.82f, 1f);

				GameObject inner = new GameObject("IntelectoInnerBox", typeof(RectTransform), typeof(Image));
				inner.transform.SetParent(intPanel.transform, false);
				RectTransform inRT = inner.GetComponent<RectTransform>();
				inRT.anchorMin = new Vector2(0.003f, 0.008f);
				inRT.anchorMax = new Vector2(0.997f, 0.992f);
				inRT.offsetMin = Vector2.zero;
				inRT.offsetMax = Vector2.zero;
				((Graphic)inner.GetComponent<Image>()).color = new Color(0.03f, 0.03f, 0.04f, 0.99f);

				GameObject titleGO = new GameObject("IntelectoTitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
				titleGO.transform.SetParent(inner.transform, false);
				RectTransform tRT = titleGO.GetComponent<RectTransform>();
				tRT.anchorMin = new Vector2(0.02f, 0.86f);
				tRT.anchorMax = new Vector2(0.98f, 0.98f);
				tRT.offsetMin = Vector2.zero;
				tRT.offsetMax = Vector2.zero;
				TextMeshProUGUI tTMP = titleGO.GetComponent<TextMeshProUGUI>();
				((TMP_Text)tTMP).text = "SECCION 1: USAR OBJETOS CURATIVOS  |  SECCION 2: CRAFTEAR CON INTELECTO";
				((TMP_Text)tTMP).fontSize = 26f;
				((TMP_Text)tTMP).alignment = TextAlignmentOptions.Center;
				((Graphic)tTMP).color = new Color(0.32f, 0.66f, 0.98f, 1f);
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)tTMP);

				GameObject statusGO = new GameObject("InventorySummaryText", typeof(RectTransform), typeof(TextMeshProUGUI));
				statusGO.transform.SetParent(inner.transform, false);
				RectTransform stRT = statusGO.GetComponent<RectTransform>();
				stRT.anchorMin = new Vector2(0.02f, 0.74f);
				stRT.anchorMax = new Vector2(0.98f, 0.85f);
				stRT.offsetMin = Vector2.zero;
				stRT.offsetMax = Vector2.zero;
				TextMeshProUGUI stTMP = statusGO.GetComponent<TextMeshProUGUI>();
				((TMP_Text)stTMP).fontSize = 24f;
				((TMP_Text)stTMP).alignment = TextAlignmentOptions.Center;
				((Graphic)stTMP).color = new Color(0.90f, 0.88f, 0.84f, 1f);
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)stTMP);

				GameObject dynContainerGO = new GameObject("DynamicButtonsContainer", typeof(RectTransform));
				dynContainerGO.transform.SetParent(inner.transform, false);
				RectTransform dcRT = dynContainerGO.GetComponent<RectTransform>();
				dcRT.anchorMin = new Vector2(0.02f, 0.19f);
				dcRT.anchorMax = new Vector2(0.98f, 0.73f);
				dcRT.offsetMin = Vector2.zero;
				dcRT.offsetMax = Vector2.zero;

				Button closeBtn = CreateRuntimeVoiceButton(inner.transform, "CancelIntelectoButton", new Vector2(0.22f, 0.025f), new Vector2(0.78f, 0.17f), new Color(0.26f, 0.26f, 0.30f, 1f), "CANCELAR / VOLVER A LAS 4 VOCES (NO GASTA TURNO)", CloseIntelectoPanel);

				CombatIntelectoUI uiComp = intPanel.GetComponent<CombatIntelectoUI>();
				uiComp.battleManager = this;
				uiComp.panelRoot = intPanel;
				uiComp.titleText = tTMP;
				uiComp.inventorySummaryText = stTMP;
				uiComp.dynamicButtonsContainer = dcRT;
				uiComp.cancelButton = closeBtn;
				uiComp.customFont = ((TMP_Text)tTMP).font;

				intelectoAndItemsPanel = intPanel;
				intelectoCraftingPanel = intPanel;
				intelectoCraftingUI = uiComp;
				intPanel.SetActive(false);
			}
		}

		// Garantizar anclas unificadas y ausencia de LayoutGroup en Intelecto incluso si ya existía en escena
		if ((Object)(object)intelectoAndItemsPanel != (Object)null)
		{
			RectTransform ipRT = intelectoAndItemsPanel.GetComponent<RectTransform>();
			if ((Object)(object)ipRT != (Object)null)
			{
				ipRT.anchorMin = new Vector2(0.04f, 0.02f);
				ipRT.anchorMax = new Vector2(0.96f, 0.53f);
				ipRT.offsetMin = Vector2.zero;
				ipRT.offsetMax = Vector2.zero;
			}
			if ((Object)(object)intelectoCraftingUI == (Object)null)
			{
				intelectoCraftingUI = intelectoAndItemsPanel.GetComponent<CombatIntelectoUI>();
			}
			if ((Object)(object)intelectoCraftingUI != (Object)null && (Object)(object)intelectoCraftingUI.dynamicButtonsContainer != (Object)null)
			{
				LayoutGroup strayLg = ((Component)intelectoCraftingUI.dynamicButtonsContainer).GetComponent<LayoutGroup>();
				if ((Object)(object)strayLg != (Object)null)
				{
					if (Application.isPlaying) Destroy(strayLg);
					else DestroyImmediate(strayLg);
				}
				RectTransform dcRT = intelectoCraftingUI.dynamicButtonsContainer;
				dcRT.anchorMin = new Vector2(0.02f, 0.19f);
				dcRT.anchorMax = new Vector2(0.98f, 0.73f);
				dcRT.offsetMin = Vector2.zero;
				dcRT.offsetMax = Vector2.zero;
			}
		}

		// Subpanel de Mundo Interno (mismas anclas Noir que Nivel 1 y Nivel 2)
		if ((Object)(object)mundoInternoPanel == (Object)null)
		{
			Transform existingMi = battlePanel.transform.Find("MundoInternoPanel");
			if ((Object)(object)existingMi != (Object)null)
			{
				mundoInternoPanel = ((Component)existingMi).gameObject;
				mundoInternoUI = mundoInternoPanel.GetComponent<CombatMundoInternoUI>();
			}
			else
			{
				GameObject miPanel = new GameObject("MundoInternoPanel", typeof(RectTransform), typeof(Image), typeof(CombatMundoInternoUI));
				miPanel.transform.SetParent(battlePanel.transform, false);
				RectTransform mpRT = miPanel.GetComponent<RectTransform>();
				mpRT.anchorMin = new Vector2(0.04f, 0.02f);
				mpRT.anchorMax = new Vector2(0.96f, 0.53f);
				mpRT.offsetMin = Vector2.zero;
				mpRT.offsetMax = Vector2.zero;
				((Graphic)miPanel.GetComponent<Image>()).color = new Color(0.58f, 0.26f, 0.78f, 1f);

				GameObject inner = new GameObject("MundoInternoInnerBox", typeof(RectTransform), typeof(Image));
				inner.transform.SetParent(miPanel.transform, false);
				RectTransform inRT = inner.GetComponent<RectTransform>();
				inRT.anchorMin = new Vector2(0.003f, 0.008f);
				inRT.anchorMax = new Vector2(0.997f, 0.992f);
				inRT.offsetMin = Vector2.zero;
				inRT.offsetMax = Vector2.zero;
				((Graphic)inner.GetComponent<Image>()).color = new Color(0.03f, 0.03f, 0.04f, 0.99f);

				GameObject titleGO = new GameObject("MundoInternoTitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
				titleGO.transform.SetParent(inner.transform, false);
				RectTransform tRT = titleGO.GetComponent<RectTransform>();
				tRT.anchorMin = new Vector2(0.02f, 0.86f);
				tRT.anchorMax = new Vector2(0.98f, 0.98f);
				tRT.offsetMin = Vector2.zero;
				tRT.offsetMax = Vector2.zero;
				TextMeshProUGUI tTMP = titleGO.GetComponent<TextMeshProUGUI>();
				((TMP_Text)tTMP).text = "MUNDO INTERNO: PSICOLOGIA Y EMOCIONES DE ENRIQUE";
				((TMP_Text)tTMP).fontSize = 26f;
				((TMP_Text)tTMP).alignment = TextAlignmentOptions.Center;
				((Graphic)tTMP).color = new Color(0.78f, 0.45f, 0.98f, 1f);
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)tTMP);

				GameObject statusGO = new GameObject("MundoInternoDescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
				statusGO.transform.SetParent(inner.transform, false);
				RectTransform stRT = statusGO.GetComponent<RectTransform>();
				stRT.anchorMin = new Vector2(0.02f, 0.73f);
				stRT.anchorMax = new Vector2(0.98f, 0.85f);
				stRT.offsetMin = Vector2.zero;
				stRT.offsetMax = Vector2.zero;
				TextMeshProUGUI stTMP = statusGO.GetComponent<TextMeshProUGUI>();
				((TMP_Text)stTMP).text = "Elige como influir en la psique del combate (no gasta turno si cancelas):";
				((TMP_Text)stTMP).fontSize = 22f;
				((TMP_Text)stTMP).alignment = TextAlignmentOptions.Center;
				((Graphic)stTMP).color = new Color(0.90f, 0.88f, 0.84f, 1f);
				PlayerHealthBarUI.EnsureCleanPixelFont((TMP_Text)stTMP);

				CombatMundoInternoUI miComp = miPanel.GetComponent<CombatMundoInternoUI>();
				Button insultarBtn = CreateRuntimeVoiceButton(inner.transform, "InsultarButton", new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.70f), new Color(0.85f, 0.25f, 0.45f, 1f), "[1. INSULTAR] - Provoca al enemigo reduciendo su ataque (-4 ATQ)", miComp.OnInsultarClicked);
				Button animarseBtn = CreateRuntimeVoiceButton(inner.transform, "AnimarseButton", new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.51f), new Color(0.92f, 0.72f, 0.22f, 1f), "[2. ANIMARSE] - Enrique se auto-sugestiona (+5 ATQ para el proximo golpe Corporeo)", miComp.OnAnimarseClicked);
				Button delirioBtn = CreateRuntimeVoiceButton(inner.transform, "DelirioButton", new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.33f), new Color(0.58f, 0.26f, 0.78f, 1f), "[3. DELIRIO PSIQUICO] - Confunde al rival haciendole perder su proximo turno", miComp.OnDelirioClicked);
				Button closeBtn = CreateRuntimeVoiceButton(inner.transform, "CancelMundoInternoButton", new Vector2(0.22f, 0.02f), new Vector2(0.78f, 0.14f), new Color(0.26f, 0.26f, 0.30f, 1f), "CANCELAR / VOLVER A LAS 4 VOCES (NO GASTA TURNO)", miComp.OnCancelClicked);

				miComp.battleManager = this;
				miComp.panelRoot = miPanel;
				miComp.titleText = tTMP;
				miComp.descriptionText = stTMP;
				miComp.insultarButton = insultarBtn;
				miComp.animarseButton = animarseBtn;
				miComp.delirioButton = delirioBtn;
				miComp.cancelButton = closeBtn;
				miComp.insultarText = ((Component)insultarBtn).GetComponentInChildren<TextMeshProUGUI>();
				miComp.animarseText = ((Component)animarseBtn).GetComponentInChildren<TextMeshProUGUI>();
				miComp.delirioText = ((Component)delirioBtn).GetComponentInChildren<TextMeshProUGUI>();

				mundoInternoPanel = miPanel;
				mundoInternoUI = miComp;
				miPanel.SetActive(false);
			}
		}

		if ((Object)(object)mundoInternoPanel != (Object)null)
		{
			RectTransform mpRT = mundoInternoPanel.GetComponent<RectTransform>();
			if ((Object)(object)mpRT != (Object)null)
			{
				mpRT.anchorMin = new Vector2(0.04f, 0.02f);
				mpRT.anchorMax = new Vector2(0.96f, 0.53f);
				mpRT.offsetMin = Vector2.zero;
				mpRT.offsetMax = Vector2.zero;
			}
		}

		// Subpanel de Game Over unificado (idéntico al Nivel 1 y Nivel 2)
		if ((Object)(object)gameOverPanel == (Object)null && (Object)(object)battlePanel.transform.parent != (Object)null)
		{
			Transform existingGo = battlePanel.transform.parent.Find("GameOverPanel");
			if ((Object)(object)existingGo != (Object)null)
			{
				gameOverPanel = ((Component)existingGo).gameObject;
			}
			else
			{
				gameOverPanel = BuildUnifiedGameOverPanel(battlePanel.transform.parent);
			}
		}
	}

	private GameObject BuildUnifiedGameOverPanel(Transform canvasParent)
	{
		Color pureBlack = new Color(0.02f, 0.02f, 0.025f, 1f);
		Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
		Color crimsonBorder = new Color(0.68f, 0.14f, 0.14f, 1f);
		Color subtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
		Color boneWhite = new Color(0.90f, 0.88f, 0.84f, 1f);

		GameObject goPanel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
		goPanel.transform.SetParent(canvasParent, false);
		RectTransform goRT = goPanel.GetComponent<RectTransform>();
		goRT.anchorMin = Vector2.zero;
		goRT.anchorMax = Vector2.one;
		goRT.offsetMin = Vector2.zero;
		goRT.offsetMax = Vector2.zero;
		((Graphic)goPanel.GetComponent<Image>()).color = pureBlack;

		GameObject border = new GameObject("GameOverBorderFrame", typeof(RectTransform), typeof(Image));
		border.transform.SetParent(goPanel.transform, false);
		RectTransform bRT = border.GetComponent<RectTransform>();
		bRT.anchorMin = new Vector2(0.12f, 0.12f);
		bRT.anchorMax = new Vector2(0.88f, 0.88f);
		bRT.offsetMin = Vector2.zero;
		bRT.offsetMax = Vector2.zero;
		((Graphic)border.GetComponent<Image>()).color = crimsonBorder;

		GameObject innerBox = new GameObject("GameOverInnerBox", typeof(RectTransform), typeof(Image));
		innerBox.transform.SetParent(border.transform, false);
		RectTransform ibRT = innerBox.GetComponent<RectTransform>();
		ibRT.anchorMin = new Vector2(0.005f, 0.008f);
		ibRT.anchorMax = new Vector2(0.995f, 0.992f);
		ibRT.offsetMin = Vector2.zero;
		ibRT.offsetMax = Vector2.zero;
		((Graphic)innerBox.GetComponent<Image>()).color = matteBlack;

		TextMeshProUGUI CreateGoText(string name, Vector2 min, Vector2 max, string msg, float size, Color col)
		{
			GameObject tGO = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
			tGO.transform.SetParent(innerBox.transform, false);
			RectTransform rt = tGO.GetComponent<RectTransform>();
			rt.anchorMin = min;
			rt.anchorMax = max;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			TextMeshProUGUI tmp = tGO.GetComponent<TextMeshProUGUI>();
			tmp.text = msg;
			tmp.fontSize = size;
			tmp.alignment = TextAlignmentOptions.Center;
			tmp.color = col;
			PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
			return tmp;
		}

		CreateGoText("GameOverTitleText", new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.90f), "GAME OVER", 96f, new Color(0.85f, 0.18f, 0.18f, 1f));
		CreateGoText("GameOverSubtitleText", new Vector2(0.06f, 0.53f), new Vector2(0.94f, 0.66f), "FIN DEL RECORRIDO EN SANTA FELIPINA", 38f, boneWhite);
		CreateGoText("GameOverNarrativeText", new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.52f),
			"Enrique y el Detective Vargas cayeron en la oscuridad.\nEl rastro del culpable se perdió para siempre entre las sombras.",
			26f, new Color(0.68f, 0.68f, 0.72f, 1f));

		retryBattleButton = CreateRuntimeVoiceButton(innerBox.transform, "RetryBattleButton", new Vector2(0.10f, 0.10f), new Vector2(0.47f, 0.27f), crimsonBorder, "REINTENTAR EN EL MAPA\n(ESPACIO / R)", RetryFromGameOver);
		returnToMenuButton = CreateRuntimeVoiceButton(innerBox.transform, "ReturnToMenuButton", new Vector2(0.53f, 0.10f), new Vector2(0.90f, 0.27f), subtleBorder, "VOLVER AL MENU PRINCIPAL\n(M / ESC)", ReturnToMainMenu);
		if (string.IsNullOrEmpty(menuSceneName))
		{
			menuSceneName = "00_MenuIntro";
		}

		goPanel.SetActive(false);
		return goPanel;
	}

	private static BattleManager BuildRuntimeBattleSystemIfMissing(BattleManager targetManager = null)
	{
		GameObject canvasGO = GameObject.Find("CombatCanvas");
		if ((Object)(object)canvasGO == (Object)null)
		{
			canvasGO = new GameObject("CombatCanvas");
			Canvas canvas = canvasGO.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 100;
			CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;
			canvasGO.AddComponent<GraphicRaycaster>();
		}

		if ((Object)(object)targetManager == (Object)null)
		{
			targetManager = canvasGO.GetComponent<BattleManager>();
			if ((Object)(object)targetManager == (Object)null)
			{
				targetManager = canvasGO.AddComponent<BattleManager>();
			}
		}

		GameObject panelGO = new GameObject("BattlePanel", typeof(RectTransform), typeof(Image));
		panelGO.transform.SetParent(canvasGO.transform, false);
		RectTransform panelRT = panelGO.GetComponent<RectTransform>();
		panelRT.anchorMin = Vector2.zero;
		panelRT.anchorMax = Vector2.one;
		panelRT.offsetMin = Vector2.zero;
		panelRT.offsetMax = Vector2.zero;
		Image bgImg = panelGO.GetComponent<Image>();
		bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.98f);
		targetManager.battlePanel = panelGO;
		targetManager.combatBackgroundImage = bgImg;

		// Slots para 2 enemigos simultáneos
		targetManager.enemySlots = new EnemyUISlot[2];
		for (int i = 0; i < 2; i++)
		{
			EnemyUISlot slot = new EnemyUISlot();
			GameObject slotGO = new GameObject($"EnemySlot_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
			slotGO.transform.SetParent(panelGO.transform, false);
			RectTransform slotRT = slotGO.GetComponent<RectTransform>();
			slotRT.anchorMin = (i == 0) ? new Vector2(0.18f, 0.44f) : new Vector2(0.55f, 0.44f);
			slotRT.anchorMax = (i == 0) ? new Vector2(0.52f, 0.93f) : new Vector2(0.89f, 0.93f);
			slotRT.offsetMin = Vector2.zero;
			slotRT.offsetMax = Vector2.zero;
			Image border = slotGO.GetComponent<Image>();
			border.color = new Color(0.24f, 0.24f, 0.28f, 0.9f);

			GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
			inner.transform.SetParent(slotGO.transform, false);
			RectTransform inRT = inner.GetComponent<RectTransform>();
			inRT.anchorMin = new Vector2(0.02f, 0.02f);
			inRT.anchorMax = new Vector2(0.98f, 0.98f);
			inRT.offsetMin = Vector2.zero;
			inRT.offsetMax = Vector2.zero;
			inner.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.10f, 0.95f);

			GameObject avatarGO = new GameObject("EnemySprite", typeof(RectTransform), typeof(Image));
			avatarGO.transform.SetParent(inner.transform, false);
			RectTransform avRT = avatarGO.GetComponent<RectTransform>();
			avRT.anchorMin = new Vector2(0.10f, 0.18f);
			avRT.anchorMax = new Vector2(0.90f, 0.80f);
			avRT.offsetMin = Vector2.zero;
			avRT.offsetMax = Vector2.zero;
			Image avImg = avatarGO.GetComponent<Image>();
			avImg.preserveAspect = true;

			GameObject nameGO = new GameObject("EnemyName", typeof(RectTransform), typeof(TextMeshProUGUI));
			nameGO.transform.SetParent(inner.transform, false);
			RectTransform nmRT = nameGO.GetComponent<RectTransform>();
			nmRT.anchorMin = new Vector2(0.04f, 0.82f);
			nmRT.anchorMax = new Vector2(0.96f, 0.97f);
			nmRT.offsetMin = Vector2.zero;
			nmRT.offsetMax = Vector2.zero;
			TextMeshProUGUI nmTMP = nameGO.GetComponent<TextMeshProUGUI>();
			nmTMP.fontSize = 20f;
			nmTMP.alignment = TextAlignmentOptions.Center;
			PlayerHealthBarUI.EnsureCleanPixelFont(nmTMP);

			GameObject hpGO = new GameObject("EnemyHP", typeof(RectTransform), typeof(TextMeshProUGUI));
			hpGO.transform.SetParent(inner.transform, false);
			RectTransform hpRT = hpGO.GetComponent<RectTransform>();
			hpRT.anchorMin = new Vector2(0.04f, 0.03f);
			hpRT.anchorMax = new Vector2(0.96f, 0.17f);
			hpRT.offsetMin = Vector2.zero;
			hpRT.offsetMax = Vector2.zero;
			TextMeshProUGUI hpTMP = hpGO.GetComponent<TextMeshProUGUI>();
			hpTMP.fontSize = 18f;
			hpTMP.alignment = TextAlignmentOptions.Center;
			PlayerHealthBarUI.EnsureCleanPixelFont(hpTMP);

			Button btn = slotGO.GetComponent<Button>();
			int capturedIdx = i;
			btn.onClick.AddListener(() => targetManager.SelectTargetEnemy(capturedIdx));

			slot.slotRoot = slotGO;
			slot.borderImage = border;
			slot.enemyPlaceholderImage = avImg;
			slot.enemyNameText = nmTMP;
			slot.enemyHPText = hpTMP;
			slot.selectTargetButton = btn;
			targetManager.enemySlots[i] = slot;
		}

		// Retrato de Enrique a la izquierda
		GameObject portraitFrame = new GameObject("PlayerPortraitFrame", typeof(RectTransform), typeof(Image));
		portraitFrame.transform.SetParent(panelGO.transform, false);
		RectTransform pfRT = portraitFrame.GetComponent<RectTransform>();
		pfRT.anchorMin = new Vector2(0.015f, 0.02f);
		pfRT.anchorMax = new Vector2(0.175f, 0.43f);
		pfRT.offsetMin = Vector2.zero;
		pfRT.offsetMax = Vector2.zero;
		Image pfBorder = portraitFrame.GetComponent<Image>();
		pfBorder.color = new Color(0.28f, 0.28f, 0.32f, 1f);
		targetManager.combatPortraitFrameBorder = pfBorder;

		GameObject portraitImgGO = new GameObject("PlayerPortraitImage", typeof(RectTransform), typeof(Image));
		portraitImgGO.transform.SetParent(portraitFrame.transform, false);
		RectTransform piRT = portraitImgGO.GetComponent<RectTransform>();
		piRT.anchorMin = new Vector2(0.03f, 0.03f);
		piRT.anchorMax = new Vector2(0.97f, 0.97f);
		piRT.offsetMin = Vector2.zero;
		piRT.offsetMax = Vector2.zero;
		targetManager.playerCombatPortraitImage = portraitImgGO.GetComponent<Image>();

		// Franja de estado de Enrique (PlayerStatusStrip)
		GameObject statusStrip = new GameObject("PlayerStatusStrip", typeof(RectTransform), typeof(Image));
		statusStrip.transform.SetParent(panelGO.transform, false);
		RectTransform ssRT = statusStrip.GetComponent<RectTransform>();
		ssRT.anchorMin = new Vector2(0.19f, 0.198f);
		ssRT.anchorMax = new Vector2(0.97f, 0.282f);
		ssRT.offsetMin = Vector2.zero;
		ssRT.offsetMax = Vector2.zero;
		statusStrip.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.95f);

		GameObject pHpTextGO = new GameObject("PlayerHPText", typeof(RectTransform), typeof(TextMeshProUGUI));
		pHpTextGO.transform.SetParent(statusStrip.transform, false);
		targetManager.playerHPText = pHpTextGO.GetComponent<TextMeshProUGUI>();
		PlayerHealthBarUI.EnsureCleanPixelFont(targetManager.playerHPText);

		// Registro de Combate (BattleLog)
		GameObject logBox = new GameObject("BattleLogStrip", typeof(RectTransform), typeof(Image));
		logBox.transform.SetParent(panelGO.transform, false);
		RectTransform lbRT = logBox.GetComponent<RectTransform>();
		lbRT.anchorMin = new Vector2(0.19f, 0.29f);
		lbRT.anchorMax = new Vector2(0.97f, 0.42f);
		lbRT.offsetMin = Vector2.zero;
		lbRT.offsetMax = Vector2.zero;
		logBox.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.96f);

		GameObject logTextGO = new GameObject("BattleLogText", typeof(RectTransform), typeof(TextMeshProUGUI));
		logTextGO.transform.SetParent(logBox.transform, false);
		RectTransform ltRT = logTextGO.GetComponent<RectTransform>();
		ltRT.anchorMin = new Vector2(0.02f, 0.08f);
		ltRT.anchorMax = new Vector2(0.98f, 0.92f);
		ltRT.offsetMin = Vector2.zero;
		ltRT.offsetMax = Vector2.zero;
		TextMeshProUGUI logTMP = logTextGO.GetComponent<TextMeshProUGUI>();
		logTMP.fontSize = 21f;
		logTMP.alignment = TextAlignmentOptions.Left;
		logTMP.color = new Color(0.92f, 0.90f, 0.85f, 1f);
		PlayerHealthBarUI.EnsureCleanPixelFont(logTMP);
		targetManager.battleLogText = logTMP;

		// Botones de las 4 Voces de Enrique
		targetManager.corporeoButton = CreateRuntimeVoiceButton(panelGO.transform, "Btn_Corporeo", new Vector2(0.19f, 0.02f), new Vector2(0.375f, 0.185f), new Color(0.78f, 0.22f, 0.22f, 1f), "CORPÓREO\n<size=14>Atacar</size>", targetManager.OnCorporeoButton);
		targetManager.intelectoButton = CreateRuntimeVoiceButton(panelGO.transform, "Btn_Intelecto", new Vector2(0.388f, 0.02f), new Vector2(0.573f, 0.185f), new Color(0.22f, 0.52f, 0.85f, 1f), "INTELECTO\n<size=14>Objetos / Crear</size>", targetManager.OnIntelectoButton);
		targetManager.motricidadButton = CreateRuntimeVoiceButton(panelGO.transform, "Btn_Motricidad", new Vector2(0.586f, 0.02f), new Vector2(0.771f, 0.185f), new Color(0.85f, 0.65f, 0.18f, 1f), "MOTRICIDAD\n<size=14>Guardia / Esquive</size>", targetManager.OnMotricidadButton);
		targetManager.mundoInternoButton = CreateRuntimeVoiceButton(panelGO.transform, "Btn_MundoInterno", new Vector2(0.784f, 0.02f), new Vector2(0.97f, 0.185f), new Color(0.58f, 0.26f, 0.78f, 1f), "MUNDO INTERNO\n<size=14>Personalidades</size>", targetManager.OnMundoInternoButton);

		// Panel de GIF de Acción
		GameObject gifPanel = new GameObject("ActionGifPanel", typeof(RectTransform), typeof(Image));
		gifPanel.transform.SetParent(panelGO.transform, false);
		RectTransform gpRT = gifPanel.GetComponent<RectTransform>();
		gpRT.anchorMin = new Vector2(0.25f, 0.32f);
		gpRT.anchorMax = new Vector2(0.75f, 0.76f);
		gpRT.offsetMin = Vector2.zero;
		gpRT.offsetMax = Vector2.zero;
		gifPanel.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.96f);

		GameObject gifImgGO = new GameObject("GifImage", typeof(RectTransform), typeof(Image));
		gifImgGO.transform.SetParent(gifPanel.transform, false);
		RectTransform giRT = gifImgGO.GetComponent<RectTransform>();
		giRT.anchorMin = new Vector2(0.05f, 0.18f);
		giRT.anchorMax = new Vector2(0.95f, 0.94f);
		giRT.offsetMin = Vector2.zero;
		giRT.offsetMax = Vector2.zero;
		targetManager.actionGifImage = gifImgGO.GetComponent<Image>();

		GameObject gifLblGO = new GameObject("GifLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
		gifLblGO.transform.SetParent(gifPanel.transform, false);
		RectTransform glRT = gifLblGO.GetComponent<RectTransform>();
		glRT.anchorMin = new Vector2(0.05f, 0.02f);
		glRT.anchorMax = new Vector2(0.95f, 0.18f);
		glRT.offsetMin = Vector2.zero;
		glRT.offsetMax = Vector2.zero;
		TextMeshProUGUI glTMP = gifLblGO.GetComponent<TextMeshProUGUI>();
		glTMP.fontSize = 18f;
		glTMP.alignment = TextAlignmentOptions.Center;
		PlayerHealthBarUI.EnsureCleanPixelFont(glTMP);
		targetManager.actionGifLabelText = glTMP;
		targetManager.actionGifPanel = gifPanel;
		gifPanel.SetActive(false);

		// Panel de Diálogo en Combate
		GameObject diagPanel = new GameObject("CombatDialoguePanel", typeof(RectTransform), typeof(Image));
		diagPanel.transform.SetParent(panelGO.transform, false);
		RectTransform dpRT = diagPanel.GetComponent<RectTransform>();
		dpRT.anchorMin = new Vector2(0.18f, 0.02f);
		dpRT.anchorMax = new Vector2(0.97f, 0.30f);
		dpRT.offsetMin = Vector2.zero;
		dpRT.offsetMax = Vector2.zero;
		diagPanel.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.98f);
		targetManager.combatDialoguePanel = diagPanel;

		GameObject spkGO = new GameObject("SpeakerName", typeof(RectTransform), typeof(TextMeshProUGUI));
		spkGO.transform.SetParent(diagPanel.transform, false);
		RectTransform spkRT = spkGO.GetComponent<RectTransform>();
		spkRT.anchorMin = new Vector2(0.04f, 0.72f);
		spkRT.anchorMax = new Vector2(0.80f, 0.95f);
		spkRT.offsetMin = Vector2.zero;
		spkRT.offsetMax = Vector2.zero;
		targetManager.combatSpeakerNameText = spkGO.GetComponent<TextMeshProUGUI>();
		targetManager.combatSpeakerNameText.fontSize = 24f;
		PlayerHealthBarUI.EnsureCleanPixelFont(targetManager.combatSpeakerNameText);

		GameObject dTxtGO = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
		dTxtGO.transform.SetParent(diagPanel.transform, false);
		RectTransform dtRT = dTxtGO.GetComponent<RectTransform>();
		dtRT.anchorMin = new Vector2(0.04f, 0.12f);
		dtRT.anchorMax = new Vector2(0.80f, 0.70f);
		dtRT.offsetMin = Vector2.zero;
		dtRT.offsetMax = Vector2.zero;
		targetManager.combatDialogueText = dTxtGO.GetComponent<TextMeshProUGUI>();
		targetManager.combatDialogueText.fontSize = 22f;
		PlayerHealthBarUI.EnsureCleanPixelFont(targetManager.combatDialogueText);

		targetManager.combatNextDialogueButton = CreateRuntimeVoiceButton(diagPanel.transform, "NextDialogueBtn", new Vector2(0.82f, 0.15f), new Vector2(0.98f, 0.85f), new Color(0.92f, 0.68f, 0.22f, 1f), "CONTINUAR\n[ESPACIO]", targetManager.AdvanceCombatDialogue);
		diagPanel.SetActive(false);

		targetManager.EnsureCombatGreenHealthBar();
		targetManager.SetupCombatPortraitAndWeaponUI();
		targetManager.EnforceUnifiedCombatUILayout();
		DetectiveActionMenu.EnsureInBattlePanel(targetManager);
		panelGO.SetActive(false);
		return targetManager;
	}

	private static Button CreateRuntimeVoiceButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color borderColor, string label, UnityAction onClick)
	{
		GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		btnGO.transform.SetParent(parent, false);
		RectTransform rt = btnGO.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		btnGO.GetComponent<Image>().color = borderColor;

		GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		fill.transform.SetParent(btnGO.transform, false);
		RectTransform fRT = fill.GetComponent<RectTransform>();
		fRT.anchorMin = new Vector2(0.02f, 0.06f);
		fRT.anchorMax = new Vector2(0.98f, 0.94f);
		fRT.offsetMin = Vector2.zero;
		fRT.offsetMax = Vector2.zero;
		Image fImg = fill.GetComponent<Image>();
		fImg.color = new Color(0.06f, 0.07f, 0.10f, 0.98f);

		Button btn = btnGO.GetComponent<Button>();
		btn.targetGraphic = fImg;
		btn.onClick.AddListener(onClick);

		GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		txtGO.transform.SetParent(fill.transform, false);
		RectTransform tRT = txtGO.GetComponent<RectTransform>();
		tRT.anchorMin = Vector2.zero;
		tRT.anchorMax = Vector2.one;
		tRT.offsetMin = Vector2.zero;
		tRT.offsetMax = Vector2.zero;
		TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
		tmp.text = label;
		tmp.fontSize = 18f;
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.color = Color.white;
		PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
		return btn;
	}

	public void PlayAtaqueAliadoPropioSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayAtaqueAliadoPropioSfx(sfxAtaqueAliadoPropio);
		}
		else if ((Object)(object)sfxAtaqueAliadoPropio != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxAtaqueAliadoPropio, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayAtaqueEnemigoSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayAtaqueEnemigoSfx(sfxAtaqueEnemigo);
		}
		else if ((Object)(object)sfxAtaqueEnemigo != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxAtaqueEnemigo, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayAtaqueJefeSfx()
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayAtaqueJefeSfx(sfxAtaqueJefe);
			return;
		}
		AudioClip val = (((Object)(object)sfxAtaqueJefe != (Object)null) ? sfxAtaqueJefe : sfxAtaqueEnemigo);
		if ((Object)(object)val != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(val, ((Component)Camera.main).transform.position, 0.9f);
		}
	}

	public void PlayBotonEnCombateSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayBotonEnCombateSfx(sfxBotonEnCombate);
		}
		else if ((Object)(object)sfxBotonEnCombate != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxBotonEnCombate, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayBotonConfirmacionDialogoSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayBotonConfirmacionDialogoSfx(sfxBotonConfirmacionDialogo);
		}
		else if ((Object)(object)sfxBotonConfirmacionDialogo != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxBotonConfirmacionDialogo, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayCurarseSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayCurarseSfx(sfxCurarse);
		}
		else if ((Object)(object)sfxCurarse != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxCurarse, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayEfectoSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayEfectoSfx(sfxEfecto);
		}
		else if ((Object)(object)sfxEfecto != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxEfecto, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayEsquiveSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayEsquiveSfx(sfxEsquive);
		}
		else if ((Object)(object)sfxEsquive != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxEsquive, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	private static Sprite[] LoadFilteredSpriteSequence(string sheetPath, string namePrefix)
	{
		Sprite[] allSprites = RuntimeAssetCatalog.GetSprites(sheetPath);
		List<Sprite> filtered = new List<Sprite>();
		if (allSprites != null)
		{
			for (int i = 0; i < allSprites.Length; i++)
			{
				Sprite sp = allSprites[i];
				if ((Object)(object)sp != (Object)null && sp.name.StartsWith(namePrefix, StringComparison.Ordinal))
				{
					filtered.Add(sp);
				}
			}
		}
		filtered.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
		return filtered.ToArray();
	}

	public void PlayDisparoRevolverSfx()
	{
		if (sfxDisparoRevolver38 == null)
		{
			sfxDisparoRevolver38 = RuntimeAssetCatalog.GetAudioClip("Assets/Sound/EfectodeSonido_DisparoRevolver38/Disparo_Revolver38.wav");
		}
		if (sfxDisparoRevolver38 != null && Camera.main != null)
		{
			AudioSource.PlayClipAtPoint(sfxDisparoRevolver38, Camera.main.transform.position, 0.95f);
		}
		else
		{
			PlayAtaqueAliadoPropioSfx();
		}
	}

	public void PlayRecargaRevolverSfx()
	{
		if (sfxRecargaRevolver == null)
		{
			sfxRecargaRevolver = RuntimeAssetCatalog.GetAudioClip("Assets/Sound/EfectodeSonido_RecargaRevolver/Recarga_Revolver.wav");
		}
		if (sfxRecargaRevolver != null && Camera.main != null)
		{
			AudioSource.PlayClipAtPoint(sfxRecargaRevolver, Camera.main.transform.position, 0.95f);
		}
		else
		{
			PlayEfectoSfx();
		}
	}

	public void PlayFuegoSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayFuegoSfx(sfxFuego);
		}
		else if ((Object)(object)sfxFuego != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxFuego, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayDanoFinalMuerteSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayDanoFinalMuerteSfx(sfxDanoFinalMuerte);
		}
		else if ((Object)(object)sfxDanoFinalMuerte != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxDanoFinalMuerte, ((Component)Camera.main).transform.position, 0.9f);
		}
	}

	public void PlayEncuentroSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayEncuentroSfx(sfxEncuentro);
		}
		else if ((Object)(object)sfxEncuentro != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxEncuentro, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlaySubidaDeNivelSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlaySubidaDeNivelSfx(sfxSubidaDeNivel);
		}
		else if ((Object)(object)sfxSubidaDeNivel != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxSubidaDeNivel, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public void PlayMuerteEnemigoSfx()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)MusicManager.Instance != (Object)null)
		{
			MusicManager.Instance.PlayMuerteEnemigoSfx(sfxMuerteEnemigo);
		}
		else if ((Object)(object)sfxMuerteEnemigo != (Object)null && (Object)(object)Camera.main != (Object)null)
		{
			AudioSource.PlayClipAtPoint(sfxMuerteEnemigo, ((Component)Camera.main).transform.position, 0.85f);
		}
	}

	public BattleManager()
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_029f: Unknown result type (might be due to invalid IL or missing references)
	}
}
