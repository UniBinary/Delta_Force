using Mirror;
using TMPro;
using UnityEngine;

public class Shooting : NetworkBehaviour
{
    [Header("武器列表")]
    public WeaponData[] weapons;

    [Header("发射点")]
    public Transform firePoint;

    [Header("音效 (可选)")]
    public AudioSource reloadSound;

    [Header("UI")]

    // 当前武器运行时数据
    private int _currentWeaponIndex;

    // 每把武器的弹药状态（服务端持有）
    private int[] _magazineAmmo;
    private int[] _reserveAmmo;
    private bool[] _isReloading;

    // 同步给所有客户端的弹药（自动同步）
    [SyncVar(hook = nameof(OnMagazineChanged))]
    private int _syncedMagazineAmmo;

    [SyncVar(hook = nameof(OnReserveChanged))]
    private int _syncedReserveAmmo;

    private float _nextFireTime;
    private TextMeshProUGUI _magazineText;
    private TextMeshProUGUI _reserveText;
    private TextMeshProUGUI _weaponNameText;

    // 当前武器快捷引用
    private WeaponData CurrentWeapon => weapons[_currentWeaponIndex];

    void Start()
    {
        int count = weapons.Length;
        if (count == 0)
        {
            Debug.LogError("[Shooting] 没有配置任何武器！");
            enabled = false;
            return;
        }

        _magazineAmmo = new int[count];
        _reserveAmmo = new int[count];
        _isReloading = new bool[count];

        if (isServer)
        {
            for (int i = 0; i < count; i++)
            {
                _magazineAmmo[i] = weapons[i].maxMagazineAmmo;
                _reserveAmmo[i] = weapons[i].totalReserveAmmo;
            }

            // 初始化同步值
            _syncedMagazineAmmo = _magazineAmmo[0];
            _syncedReserveAmmo = _reserveAmmo[0];
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 客户端启动时，如果 SyncVars 已有值（服务端设置过），触发布局
        if (!isServer && _syncedMagazineAmmo > 0)
        {
            // SyncVars 会自动触发 hook，这里不需要额外处理
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (weapons.Length == 0) return;

        // 查找 UI
        if (_magazineText == null) FindUIText("BulletsInFirearm", ref _magazineText);
        if (_reserveText == null) FindUIText("BulletsRemain", ref _reserveText);
        if (_weaponNameText == null) FindUIText("FirearmName", ref _weaponNameText);

        // 背包打开时禁止射击、换弹、切枪
        if (InventoryUI.IsOpen)
        {
            UpdateAmmoUI();
            return;
        }

        // 数字键切换武器（1 ~ 9）
        HandleWeaponSwitch();

        // 换弹
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading[_currentWeaponIndex])
        {
            TryReload();
        }

        // 全自动射击
        var w = CurrentWeapon;
        if (Input.GetKey(KeyCode.Mouse0) && Time.time >= _nextFireTime)
        {
            TryShoot(w);
        }

        // 更新 UI
        UpdateAmmoUI();
    }

    void HandleWeaponSwitch()
    {
        for (int i = 0; i < Mathf.Min(weapons.Length, 9); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (_currentWeaponIndex != i)
                {
                    // 本地立即切换武器模型
                    SwitchWeaponModel(_currentWeaponIndex, i);
                    _currentWeaponIndex = i;
                    _nextFireTime = 0;
                    CmdSwitchWeapon(i);
                }
                break;
            }
        }
    }

    void SwitchWeaponModel(int oldIndex, int newIndex)
    {
        // 隐藏旧武器模型
        if (oldIndex >= 0 && oldIndex < weapons.Length && weapons[oldIndex].weaponModel != null)
            weapons[oldIndex].weaponModel.SetActive(false);

        // 显示新武器模型
        if (newIndex >= 0 && newIndex < weapons.Length && weapons[newIndex].weaponModel != null)
            weapons[newIndex].weaponModel.SetActive(true);
    }

    void FindUIText(string objName, ref TextMeshProUGUI cache)
    {
        GameObject go = GameObject.Find(objName);
        if (go != null)
        {
            cache = go.GetComponent<TextMeshProUGUI>();
        }
    }

