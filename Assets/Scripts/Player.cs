using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class Player : NetworkBehaviour
{
    public float moveSpeed = 5f;
    public Rigidbody2D rb;
    public Camera cam;

    public Vector2 movement;
    public Vector2 mousePos;

    [SyncVar(hook = nameof(OnHealthChanged))]
    public int health = 100;

    public const int MaxHealth = 100;

    [SyncVar(hook = nameof(OnArmorDurabilityChanged))]
    public int armorDurability = 100;

    public const int MaxArmorDurability = 100;

    // 运行时查找血条（避免 prefab 里拖引用）
    private Image _healthBarFill;
    private Image _armorDurabilityFill;

    // 缓存的护甲等级（由 Inventory 在装备变更时设置）
    [HideInInspector] public int armorProtectionLevel = 0;

    void Awake()
    {
        // 如果玩家身上没有 Collider2D，自动加一个（子弹是 Trigger，需要碰到玩家的 Collider 才能触发碰撞）
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.isTrigger = false;
        }
    }

    public override void OnStartLocalPlayer()
    {
        if (cam != null)
        {
            cam.enabled = true;
            cam.GetComponent<AudioListener>().enabled = true;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 从场景 Canvas 中全局查找 HealthBarFill（不在玩家子物体中）
        if (isLocalPlayer)
        {
            GameObject go = GameObject.Find("HealthBarFill");
            if (go != null)
            {
                _healthBarFill = go.GetComponent<Image>();
                UpdateHealthBar(health);
            }
            go = GameObject.Find("ArmorDuraTab");
            if (go != null)
            {
                _armorDurabilityFill = go.GetComponent<Image>();
                UpdateArmorDurabilityBar(armorDurability);
            }
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);

        Vector2 lookDir = mousePos - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;
        rb.rotation = angle;
    }

    void LateUpdate()
    {
        cam.transform.rotation = Quaternion.identity;
    }

    [Server]
    public void TakeDamage(int amount, int bulletPenetration)
    {
        if (health <= 0) return;

        // 子弹穿透等级 vs 护甲防护等级 对比
        int diff = bulletPenetration - armorProtectionLevel;
        int finalDamage;
        int armorConsume;

        switch (diff)
        {
            case >= 2:  // 完全穿透
                finalDamage = 35;
                armorConsume = 35;
                break;
            case 1:     // 穿透
                finalDamage = 25;
                armorConsume = 25;
                break;
            case 0:     // 半穿透
                finalDamage = 20;
                armorConsume = 20;
                break;
            case -1:    // 不穿透
                finalDamage = 15;
                armorConsume = 15;
                break;
            case <= -2: // 钝伤
                finalDamage = 10;
                armorConsume = 10;
                break;
            default:
                finalDamage = 20;
                armorConsume = 20;
                break;
        }

        Debug.Log($"[Player] 受击: 子弹穿透={bulletPenetration} 护甲等级={armorProtectionLevel} diff={diff} 伤害={finalDamage} 护甲损耗={armorConsume}");

        // 护甲耐久扣除
        if (armorDurability > 0 && armorProtectionLevel > 0)
        {
            int oldArmor = armorDurability;
            armorDurability = Mathf.Max(0, armorDurability - armorConsume);
            // Host 模式下 SyncVar hook 不触发，手动刷新 UI
            OnArmorDurabilityChanged(oldArmor, armorDurability);

            // 护甲耐久归零时，护甲等级失效
            if (armorDurability <= 0)
            {
                Debug.Log("[Player] 护甲耐久归零！");
                armorProtectionLevel = 0;
            }
        }

        // 扣血
        int oldHealth = health;
        health = Mathf.Max(0, health - finalDamage);
        OnHealthChanged(oldHealth, health);

        if (health <= 0)
        {
            Invoke(nameof(Respawn), 3f);
        }
    }

    [Server]
    void Respawn()
    {
        int oldHealth = health;
        health = MaxHealth;
        int oldArmor = armorDurability;
        armorDurability = MaxArmorDurability;
        // Host 模式下 SyncVar hook 不触发，手动刷新 UI
        OnHealthChanged(oldHealth, health);
        OnArmorDurabilityChanged(oldArmor, armorDurability);
    }

    void OnHealthChanged(int oldVal, int newVal)
    {
        UpdateHealthBar(newVal);
    }

    void UpdateHealthBar(int value)
    {
        if (_healthBarFill != null)
            _healthBarFill.fillAmount = (float)value / MaxHealth;
    }

    void OnArmorDurabilityChanged(int oldVal, int newVal)
    {
        UpdateArmorDurabilityBar(newVal);
    }

    void UpdateArmorDurabilityBar(int value)
    {
        if (_armorDurabilityFill != null)
            _armorDurabilityFill.fillAmount = (float)value / MaxArmorDurability;
    }
}
