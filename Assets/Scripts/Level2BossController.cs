using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador del Jefe Final del Nivel 2 ("El Capataz Blindado - Mano Derecha de El Calacas").
/// Implementa una máquina de estados de 3 fases completamente distinta al Jefe del Nivel 1:
/// 1. FaseAsaltoTactico: Ataques de barrido pesado y cortina de gas tóxico en el callejón; al terminar su turno activa su Escudo Blindado.
/// 2. FaseEscudoBlindado: Reduce el daño cuerpo a cuerpo un 65% y contraataca automáticamente los golpes cuerpo a cuerpo.
///    Se rompe si es impactado por el Arma de Fuego/Habilidades del Detective o Intelecto/Mundo Interno de Enrique.
/// 3. FaseSobrecargaVulnerable: Con el blindaje roto, recibe +50% de daño adicional de todos los ataques y pierde su contraataque.
///
/// Además gestiona el evento OnBossDefeated cuando BossHP <= 0 para guardar el estado persistente de Enrique y el Detective,
/// ejecutar el fade-out de transición y cargar inmediatamente la escena "Nivel_3".
/// </summary>
public class Level2BossController : MonoBehaviour
{
    public enum BossCombatPhase
    {
        FaseAsaltoTactico = 0,
        FaseEscudoBlindado = 1,
        FaseSobrecargaVulnerable = 2
    }

    [Header("1. Estado de la Máquina de Fases del Jefe (Nivel 2)")]
    public BossCombatPhase currentPhase = BossCombatPhase.FaseAsaltoTactico;
    public int turnsInCurrentPhase = 0;
    public bool toxicSmokeFieldActive = false;
    public int toxicSmokeTurnsRemaining = 0;

    [Header("2. Parámetros de Mecánica Única (Escudo, Contraataque y Vulnerabilidad)")]
    [Range(0.1f, 0.9f)]
    [Tooltip("Fracción de daño que absorbe el escudo blindado frente a ataques cuerpo a cuerpo directos.")]
    public float shieldMeleeDamageReduction = 0.65f;

    [Range(1.1f, 2.5f)]
    [Tooltip("Multiplicador de daño que recibe el Jefe cuando su escudo ha sido roto (FaseSobrecargaVulnerable).")]
    public float vulnerableDamageMultiplier = 1.50f;

    [Tooltip("Daño base del contraataque inmediato cuando es golpeado cuerpo a cuerpo durante FaseEscudoBlindado.")]
    public int shieldCounterAttackDamage = 11;

    [Tooltip("Daño ambiental por turno del gas lacrimógeno/tóxico desplegado por el Jefe.")]
    public int toxicFieldDamagePerTurn = 5;

    [Header("3. Transición a Nivel 3 al Derrotar al Jefe (OnBossDefeated)")]
    public string bossDisplayName = "EL CAPATAZ PUNK";

    [Tooltip("Identificador de la siguiente escena a cargar al derrotar al Jefe del Nivel 2.")]
    public string nextLevelSceneName = "Nivel_3";

    public string nextSceneIdentifier
    {
        get => nextLevelSceneName;
        set => nextLevelSceneName = value;
    }

    [TextArea(2, 4)]
    public string[] bossDefeatedTransitionLines = new string[]
    {
        "NARRADOR: \"El Capataz Punk se desploma derrotado tras el intenso choque de puños. La respiración se le corta y cae de rodillas sobre el adoquín húmedo del callejón.\"",
        "DETECTIVE VARGAS: \"¡Gran combate, Bosques! Despachamos al líder de la banda que vigilaba este acceso. La puerta acorazada del complejo de El Calacas está abierta.\"",
        "ENRIQUE BOSQUES: \"Ese desgraciado dijo que El Calacas nos espera en el piso subterráneo del almacén... Ya no hay vuelta atrás.\"",
        "VOZ [INTELECTO]: \"Hemos asegurado el perímetro exterior y recolectado suministros clave. Nuestro equipo está listo para el asalto final.\"",
        "[ CAPÍTULO 3: EL COMPLEJO DE EL CALACAS ]"
    };

    public string[] bossDefeatedNarrativeLines
    {
        get => bossDefeatedTransitionLines;
        set => bossDefeatedTransitionLines = value;
    }

    /// <summary>
    /// Evento disparado inmediatamente cuando la vida del Jefe del Nivel 2 llega a 0 (BossHP <= 0).
    /// </summary>
    public event Action OnBossDefeated;

    private bool hasTriggeredDefeatEvent = false;

    private void Awake()
    {
        OnBossDefeated -= HandleBossDefeatedDefaultPersistence;
        OnBossDefeated += HandleBossDefeatedDefaultPersistence;
    }

    private void OnDestroy()
    {
        OnBossDefeated -= HandleBossDefeatedDefaultPersistence;
    }