    void TryShoot(WeaponData w)
    {
        if (_isReloading[_currentWeaponIndex]) return;

        // 用同步值做本地校验（客户端也能看到真实弹药）
        if (_syncedMagazineAmmo <= 0)
        {
            TryReload();
            return;
        }

        _nextFireTime = Time.time + w.fireRate;
        CmdShoot();
    }

    [Command]
    void CmdShoot()
    {
        int idx = _currentWeaponIndex;
        if (idx < 0 || idx >= weapons.Length) return;
        if (_magazineAmmo[idx] <= 0) return;

        _magazineAmmo[idx]--;

        // 同步到客户端
        _syncedMagazineAmmo = _magazineAmmo[idx];

        var w = weapons[idx];
        GameObject bullet = Instantiate(w.bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        rb.AddForce(firePoint.up * w.bulletForce, ForceMode2D.Impulse);
        NetworkServer.Spawn(bullet);
    }

    [Command]
    void CmdSwitchWeapon(int index)
    {
        int oldIndex = _currentWeaponIndex;
        _currentWeaponIndex = index;

        // 服务端同步武器模型
        RpcSwitchWeaponModel(oldIndex, index);

        // 同步新武器的弹药
        if (index >= 0 && index < weapons.Length)
        {
            _syncedMagazineAmmo = _magazineAmmo[index];
            _syncedReserveAmmo = _reserveAmmo[index];
        }
    }

    [ClientRpc]
    void RpcSwitchWeaponModel(int oldIndex, int newIndex)
    {
        if (isLocalPlayer) return; // 本地玩家已在 HandleWeaponSwitch 中处理
        SwitchWeaponModel(oldIndex, newIndex);
    }

    void TryReload()
    {
        int idx = _currentWeaponIndex;
        if (_isReloading[idx]) return;
        if (_syncedMagazineAmmo >= weapons[idx].maxMagazineAmmo) return;
        if (_syncedReserveAmmo <= 0) return;

        CmdReload();
    }

    [Command]
    void CmdReload()
    {
        StartCoroutine(ReloadCoroutine(_currentWeaponIndex));
    }

    System.Collections.IEnumerator ReloadCoroutine(int idx)
    {
        _isReloading[idx] = true;
        yield return new WaitForSeconds(weapons[idx].reloadTime);

        int needed = weapons[idx].maxMagazineAmmo - _magazineAmmo[idx];
        int available = Mathf.Min(needed, _reserveAmmo[idx]);

        _magazineAmmo[idx] += available;
        _reserveAmmo[idx] -= available;

        // 同步到客户端
        _syncedMagazineAmmo = _magazineAmmo[idx];
        _syncedReserveAmmo = _reserveAmmo[idx];

        _isReloading[idx] = false;

        if (reloadSound != null)
        {
            RpcPlayReloadSound();
        }
    }

    [ClientRpc]
    void RpcPlayReloadSound()
    {
        if (reloadSound != null)
        {
            reloadSound.Play();
        }
    }

    /// <summary>
    /// 由 Inventory 调用：给当前武器补充弹药（服务端执行）
    /// </summary>
    [Server]
    public void AddAmmo(int amount)
    {
        int idx = _currentWeaponIndex;
        if (idx >= 0 && idx < _reserveAmmo.Length)
        {
            _reserveAmmo[idx] += amount;
            _syncedReserveAmmo = _reserveAmmo[idx];
        }
    }

    void UpdateAmmoUI()
    {
        int idx = _currentWeaponIndex;
        if (idx < 0 || idx >= weapons.Length) return;

        if (_magazineText != null)
            _magazineText.text = _syncedMagazineAmmo.ToString("D3");
        if (_reserveText != null)
            _reserveText.text = _syncedReserveAmmo.ToString("D3");
        if (_weaponNameText != null)
            _weaponNameText.text = weapons[idx].weaponName;
    }

    // ===== SyncVar Hooks =====
    void OnMagazineChanged(int oldVal, int newVal)
    {
        if (_magazineText != null)
            _magazineText.text = newVal.ToString("D3");
    }

    void OnReserveChanged(int oldVal, int newVal)
    {
        if (_reserveText != null)
            _reserveText.text = newVal.ToString("D3");
    }
}
