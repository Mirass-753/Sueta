using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class CombatModeController : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    public ArrowController arrowController;
    public EnergySystem energySystem;
    public CameraController cameraController;
    public GridOccupancyManager occupancyManager;

    public UnityEvent onCombatEnter;
    public UnityEvent onCombatExit;

    [Header("Movement")]
    public float maxDelaySeconds = 0.3f;
    public float diagonalMissPenalty = 0.2f;
    public float combatSpeedMultiplier = 5f;
    public float gridSize = 1f;
    public Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);

    [Header("Input buffer")]
[Tooltip("Максимальное время жизни запомненного шага после нажатия, сек.")]
public float inputBufferSeconds = 0.25f;

    [Header("Input")]
    public KeyCode toggleCombatKey = KeyCode.R;
    public KeyCode attackKey = KeyCode.I;
    public KeyCode blockKey = KeyCode.K;

    [Header("Attack (Trigger-based)")]
    public Collider2D attackHitbox;
    public float attackWindowSeconds = 0.12f;
    public float attackHoldInterval = 0.2f; // интервал между авто-атаками при удержании

    [Header("Parry/Block")]
    public float parryWindowSeconds = 2f / 60f;

    [Header("Visual")]
    [Tooltip("CanvasGroup поверх экрана для затемнения в бою (опционально).")]
    public CanvasGroup combatOverlay;
    [Range(0f, 1f)]
    public float overlayMaxAlpha = 0.35f;
    public float overlayFadeSpeed = 8f;

    [Header("Miss delay curve")]
    [Tooltip("X: нормализованный угол (0 = в сторону стрелки, 1 = в противоположную). Y: множитель задержки 0..1.")]
    public AnimationCurve missCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Collision vs Players")]
    [SerializeField] private BoxCollider2D bodyCollider;   // тот же коллайдер, что и у Player
    [SerializeField] private LayerMask playerLayer;  
    
    [Header("Collision vs Environment")]
