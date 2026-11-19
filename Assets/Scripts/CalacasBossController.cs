using System;
using UnityEngine;

/// <summary>
/// Controlador del Jefe Final del Juego ("El Calacas - Líder del Crimen Clandestino").
/// Ubicado en lo más profundo de su guarida subterránea (Nivel 3).
/// Presenta una máquina de combate de 3 fases con mecánicas especiales:
/// 1. FaseMantoDeSombras: Gran evasión (35%) y contragolpe con puñal rápido. Vulnerable al Disparo del Detective y a la Bomba de Chile.
/// 2. FaseNieblaVenenosa: Despliega gas tóxico en el pasillo subterráneo y ejecuta 'Corte Sombrío Doble' dañando a Enrique y Vargas.
/// 3. FaseFrenesiMortal: Al caer bajo el 33% de vida, entra en modo Berserk desesperado (+45% ataque, armadura rota).
///
/// Al llegar su vida a 0, coordina el cierre narrativo noir donde se revela el contenido de la caja misteriosa
/// y concluye el juego hacia el menú principal.
/// </summary>
public class CalacasBossController : Level2BossController
{
    public enum CalacasPhase
    {
        MantoDeSombras = 0,
        NieblaVenenosa = 1,
        FrenesiMortal = 2
    }

    [Header("Mecánicas Únicas de El Calacas")]
    public CalacasPhase calacasPhase = CalacasPhase.MantoDeSombras;

    [Range(0.1f, 0.6f)]
    [Tooltip("Probabilidad de esquivar ataques convencionales en Fase 1 si no se usa Intelecto o Disparo.")]
    public float sombraEvasionChance = 0.35f;

    [Tooltip("Daño del contraataque con puñal en Fase 1.")]
    public int punalCounterDamage = 13;

    [Tooltip("Daño ambiental del gas venenoso en Fase 2.")]
    public int venenoFieldDamage = 6;

    [Header("Textos de Victoria y Cierre del Juego")]
    [TextArea(2, 4)]
    public string[] finalVictoryNarrativeLines = new string[]
    {
        "NARRADOR: \"El Calacas cae de rodillas contra las losas húmedas de su sanctum. Su máscara y su cuchilla tintinean en el suelo frío de la guarida.\"",
        "EL CALACAS: \"(Tosiendo sangre entre la penumbra) Maldito repartidor... No sabes lo que has desatado. Esa caja... no era un paquete de contrabando...\"",
        "DETECTIVE VARGAS: \"(Apuntando con su revólver humeante) Cállate, Calacas. Tu red de extorsión y tus matones acaban de perder su cabeza.\"",
        "ENRIQUE BOSQUES: \"(Abriendo los sellos de la misteriosa caja) Solo son documentos contables... nóminas del gobierno, expedientes policiales...\"",
        "VOZ [INTELECTO]: \"La prueba definitiva. La mafia y los altos mandos de Santa Felipina estaban en la misma nómina. El caso está cerrado.\"",
        "VOZ [MUNDO INTERNO]: \"El zumbido en tus oídos disminuye poco a poco. La ciudad sigue allá arriba, pero las voces por fin descansan en silencio.\"",
        "[ FIN DE LA PARTIDA — SANTA FELIPINA EN PAZ ]"
    };

    private void Reset()
    {
        bossDisplayName = "EL CALACAS";
        nextLevelSceneName = "00_MenuIntro";
        bossDefeatedTransitionLines = finalVictoryNarrativeLines;
    }

    public override void InitializeBossCombat()
    {
        calacasPhase = CalacasPhase.MantoDeSombras;
        currentPhase = BossCombatPhase.FaseEscudoBlindado;
        turnsInCurrentPhase = 0;
        toxicSmokeFieldActive = false;
        toxicSmokeTurnsRemaining = 0;
        bossDisplayName = "EL CALACAS";
        nextLevelSceneName = "00_MenuIntro";
        bossDefeatedTransitionLines = finalVictoryNarrativeLines;
    }

    public override string GetPhaseStatusBadge()
    {
        switch (calacasPhase)
        {
            case CalacasPhase.MantoDeSombras:
                return " <color=#B366FF>[MANTO DE SOMBRAS: 35% EVASIÓN]</color>";
            case CalacasPhase.NieblaVenenosa:
                return " <color=#55FFAA>[NIEBLA TÓXICA SUBTERRÁNEA]</color>";
            case CalacasPhase.FrenesiMortal:
                return " <color=#FF3333>[FRENESÍ MORTAL: +45% ATAQUE / DEF 0]</color>";
            default:
                return " <color=#FFD84D>[JEFE FINAL: EL CALACAS]</color>";
        }
    }

    public override int ProcessIncomingDamage(
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

        // Si estamos en Fase 1 (Manto de Sombras)
        if (calacasPhase == CalacasPhase.MantoDeSombras)
        {
            if (canBreakArmorShield)
            {
                // Un impacto perforante (disparo del detective o bomba de chile) disipa las sombras
                calacasPhase = CalacasPhase.NieblaVenenosa;
                turnsInCurrentPhase = 0;
                toxicSmokeFieldActive = true;
                toxicSmokeTurnsRemaining = 3;
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * 1.35f));
                mechanicFeedbackMessage = $"¡IMPACTO TÁCTICO DE {attackerName.ToUpper()}! ¡El Manto de Sombras de El Calacas se DISIPA (-{finalDamage} HP)! Despliega niebla venenosa.";
                return finalDamage;
            }

