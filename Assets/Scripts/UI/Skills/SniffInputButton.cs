using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SniffInputButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] ScentHuntController huntController;
    [SerializeField] Text label;
    [SerializeField] string labelText = "Нюх (1)";

    void Awake()
    {
        if (label != null)
            label.text = labelText;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (huntController == null)
            huntController = FindObjectOfType<ScentHuntController>();

        huntController?.RequestSniff();
    }
}
