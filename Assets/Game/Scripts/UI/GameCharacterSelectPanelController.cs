using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 管理角色选择面板的角色详情、选项按钮和确认事件。
/// </summary>
public sealed class GameCharacterSelectPanelController : Singleton<GameCharacterSelectPanelController>
{
    [Header("角色选择")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private TMP_Text[] optionTexts;
    [SerializeField] private string title = "角色选择";
    [SerializeField] private string fallbackName = "见习冒险者";

    private readonly Color _selectedOptionColor = new Color(1f, 0.58f, 0.16f, 1f);
    private readonly Color _fallbackOptionNormalColor = new Color(0.2f, 0.4f, 0.65f, 1f);
    private UnityAction[] _optionButtonActions;
    private Color[] _optionNormalColors;

    /// <summary>
    /// 请求确认当前角色。
    /// </summary>
    public event Action ConfirmRequested;

    /// <summary>
    /// 请求选择指定角色索引。
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
    /// 刷新角色选择详情。
    /// </summary>
    /// <param name="characterOptions">角色选项。</param>
    /// <param name="selectedIndex">当前选择索引。</param>
    public void Refresh(CharacterData[] characterOptions, int selectedIndex)
    {
        CharacterData characterData = GetOption(characterOptions, selectedIndex);
        SetText(nameText, characterData != null ? characterData.DisplayName : fallbackName);
        RefreshDetailIcon(characterData);
        SetText(descriptionText, characterData != null ? "选择一名角色作为本局基础属性。" : "当前未配置角色数据。");
        SetText(statsText, BuildCharacterStatsText(characterData));
        RefreshOptionLabels(characterOptions, selectedIndex);
        RebuildDetailLayout();
        StartCoroutine(RebuildDetailLayoutNextFrame());
    }

    /// <summary>
    /// 绑定角色选择按钮。
    /// </summary>
    private void BindButtons()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(NotifyConfirmRequested);

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
    /// 解绑角色选择按钮。
    /// </summary>
    private void UnbindButtons()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(NotifyConfirmRequested);

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
    /// 刷新角色选项按钮文本。
    /// </summary>
    /// <param name="characterOptions">角色选项。</param>
    /// <param name="selectedIndex">当前选择索引。</param>
    private void RefreshOptionLabels(CharacterData[] characterOptions, int selectedIndex)
    {
        if (optionTexts == null)
            return;

        for (int i = 0; i < optionTexts.Length; i++)
        {
            CharacterData option = GetOption(characterOptions, i);
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
    /// 获取角色属性说明。
    /// </summary>
    /// <param name="characterData">角色数据。</param>
    /// <returns>属性说明文本。</returns>
    /// <summary>
    /// 刷新角色详情图标，未手动绑定时自动在详情区域创建。
    /// </summary>
    /// <param name="characterData">当前选中的角色数据。</param>
    private void RefreshDetailIcon(CharacterData characterData)
    {
        EnsureDetailIconImage();
        if (detailIconImage == null)
            return;

        Sprite sprite = ResolveCharacterIcon(characterData);
        detailIconImage.sprite = sprite;
        detailIconImage.color = Color.white;
        detailIconImage.preserveAspect = true;
        detailIconImage.raycastTarget = false;
        detailIconImage.gameObject.SetActive(sprite != null);
        RebuildParentLayout(detailIconImage.transform);
    }

    /// <summary>
    /// 获取角色详情图标，优先使用角色头像，缺失时使用角色预制体中的首个 Sprite。
    /// </summary>
    /// <param name="characterData">角色数据。</param>
    /// <returns>可用于 UI 展示的角色图标。</returns>
    private Sprite ResolveCharacterIcon(CharacterData characterData)
    {
        if (characterData == null)
            return null;

        if (characterData.Portrait != null)
            return characterData.Portrait;

        if (characterData.PlayerPrefab == null)
            return null;

        SpriteRenderer spriteRenderer = characterData.PlayerPrefab.GetComponentInChildren<SpriteRenderer>(true);
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    /// <summary>
    /// 确保角色详情图标 Image 可用，缺失时在详情布局中创建默认图标节点。
    /// </summary>
    private void EnsureDetailIconImage()
    {
        if (detailIconImage != null)
            return;

        Transform detailRoot = nameText != null ? nameText.transform.parent : transform;
        if (detailRoot == null)
            return;

        Transform existingIcon = detailRoot.Find("CharacterIcon");
        if (existingIcon != null)
            detailIconImage = existingIcon.GetComponent<Image>();

        if (detailIconImage != null)
            return;

        GameObject iconObject = new GameObject("CharacterIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
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
    /// 获取角色属性说明。
    /// </summary>
    /// <param name="characterData">角色数据。</param>
    /// <returns>属性说明文本。</returns>
    private string BuildCharacterStatsText(CharacterData characterData)
    {
        if (characterData == null)
            return "血量：5 心\n基础攻击力：12\n攻击力倍率：100%\n移速：4.50\n经验倍率：100%\n闪避无敌：0.20s";

        return
            $"血量：{HealthDisplayUtility.FormatHeartCount(characterData.MaxHealth)} 心\n" +
            $"基础攻击力：{characterData.AttackDamage}\n" +
            $"攻击力倍率：{characterData.AttackDamageMultiplier:P0}\n" +
            $"移速：{characterData.MoveSpeed:F2}\n" +
            $"经验倍率：{characterData.ExperienceMultiplier:P0}\n" +
            $"闪避无敌：{characterData.RollInvincibleDuration:F2}s";
    }

    /// <summary>
    /// 获取指定索引角色。
    /// </summary>
    /// <param name="characterOptions">角色选项。</param>
    /// <param name="index">索引。</param>
    /// <returns>角色数据。</returns>
    private CharacterData GetOption(CharacterData[] characterOptions, int index)
    {
        if (characterOptions == null || index < 0 || index >= characterOptions.Length)
            return null;

        return characterOptions[index];
    }

    /// <summary>
    /// 通知外部确认当前角色。
    /// </summary>
    private void NotifyConfirmRequested()
    {
        ConfirmRequested?.Invoke();
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
    /// 刷新角色详情区域布局。
    /// </summary>
    private void RebuildDetailLayout()
    {
        RebuildParentLayout(nameText != null ? nameText.transform : null);
    }

    /// <summary>
    /// 等待当前帧文本尺寸更新后再次刷新角色详情布局。
    /// </summary>
    /// <returns>协程迭代器。</returns>
    private IEnumerator RebuildDetailLayoutNextFrame()
    {
        yield return null;
        RebuildDetailLayout();
    }
}
