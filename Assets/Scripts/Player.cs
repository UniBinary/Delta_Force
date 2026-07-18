using Mirror;
using TMPro;
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
    public int armorDurability = 0;

    public const int MaxArmorDurability = 100;

    // 运行时查找血条（避免 prefab 里拖引用）
    private Image _healthBarFill;
    private Image _armorDurabilityFill;
    private TMPro.TextMeshProUGUI _healthValueText;
    private TMPro.TextMeshProUGUI _armorValueText;

    // 缓存的护甲等级（由 Inventory 在装备变更时设置）
    [HideInInspector] public int armorProtectionLevel = 0;

    // 是否处于观战模式
    [SyncVar(hook = nameof(OnSpectatingChanged))]
    public bool isSpectating = false;

    // 观战目标（本地使用）
    private Player _spectatorTarget;

    // 观战提示 UI 引用（屏幕上方绿色提示）
    private TMPro.TextMeshProUGUI _spectatorHintText;

    void Awake()
    {
        // 如果玩家身上没有 Collider2D，自动加一个（子弹是 Trigger，需要碰到玩家的 Collider 才能触发碰撞）
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.isTrigger = false;
        }

        // 自动查找子摄像机引用
        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>(true);
        }

        // 尽早查找 UI 引用，避免 SyncVar hook 在 OnStartServer 中触发时引用为 null
        FindUIReferences();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // 强制重置护甲耐久，覆盖 prefab 中可能残留的旧序列化值
        Debug.Log($"[Player] OnStartServer: armorDurability INIT, was={armorDurability}, setting to 0");
        armorDurability = 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 只有本地玩家才启用自己的摄像机（含 AudioListener），
        // 非本地玩家的摄像机必须禁用整个 GameObject，
        // 否则 Host 端会错误地使用 Client 端玩家的摄像机，且多个 AudioListener 会冲突
        if (cam != null)
            cam.gameObject.SetActive(isLocalPlayer);

        // 刷新 UI 引用（Awake 中已查找，此处更新初始值）
        if (isLocalPlayer)
        {
            FindUIReferences();
            UpdateHealthBar(health);
            UpdateHealthValueText(health);
            UpdateArmorDurabilityBar(armorDurability);
            UpdateArmorValueText(armorDurability);

            // 查找观战提示文本（初始隐藏，通过 Canvas Transform.Find 查找 inactive 对象）
            GameObject canvasObj = GameObject.Find("Canvas");
            Transform hintTrans = canvasObj?.transform.Find("SpectatorHint");
            if (hintTrans != null)
            {
                _spectatorHintText = hintTrans.GetComponent<TMPro.TextMeshProUGUI>();
                if (_spectatorHintText != null)
                    _spectatorHintText.gameObject.SetActive(false);
            }
        }
    }

    void FindUIReferences()
    {
        GameObject go = GameObject.Find("HealthBarFill");
        if (go != null && _healthBarFill == null) _healthBarFill = go.GetComponent<Image>();

        go = GameObject.Find("ArmorDuraTab");
        if (go != null && _armorDurabilityFill == null) _armorDurabilityFill = go.GetComponent<Image>();

        go = GameObject.Find("HealthValue");
        if (go != null && _healthValueText == null) _healthValueText = go.GetComponent<TMPro.TextMeshProUGUI>();

        go = GameObject.Find("ArmorValue");
        if (go != null && _armorValueText == null) _armorValueText = go.GetComponent<TMPro.TextMeshProUGUI>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (isSpectating) return; // 观战模式下不响应移动输入
        if (cam == null) return;

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        if (isSpectating) return;

        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);

        Vector2 lookDir = mousePos - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;
        rb.rotation = angle;
    }

    void LateUpdate()
    {
        if (!isLocalPlayer) return;

        // 观战模式：摄像机跟随目标，UI 显示目标状态
        if (isSpectating && _spectatorTarget != null)
        {
            if (cam != null)
            {
                Vector3 targetPos = _spectatorTarget.transform.position;
                targetPos.z = cam.transform.position.z;
                cam.transform.position = targetPos;
            }

            // 将本地 UI 更新为观战目标的血量和护甲
            if (_healthBarFill != null)
                _healthBarFill.fillAmount = (float)_spectatorTarget.health / MaxHealth;
            if (_healthValueText != null)
                _healthValueText.text = _spectatorTarget.health.ToString();
            if (_armorDurabilityFill != null)
                _armorDurabilityFill.fillAmount = (float)_spectatorTarget.armorDurability / MaxArmorDurability;
            if (_armorValueText != null)
                _armorValueText.text = _spectatorTarget.armorDurability.ToString();
        }

        if (cam != null)
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

            // 同步耐久到 Inventory，防止卸甲重穿耐久回满
            Inventory inv = GetComponent<Inventory>();
            if (inv != null)
                inv.OnArmorDurabilityDamaged(armorDurability);

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
            // 不再回复满血，交由 GameManager 处理死亡/撤离流程
            GameManager.Instance.OnPlayerDied(this);
        }
    }

    // ============================================================
    // 服务端→特定客户端：处理死亡或撤离
    // evacuated=true 表示撤离成功，false 表示死亡（撤离失败）
    // ============================================================
    [TargetRpc]
    public void TargetHandleDeath(bool evacuated)
    {
        Debug.Log($"[Player] TargetHandleDeath evacuated={evacuated} isServer={isServer}");

        if (isServer)
        {
            // ========== Host 端逻辑 ==========
            int aliveCount = GameManager.Instance.GetAlivePlayerCount(this);

            if (aliveCount == 0)
            {
                // 场上没有其他玩家 → 跳转场景
                string scene = evacuated ? "EvcSucceeded" : "EvcFailed";
                GameManager.DisconnectAndGoToScene(scene);
            }
            else
            {
                // 场上还有其他玩家 → 观战
                Player target = GameManager.Instance.GetRandomAlivePlayer(this);
                if (target != null)
                {
                    BecomeSpectator(target, evacuated);
                }
            }
        }
        else
        {
            // ========== 纯客户端逻辑 ==========
            string scene = evacuated ? "EvcSucceeded" : "EvcFailed";
            GameManager.DisconnectAndGoToScene(scene);
        }
    }

    // ============================================================
    // 服务端→特定客户端：启用观战指定玩家的摄像机
    // ============================================================
    [TargetRpc]
    public void RpcEnableSpectatorCamera(NetworkIdentity targetIdentity)
    {
        BecomeSpectator(targetIdentity.GetComponent<Player>(), false);
    }

    // ============================================================
    // SyncVar hook：当观战状态改变时，所有客户端禁用该玩家的可视/物理组件
    // ============================================================
    void OnSpectatingChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            // 禁用自身及所有子物体的渲染器（包括武器模型、FirePoint 特效等）
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                sr.enabled = false;

            // 禁用所有碰撞体
            foreach (var col in GetComponentsInChildren<Collider2D>(true))
                col.enabled = false;

            if (rb != null) rb.simulated = false;

            // 同时隐藏射击相关的子物体（FirePoint 特效等）
            Shooting shooting = GetComponentInChildren<Shooting>();
            if (shooting != null && shooting.firePoint != null)
                shooting.firePoint.gameObject.SetActive(false);
        }
    }

    // ============================================================
    // 本地：转为观战模式
    // evacuated=true 撤离成功（绿字），false=死亡（红字）
    // ============================================================
    void BecomeSpectator(Player target, bool evacuated)
    {
        Debug.Log($"[Player] 进入观战模式，目标: {target.netId}, evacuated={evacuated}");

        isSpectating = true;
        _spectatorTarget = target;

        // 摄像机保持激活，在 LateUpdate 中跟随目标并显示目标状态
        if (cam != null)
        {
            cam.gameObject.SetActive(true);
        }

        // 显示观战提示（死亡=红字，撤离成功=绿字）
        ShowSpectatorHint(evacuated);
    }

    // ============================================================
    // 显示观战提示
    // ============================================================
    void ShowSpectatorHint(bool evacuated)
    {
        if (_spectatorHintText == null)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            Transform hintTrans = canvasObj?.transform.Find("SpectatorHint");
            if (hintTrans != null)
                _spectatorHintText = hintTrans.GetComponent<TMPro.TextMeshProUGUI>();
        }
        if (_spectatorHintText != null)
        {
            if (evacuated)
            {
                _spectatorHintText.text = "撤离成功，正在观战";
                _spectatorHintText.color = new Color(0.2f, 1f, 0.3f, 1f); // 绿色
            }
            else
            {
                _spectatorHintText.text = "撤离失败，正在观战";
                _spectatorHintText.color = new Color(1f, 0.1f, 0.1f, 1f); // 红色
            }
            _spectatorHintText.gameObject.SetActive(true);
        }
    }

    // ============================================================
    // 服务端→特定客户端：跳转到结果场景（观战者用）
    // ============================================================
    [TargetRpc]
    public void TargetGoToScene(string sceneName)
    {
        Debug.Log($"[Player] TargetGoToScene → {sceneName}");
        GameManager.DisconnectAndGoToScene(sceneName);
    }

    // ============================================================
    // 旧 Respawn 已废弃（保留空实现以免编译错误，实际不再调用）
    // ============================================================
    [Server]
    void Respawn()
    {
        // 不再使用 — 死亡逻辑已迁移至 GameManager.OnPlayerDied
    }

    // ============================================================
    // UI 更新
    // ============================================================
    void OnHealthChanged(int oldVal, int newVal)
    {
        // 只有本地玩家才更新 UI，避免远程玩家的 SyncVar 同步覆盖本地 UI
        if (!isLocalPlayer) return;
        UpdateHealthBar(newVal);
        UpdateHealthValueText(newVal);
    }

    void UpdateHealthBar(int value)
    {
        if (_healthBarFill != null)
            _healthBarFill.fillAmount = (float)value / MaxHealth;
    }

    void UpdateHealthValueText(int value)
    {
        if (_healthValueText != null)
            _healthValueText.text = value.ToString();
    }

    void OnArmorDurabilityChanged(int oldVal, int newVal)
    {
        // 只有本地玩家才更新 UI，避免远程玩家的 SyncVar 同步覆盖本地 UI
        if (!isLocalPlayer) return;
        Debug.Log($"[Player] OnArmorDurabilityChanged: {oldVal} -> {newVal}, protectionLevel={armorProtectionLevel}");
        UpdateArmorDurabilityBar(newVal);
        UpdateArmorValueText(newVal);
    }

    void UpdateArmorDurabilityBar(int value)
    {
        if (_armorDurabilityFill == null) FindUIReferences();
        if (_armorDurabilityFill == null) return;
        _armorDurabilityFill.fillAmount = (float)value / MaxArmorDurability;
    }

    void UpdateArmorValueText(int value)
    {
        if (_armorValueText == null) FindUIReferences();
        if (_armorValueText == null) return;
        _armorValueText.text = value.ToString();
    }

    /// <summary>由 Inventory.RefreshArmorProtection 调用，确保护甲 UI 在 protectionLevel 同步后刷新</summary>
    public void RefreshArmorUI()
    {
        // 只有本地玩家才更新 UI
        if (!isLocalPlayer) return;
        UpdateArmorDurabilityBar(armorDurability);
        UpdateArmorValueText(armorDurability);
    }

    void OnDestroy()
    {
        // 清理观战协程
        StopAllCoroutines();
    }
}
