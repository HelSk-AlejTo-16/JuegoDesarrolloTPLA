using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla un Cuarto Secreto que SOLO puede verse y abrirse interactuando (tecla E) con su Puerta Secreta.
/// - Antes de interactuar con la puerta, el techo oculta por completo el cuarto y la puerta bloquea el paso.
/// - Al acercarse a la puerta y presionar E, la puerta se abre, el techo se desactiva/transparenta
///   para revelar el cuarto secreto y sus objetos escondidos, y se reproduce el diálogo de descubrimiento.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SecretRoomZone : MonoBehaviour
{
    [Header("1. Referencias del Cuarto Secreto y Puerta")]
    [Tooltip("SpriteRenderer del techo que tapa el cuarto secreto antes de abrir la puerta.")]
    public SpriteRenderer roofRenderer;

    [Tooltip("Contenedor interior del cuarto (piso y objetos escondidos) que permanece oculto hasta usar la puerta.")]
    public GameObject roomInteriorRoot;

    [Tooltip("Objeto físico de la puerta que bloquea la entrada antes de interactuar con E.")]
    public GameObject doorBlockerObject;

    [Tooltip("Indicador flotante '[E]' sobre la puerta secreta.")]
    public GameObject promptUI;

    [Header("2. Apariencia del Techo Ocultador")]
    [Tooltip("Opacidad del techo cuando la puerta está cerrada (1 = totalmente oculto).")]
    [Range(0f, 1f)]
    public float outsideAlpha = 1.0f;

    [Tooltip("Opacidad del techo cuando se abre la puerta interactuable (0 = totalmente visible el interior).")]
    [Range(0f, 1f)]
    public float insideAlpha = 0.0f;

    [Header("3. Diálogo al Abrir la Puerta Secreta (Opcional)")]
    [TextArea(2, 3)]
    public string[] discoveryDialogueLines;

    private bool isOpened = false;
    private bool isPlayerNearDoor = false;
    private PlayerController2D playerInTrigger;

    private void Reset()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;

        // Al iniciar el juego, el cuarto secreto está cerrado y oculto hasta usar la puerta con E
        isOpened = false;
        SetRoofAlpha(outsideAlpha);

        if (roomInteriorRoot != null)
        {
            roomInteriorRoot.SetActive(false);
        }

        if (doorBlockerObject != null)
        {
            doorBlockerObject.SetActive(true);
        }

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (isOpened || !isPlayerNearDoor)
            return;

        if (CanInteractWithDoor())
        {
            if (WasInteractKeyPressedThisFrame())
            {
                OpenSecretDoor();
            }
        }
        else
        {
            if (promptUI != null && promptUI.activeSelf)
            {
                promptUI.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isOpened)
            return;

        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player == null && other.CompareTag("Player"))
        {
            player = other.GetComponentInParent<PlayerController2D>();
        }

        if (player != null)
        {
            isPlayerNearDoor = true;
            playerInTrigger = player;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player == null && other.CompareTag("Player"))
        {
            player = other.GetComponentInParent<PlayerController2D>();
        }

        if (player != null && player == playerInTrigger)
        {
            isPlayerNearDoor = false;
            playerInTrigger = null;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }

    private void OpenSecretDoor()
    {
        isOpened = true;

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        // 1. Mostrar el interior del cuarto y quitar el techo ocultador
        if (roomInteriorRoot != null)
        {
            roomInteriorRoot.SetActive(true);
        }

        SetRoofAlpha(insideAlpha);
        if (roofRenderer != null && insideAlpha <= 0.01f)
        {
            roofRenderer.enabled = false;
        }

        // 2. Desactivar el bloqueo físico de la puerta para que Enrique pueda entrar
        if (doorBlockerObject != null)
        {
            doorBlockerObject.SetActive(false);
        }

        // 3. Reproducir diálogo de descubrimiento si tiene líneas asignadas
        if (discoveryDialogueLines != null && discoveryDialogueLines.Length > 0)
        {
            if (ExplorationDialogueManager.Instance != null && !ExplorationDialogueManager.Instance.IsDialogueActive)
            {
                ExplorationDialogueManager.Instance.StartDialogue(discoveryDialogueLines);
            }
        }
    }

    private bool CanInteractWithDoor()
    {
        if (playerInTrigger != null && !playerInTrigger.canMove)
            return false;

        if (ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive)
            return false;

        BattleManager battleMgr = FindAnyObjectByType<BattleManager>();
        if (battleMgr != null && battleMgr.state != BattleState.INACTIVE)
            return false;

        return true;
    }

    private bool WasInteractKeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
        {
            return true;
        }
#endif
        return false;
    }

    private void SetRoofAlpha(float alpha)
    {
        if (roofRenderer != null)
        {
            Color c = roofRenderer.color;
            c.a = alpha;
            roofRenderer.color = c;
        }
    }
}
