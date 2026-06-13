using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 装备槽类型
/// </summary>
public enum EquipmentSlotType
{
    Helmet = 0,
    Armor = 1,
    Weapon = 2,
    ChestRig = 3,
    Backpack = 4
}

/// <summary>
/// 所有槽位的物品 ID 数据（序列化用）
/// </summary>
[Serializable]
public class InventoryData
{
    public int helmetItemId = -1;
    public int armorItemId = -1;
    public int[] weaponItemIds = new int[2] { -1, -1 };
    public int[] chestRigItemIds = new int[5] { -1, -1, -1, -1, -1 };
    public int[] backpackItemIds = new int[5] { -1, -1, -1, -1, -1 };
}

/// <summary>
/// 玩家装备/背包系统。挂在 Player 上。
/// 服务端权威，通过 SyncVar (JSON) 同步给所有客户端。
/// </summary>
public class Inventory : NetworkBehaviour
{
    [Header("物品数据库")]
    public ItemData[] allItems;   // 所有可用物品（索引即 itemId）

    [Header("地面物品检测")]
    public float pickupRadius = 2f;
    public LayerMask pickupLayer = -1;

    // 服务端数据
    private InventoryData _data = new InventoryData();

    // 同步给客户端的 JSON
    [SyncVar(hook = nameof(OnInventorySync))]
    private string _syncedInventoryJson = "";

    // 客户端缓存
    private InventoryUI _ui;
    private bool _uiSearched;
    private Dictionary<int, ItemData> _itemLookup;

    #region Startup

    public override void OnStartServer()
    {
        base.OnStartServer();
        BuildLookup();
        _syncedInventoryJson = JsonUtility.ToJson(_data);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        BuildLookup();
        if (!string.IsNullOrEmpty(_syncedInventoryJson))
            ApplyInventoryJson(_syncedInventoryJson);
    }

    /// <summary>
    /// 查找 InventoryUI（可找到禁用的 GameObject）
    /// </summary>
    void EnsureUI()
    {
        if (_uiSearched) return;
        _uiSearched = true;

        // FindObjectOfType 和 GameObject.Find 都找不到禁用物体
        // 用 FindObjectsOfTypeAll 可以找到包括隐藏的所有对象
        var all = Resources.FindObjectsOfTypeAll<InventoryUI>();
        foreach (var ui in all)
        {
            // 过滤掉 Prefab 资源，只保留场景中的实例
            if (ui.gameObject.scene.IsValid())
            {
                _ui = ui;
                break;
            }
        }
    }

    void BuildLookup()
    {
        _itemLookup = new Dictionary<int, ItemData>();
        for (int i = 0; i < allItems.Length; i++)
            if (allItems[i] != null)
                _itemLookup[i] = allItems[i];
    }

    #endregion

    #region Input

    void Update()
    {
        // 不管是不是本地玩家，都输出调试信息（诊断用）
        if (Input.GetKeyDown(KeyCode.F))
            Debug.Log($"[Inventory] F键按下 isLocalPlayer={isLocalPlayer} netId={netId}");

        if (!isLocalPlayer) return;

        // Tab 开关背包
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            EnsureUI();
            if (_ui != null)
            {
                _ui.SetInventory(this);
                _ui.Toggle();
            }
        }

