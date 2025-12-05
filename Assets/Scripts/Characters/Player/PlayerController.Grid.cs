using UnityEngine;

public partial class PlayerController : MonoBehaviour
{
    // ========= СЕТКА / ВСПОМОГАТЕЛЬНОЕ =========

    public void SyncPositionToGrid()
    {
        _currentCell = WorldToCell(transform.position);
        _targetPosition = transform.position;
    }

    public void SetPosition(Vector2 newPosition)
    {
        occupancyManager?.Unregister(_currentCell);
        _currentCell = WorldToCell(newPosition);
        occupancyManager?.Register(_currentCell);
        _targetPosition = CellToWorld(_currentCell);
        transform.position = _targetPosition;
    }

    private Vector2Int WorldToCell(Vector2 worldPos)
    {
        float fx = worldPos.x / gridSize - cellCenterOffset.x;
        float fy = worldPos.y / gridSize - cellCenterOffset.y;
        return new Vector2Int(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        float wx = (cell.x + cellCenterOffset.x) * gridSize;
        float wy = (cell.y + cellCenterOffset.y) * gridSize;
        return new Vector2(wx, wy);
    }
}
