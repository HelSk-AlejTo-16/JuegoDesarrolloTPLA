using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla el sub-panel de Mundo Interno en combate.
/// Permite a Enrique acceder a sus habilidades psicológicas y emocionales:
/// 1. INSULTAR / PROVOCAR: Reduce el ataque del enemigo objetivo (-4 ATQ).
/// 2. ANIMARSE / AUTO-SUGESTIÓN: Enrique se llena de coraje (+5 ATQ para su próximo golpe Corpóreo).
/// 3. DELIRIO PSÍQUICO: Confunde al enemigo objetivo haciéndole perder su próximo turno.
/// - CANCELAR / VOLVER: Regresa a las 4 voces sin consumir el turno.
/// </summary>
public class CombatMundoInternoUI : MonoBehaviour
{
    [Header("Referencias Principales")]
    public BattleManager battleManager;
    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [Header("Botones de Acción")]
    public Button insultarButton;
    public Button animarseButton;
    public Button delirioButton;
    public Button cancelButton;

    [Header("Textos de los Botones (Opcional)")]
    public TextMeshProUGUI insultarText;
    public TextMeshProUGUI animarseText;
    public TextMeshProUGUI delirioText;

    private void Awake()
    {
        if (insultarButton != null)
        {
            insultarButton.onClick.RemoveListener(OnInsultarClicked);
            insultarButton.onClick.AddListener(OnInsultarClicked);
        }
        if (animarseButton != null)
        {
            animarseButton.onClick.RemoveListener(OnAnimarseClicked);
            animarseButton.onClick.AddListener(OnAnimarseClicked);
        }
        if (delirioButton != null)
        {
            delirioButton.onClick.RemoveListener(OnDelirioClicked);
            delirioButton.onClick.AddListener(OnDelirioClicked);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    public void ShowAndRefresh(BattleManager manager)
    {
        if (manager != null)
        {
            battleManager = manager;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        RefreshUI();
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void RefreshUI()
    {
        if (battleManager == null) return;

        CombatEnemyInstance target = battleManager.GetSelectedLivingTarget();
        string targetName = (target != null && target.data != null) ? target.data.enemyName : "Enemigo";
        bool isDetectiveTurn = (battleManager.state == BattleState.DETECTIVETURN && battleManager.detectiveStats != null);

        if (isDetectiveTurn)
        {
            DetectiveStats dStats = battleManager.detectiveStats;
            if (titleText != null)
            {
                titleText.text = "TACTICA POLICIAL: HABILIDADES DEL DETECTIVE VARGAS";
            }
            if (descriptionText != null)
            {
                descriptionText.text = $"OBJETIVO: {targetName.ToUpper()}  |  BALAS .38: {dStats.municionActual}/{dStats.municionMaxima}\nElige que habilidad tactica ejecutar (no gasta turno si cancelas):";
            }
            if (insultarText != null)
            {
                insultarText.text = $"[1. ANALIZAR PUNTO DEBIL] - Expone la guardia de {targetName} (-4 DEF y +30% dano por 2 turnos)";
            }
            if (animarseText != null)
            {
                animarseText.text = "[2. CONCENTRACION TACTICA] - Recarga +3 balas .38 y otorga +5 ATQ y +15% Esquiva";
            }
            if (delirioText != null)
            {
                delirioText.text = $"[3. DISPARO DE SUPRESION] - Impacta a {targetName} y reduce su ataque (-5 ATQ y -4 VEL) (1 Bala)";
            }
            return;
        }

        if (titleText != null)
        {
            titleText.text = "MUNDO INTERNO: PSICOLOGIA Y EMOCIONES DE ENRIQUE";
        }

        if (descriptionText != null)
        {
            string buffInfo = (battleManager.playerAttackBuff > 0)
                ? $"  |  ENRIQUE: [ANIMADO +{battleManager.playerAttackBuff} ATQ listo]"
                : "";
            string debuffInfo = (target != null && target.attackDebuff > 0)
                ? $"  |  {targetName}: [-{target.attackDebuff} ATQ]"
                : "";

            descriptionText.text = $"OBJETIVO: {targetName.ToUpper()}{buffInfo}{debuffInfo}\nElige cómo influir en la psique del combate (no gasta turno si cancelas):";
        }

        PlayerStats pStats = (battleManager.playerStats != null)
            ? battleManager.playerStats
            : FindAnyObjectByType<PlayerStats>();
        int insultDebuff = (pStats != null) ? pStats.GetInsultarDebuffAmount() : 4;
        int animarseBuff = (pStats != null) ? pStats.GetAnimarseBuffAmount() : 5;
        int delirioPsyDmg = (pStats != null) ? pStats.GetDelirioPsychicDamage() : 0;

        if (insultarText != null)
        {
            int currentTargetAtk = (target != null && target.data != null)
                ? Mathf.Max(1, target.data.attack - target.attackDebuff)
                : 0;
            insultarText.text = $"[1. INSULTAR] - Provoca a {targetName} reduciendo su ataque (-{insultDebuff} ATQ) (Atq actual: {currentTargetAtk})";
        }

        if (animarseText != null)
        {
            animarseText.text = $"[2. ANIMARSE] - Enrique se auto-sugestiona (+{animarseBuff} ATQ para el proximo golpe Corporeo)";
        }

        if (delirioText != null)
        {
            string extraDmgText = delirioPsyDmg > 0 ? $" e inflige {delirioPsyDmg} de dano psiquico" : "";
            delirioText.text = $"[3. DELIRIO PSIQUICO] - Confunde a {targetName} (pierde su proximo turno){extraDmgText}";
        }
    }

    public void OnInsultarClicked()
    {
        Hide();
        if (battleManager != null)
        {
            if (battleManager.state == BattleState.DETECTIVETURN)
                battleManager.ExecuteDetectiveSkill(DetectiveActionMenu.DetectiveSkillType.AnalizarPuntoDebil);
            else
                battleManager.ExecuteMundoInternoInsultar();
        }
    }

    public void OnAnimarseClicked()
    {
        Hide();
        if (battleManager != null)
        {
            if (battleManager.state == BattleState.DETECTIVETURN)
                battleManager.ExecuteDetectiveSkill(DetectiveActionMenu.DetectiveSkillType.ConcentracionTactica);
            else
                battleManager.ExecuteMundoInternoAnimarse();
        }
    }

    public void OnDelirioClicked()
    {
        Hide();
        if (battleManager != null)
        {
            if (battleManager.state == BattleState.DETECTIVETURN)
                battleManager.ExecuteDetectiveSkill(DetectiveActionMenu.DetectiveSkillType.DisparoDeSupresion);
            else
                battleManager.ExecuteMundoInternoDelirio();
        }
    }

    public void OnCancelClicked()
    {
        Hide();
        if (battleManager != null)
        {
            battleManager.CloseMundoInternoPanel();
        }
    }
}
