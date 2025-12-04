using System;

[Serializable]
public class NetMessageBase
{
    public string type;
}

[Serializable]
public class NetMessageDamageRequest
{
    public string type = "damage_request";
    public string sourceId;  // кто бил (может быть null)
    public string targetId;  // по кому попали
    public float amount;     // "сырое" количество урона после блоков/зон
    public string zone;      // зона попадания (может быть пустой строкой)
}

[Serializable]
public class NetMessageDamageEvent
{
    public string type = "damage";
    public string sourceId;
    public string targetId;
    public float amount;     // урон, который сервер принял
    public float hp;         // новое HP цели
}

[Serializable]
public class NetMessageHpSync
{
    [Serializable]
    public class EntityHp
    {
        public string id;
        public float hp;
    }

    public string type = "hp_sync";
    public EntityHp[] entities;
}