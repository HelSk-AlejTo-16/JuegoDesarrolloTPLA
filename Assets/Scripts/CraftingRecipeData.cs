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

    // Propiedad de compatibilidad
    public bool appliesStunOrConfusion
    {
        get => appliesConfusion;
        set => appliesConfusion = value;
    }
}
