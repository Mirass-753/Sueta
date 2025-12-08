using System.Collections.Generic;
using UnityEngine;

public static class NetworkMessageHandler
{
    // ----- Удалённые игроки -----
    private static readonly Dictionary<string, RemotePlayer> players =
        new Dictionary<string, RemotePlayer>();

    // ----- Кеш HP -----
    private static readonly Dictionary<string, float> hpCache =
        new Dictionary<string, float>();

    // ----- Кеш энергии -----
    private struct EnergyCacheEntry
    {
        public float energy;
        public float maxEnergy;
    }

    private static readonly Dictionary<string, EnergyCacheEntry> energyCache =
        new Dictionary<string, EnergyCacheEntry>();

    // ================== ВХОДНАЯ ТОЧКА ==================

    public static void Handle(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        NetMessageBase baseMsg;
        try
        {
            baseMsg = JsonUtility.FromJson<NetMessageBase>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить базовое сообщение: {json}");
            return;
        }

        if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
        {
            Debug.LogWarning($"[NET] Сообщение без type: {json}");
            return;
        }

        switch (baseMsg.type)
        {
            case "move":          HandleMove(json);         break;
            case "damage":        HandleDamage(json);       break;
            case "hp_sync":       HandleHpSync(json);       break;
            case "energy_update": HandleEnergyUpdate(json); break;
            case "energy_sync":   HandleEnergySync(json);   break;
            case "disconnect":    HandleDisconnect(json);   break;
            case "item_drop":     HandleItemDrop(json);     break;
            case "item_pickup":   HandleItemPickup(json);   break;
            case "prey_spawn":    HandlePreySpawn(json);    break;
            case "prey_pos":      HandlePreyPosition(json); break;
            case "prey_kill":     HandlePreyKill(json);     break;
            default:
                // неизвестные типы просто игнорируем
                break;
        }
    }

    // ================== ДВИЖЕНИЕ ==================

    private static void HandleMove(string json)
    {
        NetMessageMove move;
        try
        {
            move = JsonUtility.FromJson<NetMessageMove>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить move: {json}");
            return;
        }

        if (move == null || string.IsNullOrEmpty(move.id))
            return;

        // свои же сообщения игнорируем
        if (move.id == PlayerController.LocalPlayerId)
            return;
        
        Vector2 pos = new Vector2(move.x, move.y);
        Vector2 dir = new Vector2(move.dirX, move.dirY);
        float aimAngle = move.aimAngle; // может быть 0
        bool inCombat = move.inCombat; // находится ли удаленный игрок в боевом режиме
        
        // Отладочное логирование
        if (Application.isPlaying && inCombat)
        {
            Debug.Log($"[NET] Received move from {move.id}: inCombat={inCombat}, aimAngle={aimAngle:F1}°");
        }
        
        if (!players.TryGetValue(move.id, out var rp) || rp == null)
        {
            rp = RemotePlayer.Create(move.id);
            players[move.id] = rp;

            // подтягиваем кеши, если уже приходили hp/energy до спавна
            ApplyCachedHp(move.id, rp);
            ApplyCachedEnergy(move.id, rp);
        }

        rp.SetNetworkState(pos, dir, move.moving, move.aimAngle, move.inCombat);
    }

    // ================== УРОН (HP) ==================

    public static void HandleDamage(string json)
    {
        Debug.Log("[NET] RAW: " + json);

        NetMessageDamageEvent msg;
        try
        {
            msg = JsonUtility.FromJson<NetMessageDamageEvent>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[NET] Failed to parse damage msg: " + e + " | json=" + json);
            return;
        }

        if (msg == null)
        {
            Debug.LogWarning("[NET] Damage message is null | json=" + json);
            return;
        }

        if (string.IsNullOrEmpty(msg.targetId))
        {
            Debug.LogWarning("[NET] Damage msg without targetId: " + json);
            return;
        }

        Debug.Log($"[NET] DAMAGE: source={msg.sourceId}, target={msg.targetId}, amount={msg.amount}, hp={msg.hp}");

        // кладём HP в кеш
        hpCache[msg.targetId] = msg.hp;

        if (!Damageable.TryGetById(msg.targetId, out var target) || target == null)
        {
            Debug.LogWarning($"[NET] Damage target '{msg.targetId}' not found in Damageable registry");
            return;
        }

        if (target.health == null)
        {
            Debug.LogWarning($"[NET] Damage target '{target.name}' has no HealthSystem");
            return;
        }

        target.health.SetCurrentHpFromServer(msg.hp);
    }

