using System;

[Serializable]
public class NetMessagePlayerAttackRequest : NetMessageBase
{
    public string sourceId;
    public float dirX;
    public float dirY;
    public string weapon;

    public NetMessagePlayerAttackRequest()
    {
        type = "player_attack_request";
    }
}

[Serializable]
public class NetMessageAttackStart : NetMessageBase
{
    public string attackId;
    public string sourceId;
    public string targetId;
    public float dirX;
    public float dirY;
    public string weapon;

    public NetMessageAttackStart()
    {
        type = "attack_start";
    }
}

[Serializable]
public class NetMessageAttackHitReport : NetMessageBase
{
    public string attackId;
    public string sourceId;
    public string targetId;
    public string hitPart;
    public float x;
    public float y;
    public float z;

    public NetMessageAttackHitReport()
    {
        type = "attack_hit_report";
    }
}
