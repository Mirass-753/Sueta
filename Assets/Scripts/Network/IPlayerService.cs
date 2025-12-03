using UnityEngine;

// Интерфейс для сервиса игрока
public interface IPlayerService
{
    void Move(Vector2 direction);
    void SetPosition(Vector2 position);
    void Interact(GameObject target);
    void Attack(Vector2 direction);
}

// Локальная реализация
public class LocalPlayerService : IPlayerService
{
    public void Move(Vector2 direction)
    {
        // Локальное движение уже обрабатывается в PlayerController
    }
    
    public void SetPosition(Vector2 position)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = position;
        }
    }
    
    public void Interact(GameObject target)
    {
        // Локальная логика взаимодействия
    }
    
    public void Attack(Vector2 direction)
    {
        // Локальная логика атаки
    }
}

// Сетевая реализация
public class NetworkPlayerService : IPlayerService
{
    public void Move(Vector2 direction)
    {
        // NetworkManager.Instance.SendPlayerMove(direction);
    }
    
    public void SetPosition(Vector2 position)
    {
        // NetworkManager.Instance.SendPlayerPosition(position);
    }
    
    public void Interact(GameObject target)
    {
        // NetworkManager.Instance.SendPlayerInteract(target.GetInstanceID());
    }
    
    public void Attack(Vector2 direction)
    {
        // NetworkManager.Instance.SendPlayerAttack(direction);
    }
}