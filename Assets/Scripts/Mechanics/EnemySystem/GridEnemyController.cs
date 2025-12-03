using System.Collections;
using UnityEngine;

public class GridEnemyController : MonoBehaviour
{
    [Header("Grid")]
    public float gridSize = 1f;
    public Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);
    public float moveDuration = 0.2f;

    [Header("AI")]
    public EnemySense sense;
    public EnemyAttack attack;
    public ArrowController arrow;
    public float aggroRange = 4f;
    public float loseRange = 6f;
    public Vector2Int[] patrolCells;
    public bool loopPatrol = true;

    [Header("Occupancy")]
    public GridOccupancyManager occupancyManager;

    private bool _isMoving;
    private int _patrolIndex;
    private Vector2Int _currentCell;

    void Start()
    {
        Vector2 startPos = transform.position;
        _currentCell = WorldToCell(startPos);
        transform.position = CellToWorld(_currentCell);

        occupancyManager?.Register(_currentCell);

        if (arrow != null)
        {
            arrow.allowPlayerInput = false;
            arrow.SetCombatActive(true);
        }
    }

    void OnDisable()
    {
        occupancyManager?.Unregister(_currentCell);
    }

    void Update()
    {
        if (sense != null && sense.HasPlayer && arrow != null)
        {
            Vector2 toPlayer = sense.player.position - transform.position;
            float maxDelta = arrow.rotationSpeedDegPerSec * Time.deltaTime;
            arrow.RotateTowards(toPlayer, maxDelta);
        }

        if (_isMoving) return;

        Vector2Int? next = DecideNextStep();
        if (next.HasValue)
            StartCoroutine(MoveToCell(next.Value));
    }

    Vector2Int? DecideNextStep()
    {
        if (sense != null && sense.HasPlayer && sense.DistanceToPlayer() <= aggroRange)
        {
            var pc = sense.PlayerCell(gridSize, cellCenterOffset);
            int dx = pc.x - _currentCell.x;
            int dy = pc.y - _currentCell.y;

            if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1 && (dx != 0 || dy != 0))
            {
                Vector2 dir = arrow != null && arrow.Direction != Vector2.zero
                    ? arrow.Direction
                    : new Vector2(Mathf.Sign(dx), Mathf.Sign(dy));

                if (attack != null) attack.TryAttack(dir);
                return null;
            }
            return StepTowards(pc);
        }

        if (patrolCells.Length == 0) return null;
        var target = patrolCells[_patrolIndex];
        if (target == _currentCell)
        {
            _patrolIndex = (_patrolIndex + 1) % patrolCells.Length;
            if (!loopPatrol && _patrolIndex == 0) return null;
            target = patrolCells[_patrolIndex];
        }
        return StepTowards(target);
    }

    Vector2Int StepTowards(Vector2Int targetCell)
    {
        int dx = targetCell.x - _currentCell.x;
        int dy = targetCell.y - _currentCell.y;
        int sx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
        int sy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
        return new Vector2Int(_currentCell.x + sx, _currentCell.y + sy);
    }

    IEnumerator MoveToCell(Vector2Int cell)
    {
        _isMoving = true;

        Vector2 targetPos = CellToWorld(cell);

        bool canMove = occupancyManager == null || occupancyManager.TryMove(_currentCell, cell);
        if (!canMove)
        {
            _isMoving = false;
            yield break;
        }

        transform.position = targetPos;
        _currentCell = cell;

        yield return new WaitForSeconds(moveDuration);
        _isMoving = false;
    }

    Vector2Int WorldToCell(Vector2 worldPos)
    {
        float fx = worldPos.x / gridSize - cellCenterOffset.x;
        float fy = worldPos.y / gridSize - cellCenterOffset.y;
        return new Vector2Int(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
    }

    Vector2 CellToWorld(Vector2Int cell)
    {
        float wx = (cell.x + cellCenterOffset.x) * gridSize;
        float wy = (cell.y + cellCenterOffset.y) * gridSize;
        return new Vector2(wx, wy);
    }
}
