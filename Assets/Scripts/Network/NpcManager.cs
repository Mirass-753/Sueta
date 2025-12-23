using System.Collections.Generic;
using UnityEngine;

public class NpcManager : MonoBehaviour
{
    public static NpcManager Instance { get; private set; }

    [SerializeField]
    private GameObject npcPrefab;

    private readonly Dictionary<string, GameObject> _npcs = new Dictionary<string, GameObject>();
    private bool _initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (_initialized)
            return;

        EnsurePrefabAvailable();
        DisableLocalEnemyPrefabs();
        _initialized = true;
    }

    private void EnsurePrefabAvailable()
    {
        if (npcPrefab != null)
            return;

        var fromScene = FindSceneEnemyTemplate();
        if (fromScene != null)
        {
            npcPrefab = fromScene;
        }
        else
        {
            Debug.LogWarning("[NPC] npcPrefab not set and no scene enemy template found");
        }
    }

    private GameObject FindSceneEnemyTemplate()
    {
        var controllers = Resources.FindObjectsOfTypeAll<GridEnemyController>();
        foreach (var ctrl in controllers)
        {
            var go = ctrl.gameObject;
            if (!go.scene.IsValid())
                continue;

            return go;
        }

        return null;
    }

    private void DisableLocalEnemyPrefabs()
    {
        var controllers = Resources.FindObjectsOfTypeAll<GridEnemyController>();
        foreach (var ctrl in controllers)
        {
            var go = ctrl.gameObject;
            if (!go.scene.IsValid())
                continue;

            var dmg = go.GetComponent<Damageable>();
            if (dmg != null && dmg.isNetworkEntity)
                continue;

            if (npcPrefab == null)
                npcPrefab = go;

            if (go.activeSelf)
            {
                Debug.Log($"[NPC] Disabling local enemy '{go.name}' to avoid duplicate spawns");
                go.SetActive(false);
            }
        }
    }

    public void OnNpcSpawn(NetMessageNpcSpawn msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.npcId))
            return;

        InitializeIfNeeded();

        if (_npcs.TryGetValue(msg.npcId, out var existing) && existing != null)
        {
            ApplyState(existing, msg.x, msg.y, msg.hp);
            return;
        }

        if (npcPrefab == null)
        {
            Debug.LogWarning($"[NPC] Cannot spawn '{msg.npcId}' — npcPrefab missing");
            return;
        }

        var instance = Instantiate(npcPrefab, new Vector3(msg.x, msg.y, 0f), Quaternion.identity);
        instance.name = $"NPC_{msg.npcId}";

        ConfigureNetworkNpc(instance, msg.npcId, msg.hp);
        _npcs[msg.npcId] = instance;
    }

    public void OnNpcState(NetMessageNpcState msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.npcId))
            return;

        if (_npcs.TryGetValue(msg.npcId, out var npc) && npc != null)
        {
            ApplyState(npc, msg.x, msg.y, msg.hp);
        }
        else
        {
            OnNpcSpawn(new NetMessageNpcSpawn
            {
                type = "npc_spawn",
                npcId = msg.npcId,
                x = msg.x,
                y = msg.y,
                hp = msg.hp
            });
        }
    }

    public void OnNpcDespawn(NetMessageNpcDespawn msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.npcId))
            return;

        if (_npcs.TryGetValue(msg.npcId, out var npc) && npc != null)
        {
            Destroy(npc);
        }

        _npcs.Remove(msg.npcId);
    }

    public void ClearAll()
    {
        foreach (var kv in _npcs)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }

        _npcs.Clear();
    }

    private static void ApplyState(GameObject npc, float x, float y, float hp)
    {
        var position = new Vector3(x, y, npc.transform.position.z);
        var controller = npc.GetComponent<GridEnemyController>();
        if (controller != null && controller.enabled)
        {
            npc.transform.position = position;
        }
        else
        {
            var smoother = npc.GetComponent<NetworkNpcSmoother>();
            if (smoother == null)
                smoother = npc.AddComponent<NetworkNpcSmoother>();

            smoother.SetTarget(position);
        }

        var damageable = npc.GetComponent<Damageable>();
        var health = damageable != null ? damageable.health : npc.GetComponent<HealthSystem>();
        if (health != null)
            health.SetCurrentHpFromServer(hp);
    }

    private static void ConfigureNetworkNpc(GameObject npc, string npcId, float hp)
    {
        DisableNpcAI(npc);

        var damageable = npc.GetComponent<Damageable>();
        if (damageable != null)
            damageable.SetNetworkIdentity(npcId, true);

        var health = damageable != null ? damageable.health : npc.GetComponent<HealthSystem>();
        if (health != null)
            health.SetCurrentHpFromServer(hp);
    }

    private static void DisableNpcAI(GameObject npc)
    {
        var controller = npc.GetComponent<GridEnemyController>();
        if (controller != null)
            controller.enabled = false;

        var sense = npc.GetComponent<EnemySense>();
        if (sense != null)
            sense.enabled = false;

        var attack = npc.GetComponent<EnemyAttack>();
        if (attack != null)
            attack.enabled = false;
    }
}
