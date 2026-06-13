using Mirror;
using TMPro;
using UnityEngine;

public class Shooting : NetworkBehaviour
{
    [Header("发射点")]
    public Transform firePoint;

    [Header("音效 (可选)")]
    public AudioSource reloadSound;

    // 运行时从 Inventory 武器槽读取
    private Inventory _inventory;
    private WeaponData[] _weaponDatas = new WeaponData[2];   // 对应武器槽 0 和 1
    private int _currentWeaponIndex = 0;

    // 每把武器的弹药状态
    private int[] _magazineAmmo = new int[2];
    private int[] _reserveAmmo = new int[2];
    private bool[] _isReloading = new bool[2];
    private bool[] _ammoInitialized = new bool[2];          // 弹药是否已初始化

    // 同步弹药
    [SyncVar(hook = nameof(OnMagazineChanged))]
    private int _syncedMagazineAmmo;

    [SyncVar(hook = nameof(OnReserveChanged))]
    private int _syncedReserveAmmo;

    private float _nextFireTime;
    private TextMeshProUGUI _magazineText;
    private TextMeshProUGUI _reserveText;
    private TextMeshProUGUI _weaponNameText;

    // 上一帧武器模型的 GameObject 引用（用于模型显示/隐藏）
    private GameObject _lastWeaponModel;

    WeaponData CurrentWeapon =>
        (_currentWeaponIndex >= 0 && _currentWeaponIndex < 2) ? _weaponDatas[_currentWeaponIndex] : null;

    void Start()
    {
        _inventory = GetComponent<Inventory>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // 服务端初始化同步值
        _syncedMagazineAmmo = 30;   // 默认值，后续由 RefreshWeapons 覆盖
        _syncedReserveAmmo = 90;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // 查找 UI 文本
        if (_magazineText == null) FindUIText("BulletsInFirearm", ref _magazineText);
        if (_reserveText == null) FindUIText("BulletsRemain", ref _reserveText);
        if (_weaponNameText == null) FindUIText("FirearmName", ref _weaponNameText);

        // 每帧刷新武器数据（从 Inventory 武器槽读取）
        RefreshWeaponsFromInventory();

        // 背包打开时禁止射击、换弹、切枪
        if (InventoryUI.IsOpen)
        {
            // 仍然刷新武器模型（让玩家看到当前武器）
            UpdateWeaponModel();
            UpdateAmmoUI();
            return;
        }

        // 数字键 1 / 2 切换武器
        HandleWeaponSwitch();

        // 换弹 R
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading[_currentWeaponIndex] && CurrentWeapon != null)
        {
            TryReload();
        }

        // 射击 鼠标左键
        if (Input.GetKey(KeyCode.Mouse0) && Time.time >= _nextFireTime && CurrentWeapon != null)
        {
            TryShoot(CurrentWeapon);
        }

