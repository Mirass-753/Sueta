using System;
using System.Collections.Generic;
using UnityEngine;

public enum BodyZone
{
    Face, Throat, Neck, Chest, Back, Paws, Tail
}

[Serializable]
public class BodyZoneCollider
{
    public BodyZone zone;
    public Collider2D collider;
    public float damageMultiplier = 1f;
}

public struct AttackData
{
    public float baseDamage;
    public Vector2 direction;
    public Transform attacker;
}

public class Damageable : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("Уникальный ID сущности в сети. Для игрока = его playerId.")]
    public string networkId;

    [Tooltip("Если true — урон по этому объекту должен идти через сервер.")]
    public bool isNetworkEntity = false;

    [Header("Refs")]
    public HealthSystem health;
    public EnergySystem energy;
    public CombatModeController combatMode;

    [Header("Zones")]
    public BodyZoneCollider[] zones;
    public float blockMultiplier = 0.3f;
    public float parryMultiplier = 0f;

    // ===== РЕЕСТР ПО ID =====

    private static readonly Dictionary<string, Damageable> _registry =
        new Dictionary<string, Damageable>();

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(networkId))
        {
            _registry[networkId] = this;
        }
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(networkId))
        {
            if (_registry.TryGetValue(networkId, out var d) && d == this)
                _registry.Remove(networkId);
        }
    }

    public static bool TryGetById(string id, out Damageable dmg)
    {
        return _registry.TryGetValue(id, out dmg);
    }

    /// <summary>
    /// Вызываем из PlayerController / RemotePlayer.Create, когда знаем айди.
    /// </summary>
    public void SetNetworkIdentity(string id, bool networkEntity)
    {
        // снять старую регистрацию
        if (!string.IsNullOrEmpty(networkId))
        {
            if (_registry.TryGetValue(networkId, out var d) && d == this)
                _registry.Remove(networkId);
        }

        networkId = id;
        isNetworkEntity = networkEntity;

        if (!string.IsNullOrEmpty(networkId))
        {
            _registry[networkId] = this;
        }
    }

    // ===== ЛОГИКА УРОНА =====

    BodyZoneCollider FindZone(Collider2D hit)
    {
        foreach (var z in zones)
            if (z.collider == hit) return z;
        return null;
    }

    public void ApplyHit(AttackData data, Collider2D hitCollider)
{
    // Лог для проверки, что мы сюда попали
    Debug.Log($"[DMG] ApplyHit on {gameObject.name}. " +
              $"isNetworkEntity={isNetworkEntity}, networkId='{networkId}'");

    // 1. Базовый урон + зона
    var zone = FindZone(hitCollider);
    float dmg = data.baseDamage * (zone != null ? zone.damageMultiplier : 1f);

    // 2. Блок / парри
    bool isBlocking = combatMode != null && combatMode.IsBlocking;
    bool isParry    = combatMode != null && combatMode.IsInParryWindow;

    if (isParry)
        dmg *= parryMultiplier;
    else if (isBlocking)
        dmg *= blockMultiplier;

    // 3. Вычитаем энергию (если есть)
    float remaining = energy != null ? energy.AbsorbDamage(dmg) : dmg;
    if (remaining <= 0f)
        return;

    // ===== СЕТЕВОЙ УРОН =====
    bool wsNull = WebSocketClient.Instance == null;
    Debug.Log($"[DMG] before net branch: isNetworkEntity={isNetworkEntity}, " +
              $"networkId='{networkId}', wsNull={wsNull}");

    if (isNetworkEntity &&
        !string.IsNullOrEmpty(networkId) &&
        WebSocketClient.Instance != null)
    {
        // кто ударил
        string sourceId = null;
        if (data.attacker != null)
        {
            var src = data.attacker.GetComponentInParent<Damageable>();
            if (src != null && !string.IsNullOrEmpty(src.networkId))
                sourceId = src.networkId;
        }

        var req = new NetMessageDamageRequest
        {
            type     = "damage_request",
            sourceId = sourceId,
            targetId = networkId,
            amount   = remaining
        };

        string json = JsonUtility.ToJson(req);
        Debug.Log("[NET] Send damage_request: " + json);
        WebSocketClient.Instance.Send(json);

        // Локально HP не трогаем – ждём ответ сервера
        return;
    }

    // ===== ЛОКАЛЬНЫЙ УРОН (NPC/оффлайн) =====
    if (health != null)
    {
        float prevHp = health.CurrentHealth;
        health.TakeDamage(remaining);

        if (Mathf.Abs(health.CurrentHealth - prevHp) > 0.01f)
        {
            Debug.Log($"[DMG] Local damage applied. " +
                      $"New HP = {health.CurrentHealth} (was {prevHp}), dmg={remaining}");
        }
    }
    else
    {
        Debug.LogWarning($"[DMG] ApplyHit on {gameObject.name}, " +
                         $"but health == null, remaining={remaining}");
    }
}

}
