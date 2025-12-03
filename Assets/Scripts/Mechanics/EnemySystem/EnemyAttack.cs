using UnityEngine;
using System.Collections;

public class EnemyAttack : MonoBehaviour
{
    public ArrowController arrow;          // опционально: направление атаки
    public float attackCooldown = 0.6f;

    [Header("Trigger Attack")]
    public Collider2D attackHitbox;        // триггер с HitboxRouter
    public float attackWindowSeconds = 0.12f;

    private float _cooldownTimer;
    public bool CanAttack => _cooldownTimer <= 0f;

    void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
    }

    public void TryAttack(Vector2 fallbackDir)
    {
        if (!CanAttack) return;
        _cooldownTimer = attackCooldown;

        // Направление можно использовать для поворота хитбокса, но не рут трансформа.
        Vector2 dir = fallbackDir;
        if (arrow != null && arrow.Direction != Vector2.zero)
            dir = arrow.Direction;

        // Если нужно повернуть только хитбокс/оружие, делай так:
        // if (attackHitbox != null && dir != Vector2.zero)
        //     attackHitbox.transform.right = dir.normalized;

        StartCoroutine(AttackWindow());
    }

    IEnumerator AttackWindow()
    {
        if (attackHitbox != null) attackHitbox.enabled = true;
        yield return new WaitForSeconds(attackWindowSeconds);
        if (attackHitbox != null) attackHitbox.enabled = false;
    }
}