        // 更新武器模型 & UI
        UpdateWeaponModel();
        UpdateAmmoUI();
    }

    /// <summary>
    /// 从 Inventory 武器槽读取 WeaponData
    /// </summary>
    void RefreshWeaponsFromInventory()
    {
        if (_inventory == null) return;
        var data = _inventory.GetData();
        bool changed = false;

        for (int i = 0; i < 2; i++)
        {
            int itemId = data.weaponItemIds[i];
            WeaponData newWd = null;
            if (itemId >= 0)
            {
                ItemData item = _inventory.GetItemData(itemId);
                if (item != null && item.itemType == ItemType.Weapon)
                    newWd = item.weaponData;
            }

            if (_weaponDatas[i] != newWd)
            {
                _weaponDatas[i] = newWd;
                // 新武器初始化弹药
                if (newWd != null && !_ammoInitialized[i])
                {
                    _magazineAmmo[i] = newWd.maxMagazineAmmo;
                    _reserveAmmo[i] = newWd.totalReserveAmmo;
                    _ammoInitialized[i] = true;
                }
                else if (newWd == null)
                {
                    _ammoInitialized[i] = false;
                    _magazineAmmo[i] = 0;
                    _reserveAmmo[i] = 0;
                }
                changed = true;
            }
        }

        // 当前武器被卸下时自动切到另一把
        if (CurrentWeapon == null)
        {
            int otherSlot = 1 - _currentWeaponIndex;
            if (_weaponDatas[otherSlot] != null)
            {
                int old = _currentWeaponIndex;
                _currentWeaponIndex = otherSlot;
                SwitchWeaponModel(old, otherSlot);
                CmdSwitchWeapon(otherSlot);
            }
        }

        // 同步弹药到 SyncVar
        if (changed && isServer)
        {
            SyncAmmoToClient();
        }
    }

    /// <summary>
    /// 显示当前武器的模型，隐藏其他的
    /// </summary>
    void UpdateWeaponModel()
    {
        WeaponData curr = CurrentWeapon;
        GameObject currModel = curr?.weaponModel;

        if (_lastWeaponModel != currModel)
        {
            // 隐藏所有武器模型
            for (int i = 0; i < 2; i++)
            {
                if (_weaponDatas[i]?.weaponModel != null)
                    _weaponDatas[i].weaponModel.SetActive(false);
            }
            // 显示当前武器模型
            if (currModel != null)
                currModel.SetActive(true);
            _lastWeaponModel = currModel;
        }
    }

    /// <summary>
    /// 从 Inventory 卸下武器时通知服务端切换
    /// </summary>
    [Command]
    void CmdSwitchWeapon(int index)
    {
        int old = _currentWeaponIndex;
        _currentWeaponIndex = index;

        // 客户端 RPC 切换模型
        RpcSwitchWeaponModel(old, index);

        // 同步弹药
        SyncAmmoToClient();
    }

    [ClientRpc]
    void RpcSwitchWeaponModel(int oldIndex, int newIndex)
    {
        if (isLocalPlayer) return;
        _currentWeaponIndex = newIndex;
        UpdateWeaponModel();
    }

    void HandleWeaponSwitch()
    {
        // 数字键 1 / 2 切换武器
        for (int i = 0; i < 2; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (_currentWeaponIndex != i && _weaponDatas[i] != null)
                {
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
        if (oldIndex >= 0 && oldIndex < 2 && _weaponDatas[oldIndex]?.weaponModel != null)
            _weaponDatas[oldIndex].weaponModel.SetActive(false);

        // 显示新武器模型
        if (newIndex >= 0 && newIndex < 2 && _weaponDatas[newIndex]?.weaponModel != null)
            _weaponDatas[newIndex].weaponModel.SetActive(true);

        _lastWeaponModel = _weaponDatas[newIndex]?.weaponModel;
    }

    void FindUIText(string objName, ref TextMeshProUGUI cache)
    {
        GameObject go = GameObject.Find(objName);
        if (go != null)
            cache = go.GetComponent<TextMeshProUGUI>();
    }

    void TryShoot(WeaponData w)
    {
        if (_isReloading[_currentWeaponIndex]) return;

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
        WeaponData w = CurrentWeapon;
        if (w == null) return;
        if (_magazineAmmo[idx] <= 0) return;

        _magazineAmmo[idx]--;
        SyncAmmoToClient();

        GameObject bullet = Instantiate(w.bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bulletComp = bullet.GetComponent<Bullet>();
        if (bulletComp != null)
            bulletComp.penetrationLevel = w.penetrationLevel;
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.AddForce(firePoint.up * w.bulletForce, ForceMode2D.Impulse);
        NetworkServer.Spawn(bullet);
    }

    void TryReload()
    {
        int idx = _currentWeaponIndex;
        if (_isReloading[idx]) return;
        if (CurrentWeapon == null) return;
        if (_syncedMagazineAmmo >= CurrentWeapon.maxMagazineAmmo) return;
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
        WeaponData w = _weaponDatas[idx];
        if (w == null) yield break;

        _isReloading[idx] = true;
        yield return new WaitForSeconds(w.reloadTime);

        int needed = w.maxMagazineAmmo - _magazineAmmo[idx];
        int available = Mathf.Min(needed, _reserveAmmo[idx]);

        _magazineAmmo[idx] += available;
        _reserveAmmo[idx] -= available;

        SyncAmmoToClient();
        _isReloading[idx] = false;

        if (reloadSound != null)
            RpcPlayReloadSound();
    }

    [ClientRpc]
    void RpcPlayReloadSound()
    {
        if (reloadSound != null)
            reloadSound.Play();
    }

    /// <summary>
    /// 同步当前武器弹药到 SyncVar（仅服务端调用）
    /// </summary>
    void SyncAmmoToClient()
    {
        int idx = _currentWeaponIndex;
        if (idx >= 0 && idx < 2)
        {
            _syncedMagazineAmmo = _magazineAmmo[idx];
            _syncedReserveAmmo = _reserveAmmo[idx];
        }
    }

    /// <summary>
    /// 由 Inventory 调用：给当前武器补充弹药（服务端执行）
    /// </summary>
    [Server]
    public void AddAmmo(int amount)
    {
        int idx = _currentWeaponIndex;
        if (idx >= 0 && idx < 2)
        {
            _reserveAmmo[idx] += amount;
            SyncAmmoToClient();
        }
    }

    void UpdateAmmoUI()
    {
        WeaponData w = CurrentWeapon;

        if (_magazineText != null)
            _magazineText.text = _syncedMagazineAmmo.ToString("D3");
        if (_reserveText != null)
            _reserveText.text = _syncedReserveAmmo.ToString("D3");
        if (_weaponNameText != null)
            _weaponNameText.text = w != null ? w.weaponName : "";
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