[SerializeField] private LayerMask environmentLayer;       // слой игроков (например, 'Player')

    private bool _combatActive;
    private bool _isTeleporting;
    private bool _isBlocking;
    private float _blockStartTime;
    private Coroutine _moveRoutine;
    private Coroutine _attackRoutine;
    private bool _toggledThisFrame;

    // буфер направления для следующего шага
    private Vector2 _queuedMoveDir;
    private bool _hasQueuedMoveDir;
    private float _queuedMoveTimestamp;

    // для управления автоатакой при удержании
    private bool _attackKeyHeld;
    private float _nextAttackTime;

    public bool IsBlocking => _isBlocking;
    public bool IsInParryWindow => _isBlocking && (Time.time - _blockStartTime) <= parryWindowSeconds;
    public bool IsCombatActive => _combatActive;

    private void Update()
    {
        // затемнение камеры (оверлей) – всегда обновляем
        UpdateOverlay();

        // работаем только у владельца
        if (playerController != null && !playerController.IsOwner)
            return;

        _toggledThisFrame = false;

        // включение/выключение боевого режима
        if (Input.GetKeyDown(toggleCombatKey))
        {
            ToggleCombat(!_combatActive);
            _toggledThisFrame = true;
        }

        if (_toggledThisFrame || !_combatActive)
            return;

        // разворачиваем кота по стрелке
        if (arrowController != null && playerController != null)
            playerController.FaceByVector(arrowController.Direction);

        HandleBlockInput();
        HandleAttackInput();
        HandleMovementInput();
    }

    private void UpdateOverlay()
    {
        if (combatOverlay == null)
            return;

        float targetAlpha = _combatActive ? overlayMaxAlpha : 0f;
        combatOverlay.alpha = Mathf.MoveTowards(
            combatOverlay.alpha,
            targetAlpha,
            overlayFadeSpeed * Time.deltaTime
        );
    }

    private void ToggleCombat(bool active)
    {
        _combatActive = active;

        if (arrowController != null)
            arrowController.SetCombatActive(active);

        if (playerController != null)
            playerController.enabled = !active;

        if (energySystem != null)
            energySystem.SetCombat(active);

        if (cameraController != null)
            cameraController.SetChunkLock(active);

        if (playerController != null)
            playerController.ResetMovementState();

        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        _isTeleporting = false;
        _isBlocking = false;
        _hasQueuedMoveDir = false;
        _queuedMoveDir = Vector2.zero;

        // сброс автоатаки
        _attackKeyHeld = false;
        _nextAttackTime = 0f;

        SnapToGrid();
        SetHitboxActive(false);

        if (active) onCombatEnter?.Invoke();
        else onCombatExit?.Invoke();
    }

    private void SnapToGrid()
    {
        Vector2Int cell = WorldToCell(transform.position);
        transform.position = CellToWorld(cell);
    }

    private void HandleBlockInput()
    {
        if (Input.GetKeyDown(blockKey))
        {
            _isBlocking = true;
            _blockStartTime = Time.time;
        }
        else if (Input.GetKeyUp(blockKey))
        {
            _isBlocking = false;
        }
    }

    /// <summary>
    /// Атака ТОЛЬКО когда зажата attackKey.
    /// Первый удар – сразу при нажатии, затем каждые attackHoldInterval секунд.
    /// </summary>
    private void HandleAttackInput()
    {
        if (_isBlocking) return;
        if (arrowController != null && arrowController.IsRotating) return;
        if (energySystem != null && energySystem.CurrentEnergy <= 0f) return;

        bool keyHeld = Input.GetKey(attackKey);

        if (keyHeld)
        {
            // первый кадр нажатия – атака сразу
            if (!_attackKeyHeld || Time.time >= _nextAttackTime)
            {
                StartAttackWindow();
                _nextAttackTime = Time.time + attackHoldInterval;
            }
        }

        // при отпускании клавиши атака полностью останавливается
        if (!keyHeld && _attackKeyHeld)
        {
            // можно принудительно выключить хитбокс (на случай, если окно ещё шло)
            SetHitboxActive(false);
        }

        _attackKeyHeld = keyHeld;
    }

    private void StartAttackWindow()
    {
        if (_attackRoutine != null)
            StopCoroutine(_attackRoutine);

        _attackRoutine = StartCoroutine(AttackWindow());

        if (energySystem != null)
            energySystem.SelfDamage();
    }

    private IEnumerator AttackWindow()
    {
        SetHitboxActive(true);
        yield return new WaitForSeconds(attackWindowSeconds);
        SetHitboxActive(false);
        _attackRoutine = null;
    }

    private void SetHitboxActive(bool active)
    {
        if (attackHitbox != null)
            attackHitbox.enabled = active;
    }

    // ============ ДВИЖЕНИЕ В БОЮ ============

    private void HandleMovementInput()
    {
        if (_isBlocking)
            return;

        // направление от зажатых клавиш (для "просто держу кнопку и еду")
        Vector2 heldDir = GetHeldDirection();
        // направление от нового нажатия в этом кадре
        Vector2 downDir = GetDownDirection();

        if (_isTeleporting)
        {
            // во время шага буферим ТОЛЬКО новые нажатия
            if (downDir != Vector2.zero)
            {
                _queuedMoveDir = downDir;
                _hasQueuedMoveDir = true;
                _queuedMoveTimestamp = Time.time;
            }
            return;
        }

        // приоритет — новое нажатие, иначе просто удержание
        Vector2 useDir = downDir != Vector2.zero ? downDir : heldDir;

        if (useDir == Vector2.zero)
        {
            _hasQueuedMoveDir = false;
            _queuedMoveDir = Vector2.zero;
            _queuedMoveTimestamp = 0f;
            return;
        }

        _moveRoutine = StartCoroutine(TeleportStep(useDir));
    }

    private Vector2 GetHeldDirection()
    {
        // Диагонали через Q/E/Z/C
        if (Input.GetKey(KeyCode.Q)) return new Vector2(-1f, 1f);
        if (Input.GetKey(KeyCode.E)) return new Vector2(1f, 1f);
        if (Input.GetKey(KeyCode.Z)) return new Vector2(-1f, -1f);
        if (Input.GetKey(KeyCode.C)) return new Vector2(1f, -1f);

        float x = 0f, y = 0f;

        // Горизонталь
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            x = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            x = 1f;

        // Вертикаль
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            y = 1f;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            y = -1f;

        // Убираем диагональ от двух кнопок — приоритет вертикали
        if (x != 0f && y != 0f)
            x = 0f;

        if (x == 0f && y == 0f)
            return Vector2.zero;

        return new Vector2(x, y);
    }

    private Vector2 GetDownDirection()
    {
        // Диагонали через Q/E/Z/C
        if (Input.GetKeyDown(KeyCode.Q)) return new Vector2(-1f, 1f);
        if (Input.GetKeyDown(KeyCode.E)) return new Vector2(1f, 1f);
        if (Input.GetKeyDown(KeyCode.Z)) return new Vector2(-1f, -1f);
        if (Input.GetKeyDown(KeyCode.C)) return new Vector2(1f, -1f);

        float x = 0f, y = 0f;

        // Горизонталь
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            x = -1f;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            x = 1f;

        // Вертикаль
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            y = 1f;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            y = -1f;

        // Убираем диагональ от двух кнопок — приоритет вертикали
        if (x != 0f && y != 0f)
            x = 0f;

        if (x == 0f && y == 0f)
            return Vector2.zero;

        return new Vector2(x, y);
    }

    /// <summary>
    /// Проверка, что в целевой точке не стоит другой игрок.
    /// </summary>
    private bool IsBlockedByOtherPlayerCombat(Vector2 targetWorldPos)
    {
        if (bodyCollider == null)
            return false;

        Vector2 size = bodyCollider.bounds.size * 0.9f;

        Collider2D hit = Physics2D.OverlapBox(
            targetWorldPos,
            size,
            0f,
            playerLayer
        );

        if (hit == null) return false;
        return hit.gameObject != gameObject;
    }

    /// <summary>
