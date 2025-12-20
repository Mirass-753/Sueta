using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;

/// <summary>
/// Минимальный и безопасный клиент вебсокета для WebGL.
/// Без рекурсивных вызовов Connect() из OnClose/OnError.
/// </summary>
public class WebSocketClient : MonoBehaviour
{
    public static WebSocketClient Instance { get; private set; }

    [Header("Connection")]
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private bool autoReconnect = true;
    [SerializeField, Range(0.5f, 10f)]
    private float reconnectDelaySeconds = 2f;

    [Header("URLs")]
    [Tooltip("Production WebSocket endpoint")]
    [SerializeField]
    private string productionUrl = "wss://catlaw.online/game-ws";

#if UNITY_EDITOR
    [Tooltip("В редакторе использовать локальный сервер вместо продового")]
    [SerializeField] private bool useEditorUrl = true;
    [SerializeField] private string editorUrl = "ws://176.98.176.64:3001";
#endif

    [Header("Queue")]
    [SerializeField, Tooltip("Максимум ожидающих сообщений до соединения")]
    private int queuedMessageLimit = 64;

    private WebSocket socket;
    private bool applicationQuitting;
    private bool connecting;

    // планируемый реконнект
    private bool reconnectScheduled;
    private float reconnectAtTime;

    // флаг, что мы сами закрываем сокет (чтобы OnClose не запускал реконнект)
    private bool closingManually;

    // очередь исходящих пока нет соединения
    private readonly Queue<string> outgoingQueue = new Queue<string>();

    // ---------------- SINGLETON ----------------

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (autoConnect)
            ScheduleReconnect(0f);
    }

    private void OnEnable()
    {
        applicationQuitting = false;
    }

    private void OnDisable()
    {
        // не делаем ничего особенного
    }

    // ---------------- URL ----------------

    private string ResolveUrl()
    {
#if UNITY_EDITOR
        if (useEditorUrl && !string.IsNullOrEmpty(editorUrl))
            return editorUrl;
#endif
        return productionUrl;
    }

    // ---------------- UPDATE ----------------

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        socket?.DispatchMessageQueue();
#endif

        if (reconnectScheduled &&
            !connecting &&
            !applicationQuitting &&
            Time.unscaledTime >= reconnectAtTime)
        {
            reconnectScheduled = false;
            _ = ConnectAsync();
        }
    }

    // ---------------- ПОДКЛЮЧЕНИЕ ----------------

    /// <summary>
    /// Явно попросить подключиться / переподключиться.
    /// </summary>
    public void Connect()
    {
        ScheduleReconnect(0f);
    }

    private void ScheduleReconnect(float delaySeconds)
    {
        if (!autoReconnect && socket != null)
            return;

        reconnectScheduled = true;
        reconnectAtTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
    }

    private async Task ConnectAsync()
    {
        if (connecting || applicationQuitting)
            return;

        connecting = true;

        string url = ResolveUrl();
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("[WS] No URL configured");
            connecting = false;
            return;
        }

        // Закрываем старый сокет, если был
        if (socket != null)
        {
            try
            {
                closingManually = true;
                await socket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WS] Error while closing old socket: " + ex);
            }
            finally
            {
                closingManually = false;
                socket = null;
            }
        }

        Debug.Log("[WS] Connecting to: " + url);
        socket = new WebSocket(url);

        socket.OnOpen += () =>
        {
            Debug.Log("[WS] Connected");
            connecting = false;
            FlushQueue();
        };

        socket.OnError += (e) =>
        {
            Debug.LogError("[WS] Error: " + e);
            connecting = false;

            if (!applicationQuitting && autoReconnect)
                ScheduleReconnect(reconnectDelaySeconds);
        };

        socket.OnClose += (code) =>
        {
            Debug.Log("[WS] Closed: " + code);
            connecting = false;

            if (!applicationQuitting && autoReconnect && !closingManually)
                ScheduleReconnect(reconnectDelaySeconds);
        };

        socket.OnMessage += (bytes) =>
        {
            try
            {
                string msg = Encoding.UTF8.GetString(bytes);
                NetworkMessageHandler.Handle(msg);
            }
            catch (Exception ex)
            {
                Debug.LogError("[WS] OnMessage exception: " + ex);
            }
        };

        try
        {
            await socket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] Connect exception: " + ex);
            connecting = false;

            if (!applicationQuitting && autoReconnect)
                ScheduleReconnect(reconnectDelaySeconds);
        }
    }

    // ---------------- ОТПРАВКА ----------------

    public async void Send(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (socket == null || socket.State != WebSocketState.Open)
        {
            EnqueueWhileDisconnected(message);
            if (!connecting && !applicationQuitting)
                ScheduleReconnect(0f);

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

    private async void EnqueueWhileDisconnected(string message)
    {
        if (outgoingQueue.Count >= queuedMessageLimit)
            outgoingQueue.Dequeue();

        outgoingQueue.Enqueue(message);

        if (!connecting && !applicationQuitting)
            ScheduleReconnect(0f);

        // маленький Yield чтобы не забивать кадр
        await Task.Yield();
    }

    private async void FlushQueue()
    {
        if (socket == null || socket.State != WebSocketState.Open)
            return;

        while (outgoingQueue.Count > 0)
        {
            string msg = outgoingQueue.Dequeue();
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

    // ---------------- ЗАКРЫТИЕ ----------------

    private async void OnApplicationQuit()
    {
        applicationQuitting = true;

        if (socket != null)
        {
            try
            {
                closingManually = true;
                await socket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError("[WS] Close on quit exception: " + ex);
            }
            finally
            {
                closingManually = false;
                socket = null;
            }
        }
    }
}
