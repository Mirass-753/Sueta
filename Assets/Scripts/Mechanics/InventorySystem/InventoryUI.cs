using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory playerInventory;
    public GameObject inventoryPanel;
    public Transform slotsParent;
    public InventorySlotUI slotPrefab;
    public Image dragIcon;
    public GameObject hoverPanel;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.U;
    public Vector2 worldOffsetFromPlayer = new Vector2(1.5f, 0.5f);

    private InventorySlotUI[] _slotUIs;
    private bool _isOpen;
    private int _dragFromIndex = -1;
    private Camera _mainCamera;

    void Start()
    {
        _mainCamera = Camera.main;
        TryBindInventory();

        _isOpen = false;
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        HideHoverPanel();

        if (dragIcon != null)
            dragIcon.enabled = false;
    }

    void OnDestroy()
    {
        UnsubscribeInventory();
    }

    void Update()
    {
        if (playerInventory == null)
            TryBindInventory();

        if (Input.GetKeyDown(toggleKey))
            SetOpen(!_isOpen);

        if (_isOpen)
            UpdatePanelPosition();
    }

    // ---------- Привязка инвентаря ----------
    void TryBindInventory()
    {
        if (playerInventory != null)
        {
            SetPlayerInventory(playerInventory);
            return;
        }

#if UNITY_2023_1_OR_NEWER
        var inv = Object.FindFirstObjectByType<PlayerInventory>();
#else
        var inv = Object.FindObjectOfType<PlayerInventory>();
#endif
        if (inv != null)
            SetPlayerInventory(inv);
    }

    public void SetPlayerInventory(PlayerInventory inv)
    {
        UnsubscribeInventory();
        playerInventory = inv;

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged += RefreshAll;
            CreateSlots();
            RefreshAll();
        }
    }

    void UnsubscribeInventory()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= RefreshAll;
    }

    void SetOpen(bool isOpen)
    {
        _isOpen = isOpen;
        if (inventoryPanel != null)
            inventoryPanel.SetActive(_isOpen);

        if (!_isOpen)
        {
            CancelDrag();
            HideHoverPanel();
        }
    }

    // ---------- UI ----------
    void UpdatePanelPosition()
    {
        if (inventoryPanel == null || _mainCamera == null || playerInventory == null) return;

        Vector3 worldPos = playerInventory.transform.position + (Vector3)worldOffsetFromPlayer;
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
        inventoryPanel.transform.position = screenPos;
    }

    void CreateSlots()
    {
        if (slotsParent == null || slotPrefab == null || playerInventory == null) return;

        foreach (Transform child in slotsParent)
            Destroy(child.gameObject);

        int count = playerInventory.SlotCount;
        _slotUIs = new InventorySlotUI[count];

        for (int i = 0; i < count; i++)
        {
            var slotUI = Instantiate(slotPrefab, slotsParent);
            slotUI.Initialize(i, this);

            var hover = slotUI.GetComponent<InventoryHover>();
            if (hover != null)
                hover.rightPanel = hoverPanel;

            _slotUIs[i] = slotUI;
        }
    }

    void RefreshAll()
    {
        if (_slotUIs == null || playerInventory == null) return;

        for (int i = 0; i < _slotUIs.Length; i++)
        {
            var slot = playerInventory.GetSlot(i);
            _slotUIs[i].SetItem(slot != null ? slot.item : null);
        }
    }

    // ---------- API для слотов ----------
    public void UseSlot(int index)
    {
        playerInventory?.UseItem(index);
    }

    public void DropFromSlot(int index)
    {
        if (playerInventory == null) return;
        var dropPos = playerInventory.transform.position + new Vector3(1f, 0f, 0f);
        playerInventory.DropItem(index, dropPos);
    }

    public void BeginDrag(int slotIndex, Sprite iconSprite, Vector2 screenPosition)
    {
        if (iconSprite == null || dragIcon == null) return;

        _dragFromIndex = slotIndex;
        dragIcon.sprite = iconSprite;
        dragIcon.enabled = true;
        dragIcon.transform.position = screenPosition;
    }

    public void Drag(Vector2 screenPosition)
    {
        if (_dragFromIndex < 0 || dragIcon == null) return;
        dragIcon.transform.position = screenPosition;
    }

    public void DropOnSlot(int targetIndex)
    {
        if (_dragFromIndex < 0 || playerInventory == null)
        {
            CancelDrag();
            return;
        }

        if (targetIndex >= 0)
            playerInventory.SwapSlots(_dragFromIndex, targetIndex);

        CancelDrag();
    }

    public void CancelDrag()
    {
        _dragFromIndex = -1;
        if (dragIcon != null)
            dragIcon.enabled = false;
    }

    public bool IsDragging => _dragFromIndex >= 0;

    public Item GetItemInSlot(int index)
    {
        var slot = playerInventory?.GetSlot(index);
        return slot != null ? slot.item : null;
    }

    private void HideHoverPanel()
    {
        if (hoverPanel != null)
            hoverPanel.SetActive(false);
    }
}
