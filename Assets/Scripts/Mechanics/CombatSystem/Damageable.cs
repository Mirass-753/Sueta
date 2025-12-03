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

/// <summary>
/// Component that routes incoming hits either to the authoritative server (players)
/// or applies them locally (NPC/offline). Uses <see cref="networkId"/> to map
/// damage events to game objects.
/// </summary>
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

    private void Awake()
    {
        if (health == null)
            health = GetComponent<HealthSystem>();
        if (energy == null)
            energy = GetComponent<EnergySystem>();
        if (combatMode == null)
            combatMode = GetComponent<CombatModeController>();

        if (health == null)
            Debug.LogWarning($"[DMG] {name} has no HealthSystem — damage won't apply");
    }

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

    private BodyZoneCollider FindZone(Collider2D hit)
    {
        foreach (var z in zones)
            if (z.collider == hit) return z;
        return null;
    }

    public void ApplyHit(AttackData data, Collider2D hitCollider)
    {
        if (health == null)
            return;

        // 1. Зона попадания
        var zone = FindZone(hitCollider);
        float dmg = data.baseDamage * (zone != null ? zone.damageMultiplier : 1f);

        // 2. Блок / парри
        bool isBlocking = combatMode != null && combatMode.IsBlocking;
        bool isParry    = combatMode != null && combatMode.IsInParryWindow;

        if (isParry)
            dmg *= parryMultiplier;
        else if (isBlocking)
            dmg *= blockMultiplier;

        if (dmg <= 0f)
            return;

        // 3. СЕТЕВАЯ ЦЕЛЬ → отправляем запрос на сервер
        if (isNetworkEntity &&
            !string.IsNullOrEmpty(networkId) &&
            WebSocketClient.Instance != null)
        {
            // пытаемся найти атакующего и его id
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
                sourceId = sourceId ?? "",
                targetId = networkId,
                amount   = dmg,
                zone     = zone != null ? zone.zone.ToString() : ""
            };

            string json = JsonUtility.ToJson(req);
            Debug.Log("[NET] Send damage_request: " + json);
            WebSocketClient.Instance.Send(json);

            // HP локально НЕ трогаем — ждём пакет damage от сервера
            return;
        }

        // 4. ЛОКАЛЬНАЯ цель (NPC, оффлайн, нет сети)
        float remaining = energy != null ? energy.AbsorbDamage(dmg) : dmg;
        if (remaining > 0f)
            health.TakeDamage(remaining);
    }
}
