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

		public int enemyIndex;

		public int motricidad;
	}

	[Header("1. Estado Actual del Combate")]
	public BattleState state;

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

	private bool isCurrentEncounterBoss;

	private string currentBossNextScene;

	private string[] currentBossVictoryNarrative;

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

	private string[] currentRewardDialogueLines;

	private static bool hasAwardedStarterWeapon;

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
			sfxAtaqueAliadoPropio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_AtaqueAliadoPropio/DSGNImpt_EXPLOSION-Mecha Damage_HY_PC-001.wav");
		}
		if ((Object)(object)sfxAtaqueEnemigo == (Object)null)
		{
			sfxAtaqueEnemigo = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_AtaqueEnemigo/DSGNImpt_EXPLOSION-Forced Shutdown_HY_PC-006.wav");
		}
		if ((Object)(object)sfxAtaqueJefe == (Object)null)
		{
			sfxAtaqueJefe = MusicManager.LoadFirstAudioClipInFolderEditor("Assets/Sound/EfectodeSonido_AtaqueJefer");
		}
		if ((Object)(object)sfxBotonEnCombate == (Object)null)
		{
			sfxBotonEnCombate = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_BotonEnCombate/DSGNMisc_HIT-Bitcrusher_HY_PC-002.wav");
		}
		if ((Object)(object)sfxBotonConfirmacionDialogo == (Object)null)
		{
			sfxBotonConfirmacionDialogo = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_BotóndeConfirmaciónDialogo/JDSherbert - Pixel UI SFX Pack - Select 1 (Sine).wav");
		}
		if ((Object)(object)sfxCurarse == (Object)null)
		{
			sfxCurarse = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_Curarse/DSGNImpt_EXPLOSION-Magically_HY_PC-001.wav");
		}
		if ((Object)(object)sfxEfecto == (Object)null)
		{
			sfxEfecto = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_Efecto/DSGNMisc_HIT-Zap Laser_HY_PC-005.wav");
		}
		if ((Object)(object)sfxEsquive == (Object)null)
		{
			sfxEsquive = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_Esquive/JDSherbert - Pixel UI SFX Pack - Cursor 2 (Sine).wav");
		}
		if ((Object)(object)sfxFuego == (Object)null)
		{
			sfxFuego = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/EfectodeSonido_Fuego/DSGNImpt_EXPLOSION-Sand Impact_HY_PC-001.wav");
		}
		if ((Object)(object)victoryMenuMusic == (Object)null)
		{
			victoryMenuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/MusicaDelMenuDesubidadeNivelMenuFindeCombate/VictoryLap_Loopable.wav");
		}
		if ((Object)(object)gameOverMusic == (Object)null)
		{
			gameOverMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/MusicaGameOver/Clement Panchout _ 80s Zombies Movie _ 2018.wav");
		}
		if ((Object)(object)starterRewardWeapon == (Object)null)
		{
			starterRewardWeapon = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_CadenaBicicleta.asset");
		}
		if ((Object)(object)sfxDanoFinalMuerte == (Object)null)
		{
			sfxDanoFinalMuerte = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/SonidoDeDañoFinalMuerte/death_3_alex.wav");
		}
		if ((Object)(object)sfxEncuentro == (Object)null)
		{
			sfxEncuentro = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/SonidoDeEncuentro/DSGNImpt_EXPLOSION-Forced Interruption_HY_PC-006.wav");
		}
		if ((Object)(object)sfxSubidaDeNivel == (Object)null)
		{
			sfxSubidaDeNivel = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/SonidoDeSubidaDeNivel/MAGSpel_CAST-Skill Ready_HY_PC-001.wav");
		}
		if ((Object)(object)sfxMuerteEnemigo == (Object)null)
		{
			sfxMuerteEnemigo = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/SonidoMuerteEnemigo/grunting_2_ian.wav");
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
		if (state == BattleState.PLAYERTURN && WasToggleInventoryKeyPressed())
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

	public void StartBattle(EnemyData enemy, string[] combatIntroDialogue = null, GameObject mapEnemyObject = null, bool isBoss = false, string bossNextScene = null, string[] bossVictoryNarrative = null, ItemData rewardItem = null, string[] rewardDialogue = null)
	{
		EnemyData[] enemies = ((!((Object)(object)enemy != (Object)null)) ? Array.Empty<EnemyData>() : new EnemyData[1] { enemy });
		GameObject[] mapEnemyObjects = ((!((Object)(object)mapEnemyObject != (Object)null)) ? null : new GameObject[1] { mapEnemyObject });
		StartBattle(enemies, combatIntroDialogue, mapEnemyObjects, isBoss, bossNextScene, bossVictoryNarrative, rewardItem, rewardDialogue);
	}

	public void StartBattle(EnemyData[] enemies, string[] combatIntroDialogue = null, GameObject[] mapEnemyObjects = null, bool isBoss = false, string bossNextScene = null, string[] bossVictoryNarrative = null, ItemData rewardItem = null, string[] rewardDialogue = null)
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
			PlayEncuentroSfx();
			if ((Object)(object)MusicManager.Instance != (Object)null)
			{
				MusicManager.Instance.PlayBattleMusic();
			}
			BuildNewRoundTurnOrder();
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
		roundTurnOrder.Add(new TurnActor
		{
			isPlayer = true,
			enemyIndex = -1,
			motricidad = motricidad
		});
		for (int i = 0; i < activeEnemies.Count; i++)
		{
			if (activeEnemies[i].IsAlive)
			{
				roundTurnOrder.Add(new TurnActor
				{
					isPlayer = false,
					enemyIndex = i,
					motricidad = activeEnemies[i].data.motricidad
				});
			}
		}
		roundTurnOrder.Sort((TurnActor a, TurnActor b) =>
		{
			int num = b.motricidad.CompareTo(a.motricidad);
			if (num != 0)
			{
				return num;
			}
			if (a.isPlayer == b.isPlayer)
			{
				return a.enemyIndex.CompareTo(b.enemyIndex);
			}
			return (!a.isPlayer) ? 1 : (-1);
		});
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
			else if (item.enemyIndex >= 0 && item.enemyIndex < activeEnemies.Count && activeEnemies[item.enemyIndex].IsAlive)
			{
				list.Add($"{activeEnemies[item.enemyIndex].data.enemyName}({item.motricidad})");
			}
		}
		return string.Join(" > ", list);
	}

	private void ExecuteCurrentTurnInQueue()
	{
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
			if (turnActor.isPlayer || (turnActor.enemyIndex >= 0 && turnActor.enemyIndex < activeEnemies.Count && activeEnemies[turnActor.enemyIndex].IsAlive))
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
			SetActionButtonsInteractable(interactable: true);
			UpdateHealthUI();
			string enemyName = activeEnemies[selectedTargetIndex].data.enemyName;
			SetBattleLog("TU TURNO (Objetivo: " + enemyName + ") | Cola: " + GetTurnOrderSummaryText());
		}
		else
		{
			state = BattleState.ENEMYTURN;
			SetActionButtonsInteractable(interactable: false);
			((MonoBehaviour)this).StartCoroutine(EnemyTurnCoroutine(turnActor2.enemyIndex));
		}
	}

	private void AdvanceTurnQueueAndContinue()
	{
		currentTurnCursor++;
		ExecuteCurrentTurnInQueue();
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
				int num5 = Mathf.Max(1, num4 - target.data.defense);
				target.currentHP = Mathf.Max(0, target.currentHP - num5);
				currentEnemy = target.data;
				currentEnemyHP = target.currentHP;
				string text = ((num2 > 0 && (Object)(object)equippedWeapon != (Object)null) ? $" (+{num2} por {equippedWeapon.itemName})" : "");
				string text2 = ((num3 > 0) ? $" (+{num3} bonus por estar ANIMADO)" : "");
				playerAttackBuff = 0;
				string text3 = (((Object)(object)equippedWeapon != (Object)null) ? ("¡Golpe contundente con " + equippedWeapon.itemName + "!") : "¡Golpe contundente a puño limpio!");
				SetBattleLog($"CORPOREO{weaponTag}: {text3} Infliges {num5} de dano fisico a {target.data.enemyName}{text}{text2}.");
				EnsureValidLivingTarget();
				UpdateHealthUI();
				CheckPostPlayerActionFlow(target);
			}));
		}
	}

	public void OnIntelectoButton()
	{
		if (state == BattleState.PLAYERTURN)
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
		if (state == BattleState.PLAYERTURN && activeEnemies.Count > 0)
		{
			EnsureValidLivingTarget();
			string enemyName = activeEnemies[selectedTargetIndex].data.enemyName;
			SetBattleLog("TU TURNO (Objetivo: " + enemyName + ") | Cola: " + GetTurnOrderSummaryText());
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

	public void UseConsumableInCombat(ItemData item)
	{
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		if (state != BattleState.PLAYERTURN || (Object)(object)item == (Object)null || ((Object)(object)InventoryManager.Instance != (Object)null && !InventoryManager.Instance.HasItem(item)))
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
		else
		{
			PlayAtaqueAliadoPropioSfx();
		}
		ActionGifData gifData = new ActionGifData
		{
			actionLabel = isHealingItem ? ("[GIF PLACEHOLDER: Enrique usando objeto curativo: " + item.itemName + "]") : ("[GIF: Enrique arrojando artefacto ofensivo: " + item.itemName + "]"),
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
				if ((Object)(object)playerStats != (Object)null)
				{
					playerStats.Heal(num2);
				}
				string arg = ((num > 0) ? $" (+{num} por Intelecto)" : "");
				string arg2 = ((!string.IsNullOrWhiteSpace(item.combatUseMessage)) ? item.combatUseMessage : $"Enrique usa {item.itemName} y recupera +{num2} HP!");
				SetBattleLog($"{arg2} (+{num2} HP{arg})");
				UpdateHealthUI();
				AdvanceTurnQueueAndContinue();
			}
			else
			{
				int num3 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetIntelectoDamageBonus() : 0);
				int num4 = Mathf.Max(1, item.damageAmount + num3);
				target.currentHP = Mathf.Max(0, target.currentHP - num4);
				currentEnemy = target.data;
				currentEnemyHP = target.currentHP;
				string battleLog = ((!string.IsNullOrWhiteSpace(item.combatUseMessage)) ? item.combatUseMessage : $"Enrique lanza {item.itemName} contra {target.data.enemyName} ({num4} dano).");
				SetBattleLog(battleLog);
				EnsureValidLivingTarget();
				UpdateHealthUI();
				CheckPostPlayerActionFlow(target);
			}
		}));
	}

	public void CraftAndUseRecipeInCombat(CraftingRecipeData recipe)
	{
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		if (state != BattleState.PLAYERTURN || (Object)(object)recipe == (Object)null || ((Object)(object)InventoryManager.Instance != (Object)null && !InventoryManager.Instance.CanCraft(recipe)))
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
			actionLabel = flag ? ("[GIF: INTELECTO - Explosion de " + recipe.recipeName + "]") : ("[GIF PLACEHOLDER: Intelecto fabricando y usando " + recipe.recipeName + "]"),
			placeholderColor = (flag ? new Color(0.88f, 0.32f, 0.08f, 1f) : new Color(0.08f, 0.58f, 0.78f, 1f)),
			gifFrames = (flag && explosionGif != null && explosionGif.gifFrames != null && explosionGif.gifFrames.Length != 0) ? explosionGif.gifFrames : ((intelectoGif != null) ? intelectoGif.gifFrames : null),
			duration = (flag && explosionGif != null) ? explosionGif.duration : 1.5f
		};
		((MonoBehaviour)this).StartCoroutine(ExecuteActionWithGifCoroutine(gifData, () =>
		{
			List<string> list = new List<string>();
			if (recipe.damageIgnoringDefense > 0)
			{
				int num = (((Object)(object)playerStats != (Object)null) ? playerStats.GetIntelectoDamageBonus() : 0);
				int num2 = recipe.damageIgnoringDefense + num;
				target.currentHP = Mathf.Max(0, target.currentHP - num2);
				currentEnemy = target.data;
				currentEnemyHP = target.currentHP;
				list.Add($"{num2} dano por fuego ignorando defensa a {target.data.enemyName}");
			}
			if (recipe.healAmount > 0 && (Object)(object)playerStats != (Object)null)
			{
				int intelectoHealBonus = playerStats.GetIntelectoHealBonus();
				int num3 = recipe.healAmount + intelectoHealBonus;
				playerStats.Heal(num3);
				list.Add($"+{num3} HP para Enrique");
			}
			if (recipe.appliesConfusion && target.IsAlive)
			{
				target.isConfused = true;
				list.Add(target.data.enemyName + " queda CONFUNDIDO (1 turno)");
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
	}

	public void OnMundoInternoButton()
	{
		if (state == BattleState.PLAYERTURN)
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
				SetBattleLog("MUNDO INTERNO: Elige una habilidad psicologica (Insultar, Animarse o Delirio Psiquico).");
			}
			else if ((Object)(object)mundoInternoPanel != (Object)null)
			{
				mundoInternoPanel.SetActive(true);
				SetBattleLog("MUNDO INTERNO: Elige una habilidad psicologica.");
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
		if (state == BattleState.PLAYERTURN && activeEnemies.Count > 0)
		{
			EnsureValidLivingTarget();
			string enemyName = activeEnemies[selectedTargetIndex].data.enemyName;
			SetBattleLog("TU TURNO (Objetivo: " + enemyName + ") | Cola: " + GetTurnOrderSummaryText());
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
		float num = ((gifData != null && gifData.duration > 0f) ? gifData.duration : 1.5f);
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
		enemyAttackGif.actionLabel = $"[GIF: {attacker.data.enemyName.ToUpper()} (Motricidad {attacker.data.motricidad}) atacando a Enrique]";
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
		int num = Mathf.Max(1, attacker.data.attack - attacker.attackDebuff);
		string text = ((attacker.attackDebuff > 0) ? $" (-{attacker.attackDebuff} por insulto)" : "");
		if (isMotricidadDefending)
		{
			float num2 = (((Object)(object)playerStats != (Object)null) ? playerStats.GetDodgeChanceAgainst(attacker.data.motricidad) : Mathf.Clamp(0.25f + (float)(12 - attacker.data.motricidad) * 0.05f, 0.15f, 0.75f));
			bool flag = Random.value <= num2;
			PlayEsquiveSfx();
			if (flag)
			{
				SetBattleLog("MOTRICIDAD: Enrique ESQUIVA por completo el ataque de " + attacker.data.enemyName + " (0 dano).");
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
				SetBattleLog($"MOTRICIDAD: Guardia defensiva reduce un {num4}% el golpe de {attacker.data.enemyName} ({num5} dano{text}).");
			}
		}
		else
		{
			if ((Object)(object)playerStats != (Object)null)
			{
				playerStats.TakeDamage(num);
			}
			SetBattleLog($"TURNO DE {attacker.data.enemyName.ToUpper()}: Ataca a Enrique e inflige {num} de dano{text}.");
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
			Sprite val = (text.Contains("ENRIQUE") ? enriquePortrait : null);
			if ((Object)(object)combatPortraitImage != (Object)null)
			{
				if ((Object)(object)val != (Object)null)
				{
					combatPortraitImage.sprite = val;
					((Graphic)combatPortraitImage).color = Color.white;
					combatPortraitImage.preserveAspect = false;
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
		if (won)
		{
			state = BattleState.WON;
			int xpAwarded = 0;
			int num = 0;
			if ((Object)(object)playerStats == (Object)null)
			{
				playerStats = Object.FindAnyObjectByType<PlayerStats>();
			}
			if ((Object)(object)playerStats != (Object)null)
			{
				num = playerStats.GainExperienceFromBattle(activeEnemies.Count, isCurrentEncounterBoss, out xpAwarded);
			}
			UpdateHealthUI();
			if (num > 0 && (Object)(object)playerStats != (Object)null)
			{
				SetBattleLog($"¡VICTORIA (+{xpAwarded} EXP)! ¡ENRIQUE SUBE AL NIVEL {playerStats.level}! Preparando selección de mejora...");
			}
			else
			{
				SetBattleLog($"VICTORIA (+{xpAwarded} EXP). Todos los enemigos han caido.");
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

	private IEnumerator ShowPostCombatFlowCoroutine(int levelsGained, int xpAwarded)
	{
		yield return (object)new WaitForSeconds(0.85f);
		ItemData itemData = currentRewardItem;
		string[] array = currentRewardDialogueLines;
		if ((Object)(object)itemData == (Object)null && !hasAwardedStarterWeapon)
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
		if ((Object)(object)itemData != (Object)null && ((Object)(object)InventoryManager.Instance == (Object)null || !InventoryManager.Instance.IsWeaponUnlocked(itemData)))
		{
			hasAwardedStarterWeapon = true;
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
				array = new string[3]
				{
					"NARRADOR: \"¡El lacayo cae al suelo inconsciente! De su cinturón se desprende una pesada " + itemData.itemName.ToUpper() + " con eslabones de acero engrasado. Enrique la recoge del asfalto.\"",
					$"VOZ [CORPOREO]: \"¡Nuestra primera arma callejera! Con esta {itemData.itemName} (+{itemData.weaponAttackBonus} ATQ, +{itemData.weaponMotricidadBonus} Motricidad) nuestros golpes seran mas letales y rapidos.\"",
					"VOZ [INTELECTO]: \"¡" + itemData.itemName.ToUpper() + " OBTENIDA Y EQUIPADA! Se ha colocado en tus manos. Puedes abrir el menú [INVENTARIO / TAB] en cualquier momento para ver tu equipamiento, o hacer clic en tu retrato para cambiarla.\""
				};
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
		if (levelsGained > 0 && (Object)(object)playerStats != (Object)null)
		{
			PlaySubidaDeNivelSfx();
			string[] lines = new string[2]
			{
				$"NARRADOR: \"¡VICTORIA! Has ganado +{xpAwarded} EXP. ¡ENRIQUE BOSQUES SUBE AL NIVEL {playerStats.level}!\"",
				"ENRIQUE BOSQUES: \"Siento como las cuatro voces cobran mas fuerza en mi mente... Debo elegir cual de mis habilidades se volvera mas poderosa.\""
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

	private void SetupEnemyVisuals()
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
		if ((Object)(object)playerCombatPortraitImage != (Object)null)
		{
			if ((Object)(object)enriquePortrait != (Object)null && (Object)(object)playerCombatPortraitImage.sprite == (Object)null)
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
				if (state == BattleState.PLAYERTURN)
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
		if (!((Object)(object)battlePanel != (Object)null) || !((Object)(object)combatInventoryButtonRoot == (Object)null))
		{
			return;
		}
		Transform val5 = battlePanel.transform.Find("Combat_InventoryAndWeaponsButton");
		if ((Object)(object)val5 != (Object)null)
		{
			combatInventoryButtonRoot = ((Component)val5).gameObject;
			combatInventoryButton = combatInventoryButtonRoot.GetComponent<Button>();
			combatInventoryButtonLabel = combatInventoryButtonRoot.GetComponentInChildren<TextMeshProUGUI>();
			combatInventoryButtonBorder = combatInventoryButtonRoot.GetComponent<Image>();
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
			if (state == BattleState.PLAYERTURN)
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
		if ((Object)(object)playerStats != (Object)null)
		{
			if ((Object)(object)playerHealthBarUI != (Object)null)
			{
				playerHealthBarUI.healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);
				playerHealthBarUI.percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);
				playerHealthBarUI.UpdateHealth(playerStats.currentHP, playerStats.maxHP, isMotricidadDefending, playerAttackBuff);
			}
			else if ((Object)(object)playerHPText != (Object)null)
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
		if ((Object)(object)corporeoButton != (Object)null)
		{
			TextMeshProUGUI componentInChildren = ((Component)corporeoButton).GetComponentInChildren<TextMeshProUGUI>();
			if ((Object)(object)componentInChildren != (Object)null)
			{
				if ((Object)(object)itemData != (Object)null)
				{
					((TMP_Text)componentInChildren).text = $"CORPÓREO [{itemData.itemName.ToUpper()}]\n<size=15>Atacar ({num3}+{num} = {num2} ATQ)</size>";
				}
				else
				{
					((TMP_Text)componentInChildren).text = $"CORPÓREO\n<size=15>Atacar (Puños: {num2} ATQ)</size>";
				}
			}
		}
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
					((TMP_Text)enemyUISlot.enemyHPText).text = $"HP: {combatEnemyInstance.currentHP} / {combatEnemyInstance.data.maxHP}{text}{text2}";
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
			#if UNITY_EDITOR
			if ((Object)(object)((TMP_Text)val6).font == (Object)null)
			{
				((TMP_Text)val6).font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Pixelzone_TMP.asset");
			}
			#endif
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
		if ((Object)(object)val12 == (Object)null)
		{
			GameObject val13 = new GameObject("DamageFill", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val13.transform.SetParent(val10, false);
			val12 = val13.transform;
		}
		RectTransform component8 = ((Component)val12).GetComponent<RectTransform>();
		component8.anchorMin = Vector2.zero;
		component8.anchorMax = Vector2.one;
		component8.offsetMin = Vector2.zero;
		component8.offsetMax = Vector2.zero;
		Image component9 = ((Component)val12).GetComponent<Image>();
		if ((Object)(object)component9 != (Object)null)
		{
			((Graphic)component9).color = new Color(0.88f, 0.14f, 0.14f, 0.75f);
			((Graphic)component9).raycastTarget = false;
		}
		Transform val14 = val10.Find("MainFill");
		if ((Object)(object)val14 == (Object)null)
		{
			GameObject val15 = new GameObject("MainFill", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val15.transform.SetParent(val10, false);
			val14 = val15.transform;
		}
		RectTransform component10 = ((Component)val14).GetComponent<RectTransform>();
		component10.anchorMin = Vector2.zero;
		component10.anchorMax = Vector2.one;
		component10.offsetMin = Vector2.zero;
		component10.offsetMax = Vector2.zero;
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

	private void SetBattleLog(string message)
	{
		if ((Object)(object)battleLogText != (Object)null)
		{
			((TMP_Text)battleLogText).text = message;
		}
	}

	private void SetActionButtonsInteractable(bool interactable)
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
		if (attacker.data.enemyType == EnemyType.JefeElCalacas)
		{
			return true;
		}
		if (!string.IsNullOrEmpty(attacker.data.enemyName))
		{
			string text = attacker.data.enemyName.ToUpperInvariant();
			if (text.Contains("JEFE") || text.Contains("BOSS") || text.Contains("CALACAS") || text.Contains("COMANDANTE"))
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
