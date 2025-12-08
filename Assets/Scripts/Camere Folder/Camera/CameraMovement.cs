using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("View Settings")]
    public int visibleCellsX = 14;
    public int visibleCellsY = 8;
    public float cellSize = 1f;

    [Header("Follow Settings")]
    public Transform target;
    public float smoothSpeed = 0.1f;
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Camera Stabilization")]
    public float fixedOrthographicSize = 4f;

    [Header("Zoom Settings")]
    [Tooltip("Ограничивает минимальный зум камеры при прокрутке колёсика мыши")]
    public float minOrthographicSize = 2f;
    [Tooltip("Максимальное отдаление камеры. По умолчанию совпадает с текущим размером камеры при старте.")]
    public float maxOrthographicSize = 0f;
    [Tooltip("Скорость приближения/отдаления при прокрутке колёсика мыши")]
    public float zoomSpeed = 2f;

    [Header("Chunk Lock")]
    public bool lockToChunksInCombat = true;
    public float lockSmooth = 0.08f;
    public bool clampToWorldBounds = false;
    public Vector2Int worldMinCell = new Vector2Int(0, 0);
    public Vector2Int worldMaxCell = new Vector2Int(13, 7);

    private Camera gameCamera;
    private Vector3 velocity = Vector3.zero;
    private Vector3 lockVelocity = Vector3.zero;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool _lockChunks;
    private float baseZ;

    void Start()
    {
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        gameCamera = GetComponent<Camera>();
        baseZ = transform.position.z; // фиксируем Z
        SetupCameraSize();
    }

    void SetupCameraSize()
    {
        if (gameCamera == null) return;

        float baseSize = (visibleCellsY * cellSize) / 2f;
        float targetAspect = (float)visibleCellsX / visibleCellsY;
        float currentAspect = (float)Screen.width / Screen.height;

        gameCamera.orthographicSize = baseSize * (targetAspect / currentAspect);
        fixedOrthographicSize = gameCamera.orthographicSize;

        if (maxOrthographicSize <= 0f)
            maxOrthographicSize = fixedOrthographicSize;

        maxOrthographicSize = Mathf.Max(maxOrthographicSize, minOrthographicSize);

        fixedOrthographicSize = Mathf.Clamp(fixedOrthographicSize, minOrthographicSize, maxOrthographicSize);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired;
        if (_lockChunks)
        {
            desired = ComputeChunkCenter(target.position) + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref lockVelocity, lockSmooth);
        }
        else
        {
            desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothSpeed);
        }
    }

    void Update()
    {
        HandleScrollZoom();

        if (gameCamera != null && gameCamera.orthographicSize != fixedOrthographicSize)
            gameCamera.orthographicSize = fixedOrthographicSize;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            SetupCameraSize();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    public void SetChunkLock(bool active)
    {
        _lockChunks = active && lockToChunksInCombat;
    }

    Vector3 ComputeChunkCenter(Vector3 pos)
    {
        float chunkW = visibleCellsX * cellSize;
        float chunkH = visibleCellsY * cellSize;

        int cx = Mathf.FloorToInt(pos.x / chunkW);
        int cy = Mathf.FloorToInt(pos.y / chunkH);

        float centerX = (cx + 0.5f) * chunkW;
        float centerY = (cy + 0.5f) * chunkH;

        if (clampToWorldBounds)
        {
            float worldMinX = (worldMinCell.x + 0.5f) * cellSize;
            float worldMaxX = (worldMaxCell.x + 0.5f) * cellSize;
            float worldMinY = (worldMinCell.y + 0.5f) * cellSize;
            float worldMaxY = (worldMaxCell.y + 0.5f) * cellSize;

            float halfW = chunkW * 0.5f;
            float halfH = chunkH * 0.5f;

            centerX = Mathf.Clamp(centerX, worldMinX + halfW, worldMaxX - halfW);
            centerY = Mathf.Clamp(centerY, worldMinY + halfH, worldMaxY - halfH);
        }

        return new Vector3(centerX, centerY, baseZ);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        float width = visibleCellsX * cellSize;
        float height = visibleCellsY * cellSize;

        Vector3 cameraPos = transform.position;
        Vector3 topLeft = cameraPos + new Vector3(-width / 2, height / 2, 0);
        Vector3 topRight = cameraPos + new Vector3(width / 2, height / 2, 0);
        Vector3 bottomLeft = cameraPos + new Vector3(-width / 2, -height / 2, 0);
        Vector3 bottomRight = cameraPos + new Vector3(width / 2, -height / 2, 0);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }

    void HandleScrollZoom()
    {
        if (gameCamera == null) return;

        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        fixedOrthographicSize -= scrollDelta * zoomSpeed;
        fixedOrthographicSize = Mathf.Clamp(fixedOrthographicSize, minOrthographicSize, maxOrthographicSize);
    }
}
