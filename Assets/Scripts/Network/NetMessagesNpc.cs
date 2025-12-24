using System;

[Serializable]
public class NetMessageNpcSpawn : NetMessageBase
{
    public string npcId;
    public float x;
    public float y;
    public float hp;
    public string state;
    public string targetId;
    public float dirX;
    public float dirY;
    public bool moving;
}

[Serializable]
public class NetMessageNpcState : NetMessageBase
{
    public string npcId;
    public float x;
    public float y;
    public float hp;
    public string state;
    public string targetId;
    public float dirX;
    public float dirY;
    public bool moving;
}

[Serializable]
public class NetMessageNpcDespawn : NetMessageBase
{
    public string npcId;
}

[Serializable]
public class NetMessageNpcAttack : NetMessageBase
{
    public string npcId;
    public string targetId;
    public float dirX;
    public float dirY;
}
