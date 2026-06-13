using UnityEngine;

/// <summary>
/// 物品类型（决定能放进哪种槽位）
/// </summary>
public enum ItemType
{
    Helmet,     // 仅头盔栏
    Armor,      // 仅护甲栏
    Weapon,     // 仅枪械栏
    Ammo,       // 弹药，仅胸挂/背包
    Item,       // 变卖物，仅背包，无使用效果
    MedKit      // 治疗物，仅胸挂/背包，右键使用消耗耐久1:1恢复血量
}

/// <summary>
/// 一把武器 or 一件装备 or 一个消耗品的定义（ScriptableObject）
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "DeltaForce/Item")]
public class ItemData : ScriptableObject
{
    [Header("基本信息")]
    public string itemName = "新物品";
    public ItemType itemType;
    public Sprite icon;

    [Header("装备属性（头盔/护甲）")]
    public int protectionLevel;     // 护甲/头盔防护等级 (1-6)

    [Header("武器属性（仅 Weapon 类型填）")]
    public WeaponData weaponData;   // 武器专属参数（子弹、射速等）

    [Header("弹药属性（仅 Ammo 类型填）")]
    public int ammoAmount;          // 补充弹药量

    [Header("治疗属性（仅 MedKit 类型填）")]
    public int healAmount;          // 每点耐久恢复的血量
    public int maxDurability;       // 治疗物最大耐久（使用次数），消耗完即消失
}