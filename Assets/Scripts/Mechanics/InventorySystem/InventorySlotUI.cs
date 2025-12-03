using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    public Image iconImage;

    private int _slotIndex;
    private InventoryUI _inventoryUI;

    public void Initialize(int slotIndex, InventoryUI inventoryUI)
    {
        _slotIndex = slotIndex;
        _inventoryUI = inventoryUI;
        SetItem(null);
    }

    public void SetItem(Item item)
    {
        if (iconImage == null) return;

        if (item != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            _inventoryUI.UseSlot(_slotIndex);
        else if (eventData.button == PointerEventData.InputButton.Right)
            _inventoryUI.DropFromSlot(_slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var item = _inventoryUI.GetItemInSlot(_slotIndex);
        if (item == null) return;

        _inventoryUI.BeginDrag(_slotIndex, iconImage.sprite, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_inventoryUI.IsDragging)
            _inventoryUI.Drag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_inventoryUI.IsDragging)
            _inventoryUI.CancelDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        _inventoryUI.DropOnSlot(_slotIndex);
    }
}
