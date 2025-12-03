using UnityEngine;

public class EnergySystem : MonoBehaviour
{
    public float maxEnergy = 100f;
    public float regenInCombatPerSec = 5f;
    public float regenOutCombatPerSec = 10f;
    public float selfDamagePercent = 0.01f;

    private bool _inCombat;
    private float _currentEnergy;
    public float CurrentEnergy => _currentEnergy;
    public bool CanRun => _currentEnergy > 0f;

    void Awake()
    {
        _currentEnergy = maxEnergy;
    }

    void Update()
    {
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
        float absorbed = Mathf.Min(amount, _currentEnergy);
        _currentEnergy -= absorbed;
        return amount - absorbed;
    }

    public void SelfDamage()
    {
        float dmg = maxEnergy * selfDamagePercent;
        _currentEnergy = Mathf.Max(0f, _currentEnergy - dmg);
    }
}
