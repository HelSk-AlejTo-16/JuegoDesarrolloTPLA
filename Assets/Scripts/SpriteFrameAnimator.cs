using UnityEngine;

/// <summary>
/// Componente ligero para animar secuencias de sprites por cuadros (para antorchas, velas y enemigos en el mapa).
/// </summary>
public class SpriteFrameAnimator : MonoBehaviour
{
    [Tooltip("Lista secuencial de sprites que componen la animación.")]
    public Sprite[] frames;

    [Tooltip("Cuadros por segundo de la animación.")]
    [Range(1f, 30f)]
    public float framesPerSecond = 8f;

    [Tooltip("Si la animación se reproduce en bucle continuo.")]
    public bool loop = true;

    [Tooltip("Desfase aleatorio inicial para que las antorchas/velas no parpadeen exactamente al mismo milisegundo.")]
    public bool randomStartOffset = true;

    private SpriteRenderer spriteRenderer;
    private float timer = 0f;
    private int currentFrame = 0;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (randomStartOffset && frames != null && frames.Length > 0)
        {
            currentFrame = Random.Range(0, frames.Length);
            timer = Random.Range(0f, 1f / Mathf.Max(1f, framesPerSecond));
        }
    }

    private void Start()
    {
        UpdateSprite();
    }

    private void Update()
    {
        if (frames == null || frames.Length <= 1 || spriteRenderer == null) return;

        timer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);

        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = frames.Length - 1;
                    enabled = false;
                }
            }

            UpdateSprite();
        }
    }

    public void SetFrames(Sprite[] newFrames, float fps = 8f)
    {
        frames = newFrames;
        framesPerSecond = fps;
        currentFrame = 0;
        timer = 0f;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (spriteRenderer != null && frames != null && currentFrame >= 0 && currentFrame < frames.Length)
        {
            if (frames[currentFrame] != null)
            {
                spriteRenderer.sprite = frames[currentFrame];
            }
        }
    }
}
