using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private DamagePopup popupPrefab;

    [Header("Settings")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private Color damageColor = Color.red;

    private Camera _cam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        _cam = Camera.main;
    }

    public void ShowDamage(int amount, Vector3 worldPosition)
    {
        if (popupPrefab == null || canvas == null)
            return;

        Vector3 wp = worldPosition + worldOffset;
        Vector3 sp = (_cam != null) ? _cam.WorldToScreenPoint(wp) : wp;

        if (_cam != null && sp.z < 0f)
            return;

        var popup = Instantiate(popupPrefab, canvas.transform);
        var rt = popup.transform as RectTransform;

        RectTransform canvasRt = canvas.transform as RectTransform;

        Vector2 anchored;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt,
            sp,
            (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _cam,
            out anchored
        );

        if (rt != null) rt.anchoredPosition = anchored;

        popup.Init(amount, damageColor);
    }
}
