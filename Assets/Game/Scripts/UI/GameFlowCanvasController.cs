using UnityEngine;

/// <summary>
/// 作为主流程 UI 门面，负责面板事件转发、角色武器选择状态和流程状态切换。
/// </summary>
public sealed class GameFlowCanvasController : MonoBehaviour
{
    [Header("流程引用")]
    [Tooltip("游戏流程状态管理器，用于旧单场景流程切换。")]
    [SerializeField] private GameStateManager gameStateManager;

    [Header("面板控制器")]
    [Tooltip("标题面板控制器。")]
    [SerializeField] private GameTitlePanelController titlePanelController;
    [Tooltip("角色选择面板控制器。")]
    [SerializeField] private GameCharacterSelectPanelController characterSelectPanelController;
    [Tooltip("武器选择面板控制器。")]
    [SerializeField] private GameWeaponSelectPanelController weaponSelectPanelController;

    [Header("角色选择")]
    [Tooltip("可供玩家选择的角色数据列表。")]
    [SerializeField] private CharacterData[] characterOptions;

    [Header("武器选择")]
    [Tooltip("可供玩家选择的武器数据列表。")]
    [SerializeField] private WeaponData[] weaponOptions;

    private int _selectedCharacterIndex;
    private int _selectedWeaponIndex;
    private bool _panelEventsBound;

    /// <summary>
    /// 当前选择的角色数据。
    /// </summary>
    public CharacterData SelectedCharacterData => GetSelectedCharacterData();

    /// <summary>
    /// 当前选择的武器数据。
    /// </summary>
    public WeaponData SelectedWeaponData => GetSelectedWeaponData();

    private void Awake()
    {
        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (gameStateManager == null)
            Debug.LogError("[GameUI] 缺少 GameStateManager，主流程 UI 无法工作。");

        ResolvePanelControllers();
        BindPanelEvents();
        ClampSelectionIndexes();
        RefreshText();
    }

    private void OnDestroy()
    {
        UnbindPanelEvents();
    }

    private void Update()
    {
        if (gameStateManager == null)
            return;

        if (gameStateManager.CurrentState != GameStateManager.GameState.Title)
            return;

        if (!Input.anyKeyDown)
            return;

        EnterCharacterSelect();
    }

    /// <summary>
    /// 刷新标题、角色选择和武器选择面板。
    /// </summary>
    public void RefreshText()
    {
        if (titlePanelController != null)
            titlePanelController.Refresh();

        RefreshCharacterSelection();
        RefreshWeaponSelection();
    }

    /// <summary>
    /// 从标题页进入角色选择页。
    /// </summary>
    public void EnterCharacterSelect()
    {
        if (gameStateManager == null)
            return;

        gameStateManager.EnterCharacterSelect();
    }

    /// <summary>
    /// 角色确认后进入武器选择页。
    /// </summary>
    public void ConfirmCharacter()
    {
        if (gameStateManager == null)
            return;

        gameStateManager.EnterWeaponSelect();
    }

    /// <summary>
    /// 从武器选择返回角色选择。
    /// </summary>
    public void BackToCharacterSelect()
    {
        if (gameStateManager == null)
            return;

        gameStateManager.EnterCharacterSelect();
    }

    /// <summary>
    /// 从武器选择页开始游戏。
    /// </summary>
    public void StartGame()
    {
        if (gameStateManager == null)
            return;

        GameRunStartContext.Configure(SelectedCharacterData, SelectedWeaponData);
        if (!GameRunStartContext.HasRunStartData)
            return;

        gameStateManager.StartPlaying();
    }

    /// <summary>
    /// 选择指定索引的角色。
    /// </summary>
    /// <param name="index">角色索引。</param>
    public void SelectCharacter(int index)
    {
        if (characterOptions == null || index < 0 || index >= characterOptions.Length)
            return;

        _selectedCharacterIndex = index;
        RefreshCharacterSelection();
    }

    /// <summary>
    /// 选择指定索引的武器。
    /// </summary>
    /// <param name="index">武器索引。</param>
    public void SelectWeapon(int index)
    {
        if (weaponOptions == null || index < 0 || index >= weaponOptions.Length)
            return;

        _selectedWeaponIndex = index;
        RefreshWeaponSelection();
    }

