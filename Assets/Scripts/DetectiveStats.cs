using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estructura de datos y componente de estadísticas del compañero "Detective" (Investigador Héctor Vargas).
/// Gestiona sus atributos independientes, estado en el equipo (hasDetectiveInParty), munición/enfriamiento,
/// estados alterados temporales en combate y progresión automática con asignación 100% aleatoria de estadísticas.
/// </summary>
public class DetectiveStats : MonoBehaviour
{
    [Serializable]
    private struct DetectiveProgressionSnapshot
    {
        public bool isSaved;
        public bool hasDetectiveInParty;
        public int nivel;
        public int expActual;
        public int expRequerida;
        public int hpMax;
        public int hpActual;
        public int ataqueCuerpoACuerpo;
        public int ataqueArma;
        public int defensa;
        public int velocidad;
        public float probabilidadEsquiva;
        public int municionActual;
        public int municionMaxima;
    }

    private static DetectiveProgressionSnapshot s_snapshot;
    private static DetectiveStats s_instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        s_snapshot = default;
        s_instance = null;
    }

    public static DetectiveStats Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindAnyObjectByType<DetectiveStats>();
            }
            return s_instance;
        }
    }

    [Header("1. Validación de Estado en el Equipo (Party Check)")]
    [Tooltip("Indica si el Detective forma parte activa del equipo del jugador para entrar en combate.")]
    public bool hasDetectiveInParty = false;

    [Tooltip("Nombre visible del compañero en la interfaz de combate.")]
    public string detectiveName = "DETECTIVE VARGAS";

    public string companionName
    {
        get => detectiveName;
        set => detectiveName = value;
    }

    [Header("2. Estadísticas Base Independientes del Detective")]
    public int HP_Max = 95;
    public int HP_Actual = 95;
    public int Ataque_CuerpoACuerpo = 14;
    public int Ataque_Arma = 24;
    public int Defensa = 6;
    public int Velocidad = 14;

    [Range(0f, 85f)]
    [Tooltip("Probabilidad porcentual base de esquivar ataques enemigos (0 a 85%).")]
    public float Probabilidad_Esquiva = 18f;

    [Header("3. Nivel y Progresión Automática")]
    public int Nivel = 1;
    public int EXP_Actual = 0;
    public int EXP_Requerida = 100;

    [Tooltip("Fracción de la EXP del jugador que recibe el Detective al ganar un combate (1.0 = 100%).")]
    [Range(0.25f, 1.5f)]
    public float expShareRatio = 1.0f;

    [Tooltip("Cantidad de mejoras aleatorias que se reparten automáticamente al subir cada nivel.")]
    public int statAllocationsPerLevel = 3;

    [Header("4. Recursos de Arma de Fuego (Revólver .38 Especial)")]
    public int municionActual = 6;
    public int municionMaxima = 6;
    public int enfriamientoArma = 0;

    [Header("5. Estados Temporales en Combate")]
    [Tooltip("Activo cuando el Detective elige la acción 'Esquivar' hasta su próximo turno.")]
    public bool isDodgingUntilNextTurn = false;

    [Tooltip("Bono temporal de probabilidad de esquiva otorgado por la acción 'Esquivar'.")]
    public float dodgeActionBonusPercent = 42f;

    [Tooltip("Bono temporal de esquiva por habilidades tácticas del Detective.")]
    public float skillDodgeBonusPercent = 0f;

    [Tooltip("Bono temporal de ataque por habilidades tácticas del Detective.")]
    public int tacticalAttackBonus = 0;

    [Tooltip("Turnos restantes del bono táctico activo.")]
    public int tacticalBuffTurnsRemaining = 0;

    [Header("6. Visuales de Combate")]
    public Sprite combatPortraitSprite;
    public Sprite combatIdleSprite;

    public Sprite companionSprite
    {
        get => combatPortraitSprite;
        set
        {
            combatPortraitSprite = value;
            combatIdleSprite = value;
        }
    }

    public bool IsAlive => HP_Actual > 0;

    // Propiedades de conveniencia y compatibilidad
    public int VidaActual { get => HP_Actual; set => HP_Actual = value; }
    public int VidaMaxima { get => HP_Max; set => HP_Max = value; }
    public int balasActuales { get => municionActual; set => municionActual = value; }
    public int balasMaximas { get => municionMaxima; set => municionMaxima = value; }
    public bool isDodgingActive { get => isDodgingUntilNextTurn; set => isDodgingUntilNextTurn = value; }
    public int nivel => Nivel;

    /// <summary>
    /// Propiedad estática de acceso rápido para consultar o activar al Detective en el equipo.
    /// </summary>
    public static bool IsDetectiveInPartyGlobal
    {
        get
        {
            if (s_instance != null)
            {
                return s_instance.hasDetectiveInParty;
            }
            if (s_snapshot.isSaved)
            {
                return s_snapshot.hasDetectiveInParty;
            }
            CompanionFollowAI companion = FindAnyObjectByType<CompanionFollowAI>();
            if (companion != null && companion.isFollowingPlayer)
            {
                return true;
            }
            return false;
        }
        set
        {
            if (s_instance != null)
            {
                s_instance.hasDetectiveInParty = value;
                s_instance.SaveProgressionSnapshot();
            }
            else
            {
                s_snapshot.hasDetectiveInParty = value;
            }
        }
    }

    private void Awake()
    {
        s_instance = this;

        if (combatPortraitSprite == null || combatPortraitSprite.name.Contains("Cardo") || combatPortraitSprite.name == "1")
        {
            Sprite detPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Detective.png");
            if (detPortrait != null)
            {
                combatPortraitSprite = detPortrait;
            }
        }
        if (combatIdleSprite == null || combatIdleSprite.name.Contains("Cardo") || combatIdleSprite.name == "1")
        {
            Sprite detStand = RuntimeAssetCatalog.GetSprite("Assets/Sprites/Detective/detective_stand_down.png");
            if (detStand != null)
            {
                combatIdleSprite = detStand;
            }
        }

        if (s_snapshot.isSaved)
        {
            RestoreFromSnapshot();
        }
        else
        {
            HP_Actual = Mathf.Clamp(HP_Actual, 1, HP_Max);
            municionActual = Mathf.Clamp(municionActual, 0, municionMaxima);
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu")))
        {
            if (s_instance == this) s_instance = null;
            return;
        }

        if (s_instance == this)
        {
            SaveProgressionSnapshot();
            s_instance = null;
        }
    }

    /// <summary>
    /// Garantiza que exista una instancia de DetectiveStats en la escena actual (asociada al compañero o al jugador).
    /// </summary>
    public static DetectiveStats EnsureInScene()
    {
        if (s_instance != null)
        {
            return s_instance;
        }

        DetectiveStats existing = FindAnyObjectByType<DetectiveStats>();
        if (existing != null)
        {
            s_instance = existing;
            return existing;
        }

        CompanionFollowAI companion = FindAnyObjectByType<CompanionFollowAI>();
        if (companion != null)
        {
            DetectiveStats attached = companion.gameObject.GetComponent<DetectiveStats>();
            if (attached == null)
            {
                attached = companion.gameObject.AddComponent<DetectiveStats>();
            }
            if (companion.isFollowingPlayer)
            {
                attached.hasDetectiveInParty = true;
            }
            if (companion.spriteRenderer != null && companion.spriteRenderer.sprite != null)
            {
                attached.combatIdleSprite = companion.spriteRenderer.sprite;
                if (attached.combatPortraitSprite == null)
                {
                    attached.combatPortraitSprite = companion.spriteRenderer.sprite;
                }
            }
            Sprite detPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Detective.png");
            if (detPortrait != null)
            {
                attached.combatPortraitSprite = detPortrait;
            }
            s_instance = attached;
            return attached;
        }

        GameObject holder = new GameObject("DetectiveStats_Runtime");
        DetectiveStats created = holder.AddComponent<DetectiveStats>();
        s_instance = created;
        return created;
    }

    /// <summary>
    /// Sincroniza el estado del equipo con la presencia del compañero en la escena actual.
    /// </summary>
    public void SyncPartyStateWithCompanion()
    {
        if (s_snapshot.isSaved && s_snapshot.hasDetectiveInParty)
        {
            hasDetectiveInParty = true;
        }

        CompanionFollowAI companion = FindAnyObjectByType<CompanionFollowAI>();
        if (companion != null)
        {
            if (companion.isFollowingPlayer)
            {
                hasDetectiveInParty = true;
            }
            if (combatIdleSprite == null && companion.spriteRenderer != null)
            {
                combatIdleSprite = companion.spriteRenderer.sprite;
            }
            if (combatPortraitSprite == null && companion.spriteRenderer != null)
            {
                combatPortraitSprite = companion.spriteRenderer.sprite;
            }
        }
    }

    /// <summary>
    /// Reinicia estados temporales al comenzar un nuevo combate.
    /// </summary>
    public void ResetCombatTemporaryStates()
    {
        isDodgingUntilNextTurn = false;
        skillDodgeBonusPercent = 0f;
        tacticalAttackBonus = 0;
        tacticalBuffTurnsRemaining = 0;
        enfriamientoArma = 0;

        if (HP_Actual <= 0)
        {
            HP_Actual = 1; // Si cayó en combate previo, reanima con exactamente 1 de vida para el siguiente combate
        }
        if (municionActual <= 0)
        {
            municionActual = Mathf.Max(2, municionMaxima / 2);
        }
    }

    /// <summary>
    /// Se invoca al inicio del turno propio del Detective para limpiar su postura de esquiva previa
    /// y reducir enfriamientos de su arma o buffs.
    /// </summary>
    public void OnStartDetectiveTurn()
    {
        isDodgingUntilNextTurn = false;

        if (enfriamientoArma > 0)
        {
            enfriamientoArma--;
        }

        if (tacticalBuffTurnsRemaining > 0)
        {
            tacticalBuffTurnsRemaining--;
            if (tacticalBuffTurnsRemaining <= 0)
            {
                tacticalAttackBonus = 0;
                skillDodgeBonusPercent = 0f;
            }
        }
    }

    /// <summary>
    /// Activa la acción defensiva 'Esquivar', incrementando temporalmente Probabilidad_Esquiva hasta su próximo turno.
    /// </summary>
    public float ActivateDodgeStance()
    {
        isDodgingUntilNextTurn = true;
        return GetEffectiveDodgeChance();
    }

    /// <summary>
    /// Calcula la Probabilidad_Esquiva efectiva actual (base + esquiva activa + bonos tácticos).
    /// </summary>
    public float GetEffectiveDodgeChance()
    {
        float total = Probabilidad_Esquiva + skillDodgeBonusPercent;
        if (isDodgingUntilNextTurn)
        {
            total += dodgeActionBonusPercent;
        }
        return Mathf.Clamp(total, 0f, 90f);
    }

    /// <summary>
    /// Evalúa si el Detective logra esquivar un ataque entrante usando su Probabilidad_Esquiva actual.
    /// </summary>
    public bool TryRollDodge(out float effectiveChance)
    {
        effectiveChance = GetEffectiveDodgeChance();
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < effectiveChance;
    }

    public int GetTotalMeleeAttack()
    {
        return Mathf.Max(1, Ataque_CuerpoACuerpo + tacticalAttackBonus);
    }

    public int GetTotalFirearmAttack()
    {
        return Mathf.Max(1, Ataque_Arma + tacticalAttackBonus);
    }

    public bool CanFireWeapon(out string reasonIfCannot)
    {
        if (municionActual <= 0)
        {
            reasonIfCannot = "Sin munición en el tambor (.38 Especial). Usa 'Concentración Táctica' o recoge munición.";
            return false;
        }
        if (enfriamientoArma > 0)
        {
            reasonIfCannot = $"El arma se está enfriando ({enfriamientoArma} turno Restante).";
            return false;
        }
        reasonIfCannot = string.Empty;
        return true;
    }

    public void ConsumeWeaponShot()
    {
        municionActual = Mathf.Max(0, municionActual - 1);
    }

    public int ReloadAmmo(int amount)
    {
        int before = municionActual;
        municionActual = Mathf.Clamp(municionActual + Mathf.Max(1, amount), 0, municionMaxima);
        enfriamientoArma = 0;
        return municionActual - before;
    }

    /// <summary>
    /// Aplica daño al Detective mitigado por su Defensa (y reducción extra si estaba en postura de esquiva).
    /// </summary>
    public int TakeDamage(int rawDamage, out int mitigatedByDefense)
    {
        int effectiveDefense = Defensa + (isDodgingUntilNextTurn ? 3 : 0);
        mitigatedByDefense = Mathf.Max(0, effectiveDefense);
        int finalDamage = Mathf.Max(1, rawDamage - mitigatedByDefense);
        HP_Actual = Mathf.Max(0, HP_Actual - finalDamage);
        SaveProgressionSnapshot();
        return finalDamage;
    }

    public void Heal(int amount)
    {
        HP_Actual = Mathf.Clamp(HP_Actual + Mathf.Max(0, amount), 0, HP_Max);
        SaveProgressionSnapshot();
    }

    public void RestoreFullHealth()
    {
        HP_Actual = HP_Max;
        municionActual = municionMaxima;
        SaveProgressionSnapshot();
    }

    /// <summary>
    /// Otorga experiencia al Detective al finalizar un combate con victoria.
    /// Si EXP_Actual >= EXP_Requerida, sube de nivel y ejecuta AutoAllocateStats() sin abrir UI de selección.
    /// </summary>
    public int GainExperienceFromVictory(int playerExpAwarded, out int detectiveExpGained, out List<string> autoLevelUpSummaryLines)
    {
        autoLevelUpSummaryLines = new List<string>();
        detectiveExpGained = Mathf.Max(1, Mathf.RoundToInt(playerExpAwarded * expShareRatio));
        EXP_Actual += detectiveExpGained;

        int levelsGained = 0;
        while (EXP_Actual >= EXP_Requerida)
        {
            EXP_Actual -= EXP_Requerida;
            Nivel++;
            levelsGained++;
            EXP_Requerida = GetRequiredExpForLevel(Nivel);

            string allocationReport = AutoAllocateStats();
            autoLevelUpSummaryLines.Add($"¡{detectiveName} sube a NIVEL {Nivel}! Mejora aleatoria: {allocationReport}");
        }

        SaveProgressionSnapshot();
        return levelsGained;
    }

    /// <summary>
    /// Subida de Nivel Desatendida (RNG Stat Allocation):
    /// Distribuye los puntos de mejora de forma 100% aleatoria (Random.Range) entre
    /// HP_Max, Ataque_CuerpoACuerpo, Ataque_Arma, Defensa y Velocidad.
    /// Prohibido abrir interfaz de selección para el jugador.
    /// </summary>
    public string AutoAllocateStats()
    {
        int hpGain = 0;
        int meleeGain = 0;
        int gunGain = 0;
        int defGain = 0;
        int spdGain = 0;

        int pointsToDistribute = Mathf.Max(1, statAllocationsPerLevel);
        for (int i = 0; i < pointsToDistribute; i++)
        {
            int randomStatIndex = UnityEngine.Random.Range(0, 5);
            switch (randomStatIndex)
            {
                case 0: // HP_Max
                    int extraHp = UnityEngine.Random.Range(8, 15);
                    HP_Max += extraHp;
                    HP_Actual = Mathf.Min(HP_Max, HP_Actual + extraHp);
                    hpGain += extraHp;
                    break;
                case 1: // Ataque_CuerpoACuerpo
                    int extraMelee = UnityEngine.Random.Range(2, 5);
                    Ataque_CuerpoACuerpo += extraMelee;
                    meleeGain += extraMelee;
                    break;
                case 2: // Ataque_Arma
                    int extraGun = UnityEngine.Random.Range(3, 6);
                    Ataque_Arma += extraGun;
                    gunGain += extraGun;
                    break;
                case 3: // Defensa
                    int extraDef = UnityEngine.Random.Range(1, 4);
                    Defensa += extraDef;
                    defGain += extraDef;
                    break;
                case 4: // Velocidad
                    int extraSpd = UnityEngine.Random.Range(1, 4);
                    Velocidad += extraSpd;
                    Probabilidad_Esquiva = Mathf.Min(65f, Probabilidad_Esquiva + 1.5f);
                    spdGain += extraSpd;
                    break;
            }
        }

        // Restaurar una fracción de salud y recargar munición al subir de nivel
        HP_Actual = Mathf.Min(HP_Max, HP_Actual + Mathf.RoundToInt(HP_Max * 0.25f));
        municionActual = municionMaxima;

        List<string> parts = new List<string>();
        if (hpGain > 0) parts.Add($"+{hpGain} HP_Max (Total {HP_Max})");
        if (meleeGain > 0) parts.Add($"+{meleeGain} Ataque_CuerpoACuerpo (Total {Ataque_CuerpoACuerpo})");
        if (gunGain > 0) parts.Add($"+{gunGain} Ataque_Arma (Total {Ataque_Arma})");
        if (defGain > 0) parts.Add($"+{defGain} Defensa (Total {Defensa})");
        if (spdGain > 0) parts.Add($"+{spdGain} Velocidad (Total {Velocidad})");

        string report = string.Join(", ", parts);
        Debug.Log($"[DetectiveStats] Nivel {Nivel} alcanzado automáticamente -> {report}");
        return report;
    }

    public int GetRequiredExpForLevel(int currentLevel)
    {
        return 100 + Mathf.Max(0, currentLevel - 1) * 55;
    }

    public void SaveProgressionSnapshot()
    {
        s_snapshot = new DetectiveProgressionSnapshot
        {
            isSaved = true,
            hasDetectiveInParty = hasDetectiveInParty,
            nivel = Nivel,
            expActual = EXP_Actual,
            expRequerida = EXP_Requerida,
            hpMax = HP_Max,
            hpActual = HP_Actual,
            ataqueCuerpoACuerpo = Ataque_CuerpoACuerpo,
            ataqueArma = Ataque_Arma,
            defensa = Defensa,
            velocidad = Velocidad,
            probabilidadEsquiva = Probabilidad_Esquiva,
            municionActual = municionActual,
            municionMaxima = municionMaxima
        };
    }

    public void RestoreFromSnapshot()
    {
        if (!s_snapshot.isSaved)
        {
            return;
        }

        hasDetectiveInParty = s_snapshot.hasDetectiveInParty;
        Nivel = Mathf.Max(1, s_snapshot.nivel);
        EXP_Actual = Mathf.Max(0, s_snapshot.expActual);
        EXP_Requerida = Mathf.Max(50, s_snapshot.expRequerida);
        HP_Max = Mathf.Max(1, s_snapshot.hpMax);
        HP_Actual = (s_snapshot.hpActual <= 0) ? 1 : Mathf.Clamp(s_snapshot.hpActual, 1, HP_Max);
        Ataque_CuerpoACuerpo = s_snapshot.ataqueCuerpoACuerpo;
        Ataque_Arma = s_snapshot.ataqueArma;
        Defensa = s_snapshot.defensa;
        Velocidad = s_snapshot.velocidad;
        Probabilidad_Esquiva = s_snapshot.probabilidadEsquiva;
        municionMaxima = Mathf.Max(1, s_snapshot.municionMaxima);
        municionActual = Mathf.Clamp(s_snapshot.municionActual, 0, municionMaxima);
    }

    public static void ResetSavedProgression()
    {
        s_snapshot = default;
    }
}
