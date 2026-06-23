using System;
using UnityEngine;

/// <summary>
/// 管理游戏主流程状态，并提供后续界面与战斗系统订阅的状态变化事件。
/// </summary>
public sealed class GameStateManager : MonoBehaviour
{
    /// <summary>
    /// 游戏运行状态。
    /// </summary>
    public enum GameState
    {
        Title,
        CharacterSelect,
        WeaponSelect,
        Playing,
        Paused,
        LevelUp,
        WeaponUpgrade,
        Shop,
        GameOver,
        Victory
    }

    [Header("流程配置")]
    [SerializeField] private GameState initialState = GameState.Title;

    /// <summary>
    /// 当前游戏状态。
    /// </summary>
    public GameState CurrentState { get; private set; }

    /// <summary>
    /// 状态发生变化时触发。
    /// </summary>
    public event Action<GameState, GameState> StateChanged;

    private void Awake()
    {
        CurrentState = initialState;
    }

    /// <summary>
    /// 切换到指定游戏状态，重复切换会被忽略。
    /// </summary>
    /// <param name="nextState">目标游戏状态。</param>
    public void ChangeState(GameState nextState)
    {
        if (CurrentState == nextState)
            return;

        GameState previousState = CurrentState;
        CurrentState = nextState;
        Debug.Log($"[GameState] {previousState} -> {CurrentState}");
        StateChanged?.Invoke(previousState, CurrentState);
    }

    /// <summary>
    /// 从标题界面进入角色选择界面。
    /// </summary>
    public void EnterCharacterSelect()
    {
        ChangeState(GameState.CharacterSelect);
    }

    /// <summary>
    /// 从角色选择进入武器选择界面。
    /// </summary>
    public void EnterWeaponSelect()
    {
        ChangeState(GameState.WeaponSelect);
    }

    /// <summary>
    /// 开始战斗流程。
    /// </summary>
    public void StartPlaying()
    {
        ChangeState(GameState.Playing);
    }

    /// <summary>
    /// 进入暂停状态。
    /// </summary>
    public void Pause()
    {
        ChangeState(GameState.Paused);
    }

    /// <summary>
    /// 从暂停恢复战斗。
    /// </summary>
    public void Resume()
    {
        ChangeState(GameState.Playing);
    }

    /// <summary>
    /// 进入升级强化选择状态。
    /// </summary>
    public void EnterLevelUp()
    {
        ChangeState(GameState.LevelUp);
    }

    /// <summary>
    /// 进入武器强化选择状态。
    /// </summary>
    public void EnterWeaponUpgrade()
    {
        ChangeState(GameState.WeaponUpgrade);
    }

    /// <summary>
    /// 楼层完成后进入商店。
    /// </summary>
    public void EnterShop()
    {
        ChangeState(GameState.Shop);
    }

    /// <summary>
    /// 玩家失败后进入战败结算。
    /// </summary>
    public void EnterGameOver()
    {
        ChangeState(GameState.GameOver);
    }

    /// <summary>
    /// 达成最终目标后进入通关结算。
    /// </summary>
    public void EnterVictory()
    {
        ChangeState(GameState.Victory);
    }

    /// <summary>
    /// 返回标题界面。
    /// </summary>
    public void ReturnToTitle()
    {
        ChangeState(GameState.Title);
    }
}

