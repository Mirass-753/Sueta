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
        // На всякий случай — если забыли проставить в инспекторе
        if (health == null)
            health = GetComponentInParent<HealthSystem>();

        if (energy == null)
            energy = GetComponentInParent<EnergySystem>();

        if (health != null)
            _hpMax = health.maxHealth;

        if (energy != null)
            _enMax = energy.maxEnergy;

        if (healthFill != null)
        {
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
        // держим бары на фиксированном смещении от головы
        transform.localPosition = new Vector3(horizontalOffset, verticalOffset, 0f);

        // HP
        if (healthFill != null && health != null)
        {
            if (_hpMax <= 0f)
                _hpMax = Mathf.Max(health.maxHealth, 0.0001f);

            float ratio = Mathf.Clamp01(health.CurrentHealth / _hpMax);

            // ВРЕМЕННЫЙ лог, чтобы увидеть, что бары читают hp
            // (можешь выключить, когда убедишься, что всё ок).
            // Debug.Log($"[BARS] {name}: hp={health.CurrentHealth}/{_hpMax}, ratio={ratio}");

            SetFill(healthFill, ratio, _healthBasePos);
        }

        // Energy
        if (energyFill != null && energy != null)
        {
            if (_enMax <= 0f)
                _enMax = Mathf.Max(energy.maxEnergy, 0.0001f);

            float ratio = Mathf.Clamp01(energy.CurrentEnergy / _enMax);
            SetFill(energyFill, ratio, _energyBasePos);
        }
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
        t.localPosition = basePos;
    }
}
