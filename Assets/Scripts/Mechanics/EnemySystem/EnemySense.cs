using UnityEngine;

public class EnemySense : MonoBehaviour
{
    [Tooltip("Если пусто, найдёт по тегу Player.")]
    public Transform player;

    public bool HasPlayer => player != null;

    void Awake()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    public float DistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector2.Distance(transform.position, player.position);
    }

    public Vector2Int PlayerCell(float gridSize, Vector2 cellOffset)
    {
        if (player == null) return Vector2Int.zero;
        var pos = player.position;
        float fx = pos.x / gridSize - cellOffset.x;
        float fy = pos.y / gridSize - cellOffset.y;
        return new Vector2Int(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
    }
}
