using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Receta de construcción improvisada para la habilidad "Intelecto" en combate.
/// </summary>
[CreateAssetMenu(fileName = "NewRecipe", menuName = "RPG/Intelecto Recipe")]
public class CraftingRecipeData : ScriptableObject
{
    [Header("Identidad de la Receta")]
    public string recipeName = "Molotov Improvisada";

    [TextArea(2, 3)]
    [Tooltip("Lo que dice la voz de Intelecto al armarlo en combate.")]
    public string intelectoComment = "VOZ [INTELECTO]: \"Combustible inflamable en un envase de vidrio con mecha. Ciencia callejera pura.\"";

    [Header("Materiales Necesarios (2 Objetos del Inventario)")]
    public ItemData materialA;
    public ItemData materialB;

    [Header("Efectos al Fabricar/Usar en Combate")]
    [Tooltip("Daño directo al enemigo ignorando su defensa.")]
    public int damageIgnoringDefense = 30;

    [Tooltip("Vida que recupera Enrique si la receta construye un objeto curativo potente.")]
    public int healAmount = 0;

    [FormerlySerializedAs("appliesStunOrConfusion")]
    [Tooltip("Si es true, confunde o aturde al enemigo por 1 turno.")]
    public bool appliesConfusion = true;

    [Header("Efectos Tácticos Adicionales (Nuevos Crafteos)")]
    [Tooltip("Si es true, el efecto ofensivo/estado impacta a TODOS los enemigos vivos en combate.")]
    public bool isAreaOfEffect = false;

    [Tooltip("Reducción de Defensa aplicada al enemigo (o a todos si es en área).")]
    public int enemyDefenseDebuff = 0;

    [Tooltip("Reducción de Ataque aplicada al enemigo (o a todos si es en área).")]
    public int enemyAttackDebuff = 0;

    [Tooltip("Reducción de Motricidad/Velocidad aplicada al enemigo (o a todos si es en área).")]
    public int enemyMotricidadDebuff = 0;

    [Tooltip("Bonificación de Ataque Físico para el próximo golpe de Enrique.")]
    public int playerAttackBuff = 0;

    [Tooltip("Si es true, activa automáticamente la Guardia Defensiva de Motricidad en esta ronda.")]
    public bool grantsPlayerGuard = false;

    [Tooltip("Cantidad de balas .38 Especial que recarga para el Detective Vargas si está en el equipo.")]
    public int detectiveAmmoRestored = 0;

    public string GetCompactEffectTag(int healBonus = 0, int dmgBonus = 0)
    {
        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
        if (damageIgnoringDefense > 0)
        {
            string areaStr = isAreaOfEffect ? " Área" : "";
            parts.Add($"{damageIgnoringDefense + dmgBonus} Daño{areaStr}");
        }
        if (healAmount > 0)
        {
            parts.Add($"+{healAmount + healBonus} HP");
        }
        if (appliesConfusion)
        {
            parts.Add(isAreaOfEffect ? "Aturde Todos" : "Aturde");
        }
        if (enemyDefenseDebuff > 0)
        {
            parts.Add($"-{enemyDefenseDebuff} DEF");
        }
        if (enemyAttackDebuff > 0)
        {
            parts.Add($"-{enemyAttackDebuff} ATQ Enem.");
        }
        if (enemyMotricidadDebuff > 0)
        {
            parts.Add($"-{enemyMotricidadDebuff} VEL");
        }
        if (playerAttackBuff > 0)
        {
            parts.Add($"+{playerAttackBuff} ATQ");
        }
        if (grantsPlayerGuard)
        {
            parts.Add("Guardia");
        }
        if (detectiveAmmoRestored > 0)
        {
            parts.Add($"+{detectiveAmmoRestored} Balas .38");
        }
        return parts.Count > 0 ? " [" + string.Join(" | ", parts) + "]" : "";
    }

    // Propiedad de compatibilidad
    public bool appliesStunOrConfusion
    {
        get => appliesConfusion;
        set => appliesConfusion = value;
    }

    public ItemData ingredientA
    {
        get => materialA;
        set => materialA = value;
    }

    public ItemData ingredientB
    {
        get => materialB;
        set => materialB = value;
    }

    public ItemData resultItem
    {
        get => null;
        set { }
    }
}
