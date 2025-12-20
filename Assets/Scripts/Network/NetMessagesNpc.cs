using System;

[Serializable]
public class NetMessageNpcSpawn : NetMessageBase
{
    public string npcId;
    public float x;
    public float y;
    public float hp;
}

[Serializable]
public class NetMessageNpcState : NetMessageBase
{
    public string npcId;
    public float x;
    public float y;
    public float hp;
}

[Serializable]
public class NetMessageNpcDespawn : NetMessageBase
{
    public string npcId;
}
