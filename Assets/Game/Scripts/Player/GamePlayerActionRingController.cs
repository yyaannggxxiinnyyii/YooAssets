using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制跟随玩家的动作环 UI，分别显示右键技能蓄力和闪避冷却进度。
/// </summary>
public sealed class GamePlayerActionRingController : MonoBehaviour
{
    [Header("玩家引用")]
    [Tooltip("玩家控制器。为空时会从父级自动查找。")]
    [SerializeField] private GamePlayerController playerController;
    [Tooltip("World Space Canvas。为空时会从父级自动查找。")]
    [SerializeField] private Canvas worldCanvas;

    [Header("技能蓄力环")]
    [Tooltip("技能蓄力环整体显隐 CanvasGroup，与闪避环分开控制。")]
    [SerializeField] private CanvasGroup skillRingGroup;
    [Tooltip("技能蓄力环填充 Image，需要设置为 Filled。")]
    [SerializeField] private Image skillFillImage;
    [Tooltip("技能蓄力环显示时的最小填充值，避免刚开始蓄力完全不可见。")]
    [SerializeField] private float minimumSkillFill = 0.02f;

    [Header("闪避冷却环")]
    [Tooltip("闪避冷却环整体显隐 CanvasGroup，与技能蓄力环分开控制。")]
    [SerializeField] private CanvasGroup dodgeRingGroup;
    [Tooltip("闪避冷却环填充 Image，需要设置为 Filled。")]
    [SerializeField] private Image dodgeFillImage;
    [Tooltip("闪避冷却完成后是否隐藏闪避环。")]
    [SerializeField] private bool hideDodgeWhenReady = true;
    [Tooltip("闪避冷却环显示时的最小填充值，避免刚开始冷却完全不可见。")]
    [SerializeField] private float minimumDodgeFill = 0.02f;

    private void Awake()
    {
        CacheReferences();
        ConfigureWorldCanvasCamera();
        RefreshSkillCharge(false, 0f);
        RefreshDodgeCooldown();
    }

    private void OnEnable()
    {
        CacheReferences();
        ConfigureWorldCanvasCamera();
        RefreshSkillCharge(false, 0f);
        RefreshDodgeCooldown();
    }

    private void LateUpdate()
    {
        RefreshDodgeCooldown();
    }

    private void OnValidate()
    {
        minimumSkillFill = Mathf.Clamp01(minimumSkillFill);
        minimumDodgeFill = Mathf.Clamp01(minimumDodgeFill);
    }

    /// <summary>
    /// 刷新右键技能蓄力环显示。
    /// </summary>
    /// <param name="visible">是否显示技能蓄力环。</param>
    /// <param name="progress">技能蓄力进度。</param>
    public void RefreshSkillCharge(bool visible, float progress)
    {
        CacheReferences();
        SetGroupVisible(skillRingGroup, visible);
        if (skillFillImage == null)
            return;

        skillFillImage.fillAmount = visible
            ? Mathf.Max(minimumSkillFill, Mathf.Clamp01(progress))
            : 0f;
    }

    /// <summary>
    /// 缓存玩家、Canvas 和进度环引用。
    /// </summary>
    private void CacheReferences()
    {
        if (playerController == null)
            playerController = GetComponentInParent<GamePlayerController>();

        if (worldCanvas == null)
            worldCanvas = GetComponentInParent<Canvas>();
    }

    /// <summary>
    /// 为 World Space Canvas 绑定主相机，避免预制体不能直接保存场景相机引用。
    /// </summary>
    private void ConfigureWorldCanvasCamera()
    {
        if (worldCanvas == null || worldCanvas.renderMode != RenderMode.WorldSpace || worldCanvas.worldCamera != null)
            return;

        worldCanvas.worldCamera = Camera.main;
    }

    /// <summary>
    /// 根据玩家闪避冷却状态刷新闪避环显示。
    /// </summary>
    private void RefreshDodgeCooldown()
    {
        if (playerController == null || dodgeFillImage == null)
            return;

        bool isCoolingDown = playerController.IsRollCoolingDown;
        bool shouldShow = !hideDodgeWhenReady || isCoolingDown;
        SetGroupVisible(dodgeRingGroup, shouldShow);

        float progress = playerController.RollCooldownProgress;
        dodgeFillImage.fillAmount = isCoolingDown
            ? Mathf.Max(minimumDodgeFill, progress)
            : 1f;
    }

    /// <summary>
    /// 设置指定环的显隐，不影响另一个环。
    /// </summary>
    /// <param name="group">需要控制的 CanvasGroup。</param>
    /// <param name="visible">是否显示。</param>
    private void SetGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
