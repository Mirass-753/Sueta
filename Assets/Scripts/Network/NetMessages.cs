using System;
using UnityEngine;

/// Базовое сообщение, чтобы быстро узнать type.
[Serializable]
public class NetMessageTypeOnly
{
    public string type;
}

/// Позиция/движение игрока.
[Serializable]
public class NetMessageMove
{
    public string type;   // "move"
    public string id;
    public float x;
    public float y;
    public float dirX;
    public float dirY;
    public bool moving;
    public float aimAngle;
}

/// Клиент → сервер: запрос нанести урон.
