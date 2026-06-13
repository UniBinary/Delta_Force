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

        // 避免重复创建
        if (FindObjectOfType<GameMapAutoStart>() != null) return;

        GameObject go = new GameObject("GameMapAutoStart");
        go.AddComponent<GameMapAutoStart>();
        Debug.Log("[GameMapAutoStart] 已自动创建，准备启动网络");
    }

    void Start()
    {
        NetworkManager nm = FindObjectOfType<NetworkManager>();
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

        if (NetworkConfig.IsHost)
        {
            nm.StartHost();
            Debug.Log("[GameMapAutoStart] 已 Host 模式启动");
        }
        else
        {
            nm.networkAddress = NetworkConfig.ServerIP;
            nm.StartClient();
            Debug.Log($"[GameMapAutoStart] 已 Client 模式启动，连接 {NetworkConfig.ServerIP}");
        }

        // 禁用 NetworkManager 默认 HUD + 组件
        nm.showGUI = false;
        var hud = nm.GetComponent<NetworkManagerHUD>();
        if (hud != null)
        {
            hud.enabled = false;
            Object.Destroy(hud);
        }
        Debug.Log("[GameMapAutoStart] 已禁用 NetworkManager 默认 HUD");
    }
}