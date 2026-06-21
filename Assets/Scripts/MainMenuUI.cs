using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 跨场景网络配置传递（静态类，不依赖 MonoBehaviour）
/// </summary>
public static class NetworkConfig
{
    public static bool IsHost { get; set; } = true;
    public static string ServerIP { get; set; } = "localhost";
    public static ushort Port { get; set; } = 7777;
}

/// <summary>
/// 主菜单 UI：Host / Client 选择 + IP / Port 输入
/// 完全由场景中预制的 Canvas UI 提供，代码仅负责逻辑绑定。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] Button _hostButton;
    [SerializeField] Button _clientButton;
    [SerializeField] Button _joinButton;
    [SerializeField] InputField _ipInputField;
    [SerializeField] InputField _portInputField;
    bool _isHost = true;

    /// <summary>编辑器/MCP 调用，设置 UI 引用并绑定事件</summary>
    public void SetReferences(Button host, Button client, Button join, InputField ip, InputField port)
    {
        _hostButton = host;
        _clientButton = client;
        _joinButton = join;
        _ipInputField = ip;
        _portInputField = port;
    }

    void Start()
    {
        if (_hostButton != null) _hostButton.onClick.AddListener(OnHostClicked);
        if (_clientButton != null) _clientButton.onClick.AddListener(OnClientClicked);
        if (_joinButton != null) _joinButton.onClick.AddListener(OnJoinClicked);
        UpdateUI();
    }

    /// <summary>解析端口号，无效时返回默认值 7777</summary>
    ushort ParsePort(InputField field)
    {
        if (field != null && ushort.TryParse(field.text.Trim(), out ushort port))
            return port;
        Debug.LogWarning($"[MainMenuUI] 端口 \"{field?.text}\" 无效，使用默认 7777");
        return 7777;
    }

    /// <summary>解析 IP 地址，空则返回 localhost</summary>
    string ParseIP(InputField field)
    {
        if (field == null) return "localhost";
        string ip = field.text.Trim();
        return string.IsNullOrEmpty(ip) ? "localhost" : ip;
    }

    void UpdateUI()
    {
        if (_hostButton != null)
            _hostButton.GetComponent<Image>().color = _isHost
                ? new Color(0.2f, 0.7f, 0.3f)
                : new Color(0.3f, 0.6f, 0.9f);
        if (_clientButton != null)
            _clientButton.GetComponent<Image>().color = !_isHost
                ? new Color(0.2f, 0.7f, 0.3f)
                : new Color(0.3f, 0.6f, 0.9f);
    }

    /// <summary>点击 Host → 读取 IP/Port → 进入 GameMap</summary>
    public void OnHostClicked()
    {
        _isHost = true;
        NetworkConfig.IsHost = true;
        NetworkConfig.ServerIP = ParseIP(_ipInputField);
        NetworkConfig.Port = ParsePort(_portInputField);
        UpdateUI();
        Debug.Log($"[MainMenuUI] Host 模式 IP={NetworkConfig.ServerIP} Port={NetworkConfig.Port} → 进入 GameMap");
        SceneManager.LoadScene("GameMap");
    }

    /// <summary>点击 Client → 仅切换选中状态</summary>
    public void OnClientClicked()
    {
        _isHost = false;
        UpdateUI();
        Debug.Log("[MainMenuUI] Client 模式 — 请输入服务器 IP/端口后点击 Join");
    }

    /// <summary>点击 Join → 读取 IP/Port → 进入 GameMap</summary>
    public void OnJoinClicked()
    {
        NetworkConfig.IsHost = false;
        NetworkConfig.ServerIP = ParseIP(_ipInputField);
        NetworkConfig.Port = ParsePort(_portInputField);
        Debug.Log($"[MainMenuUI] Client Join -> IP={NetworkConfig.ServerIP} Port={NetworkConfig.Port}");
        SceneManager.LoadScene("GameMap");
    }
}