using System.Collections.Generic;
using UnityEngine;

public class DroppedItemPool : MonoBehaviour
{
    [Header("Pool Settings")]
    public ItemPickup itemPickupPrefab;
    public int initialPoolSize = 10;

    private readonly List<ItemPickup> _pool = new List<ItemPickup>();

    private void Awake()
    {
        if (itemPickupPrefab == null)
        {
            Debug.LogError("DroppedItemPool: itemPickupPrefab is not set in inspector.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewInstance();
        }
    }

    private ItemPickup CreateNewInstance()
    {
        var instance = Instantiate(itemPickupPrefab, transform);
        instance.gameObject.SetActive(false);
        _pool.Add(instance);
        return instance;
    }

    /// <summary>
    /// ВОТ ЭТОТ метод нужен PlayerInventory.DropItem
    /// </summary>
public ItemPickup Spawn(Item item, Vector3 worldPosition)
{
    if (item == null)
    {
        Debug.LogWarning("DroppedItemPool.Spawn called with null item.");
        return null;
    }

    if (itemPickupPrefab == null)
    {
        Debug.LogError("DroppedItemPool: itemPickupPrefab is NOT set in inspector.");
        return null;
    }

    ItemPickup pickup = null;

    for (int i = 0; i < _pool.Count; i++)
    {
        if (!_pool[i].gameObject.activeInHierarchy)
        {
            pickup = _pool[i];
            break;
        }
    }

    if (pickup == null)
    {
        pickup = CreateNewInstance();
    }

    worldPosition.z = 0f;
    pickup.ReactivatePickup(worldPosition, item, 1);

    Debug.Log($"DroppedItemPool: spawned {item.itemName} at {pickup.transform.position}");
    return pickup;
}



    public void Despawn(GameObject pickupObject)
    {
        if (pickupObject == null) return;

        pickupObject.SetActive(false);
        pickupObject.transform.SetParent(transform);
    }
}
