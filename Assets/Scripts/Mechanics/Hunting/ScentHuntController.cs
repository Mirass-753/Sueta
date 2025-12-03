using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScentHuntController : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PreyController preyPrefab;
    public GameObject meatPickupPrefab;
    public Item dropItem;
    public LineRenderer scentLine;
    public Image sniffProgressFill;
    public LayerMask blockMask;

    [Header("Grid")]
    public float gridSize = 1f;
    public Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);
    public Vector2Int screenCells = new Vector2Int(14, 8);
    public int spawnScreensAway = 2;

    [Header("Sniff")]
    public KeyCode sniffKey = KeyCode.Alpha1;
    public float sniffDuration = 6f;
    public float hideLineRadiusCells = 10f; // скрываем линию, если игрок ближе

    PreyController _prey;
    bool _sniffing;
    Coroutine _sniffRoutine;

    void Update()
    {
        if (!_sniffing && _prey == null && Input.GetKeyDown(sniffKey))
            _sniffRoutine = StartCoroutine(SniffRoutine());

        UpdateScentLine();
    }

    IEnumerator SniffRoutine()
    {
        _sniffing = true;
        if (sniffProgressFill) sniffProgressFill.fillAmount = 0f;

        float t = 0f;
        while (t < sniffDuration)
        {
            t += Time.deltaTime;
            if (sniffProgressFill) sniffProgressFill.fillAmount = Mathf.Clamp01(t / sniffDuration);
            yield return null;
        }

        if (sniffProgressFill) sniffProgressFill.fillAmount = 1f;
        SpawnPrey();
        _sniffing = false;
    }

    void SpawnPrey()
    {
        if (preyPrefab == null || player == null) return;

        Vector2Int playerCell = WorldToCell(player.position);
        Vector2Int dir = Random.value > 0.5f ? Vector2Int.right : Vector2Int.left;
        if (Random.value > 0.5f) dir += Random.value > 0.5f ? Vector2Int.up : Vector2Int.down;
        dir = new Vector2Int(Mathf.Clamp(dir.x, -1, 1), Mathf.Clamp(dir.y, -1, 1));
        if (dir == Vector2Int.zero) dir = Vector2Int.right;

        Vector2Int offset = new Vector2Int(
            dir.x * spawnScreensAway * screenCells.x,
            dir.y * spawnScreensAway * screenCells.y
        );

        Vector2Int spawnCell = playerCell + offset;
        Vector3 spawnPos = CellToWorld(spawnCell);
        _prey = Instantiate(preyPrefab, spawnPos, Quaternion.identity);
        _prey.Init(player, gridSize, cellCenterOffset, blockMask, meatPickupPrefab, dropItem);
        _prey.OnKilled += HandlePreyKilled;
    }

    void HandlePreyKilled(PreyController prey)
    {
        if (_prey == prey) _prey = null;
        if (scentLine) scentLine.enabled = false;
    }

    void UpdateScentLine()
    {
        if (scentLine == null)
            return;

        if (_prey == null || _prey.IsDead)
        {
            scentLine.enabled = false;
            return;
        }

        // скрываем линию, если игрок достаточно близко
        float distCells = Vector2.Distance(WorldToCell(player.position), WorldToCell(_prey.transform.position));
        if (distCells <= hideLineRadiusCells)
        {
            scentLine.enabled = false;
            return;
        }

        // шаговая линия по клеткам (без учёта стен)
        Vector2Int a = WorldToCell(player.position);
        Vector2Int b = WorldToCell(_prey.transform.position);
        List<Vector3> points = new List<Vector3>();
        Vector2Int cur = a;

        while (cur != b)
        {
            if (cur.x != b.x) cur.x += Mathf.Sign(b.x - cur.x) > 0 ? 1 : -1;
            if (cur.y != b.y) cur.y += Mathf.Sign(b.y - cur.y) > 0 ? 1 : -1;
            points.Add(CellToWorld(cur));
        }

        scentLine.enabled = true;
        scentLine.positionCount = points.Count + 1;
        scentLine.SetPosition(0, player.position);
        for (int i = 0; i < points.Count; i++)
            scentLine.SetPosition(i + 1, points[i]);
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
