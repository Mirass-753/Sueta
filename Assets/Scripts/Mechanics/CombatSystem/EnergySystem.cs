using UnityEngine;
using Sirenix.OdinInspector; // можно для красоты инспектора

public class EnergySystem : MonoBehaviour
{
    [TitleGroup("Energy/Config")]
    public float maxEnergy = 100f;

    [TitleGroup("Energy/Config")]
    public float regenInCombatPerSec = 5f;

    [TitleGroup("Energy/Config")]
    public float regenOutCombatPerSec = 10f;

    [TitleGroup("Energy/Config")]
    [LabelText("Self Damage %")]
    public float selfDamagePercent = 0.01f;

    private bool _inCombat;
    private float _currentEnergy;
    private bool _serverAuthoritative;

    private Damageable _damageable;

    [TitleGroup("Energy/Runtime"), ShowInInspector, ReadOnly]
    public float CurrentEnergy => _currentEnergy;

    public bool CanRun => _currentEnergy > 0f;

    void Awake()
    {
        _currentEnergy = maxEnergy;
        _damageable = GetComponent<Damageable>();
        _serverAuthoritative = _damageable != null && _damageable.isNetworkEntity;

        float initial = _currentEnergy > 0f ? _currentEnergy : maxEnergy;
        _currentEnergy = Mathf.Clamp(initial, 0f, maxEnergy);

        ApplyCachedEnergyIfAny();
    }

    void Update()
    {
        if (_serverAuthoritative)
            return;

        float regen = _inCombat ? regenInCombatPerSec : regenOutCombatPerSec;
        _currentEnergy = Mathf.Min(maxEnergy, _currentEnergy + regen * Time.deltaTime);
    }

    public void SetCombat(bool active)
    {
        _inCombat = active;
    }

    public void StartRunning() { }
    public void StopRunning() { }

    public float AbsorbDamage(float amount)
{
    if (amount <= 0f)
        return 0f;

    // сколько энергии можем потратить
    float absorbed = Mathf.Min(amount, _currentEnergy);

    // локально списываем энергию всегда
    _currentEnergy -= absorbed;

    // возвращаем урон, который ПРОШЁЛ сквозь щит
    return amount - absorbed;
}


   public void SelfDamage()
{
    float dmg = maxEnergy * selfDamagePercent;
    if (dmg <= 0f)
        return;

    // локально тратим энергию
    _currentEnergy = Mathf.Max(0f, _currentEnergy - dmg);

    // если это сетевой объект – шлём на сервер
    if (_damageable != null &&
        _damageable.isNetworkEntity &&
        !string.IsNullOrEmpty(_damageable.networkId) &&
        WebSocketClient.Instance != null)
    {
        var req = new NetMessageEnergyRequest
        {
            type     = "energy_request",
            targetId = _damageable.networkId,
            amount   = dmg
        };

        WebSocketClient.Instance.Send(JsonUtility.ToJson(req));
    }
}


    public void SetCurrentEnergyFromServer(float energy)
    {
        float clamped = Mathf.Clamp(energy, 0f, maxEnergy);
        _serverAuthoritative = true;

        if (Mathf.Approximately(_currentEnergy, clamped))
            return;

        Debug.Log($"[ENERGY] {name}: server energy {_currentEnergy} -> {clamped}");
        _currentEnergy = clamped;
    }

    public void SetServerAuthority(bool active)
    {
        _serverAuthoritative = active;
    }

    private void ApplyCachedEnergyIfAny()
    {
        if (_damageable == null || string.IsNullOrEmpty(_damageable.networkId))
            return;

        if (NetworkMessageHandler.TryGetCachedEnergy(
                _damageable.networkId, out var energy, out var max))
        {
            if (max > 0f)
                maxEnergy = max;

            SetCurrentEnergyFromServer(energy);
        }
    }
}
