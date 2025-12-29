using UnityEngine;

public class HitboxRouter : MonoBehaviour
{
    [Header("Damage")]
    public float baseDamage = 100f;

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
    Debug.Log($"[HITBOX] trigger with {other.name}", other);

    // 1. ищем Damageable у цели
    var targetDamageable = other.GetComponentInParent<Damageable>();
    if (targetDamageable == null)
    {
        Debug.LogWarning($"[HITBOX] NO Damageable found on {other.name} or its parents");
        return;
    }

    Debug.Log(
        $"[HITBOX] Found Damageable on {targetDamageable.gameObject.name}, " +
        $"isNetwork={targetDamageable.isNetworkEntity}, id='{targetDamageable.networkId}'");

    // 2. актуализируем атакующего
    ResolveAttackerDamageable();

    // 3. не бьём сами себя
    if (_attackerDamageable != null && targetDamageable == _attackerDamageable)
    {
        Debug.Log("[HITBOX] Hit self, ignored");
        return;
    }

    // 4. собираем данные атаки
    string attackId = null;
    if (_attackerDamageable != null && _attackerDamageable.isNetworkEntity)
    {
        attackId = AttackContextRegistry.GetAttackId(_attackerDamageable.networkId);
        if (string.IsNullOrEmpty(attackId))
        {
            Debug.LogWarning(
                $"[HITBOX] No active attack for attacker id='{_attackerDamageable.networkId}', hit ignored");
            return;
        }
    }

    AttackData data = new AttackData
    {
        attackId           = attackId,
        baseDamage         = baseDamage,
        direction          = transform.right,
        attacker           = attackerRoot != null ? attackerRoot : transform,
        attackerDamageable = _attackerDamageable
    };

    Debug.Log(
        $"[HITBOX] Call ApplyHit: attacker={data.attacker.name}, " +
        $"target={targetDamageable.gameObject.name}");

    // 5. наносим удар (здесь уже дальше включается Damageable.ApplyHit и сеть)
    targetDamageable.ApplyHit(data, other);
    }
}
