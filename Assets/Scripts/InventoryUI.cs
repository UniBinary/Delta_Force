using UnityEngine;
using TMPro;

/// <summary>
/// 背包 UI 面板。挂在场景中的 Canvas/InventoryPanel 上。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("面板")]
    public GameObject panel;

    [Header("装备槽")]
    public EquipmentSlotUI helmetSlot;
    public EquipmentSlotUI armorSlot;
    public EquipmentSlotUI[] weaponSlots = new EquipmentSlotUI[2];
    public EquipmentSlotUI[] chestRigSlots = new EquipmentSlotUI[5];
    public EquipmentSlotUI[] backpackSlots = new EquipmentSlotUI[5];

    private Inventory _inventory;
    private EquipmentSlotUI _hoveredSlot;

    /// <summary>背包是否打开（其他脚本用此属性拦截输入）</summary>
    public static bool IsOpen { get; private set; }

    void Start()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void SetInventory(Inventory inv)
    {
        _inventory = inv;
        Refresh();
    }

    /// <summary>
    /// 获取指定槽位的耐久度（供 Refresh 调用）
    /// </summary>
    public int GetSlotDurability(EquipmentSlotType type, int index)
    {
        if (_inventory == null) return 0;
        var data = _inventory.GetData();
        if (type == EquipmentSlotType.ChestRig && (uint)index < 5)
            return data.chestRigDurabilities[index];
        if (type == EquipmentSlotType.Backpack && (uint)index < 5)
            return data.backpackDurabilities[index];
        return 0;
    }

    public void Toggle()
    {
        if (panel == null) return;
        bool show = !panel.activeSelf;
        panel.SetActive(show);
        IsOpen = show;
        if (show) Refresh();
    }

    public void Refresh()
    {
        if (_inventory == null) return;
        var data = _inventory.GetData();

        SafeSlot(helmetSlot, EquipmentSlotType.Helmet, 0, data.helmetItemId);
        SafeSlot(armorSlot, EquipmentSlotType.Armor, 0, data.armorItemId);
        for (int i = 0; i < weaponSlots.Length && i < 2; i++)
            SafeSlot(weaponSlots[i], EquipmentSlotType.Weapon, i, data.weaponItemIds[i]);
        for (int i = 0; i < chestRigSlots.Length && i < 5; i++)
            SafeSlot(chestRigSlots[i], EquipmentSlotType.ChestRig, i, data.chestRigItemIds[i]);
        for (int i = 0; i < backpackSlots.Length && i < 5; i++)
            SafeSlot(backpackSlots[i], EquipmentSlotType.Backpack, i, data.backpackItemIds[i]);
    }

    void RefreshSlot(EquipmentSlotUI slot, int itemId)
    {
        ItemData item = _inventory != null ? _inventory.GetItemData(itemId) : null;
        int dur = (item != null && item.itemType == ItemType.MedKit)
            ? GetSlotDurability(slot.slotType, slot.slotIndex) : 0;
        slot.SetItem(item, itemId, dur);
    }

    void SafeSlot(EquipmentSlotUI slot, EquipmentSlotType type, int index, int itemId)
    {
        if (slot == null) return;
        slot.Init(type, index, this);
        RefreshSlot(slot, itemId);
    }

    // ---- 拖拽 ----

    private EquipmentSlotUI _dragSource;

    public void BeginDrag(EquipmentSlotUI slot)
    {
        if (slot.itemId < 0) return;
        _dragSource = slot;
        if (slot.icon != null)
            slot.icon.raycastTarget = false;
    }

    public void EndDrag(EquipmentSlotUI target)
    {
        if (_dragSource == null) return;
        if (_dragSource.icon != null)
            _dragSource.icon.raycastTarget = true;

        if (target != null && _dragSource != target && _inventory != null)
        {
            _inventory.CmdSwapSlots(
                _dragSource.slotType, _dragSource.slotIndex,
                target.slotType, target.slotIndex
            );
        }

        _dragSource = null;
    }

    public void OnSlotRightClick(EquipmentSlotType type, int index)
    {
        if (_inventory == null) return;
        _inventory.CmdUseItem(type, index);
    }

    public void OnSlotLeftClick(EquipmentSlotType type, int index)
    {
        // 单击可扩展为"选中"状态
    }

    // ---- 悬停（用于 Q 键丢弃） ----

    public void OnSlotHoverEnter(EquipmentSlotUI slot)
    {
        _hoveredSlot = slot;
    }

    public void OnSlotHoverExit(EquipmentSlotUI slot)
    {
        if (_hoveredSlot == slot)
            _hoveredSlot = null;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (_inventory == null) return;
        if (!_inventory.isLocalPlayer) return;

        if (Input.GetKeyDown(KeyCode.Q) && _hoveredSlot != null && _hoveredSlot.itemId >= 0)
        {
            _inventory.CmdDropItem(_hoveredSlot.slotType, _hoveredSlot.slotIndex);
        }
    }
}