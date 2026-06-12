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

    void SafeSlot(EquipmentSlotUI slot, EquipmentSlotType type, int index, int itemId)
    {
        if (slot == null) return;
        slot.Init(type, index, this);
        slot.SetItem(_inventory.GetItemData(itemId), itemId);
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
}
