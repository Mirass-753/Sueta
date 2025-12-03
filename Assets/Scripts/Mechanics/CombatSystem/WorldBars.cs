using UnityEngine;

public class WorldBars : MonoBehaviour
{
    [Header("Refs")]
    public HealthSystem health;
    public EnergySystem energy;
    public Transform healthFill;
    public Transform energyFill;

    [Header("Layout")]
    public float barWidth = 1f;
    public float barHeight = 0.07f;
    public float horizontalOffset = 0f;
    public float verticalOffset = -0.4f;
    public float spacing = 0.08f;

    float _hpMax;
    float _enMax;
    Vector3 _healthBasePos;
    Vector3 _energyBasePos;

    void Awake()
    {
        if (health != null) _hpMax = health.maxHealth;
        if (energy != null) _enMax = energy.maxEnergy;

        if (healthFill != null)
        {
            // фиксируем базу и сбрасываем позицию/скейл
            _healthBasePos = Vector3.zero;
            healthFill.localPosition = _healthBasePos;
            healthFill.localScale = Vector3.one;
            SetupBar(healthFill, Color.red);
        }
        if (energyFill != null)
        {
            _energyBasePos = new Vector3(0f, -spacing, 0f);
            energyFill.localPosition = _energyBasePos;
            energyFill.localScale = Vector3.one;
            SetupBar(energyFill, Color.blue);
        }
    }

    void LateUpdate()
    {
        transform.localPosition = new Vector3(horizontalOffset, verticalOffset, 0f);

        if (healthFill != null && health != null && _hpMax > 0f)
            SetFill(healthFill, health.CurrentHealth / _hpMax, _healthBasePos);

        if (energyFill != null && energy != null && _enMax > 0f)
            SetFill(energyFill, energy.CurrentEnergy / _enMax, _energyBasePos);
    }

    void SetupBar(Transform t, Color color)
    {
        t.localScale = new Vector3(barWidth, barHeight, t.localScale.z);
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = color;
    }

    void SetFill(Transform t, float ratio, Vector3 basePos)
    {
        ratio = Mathf.Clamp01(ratio);
        var s = t.localScale;
        s.x = barWidth * ratio;
        t.localScale = s;
        t.localPosition = basePos; // фиксируем якорь, не двигаем по X
    }
}
