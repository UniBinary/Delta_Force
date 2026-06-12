using System;
using UnityEngine;

/// <summary>
/// 一把武器的配置数据（不需要挂 GameObject 上）
/// </summary>
[Serializable]
public class WeaponData
{
    [Header("基本信息")]
    public string weaponName = "手枪";

    [Header("子弹参数")]
    public int maxMagazineAmmo = 30;       // 弹匣容量
    public int totalReserveAmmo = 180;     // 总备弹量

    [Header("射击参数")]
    public float fireRate = 0.1f;          // 射速（秒）
    public float bulletForce = 20f;        // 子弹初速

    [Header("换弹")]
    public float reloadTime = 1.5f;

    [Header("Prefab")]
    public GameObject bulletPrefab;        // 子弹 prefab
    public GameObject weaponModel;         // 武器模型（可选，场景中对应的 GameObject）
}
