using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Улучшенный ИИ врага с состояниями, умным преследованием и тактическим поведением.
/// </summary>
public class GridEnemyController : MonoBehaviour
{
    public const float DefaultMoveDuration = 0.2f;

    [Header("Grid")]
    public float gridSize = 1f;
    public Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);
    public float moveDuration = DefaultMoveDuration;

    [Header("AI - Detection")]
    public EnemySense sense;
    public float aggroRange = 12f;
    public float loseRange = 14f;
    public float memoryDuration = 3f; // как долго помнить последнюю позицию игрока

    [Header("AI - Combat")]
    public EnemyAttack attack;
    public ArrowController arrow;
    public float attackRange = 1.5f; // дистанция для атаки
    public float attackCooldownAfterMove = 0.3f; // пауза после движения перед атакой

    [Header("AI - Tactics")]
    [Tooltip("Процент HP, ниже которого враг начинает отступать")]
    [Range(0f, 1f)]
    public float retreatHealthThreshold = 0.3f;
    [Tooltip("Дистанция отступления от игрока")]
    public float retreatDistance = 3f;
    [Tooltip("Вероятность отступления (0-1)")]
    [Range(0f, 1f)]
    public float retreatChance = 0.7f;

    [Header("AI - Movement")]
    public float predictionTime = 0.5f; // предсказание движения игрока
    public float obstacleAvoidanceRange = 1.5f; // проверка препятствий
    public LayerMask obstacleLayer; // слой препятствий для обхода

    [Header("AI - Patrol")]
    public Vector2Int[] patrolCells;
    public bool loopPatrol = true;
    public float patrolWaitTime = 1f; // пауза на точке патруля

    [Header("Occupancy")]
    public GridOccupancyManager occupancyManager;

    // Состояния AI
    private enum AIState
    {
        Idle,       // бездействие
        Patrol,     // патрулирование
        Chase,      // преследование
        Attack,     // атака
        Retreat,    // отступление
        Search      // поиск (потерял игрока)
    }

    private AIState _currentState = AIState.Idle;
    private bool _isMoving;
    private int _patrolIndex;
    private Vector2Int _currentCell;
    
    // Память о игроке
    private Vector2Int? _lastKnownPlayerCell;
    private float _lastSeenTime;
    private Vector2 _lastPlayerVelocity;
    private Vector2 _previousPlayerPosition;

    // Тактика
    private HealthSystem _health;
    private Damageable _damageable;
    private float _lastAttackTime;
    private float _stateChangeTime;
    private float _patrolWaitEndTime;

    // Направления для обхода препятствий (4 направления)
    private static readonly Vector2Int[] _directions = new Vector2Int[]
    {
        new Vector2Int(1, 0),   // вправо
        new Vector2Int(-1, 0), // влево
        new Vector2Int(0, 1),   // вверх
        new Vector2Int(0, -1) // вниз
    };

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

        // Находим компоненты здоровья
        _damageable = GetComponent<Damageable>();
        if (_damageable != null)
            _health = _damageable.health;
        if (_health == null)
            _health = GetComponent<HealthSystem>();

        // Инициализация состояния
        if (patrolCells != null && patrolCells.Length > 0)
        {
            _currentState = AIState.Patrol;
        }
        else
        {
            _currentState = AIState.Idle;
        }

        _stateChangeTime = Time.time;
        _previousPlayerPosition = Vector2.zero;
    }

    void OnDisable()
    {
        occupancyManager?.Unregister(_currentCell);
    }

    void Update()
    {
        // Обновление памяти о игроке
        UpdatePlayerMemory();

        // Поворот стрелки к игроку
        UpdateArrowRotation();

        // Обновление состояния
        UpdateAIState();

        // Выполнение действий в зависимости от состояния
        if (!_isMoving)
        {
            ExecuteStateAction();
        }
    }

    private void UpdatePlayerMemory()
    {
        if (sense != null && sense.HasPlayer)
        {
            Vector2 currentPlayerPos = sense.player.position;
            
            // Вычисляем скорость игрока для предсказания
            if (_previousPlayerPosition != Vector2.zero)
            {
                _lastPlayerVelocity = (currentPlayerPos - _previousPlayerPosition) / Time.deltaTime;
            }
            _previousPlayerPosition = currentPlayerPos;

            _lastKnownPlayerCell = sense.PlayerCell(gridSize, cellCenterOffset);
            _lastSeenTime = Time.time;
        }
    }

    private void UpdateArrowRotation()
    {
        if (arrow == null) return;

        Vector2 targetDirection = Vector2.zero;

        if (sense != null && sense.HasPlayer)
        {
            // Предсказание позиции игрока
            Vector2 predictedPos = PredictPlayerPosition();
            targetDirection = predictedPos - (Vector2)transform.position;
        }
        else if (_lastKnownPlayerCell.HasValue)
        {
            // Поворот к последней известной позиции
            Vector2 lastKnownPos = CellToWorld(_lastKnownPlayerCell.Value);
            targetDirection = lastKnownPos - (Vector2)transform.position;
        }

        if (targetDirection != Vector2.zero)
        {
            float maxDelta = arrow.rotationSpeedDegPerSec * Time.deltaTime;
            arrow.RotateTowards(targetDirection, maxDelta);
        }
    }

    private Vector2 PredictPlayerPosition()
    {
        if (sense == null || !sense.HasPlayer)
            return transform.position;

        Vector2 currentPos = sense.player.position;
        Vector2 predictedPos = currentPos + _lastPlayerVelocity * predictionTime;
        return predictedPos;
    }

    private void UpdateAIState()
    {
        float currentTime = Time.time;
        bool hasPlayer = sense != null && sense.HasPlayer;
        float distanceToPlayer = hasPlayer ? sense.DistanceToPlayer() : Mathf.Infinity;
        float healthPercent = _health != null ? (_health.currentHp / _health.maxHp) : 1f;

        // Проверка на смерть
        if (_health != null && _health.IsDead)
        {
            _currentState = AIState.Idle;
            return;
        }

        switch (_currentState)
        {
            case AIState.Idle:
                if (patrolCells != null && patrolCells.Length > 0)
                {
                    ChangeState(AIState.Patrol);
                }
                break;

            case AIState.Patrol:
                if (hasPlayer && distanceToPlayer <= aggroRange)
                {
                    ChangeState(AIState.Chase);
                }
                break;

            case AIState.Chase:
                if (!hasPlayer || distanceToPlayer > loseRange)
                {
                    // Потерял игрока - переходим в поиск
                    if (_lastKnownPlayerCell.HasValue && (currentTime - _lastSeenTime) < memoryDuration)
                    {
                        ChangeState(AIState.Search);
                    }
                    else
                    {
                        ChangeState(patrolCells != null && patrolCells.Length > 0 ? AIState.Patrol : AIState.Idle);
                    }
                }
                else if (distanceToPlayer <= attackRange && attack != null && attack.CanAttack)
                {
                    ChangeState(AIState.Attack);
                }
                else if (healthPercent < retreatHealthThreshold && Random.value < retreatChance)
                {
                    ChangeState(AIState.Retreat);
                }
                break;

            case AIState.Attack:
                // После атаки возвращаемся к преследованию или отступлению
                if (currentTime - _stateChangeTime > attackCooldownAfterMove)
                {
                    if (hasPlayer && distanceToPlayer > attackRange)
                    {
                        ChangeState(AIState.Chase);
                    }
                    else if (healthPercent < retreatHealthThreshold)
                    {
                        ChangeState(AIState.Retreat);
                    }
                    else if (hasPlayer && distanceToPlayer <= attackRange && attack != null && attack.CanAttack)
                    {
                        // Остаемся в атаке, если можем атаковать снова
                    }
                    else
                    {
                        ChangeState(AIState.Chase);
                    }
                }
                break;

            case AIState.Retreat:
                if (!hasPlayer || distanceToPlayer > loseRange)
                {
                    ChangeState(patrolCells != null && patrolCells.Length > 0 ? AIState.Patrol : AIState.Idle);
                }
                else if (distanceToPlayer >= retreatDistance && healthPercent > retreatHealthThreshold * 1.5f)
                {
                    // Отошли достаточно далеко и восстановили здоровье
                    ChangeState(AIState.Chase);
                }
                else if (distanceToPlayer <= attackRange && healthPercent > retreatHealthThreshold)
                {
                    // Игрок слишком близко, но здоровье восстановилось
                    ChangeState(AIState.Chase);
                }
                break;

            case AIState.Search:
                if (hasPlayer && distanceToPlayer <= aggroRange)
                {
                    ChangeState(AIState.Chase);
                }
                else if (currentTime - _lastSeenTime > memoryDuration)
                {
                    // Забыли о игроке
                    ChangeState(patrolCells != null && patrolCells.Length > 0 ? AIState.Patrol : AIState.Idle);
                }
                break;
        }
    }

    private void ChangeState(AIState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        _stateChangeTime = Time.time;
    }

    private void ExecuteStateAction()
    {
        Vector2Int? nextCell = null;

        switch (_currentState)
        {
            case AIState.Idle:
                // Ничего не делаем
                break;

            case AIState.Patrol:
                nextCell = DecidePatrolStep();
                break;

            case AIState.Chase:
                nextCell = DecideChaseStep();
                break;

            case AIState.Attack:
                nextCell = DecideAttackStep();
                break;

            case AIState.Retreat:
                nextCell = DecideRetreatStep();
                break;

            case AIState.Search:
                nextCell = DecideSearchStep();
                break;
        }

        if (nextCell.HasValue)
        {
            StartCoroutine(MoveToCell(nextCell.Value));
        }
    }

    private Vector2Int? DecidePatrolStep()
    {
        if (patrolCells == null || patrolCells.Length == 0)
            return null;

        // Пауза на точке патруля
        if (patrolCells[_patrolIndex] == _currentCell)
        {
            if (Time.time < _patrolWaitEndTime)
                return null; // ждем

            _patrolIndex = (_patrolIndex + 1) % patrolCells.Length;
            if (!loopPatrol && _patrolIndex == 0)
                return null;

            _patrolWaitEndTime = Time.time + patrolWaitTime;
        }

        Vector2Int target = patrolCells[_patrolIndex];
        return FindPathTo(target);
    }

    private Vector2Int? DecideChaseStep()
    {
        if (sense == null || !sense.HasPlayer)
            return null;

        // Предсказываем позицию игрока
        Vector2 predictedPos = PredictPlayerPosition();
        Vector2Int targetCell = WorldToCell(predictedPos);

        // Если уже рядом - не двигаемся, атакуем
        int dx = targetCell.x - _currentCell.x;
        int dy = targetCell.y - _currentCell.y;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);

        if (distance <= attackRange && attack != null && attack.CanAttack)
        {
            return null; // остаемся на месте для атаки
        }

        return FindPathTo(targetCell);
    }

    private Vector2Int? DecideAttackStep()
    {
        if (sense == null || !sense.HasPlayer)
            return null;

        Vector2Int playerCell = sense.PlayerCell(gridSize, cellCenterOffset);
        int dx = playerCell.x - _currentCell.x;
        int dy = playerCell.y - _currentCell.y;

        // Если можем атаковать - атакуем
        if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1 && (dx != 0 || dy != 0))
        {
            if (attack != null && attack.CanAttack && Time.time - _lastAttackTime > attackCooldownAfterMove)
            {
                Vector2 dir = arrow != null && arrow.Direction != Vector2.zero
                    ? arrow.Direction
                    : new Vector2(Mathf.Sign(dx), Mathf.Sign(dy)).normalized;

                attack.TryAttack(dir);
                _lastAttackTime = Time.time;
            }
            return null; // остаемся на месте
        }

        // Подходим ближе для атаки
        return FindPathTo(playerCell);
    }

    private Vector2Int? DecideRetreatStep()
    {
        if (sense == null || !sense.HasPlayer)
            return null;

        // Отступаем от игрока
        Vector2Int playerCell = sense.PlayerCell(gridSize, cellCenterOffset);
        Vector2Int retreatDirection = _currentCell - playerCell;

        // Нормализуем направление отступления
        int rx = retreatDirection.x == 0 ? 0 : (retreatDirection.x > 0 ? 1 : -1);
        int ry = retreatDirection.y == 0 ? 0 : (retreatDirection.y > 0 ? 1 : -1);

        Vector2Int retreatTarget = new Vector2Int(_currentCell.x + rx, _currentCell.y + ry);

        // Если не можем отступить в этом направлении, пробуем другие
        if (!IsCellWalkable(retreatTarget))
        {
            // Пробуем перпендикулярные направления
            Vector2Int[] alternatives = new Vector2Int[]
            {
                new Vector2Int(_currentCell.x + ry, _currentCell.y + rx), // перпендикуляр 1
                new Vector2Int(_currentCell.x - ry, _currentCell.y - rx), // перпендикуляр 2
                new Vector2Int(_currentCell.x + rx, _currentCell.y), // только X
                new Vector2Int(_currentCell.x, _currentCell.y + ry)  // только Y
            };

            foreach (var alt in alternatives)
            {
                if (IsCellWalkable(alt))
                {
                    retreatTarget = alt;
                    break;
                }
            }
        }

        return IsCellWalkable(retreatTarget) ? retreatTarget : null;
    }

    private Vector2Int? DecideSearchStep()
    {
        if (!_lastKnownPlayerCell.HasValue)
            return null;

        Vector2Int target = _lastKnownPlayerCell.Value;
        
        // Если достигли последней известной позиции, ищем рядом
        if (target == _currentCell)
        {
            // Ищем в соседних клетках
            foreach (var dir in _directions)
            {
                Vector2Int checkCell = _currentCell + dir;
                if (IsCellWalkable(checkCell))
                {
                    return checkCell;
                }
            }
            return null;
        }

        return FindPathTo(target);
    }

    /// <summary>
    /// Поиск кратчайшего пути по сетке с учетом препятствий (BFS).
    /// </summary>
    private Vector2Int? FindPathTo(Vector2Int targetCell)
    {
        if (targetCell == _currentCell)
            return null;

        var frontier = new Queue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        frontier.Enqueue(_currentCell);
        cameFrom[_currentCell] = _currentCell;

        bool found = false;
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == targetCell)
            {
                found = true;
                break;
            }

            foreach (var dir in _directions)
            {
                var next = current + dir;
                if (cameFrom.ContainsKey(next))
                    continue;
                if (!IsCellWalkable(next))
                    continue;

                frontier.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        if (!found)
            return null;

        var step = targetCell;
        while (cameFrom.TryGetValue(step, out var prev) && prev != _currentCell)
        {
            step = prev;
        }

        return step;
    }

    /// <summary>
    /// Проверяет, можно ли пройти в клетку (не занята и нет препятствий).
    /// </summary>
    private bool IsCellWalkable(Vector2Int cell)
    {
        if (cell == _currentCell)
            return false;

        // Проверка занятости через GridOccupancyManager
        if (occupancyManager != null)
        {
            // Временно проверяем, можно ли переместиться
            // (это не идеально, но работает для простых случаев)
            Vector2 worldPos = CellToWorld(cell);
            Collider2D obstacle = Physics2D.OverlapCircle(worldPos, 0.3f, obstacleLayer);
            if (obstacle != null)
                return false;
        }

        return true;
    }

    private Vector2Int StepTowards(Vector2Int targetCell)
    {
        int dx = targetCell.x - _currentCell.x;
        int dy = targetCell.y - _currentCell.y;
        if (dx == 0 && dy == 0)
            return _currentCell;

        bool moveOnX = Mathf.Abs(dx) >= Mathf.Abs(dy);
        int sx = moveOnX ? (dx > 0 ? 1 : -1) : 0;
        int sy = moveOnX ? 0 : (dy > 0 ? 1 : -1);
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

        // Плавное движение (опционально, можно оставить мгновенное)
        float elapsed = 0f;
        Vector2 startPos = transform.position;
        
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            transform.position = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        _currentCell = cell;

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

    // Отладочная информация (можно включить в редакторе)
    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            // Рисуем текущее состояние
            Color stateColor = _currentState switch
            {
                AIState.Patrol => Color.yellow,
                AIState.Chase => Color.red,
                AIState.Attack => Color.magenta,
                AIState.Retreat => Color.cyan,
                AIState.Search => Color.blue,
                _ => Color.gray
            };

            Gizmos.color = stateColor;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            // Рисуем последнюю известную позицию игрока
            if (_lastKnownPlayerCell.HasValue)
            {
                Gizmos.color = Color.green;
                Vector2 lastPos = CellToWorld(_lastKnownPlayerCell.Value);
                Gizmos.DrawWireSphere(lastPos, 0.2f);
            }
        }
    }
}
