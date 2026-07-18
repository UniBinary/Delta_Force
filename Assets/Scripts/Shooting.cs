using System.Collections.Generic;
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
    private Queue<int>[] _magazineBulletLevels = new Queue<int>[2] { new Queue<int>(), new Queue<int>() };  // 每发子弹的穿透等级队列
    private bool[] _isReloading = new bool[2];
    private bool[] _ammoInitialized = new bool[2];          // 弹药是否已初始化

    // 同步弹药
    [SyncVar(hook = nameof(OnMagazineChanged))]
    private int _syncedMagazineAmmo;

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
        // 尝试多种方式查找 Inventory（可能不在同一 GameObject 上）
        _inventory = GetComponent<Inventory>();
        if (_inventory == null)
            _inventory = GetComponentInParent<Inventory>();
        if (_inventory == null)
            _inventory = GetComponentInChildren<Inventory>();
        if (_inventory == null)
            _inventory = FindObjectOfType<Inventory>();
        Debug.Log($"[Shooting] Start: isLocalPlayer={isLocalPlayer}, isServer={isServer}, isClient={isClient}, _inventory={_inventory != null}");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _syncedMagazineAmmo = 0;
        Debug.Log("[Shooting] OnStartServer: _syncedMagazineAmmo=0");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[Shooting] OnStartClient: isLocalPlayer={isLocalPlayer}, _syncedMagazineAmmo={_syncedMagazineAmmo}");
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

        // 客户端未就绪时不能发送 Command（等待 NetworkClient.Ready）
        if (!NetworkClient.ready) return;

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
    /// 从 Inventory 武器槽读取 WeaponData（仅客户端 Update 调用）。
    /// </summary>
    int _debugFrameCount;

    void RefreshWeaponsFromInventory()
    {
        if (_inventory == null) { Debug.LogWarning("[Shooting] RefreshWeaponsFromInventory: _inventory is NULL!"); return; }
        var data = _inventory.GetData();
        if (data == null) { Debug.LogWarning("[Shooting] RefreshWeaponsFromInventory: GetData() returned NULL!"); return; }
        bool changed = false;

        // 每秒输出一次调试信息
        bool debugLog = (_debugFrameCount++ % 60 == 0);
        if (debugLog)
            Debug.Log($"[Shooting] Refresh: weaponIds=[{data.weaponItemIds[0]},{data.weaponItemIds[1]}] ItemDB.init={ItemDatabase.IsValidItemId(0)}");

        for (int i = 0; i < 2; i++)
        {
            int itemId = data.weaponItemIds[i];
            WeaponData newWd = null;
            if (itemId >= 0)
            {
                ItemData item = _inventory.GetItemData(itemId);
                if (debugLog)
                    Debug.Log($"[Shooting] slot[{i}] itemId={itemId} item={item?.itemName} type={item?.itemType} wd={item?.weaponData?.weaponName}");
                if (item != null && item.itemType == ItemType.Weapon)
                    newWd = item.weaponData;
            }
            else if (debugLog)
                Debug.Log($"[Shooting] slot[{i}] itemId={itemId} (empty)");

            if (_weaponDatas[i] != newWd)
            {
                Debug.Log($"[Shooting] slot[{i}] WEAPON CHANGED: old={_weaponDatas[i]?.weaponName} new={newWd?.weaponName}");
                _weaponDatas[i] = newWd;
                // 新武器初始化弹药（弹匣装满）
                if (newWd != null && !_ammoInitialized[i])
                {
                    _magazineAmmo[i] = 0;
                    _ammoInitialized[i] = true;
                    _magazineBulletLevels[i].Clear();
                    Debug.Log($"[Shooting] slot[{i}] AMMO INIT: _magazineAmmo[{i}]=0 (empty, need reload)");
                    // 通知服务端初始化弹药（空弹匣）
                    CmdInitWeaponAmmo(i, 0);
                    // 只在服务端直接设置 SyncVar（客户端等待服务端同步）
                    if (isServer && i == _currentWeaponIndex)
                    {
                        _syncedMagazineAmmo = 0;
                        Debug.Log($"[Shooting] slot[{i}] Server SyncVar updated to 0");
                    }
                }
                else if (newWd == null)
                {
                    _ammoInitialized[i] = false;
                    _magazineAmmo[i] = 0;
                    _magazineBulletLevels[i].Clear();
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

        // Host 模式下也需要更新 _syncedMagazineAmmo
        if (changed && isServer)
        {
            SyncAmmoToClient();
        }
    }

    /// <summary>
    /// 服务端从 Inventory 直接读取指定槽位的 WeaponData（[Command] 方法中使用）。
    /// 避免依赖 _weaponDatas（服务端 Update 不执行导致其为空）。
    /// </summary>
    WeaponData GetWeaponDataFromServerInventory(int slot)
    {
        if (_inventory == null) return null;
        var data = _inventory.GetData();
        if ((uint)slot >= 2) return null;
        int itemId = data.weaponItemIds[slot];
        if (itemId < 0) return null;
        ItemData item = _inventory.GetItemData(itemId);
        if (item != null && item.itemType == ItemType.Weapon)
            return item.weaponData;
        return null;
    }

    /// <summary>
    /// 服务端：延迟初始化弹药（若 CmdInitWeaponAmmo 尚未到达，则从武器数据推断）。
    /// </summary>
    void ServerEnsureAmmoInitialized(int slot)
    {
        if (_ammoInitialized[slot]) return;
        WeaponData w = GetWeaponDataFromServerInventory(slot);
        if (w != null)
        {
            _magazineAmmo[slot] = 0;
            _ammoInitialized[slot] = true;
            _magazineBulletLevels[slot].Clear();
        }
    }

    /// <summary>
    /// 获取当前武器匹配的背包备弹数量
    /// </summary>
    int GetReserveAmmoFromInventory()
    {
        WeaponData w = CurrentWeapon;
        if (w == null || _inventory == null) return 0;
        return _inventory.GetAmmoCount(w.ammoType);
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

        // 确保新武器的弹药已初始化
        ServerEnsureAmmoInitialized(index);

        // 客户端 RPC 切换模型
        RpcSwitchWeaponModel(old, index);

        // 同步弹药
        SyncAmmoToClient();
    }

    /// <summary>
    /// 客户端通知服务端：某武器槽弹药已初始化（拾取武器后同步弹匣弹药到服务端）。
    /// 注意：若服务端已通过 SetMagazineState 初始化（如捡枪时），不要覆盖。
    /// </summary>
    [Command]
    void CmdInitWeaponAmmo(int slot, int ammo)
    {
        if (slot >= 0 && slot < 2)
        {
            // 若服务端已初始化（例如捡枪时 SetMagazineState 已设置正确弹量），
            // 则不要用客户端传过来的 0 覆盖掉正确的弹匣数据。
            if (!_ammoInitialized[slot])
            {
                _magazineAmmo[slot] = ammo;
                _ammoInitialized[slot] = true;
                _magazineBulletLevels[slot].Clear();
            }
            if (slot == _currentWeaponIndex)
                SyncAmmoToClient();
        }
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
        // 先尝试查找活跃对象
        GameObject go = GameObject.Find(objName);
        if (go != null)
        {
            cache = go.GetComponent<TextMeshProUGUI>();
            if (cache != null) return;
        }
        // 回退：查找包括非活跃对象（UI Canvas 初始可能被禁用）
        var all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        foreach (var tmp in all)
        {
            if (tmp.name == objName && tmp.gameObject.scene.IsValid())
            {
                cache = tmp;
                break;
            }
        }
    }

    void TryShoot(WeaponData w)
    {
        if (_isReloading[_currentWeaponIndex]) return;

        if (_syncedMagazineAmmo <= 0)
        {
            Debug.Log($"[Shooting] TryShoot BLOCKED: _syncedMagazineAmmo={_syncedMagazineAmmo}");
            TryReload();
            return;
        }

        Debug.Log($"[Shooting] TryShoot OK: _syncedMagazineAmmo={_syncedMagazineAmmo}, firing CmdShoot");
        _nextFireTime = Time.time + w.fireRate;
        CmdShoot();
    }

    [Command]
    void CmdShoot()
    {
        int idx = _currentWeaponIndex;
        // 从服务端 Inventory 直接读武器数据（不依赖 _weaponDatas，因为服务端 Update 不执行）
        WeaponData w = GetWeaponDataFromServerInventory(idx);
        if (w == null) { Debug.LogWarning($"[Shooting] CmdShoot FAILED: GetWeaponDataFromServerInventory({idx}) returned null"); return; }
        // 延迟初始化弹药（处理 CmdInitWeaponAmmo 尚未到达的边界情况）
        ServerEnsureAmmoInitialized(idx);
        if (_magazineAmmo[idx] <= 0) { Debug.LogWarning($"[Shooting] CmdShoot FAILED: _magazineAmmo[{idx}]={_magazineAmmo[idx]}"); return; }
        Debug.Log($"[Shooting] CmdShoot OK: weapon={w.weaponName}, ammo={_magazineAmmo[idx]}, penLevel queue={_magazineBulletLevels[idx].Count} left");

        if (w.bulletPrefab == null)
        {
            Debug.LogError($"[Shooting] CmdShoot FAILED: weapon '{w.weaponName}' has no bulletPrefab assigned!");
            return;
        }

        _magazineAmmo[idx]--;
        SyncAmmoToClient();

        // 从弹匣队列中取出该发子弹的穿透等级
        int pen = w.penetrationLevel; // 默认用武器穿透
        if (_magazineBulletLevels[idx].Count > 0)
            pen = _magazineBulletLevels[idx].Dequeue();

        GameObject bullet = Instantiate(w.bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bulletComp = bullet.GetComponent<Bullet>();
        if (bulletComp != null)
        {
            bulletComp.penetrationLevel = pen;
        }
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

        // 检查背包中是否有匹配的弹药
        if (_inventory != null && _inventory.GetAmmoCount(CurrentWeapon.ammoType) <= 0) return;

        CmdReload();
    }

    [Command]
    void CmdReload()
    {
        ServerEnsureAmmoInitialized(_currentWeaponIndex);
        StartCoroutine(ReloadCoroutine(_currentWeaponIndex));
    }

    System.Collections.IEnumerator ReloadCoroutine(int idx)
    {
        // 服务端从 Inventory 读取武器数据（_weaponDatas 在服务端未填充）
        WeaponData w = isServer ? GetWeaponDataFromServerInventory(idx) : _weaponDatas[idx];
        if (w == null) yield break;

        _isReloading[idx] = true;
        yield return new WaitForSeconds(w.reloadTime);

        // 从背包消耗弹药
        int needed = w.maxMagazineAmmo - _magazineAmmo[idx];
        var consumedLevels = new List<int>();
        int available = 0;

        if (_inventory != null)
        {
            available = _inventory.ConsumeAmmo(w.ammoType, needed, consumedLevels);
        }

        _magazineAmmo[idx] += available;
        // 弹匣不能超过容量上限
        if (_magazineAmmo[idx] > w.maxMagazineAmmo)
            _magazineAmmo[idx] = w.maxMagazineAmmo;

        // 将每发子弹的穿透等级按顺序入队
        foreach (int level in consumedLevels)
            _magazineBulletLevels[idx].Enqueue(level);

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
    /// 同步当前武器弹匣到 SyncVar（仅服务端调用）
    /// </summary>
    void SyncAmmoToClient()
    {
        int idx = _currentWeaponIndex;
        if (idx >= 0 && idx < 2)
        {
            _syncedMagazineAmmo = _magazineAmmo[idx];
        }
    }

    /// <summary>
    /// 由 Inventory 调用：获取指定槽位的弹匣弹药数（用于丢弃武器时保存状态）。
    /// </summary>
    public int GetMagazineAmmo(int slot)
    {
        return (uint)slot < 2 ? _magazineAmmo[slot] : 0;
    }

    /// <summary>
    /// 由 Inventory 调用：设置指定槽位的弹匣状态（用于捡起武器时恢复状态）。
    /// </summary>
    [Server]
    public void SetMagazineState(int slot, int ammo)
    {
        if ((uint)slot >= 2) return;
        _magazineAmmo[slot] = ammo;
        _ammoInitialized[slot] = true;
        // 用武器默认穿透等级填充队列
        WeaponData w = GetWeaponDataFromServerInventory(slot);
        int defaultPen = w != null ? w.penetrationLevel : 1;
        _magazineBulletLevels[slot].Clear();
        for (int i = 0; i < ammo; i++)
            _magazineBulletLevels[slot].Enqueue(defaultPen);
        if (slot == _currentWeaponIndex)
        {
            SyncAmmoToClient();
            // 也更新客户端本地值（Host 模式）
            _syncedMagazineAmmo = ammo;
        }
    }

    /// <summary>
    /// 由 Inventory 调用：给当前武器补充弹药（已废弃，弹药直接留在背包中，换弹时自动消耗）
    /// </summary>
    [Server]
    public void AddAmmo(int amount, int penetrationLevel)
    {
        // 弹药现在直接留在背包中，换弹时自动消耗
        // 该方法保留以保持接口兼容，但不再使用
    }

    void UpdateAmmoUI()
    {
        WeaponData w = CurrentWeapon;

        if (_debugFrameCount % 120 == 0)
            Debug.Log($"[Shooting] UpdateAmmoUI: _magazineText={_magazineText != null} _reserveText={_reserveText != null} _weaponNameText={_weaponNameText != null} weapon={w?.weaponName} _syncedMagazineAmmo={_syncedMagazineAmmo}");

        if (_magazineText != null)
            _magazineText.text = _syncedMagazineAmmo.ToString("D3");
        if (_reserveText != null)
            _reserveText.text = GetReserveAmmoFromInventory().ToString("D3");
        if (_weaponNameText != null)
            _weaponNameText.text = w != null ? w.weaponName : "";
    }

    // ===== SyncVar Hooks =====
    void OnMagazineChanged(int oldVal, int newVal)
    {
        if (_magazineText != null)
            _magazineText.text = newVal.ToString("D3");
    }
}