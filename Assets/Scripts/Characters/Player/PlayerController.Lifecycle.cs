using System;
using UnityEngine;

public partial class PlayerController : MonoBehaviour
{
    // ========= ЖИЗНЕННЫЙ ЦИКЛ =========

    private void Awake()
    {
        EnsureCollisionSetup();

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

    private void OnDisable()
    {
        occupancyManager?.Unregister(_currentCell);
    }

    private void OnValidate()
    {
        EnsureCollisionSetup();
    }

    private void EnsureCollisionSetup()
    {
        if (bodyCollider == null)
            bodyCollider = GetComponent<BoxCollider2D>();

        if (playerLayer == 0)
            playerLayer = LayerMask.GetMask("Player");

        if (environmentLayer == 0)
            environmentLayer = LayerMask.GetMask("Environment");
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
}
