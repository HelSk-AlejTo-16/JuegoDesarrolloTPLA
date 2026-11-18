using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Compuerta blindada y consola interactiva en el Nivel 3 que bloquea físicamente el paso
/// hacia El Calacas mediante un BoxCollider2D sólido hasta que el jugador resuelva el puzzle Noir.
/// </summary>
public class GuaridaSecurityGateInteractable : MonoBehaviour
{
    [Header("Componentes Físicos y Visuales de la Compuerta")]
    public BoxCollider2D solidGateBlocker;
    public SpriteRenderer[] laserBeamRenderers;
    public SpriteRenderer consoleStatusLight;
    public GameObject promptObject;
    public GameObject supplyCacheRewardObject;

    [Header("Estado")]
    public bool isGateUnlocked = false;
    private bool playerInRange = false;

    private void Update()
    {
        if (isGateUnlocked || !playerInRange) return;
        if (Time.timeScale <= 0.01f) return;

        if (ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive)
            return;

        if (PlayerInventoryAndWeaponsUI.Instance != null
            && PlayerInventoryAndWeaponsUI.Instance.modalPanelRoot != null
            && PlayerInventoryAndWeaponsUI.Instance.modalPanelRoot.activeInHierarchy)
            return;

        BattleManager bm = FindAnyObjectByType<BattleManager>();
        if (bm != null && bm.state != BattleState.INACTIVE)
            return;

        if (GuaridaPuzzleMinigameUI.Instance != null && GuaridaPuzzleMinigameUI.Instance.isPuzzleOpen)
            return;

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
            GuaridaPuzzleMinigameUI ui = GuaridaPuzzleMinigameUI.GetOrCreate();
            ui.OpenPuzzle(this);
        }
    }

    public void UnlockSecurityGateAndRewardPlayer(bool solvedWithoutAutoBridge)
    {
        if (isGateUnlocked) return;
        isGateUnlocked = true;

        // 1. Desactivar el BoxCollider2D sólido que bloqueaba el paso hacia el Sanctum de El Calacas
        if (solidGateBlocker != null)
        {
            solidGateBlocker.enabled = false;
        }

        // 2. Apagar o cambiar a verde las barreras láser verticales
        if (laserBeamRenderers != null)
        {
            foreach (SpriteRenderer sr in laserBeamRenderers)
            {
                if (sr != null)
                {
                    sr.color = new Color(0.20f, 0.95f, 0.45f, 0.18f);
                }
            }
        }

        // 3. Cambiar el indicador luminoso de la consola a VERDE
        if (consoleStatusLight != null)
        {
            consoleStatusLight.color = new Color(0.20f, 1.0f, 0.45f, 1f);
        }

        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }

        // 4. Otorgar recompensa táctica real para la pelea final contra El Calacas
        GuaridaPuzzleMinigameUI.GrantRewardItemToInventory(
            "Tónico de Adrenalina",
            ItemCategory.ConsumibleCuracion,
            1,
            "Inyección táctica (+45 HP, +5 ATK y +4 MOT por 3 turnos)."
        );
        GuaridaPuzzleMinigameUI.GrantRewardItemToInventory(
            "Botella Vacía",
            ItemCategory.MaterialDeCrafteo,
            2,
            "Botella de vidrio lista para fabricar explosivos."
        );
        GuaridaPuzzleMinigameUI.GrantRewardItemToInventory(
            "Cinta Canela",
            ItemCategory.MaterialDeCrafteo,
            2,
            "Cinta industrial para fabricar Granada de Metralla o Parche Blindado."
        );

        PlayerStats pStats = FindAnyObjectByType<PlayerStats>();
        if (pStats != null)
        {
            pStats.Heal(25);
        }

        DetectiveStats dStats = DetectiveStats.EnsureInScene();
        if (dStats != null)
        {
            dStats.ReloadAmmo(dStats.municionMaxima);
            dStats.Heal(35);
            dStats.SaveProgressionSnapshot();
        }

        // 5. Diálogo Noir de confirmación con ExplorationDialogueManager
        ExplorationDialogueManager dialogueMgr = ExplorationDialogueManager.Instance ?? FindAnyObjectByType<ExplorationDialogueManager>();
        if (dialogueMgr != null)
        {
            dialogueMgr.StartDialogue(new string[]
            {
                "VOZ [INTELECTO]: \"Compuerta blindada desactivada. El arsenal de emergencia del búnker liberó: +1 Tónico de Adrenalina, +2 Botellas Vacías y +2 Cinta Canela (puedes fabricar una Granada de Metralla en [I]).\"",
                "DETECTIVE VARGAS: \"Aproveché la armería del control para recargar mi revólver .38 al máximo. El camino al Sanctum de El Calacas está despejado, Bosques. ¡Acabemos con esto!\""
            });
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isGateUnlocked) return;
        if (!IsPlayerCollider(other)) return;

        playerInRange = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isGateUnlocked) return;
        if (!IsPlayerCollider(other)) return;

        playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other)) return;

        playerInRange = false;
        if (promptObject != null)
            promptObject.SetActive(false);
    }

    private static bool IsPlayerCollider(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponent<PlayerController2D>() != null) return true;
        if (col.GetComponentInParent<PlayerController2D>() != null) return true;
        return col.name.Contains("Enrique") || col.name.Contains("Player");
    }
}
