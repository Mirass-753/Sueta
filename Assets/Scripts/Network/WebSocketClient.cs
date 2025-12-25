using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;

/// <summary>
/// Минимальный и безопасный WebSocket клиент для Unity (Editor/WebGL).
/// - Таймаут коннекта ждёт именно OnOpen (а не завершение socket.Connect())
/// - Без рекурсивных Connect() из OnClose/OnError
/// - Очередь исходящих сообщений до соединения
/// </summary>
public class WebSocketClient : MonoBehaviour
{
    public static WebSocketClient Instance { get; private set; }
    public event Action OnConnected;

    [Header("Connection")]
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private bool autoReconnect = true;

    [SerializeField, Range(1f, 30f)]
    private float connectTimeoutSeconds = 8f;

    [SerializeField, Range(0.5f, 10f)]
    private float reconnectDelaySeconds = 2f;

    [Header("URLs")]
    [Tooltip("Production WebSocket endpoint")]
    [SerializeField] private string productionUrl = "wss://catlaw.online/game-ws";

#if UNITY_EDITOR
    [Tooltip("В редакторе использовать локальный сервер вместо продового")]
    [SerializeField] private bool useEditorUrl = true;

    [Tooltip("Локальный WS endpoint в редакторе")]
    [SerializeField] private string editorUrl = "ws://127.0.0.1:3000/game-ws";
#endif

    [Header("Queue")]
    [SerializeField, Tooltip("Максимум ожидающих сообщений до соединения")]
    private int queuedMessageLimit = 64;

    private WebSocket socket;

    private bool applicationQuitting;
    private bool connecting;
    private bool everConnected;

    // планируемый реконнект
    private bool reconnectScheduled;
    private float reconnectAtTime;

    // флаг, что мы сами закрываем сокет (чтобы OnClose не запускал реконнект)
    private bool closingManually;

    // очередь исходящих пока нет соединения
    private readonly Queue<string> outgoingQueue = new Queue<string>();

    public bool IsConnected => socket != null && socket.State == WebSocketState.Open;
    public bool IsOpen => socket != null && socket.State == WebSocketState.Open;

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

    // ---------------- URL ----------------

    private string ResolveUrl()
    {
#if UNITY_EDITOR
        if (useEditorUrl && !string.IsNullOrEmpty(editorUrl))
            return editorUrl;
#endif
        return productionUrl;
    }

    private bool IsEditorLocalUrl(string url)
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(url))
            return false;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.IsLoopback;
#endif
        return false;
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

        bool isEditorLocalUrl = IsEditorLocalUrl(url);

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
                Debug.LogWarning("[WS] Error while closing old socket: " + ex.Message);
            }
            finally
            {
                closingManually = false;
                socket = null;
            }
        }

        Debug.Log("[WS] Connecting to: " + url);

        socket = new WebSocket(url);

        // Важно: ждём OnOpen, а не завершение socket.Connect()
        var openTcs = new TaskCompletionSource<bool>();

        // Подписки на события (NativeWebSocket типы делегатов!)
        socket.OnOpen += () =>
        {
            Debug.Log("[WS] Connected");
            connecting = false;
            everConnected = true;

            openTcs.TrySetResult(true);

            FlushQueue();
            OnConnected?.Invoke();
        };

        socket.OnError += (e) =>
        {
            if (isEditorLocalUrl)
                Debug.LogWarning("[WS] Error (editor local): " + e);
            else
                Debug.LogError("[WS] Error: " + e);

            connecting = false;

            // если мы ещё ждали открытия — завершаем ожидание с ошибкой
            if (!openTcs.Task.IsCompleted)
                openTcs.TrySetException(new Exception("WS error: " + e));

            if (!applicationQuitting && autoReconnect && !(isEditorLocalUrl && !everConnected))
                ScheduleReconnect(reconnectDelaySeconds);
        };

        socket.OnClose += (code) =>
        {
            Debug.Log("[WS] Closed: " + code);
            connecting = false;

            // если закрылись до открытия — считаем это ошибкой открытия
            if (!openTcs.Task.IsCompleted)
                openTcs.TrySetException(new Exception("WS closed before open. Code=" + code));

            if (!applicationQuitting && autoReconnect && !closingManually &&
                !(isEditorLocalUrl && !everConnected))
            {
                ScheduleReconnect(reconnectDelaySeconds);
            }
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
            Debug.Log("[WS] Connect() start, url=" + url);

            // Не await: на некоторых платформах Task может не завершаться,
            // хотя OnOpen уже пришёл.
            _ = socket.Connect();

            var finished = await Task.WhenAny(openTcs.Task, Task.Delay(TimeSpan.FromSeconds(connectTimeoutSeconds)));
            if (finished != openTcs.Task)
            {
                Debug.LogError($"[WS] Open timeout ({connectTimeoutSeconds:0.#}s). Check server/proxy/path.");
                connecting = false;

                // Закрываем сокет вручную без автопереподключения из OnClose
                try
                {
                    closingManually = true;
                    await socket.Close();
                }
                catch { /* ignore */ }
                finally
                {
                    closingManually = false;
                    socket = null;
                }

                if (!applicationQuitting && autoReconnect && !(isEditorLocalUrl && !everConnected))
                    ScheduleReconnect(reconnectDelaySeconds);

                return;
            }

            // Если openTcs завершился ошибкой — пробросим
            await openTcs.Task;

            Debug.Log("[WS] Open confirmed. State=" + (socket != null ? socket.State.ToString() : "null"));
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] Connect exception: " + ex);
            connecting = false;

            if (!applicationQuitting && autoReconnect && !(isEditorLocalUrl && !everConnected))
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
