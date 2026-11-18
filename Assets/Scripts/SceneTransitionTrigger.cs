using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Punto o zona de transición entre escenas (ej. de Escena 1: Santa Felipina -> Escena 2: El Callejón Peatonal).
/// Puede activarse automáticamente al caminar hacia el acceso del callejón o presionando [E].
/// Utiliza BossVictoryTransitionUI para mostrar la secuencia narrativa noir con fundido a negro.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Configuración de Destino")]
    public string targetSceneName = "02_Nivel2_Guarida";

    [Tooltip("Si es true, inicia la transición al entrar en el trigger; si es false, requiere presionar [E].")]
    public bool triggerOnEnter = false;

    [Tooltip("Si es true y el jefe de la zona sigue vivo, advierte a Enrique antes de cruzar.")]
    public bool requireBossDefeatedIfPresent = true;

    public GameObject bossObjectToCheck;

    [Header("Narrativa de Transición (Estilo Noir)")]
    [TextArea(2, 4)]
    public string[] transitionNarrativeLines = new string[]
    {
        "Dejas atrás la avenida principal de Santa Felipina y te adentras entre dos bloques de edificios antiguos...",
        "ENRIQUE BOSQUES: \"Los autos quemados bloquean toda la calle. A partir de aquí solo se puede cruzar a pie.\"",
        "VOZ [INTELECTO]: \"El manifiesto y el croquis coinciden: este callejón peatonal desemboca directamente en la guarida de El Calacas.\"",
        "VOZ [MUNDO INTERNO]: \"Entre la niebla del callejón alguien nos espera bajo el farol... Un hombre con gabardina que también sigue el rastro de la muerte.\"",
        "[ CAPÍTULO 2: EL CALLEJÓN DE LAS SOMBRAS ]"
    };

    [Header("Indicador Visual Flotante")]
    public GameObject promptUI;

    private bool isPlayerInside = false;
    private bool hasTriggered = false;
    private PlayerController2D playerController;

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;
        if (promptUI != null) promptUI.SetActive(false);
    }

    private void Update()
    {
        if (!isPlayerInside || hasTriggered) return;

        if (CanInteract())
        {
            if (WasInteractKeyPressed())
            {
                TryExecuteTransition();
            }
        }
        else if (promptUI != null && promptUI.activeSelf)
        {
            promptUI.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        PlayerController2D pc = other.GetComponent<PlayerController2D>();
        if (pc == null && other.CompareTag("Player"))
        {
            pc = other.GetComponentInParent<PlayerController2D>();
        }

        if (pc != null)
        {
            isPlayerInside = true;
            playerController = pc;

            if (triggerOnEnter && CanInteract())
            {
                TryExecuteTransition();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController2D pc = other.GetComponent<PlayerController2D>();
        if (pc == null && other.CompareTag("Player"))
        {
            pc = other.GetComponentInParent<PlayerController2D>();
        }

        if (pc != null && pc == playerController)
        {
            isPlayerInside = false;
            playerController = null;
            if (promptUI != null) promptUI.SetActive(false);
        }
    }

    private bool CanInteract()
    {
        if (playerController != null && !playerController.canMove) return false;
        if (ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive) return false;
        BattleManager bm = FindAnyObjectByType<BattleManager>();
        if (bm != null && bm.state != BattleState.INACTIVE) return false;
        return true;
    }

    public void TryExecuteTransition()
    {
        if (hasTriggered) return;

        if (requireBossDefeatedIfPresent && bossObjectToCheck != null && bossObjectToCheck.activeInHierarchy)
        {
            if (ExplorationDialogueManager.Instance != null)
            {
                ExplorationDialogueManager.Instance.StartDialogue(new string[]
                {
                    "VOZ [MOTRICIDAD]: \"¡Alto ahí, Enrique! El líder de los matones sigue bloqueando la esquina.\"",
                    "VOZ [CORPOREO]: \"Primero hay que quitar de en medio al Jefe Callejero antes de meternos al callejón.\""
                });
            }
            return;
        }

        hasTriggered = true;
        if (promptUI != null) promptUI.SetActive(false);
        if (playerController != null) playerController.SetCanMove(false);

        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null) stats.SaveProgressionSnapshot();
        if (InventoryManager.Instance != null) InventoryManager.Instance.SaveSnapshot();
        if (DetectiveStats.Instance != null) DetectiveStats.Instance.SaveProgressionSnapshot();

        BossVictoryTransitionUI transitionUI = BossVictoryTransitionUI.EnsureInScene();
        if (transitionUI != null)
        {
            transitionUI.StartTransition(transitionNarrativeLines, targetSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
        }
    }

    private bool WasInteractKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
            return true;
#endif
        return false;
    }
}
