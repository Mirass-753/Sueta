using System;
using UnityEngine;

[Serializable]
public class NetMessageBase
{
    public string type;
}

#region DAMAGE REQUEST  (когда клиент просит сервер нанести урон)

[Serializable]
public class NetMessageDamageRequest : NetMessageBase
{
    public string sourceId;  // кто бил (может быть null)
    public string targetId;  // по кому попали
    public float amount;     // "сырое" количество урона после блоков/зон
    public string zone;      // зона попадания (может быть пустой строкой)

    public NetMessageDamageRequest()
    {
        type = "damage_request";
    }
}

#endregion

#region DAMAGE EVENT (ответ сервера: урон принят)

[Serializable]
public class NetMessageDamageEvent : NetMessageBase
{
    public string sourceId;
    public string targetId;
    public float amount; // урон, который сервер принял
    public float hp;     // новое HP цели по версии сервера

    public NetMessageDamageEvent()
    {
        type = "damage";
    }
}

#endregion

#region HP SYNC (сервер шлёт снимок HP всех сущностей — можно использовать при входе)

[Serializable]
public class NetMessageHpSync : NetMessageBase
{
    [Serializable]
    public class EntityHp
    {
        public string id;
        public float hp;
    }

    public EntityHp[] entities;

    public NetMessageHpSync()
    {
        type = "hp_sync";
    }
}

#endregion
