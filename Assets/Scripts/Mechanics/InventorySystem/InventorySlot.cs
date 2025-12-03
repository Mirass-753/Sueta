using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    // ОБЯЗАТЕЛЬНО public
    public Item item;

    // Свойство только для чтения
    public bool IsEmpty => item == null;

    public void Clear()
    {
        item = null;
    }
}
