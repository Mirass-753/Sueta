using UnityEngine;

public interface IInventoryService
{
    void AddItem(Item item, int quantity);
    void DropItem(int slotIndex, Vector3 position);
    void UseItem(int slotIndex);
    void MoveItem(int fromSlot, int toSlot);
}

public class LocalInventoryService : IInventoryService
{
    private PlayerInventory PlayerInv
    {
        get
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<PlayerInventory>() : null;
        }
    }

    public void AddItem(Item item, int quantity)
    {
        var inv = PlayerInv;
        if (inv == null || item == null || quantity <= 0) return;

        for (int i = 0; i < quantity; i++)
        {
            inv.AddItem(item);
        }
    }

    public void DropItem(int slotIndex, Vector3 position)
    {
        var inv = PlayerInv;
        if (inv == null) return;

        inv.DropItem(slotIndex, position);
    }

    public void UseItem(int slotIndex)
    {
        var inv = PlayerInv;
        if (inv == null) return;

        inv.UseItem(slotIndex);
    }

    public void MoveItem(int fromSlot, int toSlot)
    {
        var inv = PlayerInv;
        if (inv == null) return;

        inv.SwapSlots(fromSlot, toSlot);
    }
}

public class NetworkInventoryService : IInventoryService
{
    public void AddItem(Item item, int quantity) { }
    public void DropItem(int slotIndex, Vector3 position) { }
    public void UseItem(int slotIndex) { }
    public void MoveItem(int fromSlot, int toSlot) { }
}
