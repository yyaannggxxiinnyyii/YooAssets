using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理游戏内自定义指针的位置跟随，并分别触发鼠标点击和实际开火反馈动画。
/// </summary>
public sealed class GameCursorController : MonoBehaviour
{
    [Header("流程引用")]
    [Tooltip("游戏流程状态管理器，用于区分战斗开火输入和普通鼠标点击反馈。")]
    [SerializeField] private GameStateManager gameStateManager;

    [Header("指针引用")]
    [Tooltip("用于跟随鼠标位置的指针根节点，未配置时默认使用当前物体的 RectTransform。")]
    [SerializeField] private RectTransform cursorRoot;
    [Tooltip("播放指针点击动画的 Animator，未配置时默认从当前物体子节点查找。")]
    [SerializeField] private Animator cursorAnimator;
    [Tooltip("Canvas 使用 Screen Space - Camera 或 World Space 时需要配置的 UI 相机。")]
    [SerializeField] private Camera uiCamera;

    [Header("动画参数")]
    [Tooltip("鼠标左键或右键点击时触发的准星反馈 Animator Trigger 参数名。")]
    [SerializeField] private string cursorPulseTriggerName = "CursorPulse";
    [Tooltip("实际成功开火时触发的准星开火 Animator Trigger 参数名。")]
    [SerializeField] private string firePulseTriggerName = "Fire";
    [Tooltip("开火动画速度使用的 Animator Float 参数名，1 秒标准动画会按 1 / 当前开火间隔写入该值。")]
    [SerializeField] private string fireSpeedFloatName = "FireSpeed";
    [Tooltip("换弹中使用的 Animator Bool 参数名。")]
    [SerializeField] private string reloadingBoolName = "Reloading";
    [Tooltip("换弹完成时触发的 Animator Trigger 参数名。")]
    [SerializeField] private string reloadCompleteTriggerName = "ReloadComplete";
    [Tooltip("换弹动画速度使用的 Animator Float 参数名，1 秒标准动画会按 1 / 换弹时长写入该值。")]
    [SerializeField] private string reloadSpeedFloatName = "ReloadSpeed";

    [Header("显示设置")]
    [Tooltip("启用脚本时是否隐藏系统鼠标指针。")]
    [SerializeField] private bool hideSystemCursorOnEnable = true;
    [Tooltip("Playing 状态下是否禁止左键触发点击反馈，让实际开火动画独占左键反馈。")]
    [SerializeField] private bool suppressLeftClickPulseWhilePlaying = true;