        // F 捡物品
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryPickup();
        }
    }

    #endregion

    #region Pickup

    void TryPickup()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pickupRadius, pickupLayer);
        Debug.Log($"[Inventory] F键检测: 位置={transform.position} 半径={pickupRadius} 层={pickupLayer.value} 命中数={hits.Length}");
        foreach (var hit in hits)
        {
            var pickup = hit.GetComponent<PickupItem>();
            Debug.Log($"[Inventory] 命中物体: {hit.name} pickup={pickup != null} itemId={pickup?.itemId}");
            if (pickup != null)
            {
                CmdPickupItem(pickup.netId);
                return;
            }
        }
    }

    [Command]
    public void CmdPickupItem(uint pickupNetId)
    {
        if (!NetworkServer.spawned.TryGetValue(pickupNetId, out NetworkIdentity ident))
        {
            Debug.LogWarning($"[Inventory] 找不到 netId={pickupNetId}");
            return;
        }
        PickupItem pickup = ident.GetComponent<PickupItem>();
        if (pickup == null || pickup.itemId < 0)
        {
            Debug.LogWarning($"[Inventory] pickup 为空或 itemId 无效: itemId={pickup?.itemId}");
            return;
        }

        ItemData theItem = pickup.itemId >= 0 && pickup.itemId < allItems.Length
            ? allItems[pickup.itemId] : null;
        Debug.Log($"[Inventory] 捡起 itemId={pickup.itemId} name={theItem?.itemName} type={theItem?.itemType}");

        // 尝试放进对应类型槽位
        if (!TryAddItem(pickup.itemId))
        {
            Debug.LogWarning($"[Inventory] TryAddItem 失败（包满或类型不匹配）");
            return;
        }

        // 从世界移除
        Debug.Log($"[Inventory] 服务端移除物品, 当前背包: {JsonUtility.ToJson(_data)}");
        NetworkServer.Destroy(ident.gameObject);
    }

    #endregion

    #region Slot Management (Server)

    /// <summary>
    /// 尝试把物品放进合适的槽位。成功返回 true。
    /// </summary>
    bool TryAddItem(int itemId)
    {
        if (!_itemLookup.ContainsKey(itemId)) return false;
        ItemData item = _itemLookup[itemId];

        switch (item.itemType)
        {
            case ItemType.Helmet:
                if (_data.helmetItemId >= 0) return false; // 已有头盔
                _data.helmetItemId = itemId;
                Sync();
                return true;

            case ItemType.Armor:
                if (_data.armorItemId >= 0) return false;
                _data.armorItemId = itemId;
                Sync();
                return true;

            case ItemType.Weapon:
                // 找空的武器槽
                for (int i = 0; i < 2; i++)
                {
                    if (_data.weaponItemIds[i] < 0)
                    {
                        _data.weaponItemIds[i] = itemId;
                        Sync();
                        return true;
                    }
                }
                return false;

            case ItemType.Ammo:
                // 弹药：先胸挂，后背包
                for (int i = 0; i < 5; i++)
                    if (_data.chestRigItemIds[i] < 0) { _data.chestRigItemIds[i] = itemId; Sync(); return true; }
                for (int i = 0; i < 5; i++)
                    if (_data.backpackItemIds[i] < 0) { _data.backpackItemIds[i] = itemId; Sync(); return true; }
                return false;

            case ItemType.Item:
                // 变卖物：只进背包
                for (int i = 0; i < 5; i++)
                    if (_data.backpackItemIds[i] < 0) { _data.backpackItemIds[i] = itemId; Sync(); return true; }
                return false;
        }
        return false;
    }

    #endregion

    #region Commands

    [Command]
    public void CmdEquipItem(int itemId, EquipmentSlotType slotType, int slotIndex)
    {
        if (!_itemLookup.ContainsKey(itemId)) return;
        ItemData item = _itemLookup[itemId];

        // 检查物品类型是否匹配槽位
        if (!CanPlaceInSlot(item.itemType, slotType)) return;

        // 先从旧位置移除
        RemoveFromAll(itemId);

        // 放到新槽位
        SetSlot(slotType, slotIndex, itemId);
        Sync();
    }

    [Command]
    public void CmdUnequip(EquipmentSlotType slotType, int slotIndex)
    {
        int oldId = GetSlot(slotType, slotIndex);
        SetSlot(slotType, slotIndex, -1);
        Sync();
    }

    [Command]
    public void CmdSwapSlots(EquipmentSlotType typeA, int idxA, EquipmentSlotType typeB, int idxB)
    {
        int idA = GetSlot(typeA, idxA);
        int idB = GetSlot(typeB, idxB);

        // 检查目标槽能否放 idA，源槽能否放 idB
        if (idA >= 0 && !CanPlaceInSlot(_itemLookup[idA].itemType, typeB)) return;
        if (idB >= 0 && !CanPlaceInSlot(_itemLookup[idB].itemType, typeA)) return;

        SetSlot(typeA, idxA, idB);
        SetSlot(typeB, idxB, idA);
        Sync();
    }

    [Command]
    public void CmdUseItem(EquipmentSlotType slotType, int slotIndex)
    {
        int itemId = GetSlot(slotType, slotIndex);
        if (itemId < 0 || !_itemLookup.ContainsKey(itemId)) return;

        ItemData item = _itemLookup[itemId];
        bool consumed = false;

        // 治疗
        if (item.healAmount > 0)
        {
            Player p = GetComponent<Player>();
            if (p != null && p.health < Player.MaxHealth)
            {
                p.health = Mathf.Min(Player.MaxHealth, p.health + item.healAmount);
                consumed = true;
            }
        }

        // 补充弹药
        if (item.ammoAmount > 0)
        {
            Shooting s = GetComponent<Shooting>();
            if (s != null)
            {
                s.AddAmmo(item.ammoAmount);
                consumed = true;
            }
        }

        // 消耗后移除
        if (consumed)
        {
            SetSlot(slotType, slotIndex, -1);
            Sync();
        }
    }

    #endregion

    #region Helper Methods

    bool CanPlaceInSlot(ItemType itemType, EquipmentSlotType slotType)
    {
        switch (slotType)
        {
            case EquipmentSlotType.Helmet:   return itemType == ItemType.Helmet;
            case EquipmentSlotType.Armor:    return itemType == ItemType.Armor;
            case EquipmentSlotType.Weapon:   return itemType == ItemType.Weapon;
            case EquipmentSlotType.ChestRig:
            case EquipmentSlotType.Backpack: return itemType == ItemType.Ammo || itemType == ItemType.Item;
        }
        return false;
    }

    int GetSlot(EquipmentSlotType type, int index)
    {
        switch (type)
        {
            case EquipmentSlotType.Helmet:  return _data.helmetItemId;
            case EquipmentSlotType.Armor:   return _data.armorItemId;
            case EquipmentSlotType.Weapon:  return (uint)index < 2 ? _data.weaponItemIds[index] : -1;
            case EquipmentSlotType.ChestRig: return (uint)index < 5 ? _data.chestRigItemIds[index] : -1;
            case EquipmentSlotType.Backpack: return (uint)index < 5 ? _data.backpackItemIds[index] : -1;
        }
        return -1;
    }

    void SetSlot(EquipmentSlotType type, int index, int itemId)
    {
        switch (type)
        {
            case EquipmentSlotType.Helmet:  _data.helmetItemId = itemId; break;
            case EquipmentSlotType.Armor:   _data.armorItemId = itemId; break;
            case EquipmentSlotType.Weapon:  if ((uint)index < 2) _data.weaponItemIds[index] = itemId; break;
            case EquipmentSlotType.ChestRig: if ((uint)index < 5) _data.chestRigItemIds[index] = itemId; break;
            case EquipmentSlotType.Backpack: if ((uint)index < 5) _data.backpackItemIds[index] = itemId; break;
        }
    }

    void RemoveFromAll(int itemId)
    {
        if (_data.helmetItemId == itemId) _data.helmetItemId = -1;
        if (_data.armorItemId == itemId) _data.armorItemId = -1;
        for (int i = 0; i < 2; i++) if (_data.weaponItemIds[i] == itemId) _data.weaponItemIds[i] = -1;
        for (int i = 0; i < 5; i++) if (_data.chestRigItemIds[i] == itemId) _data.chestRigItemIds[i] = -1;
        for (int i = 0; i < 5; i++) if (_data.backpackItemIds[i] == itemId) _data.backpackItemIds[i] = -1;
    }

    void Sync()
    {
        string json = JsonUtility.ToJson(_data);
        // 先赋值（触发网络同步给远程客户端）
        _syncedInventoryJson = json;
        // Host 模式下服务端直接修改 SyncVar 不会触发 hook，手动调用
        OnInventorySync("", json);

        // 服务端更新护甲等级
        RefreshArmorProtection();
    }

    #endregion

    #region Client Sync

    void OnInventorySync(string oldJson, string newJson)
    {
        // 服务端也需要刷新 UI（Host 模式下本地玩家的背包需要更新）
        ApplyInventoryJson(newJson);
    }

    void ApplyInventoryJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        _data = JsonUtility.FromJson<InventoryData>(json);
        Debug.Log($"[InventoryUI] ApplyInventoryJson 背包物品: {string.Join(",", _data.backpackItemIds)}");

        EnsureUI();
        Debug.Log($"[InventoryUI] EnsureUI 后 _ui={_ui != null}");
        if (_ui != null)
        {
            _ui.SetInventory(this);
            Debug.Log($"[InventoryUI] SetInventory 调用完毕");
        }

        // 客户端同步护甲等级
        RefreshArmorProtection();
    }

    /// <summary>
    /// 根据当前装备的护甲，更新 Player 上的 armorProtectionLevel
    /// </summary>
    void RefreshArmorProtection()
    {
        Player p = GetComponent<Player>();
        if (p == null) return;

        ItemData armor = GetItemData(_data.armorItemId);
        if (armor != null && armor.itemType == ItemType.Armor && p.armorDurability > 0)
        {
            p.armorProtectionLevel = armor.protectionLevel;
        }
        else
        {
            p.armorProtectionLevel = 0;
        }
    }

    #endregion

    #region Public (for UI)

    public ItemData GetItemData(int itemId)
    {
        if (itemId < 0 || _itemLookup == null || !_itemLookup.ContainsKey(itemId)) return null;
        return _itemLookup[itemId];
    }

    public InventoryData GetData() => _data;

    #endregion
}
