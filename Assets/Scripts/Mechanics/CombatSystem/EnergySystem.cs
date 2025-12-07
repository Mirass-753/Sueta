using UnityEngine;

public class EnergySystem : MonoBehaviour
{
    [Header("Energy Settings")]
    public float maxEnergy = 100f;

    [Tooltip("Реген в секунду, пока персонаж в бою")]
    public float regenInCombatPerSec = 5f;

    [Tooltip("Реген в секунду, вне боя")]
    public float regenOutCombatPerSec = 10f;

    [Tooltip("Самоподжог при атаке (процент от maxEnergy)")]
    public float selfDamagePercent = 0.01f;

    [Header("Debug")]
    [SerializeField] private float _currentEnergy;
    [SerializeField] private bool _serverAuthoritative;

    private bool _inCombat;
    private Damageable _damageable;

    public float CurrentEnergy => _currentEnergy;
    public bool CanRun => _currentEnergy > 0f;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();

        // Если этот Damageable помечен как сетевой — по умолчанию считаем,
        // что энергией управляет сервер.
        _serverAuthoritative = _damageable != null && _damageable.isNetworkEntity;

        // Стартовое значение
        float initial = _currentEnergy > 0f ? _currentEnergy : maxEnergy;
        _currentEnergy = Mathf.Clamp(initial, 0f, maxEnergy);

        ApplyCachedEnergyIfAny();
    }

    private void OnEnable()
    {
        if (_damageable == null)
            _damageable = GetComponent<Damageable>();

        if (_damageable != null && _damageable.isNetworkEntity)
            _serverAuthoritative = true;

        ApplyCachedEnergyIfAny();
    }

    private void Update()
    {
        // Для сетевых сущностей энергией рулит сервер — клиент только показывает.
        if (_serverAuthoritative)
            return;

        float regen = _inCombat ? regenInCombatPerSec : regenOutCombatPerSec;
        if (regen <= 0f || maxEnergy <= 0f)
            return;

        _currentEnergy = Mathf.Min(maxEnergy, _currentEnergy + regen * Time.deltaTime);
    }

    // Вызывается из CombatModeController
    public void SetCombat(bool active)
    {
        _inCombat = active;
    }

    public void StartRunning() { }
    public void StopRunning() { }

    /// <summary>
    /// Потратить энергию на amount. Возвращает, сколько реально потратили.
    /// Для сервер-авторитетных сущностей только считает, но локально не меняет.
    /// </summary>
    public float Spend(float amount)
    {
        if (amount <= 0f || maxEnergy <= 0f)
            return 0f;

        float spent = Mathf.Min(amount, _currentEnergy);

        if (!_serverAuthoritative)
        {
            _currentEnergy -= spent;
        }

        return spent;
    }

    /// <summary>
    /// Старый метод: поглотить частью энергии урон и вернуть остаток в HP.
    /// Оставляем для совместимости, но в нашей новой логике в Damageable
    /// мы используем Spend().
    /// </summary>
    public float AbsorbDamage(float amount)
    {
        if (amount <= 0f)
            return 0f;

        float spent = Mathf.Min(amount, _currentEnergy);

        if (_serverAuthoritative)
        {
            Debug.Log($"[ENERGY] {name}: server authoritative AbsorbDamage simulate spent={spent}, current={_currentEnergy}");
            return amount - spent;
        }

        _currentEnergy -= spent;
        return amount - spent;
    }

    public void SelfDamage()
    {
        if (_serverAuthoritative)
            return;

        if (maxEnergy <= 0f || selfDamagePercent <= 0f)
            return;

        float dmg = maxEnergy * selfDamagePercent;
        _currentEnergy = Mathf.Max(0f, _currentEnergy - dmg);
    }

    /// <summary>
    /// Сервер прислал новое значение энергии.
    /// </summary>
    public void SetCurrentEnergyFromServer(float energy)
    {
        float clamped = Mathf.Clamp(energy, 0f, maxEnergy);
        _serverAuthoritative = true;

        if (Mathf.Approximately(_currentEnergy, clamped))
        {
            Debug.Log($"[ENERGY] {name}: server energy {energy} (clamped {clamped}) – no change, current={_currentEnergy}");
            return;
        }

        Debug.Log($"[ENERGY] {name}: server energy {_currentEnergy} -> {clamped} (max={maxEnergy})");
        _currentEnergy = clamped;
    }

    public void SetServerAuthority(bool active)
    {
        _serverAuthoritative = active;
    }

    /// <summary>
    /// При появлении объекта пробуем подтягивать энергию из кеша NetworkMessageHandler.
    /// </summary>
    private void ApplyCachedEnergyIfAny()
    {
        if (_damageable == null || string.IsNullOrEmpty(_damageable.networkId))
            return;

        if (NetworkMessageHandler.TryGetCachedEnergy(_damageable.networkId, out var energy, out var max))
        {
            if (max > 0f)
                maxEnergy = max;

            SetCurrentEnergyFromServer(energy);
        }
    }
}
