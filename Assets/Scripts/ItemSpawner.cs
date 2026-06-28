using Mirror;
using UnityEngine;

/// <summary>
/// 物品生成器。游戏开始时在自身位置随机生成一个物品。
/// 挂在一个只有 Transform 和本组件的空 GameObject 上。
/// 每次进入游戏时自动在服务端生成一个新物品。
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Header("固定物品（留空则从 ItemDatabase 随机）")]
    public ItemData fixedItem;

    private bool _spawned;
    private float _startTime;

    void Start()
    {
        _startTime = Time.time;
    }

    void Update()
    {
        TrySpawn();
    }

    void TrySpawn()
    {
        if (_spawned) return;
        if (!NetworkServer.active) return;

        // 超时保护：5 秒后放弃，防止无限重试
        if (Time.time - _startTime > 5f)
        {
            Debug.LogWarning($"[ItemSpawner @ {transform.position}] 超时放弃生成，请检查 ItemDatabase 是否正确配置");
            _spawned = true;
            return;
        }

        if (!SpawnItem()) return; // 失败则下一帧重试
        _spawned = true;
    }

    // 返回 true 表示生成成功
    bool SpawnItem()
    {
        ItemData item = fixedItem != null ? fixedItem : ItemDatabase.GetRandomItem();
        if (item == null)
        {
            return false; // ItemDatabase 尚未就绪，静默重试
        }

        int itemId = ItemDatabase.GetItemId(item);
        if (itemId < 0)
        {
            Debug.LogWarning($"[ItemSpawner @ {transform.position}] 物品 {item.itemName} 不在数据库中");
            return true; // 固定物品不存在也视为"完成"，避免无限重试
        }

        // 从 NetworkManager 的 spawnPrefabs 中查找 "Item" prefab
        GameObject itemPrefab = null;
        if (NetworkManager.singleton != null)
        {
            foreach (var prefab in NetworkManager.singleton.spawnPrefabs)
            {
                if (prefab != null && prefab.name == "Item")
                {
                    itemPrefab = prefab;
                    break;
                }
            }
        }

        if (itemPrefab == null)
        {
            Debug.LogError("[ItemSpawner] 未在 NetworkManager.spawnPrefabs 中找到名为 'Item' 的 prefab！");
            return true; // 致命错误，标记完成避免无限重试
        }

        GameObject go = Instantiate(itemPrefab, transform.position, transform.rotation);
        go.name = $"Item_{item.itemName}";

        // 设置 PickupItem.itemId
        PickupItem pickup = go.GetComponent<PickupItem>();
        if (pickup != null) pickup.itemId = itemId;

        // 设置 Sprite
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && item.icon != null) sr.sprite = item.icon;

        // 网络生成（客户端也能看到）
        NetworkServer.Spawn(go);
        return true;
    }
}