    private static void HandleHpSync(string json)
    {
        NetMessageHpSync sync;
        try
        {
            sync = JsonUtility.FromJson<NetMessageHpSync>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить hp_sync: {json}");
            return;
        }

        if (sync?.entities == null)
            return;

        foreach (var e in sync.entities)
        {
            if (e == null || string.IsNullOrEmpty(e.id))
                continue;

            hpCache[e.id] = e.hp;

            if (Damageable.TryGetById(e.id, out var dmg) && dmg?.health != null)
            {
                dmg.health.SetCurrentHpFromServer(e.hp);
            }
        }
    }

    // ================== ЭНЕРГИЯ (ЩИТ) ==================

    private static void HandleEnergyUpdate(string json)
    {
        NetMessageEnergyUpdate msg;
        try
        {
            msg = JsonUtility.FromJson<NetMessageEnergyUpdate>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить energy_update: {json}");
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.targetId))
        {
            Debug.LogWarning($"[NET] energy_update без targetId: {json}");
            return;
        }

        // кешируем значение
        energyCache[msg.targetId] = new EnergyCacheEntry
        {
            energy = msg.energy,
            maxEnergy = msg.maxEnergy
        };

        if (!Damageable.TryGetById(msg.targetId, out var target) || target == null)
        {
            Debug.LogWarning($"[NET] energy_update: цель '{msg.targetId}' не найдена");
            return;
        }

        if (target.energy == null)
        {
            Debug.LogWarning($"[NET] energy_update: у цели '{target.name}' нет EnergySystem");
            return;
        }

        if (msg.maxEnergy > 0f)
            target.energy.maxEnergy = msg.maxEnergy;

