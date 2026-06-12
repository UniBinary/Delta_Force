using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 单个装备槽 UI。挂在每个槽位 GameObject 上。
/// 物品图标直接渲染在 background Image 上。
/// </summary>
public class EquipmentSlotUI :
    MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [Header("UI 引用")]
    public Image icon;          // 可选，兼容旧配置
    public Image background;
    public TextMeshProUGUI slotLabel;

    [Header("槽位颜色")]
    public Color helmetColor  = new Color(0.35f, 0.35f, 0.60f, 0.7f);
    public Color armorColor   = new Color(0.55f, 0.30f, 0.30f, 0.7f);
    public Color weaponColor  = new Color(0.60f, 0.50f, 0.15f, 0.7f);
    public Color chestColor   = new Color(0.25f, 0.25f, 0.25f, 0.7f);
    public Color backpackColor = new Color(0.20f, 0.35f, 0.20f, 0.7f);

    [HideInInspector] public EquipmentSlotType slotType;
    [HideInInspector] public int slotIndex;
    [HideInInspector] public int itemId = -1;

    private InventoryUI _ui;
    private Color _emptyColor;

    static readonly string[] SlotLabels =
        { "头盔", "护甲", "主武器", "副武器",
          "胸挂1","胸挂2","胸挂3","胸挂4","胸挂5",
          "背包1","背包2","背包3","背包4","背包5" };

    void Awake()
    {
        if (background == null)
            background = GetComponent<Image>();
    }

    public void Init(EquipmentSlotType type, int index, InventoryUI ui)
    {
        slotType = type;
        slotIndex = index;
        _ui = ui;

        Color col = type switch
        {
            EquipmentSlotType.Helmet   => helmetColor,
            EquipmentSlotType.Armor    => armorColor,
            EquipmentSlotType.Weapon   => weaponColor,
            EquipmentSlotType.ChestRig => chestColor,
            EquipmentSlotType.Backpack => backpackColor,
            _ => Color.gray
        };

        if (background != null)
            background.color = col;
        else if (icon != null)
            icon.color = col;

        // 槽位标签
        if (slotLabel != null)
        {
            int labelIdx = type switch
            {
                EquipmentSlotType.Helmet   => 0,
                EquipmentSlotType.Armor    => 1,
                EquipmentSlotType.Weapon   => 2 + index,
                EquipmentSlotType.ChestRig => 4 + index,
                EquipmentSlotType.Backpack => 9 + index,
                _ => -1
            };
            slotLabel.text = labelIdx >= 0 && labelIdx < SlotLabels.Length
                ? SlotLabels[labelIdx] : "";
        }
    }

    public void SetItem(ItemData item, int id)
    {
        itemId = id;
        Sprite sprite = (item != null) ? item.icon : null;

        // 优先用 icon（独立的图标 Image），没有就用 background 的 Sprite
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
        }

        if (background != null)
        {
            if (sprite != null)
            {
                background.sprite = sprite;
                background.color = Color.white;       // 还原白色以正确渲染 Sprite
            }
            else if (icon == null || icon.sprite == null)
            {
                // 无物品时恢复空槽颜色
                background.sprite = null;
                UpdateEmptyColor();
            }
        }
    }

    void UpdateEmptyColor()
    {
        Color col = slotType switch
        {
            EquipmentSlotType.Helmet   => helmetColor,
            EquipmentSlotType.Armor    => armorColor,
            EquipmentSlotType.Weapon   => weaponColor,
            EquipmentSlotType.ChestRig => chestColor,
            EquipmentSlotType.Backpack => backpackColor,
            _ => Color.gray
        };
        if (background != null) background.color = col;
    }

    // ---- 拖拽 ----

    public void OnBeginDrag(PointerEventData e)
    {
        if (itemId < 0) return;
        _ui?.BeginDrag(this);
    }

    public void OnDrag(PointerEventData e) { }

    public void OnEndDrag(PointerEventData e)
    {
        _ui?.EndDrag(this);
    }

    // ---- 点击 ----

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right)
            _ui?.OnSlotRightClick(slotType, slotIndex);
        else if (e.button == PointerEventData.InputButton.Left)
            _ui?.OnSlotLeftClick(slotType, slotIndex);
    }

}
