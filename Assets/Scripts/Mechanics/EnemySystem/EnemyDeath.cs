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
        // можно добавить анимацию/эффект тут
        Destroy(gameObject, destroyDelay);
    }
}
