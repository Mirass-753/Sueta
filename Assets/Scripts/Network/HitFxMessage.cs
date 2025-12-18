using System;

[Serializable]
public class HitFxMessage
{
    public string type;     // "hit_fx"
    public string fx;       // "claws"
    public string targetId; // кого ударили
    public string zone;     // зона попадания
    public float x;
    public float y;
    public float z;
}