            // Probabilidad de esquiva ágil
            float roll = UnityEngine.Random.value;
            if (roll < sombraEvasionChance && isDirectMeleeAttack)
            {
                triggeredCounterAttack = true;
                counterAttackDamage = punalCounterDamage;
                finalDamage = 0;
                mechanicFeedbackMessage = $"¡DESPLAZAMIENTO SOMBRÍO! El Calacas se desvanece en la penumbra, esquiva el ataque y contraataca con su estilete (-{counterAttackDamage} HP a Enrique).";
                return 0;
            }

            finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * 0.85f));
            mechanicFeedbackMessage = $"{attackerName} conecta un golpe sólido contra El Calacas (-{finalDamage} HP).";
            return finalDamage;
        }

        // Si estamos en Fase 3 (Frenesí)
        if (calacasPhase == CalacasPhase.FrenesiMortal)
        {
            finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * 1.45f));
            mechanicFeedbackMessage = $"¡IMPACTO CRÍTICO EN FRENESÍ! El Calacas descuidó su defensa por atacar (-{finalDamage} HP).";
            return finalDamage;
        }

        // Fase 2 (Niebla Venenosa estándar)
        mechanicFeedbackMessage = $"{attackerName} golpea a El Calacas entre la niebla venenosa (-{finalDamage} HP).";
        return finalDamage;
    }

    public override bool TryBreakShieldWithSkill(string skillName, out string feedback)
    {
        if (calacasPhase == CalacasPhase.MantoDeSombras)
        {
            calacasPhase = CalacasPhase.NieblaVenenosa;
            turnsInCurrentPhase = 0;
            toxicSmokeFieldActive = true;
            toxicSmokeTurnsRemaining = 3;
            feedback = $"¡'{skillName}' alumbra el pasillo oscuro! Las sombras de El Calacas se desvanecen.";
            return true;
        }

        feedback = $"'{skillName}' desestabiliza a El Calacas en el combate.";
        return false;
    }

    public override void ExecuteBossTurnStateTransition(
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

        float hpPercent = (float)currentHP / Mathf.Max(1, maxHP);

        // Transición automática a Frenesí si la vida cae por debajo del 35%
        if (hpPercent <= 0.35f && calacasPhase != CalacasPhase.FrenesiMortal)
        {
            calacasPhase = CalacasPhase.FrenesiMortal;
            turnsInCurrentPhase = 0;
            attackDamageMultiplier = 1.45f;
            bossActionLog = "¡EL CALACAS ENTRA EN FRENESÍ MORTAL! Su mirada se desquicia, desenvaina dos estiletes y arremete con velocidad salvaje (+45% daño, 0 armadura).";
            return;
        }

        // Transición a Niebla si cae por debajo del 70%
        if (hpPercent <= 0.70f && calacasPhase == CalacasPhase.MantoDeSombras)
        {
            calacasPhase = CalacasPhase.NieblaVenenosa;
            turnsInCurrentPhase = 0;
            toxicSmokeFieldActive = true;
            toxicSmokeTurnsRemaining = 3;
            attackDamageMultiplier = 1.20f;
            bossActionLog = "¡El Calacas lanza una ampolleta de gas tóxico contra el suelo de piedra! La guarida se llena de vapor corrosivo.";
            fieldDamageToParty = venenoFieldDamage;
            return;
        }

        if (toxicSmokeFieldActive && toxicSmokeTurnsRemaining > 0)
        {
            toxicSmokeTurnsRemaining--;
            fieldDamageToParty = venenoFieldDamage;
            if (toxicSmokeTurnsRemaining <= 0)
            {
                toxicSmokeFieldActive = false;
            }
        }

        switch (calacasPhase)
        {
            case CalacasPhase.FrenesiMortal:
                attackDamageMultiplier = 1.40f;
                bossActionLog = "¡El Calacas descarga una ráfaga despiadada de cortes y patadas voladoras dirigidas a órganos vitales!";
                break;

            case CalacasPhase.NieblaVenenosa:
                attackDamageMultiplier = 1.25f;
                bossActionLog = "¡El Calacas ejecuta su técnica 'Corte Sombrío': un tajo giratorio que golpea a ambos aliados en el pasillo estrecho!";
                break;

            default:
                attackDamageMultiplier = 1.10f;
                bossActionLog = "El Calacas se mueve velozmente entre los pilares de piedra y lanza una estocada certera con su puñal.";
                break;
        }
    }

    public override void NotifyBossDefeated(BattleManager bm)
    {
        base.NotifyBossDefeated(bm);

        if (bm != null)
        {
            bm.isCurrentEncounterBoss = true;
            bm.currentBossNextScene = "00_MenuIntro";
            bm.currentBossVictoryNarrative = finalVictoryNarrativeLines;
        }
    }
}
