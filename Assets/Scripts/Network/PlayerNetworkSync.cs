using UnityEngine;

public class PlayerNetworkSync : MonoBehaviour
{
    [SerializeField] private float sendInterval = 0.1f; // 10 раз в секунду
    [SerializeField] private float heartbeatInterval = 5f; // 0 — отключить heartbeat
    [SerializeField] private float aimAngleSendThresholdDeg = 1.5f; // Минимальное изменение угла для отправки
    [SerializeField] private float minAimSendInterval = 0.05f; // Минимальный интервал между отправками угла

    private float _timeSinceLastSend;
    private float _timeSinceLastHeartbeat;
    private float _timeSinceLastAimSend;
    private Vector2 _lastPosition;
    private CombatModeController _combatController;
    private ArrowController _arrowController;
    private NetMessageMove _lastSentMove;

    private void Start()
    {
        if (this == null || transform == null)
            return;
            
        try
        {
            _lastPosition = transform.position;
            
            // Находим CombatModeController или ArrowController для получения угла стрелки
            _combatController = GetComponent<CombatModeController>();
            if (_combatController == null)
                _combatController = GetComponentInChildren<CombatModeController>();
            
            if (_combatController != null && _combatController.arrowController != null)
            {
                _arrowController = _combatController.arrowController;
            }
            else
            {
                // Если не нашли через CombatModeController, ищем напрямую
                _arrowController = GetComponent<ArrowController>();
                if (_arrowController == null)
                    _arrowController = GetComponentInChildren<ArrowController>();
            }
        }
        catch (System.Exception)
        {
            // Игнорируем ошибки инициализации в редакторе
            _combatController = null;
            _arrowController = null;
        }
    }

    private void Update()
    {
        _timeSinceLastSend += Time.deltaTime;
        _timeSinceLastHeartbeat += Time.deltaTime;
        _timeSinceLastAimSend += Time.deltaTime;
        if (_timeSinceLastSend < sendInterval)
        {
            return;
        }

        _timeSinceLastSend = 0f;

        if (WebSocketClient.Instance == null)
        {
            return;
        }

        // Проверяем, что LocalPlayerId инициализирован
        if (string.IsNullOrEmpty(PlayerController.LocalPlayerId))
        {
            return;
        }

        Vector2 pos = transform.position;
        Vector2 delta = pos - _lastPosition;
        _lastPosition = pos;

        bool moving = delta.sqrMagnitude > 0.0001f;
        Vector2 dir = moving ? delta.normalized : Vector2.zero;

        // Получаем угол стрелки и состояние боевого режима
        float aimAngle = 0f;
        bool inCombat = false;
        
        try
        {
            // Если компоненты не найдены, попробуем найти их снова (на случай, если они добавились позже)
            if (_combatController == null && _arrowController == null)
            {
                _combatController = GetComponent<CombatModeController>();
                if (_combatController == null)
                    _combatController = GetComponentInChildren<CombatModeController>();
                
                if (_combatController != null && _combatController.arrowController != null)
                {
                    _arrowController = _combatController.arrowController;
                }
                else
                {
                    _arrowController = GetComponent<ArrowController>();
                    if (_arrowController == null)
                        _arrowController = GetComponentInChildren<ArrowController>();
                }
            }

            if (_combatController != null)
            {
                inCombat = _combatController.IsCombatActive;
                if (inCombat && _arrowController != null && _arrowController.gameObject != null && _arrowController.gameObject.activeSelf)
                {
                    aimAngle = _arrowController.Angle;
                }
            }
            else if (_arrowController != null && _arrowController.gameObject != null && _arrowController.gameObject.activeSelf)
            {
                // Если нет CombatModeController, используем активность стрелки как индикатор
                aimAngle = _arrowController.Angle;
                inCombat = true;
            }
        }
        catch (System.Exception)
        {
            // Игнорируем ошибки доступа к компонентам (может происходить в редакторе)
            inCombat = false;
            aimAngle = 0f;
        }

        var msg = new NetMessageMove
        {
            type  = "move",
            id    = PlayerController.LocalPlayerId,
            x     = pos.x,
            y     = pos.y,
            dirX  = dir.x,
            dirY  = dir.y,
            moving = moving,
            aimAngle = aimAngle,
            inCombat = inCombat
        };

        bool stateChanged = HasStateChanged(msg, out bool aimShouldSend);
        bool heartbeatDue = heartbeatInterval > 0f && _timeSinceLastHeartbeat >= heartbeatInterval;

        if (!stateChanged && !heartbeatDue)
        {
            return;
        }

        string json = JsonUtility.ToJson(msg);
        WebSocketClient.Instance.Send(json);

        _lastSentMove = msg;
        if (stateChanged)
        {
            _timeSinceLastHeartbeat = 0f;
            if (aimShouldSend)
                _timeSinceLastAimSend = 0f;
        }
        else if (heartbeatDue)
        {
            _timeSinceLastHeartbeat = 0f;
        }

        // Отладочное логирование (можно убрать после проверки)
        if (Application.isPlaying && inCombat && stateChanged)
        {
            Debug.Log($"[NET-SYNC] Sending move: inCombat={inCombat}, aimAngle={aimAngle:F1}°, arrowFound={_arrowController != null}");
        }
    }

    private bool HasStateChanged(NetMessageMove msg, out bool aimShouldSend)
    {
        if (_lastSentMove == null)
        {
            aimShouldSend = true;
            return true;
        }

        bool positionChanged = Vector2.SqrMagnitude(new Vector2(msg.x, msg.y) - new Vector2(_lastSentMove.x, _lastSentMove.y)) > 0.0001f;
        bool dirChanged = Vector2.SqrMagnitude(new Vector2(msg.dirX, msg.dirY) - new Vector2(_lastSentMove.dirX, _lastSentMove.dirY)) > 0.0001f;
        bool movingChanged = msg.moving != _lastSentMove.moving;
        bool combatChanged = msg.inCombat != _lastSentMove.inCombat;

        float aimDelta = Mathf.Abs(Mathf.DeltaAngle(msg.aimAngle, _lastSentMove.aimAngle));
        bool aimChangedEnough = aimDelta >= aimAngleSendThresholdDeg;
        bool aimIntervalReached = _timeSinceLastAimSend >= minAimSendInterval;
        aimShouldSend = aimChangedEnough && aimIntervalReached;

        return positionChanged || dirChanged || movingChanged || aimShouldSend || combatChanged;
    }
}
