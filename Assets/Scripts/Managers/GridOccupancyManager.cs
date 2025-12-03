using System.Collections.Generic;
using UnityEngine;

/// Хранит занятые клетки и арбитрирует ход. Без синглтона: положи на объект в сцене
/// и передай ссылку в контроллеры.
public class GridOccupancyManager : MonoBehaviour
{
    private readonly HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>();

    /// Регистрация стартовой клетки. Возвращает true, если свободно.
    public bool Register(Vector2Int cell)
    {
        if (_occupied.Contains(cell)) return false;
        _occupied.Add(cell);
        return true;
    }

    public void Unregister(Vector2Int cell)
    {
        _occupied.Remove(cell);
    }

    /// Попытка перейти из from в to. true — если удалось, и клетка занята за агентом.
    public bool TryMove(Vector2Int from, Vector2Int to)
    {
        if (from == to) return true;
        if (_occupied.Contains(to)) return false;
        _occupied.Remove(from);
        _occupied.Add(to);
        return true;
    }
}
