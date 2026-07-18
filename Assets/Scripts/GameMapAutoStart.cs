using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameMap 场景加载后自动创建，根据 MainMenu 设置的 NetworkConfig 启动网络。
/// 支持 Host / Client / Server 三种模式。
/// </summary>
public class GameMapAutoStart : MonoBehaviour
{
    private GameObject _serverCloseButton;

    void Start()
    {
        NetworkManager nm = gameObject.GetComponent<NetworkManager>();
        if (nm == null)
        {
            Debug.LogError("[GameMapAutoStart] 未找到 NetworkManager！");
            return;
        }

        if (nm.mode != NetworkManagerMode.Offline)
        {
            Debug.Log("[GameMapAutoStart] NetworkManager 已在运行，跳过自动启动");
            return;
        }

        // 将 IP 和 Port 设置到 NetworkManager
        nm.networkAddress = NetworkConfig.ServerIP;

        // 通过 PortTransport 接口设置端口（KCP / Telepathy 均支持）
        if (nm.transport is PortTransport portTransport)
        {
            portTransport.Port = NetworkConfig.Port;
            Debug.Log($"[GameMapAutoStart] Transport 端口已设为 {NetworkConfig.Port}");
        }
        else
        {
            Debug.LogWarning($"[GameMapAutoStart] 当前 Transport 不支持 PortTransport 接口，无法设置端口");
        }

        if (NetworkConfig.IsServer)
        {
            // ============================================================
            // 纯服务器模式：无本地玩家，仅接受客户端连接
            // ============================================================
            nm.StartServer();
            Debug.Log($"[GameMapAutoStart] Server 模式启动 — Port={NetworkConfig.Port}");

            // 设置服务器端 UI
            SetupServerUI();

            // 初始启用场景摄像机（无玩家时）
            EnableAllSceneCameras();
        }
        else if (NetworkConfig.IsHost)
        {
            // ============================================================
            // Host 模式：服务器 + 本地玩家
            // ============================================================
            nm.StartHost();
            Debug.Log($"[GameMapAutoStart] Host 模式启动 — IP={NetworkConfig.ServerIP} Port={NetworkConfig.Port}");

            // 直接禁用 MainCamera（本地玩家有自己的摄像机）
            var mc = GameObject.Find("MainCamera");
            if (mc != null) mc.SetActive(false);
        }
        else
        {
            // ============================================================
            // Client 模式：连接远程服务器
            // ============================================================
            nm.StartClient();
            Debug.Log($"[GameMapAutoStart] Client 模式启动 — 连接 {NetworkConfig.ServerIP}:{NetworkConfig.Port}");

            // 直接禁用 MainCamera（客户端玩家有自己的摄像机）
            var mc = GameObject.Find("MainCamera");
            if (mc != null) mc.SetActive(false);
        }

        // 强制客户端就绪（确保 [Command] 可以正常发送）
        StartCoroutine(AutoReadyCoroutine());
    }

