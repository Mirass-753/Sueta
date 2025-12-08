using System;
using UnityEngine;

[Serializable]
public class NetMessageItemDrop : NetMessageBase
{
    public string pickupId;
    public string itemName;
    public float x;
    public float y;

    public NetMessageItemDrop()
    {
        type = "item_drop";
    }
}

[Serializable]
public class NetMessageItemPickup : NetMessageBase
{
    public string pickupId;
    public string itemName;
    public float x;
    public float y;

    public NetMessageItemPickup()
    {
        type = "item_pickup";
    }
}

public static class ItemRegistry
{
    public static Item FindItemByName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
            return null;

        var all = Resources.FindObjectsOfTypeAll<Item>();
        foreach (var it in all)
        {
            if (it != null && it.itemName == itemName)
                return it;
        }

        Debug.LogWarning($"ItemRegistry: item '{itemName}' not found in loaded assets.");
        return null;
    }
}
