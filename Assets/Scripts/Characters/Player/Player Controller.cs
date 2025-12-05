using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerController : MonoBehaviour
{
    // ====== СТАТИКА / СЕТЬ ======
    public static string LocalPlayerId { get; private set; }

    // заглушка, если где-то в старом коде ещё спрашивают IsOwner
    public bool IsOwner => true;

    private string myId;
    private float syncTimer = 0f;
    private const float sendRate = 0.1f; // 10 раз в секунду
    private Vector2 lastSentPos;

    // ====== ПАРАМЕТРЫ ДВИЖЕНИЯ ======
    [Header("Movement Settings")]
    public float gridSize = 1f;
    public float moveDuration = 0.2f;
    public float continuousMoveDelay = 0.1f;

    [Header("Sprite Settings")]
    public Sprite idleSprite;
    public Sprite movingSprite;

    [Header("Grid Settings")]
    [SerializeField] private Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);

    [Header("Occupancy (локально)")]
    public GridOccupancyManager occupancyManager;

    [Header("Collision")]
    [SerializeField] private BoxCollider2D bodyCollider;   // коллайдер тела игрока
    [SerializeField] private LayerMask playerLayer;        // слой, на котором висят игроки

    private Vector2 _targetPosition;
    private bool _isMoving = false;
    private bool _wantsToRun = false;
    private Vector2 _lastDirection;
    private bool _keyHeld = false;
    private float _holdTimer = 0f;

    private SpriteRenderer _spriteRenderer;
    private StaminaSystem _staminaSystem;
    private Camera _camera;

    private Vector2Int _currentCell;
    private bool _movingByClick = false;
    private Vector2Int _clickTargetCell;

    // ========= ЖИЗНЕННЫЙ ЦИКЛ =========

    private void Start()
    {
#if UNITY_2023_1_OR_NEWER
        if (occupancyManager == null)
            occupancyManager = UnityEngine.Object.FindFirstObjectByType<GridOccupancyManager>();
#else
        if (occupancyManager == null)
            occupancyManager = UnityEngine.Object.FindObjectOfType<GridOccupancyManager>();
#endif

        _spriteRenderer = GetComponent<SpriteRenderer>();
        _staminaSystem = GetComponent<StaminaSystem>();
        _camera = Camera.main;

        Vector2 startPos = transform.position;
        _currentCell = WorldToCell(startPos);
        _targetPosition = CellToWorld(_currentCell);
        transform.position = _targetPosition;

        occupancyManager?.Register(_currentCell);

        if (_spriteRenderer != null && idleSprite != null)
            SetIdleSprite();

        // камера
        var camCtrl = _camera != null ? _camera.GetComponent<CameraController>() : null;
        if (camCtrl != null) camCtrl.target = transform;

        // UI инвентаря
        var ui = FindObjectOfType<InventoryUI>();
        if (ui != null)
            ui.SetPlayerInventory(GetComponent<PlayerInventory>());
    }

    private void Awake()
    {
        // генерируем id игрока на сессию
        myId = Guid.NewGuid().ToString();
        LocalPlayerId = myId;

        // привязываем Damageable к этому id,
        // чтобы сервер мог идентифицировать этого кота
        var dmg = GetComponent<Damageable>();
        if (dmg != null)
        {
            dmg.SetNetworkIdentity(myId, true);
            Debug.Log($"[PLAYER] Set Damageable networkId = {myId}");
        }
        else
        {
            Debug.LogWarning("[PLAYER] No Damageable on Player, network damage won't work");
        }
    }

    private void OnDisable()
    {
        occupancyManager?.Unregister(_currentCell);
    }

    private void Update()
    {
        // --- движение ---
        _wantsToRun = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                      && _staminaSystem != null && _staminaSystem.CanRun;

        HandleMouseClickTarget();

        Vector2 keyboardDir = GetKeyboardDirection();

        if (keyboardDir != Vector2.zero)
        {
            _movingByClick = false;
            HandleKeyboardMovement(keyboardDir);
        }
        else if (_movingByClick)
        {
            HandleClickMovement();
        }
        else
        {
            _keyHeld = false;
            _holdTimer = 0f;
            _staminaSystem?.StopRunning();
        }

        // --- отправка позиции по таймеру ---
        syncTimer += Time.deltaTime;
        if (syncTimer >= sendRate)
        {
            syncTimer = 0f;
            SendPosition();
        }
    }

    // ========= СЕТКА / ВСПОМОГАТЕЛЬНОЕ =========

    public void SyncPositionToGrid()
    {
        _currentCell = WorldToCell(transform.position);
        _targetPosition = transform.position;
    }

    public void ResetMovementState()
    {
        StopAllCoroutines();
        _isMoving = false;
        _keyHeld = false;
        _holdTimer = 0f;
        _movingByClick = false;
        SetIdleSprite();
        _staminaSystem?.StopRunning();
        SyncPositionToGrid();
    }

    public void FaceByVector(Vector2 dir)
    {
        if (_spriteRenderer == null) return;
        if (dir.x > 0.01f) _spriteRenderer.flipX = true;
        else if (dir.x < -0.01f) _spriteRenderer.flipX = false;
    }

    private void SetIdleSprite()
    {
        if (_spriteRenderer != null && idleSprite != null)
            _spriteRenderer.sprite = idleSprite;
    }

    private void SetMovingSprite()
    {
        if (_spriteRenderer != null && movingSprite != null)
            _spriteRenderer.sprite = movingSprite;
    }

    /// <summary>
    /// Прерывает текущее движение — используем только для клика мышкой.
    /// Для клавы мы теперь НЕ рвём шаг, чтобы дойти до клетки.
    /// </summary>
    private void InterruptMovement()
    {
        if (!_isMoving) return;

        StopAllCoroutines();
        _isMoving = false;
        _staminaSystem?.StopRunning();

        // чтобы не было диагональных скачков — привязываем к ближайшей клетке
        SyncPositionToGrid();
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

    // ========= КОЛЛИЗИЯ С ДРУГИМИ ИГРОКАМИ =========

    /// <summary>
    /// Проверяем, стоит ли на целевой позиции другой игрок.
    /// </summary>
    private bool IsBlockedByOtherPlayer(Vector2 targetWorldPos)
    {
        if (bodyCollider == null)
            return false;

        // берём размеры коллайдера в мире, чуть уменьшаем, чтобы не было ложных срабатываний
        Vector2 size = bodyCollider.bounds.size * 0.9f;

        Collider2D hit = Physics2D.OverlapBox(
            targetWorldPos,
            size,
            0f,
            playerLayer
        );

        // hit может быть мы сами
        if (hit == null) return false;

        return hit.gameObject != gameObject;
    }

    // ========= КЛАВИАТУРА =========

    private Vector2 GetKeyboardDirection()
    {
        // Явные диагонали только через Q/E/Z/C
        if (Input.GetKey(KeyCode.Q)) return new Vector2(-1f, 1f);
        if (Input.GetKey(KeyCode.E)) return new Vector2(1f, 1f);
        if (Input.GetKey(KeyCode.Z)) return new Vector2(-1f, -1f);
        if (Input.GetKey(KeyCode.C)) return new Vector2(1f, -1f);

        float x = 0f;
        float y = 0f;

        // Горизонталь (A/D или стрелки влево/вправо)
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            x = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            x = 1f;

        // Вертикаль (W/S или стрелки вверх/вниз)
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            y = 1f;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            y = -1f;

        // Если зажаты и горизонталь, и вертикаль одновременно (например, W+D),
        // убираем горизонталь, чтобы не было диагонали от двух клавиш.
        // Приоритет ВЕРТИКАЛИ.
        if (x != 0f && y != 0f)
        {
            x = 0f;
        }

        if (x == 0f && y == 0f)
            return Vector2.zero;

        return new Vector2(x, y);
    }

    private void HandleKeyboardMovement(Vector2 inputDirection)
    {
        // не рвём шаг на клаве, даём дойти до клетки

        if (!_isMoving)
        {
            if (inputDirection != Vector2.zero)
            {
                FaceByVector(inputDirection);

                if (!_keyHeld)
                {
                    _lastDirection = inputDirection;
                    StartCoroutine(MoveToDirection(inputDirection));
                    _keyHeld = true;
                    _holdTimer = 0f;
                }
                else
                {
                    _holdTimer += Time.deltaTime;
                    if (_holdTimer >= continuousMoveDelay)
                    {
                        _lastDirection = inputDirection;
                        StartCoroutine(MoveToDirection(inputDirection));
                        _holdTimer = 0f;
                    }
                }
            }
            else
            {
                _keyHeld = false;
                _holdTimer = 0f;
                _staminaSystem?.StopRunning();
            }
        }
        else
        {
            // пока идём — просто запоминаем последнее направление,
            // чтобы после окончания шага продолжить уже в нём
            if (inputDirection != Vector2.zero)
                _lastDirection = inputDirection;
        }
    }

    // ========= МЫШЬ =========

    private void HandleMouseClickTarget()
    {
        if (_camera == null) return;

        if (EventSystem.current != null)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count > 0)
                return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            _clickTargetCell = WorldToCell(mouseWorld);
            _movingByClick = true;

            // новый клик — прерываем текущий шаг, чтобы кот сразу сменил цель
            InterruptMovement();
        }
    }

    private void HandleClickMovement()
    {
        if (_isMoving) return;

        _currentCell = WorldToCell(transform.position);

        if (_currentCell == _clickTargetCell)
        {
            _movingByClick = false;
            return;
        }

        int dx = _clickTargetCell.x - _currentCell.x;
        int dy = _clickTargetCell.y - _currentCell.y;

        Vector2 stepDir = Vector2.zero;
        if (dx > 0) stepDir.x = 1f; else if (dx < 0) stepDir.x = -1f;
        if (dy > 0) stepDir.y = 1f; else if (dy < 0) stepDir.y = -1f;

        if (stepDir == Vector2.zero)
        {
            _movingByClick = false;
            return;
        }

        FaceByVector(stepDir);
        _lastDirection = stepDir;
        StartCoroutine(MoveToDirection(stepDir));
    }

    // ========= ШАГ ПО СЕТКЕ =========

    private IEnumerator MoveToDirection(Vector2 direction)
    {
        _isMoving = true;

        SetMovingSprite();

        if (_wantsToRun && _staminaSystem != null)
            _staminaSystem.StartRunning();

        FaceByVector(direction);

        Vector2Int cellStep = new Vector2Int(
            direction.x > 0 ? 1 : (direction.x < 0 ? -1 : 0),
            direction.y > 0 ? 1 : (direction.y < 0 ? -1 : 0)
        );

        Vector2Int nextCell = _currentCell + cellStep;
        Vector2 targetPos = CellToWorld(nextCell);

        // 1) проверка на других игроков
        if (IsBlockedByOtherPlayer(targetPos))
        {
            Debug.Log("[MOVE] blocked by other player at " + targetPos);
            _isMoving = false;
            _staminaSystem?.StopRunning();
            SetIdleSprite();
            yield break;
        }

        // 2) проверка по гриду (стены, препятствия и т.п.)
        bool canMove = occupancyManager == null || occupancyManager.TryMove(_currentCell, nextCell);
        if (!canMove)
        {
            _isMoving = false;
            _staminaSystem?.StopRunning();
            SetIdleSprite();
            yield break;
        }

        Vector2 startPosition = transform.position;
        float elapsedTime = 0f;
        float baseMoveDuration = _wantsToRun ? moveDuration / 1.5f : moveDuration;
        float currentMoveDuration = baseMoveDuration * direction.magnitude;

        while (elapsedTime < currentMoveDuration)
        {
            float t = elapsedTime / currentMoveDuration;
            transform.position = Vector2.Lerp(startPosition, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        _currentCell = nextCell;

        // это последний шаг для клика мышкой?
        bool isFinalClickStep = _movingByClick && (_currentCell == _clickTargetCell);

        _isMoving = false;

        // если не держим клавишу, то:
        // - при обычном движении по клавиатуре всегда возвращаем idle;
        // - при движении по клику — только на последнем шаге.
        if (!_keyHeld && (!_movingByClick || isFinalClickStep))
        {
            SetIdleSprite();
        }

        _staminaSystem?.StopRunning();

        // автоповтор для зажатой клавиши
        if (_keyHeld && _lastDirection == direction)
        {
            yield return new WaitForSeconds(0.05f);
            if (!_isMoving && _keyHeld)
                StartCoroutine(MoveToDirection(direction));
        }
    }

    public void SetPosition(Vector2 newPosition)
    {
        occupancyManager?.Unregister(_currentCell);
        _currentCell = WorldToCell(newPosition);
        occupancyManager?.Register(_currentCell);
        _targetPosition = CellToWorld(_currentCell);
        transform.position = _targetPosition;
    }

    // ========= ОТПРАВКА В СЕТЬ =========

    private void SendPosition()
    {
        if (WebSocketClient.Instance == null)
            return;

        Vector2 pos = transform.position;

        // не слать, если почти не двигались
        if (Vector2.Distance(pos, lastSentPos) < 0.01f)
            return;

        lastSentPos = pos;

        NetMessageMove msg = new NetMessageMove
        {
            type   = "move",
            id     = myId,
            x      = pos.x,
            y      = pos.y,
            dirX   = _lastDirection.x,
            dirY   = _lastDirection.y,
            moving = _isMoving
        };

        string json = JsonUtility.ToJson(msg);
        WebSocketClient.Instance.Send(json);
    }
}
