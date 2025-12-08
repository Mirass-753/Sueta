using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PreyController : MonoBehaviour
{
    [Header("Owner")]
    public Transform owner;                  // за кем охотится белка (владелец нюха)
    public string networkId;
    public bool isOwnerInstance = true;

    [Header("Move")]
    [Tooltip("Скорость в клетках в секунду")]
    public float cellsPerSecond = 2f;
    public float gridSize = 1f;
    public Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);
    public LayerMask blockMask;

    [Header("Behavior")]
    public float detectRadiusCells = 7f;
    public float alertPause = 0.4f;
    public float fleePauseBetweenSteps = 0.05f;

    [Header("Drop")]
    public GameObject pickupPrefab;
    public Item dropItem;

    public event Action<PreyController> OnKilled;

    private static readonly Dictionary<string, PreyController> registry = new Dictionary<string, PreyController>();

    bool _alerted;
    bool _fleeing;
    bool _moving;
    bool _dead;
    bool _sentKill;
    float _netSyncTimer;

    readonly List<Transform> _hunters = new List<Transform>();

    public bool IsDead => _dead;

    public static bool TryGetByNetworkId(string id, out PreyController prey)
    {
        if (!string.IsNullOrEmpty(id) && registry.TryGetValue(id, out prey) && prey != null)
            return true;

        prey = null;
        return false;
    }

    public void Init(Transform ownerTransform, float grid, Vector2 offset, LayerMask blocks, GameObject pickup, Item item, string newNetworkId = null, bool ownerInstance = true)
    {
        owner = ownerTransform;
        gridSize = grid;
        cellCenterOffset = offset;
        blockMask = blocks;
        pickupPrefab = pickup;
        dropItem = item;

        isOwnerInstance = ownerInstance;

        if (!string.IsNullOrEmpty(newNetworkId))
            networkId = newNetworkId;
        else if (string.IsNullOrEmpty(networkId))
            networkId = Guid.NewGuid().ToString();

        Register();
        RebuildHuntersList();
    }

    void Update()
    {
        if (_dead)
            return;

        _netSyncTimer += Time.deltaTime;
        if (isOwnerInstance && _netSyncTimer >= 0.2f)
        {
            _netSyncTimer = 0f;
            SendNetworkPosition();
        }

        RebuildHuntersList();

        Transform closestHunter = GetClosestHunter();
        if (closestHunter == null)
            return;

        // смерть, если охотник встал на ту же клетку
        if (WorldToCell(transform.position) == WorldToCell(closestHunter.position))
        {
            Kill();
            return;
        }

        float distCells = Vector2.Distance(WorldToCell(transform.position), WorldToCell(closestHunter.position));
        if (!_alerted && distCells <= detectRadiusCells)
        {
            _alerted = true;
            StartCoroutine(AlertThenFlee());
        }
    }

    IEnumerator AlertThenFlee()
    {
        yield return new WaitForSeconds(alertPause);
        _fleeing = true;

        while (_fleeing && !_dead)
        {
            yield return MoveOneFleeStep();
            yield return new WaitForSeconds(fleePauseBetweenSteps);
        }
    }

    IEnumerator MoveOneFleeStep()
    {
        if (_moving) yield break;
        _moving = true;

        Vector2Int myCell = WorldToCell(transform.position);
        Transform closestHunter = GetClosestHunter();
        Vector2Int ownerCell = closestHunter != null ? WorldToCell(closestHunter.position) : WorldToCell(transform.position);

        Vector2Int bestDir = Vector2Int.zero;
        float bestDist = -Mathf.Infinity;

        Vector2Int[] dirs = {
            new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
            new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1)
        };

        foreach (var d in dirs)
        {
            Vector2Int candidate = myCell + d;
            float dist = (candidate - ownerCell).sqrMagnitude;
            if (dist <= bestDist) continue;

            Vector3 world = CellToWorld(candidate);
            bool blocked = Physics2D.OverlapCircle(world, 0.1f, blockMask);
            if (blocked) continue;

            bestDist = dist;
            bestDir = d;
        }

        if (bestDir == Vector2Int.zero)
        {
            _moving = false;
            yield break;
        }

        float moveDuration = Mathf.Max(0.01f, 1f / Mathf.Max(0.01f, cellsPerSecond));
        Vector3 start = transform.position;
        Vector3 target = CellToWorld(myCell + bestDir);
        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / moveDuration);
            transform.position = Vector3.Lerp(start, target, k);
            yield return null;
        }
        transform.position = target;
        _moving = false;
    }

    public void HitByPlayer() => Kill();

    public void Kill(bool fromNetwork = false)
    {
        if (_dead) return;
        _dead = true;
        _fleeing = false;
        _moving = false;

        if (!_sentKill && !fromNetwork)
        {
            _sentKill = true;
            SendKillMessage();
        }

        if (pickupPrefab != null && dropItem != null)
        {
            var go = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup != null)
                pickup.ReactivatePickup(transform.position, dropItem);
        }

        OnKilled?.Invoke(this);
        Unregister();
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_dead) return;
        if (other.GetComponent<PlayerController>() != null || other.GetComponent<RemotePlayer>() != null)
        {
            Kill();
        }
    }

    public void SetNetworkPosition(Vector3 pos)
    {
        if (isOwnerInstance || _dead)
            return;

        transform.position = pos;
    }

    void RebuildHuntersList()
    {
        _hunters.Clear();
        if (owner != null)
            _hunters.Add(owner);

        var locals = FindObjectsOfType<PlayerController>();
        foreach (var p in locals)
        {
            if (p != null && !_hunters.Contains(p.transform))
                _hunters.Add(p.transform);
        }

        var remotes = FindObjectsOfType<RemotePlayer>();
        foreach (var rp in remotes)
        {
            if (rp != null && !_hunters.Contains(rp.transform))
                _hunters.Add(rp.transform);
        }
    }

    Transform GetClosestHunter()
    {
        Transform result = null;
        float bestDist = float.MaxValue;
        Vector2Int myCell = WorldToCell(transform.position);

        foreach (var h in _hunters)
        {
            if (h == null) continue;
            float dist = Vector2Int.Distance(myCell, WorldToCell(h.position));
            if (dist < bestDist)
            {
                bestDist = dist;
                result = h;
            }
        }

        return result;
    }

    void SendNetworkPosition()
    {
        if (WebSocketClient.Instance == null || string.IsNullOrEmpty(networkId))
            return;

        var msg = new NetMessagePreyPosition
        {
            type = "prey_pos",
            id = networkId,
            x = transform.position.x,
            y = transform.position.y
        };

        WebSocketClient.Instance.Send(JsonUtility.ToJson(msg));
    }

    void SendKillMessage()
    {
        if (WebSocketClient.Instance == null || string.IsNullOrEmpty(networkId))
            return;

        var msg = new NetMessagePreyKill
        {
            type = "prey_kill",
            id = networkId,
            killerId = PlayerController.LocalPlayerId
        };

        WebSocketClient.Instance.Send(JsonUtility.ToJson(msg));
    }

    void Register()
    {
        if (string.IsNullOrEmpty(networkId))
            return;

        registry[networkId] = this;
    }

    void Unregister()
    {
        if (!string.IsNullOrEmpty(networkId))
            registry.Remove(networkId);
    }

    Vector2Int WorldToCell(Vector2 worldPos)
    {
        float fx = worldPos.x / gridSize - cellCenterOffset.x;
        float fy = worldPos.y / gridSize - cellCenterOffset.y;
        return new Vector2Int(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
    }

    Vector3 CellToWorld(Vector2Int cell)
    {
        float wx = (cell.x + cellCenterOffset.x) * gridSize;
        float wy = (cell.y + cellCenterOffset.y) * gridSize;
        return new Vector3(wx, wy, 0f);
    }
}
