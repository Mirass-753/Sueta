using UnityEngine;
using UnityEngine.UI;

public class SkillProgressUI : MonoBehaviour
{
    [SerializeField] Text skillNameText;
    [SerializeField] Text levelText;
    [SerializeField] Image progressFill;

    public void SetSkillName(string skillName)
    {
        if (skillNameText != null)
            skillNameText.text = skillName;
    }

    public void SetProgress(int level, int maxLevel, float progress01)
    {
        if (levelText != null)
            levelText.text = level.ToString();

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(progress01);
    }
}
