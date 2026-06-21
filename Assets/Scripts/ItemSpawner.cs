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

    void Start()
    {
        TrySpawn();
    }

    void Update()
    {
        TrySpawn();
    }

    void TrySpawn()
    {
        if (_spawned) return;
        if (!NetworkServer.active) return;
        _spawned = true;
        SpawnItem();
    }

    void SpawnItem()
    {
        ItemData item = fixedItem != null ? fixedItem : ItemDatabase.GetRandomItem();
        if (item == null)
        {
            Debug.LogWarning($"[ItemSpawner @ {transform.position}] 无法获取物品");
            return;
        }

        int itemId = ItemDatabase.GetItemId(item);
        if (itemId < 0)
        {
            Debug.LogWarning($"[ItemSpawner @ {transform.position}] 物品 {item.itemName} 不在数据库中");
            return;
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
            return;
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
    }
}