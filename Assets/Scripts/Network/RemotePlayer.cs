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
    [SerializeField] private ArrowController arrow;   // стрелка удалённого игрока
    [SerializeField] private RemoteArrowSmoother arrowSmoother;


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
        if (string.IsNullOrEmpty(id))
            return;
            
        this.id = id;

        try
        {
            // найдём компоненты (если не проставил руками в инспекторе)
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null && gameObject != null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (damageable == null)
                damageable = GetComponent<Damageable>();

            // Находим стрелку, если не проставлена в инспекторе
            if (arrow == null)
            {
                arrow = GetComponentInChildren<ArrowController>(true); // включая неактивные
                if (arrow == null)
                {
                    // Ищем по имени "Arrow"
                    Transform arrowTransform = transform.Find("Arrow");
                    if (arrowTransform != null)
                        arrow = arrowTransform.GetComponent<ArrowController>();
                }
                
                // Логируем результат поиска
                if (arrow != null && Application.isPlaying)
                {
                    Debug.Log($"[REMOTE] {name} (id={id}): Found arrow component: {arrow.name}, active={arrow.gameObject.activeSelf}");
                }
                else if (arrow == null && Application.isPlaying)
                {
                    Debug.LogWarning($"[REMOTE] {name} (id={id}): Arrow not found in Init! Searched in children and by name 'Arrow'");
                }
            }

            if (arrowSmoother == null)
            {
                arrowSmoother = GetComponent<RemoteArrowSmoother>();
                if (arrowSmoother == null && arrow != null)
                    arrowSmoother = arrow.GetComponent<RemoteArrowSmoother>();
                if (arrowSmoother != null && arrowSmoother.gameObject != null && Application.isPlaying)
                {
                    Debug.Log($"[REMOTE] {name} (id={id}): Found RemoteArrowSmoother on {arrowSmoother.gameObject.name}");
                }
            }

            // задаём начальный спрайт
            if (spriteRenderer != null && idleSprite != null)
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
        catch (System.Exception e)
        {
            Debug.LogWarning($"[REMOTE] Error during Init: {e.Message}");
        }
    }

    // ---------- ПРИЁМ СОСТОЯНИЯ СЕТИ ----------

    public void SetNetworkState(Vector2 pos, Vector2 dir, bool moving, float aimAngle = 0f, bool inCombat = false)
    {
        targetPos = pos;
        lastDir = dir;
        isMoving = moving;

        try
        {
            if (spriteRenderer != null && spriteRenderer.gameObject != null)
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
        }
        catch (System.Exception)
        {
            // Игнорируем ошибки доступа к spriteRenderer (может быть null в редакторе)
        }
        
        try
        {
            // Если стрелка не найдена, попробуем найти её снова
            if (arrow == null)
            {
                arrow = GetComponentInChildren<ArrowController>();
                if (arrow == null)
                {
                    Transform arrowTransform = transform.Find("Arrow");
                    if (arrowTransform != null)
                        arrow = arrowTransform.GetComponent<ArrowController>();
                }
            }

            if (arrowSmoother == null)
            {
                arrowSmoother = GetComponent<RemoteArrowSmoother>();
                if (arrowSmoother == null && arrow != null)
                    arrowSmoother = arrow.GetComponent<RemoteArrowSmoother>();
            }

            // Проверяем, что arrow существует и не уничтожен
            if (arrow != null)
            {
                // Unity перегружает == для MonoBehaviour, поэтому используем ReferenceEquals
                var arrowRef = arrow;
                if (arrowRef != null && arrowRef.gameObject != null)
                {
                    // удалённому игроку не нужен локальный ввод
                    arrowRef.allowPlayerInput = false;
                    
                    // Для удаленных игроков стрелка должна скрываться вне боевого режима,
                    // независимо от настройки hideWhenNotCombat
                    if (inCombat)
                    {
                        // Показываем стрелку и устанавливаем угол
                        arrowRef.SetCombatActive(true);
                        // Явно активируем стрелку, если она была деактивирована
                        if (!arrowRef.gameObject.activeSelf)
                        {
                            arrowRef.gameObject.SetActive(true);
                        }
                        if (arrowSmoother != null)
                        {
                            arrowSmoother.SetTargetAngle(aimAngle);
                        }
                        else
                        {
                            arrowRef.SetAngle(aimAngle);
                        }
                        // Отладочное логирование
                        Debug.Log($"[REMOTE] {name} (id={id}): Set arrow angle={aimAngle:F1}°, inCombat={inCombat}, arrowActive={arrowRef.gameObject.activeSelf}, hideWhenNotCombat={arrowRef.hideWhenNotCombat}");
                    }
                    else
                    {
                        // Скрываем стрелку для удаленного игрока, когда он не в боевом режиме
                        arrowRef.SetCombatActive(false);
                        // Явно деактивируем стрелку, даже если hideWhenNotCombat=false
                        if (arrowRef.gameObject.activeSelf)
                        {
                            arrowRef.gameObject.SetActive(false);
                        }
                        // Отладочное логирование
                        if (Application.isPlaying)
                        {
                            Debug.Log($"[REMOTE] {name} (id={id}): inCombat=false, hiding arrow");
                        }
                    }
                }
            }
            else
            {
                // Логируем только если стрелка действительно не найдена (не в редакторе)
                if (Application.isPlaying)
                {
                    Debug.LogWarning($"[REMOTE] {name} (id={id}): Arrow not found! Cannot show arrow for remote player. inCombat={inCombat}, aimAngle={aimAngle}");
                }
            }
        }
        catch (System.Exception e)
        {
            // Логируем ошибки в рантайме для отладки
            if (Application.isPlaying)
            {
                Debug.LogWarning($"[REMOTE] Error accessing arrow for {name}: {e.Message}");
            }
        }
    }

    // ---------- ОБНОВЛЕНИЕ ПОЗИЦИИ ----------

    private void Update()
    {
        if (transform == null)
            return;
            
        transform.position = Vector2.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * 10f
        );
    }
}