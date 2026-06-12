using UnityEngine;

/// <summary>
/// 物品类型（决定能放进哪种槽位）
/// </summary>
public enum ItemType
{
    Helmet,     // 仅头盔栏
    Armor,      // 仅护甲栏
    Weapon,     // 仅枪械栏
    Consumable, // 医疗包/弹药等，仅胸挂/背包
    Item        // 变卖物，仅胸挂/背包，无使用效果
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

    [Header("装备属性")]
    public int armorValue;      // 护甲值加成
    public int damageBonus;     // 伤害加成

    [Header("消耗品")]
    public int healAmount;      // 治疗量（>0 表示可回血）
    public int ammoAmount;      // 补充弹药量（>0 表示可补弹药）
}
