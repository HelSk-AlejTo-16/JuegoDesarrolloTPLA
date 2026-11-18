using UnityEngine;

public enum EnemyType
{
    LacayoRapidoDebil,
    LacayoLentoFuerte,
    JefeElCalacas,
    SicarioCuchilleroNivel2,
    SicarioVerdugoNivel2,
    JefeNivel2Capataz
}

/// <summary>
/// Sistema 2: Configuración de datos de enemigos mediante ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "RPG/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identidad y Tipo")]
    public string enemyName = "Nuevo Enemigo";
    public EnemyType enemyType = EnemyType.LacayoRapidoDebil;

    [Header("Estadísticas de Combate")]
    public int maxHP = 45;
    public int attack = 10;
    public int defense = 2;

    [Tooltip("Define su velocidad en el orden de turno contra la Motricidad de Enrique.")]
    public int motricidad = 10;

    [Header("Recompensa de Experiencia y Ataques")]
    [Tooltip("Cantidad de EXP otorgada al derrotar a este enemigo (0 usa el valor base del nivel).")]
    public int EXP_Reward = 45;

    [Tooltip("Nombre del ataque principal del enemigo mostrado en el registro de combate.")]
    public string primaryAttackName = "Golpe Callejero";

    [Tooltip("Nombre del ataque especial/secundario del enemigo.")]
    public string secondaryAttackName = "Embate Brutal";

    [Header("Representación Visual y Animaciones")]
    [Tooltip("Color sólido para representarlo si no hay sprite.")]
    public Color placeholderColor = new Color(1f, 0.55f, 0.1f, 1f);

    [Tooltip("Sprite principal (primer frame de Idle) del enemigo.")]
    public Sprite enemySprite;

    [Tooltip("Animator Controller para animar al enemigo en el mapa.")]
    public RuntimeAnimatorController animatorController;

    [Header("Frames de Animación para Combate")]
    public Sprite[] idleFrames;
    public Sprite[] idleAltFrames;
    public Sprite[] walkFrames;
    public Sprite[] runFrames;
    public Sprite[] attackFrames;
    public Sprite[] attack2Frames;
    public Sprite[] attack3Frames;
    public Sprite[] specialFrames;
    public Sprite[] hurtFrames;
    public Sprite[] deadFrames;

    [Header("Diálogo Opcional a Mitad de Vida (vacío = sin diálogo)")]
    [TextArea(2, 4)]
    public string[] halfHealthDialogueLines;
}
