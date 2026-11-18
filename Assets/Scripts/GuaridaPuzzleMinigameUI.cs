using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Sistema de Puzzle Noir simplificado para la única terminal del Nivel 3 (La Guarida de El Calacas).
/// Mecánica intuitiva de doble paso:
/// 1. Conectar los 4 cables de colores con sus puertos correspondientes en la derecha.
/// 2. Activar los 3 disyuntores de corte para desenergizar la barrera láser.
/// Respeta la tipografía Pixelzone_TMP y el estilo visual Noir del juego.
/// </summary>
public class GuaridaPuzzleMinigameUI : MonoBehaviour
{
    public static GuaridaPuzzleMinigameUI Instance { get; private set; }

    [Header("Referencias del Mundo")]
    public GuaridaSecurityGateInteractable linkedGate;

    [Header("Estado del Puzzle")]
    public bool isPuzzleOpen = false;
    public bool isSolved = false;

    private const int CableCount = 4;
    private const int BreakerCount = 3;

    private readonly string[] cableColorNames = new string[] { "ROJO", "CIAN", "ÁMBAR", "VERDE" };
    private readonly string[] breakerNames = new string[]
    {
        "DISYUNTOR 01: LÁSERES",
        "DISYUNTOR 02: CERROJO",
        "DISYUNTOR 03: ALARMA"
    };

    private static readonly Color[] CablePalette = new Color[]
    {
        new Color(0.95f, 0.25f, 0.25f, 1f), // Rojo
        new Color(0.20f, 0.85f, 1.00f, 1f), // Cian
        new Color(1.00f, 0.82f, 0.25f, 1f), // Ámbar
        new Color(0.25f, 0.95f, 0.45f, 1f)  // Verde
    };

    // Estado lógico del puzzle de cables + disyuntores
    private int[] rightSocketToColor = new int[] { 2, 0, 3, 1 };
    private bool[] wireConnected = new bool[CableCount];
    private bool[] breakerActive = new bool[BreakerCount];
    private int selectedLeftColor = -1;
    private int selectedRightSlot = -1;
    private bool stateInitialized = false;
    private bool hintUsed = false;

    // Referencias UI
    private Canvas puzzleCanvas;
    private GameObject modalRoot;
    private TextMeshProUGUI statusFeedbackTMP;
    private TextMeshProUGUI speakerLabelTMP;
    private Image speakerPortraitImg;
    private TextMeshProUGUI targetSignatureTMP;
    private TextMeshProUGUI breakerSummaryTMP;

    private Button[] leftCableButtons = new Button[CableCount];
    private Image[] leftCableBorders = new Image[CableCount];
    private TextMeshProUGUI[] leftCableTexts = new TextMeshProUGUI[CableCount];
    private Image[] leftPinDots = new Image[CableCount];

    private Button[] rightSocketButtons = new Button[CableCount];
    private Image[] rightSocketBorders = new Image[CableCount];
    private TextMeshProUGUI[] rightSocketTexts = new TextMeshProUGUI[CableCount];
    private Image[] rightPinDots = new Image[CableCount];

    private Image[] wireLineImages = new Image[CableCount];
    private readonly Vector2[] leftPinPositions = new Vector2[CableCount];
    private readonly Vector2[] rightPinPositions = new Vector2[CableCount];

    private Button[] breakerButtons = new Button[BreakerCount];
    private Image[] breakerBorders = new Image[BreakerCount];
    private TextMeshProUGUI[] breakerTexts = new TextMeshProUGUI[BreakerCount];

    private Button unlockMasterBtn;
    private Image unlockMasterBorder;
    private TextMeshProUGUI unlockMasterText;
    private Button intelAutoStabilizeBtn;

    // Paleta Noir Estandarizada
    private static readonly Color NoirBgBlack = new Color(0.05f, 0.05f, 0.06f, 0.98f);
    private static readonly Color NoirInnerCard = new Color(0.08f, 0.08f, 0.10f, 1f);
    private static readonly Color BorderCyan = new Color(0.20f, 0.85f, 1.00f, 1f);
    private static readonly Color BorderGold = new Color(1.00f, 0.82f, 0.25f, 1f);
    private static readonly Color BorderGreen = new Color(0.25f, 0.95f, 0.45f, 1f);
    private static readonly Color BorderRed = new Color(0.95f, 0.25f, 0.25f, 1f);
    private static readonly Color BorderMuted = new Color(0.35f, 0.38f, 0.44f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildPuzzleUI();
        ClosePuzzleImmediate();
    }

    private void Update()
    {
        if (!isPuzzleOpen) return;

        bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            escPressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            escPressed = true;
#endif

        if (escPressed)
        {
            ClosePuzzle();
        }
    }

    public static GuaridaPuzzleMinigameUI GetOrCreate()
    {
        if (Instance != null) return Instance;
        GuaridaPuzzleMinigameUI existing = FindAnyObjectByType<GuaridaPuzzleMinigameUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("GuaridaPuzzleMinigameUI_System");
        return go.AddComponent<GuaridaPuzzleMinigameUI>();
    }

