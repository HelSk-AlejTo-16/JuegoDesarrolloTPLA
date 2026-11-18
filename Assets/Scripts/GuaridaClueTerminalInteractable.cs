using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Terminales secundarios de pista en el pasillo del Nivel 3 (Subestación Norte y Nodo Sur).
/// Permiten inspeccionar con [E] las pistas del puzzle y recoger suministros extra.
/// </summary>
public class GuaridaClueTerminalInteractable : MonoBehaviour
{
    public static bool NorthClueInspected = false;
    public static bool SouthClueInspected = false;

    public bool isNorthTerminal = true;
    public GameObject promptObject;
    public SpriteRenderer indicatorRenderer;

    private bool playerInRange = false;
    private bool alreadyLootedSupplies = false;

    private void Awake()
    {
        if (isNorthTerminal)
            NorthClueInspected = false;
        else
            SouthClueInspected = false;
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (Time.timeScale <= 0.01f) return;
        if (ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive) return;

        if (PlayerInventoryAndWeaponsUI.Instance != null
            && PlayerInventoryAndWeaponsUI.Instance.modalPanelRoot != null
            && PlayerInventoryAndWeaponsUI.Instance.modalPanelRoot.activeInHierarchy)
            return;

        BattleManager bm = FindAnyObjectByType<BattleManager>();
        if (bm != null && bm.state != BattleState.INACTIVE) return;

        if (GuaridaPuzzleMinigameUI.Instance != null && GuaridaPuzzleMinigameUI.Instance.isPuzzleOpen) return;

        bool interactPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            interactPressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
            interactPressed = true;
#endif

        if (interactPressed)
        {
            InspectClueNode();
        }
    }

    public void InspectClueNode()
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();

        if (indicatorRenderer != null)
            indicatorRenderer.color = new Color(0.25f, 0.95f, 0.55f, 1f);

        ExplorationDialogueManager dialogueMgr = ExplorationDialogueManager.Instance ?? FindAnyObjectByType<ExplorationDialogueManager>();

        if (isNorthTerminal)
        {
            NorthClueInspected = true;
            string lootNotice = "";
            if (!alreadyLootedSupplies)
            {
                alreadyLootedSupplies = true;
                GuaridaPuzzleMinigameUI.GrantRewardItemToInventory("Gasolina", ItemCategory.MaterialDeCrafteo, 1, "Combustible hallado junto a la subestación.");
                GuaridaPuzzleMinigameUI.GrantRewardItemToInventory("Cinta Canela", ItemCategory.MaterialDeCrafteo, 1, "Cinta aislante del panel eléctrico.");
                lootNotice = " [OBTIENES: +1 Gasolina, +1 Cinta Canela]";
            }

            if (dialogueMgr != null)
            {
                dialogueMgr.StartDialogue(new string[]
                {
                    $"VOZ [INTELECTO]: \"Bitácora de la Subestación Alfa-Beta: El RELÉ ALFA-01 opera exactamente a [75V] y el RELÉ BETA-02 requiere carga máxima de [100V].\"{lootNotice}"
                });
            }
        }
        else
        {
            SouthClueInspected = true;
            string lootNotice = "";
            if (!alreadyLootedSupplies)
            {
                alreadyLootedSupplies = true;
                GuaridaPuzzleMinigameUI.GrantRewardItemToInventory("Botella Vacía", ItemCategory.MaterialDeCrafteo, 1, "Envase de vidrio grueso.");
                GuaridaPuzzleMinigameUI.GrantRewardItemToInventory("Gasolina", ItemCategory.MaterialDeCrafteo, 1, "Reserva del generador auxiliar.");
                lootNotice = " [OBTIENES: +1 Botella Vacía, +1 Gasolina]";
            }

            if (dialogueMgr != null)
            {
                dialogueMgr.StartDialogue(new string[]
                {
                    $"DETECTIVE VARGAS: \"Mira este esquema eléctrico pegado al generador, Bosques: el RELÉ GAMMA-03 debe fijarse en [50V] y el RELÉ OMEGA-04 en [100V]. Ya tenemos los cuatro valores para la consola de la compuerta.\"{lootNotice}"
                });
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other)) return;
        playerInRange = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayerCollider(other)) return;
        playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other)) return;
        playerInRange = false;
        if (promptObject != null) promptObject.SetActive(false);
    }

    private static bool IsPlayerCollider(Collider2D col)
    {
        if (col == null) return false;
        return col.CompareTag("Player")
            || col.GetComponent<PlayerController2D>() != null
            || col.GetComponentInParent<PlayerController2D>() != null
            || col.name.Contains("Enrique")
            || col.name.Contains("Player");
    }
}
