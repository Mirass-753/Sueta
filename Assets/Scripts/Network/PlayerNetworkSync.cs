using UnityEngine;

public class PlayerNetworkSync : MonoBehaviour
{
    [SerializeField] private float sendInterval = 0.1f; // 10 раз в секунду

    private float _timeSinceLastSend;
    private Vector2 _lastPosition;
    private CombatModeController _combatController;
    private ArrowController _arrowController;

    private void Start()
    {
        if (transform == null)
            return;
            
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

    private void Update()
    {
        _timeSinceLastSend += Time.deltaTime;
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

        string json = JsonUtility.ToJson(msg);
        WebSocketClient.Instance.Send(json);
    }
}
