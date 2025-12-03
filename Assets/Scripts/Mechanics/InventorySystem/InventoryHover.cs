using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject rightPanel;

    void Awake()
    {
        if (rightPanel != null) rightPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (rightPanel != null) rightPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (rightPanel != null) rightPanel.SetActive(false);
    }
}