    [Header("弹药显示")]
    [Tooltip("用于显示当前弹夹比例的菱形填充 Image，Image Type 需要设置为 Filled。")]
    [SerializeField] private Image ammoFillImage;
    [Tooltip("弹药充足时菱形填充使用的颜色。")]
    [SerializeField] private Color fullAmmoColor = new Color(1f, 0.95f, 0.25f, 0.95f);
    [Tooltip("低弹药时菱形填充使用的颜色。")]
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.58f, 0.22f, 0.95f);
    [Tooltip("弹药比例低于该值时更明显地偏向低弹药颜色。")]
    [SerializeField] private float lowAmmoThreshold = 0.3f;

    private int _cursorPulseTriggerHash;
    private int _firePulseTriggerHash;
    private int _fireSpeedFloatHash;
    private int _reloadingBoolHash;
    private int _reloadCompleteTriggerHash;
    private int _reloadSpeedFloatHash;
    private float _currentAmmoRate = 1f;
    private float _reloadDuration = 1f;
    private float _reloadTimer;
    private int _lastPulseFrame = -1;
    private bool _wasReloading;
    private bool _isReloading;
    private bool _isWeaponResourceProgressActive;

    private void Awake()
    {
        ResolveReferences();
        CacheAnimatorHashes();
        ResetWeaponResourceFill();
    }

    private void OnEnable()
    {
        if (hideSystemCursorOnEnable)
            Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (hideSystemCursorOnEnable)
            Cursor.visible = true;
    }

    private void Update()
    {
        FollowMousePosition();
        TickClickInput();
    }

    private void LateUpdate()
    {
        ApplyAmmoFill();
    }

    /// <summary>
    /// 外部切换指针显示状态时同步控制自定义指针和系统指针。
    /// </summary>
    /// <param name="visible">自定义指针是否显示。</param>
    public void SetCursorVisible(bool visible)
    {
        if (cursorRoot != null)
            cursorRoot.gameObject.SetActive(visible);

        if (hideSystemCursorOnEnable)
            Cursor.visible = !visible;
    }

    /// <summary>
    /// 切换准星上的武器资源进度模式，非战斗状态保留满进度填充用于移动和交互指向。
    /// </summary>
    /// <param name="visible">是否显示准星资源进度。</param>
    public void SetWeaponResourceVisible(bool visible)
    {
        _isWeaponResourceProgressActive = visible;

        if (!visible)
            ResetWeaponResourceFill();

        if (ammoFillImage != null)
            ammoFillImage.gameObject.SetActive(true);
    }

    /// <summary>
    /// 播放一次鼠标点击准星反馈动画。
    /// </summary>
    public void PlayCursorPulse()
    {
        if (_lastPulseFrame == Time.frameCount)
            return;

        _lastPulseFrame = Time.frameCount;
        PlayTrigger(_cursorPulseTriggerHash);
    }

    /// <summary>
    /// 播放一次实际开火准星反馈动画，并按当前开火间隔调整播放速度。
    /// </summary>
    /// <param name="attackInterval">当前实时开火间隔。</param>
    public void PlayFirePulse(float attackInterval)
    {
        if (cursorAnimator != null && _fireSpeedFloatHash != 0)
            cursorAnimator.SetFloat(_fireSpeedFloatHash, 1f / Mathf.Max(0.01f, attackInterval));

        PlayTrigger(_firePulseTriggerHash);
    }

    /// <summary>
    /// 刷新指针菱形填充的弹夹显示。
    /// </summary>
    /// <param name="currentAmmo">当前弹夹内弹药数量。</param>
    /// <param name="maxAmmo">当前弹夹最大弹药数量。</param>
    public void RefreshAmmo(int currentAmmo, int maxAmmo)
    {
        if (ammoFillImage == null)
            return;

        _currentAmmoRate = maxAmmo > 0 ? Mathf.Clamp01((float)currentAmmo / maxAmmo) : 1f;
    }

    /// <summary>
    /// 将当前武器状态写入菱形填充，换弹中显示换弹进度，平时显示弹药比例。
    /// </summary>
    private void ApplyAmmoFill()
    {
        if (ammoFillImage == null)
            return;

        float displayRate = GetDisplayFillRate();
        ammoFillImage.fillAmount = displayRate;
        ammoFillImage.color = GetAmmoFillColor(displayRate);
    }

    /// <summary>
    /// 刷新指针换弹动画状态，并在换弹结束时触发完成动画。
    /// </summary>
    /// <param name="isReloading">当前是否正在换弹。</param>
    /// <param name="reloadDuration">当前武器换弹总时长。</param>
    public void RefreshReloadState(bool isReloading, float reloadDuration)
    {
        if (!_isWeaponResourceProgressActive)
        {
            ResetWeaponResourceFill();
            return;
        }

        float safeReloadDuration = Mathf.Max(0.01f, reloadDuration);
        if (!_wasReloading && isReloading)
        {
            _reloadTimer = 0f;
            _reloadDuration = safeReloadDuration;
        }
        else if (isReloading)
        {
            _reloadDuration = safeReloadDuration;
        }

        _isReloading = isReloading;

        if (cursorAnimator == null)
        {
            _wasReloading = isReloading;
            return;
        }

        if (isReloading && _reloadSpeedFloatHash != 0)
            cursorAnimator.SetFloat(_reloadSpeedFloatHash, 1f / safeReloadDuration);

        if (_reloadingBoolHash != 0)
            cursorAnimator.SetBool(_reloadingBoolHash, isReloading);

        if (_wasReloading && !isReloading)
            PlayTrigger(_reloadCompleteTriggerHash);

        _wasReloading = isReloading;
    }

    /// <summary>
    /// 根据当前模式获取菱形填充比例，换弹中使用本地计时器计算进度。
    /// </summary>
    /// <returns>当前应显示的填充比例。</returns>
    private float GetDisplayFillRate()
    {
        if (!_isWeaponResourceProgressActive)
            return 1f;

        if (!_isReloading)
            return _currentAmmoRate;

        _reloadTimer = Mathf.Min(_reloadDuration, _reloadTimer + Time.deltaTime);
        return Mathf.Clamp01(_reloadTimer / Mathf.Max(0.01f, _reloadDuration));
    }

    /// <summary>
    /// 从当前物体补齐未手动配置的指针引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (cursorRoot == null)
            cursorRoot = transform as RectTransform;

        if (cursorAnimator == null)
            cursorAnimator = GetComponentInChildren<Animator>(true);
    }

    /// <summary>
    /// 缓存 Animator Trigger 名称哈希，减少点击输入时的字符串查找。
    /// </summary>
    private void CacheAnimatorHashes()
    {
        _cursorPulseTriggerHash = GetTriggerHash(cursorPulseTriggerName);
        _firePulseTriggerHash = GetTriggerHash(firePulseTriggerName);
        _fireSpeedFloatHash = GetAnimatorParameterHash(fireSpeedFloatName);
        _reloadingBoolHash = GetTriggerHash(reloadingBoolName);
        _reloadCompleteTriggerHash = GetTriggerHash(reloadCompleteTriggerName);
        _reloadSpeedFloatHash = GetAnimatorParameterHash(reloadSpeedFloatName);
    }

    /// <summary>
    /// 将 Animator Trigger 参数名转换为哈希，空参数名返回无效哈希。
    /// </summary>
    /// <param name="triggerName">Animator Trigger 参数名。</param>
    /// <returns>Trigger 参数哈希，未配置时返回 0。</returns>
    private int GetTriggerHash(string triggerName)
    {
        return GetAnimatorParameterHash(triggerName);
    }

    /// <summary>
    /// 将 Animator 参数名转换为哈希，空参数名返回无效哈希。
    /// </summary>
    /// <param name="parameterName">Animator 参数名。</param>
    /// <returns>Animator 参数哈希，未配置时返回 0。</returns>
    private int GetAnimatorParameterHash(string parameterName)
    {
        return string.IsNullOrEmpty(parameterName) ? 0 : Animator.StringToHash(parameterName);
    }

    /// <summary>
    /// 根据弹药比例计算菱形填充颜色，低弹量时轻微转向橙色。
    /// </summary>
    /// <param name="ammoRate">当前弹药比例，范围 0 到 1。</param>
    /// <returns>弹药填充颜色。</returns>
    private Color GetAmmoFillColor(float ammoRate)
    {
        float safeThreshold = Mathf.Clamp01(lowAmmoThreshold);
        if (safeThreshold <= 0f)
            return fullAmmoColor;

        float colorRate = Mathf.Clamp01(ammoRate / safeThreshold);
        return Color.Lerp(lowAmmoColor, fullAmmoColor, colorRate);
    }

    /// <summary>
    /// 将指针武器资源进度恢复为满状态，用于非战斗界面避免保留上一帧弹夹或换弹进度。
    /// </summary>
    private void ResetWeaponResourceFill()
    {
        _currentAmmoRate = 1f;
        _reloadTimer = 0f;
        _isReloading = false;
        _wasReloading = false;

        if (cursorAnimator != null && _reloadingBoolHash != 0)
            cursorAnimator.SetBool(_reloadingBoolHash, false);

        if (ammoFillImage == null)
            return;

        ammoFillImage.fillAmount = 1f;
        ammoFillImage.color = GetAmmoFillColor(1f);
    }

    /// <summary>
    /// 将指针根节点移动到当前鼠标屏幕位置。
    /// </summary>
    private void FollowMousePosition()
    {
        if (cursorRoot == null)
            return;

        RectTransform parentRect = cursorRoot.parent as RectTransform;
        if (parentRect == null)
        {
            cursorRoot.position = Input.mousePosition;
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, Input.mousePosition, uiCamera, out Vector2 localPoint))
            return;

        cursorRoot.anchoredPosition = localPoint;
    }

    /// <summary>
    /// 检测鼠标左右键按下输入，并触发对应动画。
    /// </summary>
    private void TickClickInput()
    {
        if (Input.GetMouseButtonDown(0) && ShouldPlayLeftClickPulse())
            PlayCursorPulse();

        if (Input.GetMouseButtonDown(1))
            PlayCursorPulse();
    }

    /// <summary>
    /// 判断左键按下是否需要播放普通点击反馈，战斗中左键由开火反馈接管。
    /// </summary>
    /// <returns>需要播放左键点击反馈时返回 true。</returns>
    private bool ShouldPlayLeftClickPulse()
    {
        if (!suppressLeftClickPulseWhilePlaying)
            return true;

        return gameStateManager == null || gameStateManager.CurrentState != GameStateManager.GameState.Playing;
    }

    /// <summary>
    /// 安全触发指针 Animator 参数，避免未配置 Animator 时抛出异常。
    /// </summary>
    /// <param name="triggerHash">Animator Trigger 参数哈希。</param>
    private void PlayTrigger(int triggerHash)
    {
        if (cursorAnimator == null || triggerHash == 0)
            return;

        cursorAnimator.SetTrigger(triggerHash);
    }
}