    public void OpenPuzzle(GuaridaSecurityGateInteractable gate)
    {
        linkedGate = gate;
        if (puzzleCanvas == null || modalRoot == null)
        {
            BuildPuzzleUI();
        }

        EnsureEventSystemReady();

        if (!isSolved && !stateInitialized)
        {
            ResetPuzzleState(false);
        }

        isPuzzleOpen = true;
        modalRoot.SetActive(true);

        // Pausar movimiento del jugador mientras resuelve el puzzle
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc != null) pc.SetCanMove(false);

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();

        SetFeedbackMessage(
            "DETECTIVE VARGAS",
            "«Esta terminal controla la compuerta láser, Bosques. Une cada cable con el puerto del mismo color a la derecha y corta los 3 disyuntores para abrir el paso.»",
            BorderGold,
            false
        );

        RefreshUIState();
    }

    public void ClosePuzzle()
    {
        if (!isPuzzleOpen) return;
        isPuzzleOpen = false;

        if (modalRoot != null)
            modalRoot.SetActive(false);

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayBotonEnCombateSfx();

        // Restaurar movimiento del jugador si no hay diálogo ni combate activo
        bool dialogueBusy = ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive;
        BattleManager bm = FindAnyObjectByType<BattleManager>();
        bool battleBusy = bm != null && bm.state != BattleState.INACTIVE;

        if (!dialogueBusy && !battleBusy)
        {
            PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
            if (pc != null) pc.SetCanMove(true);
        }
    }

    private void ClosePuzzleImmediate()
    {
        isPuzzleOpen = false;
        if (modalRoot != null)
            modalRoot.SetActive(false);
    }

    public void ResetPuzzleState(bool playSound = true)
    {
        if (isSolved) return;

        for (int i = 0; i < CableCount; i++)
            wireConnected[i] = false;

        for (int i = 0; i < BreakerCount; i++)
            breakerActive[i] = false;

        selectedLeftColor = -1;
        selectedRightSlot = -1;
        stateInitialized = true;
        ShuffleRightSockets();

        if (playSound && MusicManager.Instance != null)
            MusicManager.Instance.PlayBotonEnCombateSfx();

        SetFeedbackMessage(
            "VOZ [INTELECTO]",
            "«Panel reiniciado. Selecciona un cable a la izquierda y conéctalo con su puerto del mismo color, luego activa los 3 disyuntores.»",
            BorderCyan,
            true
        );

        RefreshUIState();
    }

    private void ShuffleRightSockets()
    {
        rightSocketToColor = new int[] { 0, 1, 2, 3 };
        for (int i = CableCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = rightSocketToColor[i];
            rightSocketToColor[i] = rightSocketToColor[j];
            rightSocketToColor[j] = tmp;
        }

        // Evitar que queden todos alineados horizontalmente sin cruce
        bool allIdentity = true;
        for (int i = 0; i < CableCount; i++)
        {
            if (rightSocketToColor[i] != i)
            {
                allIdentity = false;
                break;
            }
        }
        if (allIdentity)
        {
            rightSocketToColor = new int[] { 2, 0, 3, 1 };
        }
    }

    private void OnLeftCableClicked(int colorIdx)
    {
        if (isSolved || colorIdx < 0 || colorIdx >= CableCount) return;

        if (wireConnected[colorIdx])
        {
            SetFeedbackMessage(
                "VOZ [INTELECTO]",
                $"«El cable {cableColorNames[colorIdx]} ya está conectado a su puerto correspondiente.»",
                CablePalette[colorIdx],
                true
            );
            return;
        }

        if (selectedRightSlot >= 0)
        {
            TryConnectPair(colorIdx, selectedRightSlot);
            return;
        }

        selectedLeftColor = (selectedLeftColor == colorIdx) ? -1 : colorIdx;

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayBotonEnCombateSfx();

        if (selectedLeftColor >= 0)
        {
            SetFeedbackMessage(
                "VOZ [INTELECTO]",
                $"«Cable {cableColorNames[colorIdx]} listo. Haz clic ahora en el puerto {cableColorNames[colorIdx]} de la derecha.»",
                CablePalette[colorIdx],
                true
            );
        }

        RefreshUIState();
    }

    private void OnRightSocketClicked(int slotIdx)
    {
        if (isSolved || slotIdx < 0 || slotIdx >= CableCount) return;

        int targetColor = rightSocketToColor[slotIdx];
        if (wireConnected[targetColor])
        {
            SetFeedbackMessage(
                "VOZ [INTELECTO]",
                $"«El puerto {cableColorNames[targetColor]} ya tiene su cable conectado.»",
                CablePalette[targetColor],
                true
            );
            return;
        }

        if (selectedLeftColor >= 0)
        {
            TryConnectPair(selectedLeftColor, slotIdx);
            return;
        }

        selectedRightSlot = (selectedRightSlot == slotIdx) ? -1 : slotIdx;

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayBotonEnCombateSfx();

        if (selectedRightSlot >= 0)
        {
            SetFeedbackMessage(
                "VOZ [INTELECTO]",
                $"«Puerto {cableColorNames[targetColor]} seleccionado. Haz clic en el cable {cableColorNames[targetColor]} de la izquierda.»",
                CablePalette[targetColor],
                true
            );
        }

        RefreshUIState();
    }

    private void TryConnectPair(int leftColorIdx, int rightSlotIdx)
    {
        int rightColorIdx = rightSocketToColor[rightSlotIdx];
        selectedLeftColor = -1;
        selectedRightSlot = -1;

        if (leftColorIdx == rightColorIdx)
        {
            wireConnected[leftColorIdx] = true;
            if (MusicManager.Instance != null)
                MusicManager.Instance.PlayEfectoSfx();

            int wired = CountConnectedWires();
            int activeBreakers = CountActiveBreakers();
            if (wired == CableCount && activeBreakers == BreakerCount)
            {
                NotifyCircuitReady();
            }
            else if (wired == CableCount)
            {
                SetFeedbackMessage(
                    "DETECTIVE VARGAS",
                    "«¡Todos los cables están empalmados! Solo falta cortar los disyuntores de la derecha para desactivar los láseres.»",
                    BorderGreen,
                    false
                );
            }
            else
            {
                SetFeedbackMessage(
                    "VOZ [INTELECTO]",
                    $"«Conexión {cableColorNames[leftColorIdx]} asegurada ({wired}/{CableCount} cables listos). Continúa con el siguiente color.»",
                    CablePalette[leftColorIdx],
                    true
                );
            }
        }
        else
        {
            if (MusicManager.Instance != null)
                MusicManager.Instance.PlayBotonEnCombateSfx();

            SetFeedbackMessage(
                "VOZ [MOTRICIDAD]",
                $"«¡Chispazo de polaridad! El cable {cableColorNames[leftColorIdx]} no encaja con el puerto {cableColorNames[rightColorIdx]}. Conecta siempre colores iguales.»",
                BorderRed,
                true
            );
        }

        RefreshUIState();
    }

    private void OnBreakerClicked(int breakerIdx)
    {
        if (isSolved || breakerIdx < 0 || breakerIdx >= BreakerCount) return;

        breakerActive[breakerIdx] = !breakerActive[breakerIdx];

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayBotonEnCombateSfx();

        int wired = CountConnectedWires();
        int activeBreakers = CountActiveBreakers();

        if (wired == CableCount && activeBreakers == BreakerCount)
        {
            NotifyCircuitReady();
        }
        else if (breakerActive[breakerIdx])
        {
            SetFeedbackMessage(
                "DETECTIVE VARGAS",
                $"«{breakerNames[breakerIdx]} cortado ({activeBreakers}/{BreakerCount} disyuntores).»",
                BorderGold,
                false
            );
        }
        else
        {
            SetFeedbackMessage(
                "VOZ [MOTRICIDAD]",
                $"«Has vuelto a cerrar {breakerNames[breakerIdx]}. Todos los disyuntores deben quedar en verde.»",
                BorderCyan,
                true
            );
        }

        RefreshUIState();
    }

    private void NotifyCircuitReady()
    {
        SetFeedbackMessage(
            "VOZ [INTELECTO]",
            "«¡Circuito 100% operativo y disyuntores cortados! Pulsa [EJECUTAR DESBLOQUEO DE COMPUERTA] para apagar los láseres.»",
            BorderGreen,
            true
        );
    }

    private void OnIntelAutoAssistClicked()
    {
        if (isSolved) return;

        // Conectar automáticamente hasta 2 cables pendientes y activar hasta 2 disyuntores pendientes
        int autoWires = 0;
        for (int i = 0; i < CableCount; i++)
        {
            if (!wireConnected[i])
            {
                wireConnected[i] = true;
                autoWires++;
                if (autoWires >= 2) break;
            }
        }

        int autoBreakers = 0;
        for (int i = 0; i < BreakerCount; i++)
        {
            if (!breakerActive[i])
            {
                breakerActive[i] = true;
                autoBreakers++;
                if (autoBreakers >= 2) break;
            }
        }

        selectedLeftColor = -1;
        selectedRightSlot = -1;
        hintUsed = true;

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayEfectoSfx();

        if (CountConnectedWires() == CableCount && CountActiveBreakers() == BreakerCount)
        {
            NotifyCircuitReady();
        }
        else
        {
            SetFeedbackMessage(
                "VOZ [INTELECTO]",
                "«He empalmado parte del cableado y reducido la carga de los disyuntores. Termina las conexiones restantes.»",
                BorderCyan,
                true
            );
        }

        RefreshUIState();
    }

    private void OnMasterUnlockClicked()
    {
        if (isSolved)
        {
            ClosePuzzle();
            return;
        }

        int wired = CountConnectedWires();
        int activeBreakers = CountActiveBreakers();
        if (wired < CableCount || activeBreakers < BreakerCount)
        {
            if (MusicManager.Instance != null)
                MusicManager.Instance.PlayBotonEnCombateSfx();

            SetFeedbackMessage(
                "VOZ [MOTRICIDAD]",
                $"«Todavía falta completar el panel: Cables ({wired}/{CableCount}) y Disyuntores ({activeBreakers}/{BreakerCount}).»",
                BorderRed,
                true
            );
            return;
        }

        isSolved = true;
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlaySubidaDeNivelSfx();

        RefreshUIState();

        SetFeedbackMessage(
            "DETECTIVE VARGAS",
            "«¡Listo, Bosques! Los láseres cayeron y la compuerta blindada acaba de desbloquearse.»",
            BorderGreen,
            false
        );

        StartCoroutine(CompletePuzzleRoutine());
    }

    private IEnumerator CompletePuzzleRoutine()
    {
        yield return new WaitForSeconds(0.55f);

        ClosePuzzle();

        if (linkedGate != null)
        {
            linkedGate.UnlockSecurityGateAndRewardPlayer(!hintUsed);
        }
    }

    private int CountConnectedWires()
    {
        int count = 0;
        for (int i = 0; i < CableCount; i++)
        {
            if (wireConnected[i]) count++;
        }
        return count;
    }

    private int CountActiveBreakers()
    {
        int count = 0;
        for (int i = 0; i < BreakerCount; i++)
        {
            if (breakerActive[i]) count++;
        }
        return count;
    }

    private int FindRightSlotForColor(int colorIdx)
    {
        for (int slot = 0; slot < CableCount; slot++)
        {
            if (rightSocketToColor[slot] == colorIdx)
                return slot;
        }
        return 0;
    }

    private void RefreshUIState()
    {
        int wired = CountConnectedWires();
        int activeBreakers = CountActiveBreakers();
        bool allReady = (wired == CableCount && activeBreakers == BreakerCount);

        if (targetSignatureTMP != null)
        {
            targetSignatureTMP.text =
                $"CONSOLA MAESTRA  —  CABLES EMPALMADOS: <color=#40FF75>{wired}/{CableCount}</color>   |   DISYUNTORES CORTADOS: <color=#FFD940>{activeBreakers}/{BreakerCount}</color>";
        }

        // Actualizar conectores izquierdos (colores fijos 0..3)
        for (int c = 0; c < CableCount; c++)
        {
            bool isDone = wireConnected[c];
            bool isSel = (selectedLeftColor == c);
            Color baseColor = CablePalette[c];

            if (leftCableBorders[c] != null)
                leftCableBorders[c].color = isSel ? Color.white : (isDone ? BorderGreen : baseColor);

            if (leftPinDots[c] != null)
                leftPinDots[c].color = isDone ? baseColor : (isSel ? Color.white : baseColor * 0.7f);

            if (leftCableTexts[c] != null)
            {
                string tag = isDone ? "<color=#45FF75>[CONECTADO]</color>" : (isSel ? "<color=#FFFFFF>[ELIGE PUERTO]</color>" : "[SELECCIONAR]");
                leftCableTexts[c].text = $"CABLE {cableColorNames[c]}\n{tag}";
            }
        }

        // Actualizar puertos derechos (colores mezclados) y líneas de cable conectadas
        for (int slot = 0; slot < CableCount; slot++)
        {
            int targetColor = rightSocketToColor[slot];
            bool isDone = wireConnected[targetColor];
            bool isSel = (selectedRightSlot == slot);
            Color baseColor = CablePalette[targetColor];

            if (rightSocketBorders[slot] != null)
                rightSocketBorders[slot].color = isSel ? Color.white : (isDone ? BorderGreen : baseColor);

            if (rightPinDots[slot] != null)
                rightPinDots[slot].color = isDone ? baseColor : (isSel ? Color.white : baseColor * 0.7f);

            if (rightSocketTexts[slot] != null)
            {
                string tag = isDone ? "<color=#45FF75>[CONECTADO]</color>" : (isSel ? "<color=#FFFFFF>[ELIGE CABLE]</color>" : "[PUERTO LIBRE]");
                rightSocketTexts[slot].text = $"PUERTO {cableColorNames[targetColor]}\n{tag}";
            }
        }

        // Actualizar trazado de cables conectados entre terminales izquierda y derecha
        for (int c = 0; c < CableCount; c++)
        {
            Image wireImg = wireLineImages[c];
            if (wireImg == null) continue;

            wireImg.gameObject.SetActive(wireConnected[c]);
            if (wireConnected[c])
            {
                int slot = FindRightSlotForColor(c);
                PositionWireSegment(wireImg.rectTransform, leftPinPositions[c], rightPinPositions[slot], 8f);
                wireImg.color = CablePalette[c];
            }
        }

        // Actualizar 3 Disyuntores
        for (int b = 0; b < BreakerCount; b++)
        {
            bool on = breakerActive[b];
            if (breakerBorders[b] != null)
                breakerBorders[b].color = on ? BorderGreen : BorderRed;

            if (breakerTexts[b] != null)
            {
                string stateTag = on ? "<color=#45FF75>[ CORTADO / OK ]</color>" : "<color=#FF5555>[ BLOQUEADO / PULSAR ]</color>";
                breakerTexts[b].text = $"{breakerNames[b]}\n{stateTag}";
            }
        }

        if (breakerSummaryTMP != null)
        {
            breakerSummaryTMP.text = allReady
                ? "<color=#45FF75>SISTEMA LISTO PARA DESBLOQUEO</color>"
                : $"PENDIENTE: {CableCount - wired} CABLE(S)  |  {BreakerCount - activeBreakers} DISYUNTOR(ES)";
        }

        if (unlockMasterBorder != null && unlockMasterText != null)
        {
            unlockMasterBorder.color = allReady ? BorderGreen : BorderMuted;
            unlockMasterText.text = allReady
                ? "[ EJECUTAR DESBLOQUEO DE COMPUERTA ]"
                : $"[ BLOQUEADO — CABLES {wired}/{CableCount} | CORTES {activeBreakers}/{BreakerCount} ]";
            unlockMasterText.color = allReady ? new Color(0.35f, 1f, 0.55f, 1f) : new Color(0.65f, 0.65f, 0.70f, 1f);
        }
    }

    private static void PositionWireSegment(RectTransform wireRT, Vector2 startPos, Vector2 endPos, float thickness)
    {
        Vector2 delta = endPos - startPos;
        float length = delta.magnitude;
        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        wireRT.anchoredPosition = (startPos + endPos) * 0.5f;
        wireRT.sizeDelta = new Vector2(length, thickness);
        wireRT.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    private void SetFeedbackMessage(string speaker, string message, Color speakerColor, bool useIntelPortrait)
    {
        if (speakerLabelTMP != null)
        {
            speakerLabelTMP.text = speaker;
            speakerLabelTMP.color = speakerColor;
        }
        if (statusFeedbackTMP != null)
        {
            statusFeedbackTMP.text = message;
        }
        if (speakerPortraitImg != null)
        {
            Sprite sp = useIntelPortrait
                ? Capitulo3GuaridaNarrative.LoadSpriteResource("Assets/Portraits/intelecto.png")
                : Capitulo3GuaridaNarrative.LoadSpriteResource("Assets/Portraits/Detective.png");

            if (sp != null)
            {
                speakerPortraitImg.sprite = sp;
                speakerPortraitImg.color = Color.white;
            }
        }
    }

    // =========================================================================================
    // CONSTRUCCIÓN PROCEDURAL DE LA UI NOIR (100% PIXELZONE_TMP Y ESTILO DEL JUEGO)
    // =========================================================================================

    private void BuildPuzzleUI()
    {
        TMP_FontAsset pixelFont = PlayerHealthBarUI.GetCleanPixelFont();

        GameObject canvasGO = new GameObject("GuaridaPuzzleCanvas");
        canvasGO.transform.SetParent(transform, false);

        puzzleCanvas = canvasGO.AddComponent<Canvas>();
        puzzleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        puzzleCanvas.sortingOrder = 140;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        modalRoot = new GameObject("PuzzleModalRoot");
        modalRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform backdropRT = modalRoot.AddComponent<RectTransform>();
        backdropRT.anchorMin = Vector2.zero;
        backdropRT.anchorMax = Vector2.one;
        backdropRT.offsetMin = Vector2.zero;
        backdropRT.offsetMax = Vector2.zero;

        Image backdropImg = modalRoot.AddComponent<Image>();
        backdropImg.color = new Color(0.01f, 0.01f, 0.02f, 0.86f);

        CreateFramedBox(
            "MainPuzzleWindow",
            modalRoot.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1180f, 720f),
            BorderCyan,
            NoirBgBlack,
            out RectTransform innerWindowRT
        );

        // 1. CABECERA SUPERIOR
        CreateFramedBox(
            "HeaderBar",
            innerWindowRT,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -44f),
            new Vector2(1136f, 64f),
            BorderGold,
            NoirInnerCard,
            out RectTransform headerInnerRT
        );

        TextMeshProUGUI titleTMP = CreateStyledTMP(
            "HeaderTitle",
            headerInnerRT,
            "TERMINAL ÚNICA DE LA COMPUERTA  //  EMPALME Y CORTE LÁSER",
            25f,
            new Color(1f, 0.86f, 0.28f, 1f),
            TextAlignmentOptions.MidlineLeft,
            pixelFont
        );
        RectTransform titleRT = titleTMP.rectTransform;
        titleRT.anchorMin = Vector2.zero;
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(20f, 0f);
        titleRT.offsetMax = new Vector2(-180f, 0f);

        CreateNoirButton(
            "CloseBtn",
            headerInnerRT,
            new Vector2(1f, 0.5f),
            new Vector2(-84f, 0f),
            new Vector2(144f, 44f),
            "[ESC] SALIR",
            18f,
            BorderRed,
            new Color(1f, 0.55f, 0.55f, 1f),
            pixelFont,
            () => ClosePuzzle()
        );

        // 2. BANDA DE ESTADO DEL CIRCUITO
        CreateFramedBox(
            "TargetSignatureBanner",
            innerWindowRT,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -106f),
            new Vector2(1136f, 44f),
            new Color(0.20f, 0.55f, 0.72f, 1f),
            new Color(0.06f, 0.08f, 0.11f, 1f),
            out RectTransform targetBannerInnerRT
        );

        targetSignatureTMP = CreateStyledTMP(
            "TargetSignatureText",
            targetBannerInnerRT,
            "CABLES EMPALMADOS: 0/4   |   DISYUNTORES CORTADOS: 0/3",
            20f,
            Color.white,
            TextAlignmentOptions.Center,
            pixelFont
        );
        StretchFull(targetSignatureTMP.rectTransform, 10f, 4f);

        // 3A. PANEL IZQUIERDO: EMPALME DE CABLES POR COLOR (740 x 336)
        CreateFramedBox(
            "CablePatchBoard",
            innerWindowRT,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-198f, 42f),
            new Vector2(740f, 336f),
            BorderCyan,
            NoirInnerCard,
            out RectTransform cableBoardInnerRT
        );

        TextMeshProUGUI cableHeaderTMP = CreateStyledTMP(
            "CableBoardTitle",
            cableBoardInnerRT,
            "PASO 1: UNE CADA CABLE CON EL PUERTO DEL MISMO COLOR",
            18f,
            BorderCyan,
            TextAlignmentOptions.Center,
            pixelFont
        );
        RectTransform cableHeaderRT = cableHeaderTMP.rectTransform;
        cableHeaderRT.anchorMin = new Vector2(0f, 1f);
        cableHeaderRT.anchorMax = new Vector2(1f, 1f);
        cableHeaderRT.pivot = new Vector2(0.5f, 1f);
        cableHeaderRT.anchoredPosition = new Vector2(0f, -8f);
        cableHeaderRT.sizeDelta = new Vector2(-16f, 28f);

        // Capa donde se dibujan los cables conectados detrás de los pines
        GameObject wireLayerGO = new GameObject("WireLinesLayer");
        wireLayerGO.transform.SetParent(cableBoardInnerRT, false);
        RectTransform wireLayerRT = wireLayerGO.AddComponent<RectTransform>();
        wireLayerRT.anchorMin = new Vector2(0.5f, 0.5f);
        wireLayerRT.anchorMax = new Vector2(0.5f, 0.5f);
        wireLayerRT.pivot = new Vector2(0.5f, 0.5f);
        wireLayerRT.anchoredPosition = Vector2.zero;
        wireLayerRT.sizeDelta = Vector2.zero;

        for (int c = 0; c < CableCount; c++)
        {
            GameObject wireGO = new GameObject($"WireLine_{c}");
            wireGO.transform.SetParent(wireLayerRT, false);
            RectTransform wrt = wireGO.AddComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0.5f, 0.5f);
            wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);

            Image wireImg = wireGO.AddComponent<Image>();
            wireImg.color = CablePalette[c];
            wireImg.raycastTarget = false;
            wireLineImages[c] = wireImg;
            wireGO.SetActive(false);
        }

        float[] rowYs = new float[] { 86f, 20f, -46f, -112f };
        for (int i = 0; i < CableCount; i++)
        {
            int idx = i;
            float rowY = rowYs[i];

            // Conector Izquierdo (Cable origen)
            leftCableButtons[i] = CreateNoirButtonWithRefs(
                $"LeftCableBtn_{i}",
                cableBoardInnerRT,
                new Vector2(0.5f, 0.5f),
                new Vector2(-235f, rowY),
                new Vector2(226f, 52f),
                $"CABLE {cableColorNames[i]}",
                16f,
                CablePalette[i],
                Color.white,
                pixelFont,
                () => OnLeftCableClicked(idx),
                out leftCableBorders[i],
                out leftCableTexts[i]
            );

            leftPinDots[i] = CreatePinDot($"LeftPin_{i}", cableBoardInnerRT, new Vector2(-118f, rowY), CablePalette[i]);
            leftPinPositions[i] = new Vector2(-118f, rowY);

            // Conector Derecho (Puerto destino)
            rightSocketButtons[i] = CreateNoirButtonWithRefs(
                $"RightSocketBtn_{i}",
                cableBoardInnerRT,
                new Vector2(0.5f, 0.5f),
                new Vector2(235f, rowY),
                new Vector2(226f, 52f),
                "PUERTO",
                16f,
                BorderMuted,
                Color.white,
                pixelFont,
                () => OnRightSocketClicked(idx),
                out rightSocketBorders[i],
                out rightSocketTexts[i]
            );

            rightPinDots[i] = CreatePinDot($"RightPin_{i}", cableBoardInnerRT, new Vector2(118f, rowY), BorderMuted);
            rightPinPositions[i] = new Vector2(118f, rowY);
        }

        // 3B. PANEL DERECHO: 3 DISYUNTORES DE CORTE (380 x 336)
        CreateFramedBox(
            "BreakerSwitchPanel",
            innerWindowRT,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(378f, 42f),
            new Vector2(380f, 336f),
            BorderGold,
            NoirInnerCard,
            out RectTransform breakerPanelInnerRT
        );

        TextMeshProUGUI breakerHeaderTMP = CreateStyledTMP(
            "BreakerBoardTitle",
            breakerPanelInnerRT,
            "PASO 2: DISYUNTORES DE CORTE",
            18f,
            BorderGold,
            TextAlignmentOptions.Center,
            pixelFont
        );
        RectTransform breakerHeaderRT = breakerHeaderTMP.rectTransform;
        breakerHeaderRT.anchorMin = new Vector2(0f, 1f);
        breakerHeaderRT.anchorMax = new Vector2(1f, 1f);
        breakerHeaderRT.pivot = new Vector2(0.5f, 1f);
        breakerHeaderRT.anchoredPosition = new Vector2(0f, -8f);
        breakerHeaderRT.sizeDelta = new Vector2(-16f, 28f);

        float[] breakerYs = new float[] { 68f, -12f, -92f };
        for (int b = 0; b < BreakerCount; b++)
        {
            int breakerIdx = b;
            breakerButtons[b] = CreateNoirButtonWithRefs(
                $"BreakerBtn_{b}",
                breakerPanelInnerRT,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, breakerYs[b]),
                new Vector2(338f, 66f),
                breakerNames[b],
                16f,
                BorderRed,
                Color.white,
                pixelFont,
                () => OnBreakerClicked(breakerIdx),
                out breakerBorders[b],
                out breakerTexts[b]
            );
        }

        breakerSummaryTMP = CreateStyledTMP(
            "BreakerSummary",
            breakerPanelInnerRT,
            "PENDIENTE: 4 CABLE(S)  |  3 DISYUNTOR(ES)",
            15f,
            new Color(0.82f, 0.86f, 0.92f, 1f),
            TextAlignmentOptions.Center,
            pixelFont
        );
        RectTransform sumRT = breakerSummaryTMP.rectTransform;
        sumRT.anchorMin = new Vector2(0f, 0f);
        sumRT.anchorMax = new Vector2(1f, 0f);
        sumRT.pivot = new Vector2(0.5f, 0f);
        sumRT.anchoredPosition = new Vector2(0f, 16f);
        sumRT.sizeDelta = new Vector2(-16f, 26f);

        // 4. PANEL DE COMENTARIOS TÁCTICOS (VOZ INTELECTO / DETECTIVE VARGAS)
        CreateFramedBox(
            "DialogueFeedbackBox",
            innerWindowRT,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 144f),
            new Vector2(1136f, 114f),
            BorderCyan,
            NoirInnerCard,
            out RectTransform feedbackInnerRT
        );

        CreateFramedBox(
            "PortraitFrame",
            feedbackInnerRT,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(58f, 0f),
            new Vector2(92f, 92f),
            BorderGold,
            NoirBgBlack,
            out RectTransform portraitInnerRT
        );

        GameObject portImgGO = new GameObject("SpeakerPortrait");
        portImgGO.transform.SetParent(portraitInnerRT, false);
        RectTransform portImgRT = portImgGO.AddComponent<RectTransform>();
        StretchFull(portImgRT, 2f, 2f);
        speakerPortraitImg = portImgGO.AddComponent<Image>();
        speakerPortraitImg.preserveAspect = true;

        speakerLabelTMP = CreateStyledTMP(
            "SpeakerLabel",
            feedbackInnerRT,
            "VOZ [INTELECTO]",
            20f,
            BorderCyan,
            TextAlignmentOptions.MidlineLeft,
            pixelFont
        );
        RectTransform spkRT = speakerLabelTMP.rectTransform;
        spkRT.anchorMin = new Vector2(0f, 1f);
        spkRT.anchorMax = new Vector2(1f, 1f);
        spkRT.pivot = new Vector2(0.5f, 1f);
        spkRT.offsetMin = new Vector2(118f, -36f);
        spkRT.offsetMax = new Vector2(-18f, -8f);

        statusFeedbackTMP = CreateStyledTMP(
            "FeedbackText",
            feedbackInnerRT,
            "Conecta los 4 cables del mismo color y corta los 3 disyuntores.",
            19f,
            new Color(0.92f, 0.94f, 0.96f, 1f),
            TextAlignmentOptions.TopLeft,
            pixelFont
        );
        RectTransform fbTextRT = statusFeedbackTMP.rectTransform;
        fbTextRT.anchorMin = Vector2.zero;
        fbTextRT.anchorMax = Vector2.one;
        fbTextRT.offsetMin = new Vector2(118f, 10f);
        fbTextRT.offsetMax = new Vector2(-18f, -38f);

        // 5. BARRA DE BOTONES INFERIORES
        CreateNoirButton(
            "BtnReset",
            innerWindowRT,
            new Vector2(0f, 0f),
            new Vector2(148f, 46f),
            new Vector2(252f, 54f),
            "REINICIAR PANEL",
            18f,
            BorderMuted,
            new Color(0.85f, 0.88f, 0.92f, 1f),
            pixelFont,
            () => ResetPuzzleState(true)
        );

        intelAutoStabilizeBtn = CreateNoirButton(
            "BtnIntelBridge",
            innerWindowRT,
            new Vector2(0.5f, 0f),
            new Vector2(-95f, 46f),
            new Vector2(360f, 54f),
            "[INTELECTO] AUTO-CONECTAR",
            18f,
            BorderCyan,
            BorderCyan,
            pixelFont,
            () => OnIntelAutoAssistClicked()
        );

        unlockMasterBtn = CreateNoirButtonWithRefs(
            "BtnMasterUnlock",
            innerWindowRT,
            new Vector2(1f, 0f),
            new Vector2(-248f, 46f),
            new Vector2(452f, 54f),
            "[ EJECUTAR DESBLOQUEO DE COMPUERTA ]",
            19f,
            BorderGreen,
            BorderGreen,
            pixelFont,
            () => OnMasterUnlockClicked(),
            out unlockMasterBorder,
            out unlockMasterText
        );

        ResetPuzzleState(false);
    }

    private Image CreatePinDot(string name, Transform parent, Vector2 pos, Color color)
    {
        GameObject dotGO = new GameObject(name);
        dotGO.transform.SetParent(parent, false);
        RectTransform rt = dotGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(14f, 14f);

        Image img = dotGO.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private GameObject CreateFramedBox(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        Color borderColor,
        Color innerColor,
        out RectTransform innerRT)
    {
        GameObject borderGO = new GameObject(name);
        borderGO.transform.SetParent(parent, false);
        RectTransform borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.anchorMin = anchorMin;
        borderRT.anchorMax = anchorMax;
        borderRT.pivot = new Vector2(0.5f, 0.5f);
        borderRT.anchoredPosition = anchoredPos;
        borderRT.sizeDelta = sizeDelta;

        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.color = borderColor;

        GameObject innerGO = new GameObject(name + "_Inner");
        innerGO.transform.SetParent(borderGO.transform, false);
        innerRT = innerGO.AddComponent<RectTransform>();
        StretchFull(innerRT, 2f, 2f);

        Image innerImg = innerGO.AddComponent<Image>();
        innerImg.color = innerColor;

        return borderGO;
    }

    private Button CreateNoirButton(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        string label,
        float fontSize,
        Color borderColor,
        Color textColor,
        TMP_FontAsset font,
        UnityEngine.Events.UnityAction onClick)
    {
        return CreateNoirButtonWithRefs(
            name,
            parent,
            anchor,
            anchoredPos,
            sizeDelta,
            label,
            fontSize,
            borderColor,
            textColor,
            font,
            onClick,
            out _,
            out _
        );
    }

    private Button CreateNoirButtonWithRefs(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        string label,
        float fontSize,
        Color borderColor,
        Color textColor,
        TMP_FontAsset font,
        UnityEngine.Events.UnityAction onClick,
        out Image borderImgOut,
        out TextMeshProUGUI labelOut)
    {
        GameObject btnBorderGO = CreateFramedBox(
            name,
            parent,
            anchor,
            anchor,
            anchoredPos,
            sizeDelta,
            borderColor,
            new Color(0.06f, 0.06f, 0.08f, 1f),
            out RectTransform innerRT
        );
        borderImgOut = btnBorderGO.GetComponent<Image>();
        Image innerImg = innerRT.GetComponent<Image>();

        Button btn = btnBorderGO.AddComponent<Button>();
        btn.targetGraphic = innerImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        cb.highlightedColor = new Color(0.16f, 0.20f, 0.26f, 1f);
        cb.pressedColor = new Color(0.24f, 0.30f, 0.38f, 1f);
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;

        btn.onClick.AddListener(onClick);

        labelOut = CreateStyledTMP(
            name + "_Label",
            innerRT,
            label,
            fontSize,
            textColor,
            TextAlignmentOptions.Center,
            font
        );
        StretchFull(labelOut.rectTransform, 6f, 2f);

        return btn;
    }

    private TextMeshProUGUI CreateStyledTMP(
        string name,
        Transform parent,
        string text,
        float size,
        Color color,
        TextAlignmentOptions align,
        TMP_FontAsset font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
        return tmp;
    }

    private static void StretchFull(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    private static void EnsureEventSystemReady()
    {
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            es = esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    // Helper estático para entregar un ítem de recompensa al InventoryManager de forma segura
    public static void GrantRewardItemToInventory(string displayName, ItemCategory category, int amount, string description)
    {
        if (InventoryManager.Instance == null)
        {
            GameObject go = new GameObject("InventoryManager");
            go.AddComponent<InventoryManager>();
        }

        InventoryManager inv = InventoryManager.Instance;
        inv.EnsureKnownRecipesLoaded();

        // Buscar primero si ya existe en las recetas conocidas o en el inventario
        ItemData found = null;
        string targetKey = InventoryManager.GetCanonicalItemKey(displayName);

        if (inv.knownRecipes != null)
        {
            foreach (var r in inv.knownRecipes)
            {
                if (r == null) continue;
                if (r.ingredientA != null && InventoryManager.GetCanonicalItemKey(r.ingredientA.itemName) == targetKey)
                {
                    found = r.ingredientA;
                    break;
                }
                if (r.ingredientB != null && InventoryManager.GetCanonicalItemKey(r.ingredientB.itemName) == targetKey)
                {
                    found = r.ingredientB;
                    break;
                }
                if (r.resultItem != null && InventoryManager.GetCanonicalItemKey(r.resultItem.itemName) == targetKey)
                {
                    found = r.resultItem;
                    break;
                }
            }
        }

        if (found == null)
        {
            found = ScriptableObject.CreateInstance<ItemData>();
            found.name = "PuzzleReward_" + displayName.Replace(" ", "");
            found.itemName = displayName;
            found.category = category;
            found.description = description;
            if (targetKey == InventoryManager.GetCanonicalItemKey("Tónico de Adrenalina"))
            {
                found.healAmount = 55;
            }
            Sprite tonicSpr = Capitulo3GuaridaNarrative.LoadSpriteResource("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Canned-soup.png", 16f);
            if (tonicSpr != null) found.icon = tonicSpr;
            found.placeholderColor = new Color(0.25f, 0.85f, 0.95f, 1f);
        }

        inv.AddItem(found, Mathf.Max(1, amount));
        if (PlayerInventoryAndWeaponsUI.Instance != null)
        {
            PlayerInventoryAndWeaponsUI.Instance.RefreshUI();
        }
    }
}
