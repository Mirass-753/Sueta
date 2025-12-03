using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class ItemPickup : MonoBehaviour
{
    public Item item;

    private SpriteRenderer _spriteRenderer;
    private CircleCollider2D _collider;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<CircleCollider2D>();
        _collider.isTrigger = true;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_spriteRenderer != null)
            _spriteRenderer.sprite = item != null ? item.icon : null;

        gameObject.name = item != null ? $"Pickup_{item.itemName}" : "Pickup_Empty";
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        Debug.Log($"ItemPickup: player entered, can pickup {item?.itemName}");
        inventory.RegisterNearbyPickup(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        Debug.Log("ItemPickup: player left trigger");
        inventory.UnregisterNearbyPickup(this);
    }

    public void ReactivatePickup(Vector3 position, Item newItem = null, int unusedQuantity = 1)
    {
    if (newItem != null)
        item = newItem;

    transform.position = position;
    gameObject.SetActive(true);

    if (_collider == null)
        _collider = GetComponent<CircleCollider2D>();

    _collider.enabled = true;

    UpdateVisual();
    }
}