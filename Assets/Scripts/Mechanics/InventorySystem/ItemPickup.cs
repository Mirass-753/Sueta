using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class ItemPickup : MonoBehaviour
{
    public Item item;

    [Tooltip("Уникальный ID, чтобы синхронизировать этот предмет по сети.")]
    public string networkId;

    private SpriteRenderer _spriteRenderer;
    private CircleCollider2D _collider;
    private Coroutine _enableRoutine;

    private static readonly System.Collections.Generic.Dictionary<string, ItemPickup> ActivePickups
        = new System.Collections.Generic.Dictionary<string, ItemPickup>();

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureCollider();
        UpdateVisual();
        EnsureNetworkId();
    }

    private void OnValidate()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureCollider();
        UpdateVisual();
        EnsureNetworkId();
    }

    private void OnEnable()
    {
        Register();
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void UpdateVisual()
    {
        if (_spriteRenderer != null)
            _spriteRenderer.sprite = item != null ? item.icon : null;

        gameObject.name = item != null ? $"Pickup_{item.itemName}" : "Pickup_Empty";
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        AttemptPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        AttemptPickup(other);
    }

    private void EnsureCollider()
    {
        if (_collider == null)
            _collider = GetComponent<CircleCollider2D>();

        if (_collider == null)
            _collider = gameObject.AddComponent<CircleCollider2D>();

        _collider.isTrigger = true;
    }

    private void AttemptPickup(Collider2D other)
    {
        if (_collider != null && !_collider.enabled) return;
        if (!other.CompareTag("Player")) return;

        if (_collider == null)
            _collider = gameObject.AddComponent<CircleCollider2D>();

        inventory.TryPickup(this);
    }

    public void ReactivatePickup(Vector3 position, Item newItem = null, int unusedQuantity = 1, string newNetworkId = null)
    {
        if (newItem != null)
            item = newItem;

        if (!string.IsNullOrEmpty(newNetworkId))
            networkId = newNetworkId;
        EnsureNetworkId();

        transform.position = position;
        gameObject.SetActive(true);

        if (_collider == null)
            _collider = GetComponent<CircleCollider2D>();

        _collider.enabled = false;
        if (_enableRoutine != null)
            StopCoroutine(_enableRoutine);
        _enableRoutine = StartCoroutine(EnableColliderDelayed());

        UpdateVisual();
    }

    private System.Collections.IEnumerator EnableColliderDelayed()
    {
        yield return new WaitForSeconds(0.15f);
        if (_collider != null)
            _collider.enabled = true;
    }

    private void EnsureNetworkId()
    {
        if (string.IsNullOrEmpty(networkId))
            networkId = System.Guid.NewGuid().ToString();
    }

    private void Register()
    {
        EnsureNetworkId();
        ActivePickups[networkId] = this;
    }

    private void Unregister()
    {
        if (string.IsNullOrEmpty(networkId)) return;
        if (ActivePickups.TryGetValue(networkId, out var existing) && existing == this)
            ActivePickups.Remove(networkId);
    }

    public static bool TryGetByNetworkId(string id, out ItemPickup pickup)
    {
        return ActivePickups.TryGetValue(id, out pickup);
    }
}
