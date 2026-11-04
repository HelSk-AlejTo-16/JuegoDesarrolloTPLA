using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum InteractableType
{
    PickupItem,
    DialogueOnly
}

/// <summary>
/// Componente versátil para cualquier objeto recolectable, elemento examinable o NPC en el mapa.
/// Se activa al acercarse con Enrique y presionar la tecla E.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MapInteractable : MonoBehaviour
{
    [Header("1. Tipo de Interacción")]
    public InteractableType type = InteractableType.PickupItem;

    [Header("2. Configuración de Recolección (Si es PickupItem)")]
    public ItemData itemToPickup;
    public int quantity = 1;

    [TextArea(2, 3)]
    public string[] pickupDialogue = new string[]
    {
        "VOZ [CORPOREO]: \"Basura callejera.\"",
        "VOZ [INTELECTO]: \"No es basura, nos servira para improvisar un artefacto en combate. Guardalo.\""
    };

    [Header("3. Configuración de Diálogo / Examen (Si es DialogueOnly)")]
    [Tooltip("Si está activo, el diálogo inicia automáticamente al pisar el objeto sin tener que presionar E.")]
    public bool triggerAutomaticallyOnEnter = false;
    [Tooltip("Si está activo, este diálogo solo se podrá reproducir una única vez.")]
    public bool triggerDialogueOnce = false;
    [TextArea(2, 3)]
    public string[] dialogueLines = new string[]
    {
        "NARRADOR: \"Tocas la puerta oxidada, pero nadie responde desde adentro.\"",
        "VOZ [MUNDO INTERNO]: \"Detras de las cortinas alguien nos observa en silencio.\""
    };

    [Header("4. Indicador Visual Flotante (Opcional)")]
    [Tooltip("Objeto o texto '[E]' que se muestra cuando Enrique está dentro del rango.")]
    public GameObject promptUI;

    [Header("5. Referencias Opcionales")]
    public ExplorationDialogueManager explorationDialogueManager;

    private bool isPlayerInRange = false;
    private PlayerController2D playerInTrigger;
    private bool hasBeenPickedUp = false;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();

        // Si es PickupItem, configurar su sprite visual
        if (type == InteractableType.PickupItem && itemToPickup != null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (itemToPickup.icon != null)
                {
                    sr.sprite = itemToPickup.icon;
                    sr.color = Color.white;
                }
                else
                {
                    sr.color = itemToPickup.placeholderColor;
                }
            }
        }

        // Buscar PromptUI_E en los hijos si no fue asignado directamente en el inspector
        if (promptUI == null)
        {
            Transform promptChild = transform.Find("PromptUI_E");
            if (promptChild != null)
            {
                promptUI = promptChild.gameObject;
            }
        }

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (!isPlayerInRange || hasBeenPickedUp)
            return;

        if (CanInteractRightNow())
        {
            if (promptUI != null && !promptUI.activeSelf)
            {
                promptUI.SetActive(true);
            }

            if (WasInteractKeyPressedThisFrame())
            {
                ExecuteInteraction();
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
        if (hasBeenPickedUp)
            return;

        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player == null && other.CompareTag("Player"))
        {
            player = other.GetComponentInParent<PlayerController2D>();
        }

        if (player != null)
        {
            isPlayerInRange = true;
            playerInTrigger = player;

            if (type == InteractableType.DialogueOnly && triggerAutomaticallyOnEnter && CanInteractRightNow())
            {
                ExecuteInteraction();
                return;
            }

            if (promptUI != null && CanInteractRightNow())
            {
                promptUI.SetActive(true);
            }
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
            isPlayerInRange = false;
            playerInTrigger = null;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }

    private bool CanInteractRightNow()
    {
        // No interactuar si Enrique tiene el movimiento bloqueado (ej. durante el slideshow, un diálogo o combate)
        if (playerInTrigger != null && !playerInTrigger.canMove)
            return false;

        ExplorationDialogueManager dialogueMgr = (explorationDialogueManager != null)
            ? explorationDialogueManager
            : ExplorationDialogueManager.Instance;

        if (dialogueMgr != null && dialogueMgr.IsDialogueActive)
            return false;

        BattleManager battleMgr = FindAnyObjectByType<BattleManager>();
        if (battleMgr != null && battleMgr.state != BattleState.INACTIVE)
            return false;

        return true;
    }

    private void ExecuteInteraction()
    {
        ExplorationDialogueManager dialogueMgr = (explorationDialogueManager != null)
            ? explorationDialogueManager
            : ExplorationDialogueManager.Instance;

        if (type == InteractableType.PickupItem)
        {
            hasBeenPickedUp = true;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }

            if (itemToPickup != null && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(itemToPickup, Mathf.Max(1, quantity));
            }

            // Reproducir sonido de recolección / confirmación
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
            }

            // Actualizar la interfaz de inventario y equipamiento
            if (PlayerInventoryAndWeaponsUI.Instance != null)
            {
                PlayerInventoryAndWeaponsUI.Instance.RefreshUI();
            }

            string[] linesToShow = pickupDialogue;
            if ((linesToShow == null || linesToShow.Length == 0) && itemToPickup != null)
            {
                linesToShow = new string[]
                {
                    $"NARRADOR: \"Recogiste {Mathf.Max(1, quantity)}x {itemToPickup.itemName}.\""
                };
            }

            if (dialogueMgr != null && linesToShow != null && linesToShow.Length > 0)
            {
                dialogueMgr.StartDialogue(linesToShow);
            }

            // Desactivar el objeto del mapa para que no se pueda recoger dos veces
            gameObject.SetActive(false);
        }
        else if (type == InteractableType.DialogueOnly)
        {
            if (triggerDialogueOnce)
            {
                hasBeenPickedUp = true;
            }

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }

            if (dialogueMgr != null && dialogueLines != null && dialogueLines.Length > 0)
            {
                dialogueMgr.StartDialogue(dialogueLines);
            }
        }
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
}
