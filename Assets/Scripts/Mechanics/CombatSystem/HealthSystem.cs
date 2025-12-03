using UnityEngine;
using System;

public class HealthSystem : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("Максимальное здоровье (в редакторе).")]
    public float maxHp = 100f;

    [Tooltip("Текущее здоровье.")]
    public float currentHp = 100f;

    /// <summary>
    /// Жив ли объект.
    /// </summary>
    public bool IsDead => currentHp <= 0f;

    // ==== СТАРЫЙ API, КОТОРЫЙ ТРЕБУЮТ ДРУГИЕ СКРИПТЫ ====

    /// <summary>
    /// Совместимость: старое имя maxHealth.
    /// </summary>
    public float maxHealth
    {
        get => maxHp;
        set => maxHp = value;
    }

    /// <summary>
    /// Совместимость: старое свойство текущего здоровья.
    /// </summary>
    public float CurrentHealth => currentHp;

    /// <summary>
    /// Событие смерти, на которое подписываются EnemyDeath и др.
    /// </summary>
    public event Action OnDeath;

    private bool _deathInvoked = false;

    private void Awake()
    {
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
        UpdateDeathFlag();
    }

    // ================== ПУБЛИЧНЫЕ МЕТОДЫ ==================

    /// <summary>
    /// Локальное нанесение урона (когда НЕ используем серверный урон).
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        currentHp = Mathf.Max(0f, currentHp - amount);
        UpdateDeathFlag();
    }

    /// <summary>
    /// Обновление HP по данным с сервера.
    /// Вызывается из NetworkMessageHandler.HandleDamage.
    /// </summary>
    public void SetCurrentHpFromServer(float hp)
    {
        float clamped = Mathf.Clamp(hp, 0f, maxHp);
        if (Mathf.Approximately(currentHp, clamped))
            return;

        currentHp = clamped;
        UpdateDeathFlag();

        // если хочешь — можешь тут проверять oldHp > currentHp
        // и запускать эффект попадания / смерть и т.п.
    }


    /// <summary>
    /// Лечение (локальное).
    /// </summary>
    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        UpdateDeathFlag();
    }

    // ================== ВНУТРЕННЯЯ ЛОГИКА ==================

    private void UpdateDeathFlag()
    {
        // Если только что умерли — вызываем OnDeath один раз
        if (!_deathInvoked && currentHp <= 0f)
        {
            _deathInvoked = true;
            OnDeath?.Invoke();
        }
        // Если снова ожили/были похилены — даём событию шанс сработать ещё раз
        else if (currentHp > 0f)
        {
            _deathInvoked = false;
        }
    }


}
