using UnityEngine;
using UnityEngine.UI;

public class DamagePopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Text text;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Anim")]
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float floatSpeed = 40f;
    [SerializeField] private float startScale = 1.0f;
    [SerializeField] private float endScale = 1.15f;

    private float _t;
    private RectTransform _rt;

    private void Awake()
    {
        _rt = transform as RectTransform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (text == null) text = GetComponentInChildren<Text>(true);
    }

    public void Init(int amount, Color color)
    {
        if (text != null)
        {
            text.text = amount.ToString();
            text.color = color;
        }

        _t = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        _t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(_t / lifetime);

        if (_rt != null)
            _rt.anchoredPosition += Vector2.up * (floatSpeed * Time.unscaledDeltaTime);

        float s = Mathf.Lerp(startScale, endScale, k);
        transform.localScale = Vector3.one * s;

        if (canvasGroup != null)
        {
            float fade = 1f;
            if (k > 0.6f)
                fade = Mathf.InverseLerp(1f, 0.6f, k);
            canvasGroup.alpha = fade;
        }

        if (_t >= lifetime)
            Destroy(gameObject);
    }
}
