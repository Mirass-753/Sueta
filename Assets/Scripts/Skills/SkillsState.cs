using System;
using System.Collections.Generic;

public struct SkillSnapshot
{
    public string skillId;
    public string skillName;
    public int level;
    public int maxLevel;
    public float exp;
    public float expToLevel;
}

public static class SkillsState
{
    static readonly Dictionary<string, SkillSnapshot> skills = new Dictionary<string, SkillSnapshot>();

    public static event Action<SkillSnapshot> SkillUpdated;

    public static void ApplySync(NetMessageSkillSync msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.skillId))
            return;

        var snapshot = new SkillSnapshot
        {
            skillId = msg.skillId,
            skillName = msg.skillName,
            level = msg.level,
            maxLevel = msg.maxLevel,
            exp = msg.exp,
            expToLevel = msg.expToLevel
        };

        skills[msg.skillId] = snapshot;
        SkillUpdated?.Invoke(snapshot);
    }

    public static bool TryGetSkill(string skillId, out SkillSnapshot snapshot)
    {
        return skills.TryGetValue(skillId, out snapshot);
    }
}
