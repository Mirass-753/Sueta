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
        $"[DMG] ApplyHit on {name}: isNetworkEntity={isNetworkEntity}, " +
        $"id='{networkId}', healthNull={healthIsNull}, energyNull={energyIsNull}");

    // Если это НЕ сетевой объект и у него нет health — игнорируем
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
        dmg *= zone.damageMultiplier;

    // ---------- 2. Блок / парри ----------
    bool isBlocking = combatMode != null && combatMode.IsBlocking;
    bool isParry   = combatMode != null && combatMode.IsInParryWindow;

    if (isParry)
        dmg *= parryMultiplier;
    else if (isBlocking)
        dmg *= blockMultiplier;

    if (dmg <= 0f)
        return;

    // ---------- 3. Щит / энергия ----------
    float remaining   = dmg;   // то, что пойдёт в HP
    float energySpent = 0f;    // сколько списали с энергии

    float energyBefore  = 0f;
    float energyThresh  = 0f;
    bool  hasEnergy     = !energyIsNull;

    if (!energyIsNull)
    {
        energyBefore = energy.CurrentEnergy;
        energyThresh = energy.maxEnergy * 0.5f;   // 50%

        // считаем, сколько энергии поглотит удар
        remaining   = energy.AbsorbDamage(dmg);   // вернёт "остаток" урона
        energySpent = Mathf.Max(0f, dmg - remaining);

        // ГЕЙТ НА 50%:
        // если ДО удара энергии было больше половины,
        // здоровье в ЭТОМ ударе вообще не трогаем,
        // даже если удар снес энергию ниже порога.
        if (energyBefore > energyThresh)
        {
            remaining = 0f;
        }
    }

    Debug.Log(
        $"[DMG-CHECK] after energy: base={dmg}, final={remaining}, " +
        $"isNetworkEntity={isNetworkEntity}, networkId='{networkId}'");

    // ---------- 4. СЕТЕВАЯ ВЕТКА ----------
    if (isNetworkEntity && !string.IsNullOrEmpty(networkId))
    {
        var ws = WebSocketClient.Instance;
        if (ws == null)
        {
            Debug.LogWarning(
                $"[NET] damage_request SKIPPED ({name}): " +
                $"WebSocketClient.Instance is null, applying local damage={remaining}");

            if (!healthIsNull && remaining > 0f)
                health.TakeDamage(remaining);

            return;
        }

        // Сначала отправляем списание энергии, если что-то потратили
        if (hasEnergy && energySpent > 0f)
        {
            var eReq = new NetMessageEnergyRequest
            {
                type     = "energy_request",
                targetId = networkId,
                amount   = energySpent
            };

            string eJson = JsonUtility.ToJson(eReq);
            Debug.Log($"[DMG-NET] SEND energy_request: {eJson}");
            ws.Send(eJson);
        }

        // Потом — урон по HP, НО ТОЛЬКО ЕСЛИ ЧТО-ТО ОСТАЛОСЬ
        if (remaining > 0f)
        {
            string sourceId = null;
            if (data.attackerDamageable != null &&
                data.attackerDamageable.isNetworkEntity &&
                !string.IsNullOrEmpty(data.attackerDamageable.networkId))
            {
                sourceId = data.attackerDamageable.networkId;
            }

            var dReq = new NetMessageDamageRequest
            {
                type     = "damage_request",
                sourceId = sourceId,
                targetId = networkId,
                amount   = remaining,
                zone     = zone != null ? zone.zone.ToString() : ""
            };

            string dJson = JsonUtility.ToJson(dReq);
            Debug.Log($"[DMG-NET] SEND damage_request: {dJson}");
            ws.Send(dJson);
        }

        // Локально HP не меняем — ждём события "damage" от сервера.
        return;
    }

    // ---------- 5. Оффлайн / несетевые цели ----------
    if (!healthIsNull && remaining > 0f)
        health.TakeDamage(remaining);
}




}
