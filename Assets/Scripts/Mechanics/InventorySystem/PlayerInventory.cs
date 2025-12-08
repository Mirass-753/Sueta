using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private int slotCount = 5;

    [Header("Grid Settings")]
    [Tooltip("Размер клетки для проверки расстояния до предметов.")]
    public float gridSize = 1f;
    [Tooltip("Смещение центра клетки (как в контроллере игрока).")]
    public Vector2 cellCenterOffset = new Vector2(0.5f, 0.5f);

    [Header("Owner")]
    [Tooltip("Трансформ игрока. Если не задан, возьмется из объекта с тегом Player.")]
    public Transform ownerTransform;

    public int SlotCount => slotCount;

    private InventorySlot[] _slots;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (ownerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                ownerTransform = player.transform;
        }

        _slots = new InventorySlot[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            _slots[i] = new InventorySlot();
        }
    }

    public bool TryPickup(ItemPickup pickup)
    {
        if (pickup == null || pickup.item == null)
            return false;

        if (ownerTransform != null)
        {
            Vector2Int playerCell = WorldToCell(ownerTransform.position);
            Vector2Int itemCell = WorldToCell(pickup.transform.position);
            if (playerCell != itemCell)
                return false; // предмет не в той же клетке
        }

        bool added = AddItem(pickup.item);

        Debug.Log($"TryPickup: {pickup.item.itemName}, added={added}");

        if (added)
        {
            var pool = FindFirstObjectByType<DroppedItemPool>();
            if (pool != null)
                pool.Despawn(pickup.gameObject);
            else
                pickup.gameObject.SetActive(false);
        }

        return added;
    }

    public bool AddItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;

        bool addedAny = false;

        for (int q = 0; q < quantity; q++)
        {
            bool placed = false;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i].item = item;
                    addedAny = true;
                    placed = true;
                    break;
                }
            }

            if (!placed)
                break;
        }

        if (addedAny)
        {
            OnInventoryChanged?.Invoke();
        }
        else
        {
            Debug.Log("Inventory full, cannot add item " + item.itemName);
        }

        return addedAny;
    }

    public void UseItem(int index)
    {
        if (!IsValidIndex(index)) return;

        var slot = _slots[index];
        if (slot.IsEmpty) return;

        slot.item.Use();
        slot.Clear();
        OnInventoryChanged?.Invoke();
    }

    public void DropItem(int index, Vector3 worldPosition)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"DropItem: index {index} is invalid.");
            return;
        }

        var slot = _slots[index];
        if (slot.IsEmpty)
        {
            Debug.Log($"DropItem: slot {index} is empty.");
            return;
        }

        var pool = FindFirstObjectByType<DroppedItemPool>();
        if (pool == null)
        {
            Debug.LogWarning("DropItem: DroppedItemPool not found in scene.");
            return;
        }

        worldPosition.z = 0f;

        Debug.Log($"DropItem: dropping {slot.item.itemName} at {worldPosition}");
        pool.Spawn(slot.item, worldPosition);

        slot.Clear();
        OnInventoryChanged?.Invoke();
    }

    public void SwapSlots(int fromIndex, int toIndex)
    {
        if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex) || fromIndex == toIndex)
            return;

        var temp = _slots[fromIndex].item;
        _slots[fromIndex].item = _slots[toIndex].item;
        _slots[toIndex].item = temp;

        OnInventoryChanged?.Invoke();
    }

    public InventorySlot GetSlot(int index)
    {
        if (!IsValidIndex(index)) return null;
        return _slots[index];
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < _slots.Length;
    }

    private Vector2Int WorldToCell(Vector2 worldPos)
    {
        float fx = worldPos.x / gridSize - cellCenterOffset.x;
        float fy = worldPos.y / gridSize - cellCenterOffset.y;
        return new Vector2Int(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
    }
}
