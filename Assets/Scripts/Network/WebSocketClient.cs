using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;

public class WebSocketClient : MonoBehaviour
{
    public static WebSocketClient Instance;

    [Header("Connection")]
    [SerializeField]
    private bool autoConnectInEditor = true;

    [Header("URLs")]
    [Tooltip("Production WebSocket endpoint (default)")]
    [SerializeField]
    private string productionUrl = "wss://catlaw.online/ws";

    [Tooltip("Override URL when running in the Unity editor (e.g. local Node server)")]
    [SerializeField]
    private bool overrideUrlInEditor = true;

    [SerializeField]
    private string editorUrl = "ws://127.0.0.1:3000";

    // собственно сокет
    private WebSocket socket;

    // ---- ВАЖНО: здесь выбираем URL ----
    private string GetUrl()
    {
        // 1) env-переменная имеет наивысший приоритет (например, для билдов)
        var envUrl = Environment.GetEnvironmentVariable("WEBSOCKET_URL");
        if (!string.IsNullOrEmpty(envUrl))
            return envUrl;

#if UNITY_EDITOR
        // 2) в редакторе можно включить локальный сервер
        if (overrideUrlInEditor && !string.IsNullOrEmpty(editorUrl))
            return editorUrl;
#endif

        // 3) по умолчанию — продовый wss
        return productionUrl;
    }


    private string WebSocketUrl => GetUrl();

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        if (autoConnectInEditor)
#endif
        {
            await Connect();
        }
    }

    // Подключение
    public async System.Threading.Tasks.Task Connect()
    {
        // если уже был сокет — закрываем
        if (socket != null)
        {
            try
            {
                await socket.Close();
            }
            catch { }
            socket = null;
        }

        var url = WebSocketUrl;
        Debug.Log("[WS] Connecting to: " + url);

        socket = new WebSocket(url);

        socket.OnOpen += () =>
        {
            Debug.Log("[WS] Connected");
        };

        socket.OnError += (e) =>
        {
            Debug.LogError("[WS] Error: " + e);
        };

        socket.OnClose += (code) =>
        {
            Debug.Log("[WS] Closed: " + code);
        };

        socket.OnMessage += (bytes) =>
        {
            // получаем JSON и отдаём в наш хендлер
            var msg = Encoding.UTF8.GetString(bytes);
            NetworkMessageHandler.Handle(msg);
        };

        try
        {
            await socket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] Connect exception: " + ex);
        }
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private void Update()
    {
        // нужно для NativeWebSocket вне WebGL
        socket?.DispatchMessageQueue();
    }
#endif

    // Отправка строки
    public async void Send(string message)
    {
        if (socket == null)
        {
            // Debug.LogWarning("[WS] Send called, but socket is null");
            return;
        }

        if (socket.State != WebSocketState.Open)
        {
            // чтобы не спамило в консоль, просто тихо выходим
            // Debug.LogWarning("[WS] Send called, but socket state is " + socket.State);
            return;
        }

        try
        {
            await socket.SendText(message);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] Send exception: " + ex);
        }
    }

    private async void OnApplicationQuit()
    {
        if (socket != null)
        {
            try
            {
                await socket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError("[WS] Close exception: " + ex);
            }
        }
    }
}
