using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;

public partial class PlayerController : MonoBehaviour
{
    // ====== СТАТИКА / СЕТЬ ======

    // То, что реально используется в коде (как и было)
    public static string LocalPlayerId { get; private set; }

    // заглушка, если где-то в старом коде ещё спрашивают IsOwner
    public bool IsOwner => true;

    private string myId;
    private float syncTimer = 0f;
    private const float sendRate = 0.1f; // 10 раз в секунду
    private Vector2 lastSentPos;

    // Красивый вывод сетевых штук в инспекторе
    [TitleGroup("Network", Alignment = TitleAlignments.Centered)]
    [ShowInInspector, ReadOnly, LabelText("Local Player Id")]
    private string LocalPlayerIdDebug => LocalPlayerId;

    [Header("Combat / Arrow")]
    [SerializeField] private ArrowController combatArrow;


    [TitleGroup("Network")]
    [ShowInInspector, ReadOnly, LabelText("My Id (instance)")]
    private string MyIdDebug => myId;

    [TitleGroup("Network")]
    [ShowInInspector, ReadOnly, LabelText("Is Owner")]
    private bool IsOwnerDebug => IsOwner;

    // ====== ПАРАМЕТРЫ ДВИЖЕНИЯ ======

    [TitleGroup("Movement/Config")]
    [LabelText("Grid Size")]
    [MinValue(0.1f)]
    public float gridSize = 1f;

    [TitleGroup("Movement/Config")]
    [LabelText("Move Duration")]
    [MinValue(0.01f)]
    public float moveDuration = 0.2f;

    [TitleGroup("Movement/Config")]
    [LabelText("Continuous Move Delay")]
    [MinValue(0f)]
    public float continuousMoveDelay = 0.1f;

    [TitleGroup("Movement/Sprites")]
    [LabelText("Idle Sprite")]
    public Sprite idleSprite;

    [TitleGroup("Movement/Sprites")]
    [LabelText("Moving Sprite")]
    public Sprite movingSprite;

    [TitleGroup("Movement/Grid")]
    [SerializeField, LabelText("Cell Center Offset")]
    private Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);

    [TitleGroup("Movement/Grid")]
    [LabelText("Occupancy Manager")]
    
    public GridOccupancyManager occupancyManager;

    // ====== КОЛЛИЗИЯ ======

    [TitleGroup("Collision")]
    [SerializeField, LabelText("Body Collider")]
    private BoxCollider2D bodyCollider;   // коллайдер тела игрока

    [TitleGroup("Collision")]
    [SerializeField, LabelText("Player Layer")]
    private LayerMask playerLayer;        // слой, на котором висят игроки

    [TitleGroup("Collision")]
    [SerializeField, LabelText("Environment Layer")]
    private LayerMask environmentLayer;   // слой окружения (деревья, стены и т.п.)

    // ====== РАНТАЙМ-СОСТОЯНИЕ (только просмотр) ======

    [FoldoutGroup("Runtime State"), ShowInInspector, ReadOnly, LabelText("Target Position")]
    private Vector2 TargetPosition => _targetPosition;

    [FoldoutGroup("Runtime State"), ShowInInspector, ReadOnly, LabelText("Is Moving")]
    private bool IsMoving => _isMoving;

    [FoldoutGroup("Runtime State"), ShowInInspector, ReadOnly, LabelText("Wants To Run")]
    private bool WantsToRun => _wantsToRun;

    [FoldoutGroup("Runtime State"), ShowInInspector, ReadOnly, LabelText("Last Direction")]
    private Vector2 LastDirection => _lastDirection;

    [FoldoutGroup("Runtime State"), ShowInInspector, ReadOnly, LabelText("Current Cell")]
    private Vector2Int CurrentCell => _currentCell;

    [FoldoutGroup("Runtime State"), ShowInInspector, ReadOnly, LabelText("Moving By Click")]
    private bool MovingByClick => _movingByClick;

    // ====== РЕАЛЬНЫЕ ПОЛЯ (как были, логика не меняется) ======

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
