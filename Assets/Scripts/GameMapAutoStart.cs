using Mirror;
using UnityEngine;

/// <summary>
/// 当 GameMap 场景加载时，自动以 Host 模式启动 NetworkManager。
/// 这样从主菜单进入游戏后无需手动操作即可开始。
/// </summary>
public class GameMapAutoStart : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name != "GameMap") return;

        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null) return;

        // 如果尚未启动，以 Host 模式启动（Server + Client 合一，适合单机/本地测试）
        if (nm.mode == NetworkManagerMode.Offline)
        {
            nm.StartHost();
            Debug.Log("[GameMapAutoStart] NetworkManager 已自动以 Host 模式启动");
        }
    }
}