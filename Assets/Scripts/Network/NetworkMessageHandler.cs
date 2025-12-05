using System.Collections.Generic;
using UnityEngine;

/// Базовый тип, чтобы вытащить поле "type" из любого сообщения.
public static class NetworkMessageHandler
{
    // id игрока -> его визуальный удалённый кот
    private static readonly Dictionary<string, RemotePlayer> players =
        new Dictionary<string, RemotePlayer>();

    // Кеш HP от сервера, чтобы применять его даже если объект ещё не создан
    private static readonly Dictionary<string, float> hpCache =
        new Dictionary<string, float>();

    /// Точка входа: сюда WebSocketClient передаёт сырую строку json.
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
            Debug.LogWarning($"[Net] Не удалось распарсить тип сообщения: {json}");
            return;
        }

        if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
        {
            // старые сообщения без type можно либо игнорировать, либо считать move
            // здесь строго: игнорируем
            Debug.LogWarning($"[Net] Сообщение без type: {json}");
            return;
        }

        switch (baseMsg.type)
        {
            case "move":
                HandleMove(json);
                break;

            case "damage":
                HandleDamage(json);
                break;

            case "hp_sync":
                HandleHpSync(json);
                break;

            case "disconnect":
                HandleDisconnect(json);
                break;

            default:
                // необязательный лог:
                // Debug.Log($"[Net] Неизвестный type: {baseMsg.type}");
                break;
        }
    }

    // ---------- ДВИЖЕНИЕ ----------

    private static void HandleMove(string json)
    {
        NetMessageMove move;
        try
        {
            move = JsonUtility.FromJson<NetMessageMove>(json);
        }
        catch
        {
            Debug.LogWarning($"[Net] Не удалось распарсить move: {json}");
            return;
        }

        if (move == null || string.IsNullOrEmpty(move.id))
            return;

        // свои же сообщения игнорируем (локальный кот и так знает, где он)
        if (move.id == PlayerController.LocalPlayerId)
            return;

        if (!players.TryGetValue(move.id, out var rp) || rp == null)
        {
            rp = RemotePlayer.Create(move.id);
            players[move.id] = rp;
            ApplyCachedHp(move.id, rp);
        }

        Vector2 pos = new Vector2(move.x, move.y);
        Vector2 dir = new Vector2(move.dirX, move.dirY);
        rp.SetNetworkState(pos, dir, move.moving);
    }

    // ---------- УРОН ОТ СЕРВЕРА ----------

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

        Debug.Log($"[NET] DAMAGE: source={msg.sourceId}, target={msg.targetId}, " +
                  $"amount={msg.amount}, hp={msg.hp}");

        // Ищем цель по networkId через реестр Damageable
        if (!Damageable.TryGetById(msg.targetId, out var target))
        {
            Debug.LogWarning($"[NET] Damage target '{msg.targetId}' not found in Damageable registry");
            return;
        }

        if (target.health == null)
        {
            Debug.LogWarning($"[NET] Damage target '{target.name}' has no HealthSystem");
            return;
        }

        // >>> ВОТ ЭТОТ ВЫЗОВ — САМОЕ ГЛАВНОЕ <<<
        target.health.SetCurrentHpFromServer(msg.hp);
    }

    // ---------- СИНХРОНИЗАЦИЯ HP ДЛЯ НОВЫХ КЛИЕНТОВ ----------

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

    // ---------- ОТКЛЮЧЕНИЕ ИГРОКА ----------

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
            Debug.LogWarning($"[Net] Не удалось распарсить disconnect: {json}");
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
    }

    // ---------- Сброс мира (например, при смене сцены) ----------

    public static void ClearAll()
    {
        foreach (var kv in players)
        {
            if (kv.Value != null)
                Object.Destroy(kv.Value.gameObject);
        }
        players.Clear();
        hpCache.Clear();
    }

    // ---------- ВНУТРЕННИЕ УТИЛИТЫ ----------

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
}
