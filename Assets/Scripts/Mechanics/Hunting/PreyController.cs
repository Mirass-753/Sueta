using System;
using System.Collections;
using UnityEngine;

public class PreyController : MonoBehaviour
{
    [Header("Owner")]
    public Transform owner;                  // за кем охотится белка (владелец нюха)

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

    bool _alerted;
    bool _fleeing;
    bool _moving;
    bool _dead;

    public bool IsDead => _dead;

    public void Init(Transform ownerTransform, float grid, Vector2 offset, LayerMask blocks, GameObject pickup, Item item)
    {
        owner = ownerTransform;
        gridSize = grid;
        cellCenterOffset = offset;
        blockMask = blocks;
        pickupPrefab = pickup;
        dropItem = item;
    }

    void Update()
    {
        if (_dead || owner == null) return;

        // смерть, если владелец встал на ту же клетку
        if (WorldToCell(transform.position) == WorldToCell(owner.position))
        {
            Kill();
            return;
        }

        float distCells = Vector2.Distance(WorldToCell(transform.position), WorldToCell(owner.position));
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
        Vector2Int ownerCell = WorldToCell(owner.position);

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

    public void Kill()
    {
        if (_dead) return;
        _dead = true;
        _fleeing = false;
        _moving = false;

        if (pickupPrefab != null && dropItem != null)
        {
            var go = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup != null)
                pickup.ReactivatePickup(transform.position, dropItem);
        }

        OnKilled?.Invoke(this);
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_dead) return;
        if (owner != null && other.transform == owner)
            Kill();
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
