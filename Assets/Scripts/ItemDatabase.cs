using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局物品数据库。挂在场景中的 GameManager GameObject 上。
/// 提供静态查询接口，所有 Inventory 通过此类获取 ItemData。
/// </summary>
public class ItemDatabase : MonoBehaviour
{
    [Header("物品列表（索引 = itemId）")]
    public ItemData[] allItems;

    private static Dictionary<int, ItemData> _lookup;
    private static bool _initialized;

    void Awake()
    {
        BuildLookup();
    }

    void BuildLookup()
    {
        _lookup = new Dictionary<int, ItemData>();
        for (int i = 0; i < allItems.Length; i++)
            if (allItems[i] != null)
                _lookup[i] = allItems[i];
        _initialized = true;
    }

    /// <summary>
    /// 根据 itemId 获取物品数据。未初始化或 ID 无效返回 null。
    /// </summary>
    public static ItemData GetItemData(int itemId)
    {
        if (!_initialized || _lookup == null) return null;
        if (itemId < 0 || !_lookup.TryGetValue(itemId, out var item)) return null;
        return item;
    }

    /// <summary>
    /// 判断 itemId 是否合法（存在于数据库中）。
    /// </summary>
    public static bool IsValidItemId(int itemId)
    {
        return _initialized && _lookup != null && _lookup.ContainsKey(itemId);
    }

    /// <summary>
    /// 从数据库中随机获取一个物品。
    /// </summary>
    public static ItemData GetRandomItem()
    {
        if (!_initialized || _lookup == null || _lookup.Count == 0) return null;
        // 收集所有有效索引
        var ids = new System.Collections.Generic.List<int>(_lookup.Keys);
        int randomId = ids[Random.Range(0, ids.Count)];
        return _lookup[randomId];
    }

    /// <summary>
    /// 根据 ItemData 反向查找其 itemId（索引）。未找到返回 -1。
    /// </summary>
    public static int GetItemId(ItemData item)
    {
        if (!_initialized || _lookup == null || item == null) return -1;
        foreach (var kv in _lookup)
            if (kv.Value == item)
                return kv.Key;
        return -1;
    }
}
