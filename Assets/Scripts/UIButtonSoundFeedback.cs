using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Retroalimentación sonora para botones de la interfaz de usuario.
/// Reproduce el efecto sfxEsquive (Cursor 2 Sine) al pasar el puntero del mouse sobre el botón,
/// manteniendo la coherencia estética y sonora del proyecto.
/// </summary>
public class UIButtonSoundFeedback : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Sonido al pasar el ratón (Hover)")]
    [Tooltip("Clip personalizado para hover. Si es nulo, usa el sonido de Cursor registrado en MusicManager.")]
    public AudioClip customHoverClip;

    [Range(0f, 1f)]
    public float hoverVolumeScale = 0.55f;

    [Header("Sonido al hacer clic (Opcional)")]
    [Tooltip("Si está activo, reproduce sonido de clic al pulsar el botón.")]
    public bool playClickSound = false;

    [Tooltip("Clip personalizado para clic. Si es nulo y playClickSound está activo, usa el sonido de confirmación de diálogo.")]
    public AudioClip customClickClip;

    [Range(0f, 1f)]
    public float clickVolumeScale = 0.85f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (MusicManager.Instance == null) return;

        if (customHoverClip != null)
        {
            MusicManager.Instance.PlaySFX(customHoverClip, hoverVolumeScale);
        }
        else
        {
            MusicManager.Instance.PlayEsquiveSfx();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!playClickSound || MusicManager.Instance == null) return;

        if (customClickClip != null)
        {
            MusicManager.Instance.PlaySFX(customClickClip, clickVolumeScale);
        }
        else
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }
    }
}
