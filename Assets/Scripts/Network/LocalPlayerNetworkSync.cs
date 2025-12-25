using UnityEngine;

public class LocalPlayerNetworkSync : MonoBehaviour
{
    [SerializeField] private float sendInterval = 0.05f;

    private Damageable _damageable;
    private CombatModeController _combatController;
    private ArrowController _arrowController;
    private Vector2 _lastPosition;
    private float _timeSinceLastSend;
    private bool _sentInitialMove;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
        _combatController = GetComponent<CombatModeController>() ?? GetComponentInChildren<CombatModeController>();
        _arrowController = _combatController != null
            ? _combatController.arrowController
            : GetComponent<ArrowController>() ?? GetComponentInChildren<ArrowController>();
    }

    private void OnEnable()
    {
        _lastPosition = transform.position;
        var ws = WebSocketClient.Instance;
        if (ws != null)
            ws.OnConnected += HandleConnected;
    }

    private void OnDisable()
    {
        var ws = WebSocketClient.Instance;
        if (ws != null)
            ws.OnConnected -= HandleConnected;
    }

    private void Update()
    {
        _timeSinceLastSend += Time.deltaTime;

        if (!_sentInitialMove)
        {
            TrySendMove(true);
            return;
        }

        if (_timeSinceLastSend < sendInterval)
            return;

        TrySendMove(false);
    }

    private void HandleConnected()
    {
        _sentInitialMove = false;
        TrySendMove(true);
    }

    private void TrySendMove(bool force)
    {
        var ws = WebSocketClient.Instance;
        if (ws == null || !ws.IsOpen)
            return;

        string networkId = ResolveNetworkId();
        if (string.IsNullOrEmpty(networkId))
            return;

        Vector2 pos = transform.position;
        Vector2 delta = pos - _lastPosition;
        bool moving = delta.sqrMagnitude > 0.0001f;
        Vector2 dir = moving ? delta.normalized : Vector2.zero;
        _lastPosition = pos;

        float aimAngle = 0f;
        bool inCombat = false;
        if (_combatController != null)
        {
            inCombat = _combatController.IsCombatActive;
            if (inCombat && _arrowController != null && _arrowController.gameObject.activeSelf)
                aimAngle = _arrowController.Angle;
        }
        else if (_arrowController != null && _arrowController.gameObject.activeSelf)
        {
            inCombat = true;
            aimAngle = _arrowController.Angle;
        }

        var msg = new NetMessageMove
        {
            type = "move",
            id = networkId,
            x = pos.x,
            y = pos.y,
            dirX = dir.x,
            dirY = dir.y,
            moving = moving,
            aimAngle = aimAngle,
            inCombat = inCombat
        };

        string json = JsonUtility.ToJson(msg);
        ws.Send(json);

        _timeSinceLastSend = 0f;
        _sentInitialMove = true;
    }

    private string ResolveNetworkId()
    {
        if (_damageable != null && !string.IsNullOrEmpty(_damageable.networkId))
            return _damageable.networkId;

        if (!string.IsNullOrEmpty(PlayerController.LocalPlayerId))
            return PlayerController.LocalPlayerId;

        return null;
    }
}
