using UnityEngine;

public class SkillsPanelUI : MonoBehaviour
{
    [SerializeField] float padding = 8f;
    [SerializeField] float spacing = 6f;

    void OnEnable()
    {
        Layout();
    }

    void OnTransformChildrenChanged()
    {
        Layout();
    }

    public void Layout()
    {
        var rect = transform as RectTransform;
        if (rect == null)
            return;

        float y = padding;
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf)
                continue;

            var childRect = child as RectTransform;
            if (childRect == null)
                continue;

            childRect.anchorMin = new Vector2(0f, 0f);
            childRect.anchorMax = new Vector2(0f, 0f);
            childRect.pivot = new Vector2(0f, 0f);
            childRect.anchoredPosition = new Vector2(padding, y);
            y += childRect.sizeDelta.y + spacing;
        }

        if (y > padding)
            y -= spacing;

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + padding);
    }
}
