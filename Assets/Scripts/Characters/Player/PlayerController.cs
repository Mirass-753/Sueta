using System;
using UnityEngine;

public partial class PlayerController : MonoBehaviour
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
}
