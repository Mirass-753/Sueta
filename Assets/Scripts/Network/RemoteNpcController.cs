using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemoteNpcController : MonoBehaviour
{
    [SerializeField]
    private float moveDuration = GridEnemyController.DefaultMoveDuration;

    [Header("Grid")]
    [SerializeField]
    private float gridSize = 1f;

    [SerializeField]
    private Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);

    [Header("Combat Move Delay")]
    [SerializeField]
    private float maxDelaySeconds = 0.3f;

    [SerializeField]
    private float diagonalMissPenalty = 0.2f;

    [SerializeField]
    private AnimationCurve missCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField]
    private float snapDistance = 2f;

    private string _npcId;
    private string _state;
    private float _hp;

    private Vector2Int _currentCell;
    private Vector2Int _desiredCell;
    private bool _hasDesiredCell;
    private bool _isMoving;
    private bool _hasPendingMove;
    private Coroutine _moveRoutine;
    private readonly Queue<Vector2Int> _moveQueue = new Queue<Vector2Int>();
    private bool _pendingRebuild;
    private bool _skipDelayUntilSynced;

    private EnemyAttack _attack;
    private ArrowController _arrow;
    private RemoteArrowSmoother _arrowSmoother;

    public string NpcId => _npcId;
    public string State => _state;
    public float Hp => _hp;

    private void Awake()
    {
        _attack = GetComponent<EnemyAttack>();
        _arrow = GetComponent<ArrowController>();
        if (_arrow == null)
            _arrow = GetComponentInChildren<ArrowController>(true);
        _arrowSmoother = GetComponent<RemoteArrowSmoother>();
        if (_arrowSmoother == null && _arrow != null)
        {
            _arrowSmoother = _arrow.GetComponent<RemoteArrowSmoother>();
            if (_arrowSmoother == null)
                _arrowSmoother = _arrow.gameObject.AddComponent<RemoteArrowSmoother>();
        }
        var gridController = GetComponent<GridEnemyController>();
        if (gridController != null)
        {
            gridSize = gridController.gridSize;
            cellCenterOffset = gridController.cellCenterOffset;
            moveDuration = gridController.moveDuration;
        }

        if (_attack != null && _attack.attackHitbox != null)
        {
            _attack.attackHitbox.enabled = false;
            _attack.attackHitbox = null;
        }

        if (_arrow != null)
        {
            _arrow.allowPlayerInput = false;
            _arrow.hideWhenNotCombat = false;
            _arrow.SetCombatActive(true);
            var arrowRenderer = _arrow.GetComponent<SpriteRenderer>();
            if (arrowRenderer != null && !arrowRenderer.enabled)
                arrowRenderer.enabled = true;
        }

        if (_arrow != null && _arrowSmoother != null)
            _arrowSmoother.MatchRotationSpeed(_arrow.rotationSpeedDegPerSec);
    }

    public void Initialize(string npcId, float hp)
    {
        _npcId = npcId;
        _hp = hp;
        _currentCell = WorldToCell(transform.position);
        transform.position = CellToWorld(_currentCell);
    }

    public void ApplyState(Vector3 position, float hp, string state, Vector2 direction, bool moving)
    {
        _hp = hp;
        if (!string.IsNullOrEmpty(state))
            _state = state;
        SetDesiredCell(position, force: !_hasDesiredCell);

        if (_arrow != null && direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (_arrowSmoother != null)
                _arrowSmoother.SetTargetAngle(angle);
            else
                _arrow.SetAngle(angle);
        }
    }

    public void PlayAttack(Vector2 direction)
    {
        if (_arrow != null && direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (_arrowSmoother != null)
                _arrowSmoother.SetTargetAngle(angle);
            else
                _arrow.SetAngle(angle);
        }

        if (_attack != null)
        {
            _attack.TryAttack(direction);
        }
    }

    private void SetDesiredCell(Vector3 position, bool force)
    {
        var desired = WorldToCell(position);
        if (!force && snapDistance > 0f)
        {
            float distance = Vector3.Distance(transform.position, position);
            if (distance > snapDistance)
                force = true;
        }
        _desiredCell = desired;

        if (force)
        {
            _skipDelayUntilSynced = true;
        }

        _hasDesiredCell = true;
        _pendingRebuild = true;
        if (_isMoving)
        {
            _hasPendingMove = true;
            return;
        }

        TryStartMove();
    }

    private void TryStartMove()
    {
        if (_isMoving || !_hasDesiredCell)
            return;

        if (_pendingRebuild)
            RebuildMoveQueue();

        if (_moveQueue.Count == 0)
            return;

        if (_moveRoutine != null)
            StopCoroutine(_moveRoutine);

        _moveRoutine = StartCoroutine(MoveStep());
    }

    private IEnumerator MoveStep()
    {
        _isMoving = true;

        if (_pendingRebuild)
            RebuildMoveQueue();

        if (_moveQueue.Count == 0)
        {
            _isMoving = false;
            _moveRoutine = null;
            yield break;
        }

        Vector2Int nextCell = _moveQueue.Dequeue();
        Vector2 targetPos = CellToWorld(nextCell);

        float elapsed = 0f;
        float duration = Mathf.Max(moveDuration, 0f);
        Vector2 startPos = transform.position;

        if (duration <= 0f)
        {
            transform.position = targetPos;
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            transform.position = targetPos;
        }

        Vector2Int step = nextCell - _currentCell;
        float delay = _skipDelayUntilSynced ? 0f : ComputeDelay(step);
        _currentCell = nextCell;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _isMoving = false;
        _moveRoutine = null;

        if (_currentCell == _desiredCell)
            _skipDelayUntilSynced = false;

        if (_moveQueue.Count > 0)
        {
            TryStartMove();
            yield break;
        }

        if (_hasPendingMove)
        {
            _hasPendingMove = false;
            TryStartMove();
        }
    }

    private void RebuildMoveQueue()
    {
        _moveQueue.Clear();
        _pendingRebuild = false;

        Vector2Int cursor = _currentCell;
        int maxSteps = Mathf.Abs(_desiredCell.x - _currentCell.x) + Mathf.Abs(_desiredCell.y - _currentCell.y);
        for (int i = 0; i < maxSteps && cursor != _desiredCell; i++)
        {
            cursor = StepTowards(cursor, _desiredCell);
            _moveQueue.Enqueue(cursor);
        }
    }

    private float ComputeDelay(Vector2Int step)
    {
        if (_arrow == null || maxDelaySeconds <= 0f)
            return 0f;

        Vector2 moveDir = new Vector2(step.x, step.y).normalized;
        if (moveDir == Vector2.zero)
            return 0f;

        float moveAngle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
        float diff = Mathf.Abs(Mathf.DeltaAngle(_arrow.Angle, moveAngle));

        float t = Mathf.InverseLerp(0f, 180f, diff);

        float coeff;
        if (missCurve != null && missCurve.keys != null && missCurve.keys.Length > 0)
            coeff = Mathf.Clamp01(missCurve.Evaluate(t));
        else
            coeff = t;

        bool isDiagonalMove = step.x != 0 && step.y != 0;
        if (isDiagonalMove && diff > 5f && coeff < diagonalMissPenalty)
            coeff = Mathf.Max(coeff, diagonalMissPenalty);

        return maxDelaySeconds * coeff;
    }

    private Vector2Int StepTowards(Vector2Int current, Vector2Int target)
    {
        int dx = target.x - current.x;
        int dy = target.y - current.y;
        int sx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
        int sy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
        return new Vector2Int(current.x + sx, current.y + sy);
    }

    private Vector2Int WorldToCell(Vector2 worldPos)
    {
        float fx = worldPos.x / gridSize - cellCenterOffset.x;
        float fy = worldPos.y / gridSize - cellCenterOffset.y;
        return new Vector2Int(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        float wx = (cell.x + cellCenterOffset.x) * gridSize;
        float wy = (cell.y + cellCenterOffset.y) * gridSize;
        return new Vector2(wx, wy);
    }
}
