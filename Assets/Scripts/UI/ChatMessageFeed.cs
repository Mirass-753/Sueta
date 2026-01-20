using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageFeed : MonoBehaviour
{
    public static ChatMessageFeed Instance { get; private set; }

    [Header("Layout")]
    [SerializeField] private Vector2 anchorOffset = new Vector2(16f, -16f);
    [SerializeField] private float maxWidth = 360f;
    [SerializeField] private float messageLifetime = 6f;
    [SerializeField] private int maxMessages = 6;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Return;
    [SerializeField] private int maxMessageLength = 160;

    private readonly List<ChatEntry> entries = new List<ChatEntry>();

    private RectTransform chatRoot;
    private RectTransform messagesRoot;
    private InputField inputField;
    private Font defaultFont;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        EnsureChatUi();
        HideInput();
    }

    private void Update()
    {
        if (inputField == null)
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (!inputField.gameObject.activeSelf)
            {
                ShowInput();
            }
            else
            {
                TrySendMessage();
            }
        }

        if (inputField.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            HideInput();

        CleanupExpiredMessages();
    }

    public void AddMessage(string senderId, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || messagesRoot == null)
            return;

        string safeMessage = message.Trim();
        if (safeMessage.Length > maxMessageLength)
            safeMessage = safeMessage.Substring(0, maxMessageLength);

        if (entries.Count >= maxMessages)
            RemoveEntry(entries[0]);

        var messageObject = new GameObject("ChatMessage", typeof(RectTransform));
        messageObject.transform.SetParent(messagesRoot, false);

        var text = messageObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = $"{FormatSender(senderId)}: {safeMessage}";

        var rect = messageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(maxWidth, 0f);

        var fitter = messageObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var shadow = messageObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(1f, -1f);

        entries.Add(new ChatEntry
        {
            expiresAt = Time.unscaledTime + messageLifetime,
            messageObject = messageObject,
        });
    }

    private void CleanupExpiredMessages()
    {
        if (entries.Count == 0)
            return;

        float now = Time.unscaledTime;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (now < entries[i].expiresAt)
                continue;

            RemoveEntry(entries[i]);
        }
    }

    private void RemoveEntry(ChatEntry entry)
    {
        if (entry.messageObject != null)
            Destroy(entry.messageObject);

        entries.Remove(entry);
    }

    private void TrySendMessage()
    {
        if (inputField == null)
            return;

        string text = inputField.text ?? string.Empty;
        string trimmed = text.Trim();
        inputField.text = string.Empty;

        HideInput();

        if (string.IsNullOrEmpty(trimmed))
            return;

        if (trimmed.Length > maxMessageLength)
            trimmed = trimmed.Substring(0, maxMessageLength);

        var ws = WebSocketClient.Instance;
        if (ws == null)
            return;

        var msg = new NetMessageChat
        {
            id = PlayerController.LocalPlayerId,
            text = trimmed,
        };
        ws.Send(JsonUtility.ToJson(msg));
    }

    private void ShowInput()
    {
        if (inputField == null)
            return;

        inputField.gameObject.SetActive(true);
        inputField.ActivateInputField();
    }

    private void HideInput()
    {
        if (inputField == null)
            return;

        inputField.DeactivateInputField();
        inputField.gameObject.SetActive(false);
    }

    private string FormatSender(string senderId)
    {
        if (string.IsNullOrEmpty(senderId))
            return "Игрок";

        const int shortLength = 6;
        if (senderId.Length <= shortLength)
            return $"Игрок {senderId}";

        return $"Игрок {senderId.Substring(0, shortLength)}";
    }

    private void EnsureChatUi()
    {
        chatRoot = transform.Find("ChatUI") as RectTransform;
        if (chatRoot == null)
        {
            var chatRootObject = new GameObject("ChatUI", typeof(RectTransform));
            chatRoot = chatRootObject.GetComponent<RectTransform>();
            chatRoot.SetParent(transform, false);
        }

        chatRoot.anchorMin = new Vector2(0f, 1f);
        chatRoot.anchorMax = new Vector2(0f, 1f);
        chatRoot.pivot = new Vector2(0f, 1f);
        chatRoot.anchoredPosition = anchorOffset;
        chatRoot.sizeDelta = new Vector2(maxWidth, 0f);

        if (chatRoot.GetComponent<VerticalLayoutGroup>() == null)
        {
            var layout = chatRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.spacing = 6f;
        }

        var rootFitter = chatRoot.GetComponent<ContentSizeFitter>();
        if (rootFitter == null)
            rootFitter = chatRoot.gameObject.AddComponent<ContentSizeFitter>();

        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        messagesRoot = chatRoot.Find("Messages") as RectTransform;
        if (messagesRoot == null)
        {
            var messagesObject = new GameObject("Messages", typeof(RectTransform));
            messagesRoot = messagesObject.GetComponent<RectTransform>();
            messagesRoot.SetParent(chatRoot, false);
        }

        messagesRoot.anchorMin = new Vector2(0f, 1f);
        messagesRoot.anchorMax = new Vector2(0f, 1f);
        messagesRoot.pivot = new Vector2(0f, 1f);
        messagesRoot.sizeDelta = new Vector2(maxWidth, 0f);

        if (messagesRoot.GetComponent<VerticalLayoutGroup>() == null)
        {
            var layout = messagesRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.spacing = 4f;
        }

        var messagesFitter = messagesRoot.GetComponent<ContentSizeFitter>();
        if (messagesFitter == null)
            messagesFitter = messagesRoot.gameObject.AddComponent<ContentSizeFitter>();

        messagesFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        messagesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        inputField = chatRoot.GetComponentInChildren<InputField>(true);
        if (inputField == null)
            inputField = CreateInputField(chatRoot, maxWidth);

        inputField.characterLimit = maxMessageLength;
        inputField.lineType = InputField.LineType.SingleLine;
    }

    private InputField CreateInputField(Transform parent, float width)
    {
        var inputObject = new GameObject("ChatInput", typeof(RectTransform));
        var inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.SetParent(parent, false);
        inputRect.sizeDelta = new Vector2(width, 28f);

        var background = inputObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.35f);

        var input = inputObject.AddComponent<InputField>();
        input.transition = Selectable.Transition.ColorTint;

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(inputObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        var placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
        placeholderObject.transform.SetParent(inputObject.transform, false);
        var placeholder = placeholderObject.AddComponent<Text>();
        placeholder.font = defaultFont;
        placeholder.fontSize = 16;
        placeholder.color = new Color(1f, 1f, 1f, 0.5f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = "Введите сообщение...";

        var placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = new Vector2(0f, 0f);
        placeholderRect.anchorMax = new Vector2(1f, 1f);
        placeholderRect.offsetMin = new Vector2(8f, 2f);
        placeholderRect.offsetMax = new Vector2(-8f, -2f);

        input.textComponent = text;
        input.placeholder = placeholder;

        var layoutElement = inputObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = 28f;

        return input;
    }

    private struct ChatEntry
    {
        public float expiresAt;
        public GameObject messageObject;
    }
}
