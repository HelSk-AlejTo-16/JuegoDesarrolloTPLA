using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema 4: Trigger en el mapa para iniciar el combate al acercarse a uno o varios lacayos/jefes.
/// Soporta tanto 1 enemigo individual como múltiples enemigos en la misma pantalla de batalla.
/// En el Nivel 2 soporta grupos dobles garantizados (forceDoubleEncounterGroup) y enlace con Level2BossController.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyEncounterTrigger : MonoBehaviour
{
    [Header("Configuración del Encuentro (1 o Varios Enemigos)")]
    [Tooltip("Lista de enemigos que aparecerán juntos en la misma pantalla de combate.")]
    public EnemyData[] enemies;

    [Tooltip("Compatibilidad individual si 'enemies' está vacío.")]
    public EnemyData enemyData;

    [Tooltip("Si está activo (ej. Nivel 2), garantiza que el encuentro instancie siempre un grupo de 2 enemigos simultáneos.")]
    public bool forceDoubleEncounterGroup = false;

    [Tooltip("Controlador opcional de mecánicas únicas del Jefe del Nivel 2.")]
    public Level2BossController level2BossController;

    [Tooltip("Objetos del mapa (ej. los 2 lacayos) que se desactivarán al ganar el combate.")]
    public GameObject[] mapObjectsToDisableOnWin;

    [Tooltip("Referencia al BattleManager de la escena.")]
    public BattleManager battleManager;

    [Header("Diálogo Inicial del Combate (Tutorial de las 4 Voces)")]
    [TextArea(2, 4)]
    public string[] introDialogueLines = new string[]
    {
        "LACAYO RAPIDO: \"Alto ahi, repartidor! Somos dos contra uno, entrega el paquete del Jefe!\"",
        "LACAYO FUERTE: \"Si no hablas por las buenas, te vamos a moler a golpes entre los dos.\"",
        "VOZ [CORPOREO]: \"Dos idiotas frente a nosotros! Haz clic sobre cualquiera de ellos para elegir objetivo y usa mi boton (CORPOREO) para golpear!\"",
        "VOZ [INTELECTO]: \"El grandulon tiene mucha defensa (10). Contra el, usa mi boton (INTELECTO) para improvisar un artefacto e ignorar su armadura.\"",
        "VOZ [MOTRICIDAD]: \"Por velocidad actuamos primero nosotros (12), luego el rapido (10) y al final el lento (6). Mi boton (MOTRICIDAD) nos defiende de ambos en esta ronda!\"",
        "VOZ [MUNDO INTERNO]: \"O usa mi boton (MUNDO INTERNO) sobre uno de ellos para confundirlo y que pierda su turno en la cola.\""
    };

    [Header("Transición de Escenario al Vencer al Jefe (Boss Transition)")]
    [Tooltip("Indica si este combate es contra un jefe que dispara cambio de escenario al ganar.")]
    public bool isBossEncounter = false;

    [Tooltip("Nombre de la escena de Unity a cargar tras vencer al jefe.")]
    public string nextSceneName = "02_Nivel2_Guarida";

    [Tooltip("Textos narrativos mostrados en pantalla negra antes de cargar el nuevo escenario.")]
    [TextArea(2, 4)]
    public string[] victoryNarrativeLines = new string[]
    {
        "El Jefe Callejero cae noqueado contra el pavimento, soltando las llaves del almacén clandestino...",
        "ENRIQUE BOSQUES: \"(Limpiando la sangre de sus nudillos) Se acabó. El rastro conduce detrás de este portón.\"",
        "Las voces en tu mente guardan silencio mientras abres la pesada reja de metal...",
        "[ CAPÍTULO 2: LA GUARIDA DE LA MAFIA ]"
    };

    [Header("Recompensa al Ganar el Combate (Arma u Objetos)")]
    [Tooltip("Arma u objeto principal que se entrega automaticamente a Enrique al vencer este combate (ej. Cadena de Bicicleta).")]
    public ItemData rewardItemOnWin;

    [Tooltip("Lista de objetos o materiales adicionales que se entregan al ganar (ej. Botellas Vacias, Gasolina, Cinta Canela al final del Nivel 1).")]
    public List<InventorySlot> rewardItemsOnWin = new List<InventorySlot>();

    [TextArea(2, 4)]
    [Tooltip("Lineas de dialogo que explican la recompensa obtenida al vencer al enemigo.")]
    public string[] rewardDialogueLines;

    private bool hasTriggered = false;

    private void Reset()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController2D>();
        }

        if (player != null)
        {
            if (battleManager == null)
            {
                battleManager = FindAnyObjectByType<BattleManager>();
            }
            if (battleManager == null)
            {
                battleManager = BattleManager.EnsureInScene();
            }

            List<EnemyData> encounterList = new List<EnemyData>();
            if (enemies != null && enemies.Length > 0)
            {
                foreach (EnemyData e in enemies)
                {
                    if (e != null) encounterList.Add(e);
                }
            }
            if (encounterList.Count == 0 && enemyData != null)
            {
                encounterList.Add(enemyData);
            }

            if (forceDoubleEncounterGroup && !isBossEncounter && encounterList.Count == 1 && encounterList[0] != null)
            {
                encounterList.Add(encounterList[0]);
            }

            if (battleManager == null || encounterList.Count == 0)
            {
                Debug.LogWarning($"[EnemyEncounterTrigger] Falta asignar BattleManager o EnemyData en '{gameObject.name}'.");
                return;
            }

            hasTriggered = true;

            if (level2BossController == null)
            {
                level2BossController = GetComponent<Level2BossController>();
            }
            battleManager.activeLevel2BossController = level2BossController;

            // 1. Bloquear el movimiento de Enrique en el mapa
            player.SetCanMove(false);

            // 2. Preparar lista de GameObjects del mapa a desactivar cuando gane la batalla
            GameObject[] objectsToDisable = (mapObjectsToDisableOnWin != null && mapObjectsToDisableOnWin.Length > 0)
                ? mapObjectsToDisableOnWin
                : new GameObject[] { this.gameObject };

            // 3. Activar el panel de combate e iniciar la pelea múltiple
            if (battleManager.battlePanel != null)
            {
                battleManager.battlePanel.SetActive(true);
            }

            battleManager.StartBattle(
                encounterList.ToArray(),
                introDialogueLines,
                objectsToDisable,
                isBossEncounter,
                nextSceneName,
                victoryNarrativeLines,
                rewardItemOnWin,
                rewardDialogueLines,
                rewardItemsOnWin
            );
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}
