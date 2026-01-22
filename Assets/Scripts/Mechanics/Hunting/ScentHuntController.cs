using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScentHuntController : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PreyController preyPrefab;
    public GameObject meatPickupPrefab;
    public Item dropItem;
    public LineRenderer scentLine;
    public SkillProgressUI sniffSkillUI;
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

    [Header("Sniff Skill")]
    public string sniffSkillName = "Нюх";
    public int sniffMaxLevel = 9;
    public float sniffExperiencePerUse = 0.1f;
    public float sniffExperiencePerLevel = 1f;

    PreyController _prey;
    bool _sniffing;
    Coroutine _sniffRoutine;
    string _currentPreyId;
    int _sniffLevel = 1;
    float _sniffExperience;

    void Awake()
    {
        NetworkMessageHandler.TryConsumePendingPreySpawns(this);
        if (sniffSkillUI == null)
            sniffSkillUI = FindObjectOfType<SkillProgressUI>();
        UpdateSniffSkillUI();
    }

    void Update()
    {
        EnsurePlayer();

        NetworkMessageHandler.TryConsumePendingPreySpawns(this);

        if (!_sniffing && _prey == null && Input.GetKeyDown(sniffKey))
            _sniffRoutine = StartCoroutine(SniffRoutine());

        UpdateScentLine();
    }

    IEnumerator SniffRoutine()
    {
        _sniffing = true;

        float t = 0f;
        while (t < sniffDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        SpawnPrey();
        GainSniffExperience();
        _sniffing = false;
    }

    void SpawnPrey()
    {
        EnsurePlayer();
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
        _currentPreyId = _prey.networkId;

        SendPreySpawn(_prey, spawnPos);
    }

    void GainSniffExperience()
    {
        if (sniffMaxLevel < 1)
            sniffMaxLevel = 1;

        if (_sniffLevel >= sniffMaxLevel)
        {
            _sniffLevel = sniffMaxLevel;
            _sniffExperience = 0f;
            UpdateSniffSkillUI();
            return;
        }

        _sniffExperience += Mathf.Max(0f, sniffExperiencePerUse);
        float levelSize = Mathf.Max(0.01f, sniffExperiencePerLevel);

        while (_sniffExperience >= levelSize && _sniffLevel < sniffMaxLevel)
        {
            _sniffExperience -= levelSize;
            _sniffLevel += 1;
        }

        if (_sniffLevel >= sniffMaxLevel)
            _sniffExperience = 0f;

        UpdateSniffSkillUI();
    }

    void UpdateSniffSkillUI()
    {
        if (sniffSkillUI == null)
            return;

        sniffSkillUI.SetSkillName(sniffSkillName);

        float levelSize = Mathf.Max(0.01f, sniffExperiencePerLevel);
        float progress = _sniffLevel >= sniffMaxLevel ? 1f : Mathf.Clamp01(_sniffExperience / levelSize);
        sniffSkillUI.SetProgress(_sniffLevel, sniffMaxLevel, progress);
    }

    void HandlePreyKilled(PreyController prey)
    {
        if (_prey == prey) _prey = null;
        if (scentLine) scentLine.enabled = false;
        _currentPreyId = null;
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

    void EnsurePlayer()
    {
        if (player != null) return;

        var local = FindObjectOfType<PlayerController>();
        if (local != null)
            player = local.transform;
    }

    void SendPreySpawn(PreyController prey, Vector3 spawnPos)
    {
        if (prey == null || WebSocketClient.Instance == null || string.IsNullOrEmpty(prey.networkId))
            return;

        var msg = new NetMessagePreySpawn
        {
            preyId = prey.networkId,
            x = spawnPos.x,
            y = spawnPos.y,
            ownerId = PlayerController.LocalPlayerId,
            dropItemName = dropItem != null ? dropItem.name : null
        };

        WebSocketClient.Instance.Send(JsonUtility.ToJson(msg));
    }
}
