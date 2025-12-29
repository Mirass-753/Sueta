using System.Collections.Generic;

public static class AttackContextRegistry
{
    private static readonly Dictionary<string, string> _activeAttacks =
        new Dictionary<string, string>();

    public static void SetAttack(string sourceId, string attackId)
    {
        if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(attackId))
            return;

        _activeAttacks[sourceId] = attackId;
    }

    public static string GetAttackId(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
            return null;

        return _activeAttacks.TryGetValue(sourceId, out var attackId) ? attackId : null;
    }

    public static void ClearAttack(string sourceId, string attackId = null)
    {
        if (string.IsNullOrEmpty(sourceId))
            return;

        if (attackId == null)
        {
            _activeAttacks.Remove(sourceId);
            return;
        }

        if (_activeAttacks.TryGetValue(sourceId, out var existing) && existing == attackId)
            _activeAttacks.Remove(sourceId);
    }
}
