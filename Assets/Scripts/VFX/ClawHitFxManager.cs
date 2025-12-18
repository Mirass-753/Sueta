using System;
using UnityEngine;

public class ClawHitFxManager : MonoBehaviour
{
    public static ClawHitFxManager Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private ClawHitFx clawPrefab;

    [Header("Placement")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.2f, 0f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SpawnClaws(string targetId, string zone, Vector3 fallbackWorldPos)
    {
        if (clawPrefab == null)
        {
            Debug.LogWarning("[VFX] ClawHitFxManager has no clawPrefab assigned");
            return;
        }

        Vector3 spawnPos = fallbackWorldPos + worldOffset;
        Transform parent = null;

        var target = FindTargetDamageable(targetId);
        if (target != null)
        {
            var zoneTransform = FindZoneTransform(target, zone);
            if (zoneTransform != null)
            {
                parent = zoneTransform;
                spawnPos = zoneTransform.position + worldOffset;
            }
            else
            {
                parent = target.transform;
                spawnPos = target.transform.position + worldOffset;
            }
        }

        var fx = Instantiate(clawPrefab, spawnPos, Quaternion.identity, parent);
        fx.Init();
    }

    private Damageable FindTargetDamageable(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (Damageable.TryGetById(id, out var dmg) && dmg != null)
            return dmg;

        foreach (var candidate in FindObjectsOfType<Damageable>(true))
        {
            if (candidate != null && candidate.networkId == id)
                return candidate;
        }

        return null;
    }

    private Transform FindZoneTransform(Damageable target, string zone)
    {
        if (target?.zones == null || string.IsNullOrEmpty(zone))
            return null;

        if (!Enum.TryParse(zone, true, out BodyZone parsedZone))
            return null;

        foreach (var z in target.zones)
        {
            if (z == null || z.collider == null)
                continue;

            if (z.zone == parsedZone)
                return z.collider.transform;
        }

        return null;
    }
}
