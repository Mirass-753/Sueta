using UnityEngine;

public class ClawHitFx : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Scale")]
    [SerializeField] private float startScale = 0.5f;
    [SerializeField] private float peakScale = 1.5f;
    [SerializeField] private float settleScale = 1.0f;

    [Header("Timing (seconds)")]
    [SerializeField] private float growTime = 0.06f;    // 0.5 -> 1.5
    [SerializeField] private float settleTime = 0.08f;  // 1.5 -> 1.0
    [SerializeField] private float holdTime = 0.10f;    // держим 1.0
    [SerializeField] private float fadeTime = 0.12f;    // исчезаем

    [Header("Options")]
    [SerializeField] private bool useUnscaledTime = true; // чтобы не зависело от хит-стопов
    [SerializeField] private bool randomFlipX = true;
    [SerializeField] private bool randomRotation = true;
    [SerializeField] private float rotationRange = 20f;

    private float _t;
    private Color _baseColor;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        _baseColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
    }

    public void Init()
    {
        _t = 0f;

        if (randomFlipX && spriteRenderer != null)
            spriteRenderer.flipX = Random.value > 0.5f;

        if (randomRotation)
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-rotationRange, rotationRange));

        transform.localScale = Vector3.one * startScale;

        if (spriteRenderer != null)
            spriteRenderer.color = _baseColor;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _t += dt;

        float a = growTime;
        float b = a + settleTime;
        float c = b + holdTime;
        float d = c + fadeTime;

        if (_t <= a)
        {
            float k = Mathf.Clamp01(_t / Mathf.Max(0.0001f, growTime));
            float s = Mathf.Lerp(startScale, peakScale, EaseOutBack(k));
            transform.localScale = Vector3.one * s;
            return;
        }

        if (_t <= b)
        {
            float k = Mathf.Clamp01((_t - a) / Mathf.Max(0.0001f, settleTime));
            float s = Mathf.Lerp(peakScale, settleScale, EaseOutCubic(k));
            transform.localScale = Vector3.one * s;
            return;
        }

        if (_t <= c)
        {
            transform.localScale = Vector3.one * settleScale;
            return;
        }

        if (_t <= d)
        {
            transform.localScale = Vector3.one * settleScale;

            if (spriteRenderer != null)
            {
                float k = Mathf.Clamp01((_t - c) / Mathf.Max(0.0001f, fadeTime));
                var col = _baseColor;
                col.a = Mathf.Lerp(_baseColor.a, 0f, k);
                spriteRenderer.color = col;
            }
            return;
        }

        Destroy(gameObject);
    }

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
