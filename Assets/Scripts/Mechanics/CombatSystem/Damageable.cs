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
    public Damageable attackerDamageable;
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
    bool healthIsNull = (health == null);
    bool energyIsNull = (energy == null);

    Debug.Log(
        $"[DMG] ApplyHit on {name}: " +
        $"isNetworkEntity={isNetworkEntity}, id='{networkId}', " +
        $"healthNull={healthIsNull}, energyNull={energyIsNull}");

    // Если это НЕ сетевой объект и у него нет health — просто игнорируем хит
    if (healthIsNull && !isNetworkEntity)
    {
        Debug.LogWarning(
            $"[DMG] {name}: health is null and not network entity, hit ignored");
        return;
    }

    // ---------- 1. Базовый урон + зона ----------
    float dmg = data.baseDamage;
    var zone = FindZone(hitCollider);
    if (zone != null)
    {
        dmg *= zone.damageMultiplier;
    }

    // ---------- 2. Блок / парри ----------
    bool isBlocking = combatMode != null && combatMode.IsBlocking;
    bool isParry    = combatMode != null && combatMode.IsInParryWindow;

    if (isParry)
        dmg *= parryMultiplier;
    else if (isBlocking)
        dmg *= blockMultiplier;

    // ---------- 3. Щит / энергия ----------
    float remaining = dmg;
    if (!energyIsNull)
    {
        remaining = energy.AbsorbDamage(dmg);
    }

    Debug.Log(
        $"[DMG-CHECK] after energy: base={data.baseDamage}, " +
        $"final={remaining}, isNetworkEntity={isNetworkEntity}, " +
        $"networkId='{networkId}'");

    // Для НЕсетевых целей: если щит всё съел — дальше нечего делать
    if (remaining <= 0f && !isNetworkEntity)
    {
        return;
    }

    // ---------- 4. СЕТЕВАЯ ВЕТКА (PvP / сетевые сущности) ----------
    if (isNetworkEntity && !string.IsNullOrEmpty(networkId))
    {
        // Даже если remaining <= 0, всё равно отправим небольшой тик урона,
        // чтобы сервер гарантированно получил запрос.
        float amountForServer = remaining > 0f
            ? remaining
            : Mathf.Max(0.01f, dmg);

        var ws = WebSocketClient.Instance;
        if (ws == null)
        {
            Debug.LogWarning(
                $"[NET] damage_request SKIPPED ({name}): " +
                $"WebSocketClient.Instance is null, applying local damage amount={amountForServer}");

            if (!healthIsNull)
                health.TakeDamage(amountForServer);

            return;
        }

        // Пытаемся взять sourceId из атакующего Damageable
        string sourceId = null;
        if (data.attackerDamageable != null &&
            data.attackerDamageable.isNetworkEntity &&
            !string.IsNullOrEmpty(data.attackerDamageable.networkId))
        {
            sourceId = data.attackerDamageable.networkId;
        }

        var req = new NetMessageDamageRequest
        {
            type     = "damage_request",
            sourceId = sourceId,
            targetId = networkId,
            amount   = amountForServer,
            zone     = zone != null ? zone.zone.ToString() : ""
        };

        string json = JsonUtility.ToJson(req);
        Debug.Log($"[DMG-NET] SEND damage_request: {json}");
        ws.Send(json);

        // Важно: локально HP не меняем — ждём пакет "damage" от сервера
        return;
    }

    // ---------- 5. Оффлайн / несетевые цели ----------
    if (!healthIsNull && remaining > 0f)
    {
        health.TakeDamage(remaining);
    }
}



}
