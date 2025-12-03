using System.Collections.Generic;
using UnityEngine;

/// Базовый тип, чтобы вытащить поле "type" из любого сообщения.


public static class NetworkMessageHandler
{
    // id игрока -> его визуальный удалённый кот
    private static readonly Dictionary<string, RemotePlayer> players =
        new Dictionary<string, RemotePlayer>();

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
        }

        Vector2 pos = new Vector2(move.x, move.y);
        Vector2 dir = new Vector2(move.dirX, move.dirY);
        rp.SetNetworkState(pos, dir, move.moving);
    }

    // ---------- УРОН ОТ СЕРВЕРА ----------

    private static void HandleDamage(string json)
    {
        NetMessageDamageEvent dmg;
        try
        {
            dmg = JsonUtility.FromJson<NetMessageDamageEvent>(json);
        }
        catch
        {
            Debug.LogWarning($"[Net] Не удалось распарсить damage: {json}");
            return;
        }

        if (dmg == null || string.IsNullOrEmpty(dmg.targetId))
            return;

        if (!Damageable.TryGetById(dmg.targetId, out var target) || target == null)
            return;

        // применяем значение HP, которое посчитал сервер
        if (target.health != null)
        {
            target.health.SetCurrentHpFromServer(dmg.hp);
        }

        // здесь позже можно добавить анимации попадания, эффект вспышки и т.п.
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
    }
}
