using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 MainMenu 中设置网络模式，GameMap 场景加载后由 GameMapAutoStart 读取。
/// </summary>
public static class NetworkConfig
{
    public static bool IsHost { get; set; } = true;
    public static string ServerIP { get; set; } = "localhost";
}

/// <summary>
/// 主菜单 UI：Host 一键开始，Client 输入 IP + Join 按钮。
/// 所有 UI 元素在 Start() 中动态创建，无需手动编辑场景。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    private Button _joinButton;
    private Button _hostButton;
    private Button _clientButton;
    private InputField _ipInputField;
    private GameObject _clientConfigGroup;

    private bool _isHost = true;

    void Start()
    {
        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("[MainMenuUI] 未找到 Canvas！");
            return;
        }

        CreateUI(canvasGo.transform);

        // 绑定事件
        if (_hostButton != null)
            _hostButton.onClick.AddListener(OnHostClicked);
        if (_clientButton != null)
            _clientButton.onClick.AddListener(OnClientClicked);
        if (_joinButton != null)
            _joinButton.onClick.AddListener(OnJoinClicked);

        // 默认 Host 模式——只显示两个主按钮
        UpdateUI();
        Debug.Log("[MainMenuUI] 初始化完成，点击 Host 直接开始，或点击 Client 输入 IP");
    }

    void CreateUI(Transform canvasParent)
    {
        // Host 按钮（点击直接开始游戏）
        _hostButton = CreateButton("HostButton", canvasParent, "Host（主机）", new Vector2(0, 80), new Vector2(220, 60));

        // Client 按钮（点击展开 IP 输入）
        _clientButton = CreateButton("ClientButton", canvasParent, "Client（客户端）", new Vector2(0, 0), new Vector2(220, 60));

        // Client 配置组：IP 输入 + Join 按钮（默认隐藏）
        _clientConfigGroup = CreateUIObject("ClientConfigGroup", canvasParent);
        RectTransform cfgRT = _clientConfigGroup.GetComponent<RectTransform>();
        cfgRT.anchorMin = new Vector2(0.5f, 0.5f);
        cfgRT.anchorMax = new Vector2(0.5f, 0.5f);
        cfgRT.anchoredPosition = new Vector2(0, -80);
        cfgRT.sizeDelta = new Vector2(400, 100);

        // IP 标签
        CreateText("IPLabel", _clientConfigGroup.transform, "Server IP:", new Vector2(-160, 25), new Vector2(100, 30))
            .GetComponent<Text>().alignment = TextAnchor.MiddleRight;

        // IP 输入框
        _ipInputField = CreateInputField("IPInputField", _clientConfigGroup.transform, "localhost", new Vector2(30, 25), new Vector2(200, 30));

        // Join 按钮
        _joinButton = CreateButton("JoinButton", _clientConfigGroup.transform, "Join", new Vector2(0, -30), new Vector2(180, 50));
    }

    #region UI 工厂方法
    GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.6f, 0.9f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.4f, 0.7f, 1f);
        cb.pressedColor = new Color(0.2f, 0.4f, 0.7f);
        btn.colors = cb;

        // 按钮文字
        GameObject textGo = CreateUIObject("Text", go.transform);
        RectTransform textRT = textGo.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        Text text = textGo.AddComponent<Text>();
        text.text = label;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }

    InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = Color.white;

        InputField input = go.AddComponent<InputField>();

        // 文字区域
        GameObject textGo = CreateUIObject("Text", go.transform);
        RectTransform textRT = textGo.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(5, 2);
        textRT.offsetMax = new Vector2(-5, -2);
        Text text = textGo.AddComponent<Text>();
        text.text = placeholder;
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.black;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        input.textComponent = text;

        // Placeholder
        GameObject phGo = CreateUIObject("Placeholder", go.transform);
        RectTransform phRT = phGo.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(5, 2);
        phRT.offsetMax = new Vector2(-5, -2);
        Text phText = phGo.AddComponent<Text>();
        phText.text = "Enter IP...";
        phText.fontSize = 16;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.5f, 0.5f, 0.5f);
        phText.fontStyle = FontStyle.Italic;
        phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        input.placeholder = phText;

        return input;
    }

    GameObject CreateText(string name, Transform parent, string content, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = 16;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return go;
    }
    #endregion

    void UpdateUI()
    {
        // Host 选中时隐藏 Client 配置；Client 选中时显示
        if (_clientConfigGroup != null)
            _clientConfigGroup.SetActive(!_isHost);

        if (_hostButton != null)
            _hostButton.GetComponent<Image>().color = _isHost ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.3f, 0.6f, 0.9f);
        if (_clientButton != null)
            _clientButton.GetComponent<Image>().color = !_isHost ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.3f, 0.6f, 0.9f);
    }

    /// <summary>点击 Host → 直接开始游戏</summary>
    public void OnHostClicked()
    {
        _isHost = true;
        NetworkConfig.IsHost = true;
        NetworkConfig.ServerIP = "localhost";
        UpdateUI();
        Debug.Log("[MainMenuUI] Host 模式 — 直接进入 GameMap");
        SceneManager.LoadScene("GameMap");
    }

    /// <summary>点击 Client → 展开 IP 输入区域</summary>
    public void OnClientClicked()
    {
        _isHost = false;
        UpdateUI();
        Debug.Log("[MainMenuUI] Client 模式 — 请输入服务器 IP 后点击 Join");
    }

    /// <summary>点击 Join → 使用输入的 IP 进入 GameMap</summary>
    public void OnJoinClicked()
    {
        if (_ipInputField != null)
        {
            string ip = _ipInputField.text.Trim();
            if (string.IsNullOrEmpty(ip)) ip = "localhost";
            NetworkConfig.ServerIP = ip;
        }
        NetworkConfig.IsHost = false;
        Debug.Log($"[MainMenuUI] Client Join -> IP={NetworkConfig.ServerIP}");
        SceneManager.LoadScene("GameMap");
    }
}