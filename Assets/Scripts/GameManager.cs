using System.Collections;
using Mirror;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏全局管理器：处理玩家死亡/撤离、观战切换、场景跳转。
/// 挂载在 GameMap 场景的 GameManager 对象上。
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // 监听玩家连接/断开，用于判断场上是否还有其他人
        Debug.Log("[GameManager] 服务端已就绪");
    }

    // ============================================================
    // 玩家死亡 — 撤离失败
    // ============================================================
    [Server]
    public void OnPlayerDied(Player deadPlayer)
    {
        Debug.Log($"[GameManager] 玩家死亡: netId={deadPlayer.netId}");
        deadPlayer.TargetHandleDeath(false);
        // 延迟检查：等待 TargetRpc 处理后，检查是否所有观战者也需要跳转
        StartCoroutine(CheckSpectatorsAfterDelay(false));
    }

    // ============================================================
    // 玩家撤离成功（撤离点读秒完成后调用）
    // ============================================================
    [Server]
    public void OnPlayerEvacuated(Player evacuatedPlayer)
    {
        Debug.Log($"[GameManager] 玩家撤离成功: netId={evacuatedPlayer.netId}");
        // 标记该玩家不再存活，使 GetAlivePlayerCount 不会计入
        evacuatedPlayer.health = 0;
        evacuatedPlayer.TargetHandleDeath(true);
        StartCoroutine(CheckSpectatorsAfterDelay(true));
    }

    // ============================================================
    // 延迟检查：当场上没有存活玩家时，通知所有观战者跳转场景
    // ============================================================
    [Server]
    IEnumerator CheckSpectatorsAfterDelay(bool wasEvacuation)
    {
        yield return new WaitForSeconds(0.5f);
        if (GetAlivePlayerCount() == 0)
        {
            string scene = wasEvacuation ? "EvcSucceeded" : "EvcFailed";
            Debug.Log($"[GameManager] 所有玩家已离开，通知观战者跳转 → {scene}");
            foreach (Player p in FindObjectsOfType<Player>())
            {
                if (p.isSpectating)
                {
                    p.TargetGoToScene(scene);
                }
            }
        }
    }

    // ============================================================
    // 获取场上除 exclude 外还活着的玩家数量
    // ============================================================
    [Server]
    public int GetAlivePlayerCount(Player exclude = null)
    {
        int count = 0;
        foreach (Player p in FindObjectsOfType<Player>())
        {
            if (p == exclude) continue;
            if (p.health > 0) count++;
        }
        return count;
    }

    // ============================================================
    // 随机获取一名还活着的玩家（排除 exclude）
    // ============================================================
    [Server]
    public Player GetRandomAlivePlayer(Player exclude)
    {
        var alive = FindObjectsOfType<Player>()
            .Where(p => p.health > 0 && p != exclude)
            .ToArray();
        if (alive.Length == 0) return null;
        return alive[Random.Range(0, alive.Length)];
    }

    // ============================================================
    // 服务端：当前玩家转为观战指定目标
    // ============================================================
    [Server]
    public void StartSpectating(Player spectator, Player target)
    {
        Debug.Log($"[GameManager] {spectator.netId} 开始观战 {target.netId}");
        spectator.RpcEnableSpectatorCamera(target.netIdentity);
    }

    // ============================================================
    // 客户端：断开连接并切换到结果场景
    // ============================================================
    public static void DisconnectAndGoToScene(string sceneName)
    {
        // 如果是 Host，先停掉 NetworkServer；如果是 Client，断开连接
        if (NetworkServer.active)
        {
            NetworkServer.Shutdown();
        }
        if (NetworkClient.active)
        {
            NetworkClient.Shutdown();
        }

        // 清空静态 Instance，避免切换场景后残留引用
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }

        // 销毁 NetworkManager 单例（有 DontDestroyOnLoad），确保下次进入
        // 场景时创建全新实例，避免旧 GameMapAutoStart 残留引用
        if (NetworkManager.singleton != null)
        {
            Destroy(NetworkManager.singleton.gameObject);
        }

        SceneManager.LoadScene(sceneName);
    }
}
