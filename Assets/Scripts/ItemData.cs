using UnityEngine;

public enum ItemCategory
{
    ConsumibleCuracion = 0,
    MaterialDeCrafteo = 1,
    ObjetoOfensivoDirecto = 2,
    Arma = 3,

    // Alias de compatibilidad
    ConsumibleDirecto = ConsumibleCuracion,
    MaterialDeCalle = MaterialDeCrafteo,
    ArtefactoCrafteado = ObjetoOfensivoDirecto,
    Material = MaterialDeCrafteo,
    Consumible = ConsumibleCuracion
}

/// <summary>
/// Configuración de un objeto del inventario (Consumible de Curación, Material de Crafteo u Objeto Ofensivo Directo).
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "RPG/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identidad del Objeto")]
    public string itemID = "item_nuevo";
    public string itemName = "Nuevo Objeto";

    [TextArea(2, 3)]
    public string description = "Objeto encontrado en las calles de Santa Felipina.";

    [TextArea(2, 3)]
    [Tooltip("Mensaje que se muestra en el registro de combate al usar el objeto.")]
    public string combatUseMessage = "Enrique se toma un refresco bien frio y recupera energia!";

    [Header("Representación Visual (Placeholder / Arte Final)")]
    [Tooltip("Color para representarlo en el mapa y en la UI sin sprites.")]
    public Color placeholderColor = new Color(0.35f, 0.90f, 0.45f, 1f);

    [Tooltip("Opcional: Icono definitivo del objeto para el futuro.")]
    public Sprite icon;

    [Header("Categoría y Efectos en Combate")]
    public ItemCategory category = ItemCategory.ConsumibleCuracion;

    [Tooltip("Cantidad de HP que cura a Enrique al usarse en combate.")]
    public int healAmount = 35;

    [Tooltip("Cantidad de daño directo si es un objeto ofensivo directo.")]
    public int damageAmount = 0;

    [Tooltip("Si es true, limpia estados negativos de Enrique.")]
    public bool curesStatusEffects = false;

    [Tooltip("Si es true, aturde o confunde al objetivo.")]
    public bool confusesTarget = false;

    [Header(" Estadísticas de Arma (Si category == Arma)")]
    [Tooltip("Bonificación de daño físico que se suma a Corpóreo mientras esta arma esté equipada.")]
    public int weaponAttackBonus = 0;

    [Tooltip("Bonificación de Motricidad (velocidad de turno y esquive) mientras esta arma esté equipada.")]
    public int weaponMotricidadBonus = 0;

    // Propiedades de compatibilidad con versiones previas
    public int effectValue
    {
        get => (category == ItemCategory.ConsumibleCuracion || healAmount > 0) ? healAmount : damageAmount;
        set
        {
            if (category == ItemCategory.ConsumibleCuracion)
                healAmount = value;
            else
                damageAmount = value;
        }
    }

    public bool healsPlayer
    {
        get => category == ItemCategory.ConsumibleCuracion || healAmount > 0;
        set
        {
            if (value) category = ItemCategory.ConsumibleCuracion;
        }
    }
}
