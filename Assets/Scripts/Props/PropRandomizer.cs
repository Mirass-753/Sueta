using UnityEngine;

[DisallowMultipleComponent]
public class RandomizeVisual : MonoBehaviour
{
    [Header("Масштаб")]
    [SerializeField] private bool randomizeScale = true;
    [SerializeField] private float minScale = 0.9f;
    [SerializeField] private float maxScale = 1.1f;

    [Header("Отражение")]
    [SerializeField] private bool randomFlipX = true;

    [Header("Поворот (оси Z для 2D)")]
    [SerializeField] private bool randomizeRotation = false;
    [SerializeField] private float minRotationZ = -5f;
    [SerializeField] private float maxRotationZ = 5f;

    private void Awake()
    {
        Vector3 scale = transform.localScale;

        // Случайный масштаб (одинаковый по X и Y)
        if (randomizeScale)
        {
            float s = Random.Range(minScale, maxScale);
            scale.x *= s;
            scale.y *= s;
        }

        // Случайный флип по X (зеркалим)
        if (randomFlipX && Random.value < 0.5f)
        {
            scale.x *= -1f;
        }

        transform.localScale = scale;

        // Лёгкий случайный поворот
        if (randomizeRotation)
        {
            float z = Random.Range(minRotationZ, maxRotationZ);
            transform.rotation = Quaternion.Euler(0f, 0f, z);
        }
    }
}
