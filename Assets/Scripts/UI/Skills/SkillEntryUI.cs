using UnityEngine;

public class SkillEntryUI : MonoBehaviour
{
    [SerializeField] string skillId = "sniff";
    [SerializeField] SkillProgressUI progressUI;

    void OnEnable()
    {
        SkillsState.SkillUpdated += HandleSkillUpdated;

        if (SkillsState.TryGetSkill(skillId, out var snapshot))
            ApplySnapshot(snapshot);
    }

    void OnDisable()
    {
        SkillsState.SkillUpdated -= HandleSkillUpdated;
    }

    void HandleSkillUpdated(SkillSnapshot snapshot)
    {
        if (snapshot.skillId != skillId)
            return;

        ApplySnapshot(snapshot);
    }

    void ApplySnapshot(SkillSnapshot snapshot)
    {
        if (progressUI == null)
            return;

        progressUI.SetSkillName(snapshot.skillName);

        float denom = Mathf.Max(0.01f, snapshot.expToLevel);
        float progress = snapshot.level >= snapshot.maxLevel
            ? 1f
            : Mathf.Clamp01(snapshot.exp / denom);
        progressUI.SetProgress(snapshot.level, snapshot.maxLevel, progress);
    }
}
