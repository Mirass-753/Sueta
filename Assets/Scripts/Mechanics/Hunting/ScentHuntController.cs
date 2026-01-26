using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScentHuntController : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PreyController preyPrefab;
    public GameObject meatPickupPrefab;
    public LineRenderer scentLine;
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
    public string sniffSkillId = "sniff";
    public string sniffSkillName = "Нюх";
    public float sniffExpGain = 5f;
    public float sniffExpToLevel = 100f;
    public int sniffMaxLevel = 10;
    public int sniffStartLevel = 1;

    PreyController _prey;
    bool _sniffing;
    Coroutine _sniffRoutine;

    void Awake()
    {
        NetworkMessageHandler.TryConsumePendingPreySpawns(this);
    }

    void Update()
    {
        EnsurePlayer();

        NetworkMessageHandler.TryConsumePendingPreySpawns(this);

        if (Input.GetKeyDown(sniffKey))
        {
            SkillsState.AddLocalExp(sniffSkillId, sniffSkillName, sniffExpGain, sniffExpToLevel, sniffMaxLevel, sniffStartLevel);
            RequestSniff();
        }

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

        SendSniffRequest();
        _sniffing = false;
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

    void EnsurePlayer()
    {
        if (player != null) return;

        var local = FindObjectOfType<PlayerController>();
        if (local != null)
            player = local.transform;
    }

    void SendSniffRequest()
    {
        if (WebSocketClient.Instance == null)
            return;

        var playerId = PlayerController.LocalPlayerId;
        if (string.IsNullOrEmpty(playerId))
            return;

        var msg = new NetMessageSniffRequest
        {
            playerId = playerId
        };

        WebSocketClient.Instance.Send(JsonUtility.ToJson(msg));
    }

    public void RequestSniff()
    {
        if (_sniffing || _prey != null)
            return;

        _sniffRoutine = StartCoroutine(SniffRoutine());
    }

    public void AssignPrey(PreyController prey)
    {
        if (prey == null)
            return;

        if (_prey != null)
            _prey.OnKilled -= HandlePreyKilled;

        _prey = prey;
        _prey.OnKilled += HandlePreyKilled;
    }
}
