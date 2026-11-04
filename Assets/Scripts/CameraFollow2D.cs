using UnityEngine;

/// <summary>
/// Seguimiento suave de cámara 2D en tiempo real hacia Enrique_Player.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("Objetivo a seguir (si es null, busca automáticamente al Player).")]
    public Transform target;

    [Tooltip("Tiempo de suavizado para el movimiento de la cámara.")]
    public float smoothTime = 0.18f;

    [Tooltip("Offset respecto a la posición del jugador.")]
    public Vector3 offset = new Vector3(0f, 0.6f, -10f);

    [Header("Límites Opcionales de la Cámara")]
    public bool useBounds = false;
    public Vector2 minBounds = new Vector2(-15f, -10f);
    public Vector2 maxBounds = new Vector2(15f, 10f);

    private Vector3 currentVelocity = Vector3.zero;

    private void LateUpdate()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                return;
            }
        }

        Vector3 targetPos = target.position + offset;

        if (useBounds)
        {
            targetPos.x = Mathf.Clamp(targetPos.x, minBounds.x, maxBounds.x);
            targetPos.y = Mathf.Clamp(targetPos.y, minBounds.y, maxBounds.y);
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, smoothTime);
    }
}
