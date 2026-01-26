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

    public static void AddLocalExp(string skillId, string skillName, float expAmount, float expToLevel = 100f, int maxLevel = 10, int startLevel = 1)
    {
        if (string.IsNullOrEmpty(skillId))
            return;

        if (!skills.TryGetValue(skillId, out var snapshot))
        {
            int clampedStartLevel = Math.Max(1, startLevel);
            int clampedMaxLevel = Math.Max(clampedStartLevel, maxLevel);
            float clampedExpToLevel = Math.Max(1f, expToLevel);

            snapshot = new SkillSnapshot
            {
                skillId = skillId,
                skillName = string.IsNullOrEmpty(skillName) ? skillId : skillName,
                level = clampedStartLevel,
                maxLevel = clampedMaxLevel,
                exp = 0f,
                expToLevel = clampedExpToLevel
            };
        }

        if (!string.IsNullOrEmpty(skillName))
            snapshot.skillName = skillName;

        snapshot.expToLevel = Math.Max(1f, snapshot.expToLevel);
        snapshot.maxLevel = Math.Max(snapshot.level, snapshot.maxLevel);

        if (snapshot.level >= snapshot.maxLevel)
        {
            snapshot.exp = snapshot.expToLevel;
        }
        else
        {
            snapshot.exp += expAmount;

            while (snapshot.exp >= snapshot.expToLevel && snapshot.level < snapshot.maxLevel)
            {
                snapshot.exp -= snapshot.expToLevel;
                snapshot.level += 1;
            }

            if (snapshot.level >= snapshot.maxLevel)
                snapshot.exp = snapshot.expToLevel;
        }

        skills[skillId] = snapshot;
        SkillUpdated?.Invoke(snapshot);
    }
}
