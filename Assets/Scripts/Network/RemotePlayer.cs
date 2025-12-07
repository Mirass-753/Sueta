using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    // Те же статические настройки, что у тебя были
    public static GameObject prefab;
    public static Sprite idleSprite;
    public static Sprite movingSprite;

    [Header("Network")]
    [Tooltip("ID игрока из сети (тот же, что приходит в NetMessageMove.id)")]
    public string id;

    [Header("Components")]
    [SerializeField] private Damageable damageable;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private RemoteArrowView arrowView;

    private Vector2 targetPos;
    private Vector2 lastDir;
    private bool isMoving;

    // ---------- СОЗДАНИЕ REMOTE-ИГРОКА ----------

    public static RemotePlayer Create(string id)
    {
        GameObject obj;

        if (prefab != null)
            obj = Instantiate(prefab);
        else
            obj = new GameObject($"Remote_{id}");

        var rp = obj.GetComponent<RemotePlayer>();
        if (rp == null)
            rp = obj.AddComponent<RemotePlayer>();

        rp.Init(id);
        return rp;
    }

    /// <summary>
    /// Инициализация remote-кота после спавна.
    /// </summary>
    public void Init(string id)
    {
        this.id = id;

        // найдём компоненты (если не проставил руками в инспекторе)
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (damageable == null)
            damageable = GetComponent<Damageable>();

        // задаём начальный спрайт
        if (idleSprite != null)
            spriteRenderer.sprite = idleSprite;

        // ВАЖНО: привязываем Damageable к тому же id,
        // который использует сеть (NetMessageMove.id, damage.targetId и т.п.)
        if (damageable != null)
        {
            damageable.SetNetworkIdentity(id, true);
            Debug.Log($"[REMOTE] Init {name}, networkId = {damageable.networkId}");
        }
        else
        {
            Debug.LogWarning($"[REMOTE] {name} has no Damageable — incoming HP won't apply");
        }
    }

    // ---------- ПРИЁМ СОСТОЯНИЯ СЕТИ ----------

    public void SetNetworkState(Vector2 pos, Vector2 dir, bool moving, float aimAngle = 0f)
    {
        targetPos = pos;
        lastDir = dir;
        isMoving = moving;

        if (spriteRenderer != null)
        {
            // смена спрайта
            if (isMoving && movingSprite != null)
                spriteRenderer.sprite = movingSprite;
            else if (!isMoving && idleSprite != null)
                spriteRenderer.sprite = idleSprite;

            // поворот
            if (lastDir.x > 0.01f) spriteRenderer.flipX = true;
            else if (lastDir.x < -0.01f) spriteRenderer.flipX = false;
        }
        if (arrowView != null)
    {
        arrowView.SetAngle(aimAngle);
    }
    }

    // ---------- ОБНОВЛЕНИЕ ПОЗИЦИИ ----------

    private void Update()
    {
        transform.position = Vector2.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * 10f
        );
    }
}
