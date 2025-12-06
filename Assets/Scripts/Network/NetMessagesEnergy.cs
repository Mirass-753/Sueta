using System;

[Serializable]
public class NetMessageEnergyRequest : NetMessageBase
{
    public string targetId;
    public float amount;

    public NetMessageEnergyRequest()
    {
        type = "energy_request";
    }
}

[Serializable]
public class NetMessageEnergyUpdate : NetMessageBase
{
    public string targetId;
    public float energy;
    public float maxEnergy;

    public NetMessageEnergyUpdate()
    {
        type = "energy_update";
    }
}

[Serializable]
public class NetMessageEnergySync : NetMessageBase
{
    [Serializable]
    public class EntityEnergy
    {
        public string id;
        public float energy;
        public float maxEnergy;
    }

    public EntityEnergy[] entities;

    public NetMessageEnergySync()
    {
        type = "energy_sync";
    }
}
