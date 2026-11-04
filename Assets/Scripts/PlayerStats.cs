using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sistema 2: Estadísticas de Enrique Bosques, sus 4 Voces/Habilidades y Sistema de Niveles/Progresión.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Nivel y Experiencia de Enrique")]
    [Tooltip("Nivel actual de Enrique Bosques.")]
    public int level = 1;

    [Tooltip("Experiencia actual acumulada hacia el siguiente nivel.")]
    public int currentXP = 0;

    [Tooltip("Experiencia necesaria para subir al siguiente nivel.")]
    public int xpToNextLevel = 100;

    [Tooltip("Puntos de habilidad disponibles para mejorar cualquiera de las 4 voces.")]
    public int availableSkillPoints = 0;

    [Tooltip("Cantidad de combates ganados en la partida actual.")]
    public int battlesWonCount = 0;

    [Header("Salud de Enrique")]
    public int maxHP = 100;
    public int currentHP = 100;

    [Header("Las 4 Voces / Estadísticas Base")]
    [Tooltip("Corpóreo: Daño físico directo (golpes) y vida máxima.")]
    public int corporeo = 12;

    [Tooltip("Intelecto: Construcción/improvisación y eficacia de objetos/recetas en combate.")]
    public int intelecto = 14;

    [Tooltip("Motricidad: Velocidad para el orden de turno y eficacia de esquive/defensa.")]
    public int motricidad = 12;

    [Tooltip("Mundo Interno: Aplicación y potencia de habilidades psicológicas (Insultar, Animarse, Delirio).")]
    public int mundoInterno = 12;

    [Header("Mejoras Invertidas por Habilidad")]
    public int corporeoUpgrades = 0;
    public int intelectoUpgrades = 0;
    public int motricidadUpgrades = 0;
    public int mundoInternoUpgrades = 0;

    public bool IsAlive => currentHP > 0;

    public event Action OnStatsChanged;
    public event Action<int> OnLevelUp;

    // Snapshot estático para conservar el nivel y mejoras entre escenas (Nivel 1 -> Nivel 2)
    private struct ProgressionSnapshot
    {
        public bool initialized;
        public int level;
        public int currentXP;
        public int xpToNextLevel;
        public int availableSkillPoints;
        public int battlesWonCount;
        public int maxHP;
        public int currentHP;
        public int corporeo;
        public int intelecto;
        public int motricidad;
        public int mundoInterno;
        public int corporeoUpgrades;
        public int intelectoUpgrades;
        public int motricidadUpgrades;
        public int mundoInternoUpgrades;
    }

    private static ProgressionSnapshot savedSnapshot;
    private static bool suppressSaveOnDestroy = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        savedSnapshot = default;
        suppressSaveOnDestroy = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu") || activeScene.name.Contains("01_Nivel1")))
        {
            ResetSavedProgression();
        }
    }

    public static void ResetSavedProgression()
    {
        savedSnapshot = default;
        suppressSaveOnDestroy = true;
    }

    public static void EnableSaveOnDestroy()
    {
        suppressSaveOnDestroy = false;
    }

    private void Awake()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool isIntroOrMenu = activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu"));
        bool isLevel1 = activeScene.IsValid() && activeScene.name.Contains("01_Nivel1");

        // Al iniciar o reiniciar una partida en el Menú Intro o en el Nivel 1 (Santa Felipina), siempre arrancar fresco
        if (isIntroOrMenu || isLevel1)
        {
            ResetSavedProgression();
        }

        suppressSaveOnDestroy = false;

        if (savedSnapshot.initialized)
        {
            ApplySnapshot(savedSnapshot);
        }
        else
        {
            currentHP = Mathf.Clamp(currentHP, 1, maxHP);
            SaveProgressionSnapshot();
        }
    }

    private void Start()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && !activeScene.name.Contains("00_MenuIntro") && !activeScene.name.Contains("Intro") && !activeScene.name.Contains("Menu"))
        {
            PlayerInventoryAndWeaponsUI.EnsureInScene();
            PlayerLevelUpAndStatsUI.EnsureInScene();
        }
    }

    private void OnDestroy()
    {
        if (suppressSaveOnDestroy) return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu")))
        {
            return;
        }

        if (IsAlive)
        {
            SaveProgressionSnapshot();
        }
    }

    public void SaveProgressionSnapshot()
    {
        savedSnapshot = new ProgressionSnapshot
        {
            initialized = true,
            level = this.level,
            currentXP = this.currentXP,
            xpToNextLevel = this.xpToNextLevel,
            availableSkillPoints = this.availableSkillPoints,
            battlesWonCount = this.battlesWonCount,
            maxHP = this.maxHP,
            currentHP = Mathf.Clamp(this.currentHP, 1, this.maxHP),
            corporeo = this.corporeo,
            intelecto = this.intelecto,
            motricidad = this.motricidad,
            mundoInterno = this.mundoInterno,
            corporeoUpgrades = this.corporeoUpgrades,
            intelectoUpgrades = this.intelectoUpgrades,
            motricidadUpgrades = this.motricidadUpgrades,
            mundoInternoUpgrades = this.mundoInternoUpgrades
        };
    }

    private void ApplySnapshot(ProgressionSnapshot snap)
    {
        level = Mathf.Max(1, snap.level);
        currentXP = Mathf.Max(0, snap.currentXP);
        xpToNextLevel = Mathf.Max(50, snap.xpToNextLevel);
        availableSkillPoints = Mathf.Max(0, snap.availableSkillPoints);
        battlesWonCount = Mathf.Max(0, snap.battlesWonCount);
        maxHP = Mathf.Max(1, snap.maxHP);
        currentHP = Mathf.Clamp(snap.currentHP, 1, maxHP);
        corporeo = snap.corporeo;
        intelecto = snap.intelecto;
        motricidad = snap.motricidad;
        mundoInterno = snap.mundoInterno;
        corporeoUpgrades = snap.corporeoUpgrades;
        intelectoUpgrades = snap.intelectoUpgrades;
        motricidadUpgrades = snap.motricidadUpgrades;
        mundoInternoUpgrades = snap.mundoInternoUpgrades;
    }

    public void TakeDamage(int damage)
    {
        int finalDamage = Mathf.Max(0, damage);
        currentHP = Mathf.Max(0, currentHP - finalDamage);
        OnStatsChanged?.Invoke();
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        SaveProgressionSnapshot();
        OnStatsChanged?.Invoke();
    }

    public void RestoreFullHealth()
    {
        currentHP = maxHP;
        SaveProgressionSnapshot();
        OnStatsChanged?.Invoke();
    }

    // =========================================================================
    // SISTEMA DE EXPERIENCIA Y SUBIDA DE NIVEL EN COMBATE
    // =========================================================================

    /// <summary>
    /// Otorga experiencia al finalizar un combate.
    /// Garantiza que el primer combate ganado siempre suba a Enrique de nivel.
    /// Devuelve cuántos niveles subió en este combate.
    /// </summary>
    public int GainExperienceFromBattle(int enemyCount, bool isBoss, out int xpAwarded)
    {
        battlesWonCount++;

        int baseReward = isBoss ? 150 : Mathf.Max(65, enemyCount * 55);

        // Garantizar que el primer combate siempre suba de nivel a Enrique
        if (battlesWonCount == 1 && (currentXP + baseReward) < xpToNextLevel)
        {
            baseReward = xpToNextLevel - currentXP;
        }

        xpAwarded = baseReward;
        currentXP += xpAwarded;

        int levelsGained = 0;
        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            level++;
            availableSkillPoints++;
            levelsGained++;

            // Escalar ligeramente la experiencia requerida para el siguiente nivel
            xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.25f);

            // Recuperar un 25% de vida al subir de nivel
            int levelUpHeal = Mathf.Max(15, Mathf.RoundToInt(maxHP * 0.25f));
            currentHP = Mathf.Min(maxHP, currentHP + levelUpHeal);
        }

        SaveProgressionSnapshot();
        OnStatsChanged?.Invoke();

        if (levelsGained > 0)
        {
            OnLevelUp?.Invoke(level);
        }

        return levelsGained;
    }

    /// <summary>
    /// Mejora una de las 4 habilidades/voces gastando 1 punto de habilidad disponible.
    /// Hace que esa habilidad sea notablemente más poderosa en combate.
    /// </summary>
    public bool UpgradeSkill(SkillVoiceType voice)
    {
        if (availableSkillPoints <= 0)
            return false;

        availableSkillPoints--;

        switch (voice)
        {
            case SkillVoiceType.Corporeo:
                corporeo += 4;
                corporeoUpgrades++;
                maxHP += 15;
                currentHP = Mathf.Min(maxHP, currentHP + 15);
                break;

            case SkillVoiceType.Intelecto:
                intelecto += 4;
                intelectoUpgrades++;
                break;

            case SkillVoiceType.Motricidad:
                motricidad += 4;
                motricidadUpgrades++;
                break;

            case SkillVoiceType.MundoInterno:
                mundoInterno += 4;
                mundoInternoUpgrades++;
                break;
        }

        SaveProgressionSnapshot();
        OnStatsChanged?.Invoke();
        return true;
    }

    // =========================================================================
    // CÁLCULOS DE PODER DE CADA HABILIDAD EN COMBATE
    // =========================================================================

    /// <summary>
    /// Bonificación de daño para recetas ofensivas de Intelecto (ej. Molotov Improvisada) y objetos ofensivos.
    /// </summary>
    public int GetIntelectoDamageBonus()
    {
        int statAboveBase = Mathf.Max(0, intelecto - 14);
        return (statAboveBase * 2) + (intelectoUpgrades * 4);
    }

    /// <summary>
    /// Bonificación de curación para consumibles (Refresco, Torta) y recetas curativas (Vendaje Rudo).
    /// </summary>
    public int GetIntelectoHealBonus()
    {
        int statAboveBase = Mathf.Max(0, intelecto - 14);
        return (statAboveBase * 2) + (intelectoUpgrades * 4);
    }

    /// <summary>
    /// Probabilidad de esquivar completamente un ataque enemigo al usar Guardia de Motricidad (0.15 a 0.85).
    /// </summary>
    public float GetDodgeChanceAgainst(int enemyMotricidad)
    {
        float bonusFromUpgrades = motricidadUpgrades * 0.04f;
        int effectiveMot = GetEffectiveMotricidad();
        return Mathf.Clamp(0.25f + (effectiveMot - enemyMotricidad) * 0.05f + bonusFromUpgrades, 0.15f, 0.85f);
    }

    /// <summary>
    /// Bonificación de ataque físico otorgada por el arma equipada actualmente en InventoryManager.
    /// </summary>
    public int GetEquippedWeaponAttackBonus()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.equippedWeapon != null)
        {
            return InventoryManager.Instance.equippedWeapon.weaponAttackBonus;
        }
        return 0;
    }

    /// <summary>
    /// Bonificación de Motricidad otorgada por el arma equipada actualmente en InventoryManager.
    /// </summary>
    public int GetEquippedWeaponMotricidadBonus()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.equippedWeapon != null)
        {
            return InventoryManager.Instance.equippedWeapon.weaponMotricidadBonus;
        }
        return 0;
    }

    /// <summary>
    /// Daño físico total de Enrique (Corpóreo base + Bono del arma equipada).
    /// </summary>
    public int GetTotalPhysicalAttack()
    {
        return Mathf.Max(1, corporeo + GetEquippedWeaponAttackBonus());
    }

    /// <summary>
    /// Motricidad efectiva total de Enrique (Motricidad base + Bono del arma equipada).
    /// </summary>
    public int GetEffectiveMotricidad()
    {
        return Mathf.Max(1, motricidad + GetEquippedWeaponMotricidadBonus());
    }

    /// <summary>
    /// Nombre del arma equipada actualmente (o "Puños Desnudos" si no tiene arma equipada).
    /// </summary>
    public string GetEquippedWeaponName()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.equippedWeapon != null)
        {
            return InventoryManager.Instance.equippedWeapon.itemName;
        }
        return "Puños Desnudos";
    }

    public void NotifyEquipmentChanged()
    {
        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// Porcentaje base de esquive contra un enemigo estándar (Motricidad 10).
    /// </summary>
    public int GetDisplayDodgePercentage()
    {
        return Mathf.RoundToInt(GetDodgeChanceAgainst(10) * 100f);
    }

    /// <summary>
    /// Multiplicador de daño recibido cuando la Guardia de Motricidad amortigua un golpe (menor = más reducción).
    /// Base: 0.30f (reduce 70% del daño). Con mejoras reduce hasta un 85% del daño (0.15f).
    /// </summary>
    public float GetGuardDamageMultiplier()
    {
        float reduction = Mathf.Clamp(0.70f + (motricidadUpgrades * 0.05f) + Mathf.Max(0, motricidad - 12) * 0.005f, 0.70f, 0.85f);
        return 1f - reduction;
    }

    public int GetDisplayGuardReductionPercentage()
    {
        return Mathf.RoundToInt((1f - GetGuardDamageMultiplier()) * 100f);
    }

    /// <summary>
    /// Cantidad de ataque que pierde el enemigo al usar INSULTAR (Mundo Interno).
    /// Base: 4. Sube en +3 por cada mejora de Mundo Interno (4 -> 7 -> 10...).
    /// </summary>
    public int GetInsultarDebuffAmount()
    {
        int extra = Mathf.Max(0, (mundoInterno - 12) / 2) + (mundoInternoUpgrades * 1);
        return 4 + extra;
    }

    /// <summary>
    /// Bonificación de ataque para el próximo golpe Corpóreo al usar ANIMARSE (Mundo Interno).
    /// Base: 5. Sube en +4 por cada mejora de Mundo Interno (5 -> 9 -> 13...).
    /// </summary>
    public int GetAnimarseBuffAmount()
    {
        int extra = Mathf.Max(0, (mundoInterno - 12) / 2) + (mundoInternoUpgrades * 2);
        return 5 + extra;
    }

    /// <summary>
    /// Daño psíquico directo (ignorando defensa) que inflige DELIRIO PSÍQUICO además de confundir al enemigo.
    /// Base: 0 en rango inicial, y sube en +8 de daño psíquico por cada mejora de Mundo Interno.
    /// </summary>
    public int GetDelirioPsychicDamage()
    {
        if (mundoInterno <= 12 && mundoInternoUpgrades <= 0)
            return 0;
        return Mathf.Max(0, (mundoInterno - 12) * 2);
    }
}
