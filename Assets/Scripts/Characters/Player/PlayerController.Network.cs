using UnityEngine;

public partial class PlayerController : MonoBehaviour
{
    // ========= ОТПРАВКА В СЕТЬ =========

    private void SendPosition()
{
    if (WebSocketClient.Instance == null)
        return;

    Vector2 pos = transform.position;

    if (Vector2.Distance(pos, lastSentPos) < 0.01f)
        return;

    lastSentPos = pos;

    // угол стрелки (если боевой режим включён и ссылка есть)
    float aimAngle = 0f;
    if (combatArrow != null)
        aimAngle = combatArrow.Angle;

    NetMessageMove msg = new NetMessageMove
    {
        type     = "move",
        id       = myId,
        x        = pos.x,
        y        = pos.y,
        dirX     = _lastDirection.x,
        dirY     = _lastDirection.y,
        moving   = _isMoving,
        aimAngle = aimAngle          // НОВОЕ ПОЛЕ
    };

    string json = JsonUtility.ToJson(msg);
    WebSocketClient.Instance.Send(json);
}

}
