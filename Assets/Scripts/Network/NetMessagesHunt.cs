using System;

[Serializable]
public class NetMessagePreySpawn : NetMessageBase
{
    public string preyId;
    public float x;
    public float y;
    public string ownerId;
    public string dropItemName;

    public NetMessagePreySpawn()
    {
        type = "prey_spawn";
    }
}

[Serializable]
public class NetMessagePreyPosition : NetMessageBase
{
    public string id;
    public float x;
    public float y;

    public NetMessagePreyPosition()
    {
        type = "prey_pos";
    }
}

[Serializable]
public class NetMessagePreyKill : NetMessageBase
{
    public string id;
    public string killerId;

    public NetMessagePreyKill()
    {
        type = "prey_kill";
    }
}
