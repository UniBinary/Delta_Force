using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameMap 场景加载后自动创建，根据 MainMenu 设置的 NetworkConfig 启动网络。
/// </summary>
public class GameMapAutoStart : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnSceneLoaded()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "GameMap") return;
    }

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

        if (NetworkConfig.IsHost)
        {
            nm.StartHost();
            Debug.Log($"[GameMapAutoStart] Host 模式启动 — IP={NetworkConfig.ServerIP} Port={NetworkConfig.Port}");
        }
        else
        {
            nm.StartClient();
            Debug.Log($"[GameMapAutoStart] Client 模式启动 — 连接 {NetworkConfig.ServerIP}:{NetworkConfig.Port}");
        }
    }
}