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

[Serializable]
public class NetMessageSniffRequest : NetMessageBase
{
    public string playerId;

    public NetMessageSniffRequest()
    {
        type = "sniff_request";
    }
}

[Serializable]
public class NetMessageSkillSync : NetMessageBase
{
    public string playerId;
    public string skillId;
    public string skillName;
    public int level;
    public int maxLevel;
    public float exp;
    public float expToLevel;

    public NetMessageSkillSync()
    {
        type = "skill_sync";
    }
}
