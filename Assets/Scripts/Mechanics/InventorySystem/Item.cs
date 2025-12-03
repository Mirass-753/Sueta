using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    [Header("Item Settings")]
    public string itemName = "New Item";
    public Sprite icon = null;
    public ItemType itemType = ItemType.Generic;
    public int maxStack = 1;

    [TextArea]
    public string description = "Item description";

    public enum ItemType
    {
        Generic,
        Weapon,
        Consumable,
        Resource
    }

    public virtual void Use()
    {
        Debug.Log($"Using {itemName}");
    }
}