    System.Collections.IEnumerator AutoReadyCoroutine()
    {
        yield return new WaitUntil(() => NetworkClient.active);
        yield return new WaitForSeconds(0.5f);
        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();
            Debug.Log("[GameMapAutoStart] 已强制调用 NetworkClient.Ready()");
        }
        else
        {
            Debug.Log("[GameMapAutoStart] NetworkClient 已就绪");
        }
    }

    // ============================================================
    // 摄像机管理（仅 Server 模式使用）
    // ============================================================

    /// <summary>
    /// 禁用场景中所有摄像机（Server 模式用，包括玩家摄像机）。
    /// </summary>
    void DisableAllCameras()
    {
        Camera[] allCameras = FindObjectsOfType<Camera>(true);
        int count = 0;
        foreach (Camera cam in allCameras)
        {
            if (cam.GetComponentInParent<Canvas>() != null) continue;

            cam.gameObject.SetActive(false);
            count++;
            Debug.Log($"[GameMapAutoStart] 禁用摄像机: {cam.gameObject.name}" +
                (cam.GetComponentInParent<Player>() != null ? " (玩家)" : ""));
        }
        Debug.Log($"[GameMapAutoStart] 共禁用 {count} 个摄像机");
    }

    /// <summary>
    /// 启用场景中所有非玩家摄像机（Server 模式初始用）。
    /// </summary>
    void EnableAllSceneCameras()
    {
        Camera[] allCameras = FindObjectsOfType<Camera>(true);
        int count = 0;
        foreach (Camera cam in allCameras)
        {
            if (cam.GetComponentInParent<Player>() != null) continue;
            if (cam.GetComponentInParent<Canvas>() != null) continue;

            cam.gameObject.SetActive(true);
            count++;
            Debug.Log($"[GameMapAutoStart] 启用场景摄像机: {cam.gameObject.name}");
        }
        Debug.Log($"[GameMapAutoStart] 共启用 {count} 个场景摄像机");
    }

    // ============================================================
    // 服务器 UI 管理
    // ============================================================

    /// <summary>
    /// 创建服务器专用 UI：右下角"关闭服务器"按钮，并禁用其他 UI
    /// </summary>
    void SetupServerUI()
    {
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null)
        {
            Debug.LogError("[GameMapAutoStart] 未找到 Canvas，无法创建服务器 UI");
            return;
        }

        // 禁用所有现有 UI 子物体（除了我们将要创建的服务端按钮）
        foreach (Transform child in canvasObj.transform)
        {
            child.gameObject.SetActive(false);
        }

        CreateServerCloseButton(canvasObj);
    }

    /// <summary>
    /// 在 Canvas 右下角创建"关闭服务器"按钮
    /// </summary>
    void CreateServerCloseButton(GameObject canvasObj)
    {
        _serverCloseButton = new GameObject("ServerCloseButton", typeof(RectTransform));
        _serverCloseButton.transform.SetParent(canvasObj.transform, false);

        RectTransform rt = _serverCloseButton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-20f, 20f);
        rt.sizeDelta = new Vector2(160f, 50f);

        Image btnImage = _serverCloseButton.AddComponent<Image>();
        btnImage.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);

        Button btn = _serverCloseButton.AddComponent<Button>();
        btn.onClick.AddListener(OnServerCloseClicked);

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(_serverCloseButton.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.text = "关闭服务器";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        _serverCloseButton.transform.SetAsLastSibling();

        Debug.Log("[GameMapAutoStart] 服务器关闭按钮已创建");
    }

    /// <summary>
    /// 点击"关闭服务器" → 断开连接并返回主菜单
    /// </summary>
    void OnServerCloseClicked()
    {
        Debug.Log("[GameMapAutoStart] 关闭服务器，返回主菜单");

        if (NetworkServer.active)
        {
            NetworkServer.Shutdown();
        }
        if (NetworkClient.active)
        {
            NetworkClient.Shutdown();
        }

        if (GameManager.Instance != null)
        {
            Destroy(GameManager.Instance.gameObject);
        }

        // 销毁 NetworkManager 单例（有 DontDestroyOnLoad）
        Destroy(gameObject);

        SceneManager.LoadScene("MainMenu");
    }

    // ============================================================
    // 每帧更新：Server 模式下根据玩家数量控制场景摄像机
    // ============================================================

    void Update()
    {
        if (!NetworkConfig.IsServer) return;

        bool hasPlayers = false;
        Player[] players = FindObjectsOfType<Player>();
        foreach (Player p in players)
        {
            if (p.health > 0)
            {
                hasPlayers = true;
                break;
            }
        }

        // 有玩家 → 禁用所有摄像机（含玩家摄像机）；无玩家 → 启用场景摄像机
        if (hasPlayers)
            DisableAllCameras();
        else
            EnableAllSceneCameras();
    }
}