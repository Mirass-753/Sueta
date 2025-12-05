using UnityEngine;

public partial class PlayerController : MonoBehaviour
{
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
