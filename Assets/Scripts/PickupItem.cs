using Mirror;
using UnityEngine;

/// <summary>
/// 地面上的可捡拾物品。挂在物品预制体上。
/// 需要在 NetworkManager 中注册 spawnable prefab。
/// </summary>
public class PickupItem : NetworkBehaviour
{
    [Header("物品")]
    public int itemId = -1;   // 对应 Inventory.allItems 中的索引
    public SpriteRenderer sr;
    public float bobSpeed = 1f;
    public float bobAmount = 0.2f;
    public float clickPickupRange = 3f; // 点击拾取的最大距离

    private float _startY;

    void Awake()
    {
        // 设为触发器避免物理阻挡玩家，Physics2D.OverlapCircleAll 仍能检测到
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public override void OnStartServer()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        _startY = transform.position.y;
    }

    void Update()
    {
        // 简单浮动动画
        if (sr != null)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            transform.position = new Vector3(
                transform.position.x,
                _startY + bob,
                transform.position.z
            );
        }
    }

    void OnMouseDown()
    {
        if (NetworkClient.localPlayer == null) return;

        // 距离检查
        Vector3 playerPos = NetworkClient.localPlayer.transform.position;
        float dist = Vector3.Distance(playerPos, transform.position);
        if (dist > clickPickupRange) return;

        Inventory inv = NetworkClient.localPlayer.GetComponent<Inventory>();
        if (inv != null)
            inv.CmdPickupItem(netId);
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
