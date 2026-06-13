using Mirror;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    public float lifetime = 3f;
    [Header("伤害值")]
    public int damage = 20;
    [Header("穿透等级 (1-6，越高越强)")]
    public int penetrationLevel = 1;

    private Rigidbody2D _rb;
    private int _frameCount;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        // 高速子弹需要连续碰撞检测，防止隧穿
        if (_rb != null)
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public override void OnStartServer()
    {
        Debug.Log($"[Bullet] 服务端生成。Rigidbody2D={_rb != null}, simulated={_rb?.simulated}, collider={GetComponent<Collider2D>()?.isTrigger}, layer={gameObject.layer}");
        Invoke(nameof(DestroySelf), lifetime);
    }

    void Update()
    {
        // 前 5 帧报告位置，确认子弹在飞
        if (isServer && _frameCount < 5)
        {
            Debug.Log($"[Bullet] frame {_frameCount}: pos={transform.position}, velocity={_rb?.velocity}");
            _frameCount++;
        }
    }

    [Server]
    void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[Bullet] OnTriggerEnter2D → {other.name} (isServer={isServer})");
        
        if (!isServer) return;

        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            Debug.Log($"[Bullet] 命中玩家，穿透={penetrationLevel}，基础伤害={damage}");
            player.TakeDamage(damage, penetrationLevel);
        }
        else
        {
            Debug.Log($"[Bullet] 碰到 {other.name}，无 Player 组件");
        }

        NetworkServer.Destroy(gameObject);
    }

    // 如果 Trigger 不触发，试试普通碰撞
    void OnCollisionEnter2D(Collision2D col)
    {
        Debug.Log($"[Bullet] OnCollisionEnter2D → {col.collider.name}");
    }
}
