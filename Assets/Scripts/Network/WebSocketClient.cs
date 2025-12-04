using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;

public class WebSocketClient : MonoBehaviour
{
    public static WebSocketClient Instance;

    // Автосоздание клиента, если забыли положить его в сцену/префаб.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance != null)
            return;

        var existing = FindObjectOfType<WebSocketClient>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("WebSocketClient");
        go.AddComponent<WebSocketClient>();
    }

    [Header("Connection")]
    [SerializeField]
    private bool autoConnectInEditor = true;

    [SerializeField]
    [Tooltip("Автоматически пытаться переподключиться при обрыве")]
    private bool autoReconnect = true;

    [SerializeField]
    [Range(0.5f, 10f)]
    private float reconnectDelaySeconds = 2f;

    [Header("URLs")]
    [Tooltip("Production WebSocket endpoint (default)")]
    [SerializeField]
    private string productionUrl = "wss://catlaw.online/ws";

    [Tooltip("Override URL when running in the Unity editor (e.g. local Node server)")]
    [SerializeField]
    private bool overrideUrlInEditor = true;

    [SerializeField]
    private string editorUrl = "ws://127.0.0.1:3000";

    [Tooltip("В редакторе: если локальный URL недоступен, попробовать production")]
    [SerializeField]
    private bool fallbackToProductionInEditor = true;

    // собственно сокет
    private WebSocket socket;

    private bool applicationQuitting;
    private bool reconnecting;
    private bool skipReconnectOnce;

    // Очередь сообщений, которые пришли до установления соединения.
    private readonly System.Collections.Generic.Queue<string> outgoingQueue =
        new System.Collections.Generic.Queue<string>();

    [SerializeField, Tooltip("Максимум ожидающих сообщений до соединения")]
    private int queuedMessageLimit = 64;

    // ---- ВАЖНО: здесь выбираем URL ----
    private string[] BuildCandidateUrls()
    {
        // 1) env-переменная имеет наивысший приоритет (например, для билдов)
        var envUrl = Environment.GetEnvironmentVariable("WEBSOCKET_URL");
        if (!string.IsNullOrEmpty(envUrl))
            return new[] { envUrl };

        // 2) в редакторе можно попытаться подключиться к локальному серверу,
        // а затем — к продовому endpoint (если включён fallback)
#if UNITY_EDITOR
        if (overrideUrlInEditor && !string.IsNullOrEmpty(editorUrl))
        {
            if (fallbackToProductionInEditor && !string.IsNullOrEmpty(productionUrl))
                return new[] { editorUrl, productionUrl };

            return new[] { editorUrl };
        }
#endif

        // 3) по умолчанию — продовый wss
        return new[] { productionUrl };
    }

    private string[] candidateUrls = Array.Empty<string>();
    private int currentUrlIndex = 0;

    private bool connectRequested;
    private bool connecting;

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
            connectRequested = true;
            await Connect();
        }
    }

    private void OnEnable()
    {
        applicationQuitting = false;
    }

    // Подключение
    public async Task Connect()
    {
        connectRequested = true;
        if (connecting)
            return;

        candidateUrls = BuildCandidateUrls();
        currentUrlIndex = 0;
        await ConnectToCurrentCandidate();
    }

    private async Task ConnectToCurrentCandidate()
    {
        connecting = true;
        if (candidateUrls == null || candidateUrls.Length == 0)
        {
            Debug.LogError("[WS] No WebSocket URLs configured");
            connecting = false;
            return;
        }

        var url = candidateUrls[Mathf.Clamp(currentUrlIndex, 0, candidateUrls.Length - 1)];

        // если уже был сокет — закрываем
        if (socket != null)
        {
            try
            {
                skipReconnectOnce = true;
                await socket.Close();
            }
            catch { }
            socket = null;
        }

        Debug.Log("[WS] Connecting to: " + url);

        socket = new WebSocket(url);

        socket.OnOpen += () =>
        {
            Debug.Log("[WS] Connected");
            reconnecting = false;
            connecting = false;
            FlushQueue();
        };

        socket.OnError += (e) =>
        {
            Debug.LogError("[WS] Error: " + e);
            connecting = false;
            TryNextCandidateOrReconnect("error");
        };

        socket.OnClose += (code) =>
        {
            Debug.Log("[WS] Closed: " + code);
            connecting = false;
            TryNextCandidateOrReconnect("close");
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
            TryNextCandidateOrReconnect("exception");
            connecting = false;
        }
    }

    private async void TryNextCandidateOrReconnect(string reason)
    {
        // Если есть следующий URL в списке — пробуем его без дополнительной задержки.
        if (candidateUrls != null && currentUrlIndex + 1 < candidateUrls.Length)
        {
            currentUrlIndex++;
            Debug.Log($"[WS] Trying fallback URL: {candidateUrls[currentUrlIndex]}");
            ScheduleConnectToCurrentCandidate();
            return;
        }

        TryScheduleReconnect(reason);
    }

    private void ScheduleConnectToCurrentCandidate()
    {
        if (connecting)
            return;

        _ = ConnectToCurrentCandidateDelayed();
    }

    private async System.Threading.Tasks.Task ConnectToCurrentCandidateDelayed()
    {
        // Разрываем прямую рекурсию по стеку (OnClose/OnError -> Connect -> Close -> ...)
        // чтобы избежать переполнения стека в WebGL.
        await Task.Yield();
        await ConnectToCurrentCandidate();
    }

    private async void TryScheduleReconnect(string reason)
    {
        if (!autoReconnect || applicationQuitting)
            return;

        if (skipReconnectOnce)
        {
            skipReconnectOnce = false;
            return;
        }

        if (reconnecting)
            return;

        reconnecting = true;
        Debug.Log($"[WS] Reconnecting in {reconnectDelaySeconds:0.##}s ({reason})");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(0.5f, reconnectDelaySeconds)));
        }
        catch
        {
            reconnecting = false;
            return;
        }

        reconnecting = false;
        await Connect();
    }

    // ВАЖНО: тут был синтаксический мусор, оставляем только этот вариант
    private async void FlushQueue()
    {
        if (socket == null || socket.State != WebSocketState.Open)
            return;

        while (outgoingQueue.Count > 0)
        {
            var msg = outgoingQueue.Dequeue();
            try
            {
                await socket.SendText(msg);
            }
            catch (Exception ex)
            {
                Debug.LogError("[WS] Flush send exception: " + ex);
                break;
            }
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
            Debug.LogWarning("[WS] Send called, but socket is null (not connected)");
            EnqueueWhileConnecting(message);
            return;
        }

        if (socket.State != WebSocketState.Open)
        {
            Debug.LogWarning("[WS] Send called, but socket state is " + socket.State);
            EnqueueWhileConnecting(message);
            TryScheduleReconnect("send_state=" + socket.State);
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

    private async void EnqueueWhileConnecting(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (outgoingQueue.Count >= queuedMessageLimit)
            outgoingQueue.Dequeue();

        outgoingQueue.Enqueue(message);

        if (!connectRequested && !connecting)
        {
            connectRequested = true;
            await Connect();
        }
    }

    private async void OnApplicationQuit()
    {
        applicationQuitting = true;

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
