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

    // 运行时查找血条（避免 prefab 里拖引用）
    private Image _healthBarFill;

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
    public void TakeDamage(int amount)
    {
        if (health <= 0) return;

        int oldHealth = health;
        health = Mathf.Max(0, health - amount);
        // Host 模式下 SyncVar hook 不触发，手动刷新 UI
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
        // Host 模式下 SyncVar hook 不触发，手动刷新 UI
        OnHealthChanged(oldHealth, health);
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
}
