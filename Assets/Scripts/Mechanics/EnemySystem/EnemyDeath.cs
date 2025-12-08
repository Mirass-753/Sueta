using UnityEngine;

public class EnemyDeath : MonoBehaviour
{
    public HealthSystem health;
    public MonoBehaviour[] disableOnDeath;
    public float destroyDelay = 1.5f;

    void Awake()
    {
        if (health == null)
            health = GetComponent<HealthSystem>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDeath += Die;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDeath -= Die;
    }

    void Die()
    {
        foreach (var c in disableOnDeath)
        {
            if (c != null) c.enabled = false;
        }

        // Явно выключаем контроллер и коллайдеры, чтобы после смерти не оставалась "невидимая стена".
        var controller = GetComponent<GridEnemyController>();
        if (controller != null)
            controller.enabled = false;

        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        foreach (var col3D in GetComponentsInChildren<Collider>())
            col3D.enabled = false;

        // можно добавить анимацию/эффект тут
        Destroy(gameObject, destroyDelay);
    }
}
