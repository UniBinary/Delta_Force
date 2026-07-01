using Mirror;
using UnityEngine;

/// <summary>
/// 地面上的可捡拾物品。挂在物品预制体上。
/// 需要在 NetworkManager 中注册 spawnable prefab。
/// 拾取方式：靠近后按 F 键（由 Inventory.TryPickup 处理）。
/// </summary>
public class PickupItem : NetworkBehaviour
{
    [Header("物品")]
    public int itemId = -1;        // 对应 ItemDatabase 中的索引
    public int ammoCount;          // 弹药实际数量（仅 Ammo 类型有效，>0 时覆盖 ItemData.ammoAmount）
    public int magazineAmmo;       // 武器弹匣剩余子弹（仅 Weapon 类型有效）
    public SpriteRenderer sr;

    void Awake()
    {
        // 自动查找 SpriteRenderer
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        // 设为触发器避免物理阻挡玩家，Physics2D.OverlapCircleAll 仍能检测到
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>
    /// 场景编辑器/代码中设置物品
    /// </summary>
    public void SetItem(int id, Sprite icon)
    {
        itemId = id;
        if (sr != null && icon != null)
            sr.sprite = icon;
    }
}