    /// <summary>
    /// Inicializa la máquina de estados al comenzar el combate contra el Jefe del Nivel 2.
    /// </summary>
    public virtual void InitializeBossCombat()
    {
        currentPhase = BossCombatPhase.FaseEscudoBlindado;
        turnsInCurrentPhase = 0;
        toxicSmokeFieldActive = false;
        toxicSmokeTurnsRemaining = 0;
        hasTriggeredDefeatEvent = false;
    }

    /// <summary>
    /// Devuelve la etiqueta descriptiva de la fase actual para mostrarla junto al HP del Jefe en la UI.
    /// </summary>
    public virtual string GetPhaseStatusBadge()
    {
        switch (currentPhase)
        {
            case BossCombatPhase.FaseEscudoBlindado:
                return " <color=#55CCFF>[ESCUDO BLINDADO + CONTRAATAQUE]</color>";
            case BossCombatPhase.FaseSobrecargaVulnerable:
                return " <color=#FF5555>[GUARDIA ROTA: +50% DAÑO]</color>";
            default:
                return toxicSmokeFieldActive
                    ? " <color=#AAFF55>[ASALTO + GAS ACTIVO]</color>"
                    : " <color=#FFD84D>[POSTURA DE ASALTO]</color>";
        }
    }

    /// <summary>
    /// Evalúa y modifica el daño recibido por el Jefe según su fase actual y el tipo de ataque empleado.
    /// Determina también si el Jefe responde con un contraataque o si su escudo blindado se rompe.
    /// </summary>
    public virtual int ProcessIncomingDamage(
        int baseDamage,
        bool isDirectMeleeAttack,
        bool canBreakArmorShield,
        string attackerName,
        out bool triggeredCounterAttack,
        out int counterAttackDamage,
        out string mechanicFeedbackMessage)
    {
        triggeredCounterAttack = false;
        counterAttackDamage = 0;
        mechanicFeedbackMessage = string.Empty;

        int finalDamage = Mathf.Max(1, baseDamage);

        if (currentPhase == BossCombatPhase.FaseEscudoBlindado)
        {
            if (canBreakArmorShield)
            {
                // El disparo de arma de fuego del Detective, habilidad táctica o objeto/voz de Enrique rompe el escudo
                currentPhase = BossCombatPhase.FaseSobrecargaVulnerable;
                turnsInCurrentPhase = 0;
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * 1.25f));
                mechanicFeedbackMessage = $"¡IMPACTO PERFORANTE DE {attackerName.ToUpper()}! ¡El Escudo Blindado del Jefe se FRACTURA y queda VULNERABLE (+50% daño)!";
            }
            else if (isDirectMeleeAttack)
            {
                // Ataque cuerpo a cuerpo directo contra el escudo activo: daño reducido y activa contraataque
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * (1f - shieldMeleeDamageReduction)));
                triggeredCounterAttack = true;
                counterAttackDamage = shieldCounterAttackDamage;
                mechanicFeedbackMessage = $"¡ESCUDO BLINDADO! El Jefe absorbe el golpe cuerpo a cuerpo (-65% daño) y ejecuta un CONTRAATAQUE INMEDIATO (-{counterAttackDamage} HP).";
            }
            else
            {
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * 0.75f));
                mechanicFeedbackMessage = "El Escudo Blindado mitiga parcialmente el impacto. ¡Usa el Arma de Fuego/Habilidades del Detective o Intelecto para romperlo!";
            }
        }
        else if (currentPhase == BossCombatPhase.FaseSobrecargaVulnerable)
        {
            finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * vulnerableDamageMultiplier));
            mechanicFeedbackMessage = $"¡OBJETIVO VULNERABLE! {attackerName} inflige daño amplificado (+50%): -{finalDamage} HP.";
        }
        else
        {
            mechanicFeedbackMessage = $"{attackerName} golpea al Jefe en Postura de Asalto (-{finalDamage} HP).";
        }

        return finalDamage;
    }

    /// <summary>
    /// Permite que una habilidad de estado (como 'Analizar Punto Débil' del Detective o 'Mundo Interno')
    /// fuerce la ruptura del escudo blindado aunque no haga daño directo.
    /// </summary>
    public virtual bool TryBreakShieldWithSkill(string skillName, out string feedback)
    {
        if (currentPhase == BossCombatPhase.FaseEscudoBlindado)
        {
            currentPhase = BossCombatPhase.FaseSobrecargaVulnerable;
            turnsInCurrentPhase = 0;
            feedback = $"¡'{skillName}' desmantela la cobertura del Jefe! Su Escudo Blindado cae y entra en SOBRECARGA VULNERABLE (+50% daño recibido).";
            return true;
        }

        feedback = $"'{skillName}' debilita la postura del Jefe.";
        return false;
    }

    /// <summary>
    /// Ejecuta la lógica de máquina de estados durante el turno del Jefe del Nivel 2,
    /// devolviendo el multiplicador de daño de su acción, el mensaje de ataque y si despliega efecto de campo.
    /// </summary>
    public virtual void ExecuteBossTurnStateTransition(
        int currentHP,
        int maxHP,
        out float attackDamageMultiplier,
        out string bossActionLog,
        out int fieldDamageToParty)
    {
        turnsInCurrentPhase++;
        fieldDamageToParty = 0;
        attackDamageMultiplier = 1.0f;
        bossActionLog = string.Empty;

        if (toxicSmokeFieldActive && toxicSmokeTurnsRemaining > 0)
        {
            toxicSmokeTurnsRemaining--;
            fieldDamageToParty = toxicFieldDamagePerTurn;
            if (toxicSmokeTurnsRemaining <= 0)
            {
                toxicSmokeFieldActive = false;
            }
        }

        switch (currentPhase)
        {
            case BossCombatPhase.FaseSobrecargaVulnerable:
                // Tras estar vulnerable, el Jefe se recompone, lanza una granada de gas y pasa a Asalto Táctico
                currentPhase = BossCombatPhase.FaseAsaltoTactico;
                turnsInCurrentPhase = 0;
                toxicSmokeFieldActive = true;
                toxicSmokeTurnsRemaining = 2;
                attackDamageMultiplier = 1.15f;
                bossActionLog = "¡El Jefe se recupera de la sobrecarga, detona una GRANADA DE GAS TÓXICO en el callejón y arremete con furia!";
                break;

            case BossCombatPhase.FaseAsaltoTactico:
                // Después de atacar en modo asalto, levanta su Escudo Blindado Antidisturbios para el siguiente ciclo
                currentPhase = BossCombatPhase.FaseEscudoBlindado;
                turnsInCurrentPhase = 0;
                attackDamageMultiplier = 1.25f;
                bossActionLog = "¡El Jefe descarga un Mandoble Brutal y alza su ESCUDO BLINDADO ANTIDISTURBIOS (Contraatacará golpes cuerpo a cuerpo)!";
                break;

            case BossCombatPhase.FaseEscudoBlindado:
                if (turnsInCurrentPhase >= 2)
                {
                    // Si nadie rompió su escudo en 2 turnos, descarga un embate pesado y baja la guardia brevemente
                    currentPhase = BossCombatPhase.FaseAsaltoTactico;
                    turnsInCurrentPhase = 0;
                    attackDamageMultiplier = 1.35f;
                    bossActionLog = "¡El Jefe carga con todo el peso de su Escudo Blindado en una embestida demoledora!";
                }
                else
                {
                    attackDamageMultiplier = 1.0f;
                    bossActionLog = "El Jefe avanza tras su Escudo Blindado y golpea con su maza de acero. ¡Rómpele el escudo con el Revólver .38 o Habilidades!";
                }
                break;
        }
    }

    /// <summary>
    /// Se invoca cuando BossHP <= 0. Dispara el evento OnBossDefeated, guarda el estado persistente
    /// del jugador y del Detective, y coordina el cambio de escena hacia Nivel_3.
    /// </summary>
    public virtual void NotifyBossDefeated(BattleManager bm)
    {
        if (hasTriggeredDefeatEvent)
        {
            return;
        }
        hasTriggeredDefeatEvent = true;

        if (bm != null)
        {
            bm.isCurrentEncounterBoss = true;
            if (string.IsNullOrEmpty(bm.currentBossNextScene))
            {
                bm.currentBossNextScene = nextLevelSceneName;
            }
            if (bm.currentBossVictoryNarrative == null || bm.currentBossVictoryNarrative.Length == 0)
            {
                bm.currentBossVictoryNarrative = bossDefeatedTransitionLines;
            }
        }

        OnBossDefeated?.Invoke();
    }

    private void HandleBossDefeatedDefaultPersistence()
    {
        PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.SaveProgressionSnapshot();
        }

        DetectiveStats detectiveStats = DetectiveStats.Instance;
        if (detectiveStats != null)
        {
            detectiveStats.SaveProgressionSnapshot();
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SaveSnapshot();
        }

        Debug.Log($"[Level2BossController] OnBossDefeated ejecutado. Estado de Enrique y Detective guardado. Próxima escena: {nextLevelSceneName}");
    }

    /// <summary>
    /// Ejecuta directamente la transición con fade-out hacia la escena Nivel_3 si se invoca fuera del flujo estándar.
    /// </summary>
    public virtual void TriggerImmediateFadeAndLoadNextLevel()
    {
        HandleBossDefeatedDefaultPersistence();
        string targetScene = string.IsNullOrEmpty(nextLevelSceneName) ? "Nivel_3" : nextLevelSceneName;

        BossVictoryTransitionUI transitionUI = BossVictoryTransitionUI.EnsureInScene();
        if (transitionUI != null)
        {
            transitionUI.StartTransition(bossDefeatedTransitionLines, targetScene);
        }
        else
        {
            SceneManager.LoadScene(targetScene);
        }
    }
}
