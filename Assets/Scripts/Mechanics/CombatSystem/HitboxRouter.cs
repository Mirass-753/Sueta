using UnityEngine;

public class HitboxRouter : MonoBehaviour
{
    [Header("Damage")]
    public float baseDamage = 10f;

    [Header("Attacker")]
    [Tooltip("Корень атакующего (где висит Damageable / PlayerController). " +
             "Если не указать, будет использован transform.root.")]
    public Transform attackerRoot;

    private Damageable _attackerDamageable;

    private void Awake()
    {
        if (attackerRoot == null)
            attackerRoot = transform.root;

        ResolveAttackerDamageable();
    }

    private void ResolveAttackerDamageable()
    {
        if (_attackerDamageable != null)
            return;

        if (attackerRoot != null)
            _attackerDamageable = attackerRoot.GetComponentInChildren<Damageable>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("[HITBOX] trigger with " + other.name);
        // кого ударили
        var targetDamageable = other.GetComponentInParent<Damageable>();
        if (targetDamageable == null)
            return;

        // хитбоксы могут спауниться раньше, чем создаётся/инициализируется атакующий
        // (например, снаряды). Поэтому перед атакой пробуем ещё раз найти Damageable.
        ResolveAttackerDamageable();

        // не бьём сами себя
        if (_attackerDamageable != null && targetDamageable == _attackerDamageable)
            return;

        // формируем данные атаки
        AttackData data = new AttackData
        {
            baseDamage = baseDamage,
            direction  = transform.right,      // направление стрелки
            attacker   = attackerRoot != null ? attackerRoot : transform,
            attackerDamageable = _attackerDamageable
        };

        targetDamageable.ApplyHit(data, other);
    }
}
