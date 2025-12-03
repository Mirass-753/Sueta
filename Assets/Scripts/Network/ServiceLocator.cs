using UnityEngine;

// Центральный доступ ко всем сервисам
public static class ServiceLocator
{
    // Сервисы
    public static IInventoryService Inventory { get; private set; }
    public static IPlayerService Player { get; private set; }
    
    // Инициализация (вызывать в начале игры)
    public static void Initialize(bool isNetworkGame = false)
    {
        if (isNetworkGame)
        {
            // Для сетевой игры
            Inventory = new NetworkInventoryService();
            Player = new NetworkPlayerService();
        }
        else
        {
            // Для одиночной игры
            Inventory = new LocalInventoryService();
            Player = new LocalPlayerService();
        }
        
        Debug.Log($"ServiceLocator initialized: {(isNetworkGame ? "Network" : "Local")} mode");
    }
    
    // Смена режима (например, при подключении к серверу)
    public static void SwitchToNetworkMode()
    {
        Inventory = new NetworkInventoryService();
        Player = new NetworkPlayerService();
        Debug.Log("Switched to Network mode");
    }
}