        target.energy.SetCurrentEnergyFromServer(msg.energy);
    }

    private static void HandleEnergySync(string json)
    {
        NetMessageEnergySync sync;
        try
        {
            sync = JsonUtility.FromJson<NetMessageEnergySync>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить energy_sync: {json}");
            return;
        }

        if (sync?.entities == null)
            return;

        foreach (var e in sync.entities)
        {
            if (e == null || string.IsNullOrEmpty(e.id))
                continue;

            energyCache[e.id] = new EnergyCacheEntry
            {
                energy = e.energy,
                maxEnergy = e.maxEnergy
            };

            if (Damageable.TryGetById(e.id, out var dmg) && dmg?.energy != null)
            {
                if (e.maxEnergy > 0f)
                    dmg.energy.maxEnergy = e.maxEnergy;

                dmg.energy.SetCurrentEnergyFromServer(e.energy);
            }
        }
    }

    // ================== ПРЕДМЕТЫ ==================

    private static void HandleItemDrop(string json)
    {
        NetMessageItemDrop msg;
        try
        {
            msg = JsonUtility.FromJson<NetMessageItemDrop>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить item_drop: {json}");
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.pickupId) || string.IsNullOrEmpty(msg.itemName))
            return;

        if (ItemPickup.TryGetByNetworkId(msg.pickupId, out var existing) && existing != null)
        {
            existing.ReactivatePickup(new Vector3(msg.x, msg.y, 0f), existing.item, 1, msg.pickupId);
            return;
        }

        var item = ItemRegistry.FindItemByName(msg.itemName);
        if (item == null)
            return;

        var pool = Object.FindFirstObjectByType<DroppedItemPool>();
        if (pool == null)
        {
            Debug.LogWarning("[NET] DroppedItemPool not found to spawn network drop");
            return;
        }

        var pickup = pool.Spawn(item, new Vector3(msg.x, msg.y, 0f));
        if (pickup != null)
            pickup.networkId = msg.pickupId;
    }

    private static void HandleItemPickup(string json)
    {
        NetMessageItemPickup msg;
        try
        {
            msg = JsonUtility.FromJson<NetMessageItemPickup>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить item_pickup: {json}");
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.pickupId))
            return;

        if (ItemPickup.TryGetByNetworkId(msg.pickupId, out var pickup) && pickup != null)
        {
            var pool = Object.FindFirstObjectByType<DroppedItemPool>();
            if (pool != null)
                pool.Despawn(pickup.gameObject);
            else
                pickup.gameObject.SetActive(false);
        }
    }

    // ================== ОХОТА ==================

    private static void HandlePreySpawn(string json)
    {
        NetMessagePreySpawn msg;
        try
        {
            msg = JsonUtility.FromJson<NetMessagePreySpawn>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить prey_spawn: {json}");
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.preyId))
            return;

        if (PreyController.TryGetByNetworkId(msg.preyId, out _))
            return; // уже есть

        var hunt = Object.FindFirstObjectByType<ScentHuntController>();
        if (hunt == null || hunt.preyPrefab == null)
            return;

        var pos = new Vector3(msg.x, msg.y, 0f);
        var prey = Object.Instantiate(hunt.preyPrefab, pos, Quaternion.identity);
        Item drop = null;
        if (!string.IsNullOrEmpty(msg.dropItemName))
            drop = ItemRegistry.FindItemByName(msg.dropItemName);

        prey.Init(hunt.player != null ? hunt.player : null, hunt.gridSize, hunt.cellCenterOffset, hunt.blockMask, hunt.meatPickupPrefab, drop, msg.preyId, false);
    }

    private static void HandlePreyPosition(string json)
    {
        NetMessagePreyPosition msg;
        try
        {
            msg = JsonUtility.FromJson<NetMessagePreyPosition>(json);
        }
        catch
        {
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.id))
            return;

        if (PreyController.TryGetByNetworkId(msg.id, out var prey) && prey != null)
            prey.SetNetworkPosition(new Vector3(msg.x, msg.y, 0f));
    }

    private static void HandlePreyKill(string json)
    {
        NetMessagePreyKill msg;
        try
        {
            msg = JsonUtility.FromJson<NetMessagePreyKill>(json);
        }
        catch
        {
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.id))
            return;

        if (PreyController.TryGetByNetworkId(msg.id, out var prey) && prey != null)
            prey.Kill(true);
    }

    // ================== DISCONNECT ==================

    [System.Serializable]
    private class NetMessageDisconnect
    {
        public string type; // "disconnect"
        public string id;
    }

    private static void HandleDisconnect(string json)
    {
        NetMessageDisconnect disc;
        try
        {
            disc = JsonUtility.FromJson<NetMessageDisconnect>(json);
        }
        catch
        {
            Debug.LogWarning($"[NET] Не удалось распарсить disconnect: {json}");
            return;
        }

        if (disc == null || string.IsNullOrEmpty(disc.id))
            return;

        if (players.TryGetValue(disc.id, out var rp) && rp != null)
        {
            Object.Destroy(rp.gameObject);
        }

        players.Remove(disc.id);
        hpCache.Remove(disc.id);
        energyCache.Remove(disc.id);
    }

    // ================== СБРОС МИРА ==================

    public static void ClearAll()
    {
        foreach (var kv in players)
        {
            if (kv.Value != null)
                Object.Destroy(kv.Value.gameObject);
        }

        players.Clear();
        hpCache.Clear();
        energyCache.Clear();
    }

    // ================== ВСПОМОГАТЕЛЬНЫЕ ==================

    private static void ApplyCachedHp(string id, RemotePlayer rp)
    {
        if (rp == null || string.IsNullOrEmpty(id))
            return;

        if (!hpCache.TryGetValue(id, out var hp))
            return;

        var dmg = rp.GetComponent<Damageable>();
        if (dmg?.health == null)
            return;

        dmg.health.SetCurrentHpFromServer(hp);
    }

    private static void ApplyCachedEnergy(string id, RemotePlayer rp)
    {
        if (rp == null || string.IsNullOrEmpty(id))
            return;

        if (!energyCache.TryGetValue(id, out var entry))
            return;

        var dmg = rp.GetComponent<Damageable>();
        if (dmg?.energy == null)
            return;

        if (entry.maxEnergy > 0f)
            dmg.energy.maxEnergy = entry.maxEnergy;

        dmg.energy.SetCurrentEnergyFromServer(entry.energy);
    }

    /// <summary>
    /// Вытащить закешированную энергию для Damageable,
    /// когда объект только появился (используется в EnergySystem.ApplyCachedEnergyIfAny).
    /// </summary>
    public static bool TryGetCachedEnergy(string id, out float energy, out float maxEnergy)
    {
        if (energyCache.TryGetValue(id, out var entry))
        {
            energy = entry.energy;
            maxEnergy = entry.maxEnergy;
            return true;
        }

        energy = 0f;
        maxEnergy = 0f;
        return false;
    }
}