/// Проверка, что в целевой точке стоит объект окружения (дерево, камень).
/// </summary>
private bool IsBlockedByEnvironment(Vector2 targetWorldPos)
{
    if (bodyCollider == null)
        return false;

    Vector2 size = bodyCollider.bounds.size * 0.9f;

    Collider2D hit = Physics2D.OverlapBox(
        targetWorldPos,
        size,
        0f,
        environmentLayer
    );

    return hit != null;
}


    private IEnumerator TeleportStep(Vector2 dir)
    {
        _isTeleporting = true;
        _hasQueuedMoveDir = false;
        _queuedMoveTimestamp = 0f;

        Vector2Int step = new Vector2Int(
            dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0),
            dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0)
        );

        Vector2Int currentCell = WorldToCell(transform.position);
        Vector2Int nextCell = currentCell + step;
        Vector2 targetPos = CellToWorld(nextCell);

        // 1) коллизия с другими игроками
        if (IsBlockedByOtherPlayerCombat(targetPos))
        {
            _isTeleporting = false;
            _moveRoutine = null;
            yield break;
        }

        // 1b) коллизия с окружением (деревья, камни)
if (IsBlockedByEnvironment(targetPos)) 
{
    _isTeleporting = false;
    _moveRoutine = null;
    yield break;
}


        // 2) препятствия по гриду
        bool canMove = occupancyManager == null || occupancyManager.TryMove(currentCell, nextCell);
        if (!canMove)
        {
            transform.position = CellToWorld(currentCell);
            _isTeleporting = false;
            _moveRoutine = null;
            yield break;
        }

        float baseMove = moveDurationBase();
        float stepLength = step.magnitude; // 1 или sqrt(2)
        float moveTime = (baseMove * stepLength) / Mathf.Max(combatSpeedMultiplier, 0.0001f);

        float t = 0f;
        Vector2 startPos = transform.position;

        while (t < moveTime)
        {
            float u = t / moveTime;
            transform.position = Vector2.Lerp(startPos, targetPos, u);
            t += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;

        float delay = ComputeDelay(dir);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _isTeleporting = false;
        _moveRoutine = null;

       bool queuedIsFresh = _hasQueuedMoveDir && (Time.time - _queuedMoveTimestamp) <= inputBufferSeconds;


        if (_combatActive && !_isBlocking && queuedIsFresh && _queuedMoveDir != Vector2.zero)
        {
            Vector2 nextDir = _queuedMoveDir;
            _queuedMoveDir = Vector2.zero;
            _hasQueuedMoveDir = false;
            _queuedMoveTimestamp = 0f;

            _moveRoutine = StartCoroutine(TeleportStep(nextDir));
        }
    }

    private float ComputeDelay(Vector2 moveDir)
    {
        if (arrowController == null || maxDelaySeconds <= 0f)
            return 0f;

        float moveAngle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
        float diff = Mathf.Abs(Mathf.DeltaAngle(arrowController.Angle, moveAngle)); // 0..180

        float t = Mathf.InverseLerp(0f, 180f, diff);

        float coeff;
        if (missCurve != null && missCurve.keys != null && missCurve.keys.Length > 0)
            coeff = Mathf.Clamp01(missCurve.Evaluate(t));
        else
            coeff = t;

        bool isDiagonalMove = Mathf.Abs(moveDir.x) > 0 && Mathf.Abs(moveDir.y) > 0;

        if (isDiagonalMove && diff > 5f && coeff < diagonalMissPenalty)
            coeff = Mathf.Max(coeff, diagonalMissPenalty);

        return maxDelaySeconds * coeff;
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

    private float moveDurationBase()
    {
        return playerController != null ? playerController.moveDuration : 0.2f;
    }
}
