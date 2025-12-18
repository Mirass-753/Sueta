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

    // Локальный (несетевой) объект без health — игнорируем
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
    bool isParry    = combatMode != null && combatMode.IsInParryWindow;

    if (isParry)
        dmg *= parryMultiplier;
    else if (isBlocking)
        dmg *= blockMultiplier;

    // ---------- 3. Щит / энергия ----------
    float remaining     = dmg; // то, что пойдёт в HP
    float energySpent   = 0f;  // то, что съела энергия
    float energyBefore  = 0f;
    float fracBefore    = 0f;

    if (!energyIsNull && dmg > 0f && energy.maxEnergy > 0f)
    {
        energyBefore = energy.CurrentEnergy;
        fracBefore   = energyBefore / energy.maxEnergy;

        // Пока энергии >= 50% — она работает как полноценный щит:
        // весь урон уходит в энергию, HP не трогаем.
        if (fracBefore >= 0.5f)
        {
            energySpent = energy.Spend(dmg);
            remaining   = 0f;
        }
        else
        {
            // Ниже порога 50% — каждый удар бьёт и по энергии, и по HP.
            // Коэффициент можно подкрутить.
            const float hpShare = 0.5f; // 50% урона гарантированно по HP

            float dmgToEnergy = dmg * (1f - hpShare);

            // тратим энергию только на её часть урона
            energySpent = energy.Spend(dmgToEnergy);

            // всё остальное идёт в HP (включая ту часть, которую
            // энергия не смогла покрыть)
            remaining = dmg - energySpent;
        }
    }

    Debug.Log(
        $"[DMG-CHECK] after energy: base={dmg}, remaining={remaining}, " +
        $"energySpent={energySpent}, energyBefore={energyBefore}, fracBefore={fracBefore}, " +
        $"isNetworkEntity={isNetworkEntity}, networkId='{networkId}'");

    // Если урона по HP не осталось и это НЕ сетевой объект — всё, выходим
    if (remaining <= 0f && !isNetworkEntity)
        return;

    // ---------- 4. СЕТЕВАЯ ВЕТКА (игроки / сетевые сущности) ----------
    if (isNetworkEntity && !string.IsNullOrEmpty(networkId))
    {
        var ws = WebSocketClient.Instance;
        if (ws == null)
        {
            Debug.LogWarning(
                $"[NET] damage_request SKIPPED ({name}): " +
                $"WebSocketClient.Instance is null, applying local damage remaining={remaining}");

            if (!healthIsNull && remaining > 0f)
                health.TakeDamage(remaining);

            return;
        }

        // 4.1. Сначала отправляем, сколько энергии потратили
        if (!energyIsNull && energySpent > 0f)
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

        // 4.2. Потом — урон по HP (ТОЛЬКО если что-то осталось)
        if (remaining > 0f)
        {
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
                amount   = remaining,
                zone     = zone != null ? zone.zone.ToString() : "",
                x        = transform.position.x,
                y        = transform.position.y,
                z        = transform.position.z
            };

            string json = JsonUtility.ToJson(req);
            Debug.Log($"[DMG-NET] SEND damage_request: {json}");
            ws.Send(json);
        }

        // ЛОКАЛЬНО HP/энергию не меняем — ждём damage/energy_update от сервера.
        return;
    }

    // ---------- 5. Оффлайн / несетевые цели ----------
    if (!healthIsNull && remaining > 0f)
        health.TakeDamage(remaining);
}


}