    /// <summary>
    /// 查找挂在各 UI 面板上的子控制器。
    /// </summary>
    private void ResolvePanelControllers()
    {
        if (titlePanelController == null)
            titlePanelController = GetComponentInChildren<GameTitlePanelController>(true);

        if (characterSelectPanelController == null)
            characterSelectPanelController = GetComponentInChildren<GameCharacterSelectPanelController>(true);

        if (weaponSelectPanelController == null)
            weaponSelectPanelController = GetComponentInChildren<GameWeaponSelectPanelController>(true);
    }

    /// <summary>
    /// 绑定子面板事件，由门面继续处理流程切换和选择状态。
    /// </summary>
    private void BindPanelEvents()
    {
        if (_panelEventsBound)
            return;

        if (titlePanelController != null)
            titlePanelController.ContinueRequested += EnterCharacterSelect;

        if (characterSelectPanelController != null)
        {
            characterSelectPanelController.ConfirmRequested += ConfirmCharacter;
            characterSelectPanelController.OptionSelected += SelectCharacter;
        }

        if (weaponSelectPanelController != null)
        {
            weaponSelectPanelController.BackRequested += BackToCharacterSelect;
            weaponSelectPanelController.StartRequested += StartGame;
            weaponSelectPanelController.OptionSelected += SelectWeapon;
        }

        _panelEventsBound = true;
    }

    /// <summary>
    /// 解绑子面板事件，避免对象销毁后残留委托。
    /// </summary>
    private void UnbindPanelEvents()
    {
        if (!_panelEventsBound)
            return;

        if (titlePanelController != null)
            titlePanelController.ContinueRequested -= EnterCharacterSelect;

        if (characterSelectPanelController != null)
        {
            characterSelectPanelController.ConfirmRequested -= ConfirmCharacter;
            characterSelectPanelController.OptionSelected -= SelectCharacter;
        }

        if (weaponSelectPanelController != null)
        {
            weaponSelectPanelController.BackRequested -= BackToCharacterSelect;
            weaponSelectPanelController.StartRequested -= StartGame;
            weaponSelectPanelController.OptionSelected -= SelectWeapon;
        }

        _panelEventsBound = false;
    }

    /// <summary>
    /// 刷新角色选择面板。
    /// </summary>
    private void RefreshCharacterSelection()
    {
        if (characterSelectPanelController != null)
            characterSelectPanelController.Refresh(characterOptions, _selectedCharacterIndex);
    }

    /// <summary>
    /// 刷新武器选择面板。
    /// </summary>
    private void RefreshWeaponSelection()
    {
        if (weaponSelectPanelController != null)
            weaponSelectPanelController.Refresh(weaponOptions, _selectedWeaponIndex);
    }

    /// <summary>
    /// 获取当前选择角色。
    /// </summary>
    /// <returns>角色数据。</returns>
    private CharacterData GetSelectedCharacterData()
    {
        if (characterOptions == null || characterOptions.Length <= 0)
            return null;

        int index = Mathf.Clamp(_selectedCharacterIndex, 0, characterOptions.Length - 1);
        return characterOptions[index];
    }

    /// <summary>
    /// 获取当前选择武器。
    /// </summary>
    /// <returns>武器数据。</returns>
    private WeaponData GetSelectedWeaponData()
    {
        if (weaponOptions == null || weaponOptions.Length <= 0)
            return null;

        int index = Mathf.Clamp(_selectedWeaponIndex, 0, weaponOptions.Length - 1);
        return weaponOptions[index];
    }

    /// <summary>
    /// 修正选择索引到可用范围内。
    /// </summary>
    private void ClampSelectionIndexes()
    {
        if (characterOptions != null && characterOptions.Length > 0)
            _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, characterOptions.Length - 1);

        if (weaponOptions != null && weaponOptions.Length > 0)
            _selectedWeaponIndex = Mathf.Clamp(_selectedWeaponIndex, 0, weaponOptions.Length - 1);
    }
}
