using UnityEngine;

public class PlayerNetworkSync : MonoBehaviour
{
    [SerializeField] private float sendInterval = 0.1f; // 10 раз в секунду

    private float _timeSinceLastSend;
    private Vector2 _lastPosition;

    private void Start()
    {
        _lastPosition = transform.position;
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

        Vector2 pos = transform.position;
        Vector2 delta = pos - _lastPosition;
        _lastPosition = pos;

        bool moving = delta.sqrMagnitude > 0.0001f;
        Vector2 dir = moving ? delta.normalized : Vector2.zero;

        var msg = new NetMessageMove
        {
            type  = "move",
            id    = PlayerController.LocalPlayerId,
            x     = pos.x,
            y     = pos.y,
            dirX  = dir.x,
            dirY  = dir.y,
            moving = moving
        };

        string json = JsonUtility.ToJson(msg);
        WebSocketClient.Instance.Send(json);
    }
}
