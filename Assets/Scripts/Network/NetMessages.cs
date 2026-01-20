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
    public bool inCombat; // находится ли игрок в боевом режиме
}

/// Общий чат.
[Serializable]
public class NetMessageChat : NetMessageBase
{
    public string id;
    public string text;

    public NetMessageChat()
    {
        type = "chat";
    }
}

/// Клиент → сервер: запрос нанести урон.
