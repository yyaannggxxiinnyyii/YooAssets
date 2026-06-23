using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 管理武器选择面板的武器详情、选项按钮、返回和开始事件。
/// </summary>
public sealed class GameWeaponSelectPanelController : Singleton<GameWeaponSelectPanelController>
{
    [Header("武器选择")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button backButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private TMP_Text[] optionTexts;
    [SerializeField] private string title = "武器选择";
    [SerializeField] private string fallbackName = "训练飞弹";

    private readonly Color _selectedOptionColor = new Color(1f, 0.58f, 0.16f, 1f);
    private readonly Color _fallbackOptionNormalColor = new Color(0.2f, 0.4f, 0.65f, 1f);
    private UnityAction[] _optionButtonActions;
    private Color[] _optionNormalColors;

    /// <summary>
    /// 请求返回角色选择。
    /// </summary>
    public event Action BackRequested;

    /// <summary>
    /// 请求开始游戏。
    /// </summary>
    public event Action StartRequested;

    /// <summary>
    /// 请求选择指定武器索引。
    /// </summary>
    public event Action<int> OptionSelected;

    protected override void Awake()
    {
        base.Awake();
        CacheOptionButtonColors();
        SetText(titleText, title);
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    /// <summary>
    /// 刷新武器选择详情。
    /// </summary>
    /// <param name="weaponOptions">武器选项。</param>
    /// <param name="selectedIndex">当前选择索引。</param>
    public void Refresh(WeaponData[] weaponOptions, int selectedIndex)
    {
        WeaponData weaponData = GetOption(weaponOptions, selectedIndex);
        SetText(nameText, weaponData != null ? weaponData.DisplayName : fallbackName);
        RefreshDetailIcon(weaponData);
        SetText(descriptionText, weaponData != null ? weaponData.Description : "当前未配置武器数据。");
        SetText(statsText, BuildWeaponStatsText(weaponData));
        RefreshOptionLabels(weaponOptions, selectedIndex);
        RebuildDetailLayout();
        StartCoroutine(RebuildDetailLayoutNextFrame());
    }

    /// <summary>
    /// 绑定武器选择按钮。
    /// </summary>
    private void BindButtons()
    {
        if (backButton != null)
            backButton.onClick.AddListener(NotifyBackRequested);

        if (startButton != null)
            startButton.onClick.AddListener(NotifyStartRequested);

        if (optionButtons == null)
            return;

        _optionButtonActions = new UnityAction[optionButtons.Length];
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            _optionButtonActions[i] = () => OptionSelected?.Invoke(index);
            if (optionButtons[i] != null)
                optionButtons[i].onClick.AddListener(_optionButtonActions[i]);
        }
    }

    /// <summary>
    /// 解绑武器选择按钮。
    /// </summary>
    private void UnbindButtons()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(NotifyBackRequested);

        if (startButton != null)
            startButton.onClick.RemoveListener(NotifyStartRequested);

        if (optionButtons == null || _optionButtonActions == null)
            return;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null && i < _optionButtonActions.Length)
                optionButtons[i].onClick.RemoveListener(_optionButtonActions[i]);
        }
    }

    /// <summary>
    /// 缓存选项按钮在场景中配置的默认背景色。
    /// </summary>
    private void CacheOptionButtonColors()
    {
        if (optionButtons == null)
            return;

        _optionNormalColors = new Color[optionButtons.Length];
        Color resolvedNormalColor = FindConfiguredNormalOptionColor();
        for (int i = 0; i < optionButtons.Length; i++)
        {
            Image targetImage = optionButtons[i] != null ? optionButtons[i].targetGraphic as Image : null;
            Color currentColor = targetImage != null ? targetImage.color : resolvedNormalColor;
            _optionNormalColors[i] = IsSameColor(currentColor, _selectedOptionColor)
                ? resolvedNormalColor
                : currentColor;
        }
    }

    /// <summary>
    /// 从场景按钮中提取非选中态底色，避免把初始选中按钮的橙色当作普通色缓存。
    /// </summary>
    /// <returns>可用于未选中状态的按钮底色。</returns>
    private Color FindConfiguredNormalOptionColor()
    {
        if (optionButtons == null)
            return _fallbackOptionNormalColor;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            Image targetImage = optionButtons[i] != null ? optionButtons[i].targetGraphic as Image : null;
            if (targetImage != null && !IsSameColor(targetImage.color, _selectedOptionColor))
                return targetImage.color;
        }

        return _fallbackOptionNormalColor;
    }

    /// <summary>
    /// 使用容差比较颜色，兼容 Unity 序列化和运行时浮点误差。
    /// </summary>
    /// <param name="first">第一个颜色。</param>
    /// <param name="second">第二个颜色。</param>
    /// <returns>两个颜色是否近似一致。</returns>
    private bool IsSameColor(Color first, Color second)
    {
        const float Tolerance = 0.01f;
        return Mathf.Abs(first.r - second.r) <= Tolerance
            && Mathf.Abs(first.g - second.g) <= Tolerance
            && Mathf.Abs(first.b - second.b) <= Tolerance
            && Mathf.Abs(first.a - second.a) <= Tolerance;
    }

    /// <summary>
    /// 刷新武器选项按钮文本。
    /// </summary>
    /// <param name="weaponOptions">武器选项。</param>
    /// <param name="selectedIndex">当前选择索引。</param>
    private void RefreshOptionLabels(WeaponData[] weaponOptions, int selectedIndex)
    {
        if (optionTexts == null)
            return;

        for (int i = 0; i < optionTexts.Length; i++)
        {
            WeaponData option = GetOption(weaponOptions, i);
            string optionName = option != null ? option.DisplayName : i == 0 ? fallbackName : "未解锁";
            SetText(optionTexts[i], optionName);
            RefreshOptionButtonColor(i, i == selectedIndex);
        }
    }

    /// <summary>
    /// 根据选中状态刷新选项按钮背景色。
    /// </summary>
    /// <param name="index">按钮索引。</param>
    /// <param name="selected">是否选中。</param>
    private void RefreshOptionButtonColor(int index, bool selected)
    {
        if (optionButtons == null || index < 0 || index >= optionButtons.Length || optionButtons[index] == null)
            return;

        Image targetImage = optionButtons[index].targetGraphic as Image;
        if (targetImage == null)
            return;

        Color normalColor = _optionNormalColors != null && index < _optionNormalColors.Length
            ? _optionNormalColors[index]
            : targetImage.color;
        targetImage.color = selected ? _selectedOptionColor : normalColor;
    }

    /// <summary>
    /// 获取武器属性说明。
    /// </summary>
    /// <param name="weaponData">武器数据。</param>
    /// <returns>属性说明文本。</returns>
    /// <summary>
    /// 刷新武器详情图标，未手动绑定时自动在详情区域创建。
    /// </summary>
    /// <param name="weaponData">当前选中的武器数据。</param>
    private void RefreshDetailIcon(WeaponData weaponData)
    {
        EnsureDetailIconImage();
        if (detailIconImage == null)
            return;

        Sprite sprite = weaponData != null ? weaponData.Icon : null;
        detailIconImage.sprite = sprite;
        detailIconImage.color = Color.white;
        detailIconImage.preserveAspect = true;
        detailIconImage.raycastTarget = false;
        detailIconImage.gameObject.SetActive(sprite != null);
        RebuildParentLayout(detailIconImage.transform);
    }

    /// <summary>
    /// 确保武器详情图标 Image 可用，缺失时在详情布局中创建默认图标节点。
    /// </summary>
    private void EnsureDetailIconImage()
    {
        if (detailIconImage != null)
            return;

        Transform detailRoot = nameText != null ? nameText.transform.parent : transform;
        if (detailRoot == null)
            return;

        Transform existingIcon = detailRoot.Find("WeaponIcon");
        if (existingIcon != null)
            detailIconImage = existingIcon.GetComponent<Image>();

        if (detailIconImage != null)
            return;

        GameObject iconObject = new GameObject("WeaponIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(detailRoot, false);
        iconObject.transform.SetAsFirstSibling();

        detailIconImage = iconObject.GetComponent<Image>();
        LayoutElement layoutElement = iconObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 132f;
        layoutElement.preferredHeight = 132f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
    }

    /// <summary>
    /// 获取武器属性说明。
    /// </summary>
    /// <param name="weaponData">武器数据。</param>
    /// <returns>属性说明文本。</returns>
    private string BuildWeaponStatsText(WeaponData weaponData)
    {
        if (weaponData == null)
            return "武器攻击力：12\n射速：0.55s\n弹速：9.00\n暴击率：5%\n暴击伤害：180%\n子弹穿透：0\n子弹反弹：0\n弹道数：1\n子弹存在时间：2.20s";

        return
            $"武器攻击力：{weaponData.BaseDamage}\n" +
            $"射速：{weaponData.AttackInterval:F2}s\n" +
            $"弹速：{weaponData.ProjectileSpeed:F2}\n" +
            $"暴击率：{weaponData.CriticalRate:P0}\n" +
            $"暴击伤害：{weaponData.CriticalDamage:P0}\n" +
            $"子弹穿透：{weaponData.ProjectilePierce}\n" +
            $"子弹反弹：{weaponData.ProjectileBounce}\n" +
            $"弹道数：{weaponData.ProjectileCount}\n" +
            $"子弹存在时间：{weaponData.ProjectileLifeTime:F2}s";
    }

    /// <summary>
    /// 获取指定索引武器。
    /// </summary>
    /// <param name="weaponOptions">武器选项。</param>
    /// <param name="index">索引。</param>
    /// <returns>武器数据。</returns>
    private WeaponData GetOption(WeaponData[] weaponOptions, int index)
    {
        if (weaponOptions == null || index < 0 || index >= weaponOptions.Length)
            return null;

        return weaponOptions[index];
    }

    /// <summary>
    /// 通知外部返回角色选择。
    /// </summary>
    private void NotifyBackRequested()
    {
        BackRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部开始游戏。
    /// </summary>
    private void NotifyStartRequested()
    {
        StartRequested?.Invoke();
    }

    /// <summary>
    /// 安全设置 TMP 文本。
    /// </summary>
    /// <param name="targetText">目标文本组件。</param>
    /// <param name="content">显示内容。</param>
    private void SetText(TMP_Text targetText, string content)
    {
        if (targetText == null)
            return;

        targetText.text = content;
        targetText.ForceMeshUpdate();
        RebuildParentLayout(targetText.transform);
    }

    /// <summary>
    /// 刷新文本所在的父级布局。
    /// </summary>
    /// <param name="targetTransform">发生文本变化的节点。</param>
    private void RebuildParentLayout(Transform targetTransform)
    {
        Transform currentTransform = targetTransform;
        while (currentTransform != null)
        {
            if (currentTransform.GetComponent<LayoutGroup>() != null && currentTransform is RectTransform layoutRectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRectTransform);
                return;
            }

            currentTransform = currentTransform.parent;
        }
    }

    /// <summary>
    /// 刷新武器详情区域布局。
    /// </summary>
    private void RebuildDetailLayout()
    {
        RebuildParentLayout(nameText != null ? nameText.transform : null);
    }

    /// <summary>
    /// 等待当前帧文本尺寸更新后再次刷新武器详情布局。
    /// </summary>
    /// <returns>协程迭代器。</returns>
    private IEnumerator RebuildDetailLayoutNextFrame()
    {
        yield return null;
        RebuildDetailLayout();
    }
}
